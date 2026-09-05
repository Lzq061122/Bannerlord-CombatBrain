using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Cavalry;
using CombatBrain.Coordination;

namespace CombatBrain.Ai
{
    // F8 骑兵多波次调度 —— 胶水层（编队级驱动）。
    // 职责：0.5s 低频驱动纯逻辑 CavalryWaveFSM（只读消费，零改动）：
    //   SCOUT(侦察) → SETUP(列阵) → CHARGE(冲锋) → RECALL(回撤) → REFORM(重整) → SETUP 闭环。
    //
    // 性能预算（三条铁律落地）：
    //   * 编队级聚合（聚合 > 遍历）：每 0.5s 只遍历各队非空编队（O(编队数)≈个位数），
    //     命令整编队，绝不逐兵每帧；无空间查询、无 MBList 遍历。
    //   * 剧场效应：接管玩家 150m（CavalryDriveRadius，t48 骑兵扩剧场）内的骑兵编队；
    //     150m 外仅暂停驱动（FSM 冻结），300m 外完整释放交回原版。
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

        private const float CavalryDriveRadius = 150f; // t48 骑兵剧场：冲锋距离远超步兵 70m 剧场——
                                                       // 70m 会把出半径的骑兵编队 FSM 状态删除，回场重走
                                                       // Scout→Setup，elapsed 永远攒不到超时 → 整场不冲（实测 P0）。
                                                       // 150m 与步兵 70m 区分；150m 外仅暂停驱动（不删状态不下命令）。
        private const float ReleaseRadius = 300f;      // 兜底：远离战场 2×剧场 → 完整释放（删状态+Advance 交回原版，
                                                       // 保留 P0-⑤ 防命令泄漏语义；正常冲锋不会到 300m 外）
        private const float ShieldwayDist = 10f;       // t58 盾墙挡路判定：己方步兵中心距冲锋线垂距 <10m 视为挡路
        private const float BypassClearance = 12f;     // t58 绕行侧偏余量：绕行点在挡路步兵对侧再让开 12m
        private const float BypassArriveDist = 8f;     // t58 到达绕行点判定距离（到位即正式冲锋）
        private const float BypassTimeout = 6f;        // t58 绕行超时兜底：超时直接转正式冲锋（防绕行卡死）
        private const float ImpactRadius = 6f;   // CHARGE 触敌判定半径（贴脸即回撤，防卡死）
        private const float RallyDistance = 30f; // RECALL 回撤达标距离
        private const float ChargeTimeout = 12f; // 各状态超时兜底（防任何卡死）
        private const float RecallTimeout = 6f;
        private const float ReformTimeout = 4f;
        private const float SetupTimeout = 8f;

        private float _sinceTick;
        private Func<Team, Formation, bool> _isEnemyFormationKnown; // X6 感知过滤委托（本拍生效，GetKnownEnemy/SelectRallyPoint 消费）
        private readonly Dictionary<Formation, CavalryState> _states = new Dictionary<Formation, CavalryState>();
        private readonly Dictionary<Formation, float> _stateElapsed = new Dictionary<Formation, float>();

        // t58 绕行子步骤状态：Charge 状态内先侧移绕开己方盾墙（到位/超时再正式冲锋）
        private readonly Dictionary<Formation, Formation> _pendingChargeTarget = new Dictionary<Formation, Formation>();
        private readonly Dictionary<Formation, Vec2> _pendingChargePt = new Dictionary<Formation, Vec2>();
        private readonly Dictionary<Formation, float> _pendingChargeElapsed = new Dictionary<Formation, float>();

        // 日志明细用：骑兵 FSM 当前状态名（无记录 → "-"，只读）
        public string GetStateName(Formation formation)
        {
            CavalryState s;
            return _states.TryGetValue(formation, out s) ? s.ToString() : "-";
        }

