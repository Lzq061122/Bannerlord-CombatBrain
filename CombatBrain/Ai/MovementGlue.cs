using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Combat;
using CombatBrain.Lod;
using CombatBrain.Movement;

namespace CombatBrain.Ai
{
    // F6 走位/绕侧 —— 胶水层（第一条接线样板）。
    // 职责：把纯逻辑 MovementController.Decide 接到 Agent 上，驱动走位/距离控制/绕侧。
    //
    // 性能预算（三条铁律落地）：
    //   * 0.2s 低频周期驱动（TickInterval），绝不逐帧遍历/逐帧决策。
    //   * 剧场效应：只处理 L2/L3（玩家可见战斗区）agent；L0/L1 一律跳过，交给原版 AI。
    //   * 跳帧聚合：L3 每拍必判，L2 按 BehaviorLodScheduler.StepFor 跳帧，500 人战场每拍只查 1/step。
    //   * 敌情用 Mission.GetNearbyEnemyAgents（引擎空间哈希，O(邻域)），不 O(n²) 全图扫描；
    //     每拍复用 MBList 缓冲，零分配。
    //
    // API 核实（BannerlordSage，游戏 v1.4.8 精确匹配）：
    //   * Agent.SetMovementDirection(in Vec2)  —— Agent.cs L2402：public void SetMovementDirection(in Vec2 direction)
    //   * Agent.Position(Vec3)/LookDirection(Vec3)/IsActive()/IsAIControlled/IsHuman/HasMount/Index/Team
    //   * Agent.GetPrimaryWieldedItemIndex()/Equipment[EquipmentIndex].CurrentUsageItem.GetRealWeaponLength()
    //   * Mission.GetNearbyEnemyAgents(Vec2,float,Team,MBList<Agent>) —— Mission.cs L6582
    //   * Mission.GetNearbyAllyAgents(Vec2,float,Team,MBList<Agent>)  —— Mission.cs L6575
    // 说明：原版 HumanAIComponent 每帧自行驱动移动，本层按 0.2s 叠加决策输入（SetMovementDirection 为
    // 引擎级移动方向输入，会被混合/覆盖一部分）；如需完全接管走位，后续可升级为
    // SetScriptedFlags/脱离编队，属 F6 之后的增强项，不在此样板内。
    public sealed class MovementGlue
    {
        // 决策周期：契约要求 ≤0.2s；取 0.2s（比 L2 的 0.25s 稍快，保证接线段不被 LOD 节流）
        public const float TickInterval = 0.2f;

        private const float ScanRadius = 25f;          // 敌情扫描半径，与 BehaviorLodScheduler.L2CombatRadius 对齐
        private const float SurroundedRadius = 1.6f;   // 被围判定半径
        private const int SurroundedCount = 2;         // 半径内 ≥2 个敌人视为被围
        private const float AllySearchRadius = 4f;     // 后撤时友军搜索半径
        private const float DefaultReach = 1.0f;       // 无武器/空手默认有效距离(米)
        private const float RetreatFallbackDist = 5f;  // 无友军可退时的退点距离
        private const float ReportInterval = 10f;      // 决策直方图上报间隔

        private float _sinceTick;
        private float _sinceReport;
        private int _phase;                            // 跳帧相位轮转

        // 每拍复用缓冲，避免 GC 压力
        private readonly MBList<Agent> _enemyBuffer = new MBList<Agent>();
        private readonly MBList<Agent> _allyBuffer = new MBList<Agent>();

        // F12 决策抖动&服从度横切包装（走位决策前调用，详见 DecisionJitterGlue.cs）
        private readonly DecisionJitterGlue _jitter = new DecisionJitterGlue();

        // 决策直方图（仅日志观测，10s 一报）
        private int _cAdvance;
        private int _cStepBack;
        private int _cStrafe;
        private int _cRetreat;
        private int _cHold;
        private int _cSkipped;
        private int _cJitter; // F12 犹豫/维持期跳过的决策数

