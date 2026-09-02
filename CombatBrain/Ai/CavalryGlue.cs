using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Cavalry;

namespace CombatBrain.Ai
{
    // F8 骑兵多波次调度 —— 胶水层（编队级驱动）。
    // 职责：0.5s 低频驱动纯逻辑 CavalryWaveFSM（只读消费，零改动）：
    //   SCOUT(侦察) → SETUP(列阵) → CHARGE(冲锋) → RECALL(回撤) → REFORM(重整) → SETUP 闭环。
    //
    // 性能预算（三条铁律落地）：
    //   * 编队级聚合（聚合 > 遍历）：每 0.5s 只遍历各队非空编队（O(编队数)≈个位数），
    //     命令整编队，绝不逐兵每帧；无空间查询、无 MBList 遍历。
    //   * 剧场效应：只接管玩家 70m（PlayerFocusRadius）内的骑兵编队，远处交原版 AI。
    //   * 命令只在 FSM 状态跃迁时下发一次（防每拍刷命令）。
    // 反贴脸卡死：CHARGE 触敌（两编队中位距 ≤6m）→ RECALL；RECALL 拉到 30m 外或 6s 超时 → REFORM；
    // REFORM 4s 超时 → SETUP；SETUP/SCOUT 超时兜底推进——任何状态都有出路。
    // API 核实（BannerlordSage，游戏 v1.4.8 精确匹配）：
    //   * Formation.QuerySystem.IsCavalryFormation —— FormationQuerySystem.cs L195
    //   * Formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation —— L275
    //   * Formation.SetMovementOrder(MovementOrder) —— Formation.cs L685
    //   * Formation.SetArrangementOrder(ArrangementOrder) —— Formation.cs L722
    //   * Formation.CountOfUnits —— L182；Formation.CachedMedianPosition —— L247
    //   * MovementOrder.MovementOrderStop/Charge/FallBack/ChargeToTarget(Formation) —— MovementOrder.cs L53/L57/L61/L397
    //   * ArrangementOrder.ArrangementOrderLine/Column —— ArrangementOrder.cs L36/L34
    //   * Team.IsPlayerTeam —— Team.cs L40（v1.4.8，P0-④ 玩家队豁免）
    // 说明：命令覆盖原版编队 AI 的移动/阵型指令（F8 即波次指挥）；玩家手动下令的覆盖不做特判（骨架阶段）。
    public sealed class CavalryGlue
    {
        public const float TickInterval = 0.5f;

        private const float DriveRadius = 70f;   // 剧场上限：与 BehaviorLodScheduler.PlayerFocusRadius 对齐
        private const float ImpactRadius = 6f;   // CHARGE 触敌判定半径（贴脸即回撤，防卡死）
        private const float RallyDistance = 30f; // RECALL 回撤达标距离
        private const float ChargeTimeout = 12f; // 各状态超时兜底（防任何卡死）
        private const float RecallTimeout = 6f;
        private const float ReformTimeout = 4f;
        private const float SetupTimeout = 8f;

        private float _sinceTick;
        private readonly Dictionary<Formation, CavalryState> _states = new Dictionary<Formation, CavalryState>();
        private readonly Dictionary<Formation, float> _stateElapsed = new Dictionary<Formation, float>();

        /// <summary>
        /// 编队级低频驱动：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="playerCenter">玩家焦点中心（剧场上限基准）</param>
        /// <param name="getIntent">各队当前意图（F2 意图引擎输出，机会窗触发源）</param>
        public void Tick(Mission mission, float dt, Vec2 playerCenter, Func<Team, IntentType> getIntent)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            foreach (Team team in mission.Teams)
            {
                if (team == null || team.IsPlayerTeam) continue; // P0-④ 玩家队豁免：玩家骑兵归玩家指挥，不接管波次
                IntentType intent = getIntent != null ? getIntent(team) : IntentType.Attack;

                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation == null || formation.CountOfUnits == 0) continue;
                    if (formation.QuerySystem == null || !formation.QuerySystem.IsCavalryFormation) continue; // 只接骑兵编队

                    Vec2 med = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian); // P1-④ 统一入口
                    if (VecMath.Dist(med.x, med.y, playerCenter.x, playerCenter.y) > DriveRadius)
                    {
                        ReleaseOutsideRadius(formation); // P0-⑤ 剧场外释放 FSM 状态与命令（幂等）
                        continue;
                    }