        /// <summary>
        /// 编队级低频驱动：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="playerCenter">玩家焦点中心（剧场上限基准）</param>
        /// <param name="getIntent">各队当前意图（F2 意图引擎输出，机会窗触发源）</param>
        public void Tick(Mission mission, float dt, Vec2 playerCenter, Func<Team, IntentType> getIntent,
            Func<Team, Formation, bool> isEnemyFormationKnown = null)
        {
            _isEnemyFormationKnown = isEnemyFormationKnown; // X6：本拍过滤委托（GetKnownEnemy/SelectRallyPoint 消费）
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            foreach (Team team in mission.Teams)
            {
                if (team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队豁免（MCM 可开关 t66）：玩家骑兵归玩家指挥，不接管波次
                IntentType intent = getIntent != null ? getIntent(team) : IntentType.Attack;

                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation == null || formation.CountOfUnits == 0) continue;
                    if (formation.QuerySystem == null || !formation.QuerySystem.IsCavalryFormation) continue; // 只接骑兵编队

                    Vec2 med = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian); // P1-④ 统一入口
                    float dist = VecMath.Dist(med.x, med.y, playerCenter.x, playerCenter.y);
                    if (dist > ReleaseRadius)
                    {
                        ReleaseOutsideRadius(formation); // 300m 外视为离开战场：完整释放（幂等，P0-⑤ 防命令残留保留）
                        continue;
                    }
                    if (dist > CavalryDriveRadius)
                    {
                        // t48：150m 外仅暂停驱动——FSM 状态与 elapsed 冻结保留（不删状态不下命令），
                        // 消除「出半径删状态→回场重走 Scout→Setup」的无限重置；冲锋跨出剧场也不被打断。
                        // 权衡：剧场外短暂保留本 mod 命令（如冲锋中）比交回原版更符合骑兵行为；300m 外兜底释放。
                        continue;
                    }

                    TickFormation(formation, intent, dt);
                }
            }
        }

        // 300m 外兜底释放（t48 收窄职责：150m 外仅暂停，不再删状态）：丢弃 FSM 状态 + 下发 Advance
        // 交回原版，防命令永久残留（P0-⑤ 语义保留）。幂等：无状态直接返回，重复调用无害。
        private void ReleaseOutsideRadius(Formation formation)
        {
            if (!_states.ContainsKey(formation)) return;
            _states.Remove(formation);
            _stateElapsed.Remove(formation);
            _pendingChargeTarget.Remove(formation); // t58 绕行子步骤随释放清理
            _pendingChargePt.Remove(formation);
            _pendingChargeElapsed.Remove(formation);
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

            // t58 绕行子步骤：Charge 状态下先侧移绕开己方盾墙（用户实测 P0：直线冲锋穿己方盾墙冲乱阵型）。
            // 到位/超时/目标消失 → 正式冲锋（楔形 Skein+ChargeToTarget）；绕行期间不做触敌/超时回撤
            // 判定（尚未接触敌军，ImpactFinished 无意义）。
            Formation pendingTarget;
            if (_pendingChargeTarget.TryGetValue(formation, out pendingTarget))
            {
                float pElapsed = _pendingChargeElapsed.TryGetValue(formation, out pElapsed) ? pElapsed : 0f;
                pElapsed += dt;
                _pendingChargeElapsed[formation] = pElapsed;
                Vec2 pt = _pendingChargePt[formation];
                Vec2 cur = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian);
                bool targetAlive = pendingTarget != null && pendingTarget.CountOfUnits > 0 && pendingTarget.QuerySystem != null;
                if (VecMath.Dist(cur.x, cur.y, pt.x, pt.y) <= BypassArriveDist || pElapsed > BypassTimeout || !targetAlive)
                {
                    _pendingChargeTarget.Remove(formation);
                    _pendingChargeElapsed.Remove(formation);
                    _pendingChargePt.Remove(formation);
                    IssueChargeOrder(formation, targetAlive ? pendingTarget : null);
                }
                return; // 绕行推进中：本拍结束
            }

            FormationQuerySystem enemy = GetKnownEnemy(formation);

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
                if (next == CavalryState.Charge)
                {
                    BeginCharge(formation, enemy != null ? enemy.Formation : null); // t58：先判定盾墙挡路→绕行或直冲
                }
                else if (next == CavalryState.Recall)
                {
                    BeginRecall(formation); // t83：Recall 回撤目标=重整点（优先级链+距离档位选址）
                }
                else
                {
                    IssueOrder(formation, next, enemy != null ? enemy.Formation : null);
                }
                DecisionObservatory.Observe(DecisionObservatory.BCavChange); // T1 决策分支计数：编队 FSM 转移
                CombatLog.Log(string.Format(
                    "[F8] 骑兵编队(team {0}) {1} → {2}", formation.Team.TeamIndex, state, next));
                Narrator.Notify(CombatAiMissionLogic.NarrateInfo(formation.Team,
                    Mission.Current != null ? Mission.Current.PlayerTeam : null),
                    "骑兵", // N1 叙事统一出口（编队级全量，FSM 跃迁一次性事件，正常模式显示）
                    string.Format("{S}骑兵 {1}→{2}", FeedbackText.Cavalry(state), FeedbackText.Cavalry(next)),
                    InGameFeedback.FormationColor, InGameFeedback.NotifyTactical);
            }
        }

        // t58 冲锋发起：先查己方步兵盾墙是否挡在冲锋线上——挡路则先侧移绕行（pending 登记，
        // 到位/超时后正式冲锋）；不挡路则直接楔形+ChargeToTarget。
        private void BeginCharge(Formation formation, Formation target)
        {
            Vec2 bypass;
            if (target != null && TryGetBypassPoint(formation, target, out bypass))
            {
                IssueMoveBypass(formation, bypass);
                _pendingChargeTarget[formation] = target;
                _pendingChargePt[formation] = bypass;
                _pendingChargeElapsed[formation] = 0f;
                return;
            }
            IssueChargeOrder(formation, target);
        }

        // 绕行判定：己方步兵编队中心投影落在冲锋线中段（t∈[0.2,0.85]）且垂距 < ShieldwayDist → 挡路。
        // 绕行点 = 冲锋线中点 + 背离步兵侧的法向偏移（清距 = 垂距不足量 + BypassClearance）。
        // 反作弊闸：仅用可观察位置/编队分类。
        private bool TryGetBypassPoint(Formation formation, Formation target, out Vec2 bypass)
        {
            bypass = Vec2.Zero;
            Vec2 cav = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian);
            Vec2 tgt = TeamGeometry.GetFormationPosition(target, TeamPosKind.CachedMedian);
            float dx = tgt.x - cav.x;
            float dy = tgt.y - cav.y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-3f) return false;
            float ux = dx / len;
            float uy = dy / len;
            float nx = -uy;
            float ny = ux;
            foreach (Formation f in formation.Team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (!f.QuerySystem.IsInfantryFormation) continue; // 盾墙=己方步兵
                Vec2 fp = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                float wx = fp.x - cav.x;
                float wy = fp.y - cav.y;
                float t = wx * ux + wy * uy;
                if (t < len * 0.2f || t > len * 0.85f) continue; // 不在冲锋线中段：不挡路
                float px = wx - ux * t;
                float py = wy - uy * t;
                float perp = (float)Math.Sqrt(px * px + py * py);
                if (perp >= ShieldwayDist) continue; // 离冲锋线够远：不算挡路
                float side = (px * nx + py * ny) >= 0f ? 1f : -1f; // 步兵在法向哪侧
                float clear = (ShieldwayDist - perp) + BypassClearance; // 补足垂距差 + 余量
                bypass = new Vec2(cav.x + ux * len * 0.5f - nx * side * clear,
                                  cav.y + uy * len * 0.5f - ny * side * clear);
                return true;
            }
            return false;
        }

        // 绕行移动（ProtectGlue.IssueMove 同款已核实模式）；无导航点时静默失败 → BypassTimeout 兜底转冲锋
        private static void IssueMoveBypass(Formation formation, Vec2 target)
        {
            WorldPosition wp = formation.CachedMedianPosition;
            wp.SetVec2(target);
            if (wp.GetNavMesh() == UIntPtr.Zero) return;
            CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                CommandKind.Move, 0, -1, target.x, target.y); // ATC-0 记录（不改命令）
            formation.SetMovementOrder(MovementOrder.MovementOrderMove(wp));
        }

        // t83 骑兵重整点选址：Recall 回撤目标=重整点（原 FallBack 无目标，会跑到地图边缘）。
        // 输入全可观察（TeamGeometry 编队中心/阵线/敌我距离），禁全图禁个体；无导航点保持原 FallBack 语义。
        private void BeginRecall(Formation formation)
        {
            Vec2 rally = SelectRallyPoint(formation);
            WorldPosition wp = formation.CachedMedianPosition;
            wp.SetVec2(rally);
            if (wp.GetNavMesh() == UIntPtr.Zero)
            {
                CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                    CommandKind.FallBack); // ATC-0 记录（不改命令）
                formation.SetMovementOrder(MovementOrder.MovementOrderFallBack);
                return;
            }
            CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                CommandKind.Move, 0, -1, rally.x, rally.y); // ATC-0 记录（不改命令）
            formation.SetMovementOrder(MovementOrder.MovementOrderMove(wp)); // 回撤到重整点
        }

        // 选址：候选=侧翼(50m)/主阵线后方(60m)/安全距离(100m)/目标附近安全区(60m)，
        // RallyPointSelector.SelectBest 按优先级链（侧翼>后方>安全>目标附近）+ 距离档位选择。
        // X6 感知接线：敌中心经 GetKnownEnemy 过滤（未知敌编队 → 无敌中心 → 背敌方向退化为默认朝向）。
        private Vec2 SelectRallyPoint(Formation formation)
        {
            Vec2 cav = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian);
            // 己方步兵线中心（主战线）
            Vec2 ic = Vec2.Zero; int infCount = 0;
            foreach (Formation f in formation.Team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (!f.QuerySystem.IsInfantryFormation) continue;
                Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                ic.x += p.x; ic.y += p.y; infCount++;
            }
            if (infCount == 0)
            {
                Vec2 c;
                ic = TeamGeometry.TryGetTeamCenter(formation.Team, TeamPosKind.CachedMedian, out c) ? c : cav;
            }
            else { ic.x /= infCount; ic.y /= infCount; }

            // 敌中心 → 背敌方向（安全方向）与侧翼方向
            Vec2 ec = Vec2.Zero;
            FormationQuerySystem enemy = GetKnownEnemy(formation); // X6：未知敌编队 → null（敌中心=0，背敌退化为默认朝向）
            if (enemy != null && enemy.Formation != null)
                ec = TeamGeometry.GetFormationPosition(enemy.Formation, TeamPosKind.CachedMedian);
            float dx = ic.x - ec.x, dy = ic.y - ec.y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            float bx = len > 1e-3f ? dx / len : 1f; // 背敌单位向量
            float by = len > 1e-3f ? dy / len : 0f;
            float px = -by, py = bx; // 侧翼方向（垂直背敌）

            var candidates = new RallyCandidate[]
            {
                new RallyCandidate { Priority = RallyPriority.Flank, DistanceToMainLine = 50f },
                new RallyCandidate { Priority = RallyPriority.BehindLine, DistanceToMainLine = 60f },
                new RallyCandidate { Priority = RallyPriority.SafeDistance, DistanceToMainLine = 100f },
                new RallyCandidate { Priority = RallyPriority.NearTarget, DistanceToMainLine = 60f },
            };
            RallyCandidate best = RallyPointSelector.SelectBest(candidates);
            switch (best.Priority)
            {
                case RallyPriority.Flank: return new Vec2(ic.x + px * 50f, ic.y + py * 50f);
                case RallyPriority.SafeDistance: return new Vec2(ic.x + bx * 100f, ic.y + by * 100f);
                case RallyPriority.NearTarget:
                case RallyPriority.BehindLine:
                default: return new Vec2(ic.x + bx * 60f, ic.y + by * 60f); // 后方/目标附近/降级均退到阵线后方 60m
            }
        }

        // 正式冲锋命令（楔形冲击阵型 + 指定目标）。
        // t58：v1.4.8 ArrangementOrder 8 成员核实（ArrangementOrder.cs L32-46）——楔形=Skein（无 Wedge 命名），
        // Charge 换 Skein 冲击阵（Recall/Reform 由 IssueOrder 恢复 FallBack/Column，Setup 恢复 Line）。
        private static void IssueChargeOrder(Formation formation, Formation target)
        {
            CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                CommandKind.Arrangement); // ATC-0 记录（ArrangementOrder 为引用类型不转 int）
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderSkein); // 楔形冲击阵
            CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                CommandKind.Charge, 0, target != null ? target.Index : -1); // ATC-0 记录（不改命令）
            formation.SetMovementOrder(target != null
                ? MovementOrder.MovementOrderChargeToTarget(target)
                : MovementOrder.MovementOrderCharge);
        }

        // F2 意图 → 机会窗映射（t48 拓宽）：进攻性意图均可触发冲锋——
        //   Attack/Flank/Harass=正面有敌即阵线缺口（EnemyLineGap）；Pursue=敌溃追击（EnemyChargeEnded）。
        //   修复前仅 Flank/Pursue 映射，而意图引擎几乎不产生这两个意图（实测整场只有
        //   BraceFormation/Defend/ProtectArcher）→ 机会窗恒 None、骑兵整场不冲。
        // 防守/重组意图（Defend/ProtectArcher/BraceFormation/Retreat/Reform）不冲（保持原语义）。
        private static OpportunityType MapOpportunity(IntentType intent)
        {
            switch (intent)
            {
                case IntentType.Attack:
                case IntentType.Flank:
                case IntentType.Harass: return OpportunityType.EnemyLineGap;
                case IntentType.Pursue: return OpportunityType.EnemyChargeEnded;
                default: return OpportunityType.None;
            }
        }

        // X6 感知接线：最近敌编队经 KnowledgeView 过滤——未知敌编队（未见/超期）视为无目标（null），
        //   保守降级=FSM 走超时兜底分支（Scout 超时推进 / 不盲目冲锋看不见的敌人）；null=不过滤=现状等价。
        private FormationQuerySystem GetKnownEnemy(Formation formation)
        {
            FormationQuerySystem enemy = formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation;
            if (enemy == null || enemy.Formation == null) return null;
            if (_isEnemyFormationKnown != null && !_isEnemyFormationKnown(formation.Team, enemy.Formation)) return null; // X6：未知敌编队不算目标
            return enemy;
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
                    CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry, CommandKind.Hold); // ATC-0 记录
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop); // 侦察待命
                    break;
                case CavalryState.Setup:
                    CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry, CommandKind.Hold); // ATC-0 记录
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                    CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                        CommandKind.Arrangement); // ATC-0 记录（ArrangementOrder 为引用类型不转 int）
                    formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine); // 列阵
                    break;
                case CavalryState.Charge:
                    IssueChargeOrder(formation, targetFormation); // t58：楔形冲击阵+冲锋命令（含绕行后的正式冲锋）
                    break;
                case CavalryState.Recall:
                    CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry, CommandKind.FallBack); // ATC-0 记录
                    formation.SetMovementOrder(MovementOrder.MovementOrderFallBack); // 后撤脱离
                    break;
                case CavalryState.Reform:
                    CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry, CommandKind.Hold); // ATC-0 记录
                    formation.SetMovementOrder(MovementOrder.MovementOrderStop);
                    CommandProvenance.Instance.Record(formation.Index, CommandSource.Cavalry,
                        CommandKind.Arrangement); // ATC-0 记录（ArrangementOrder 为引用类型不转 int）
                    formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderColumn); // 骑兵纵队待命再冲
                    break;
            }
        }
    }
}