        /// <summary>
        /// 低频驱动入口：由 CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="getLod">agent 的 LOD 查询（剧场上限）</param>
        /// <param name="getProfile">agent 的 FighterProfile 查询（绕侧意识，真实兵种分类）</param>
        /// <param name="getMorale">agent 所属队士气查询（F12 输入，0..1）</param>
        /// <param name="isSurvivalRetreat">F7 求生撤退 / F9 远程后撤态查询（撤退/后撤中跳过走位，优先级最高）</param>
        public void Tick(Mission mission, float dt, Func<int, LodLevel> getLod, Func<Agent, FighterProfile> getProfile, Func<Team, float> getMorale, Func<int, bool> isSurvivalRetreat)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            int agentCount = mission.Agents.Count;
            _phase = (_phase + 1) % 64;

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive() || !agent.IsAIControlled) continue;
                if (!agent.IsHuman || agent.HasMount) continue; // 骑兵走位归 F8，不在此层
                if (agent.IsRunningAway) { _cSkipped++; continue; } // 尊重原版逃跑态
                Team team = agent.Team;
                if (team == null) continue;

                LodLevel lod = getLod(agent.Index);
                if (lod < LodLevel.L2) continue; // 剧场效应：可见区外优雅降级，交原版 AI

                if (isSurvivalRetreat != null && isSurvivalRetreat(agent.Index))
                {
                    _cSkipped++; // F7 求生撤退中：走位不覆盖，让撤退方向保持
                    continue;
                }

                int step = BehaviorLodScheduler.StepFor(lod, agentCount);
                if (((agent.Index + _phase) % step) != 0) { _cSkipped++; continue; } // 跳帧决策