                    TickFormation(formation, intent, dt);
                }
            }
        }

        // P0-⑤ 剧场外命令泄漏修复：骑兵编队出 70m 后 FSM 停驱，最后下发的命令（Charge/FallBack 等）
        // 与 FSM 状态会永久残留。出半径 → 丢弃 FSM 状态 + 下发 Advance 交回原版；回场后从 SCOUT
        // 重新开始。幂等：无状态直接返回，重复调用无害。
        private void ReleaseOutsideRadius(Formation formation)
        {
            if (!_states.ContainsKey(formation)) return;
            _states.Remove(formation);
            _stateElapsed.Remove(formation);
            formation.SetMovementOrder(MovementOrder.MovementOrderAdvance); // 交回原版默认推进（MovementOrder.cs L59，v1.4.8 核实）
        }

        private void TickFormation(Formation formation, IntentType teamIntent, float dt)
        {
            CavalryState state;
            if (!_states.TryGetValue(formation, out state))
            {
                state = CavalryState.Scout;
                _states[formation] = state;
                _stateElapsed[formation] = 0f;
                IssueOrder(formation, state, null);
                return; // 首拍只下发初始命令，下一拍开始推进
            }

            float elapsed = _stateElapsed[formation];
            _stateElapsed[formation] = elapsed + dt;

            FormationQuerySystem enemy = formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation;

            var inp = new CavalryInput();
            switch (state)
            {
                case CavalryState.Scout:
                    inp.ScoutFoundWeakness = enemy != null || elapsed > SetupTimeout; // 探到目标编队 / 超时兜底
                    break;
                case CavalryState.Setup:
                    inp.Opportunity = MapOpportunity(teamIntent);
                    if (enemy != null && elapsed > SetupTimeout)
                        inp.Opportunity = OpportunityType.EnemyLineGap; // 超时兜底：有目标即冲
                    break;
                case CavalryState.Charge:
                    inp.ImpactFinished = (enemy != null && DistToEnemy(formation, enemy) <= ImpactRadius)
                        || elapsed > ChargeTimeout; // 触敌回撤 / 超时兜底（防贴脸卡住）
                    break;
                case CavalryState.Recall:
                    inp.RallyComplete = (enemy == null || DistToEnemy(formation, enemy) >= RallyDistance)
                        || elapsed > RecallTimeout; // 拉到安全距离 / 超时兜底
                    break;
                case CavalryState.Reform:
                    inp.ReformComplete = elapsed >= ReformTimeout; // 重整完成（4s 集结）
                    break;
            }

            CavalryState next = CavalryWaveFSM.Advance(state, inp);
            if (next != state)
            {
                _states[formation] = next;
                _stateElapsed[formation] = 0f;
                IssueOrder(formation, next, enemy != null ? enemy.Formation : null);
                DecisionObservatory.Observe(DecisionObservatory.BCavChange); // T1 决策分支计数：编队 FSM 转移
                CombatLog.Log(string.Format(
                    "[F8] 骑兵编队(team {0}) {1} → {2}", formation.Team.TeamIndex, state, next));
            }
        }

        // F2 意图 → 机会窗映射：侧翼=敌方阵线缺口；追击=敌骑冲锋结束空窗
        private static OpportunityType MapOpportunity(IntentType intent)
        {
            switch (intent)
            {
                case IntentType.Flank: return OpportunityType.EnemyLineGap;
                case IntentType.Pursue: return OpportunityType.EnemyChargeEnded;
                default: return OpportunityType.None;
            }
        }

        private static float DistToEnemy(Formation formation, FormationQuerySystem enemy)
        {
            // P1-④ 统一入口：跨子系统敌我对比，双方显式同口径（CachedMedian）
            Vec2 a = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian);
            Vec2 b = enemy != null && enemy.Formation != null
                ? TeamGeometry.GetFormationPosition(enemy.Formation, TeamPosKind.CachedMedian)
                : a;
            return VecMath.Dist(a.x, a.y, b.x, b.y);
        }

        // 命令只在状态跃迁时下发一次
        private static void IssueOrder(Formation formation, CavalryState state, Formation targetFormation)
        {
            switch (state)
            {
                case CavalryState.Scout:
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop); // 侦察待命
                    break;
                case CavalryState.Setup:
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                    formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine); // 列阵
                    break;
                case CavalryState.Charge:
                    formation.SetMovementOrder(targetFormation != null
                        ? MovementOrder.MovementOrderChargeToTarget(targetFormation)
                        : MovementOrder.MovementOrderCharge);
                    break;
                case CavalryState.Recall:
                    formation.SetMovementOrder(MovementOrder.MovementOrderFallBack); // 后撤脱离
                    break;
                case CavalryState.Reform:
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                    formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderColumn); // 骑兵纵队待命再冲
                    break;
            }
        }
    }
}
