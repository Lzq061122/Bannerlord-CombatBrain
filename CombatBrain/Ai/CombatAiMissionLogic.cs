using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain;
using CombatBrain.Bench;
using CombatBrain.Combat;
using CombatBrain.Events;
using CombatBrain.Intent;
using CombatBrain.Lod;
using CombatBrain.Perception;

namespace CombatBrain.Ai
{
    // 战斗AI系统本体 —— 一号项目的使命逻辑胶水层。
    // 把游戏对象喂给各子系统，驱动感知管线 / LOD / 意图引擎 / 预测格挡 / 基准。
    public sealed class CombatAiMissionLogic : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private sealed class TeamState
        {
            public Team Team;
            public PerceptionPipeline Perception;
            public readonly KnowledgeView View = new KnowledgeView(25f, 0.08f, 0.15f);
            public readonly IntentEngine Engine = new IntentEngine(TacticManual.BuildMatrix());
            public SituationVector LastVector;
            public IntentType LastIntent = IntentType.Defend;
            public float InitialCount = -1f;
            public readonly Queue<float> EnemyCountHistory = new Queue<float>();
            public Vec2 Center = Vec2.Zero;   // 上次非空编队中心(无一兵时保持)
            public float Spread = 0f;         // 上次编队离散度(无一兵时保持)
        }

        private bool _initialized;
        private float _elapsed;

        private readonly Dictionary<int, TeamState> _states = new Dictionary<int, TeamState>();
        private readonly Dictionary<int, LodLevel> _agentLod = new Dictionary<int, LodLevel>();
        private readonly Dictionary<int, FighterProfile> _profiles = new Dictionary<int, FighterProfile>();
        private readonly Dictionary<int, AttackStats> _attackStats = new Dictionary<int, AttackStats>();

        private readonly CombatEventBus _bus = new CombatEventBus();
        private ReplayRecorder _replay;
        private readonly BattleBenchmark _bench = new BattleBenchmark();

        private float _lodTimer;
        private float _intentTimer;
        private float _blockTimer;

        // F6 走位/绕侧胶水：0.2s 低频周期 + LOD 剧场效应 + 跳帧聚合（见 MovementGlue.cs）
        private readonly MovementGlue _movement = new MovementGlue();

        // F7 求生/重整胶水：0.3s 低频周期，濒死靠拢友军 + 编队重伤重整信号（见 SurvivalGlue.cs）
        private readonly SurvivalGlue _survival = new SurvivalGlue();

        // F9 远程目标/弹药取舍胶水：0.3s 低频周期，远程选目标 + 弹药告罄回退（见 RangedGlue.cs）
        private readonly RangedGlue _ranged = new RangedGlue();

        // F8 骑兵波次胶水：0.5s 编队级周期，冲锋→回撤→重整→再冲（见 CavalryGlue.cs）
        private readonly CavalryGlue _cavalry = new CavalryGlue();

        // F11 士气传导/分阶段撤退胶水：0.5s 编队级周期，邻队崩盘传染 + 请求→批准→脱离（见 MoraleGlue.cs）
        private readonly MoraleGlue _morale = new MoraleGlue();

        // F10 反制矩阵/套路记忆胶水：纯事件驱动（IntentSwitched 反制换招 + 跨战役套路落盘，见 ExperienceGlue.cs）
        private readonly ExperienceGlue _experience = new ExperienceGlue();