                ApplyDecision(mission, agent, team, getProfile(agent), dt, getMorale(team));
            }

            _sinceReport += dt;
            if (_sinceReport >= ReportInterval)
            {
                Report();
                _sinceReport = 0f;
            }
        }

        private void ApplyDecision(Mission mission, Agent agent, Team team, FighterProfile profile, float dt, float morale)
        {
            Vec2 pos = agent.Position.AsVec2;

            // 敌情扫描：引擎空间哈希，一次拿 25m 内全部敌人，O(邻域)
            mission.GetNearbyEnemyAgents(pos, ScanRadius, team, _enemyBuffer);

            Agent target = null;
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
                    target = e;
                }
                if (d <= SurroundedRadius * SurroundedRadius) nearCount++;
            }

            if (target == null)
            {
                _cSkipped++; // 25m 内无敌人：不掺和，交给原版 AI 接近
                return;
            }

            var ctx = new MovementContext
            {
                OwnX = pos.x,
                OwnY = pos.y,
                TargetX = target.Position.x,
                TargetY = target.Position.y,
                OwnReach = GetReach(agent),
                TargetReach = GetReach(target),
                FlankAware = profile.FlankAware,
                OffHandSide = ComputeOffHandSide(agent, target),
                IsSurrounded = nearCount >= SurroundedCount,
                AllyX = 0f,
                AllyY = 0f
            };

            if (ctx.IsSurrounded)
            {
                Vec2 retreat = FindRetreatPoint(mission, agent, team, target);
                ctx.AllyX = retreat.x;
                ctx.AllyY = retreat.y;
            }

            MovementDecision d2 = MovementController.Decide(ctx);

            // F12 横切：犹豫/延迟维持期跳过本轮；走形扰动移动方向（拟人化，不侵入纯逻辑）
            float signedWobble;
            if (_jitter.ShouldAct(agent.Index, dt, DecisionJitterGlue.DisciplineFrom(profile), morale, out signedWobble))
            {
                Vec2 dir = new Vec2(d2.MoveX, d2.MoveY);
                if (dir.LengthSquared > 1e-6f && Math.Abs(signedWobble) > 0.01f)
                    dir = DecisionJitterGlue.ApplyWobble(dir, signedWobble);

                // 引擎级移动方向输入（in Vec2）
                agent.SetMovementDirection(dir);
                CountAction(d2.Action);
            }
            else
            {
                _cJitter++; // F12 犹豫/维持期：本轮不覆盖移动
            }
        }

        // 武器有效距离：主手武器实时长度；空手/装备切换瞬间兜底默认值
        private static float GetReach(Agent agent)
        {
            try
            {
                EquipmentIndex idx = agent.GetPrimaryWieldedItemIndex();
                if (idx < EquipmentIndex.WeaponItemBeginSlot) return DefaultReach;
                MissionWeapon w = agent.Equipment[idx];
                if (w.IsEmpty || w.CurrentUsageItem == null) return DefaultReach;
                float reach = w.CurrentUsageItem.GetRealWeaponLength();
                if (reach < 0.5f || reach > 4f) return DefaultReach;
                return reach;
            }
            catch
            {
                return DefaultReach;
            }
        }

        // 绕侧方向：以对方朝向为基准，判我方在其左侧还是右侧（可观察、非作弊）。
        // cross = f × v（f=对方朝向单位向量，v=对方→我方单位向量）。
        // |cross| 过小（对方正对我们）时退化为确定性偏好，避免决策左右抖。
        private static float ComputeOffHandSide(Agent self, Agent target)
        {
            Vec3 look = target.LookDirection;
            float fx = look.x;
            float fy = look.y;
            float flen = (float)Math.Sqrt(fx * fx + fy * fy);
            if (flen > 1e-4f)
            {
                fx /= flen;
                fy /= flen;
            }
            else
            {
                fx = 1f;
                fy = 0f;
            }

            float vx = self.Position.x - target.Position.x;
            float vy = self.Position.y - target.Position.y;
            float vlen = (float)Math.Sqrt(vx * vx + vy * vy);
            if (vlen > 1e-4f)
            {
                vx /= vlen;
                vy /= vlen;
            }

            float cross = fx * vy - fy * vx;
            if (cross > 0.05f) return 1f;
            if (cross < -0.05f) return -1f;

            int h = (target.Index * 1103515245 + 12345) & 0x7fffffff;
            return (h & 1) == 0 ? 1f : -1f;
        }

        // 被围后撤点：近旁友军最近者；无友军退回本队编队中心；再无则退离最近敌人。
        private Vec2 FindRetreatPoint(Mission mission, Agent agent, Team team, Agent nearestEnemy)
        {
            Vec2 pos = agent.Position.AsVec2;
            mission.GetNearbyAllyAgents(pos, AllySearchRadius, team, _allyBuffer);

            if (_allyBuffer.Count > 0)
            {
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
            }

            Formation f = agent.Formation;
            if (f != null && f.CountOfUnits > 0) return f.SmoothedAverageUnitPosition;

            // 兜底：退离最近敌人 RetreatFallbackDist
            float dx = pos.x - nearestEnemy.Position.x;
            float dy = pos.y - nearestEnemy.Position.y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-4f)
            {
                dx /= len;
                dy /= len;
            }
            return new Vec2(pos.x + dx * RetreatFallbackDist, pos.y + dy * RetreatFallbackDist);
        }

        // agent 阵亡时清理抖动状态（由 CombatAiMissionLogic.OnAgentRemoved 调用）
        public void RemoveAgent(int agentIndex)
        {
            _jitter.Remove(agentIndex);
        }

        private void CountAction(MoveAction action)
        {
            switch (action)
            {
                case MoveAction.Advance: _cAdvance++; break;
                case MoveAction.StepBack: _cStepBack++; break;
                case MoveAction.StrafeLeft:
                case MoveAction.StrafeRight: _cStrafe++; break;
                case MoveAction.RetreatToAllies: _cRetreat++; break;
                default: _cHold++; break;
            }
        }

        private void Report()
        {
            CombatLog.Log(string.Format(
                "[F6] 走位决策 adv={0} back={1} strafe={2} retreat={3} hold={4} skip={5} jitter={6}",
                _cAdvance, _cStepBack, _cStrafe, _cRetreat, _cHold, _cSkipped, _cJitter));
            _cAdvance = 0;
            _cStepBack = 0;
            _cStrafe = 0;
            _cRetreat = 0;
            _cHold = 0;
            _cSkipped = 0;
            _cJitter = 0;
        }
    }
}
