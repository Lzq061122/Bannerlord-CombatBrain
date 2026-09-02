using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Combat;
using CombatBrain.Events;
using CombatBrain.Lod;
using CombatBrain.Survival;

namespace CombatBrain.Ai
{
    // F7 求生本能 + 重整信号 —— 胶水层。
    // 职责：
    //   * 个体层：濒死兵（Health/BaseHealthLimit < 30%）被集火且背后无安全区 → 主动退向友军/编队中心；
    //   * 编队层：统计"新跌破濒死线"事件，5s 窗内 ≥8 次 → 上报 IntentType.Reform 事件（重整信号）。
    // 纯逻辑 CombatBrain/Survival/SurvivalInstinct.cs 只读消费，零改动。
    //
    // 性能预算（三条铁律落地）：
    //   * 0.3s 低频周期（TickInterval），绝不逐帧；只处理 L2+（剧场上限），L0/L1 交原版 AI；
    //   * StepFor 跳帧聚合；敌/友情用 Mission.GetNearbyEnemyAgents / GetNearbyAllyAgents
    //     （引擎空间哈希 O(邻域)，F6 样板同款）；且敌情扫描只在濒死分支执行（多数兵零扫描）；
    //   * MBList 缓冲复用 + 每 agent 状态常驻字典，零每拍分配。
    // API 核实（BannerlordSage，游戏 v1.4.8 精确匹配）：
    //   * Agent.Health           —— Agent.cs L1467：public float Health
    //   * Agent.BaseHealthLimit  —— Agent.cs L881 ：public float BaseHealthLimit { get; set; }
    //   * Mission.GetNearbyEnemyAgents / GetNearbyAllyAgents —— Mission.cs L6582 / L6575（F6 已核实）
    // 与 F6 的优先级：求生撤退通过 _retreating 注册表通知 MovementGlue 跳过该兵（走位不覆盖撤退）。
    public sealed class SurvivalGlue
    {
        public const float TickInterval = 0.3f;

        private const float HpThreshold = SurvivalInstinct.HpThreshold; // 0.30
        private const float EnemyScanRadius = 25f;  // 敌情扫描半径（与 L2CombatRadius 对齐）
        private const float FocusRadius = 3f;       // 被集火判定半径
        private const int FocusCount = 2;           // 半径内 ≥2 敌 → 被集火
        private const float SafeBehindRadius = 15f; // 背后友军安全区判定半径
        private const float AllySearchRadius = 25f; // 撤退目标友军搜索半径
        private const float RetreatFallbackDist = 5f;

        private float _sinceTick;
        private int _phase;

        // 每拍复用缓冲，避免 GC 压力
        private readonly MBList<Agent> _enemyBuffer = new MBList<Agent>();
        private readonly MBList<Agent> _allyBuffer = new MBList<Agent>();

        // 每 agent 常驻状态
        private readonly DecisionJitterGlue _jitter = new DecisionJitterGlue(); // F12 横切（撤退前犹豫/走形）
        // P0-① 抖动时间基准：每 agent「上次调用 ShouldAct 的时刻」（now 基准 = 传入的 now），
        // 真实决策间隔 = now - last；传帧 dt 会让维持期/疲劳慢 6~20 倍。
        private readonly Dictionary<int, float> _lastJitterTime = new Dictionary<int, float>();
        private readonly Dictionary<int, bool> _wasCritical = new Dictionary<int, bool>(); // 濒死线跃迁检测
        private readonly Dictionary<int, bool> _retreating = new Dictionary<int, bool>();  // 撤退态（F6 跳过依据）

        // 编队层：每队一个重伤窗口 + 触发武装（单发触发，窗内事件数回落后自动重新武装）
        private readonly Dictionary<int, CasualtyReformSignal> _reformByTeam = new Dictionary<int, CasualtyReformSignal>();
        private readonly Dictionary<int, bool> _reformArmed = new Dictionary<int, bool>();

        /// <summary>F6 走位是否应跳过该兵（求生撤退中）。</summary>
        public bool IsRetreating(int agentIndex)
        {
            bool v;
            return _retreating.TryGetValue(agentIndex, out v) && v;
        }