        // F12 决策抖动横切：格挡决策点（L3）独立实例，避免与走位共用延迟计时
        private readonly DecisionJitterGlue _jitterBlock = new DecisionJitterGlue();

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Init();
        }

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;

            _replay = new ReplayRecorder(_bus);

            var mission = Mission;
            if (mission == null) return;

            foreach (Team team in mission.Teams)
            {
                if (team == null) continue;
                var state = new TeamState { Team = team };
                state.Perception = new PerceptionPipeline(state.View, 60f, 1.0f);
                _states[team.TeamIndex] = state;
            }

            // F10 事件驱动接入：订阅意图切换事件 + 载入跨战役套路记忆（一次 IO）
            _experience.Attach(_bus,
                mission.PlayerTeam != null ? mission.PlayerTeam.TeamIndex : -1,
                GetEnemyAliveCount);

            CombatLog.Log("CombatBrain 战斗AI已初始化, teams=" + _states.Count);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            _bench.BeginFrame();

            if (!_initialized) Init();

            var mission = Mission;
            if (mission == null || !mission.IsLoadingFinished)
            {
                _bench.EndFrame(dt, 0);
                return;
            }

            _elapsed += dt;
            int agentCount = mission.Agents.Count;

            _bench.BeginSection("perception");
            TickPerception(mission);
            _bench.EndSection("perception");

            _bench.BeginSection("lod");
            TickLod(mission, dt);
            _bench.EndSection("lod");

            _bench.BeginSection("intent");
            TickIntent(mission, dt);
            _bench.EndSection("intent");

            _bench.BeginSection("movement");
            TickMovement(mission, dt);
            _bench.EndSection("movement");

            _bench.BeginSection("survival");
            TickSurvival(mission, dt);
            _bench.EndSection("survival");

            _bench.BeginSection("ranged");
            TickRanged(mission, dt);
            _bench.EndSection("ranged");

            _bench.BeginSection("cavalry");
            TickCavalry(mission, dt);
            _bench.EndSection("cavalry");

            _bench.BeginSection("morale");
            TickMorale(mission, dt);
            _bench.EndSection("morale");

            _bench.BeginSection("block");
            TickBlock(mission, dt);
            _bench.EndSection("block");

            _bench.EndFrame(dt, agentCount);
        }

        // ---------- F1 感知 ----------

        private void TickPerception(Mission mission)
        {
            float now = _elapsed;
            foreach (TeamState state in _states.Values)
            {
                state.Perception.Update(now, GetTeamCenter(state.Team), mission.Agents);
                state.View.Tick(now);
            }
        }

        // ---------- F3 行为 LOD ----------

        private void TickLod(Mission mission, float dt)
        {
            _lodTimer -= dt;
            if (_lodTimer > 0f) return;
            _lodTimer = 0.5f;

            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;

            var enemyCenters = new Dictionary<int, List<Vec2>>();
            foreach (TeamState state in _states.Values)
            {
                enemyCenters[state.Team.TeamIndex] = CollectEnemyCenters(state.Team);
            }

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive()) continue;
                Team t = agent.Team;
                if (t == null) continue;

                List<Vec2> enemies;
                if (!enemyCenters.TryGetValue(t.TeamIndex, out enemies)) enemies = null;

                float dp = VecMath.Dist(agent.Position.x, agent.Position.y, playerCenter.x, playerCenter.y);
                float de = NearestDistance(agent.Position.x, agent.Position.y, enemies);
                _agentLod[agent.Index] = BehaviorLodScheduler.Evaluate(dp, de);
            }
        }

        // ---------- F2 态势向量 + 意图引擎 ----------

        private void TickIntent(Mission mission, float dt)
        {
            _intentTimer -= dt;
            if (_intentTimer > 0f) return;
            _intentTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                Team enemy = GetEnemyTeam(own);
                if (enemy == null) continue;

                int aliveSelf = own.ActiveAgents.Count;
                int aliveEnemy = enemy.ActiveAgents.Count;

                if (state.InitialCount < 0f) state.InitialCount = aliveSelf;

                // PenB 历史窗口：5 秒 @0.5s = 10 样本
                state.EnemyCountHistory.Enqueue(aliveEnemy);
                while (state.EnemyCountHistory.Count > 10) state.EnemyCountHistory.Dequeue();

                SituationVector v;
                ComposeVector(state, own, enemy, aliveSelf, aliveEnemy, out v);
                state.LastVector = v;

                IntentType newIntent;
                if (state.Engine.Evaluate(v, 0.5f, out newIntent))
                {
                    // F10 反制换招：被冷宫意图 → PickBest 换招；开局预判（仅一次、非玩家队）
                    int adjusted = _experience.AdjustIntent(own.TeamIndex, (int)newIntent);
                    int opening;
                    if ((mission.PlayerTeam == null || own != mission.PlayerTeam)
                        && _experience.TryGetOpening(own.TeamIndex, out opening))
                    {
                        adjusted = opening;
                    }
                    newIntent = (IntentType)adjusted;

                    state.LastIntent = newIntent;
                    _bus.Publish(new CombatEvent
                    {
                        Kind = CombatEventKind.IntentSwitched,
                        Time = _elapsed,
                        Id = own.TeamIndex,
                        IntValue = (int)newIntent
                    });
                    CombatLog.Log(string.Format(
                        "Team {0} 意图切换 → {1}  [{2}]",
                        own.TeamIndex, newIntent, v.ToString()));
                }
            }
        }

        private void ComposeVector(
            TeamState state, Team own, Team enemy,
            int aliveSelf, int aliveEnemy, out SituationVector v)
        {
            v = new SituationVector();

            int total = aliveSelf + aliveEnemy;
            float ratio = total > 0 ? (float)aliveSelf / (float)total : 0.5f;
            v.Power = VecMath.LogMapRatio(ratio);

            Vec2 oc = GetTeamCenter(own);
            Vec2 ec = GetTeamCenter(enemy);

            // Dist: 0 = 贴身, 1 = 很远
            v.Dist = VecMath.Clamp01(VecMath.Dist(oc.x, oc.y, ec.x, ec.y) / 80f);

            // Height: 地形优劣 = 己方中心相对敌方中心的高度优势
            float hDiff = TerrainStub.Height(oc.x, oc.y) - TerrainStub.Height(ec.x, ec.y);
            v.Height = VecMath.Clamp01(0.5f + hDiff * 0.15f);

            // Line: 阵线宽度对比——己方是否被拉散（编队离散度 vs 敌方）
            float ownSpread = FormationSpread(own);
            float enemySpread = FormationSpread(enemy);
            float spreadSum = ownSpread + enemySpread;
            v.Line = spreadSum > 1e-4f ? 1f - VecMath.Clamp01(ownSpread / spreadSum) : 0.5f;

            // Moral: 存活率衰减（士气代理）
            float survival = state.InitialCount > 0f ? aliveSelf / state.InitialCount : 1f;
            v.Moral = VecMath.Clamp01(survival);

            // PenB: 敌方虚弱度 = 过去 5 秒敌方兵力下降斜率
            v.PenB = ComputePenB(state);

            // Cav / Arc: 骨架阶段为校准占位值
            // TODO: 用 Formation.QuerySystem 编队兵种聚合（骑兵占比 / 有效射手×弹药余量）
            v.Cav = 0.5f;
            v.Arc = 0.5f;
        }

        private static float ComputePenB(TeamState state)
        {
            if (state.EnemyCountHistory.Count < 2) return 0f;
            float first = 0f;
            float last = 0f;
            int i = 0;
            foreach (float f in state.EnemyCountHistory)
            {
                if (i == 0) first = f;
                last = f;
                i++;
            }
            float drop = first - last;
            if (drop <= 0f) return 0f;
            float rate = drop / Math.Max(1f, first);
            return VecMath.Clamp01(rate * 2f);
        }

        // ---------- F6 走位/绕侧 ----------

        private void TickMovement(Mission mission, float dt)
        {
            // F7 求生撤退 / F9 远程后撤中的兵跳过走位（求生/后撤优先级高于走位）
            _movement.Tick(mission, dt, GetLod, GetProfile, GetMorale,
                i => _survival.IsRetreating(i) || _ranged.IsFallingBack(i));
        }

        // ---------- F7 求生本能 + 重整信号 ----------

        private void TickSurvival(Mission mission, float dt)
        {
            _survival.Tick(mission, dt, _elapsed, GetLod, GetProfile, GetMorale, _bus.Publish);
        }

        // ---------- F9 远程目标/弹药取舍 ----------

        private void TickRanged(Mission mission, float dt)
        {
            _ranged.Tick(mission, dt, GetLod, GetProfile, GetMorale, _survival.IsRetreating);
        }

        // ---------- F8 骑兵波次（编队级） ----------

        private void TickCavalry(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _cavalry.Tick(mission, dt, playerCenter, GetTeamIntent);
        }

        // ---------- F11 士气传导 + 分阶段撤退（编队级） ----------

        private void TickMorale(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _morale.Tick(mission, dt, _elapsed, playerCenter, GetTeamIntent, GetMorale);
        }

        private IntentType GetTeamIntent(Team team)
        {
            TeamState state;
            if (team != null && _states.TryGetValue(team.TeamIndex, out state))
                return state.LastIntent;
            return IntentType.Attack;
        }

        private LodLevel GetLod(int agentIndex)
        {
            LodLevel lod;
            return _agentLod.TryGetValue(agentIndex, out lod) ? lod : LodLevel.L0;
        }

        // F12 输入：队士气（意图引擎 0.5s 一刷的 LastVector.Moral，非逐帧重算）
        private float GetMorale(Team team)
        {
            TeamState state;
            if (team != null && _states.TryGetValue(team.TeamIndex, out state))
                return state.LastVector.Moral;
            return 0.5f;
        }

        // ---------- F4 预测式格挡 ----------

        private void TickBlock(Mission mission, float dt)
        {
            _blockTimer -= dt;
            if (_blockTimer > 0f) return;
            _blockTimer = 0.1f;

            var l3 = new List<Agent>();
            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive()) continue;
                LodLevel lod;
                if (_agentLod.TryGetValue(agent.Index, out lod) && lod == LodLevel.L3)
                    l3.Add(agent);
            }

            for (int i = 0; i < l3.Count; i++)
            {
                Agent defender = l3[i];
                Agent attacker = FindNearestEnemyAgent(defender, l3);
                if (attacker == null) continue;

                AttackStats stats = GetAttackStats(attacker.Index);
                FighterProfile defenderProfile = GetProfile(defender.Index);
                FighterProfile attackerProfile = GetProfile(attacker.Index);

                AttackDir predicted;
                float confidence;
                if (PredictiveBlock.TryPredict(stats, defenderProfile, attackerProfile.FeintRate, out predicted, out confidence))
                {
                    // F12 横切：犹豫/延迟维持期跳过本轮举盾（拟人化反应迟滞，不侵入纯逻辑）
                    float signedWobble;
                    if (_jitterBlock.ShouldAct(
                        defender.Index, dt,
                        DecisionJitterGlue.DisciplineFrom(defenderProfile),
                        GetMorale(defender.Team), out signedWobble))
                    {
                        defender.EnforceShieldUsage(ToUsageDefend(predicted));
                    }
                }
            }
        }

        public override void OnAgentHit(
            Agent affectedAgent, Agent affectorAgent,
            in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);

            if (affectorAgent == null || affectedAgent == null) return;

            AttackDir dir = MapAttackDir(affectorAgent.AttackDirection);
            if (dir == AttackDir.None) return;

            GetAttackStats(affectorAgent.Index).Record(dir);
            _bus.Publish(new CombatEvent
            {
                Kind = CombatEventKind.AttackObserved,
                Time = _elapsed,
                Id = affectorAgent.Index,
                Id2 = affectedAgent.Index,
                IntValue = (int)dir
            });
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);

            if (affectedAgent != null)
            {
                _bus.Publish(new CombatEvent
                {
                    Kind = CombatEventKind.AgentDown,
                    Time = _elapsed,
                    Id = affectedAgent.Index,
                    Id2 = affectorAgent != null ? affectorAgent.Index : -1
                });
                _agentLod.Remove(affectedAgent.Index);
                _profiles.Remove(affectedAgent.Index);
                _movement.RemoveAgent(affectedAgent.Index);
                _survival.Remove(affectedAgent.Index);
                _ranged.Remove(affectedAgent.Index);
                _jitterBlock.Remove(affectedAgent.Index);
                _morale.RecordCasualty(affectedAgent.Formation, _elapsed); // F11 脱阵窗口（事件驱动）
            }
        }

        // F10 跨战役套路记忆落盘（战役结束一次 IO，不做每帧 IO）
        // v1.4.8 无 OnMissionEnded；OnMissionStateFinalized 为战役收尾标准钩子，OnClearScene 兜底（幂等）
        public override void OnMissionStateFinalized()
        {
            base.OnMissionStateFinalized();
            _experience.OnMissionEnd();
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _experience.OnMissionEnd();
        }

        // ---------- helpers ----------

        private Team GetEnemyTeam(Team own)
        {
            foreach (TeamState state in _states.Values)
            {
                if (state.Team != own) return state.Team;
            }
            return null;
        }

        // F10 输入：给定队索引 → 其敌队存活数（意图反制判定）
        private int GetEnemyAliveCount(int teamIndex)
        {
            foreach (TeamState state in _states.Values)
            {
                if (state.Team.TeamIndex != teamIndex) continue;
                Team enemy = GetEnemyTeam(state.Team);
                return enemy != null ? enemy.ActiveAgents.Count : 0;
            }
            return 0;
        }

        private List<Vec2> CollectEnemyCenters(Team own)
        {
            var list = new List<Vec2>();
            foreach (TeamState state in _states.Values)
            {
                if (state.Team == own) continue;
                foreach (Formation f in state.Team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0) continue;
                    list.Add(f.SmoothedAverageUnitPosition);
                }
            }
            return list;
        }

        private Vec2 GetTeamCenter(Team team)
        {
            float sx = 0f;
            float sy = 0f;
            int n = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                Vec2 p = f.SmoothedAverageUnitPosition;
                sx += p.x;
                sy += p.y;
                n++;
            }

            TeamState state;
            if (n == 0)
            {
                // 无一兵：保持上次中心，避免跳回原点把 Dist 打成 1.0 假象
                return _states.TryGetValue(team.TeamIndex, out state) ? state.Center : Vec2.Zero;
            }

            var result = Vec2.Zero;
            result.x = sx / n;
            result.y = sy / n;
            if (_states.TryGetValue(team.TeamIndex, out state))
                state.Center = result;
            return result;
        }

        private float FormationSpread(Team team)
        {
            Vec2 c = GetTeamCenter(team);
            float sum = 0f;
            int n = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                Vec2 p = f.SmoothedAverageUnitPosition;
                sum += VecMath.Dist(p.x, p.y, c.x, c.y);
                n++;
            }

            TeamState state;
            if (n == 0)
            {
                // 无一兵：保持上次离散度
                return _states.TryGetValue(team.TeamIndex, out state) ? state.Spread : 0f;
            }

            float spread = sum / n;
            if (_states.TryGetValue(team.TeamIndex, out state))
                state.Spread = spread;
            return spread;
        }

        private static float NearestDistance(float x, float y, List<Vec2> points)
        {
            if (points == null || points.Count == 0) return 1000f;
            float best = float.MaxValue;
            foreach (Vec2 p in points)
            {
                float d = VecMath.Dist(x, y, p.x, p.y);
                if (d < best) best = d;
            }
            return best;
        }

        private static Agent FindNearestEnemyAgent(Agent a, List<Agent> candidates)
        {
            Agent best = null;
            float bestD = BehaviorLodScheduler.L3MeleeRadius;
            Team at = a.Team;
            foreach (Agent c in candidates)
            {
                if (c == a || c.Team == at || !c.IsActive()) continue;
                float d = VecMath.Dist(a.Position.x, a.Position.y, c.Position.x, c.Position.y);
                if (d <= bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
            return best;
        }

        private FighterProfile GetProfile(int agentIndex)
        {
            FighterProfile p;
            if (!_profiles.TryGetValue(agentIndex, out p))
            {
                p = FighterProfileFactory.Create(FighterProfileFactory.Classify(agentIndex));
                _profiles[agentIndex] = p;
            }
            return p;
        }

        private AttackStats GetAttackStats(int agentIndex)
        {
            AttackStats s;
            if (!_attackStats.TryGetValue(agentIndex, out s))
            {
                s = new AttackStats(8);
                _attackStats[agentIndex] = s;
            }
            return s;
        }

        private static AttackDir MapAttackDir(Agent.UsageDirection u)
        {
            switch (u)
            {
                case Agent.UsageDirection.AttackUp: return AttackDir.Up;
                case Agent.UsageDirection.AttackDown: return AttackDir.Down;
                case Agent.UsageDirection.AttackLeft: return AttackDir.Left;
                case Agent.UsageDirection.AttackRight: return AttackDir.Right;
            }
            return AttackDir.None;
        }

        private static Agent.UsageDirection ToUsageDefend(AttackDir d)
        {
            switch (d)
            {
                case AttackDir.Up: return Agent.UsageDirection.DefendUp;
                case AttackDir.Down: return Agent.UsageDirection.DefendDown;
                case AttackDir.Left: return Agent.UsageDirection.DefendLeft;
                case AttackDir.Right: return Agent.UsageDirection.DefendRight;
            }
            return Agent.UsageDirection.DefendAny;
        }
    }
}