        /// <summary>
        /// 低频驱动入口：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="now">战斗累计时间（重整窗口时间戳）</param>
        /// <param name="getLod">agent LOD 查询（剧场上限）</param>
        /// <param name="getProfile">agent FighterProfile 查询（F12 纪律，真实兵种分类）</param>
        /// <param name="getMorale">队士气查询（F12 输入）</param>
        /// <param name="publish">事件发布（重整信号 → 意图引擎）</param>
        public void Tick(Mission mission, float dt, float now,
            Func<int, LodLevel> getLod,
            Func<Agent, FighterProfile> getProfile,
            Func<Team, float> getMorale,
            Action<CombatEvent> publish)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            int agentCount = mission.Agents.Count;
            _phase = (_phase + 1) % 64;

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive() || !agent.IsAIControlled) continue;
                if (!agent.IsHuman || agent.HasMount) continue; // 骑兵求生归 F8
                if (agent.IsRunningAway) continue;              // 尊重原版逃跑态
                Team team = agent.Team;
                if (team == null) continue;

                LodLevel lod = getLod(agent.Index);
                if (lod < LodLevel.L2) continue; // 剧场效应：可见区外优雅降级

                int step = BehaviorLodScheduler.StepFor(lod, agentCount);
                if (((agent.Index + _phase) % step) != 0) continue; // 跳帧决策

                float hpRatio = ComputeHpRatio(agent);
                TrackCritical(agent.Index, team, hpRatio, now, publish); // 编队层重伤事件（廉价，O(1)）

                if (hpRatio >= HpThreshold)
                {
                    _retreating[agent.Index] = false; // 健康：解除撤退态，Fight 不干预
                    continue;
                }

                // 濒死分支：敌情扫描只在少数重伤兵上执行
                Vec2 pos = agent.Position.AsVec2;
                mission.GetNearbyEnemyAgents(pos, EnemyScanRadius, team, _enemyBuffer);

                Agent nearestEnemy = null;
                float bestD = float.MaxValue;
                int nearCount = 0;
                for (int i = 0; i < _enemyBuffer.Count; i++)
                {
                    Agent e = _enemyBuffer[i];
                    if (e == null || !e.IsActive()) continue;
                    float d = VecMath.DistSq(pos.x, pos.y, e.Position.x, e.Position.y);
                    if (d < bestD)
                    {
                        bestD = d;
                        nearestEnemy = e;
                    }
                    if (d <= FocusRadius * FocusRadius) nearCount++;
                }

                var ctx = new SurvivalContext
                {
                    HpRatio = hpRatio,
                    BeingFocused = nearCount >= FocusCount,
                    SafeBehind = HasSafeBehind(mission, agent, team, nearestEnemy)
                };

                SurvivalAction action = SurvivalInstinct.Decide(ctx);

                if (action == SurvivalAction.RetreatToAllies)
                {
                    ApplyRetreat(mission, agent, team, nearestEnemy, pos, now, getProfile(agent), getMorale(team));
                }
                else
                {
                    _retreating[agent.Index] = false; // Hold：稳住，不覆盖移动
                }
            }
        }

        public void Remove(int agentIndex)
        {
            _jitter.Remove(agentIndex);
            _lastJitterTime.Remove(agentIndex);
            _wasCritical.Remove(agentIndex);
            _retreating.Remove(agentIndex);
        }

        // ---------- 个体层 ----------

        private static float ComputeHpRatio(Agent agent)
        {
            float limit = agent.BaseHealthLimit;
            if (limit <= 0f) return 0f; // 无血量上限兜底：视为濒死
            float ratio = agent.Health / limit;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }

        private void ApplyRetreat(Mission mission, Agent agent, Team team, Agent nearestEnemy, Vec2 pos,
            float now, FighterProfile profile, float morale)
        {
            Vec2 target = FindRetreatTarget(mission, agent, team, nearestEnemy, pos);
            float dx = target.x - pos.x;
            float dy = target.y - pos.y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f)
            {
                _retreating[agent.Index] = false;
                return;
            }

            // P0-① 真实决策间隔：now - last（跨节流+跳帧空拍也在内）；首次近似=节流周期；时钟异常兜底。
            float last;
            if (!_lastJitterTime.TryGetValue(agent.Index, out last)) last = now - TickInterval;
            float elapsed = now - last;
            if (elapsed <= 0f) elapsed = TickInterval;
            _lastJitterTime[agent.Index] = now;

            // F12 横切：犹豫/延迟维持期本轮不跑；走形扰动撤退方向（拟人化，不侵入纯逻辑）
            float signedWobble;
            if (_jitter.ShouldAct(agent.Index, elapsed, DecisionJitterGlue.DisciplineFrom(profile), morale, out signedWobble))
            {
                Vec2 dir = new Vec2(dx / len, dy / len);
                if (Math.Abs(signedWobble) > 0.01f)
                    dir = DecisionJitterGlue.ApplyWobble(dir, signedWobble);
                agent.SetMovementDirection(dir);
                _retreating[agent.Index] = true;
                DecisionObservatory.Observe(DecisionObservatory.BSurvRetreat); // T1 决策分支计数
            }
        }

        // 背后安全区：15m 内有友军大致处于"远离敌人"方向（同向分量 > 0）；无敌人视为安全
        private bool HasSafeBehind(Mission mission, Agent agent, Team team, Agent nearestEnemy)
        {
            Vec2 pos = agent.Position.AsVec2;
            mission.GetNearbyAllyAgents(pos, SafeBehindRadius, team, _allyBuffer);

            if (nearestEnemy == null) return true;
            float ex = pos.x - nearestEnemy.Position.x;
            float ey = pos.y - nearestEnemy.Position.y;
            float elen = (float)Math.Sqrt(ex * ex + ey * ey);
            if (elen < 1e-4f) return true;
            ex /= elen;
            ey /= elen; // 单位"退向"方向（远离敌人）

            for (int i = 0; i < _allyBuffer.Count; i++)
            {
                Agent a = _allyBuffer[i];
                if (a == null || !a.IsActive()) continue;
                float ax = a.Position.x - pos.x;
                float ay = a.Position.y - pos.y;
                if (ax * ex + ay * ey > 0f) return true; // 友军在撤退方向上
            }
            return false;
        }

        // 撤退目标：最近友军 → 编队中心 → 退离最近敌人
        private Vec2 FindRetreatTarget(Mission mission, Agent agent, Team team, Agent nearestEnemy, Vec2 pos)
        {
            mission.GetNearbyAllyAgents(pos, AllySearchRadius, team, _allyBuffer);

            Agent best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _allyBuffer.Count; i++)
            {
                Agent a = _allyBuffer[i];
                if (a == null || !a.IsActive()) continue;
                float d = VecMath.DistSq(pos.x, pos.y, a.Position.x, a.Position.y);
                if (d < bestD)
                {
                    bestD = d;
                    best = a;
                }
            }
            if (best != null) return best.Position.AsVec2;

            Formation f = agent.Formation;
            if (f != null && f.CountOfUnits > 0) return TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage); // P1-④ 统一入口

            if (nearestEnemy != null)
            {
                float dx = pos.x - nearestEnemy.Position.x;
                float dy = pos.y - nearestEnemy.Position.y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len > 1e-4f)
                {
                    return new Vec2(pos.x + dx / len * RetreatFallbackDist, pos.y + dy / len * RetreatFallbackDist);
                }
            }
            return pos; // 全兜底失败：原地（不产生错误方向）
        }

        // ---------- 编队层：重伤窗口 → 重整信号 ----------

        private void TrackCritical(int agentIndex, Team team, float hpRatio, float now, Action<CombatEvent> publish)
        {
            bool was;
            if (!_wasCritical.TryGetValue(agentIndex, out was)) was = false;
            bool isCritical = hpRatio < HpThreshold;
            _wasCritical[agentIndex] = isCritical;

            if (!isCritical || was) return; // 只统计"新跌破濒死线"事件

            CasualtyReformSignal signal;
            if (!_reformByTeam.TryGetValue(team.TeamIndex, out signal))
            {
                signal = new CasualtyReformSignal();
                _reformByTeam[team.TeamIndex] = signal;
            }

            bool windowFull = signal.OnCriticalCasualty(now); // 入队 + 5s 窗判断（纯逻辑内部阈值=8）

            bool armed;
            if (!_reformArmed.TryGetValue(team.TeamIndex, out armed)) armed = true;

            if (armed && windowFull)
            {
                if (publish != null)
                {
                    publish(new CombatEvent
                    {
                        Kind = CombatEventKind.IntentSwitched,
                        Time = now,
                        Id = team.TeamIndex,
                        IntValue = (int)IntentType.Reform
                    });
                }
                CombatLog.Log(string.Format("[F7] Team {0} 编队重伤触发重整信号", team.TeamIndex));
                DecisionObservatory.Observe(DecisionObservatory.BSurvReform); // T1 决策分支计数
                _reformArmed[team.TeamIndex] = false; // 单发触发
            }
            else if (!windowFull)
            {
                _reformArmed[team.TeamIndex] = true; // 窗内事件数回落到阈值下 → 重新武装
            }
        }
    }
}
