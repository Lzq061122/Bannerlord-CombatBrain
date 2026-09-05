using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain;
using CombatBrain.Intent;

namespace CombatBrain.Ai
{
    // F3 ProtectGlue 保护核心输出（H2，P3 级）——ProtectArcher 意图的执行层（pipeline §三 F3）。
    // 目标：远程退向步兵阵后 + 步兵殿后档 + 骑兵回援。
    // 触发：本队意图 == ProtectArcher（F2 目标识别）或己方弓手受压（敌编队距弓 ≤45m，防御自保）；
    //       仅对己方编队下令（0.5s 低频 + 3m 防抖防命令风暴；剧场上限 70m 内响应）。
    // 动作：
    //   * 远程编队 → MovementOrderMove(退后目标)（ProtectPlan.ChooseRetreatTarget，退后≠溃逃）
    //   * 步兵编队 → MovementOrderMove(殿后档)（ProtectPlan.ChooseScreenTarget）
    //   * 骑兵编队 → 敌距弓 ≤40m 时 MovementOrderMove(回援弓阵)；否则不干预，交给 F8 波次指挥（CavalryGlue）
    // 反作弊闸（t12 行19 / t16 附 L131）：只读 己方编队位置(CachedMedianPosition)、敌编队位置
    //   （ClosestSignificantlyLargeEnemyFormation 引擎缓存/编队位置聚合）、己方意图(F2)；
    //   禁读：敌编队 MovementOrder/行为权重/士气值/血量/意图状态（proposal L166）。
    // 性能：0.5s 低频 O(己方编队数)，无逐兵遍历、无每拍分配（复用列表）。
    // API 核实（BannerlordSage，v1.4.8）：
    //   WorldPosition.SetVec2(Vec2) / GetNavMesh() @ WorldPosition.cs（BannerlordSage read_csharp_type）
    //   WorldPosition(Scene,Vec3) / (Scene,UIntPtr,Vec3,bool) 构造 @ 同上
    //   MovementOrder.MovementOrderMove(WorldPosition) @ MovementOrder.cs（BannerlordSage read_csharp_type）
    //   Formation.SetMovementOrder(MovementOrder) @ Formation.cs L685（CavalryGlue.cs:23 已用）
    //   FormationQuerySystem.IsInfantryFormation @ RBM OverrideBehaviorAdvance.cs:149；IsRangedFormation @ Utilities.cs:433
    //   IsCavalryFormation / IsRangedCavalryFormation @ RBM Utilities.cs:792；ClosestSignificantlyLargeEnemyFormation
    //   @ RBM OverrideBehaviorVanguard.cs:36 / FormationQuerySystem.cs L275；IsPositionInsideBoundaries @ RBM RBMBehaviorArcherFlank.cs:150
    //   Formation.CachedMedianPosition @ CavalryGlue.cs:68（F8 已用）；Mission.Teams 遍历 @ MoraleGlue.cs:76
    //   Team.IsPlayerTeam @ Team.cs L40（v1.4.8，P0-④ 玩家队豁免统一判断）
    public sealed class ProtectGlue
    {
        public const float TickInterval = 0.5f;
        private const float DriveRadius = 70f; // 剧场上限（与 MoraleGlue DriveRadius 对齐）

        private float _sinceTick;
        private readonly Dictionary<int, bool> _protecting = new Dictionary<int, bool>(); // 队 → 保护中
        private readonly List<Formation> _ranged = new List<Formation>();   // 复用列表（零分配）
        private readonly List<Formation> _infantry = new List<Formation>();
        private readonly List<Formation> _cavalry = new List<Formation>();

        /// <summary>低频驱动：CombatAiMissionLogic.OnMissionTick 调用，内部按 TickInterval 节流。</summary>
        /// <param name="isEnemyFormationKnown">X6 感知接线：敌编队是否已知（观察者队+敌编队，KnowledgeView 过滤，可 null=不过滤=现状）。</param>
        public void Tick(Mission mission, float dt, float now, Vec2 playerCenter, Func<Team, IntentType> getIntent,
            Func<Team, Formation, bool> isEnemyFormationKnown = null)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            foreach (Team team in mission.Teams)
            {
                if (team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队豁免（MCM 可开关 t66）：不对玩家编队下令（与玩家指挥打架）
                IntentType intent = getIntent != null ? getIntent(team) : IntentType.Attack;
                bool active = intent == IntentType.ProtectArcher || HasPressedRanged(team, isEnemyFormationKnown); // F2 目标识别 or 受压自保
                if (!active)
                {
                    bool wasOff; // 游戏内反馈：保护释放边沿（上拍保护中→本拍解除一次性输出）
                    if (_protecting.TryGetValue(team.TeamIndex, out wasOff) && wasOff)
                        Narrator.Notify(CombatAiMissionLogic.NarrateInfo(team,
                            Mission.Current != null ? Mission.Current.PlayerTeam : null),
                            "保护", "{S}威胁解除，恢复进攻", InGameFeedback.FormationColor, InGameFeedback.NotifyTactical); // N1 叙事统一出口
                    _protecting[team.TeamIndex] = false;
                    continue;
                }
                Vec2 tc = TeamCenter(team);
                if (VecMath.Dist(playerCenter.x, playerCenter.y, tc.x, tc.y) > DriveRadius)
                {
                    ReleaseOutsideRadius(team); // P0-⑤ 剧场外撤销已下发 Move 命令（幂等，仅首拍生效）
                    continue;
                }
                bool wasOn; // 游戏内反馈：保护激活边沿（上拍未保护→本拍激活一次性输出）
                if (!_protecting.TryGetValue(team.TeamIndex, out wasOn) || !wasOn)
                    Narrator.Notify(CombatAiMissionLogic.NarrateInfo(team,
                        Mission.Current != null ? Mission.Current.PlayerTeam : null),
                        "保护", "{S}弓手受胁，全军护射", InGameFeedback.FormationColor, InGameFeedback.NotifyTactical); // N1 叙事统一出口
                _protecting[team.TeamIndex] = true;
                Execute(team, mission);
            }
        }

        // P0-⑤ 剧场外命令泄漏修复：出半径后 Move 停刷，但已下发的 Move/Stop 若不撤销会残留
        // （原版 TeamAI 未必覆盖）。仅当上拍仍在保护中时下发一次 Advance 交回原版；_protecting
        // 随即复位 → 幂等（重复调用无害、不刷命令）。
        private void ReleaseOutsideRadius(Team team)
        {
            bool wasProtecting;
            if (!_protecting.TryGetValue(team.TeamIndex, out wasProtecting) || !wasProtecting) return;
            _protecting[team.TeamIndex] = false;
            Narrator.Notify(CombatAiMissionLogic.NarrateInfo(team,
                Mission.Current != null ? Mission.Current.PlayerTeam : null),
                "保护", "{S}解除护射（超出战场）", InGameFeedback.FormationColor, InGameFeedback.NotifyTactical); // N1 叙事统一出口（剧场外释放边沿）
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                f.SetMovementOrder(MovementOrder.MovementOrderAdvance); // 交回原版默认推进（MovementOrder.cs L59，v1.4.8 核实）
            }
        }

        /// <summary>保护状态查询（诊断/测试用）。</summary>
        public bool IsProtecting(int teamIndex)
        {
            bool v;
            return _protecting.TryGetValue(teamIndex, out v) && v;
        }

        private void Execute(Team team, Mission mission)
        {
            Vec2 ec;
            if (!TryEnemyCenter(team, mission, out ec)) return; // 无敌人

            // 己方编队分类（一次遍历，O(编队数)）
            _ranged.Clear();
            _infantry.Clear();
            _cavalry.Clear();
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (f.QuerySystem.IsRangedFormation || f.QuerySystem.IsRangedCavalryFormation) _ranged.Add(f);
                else if (f.QuerySystem.IsInfantryFormation) _infantry.Add(f);
                else if (f.QuerySystem.IsCavalryFormation) _cavalry.Add(f);
            }
            if (_ranged.Count == 0) return; // 无远程可护

            Vec2 ic = CenterOf(_infantry);
            float clearance = ProtectPlan.RetreatClearance(SpreadOf(_infantry));
            Vec2 rc = CenterOf(_ranged);

            // ① 远程退向步兵阵后（退后≠溃逃：目标是阵后受保护射击位；未受压不打断）
            foreach (Formation r in _ranged)
            {
                Vec2 rp = TeamGeometry.GetFormationPosition(r, TeamPosKind.CachedMedian); // P1-④ 统一入口
                float enemyToR = VecMath.Dist(ec.x, ec.y, rp.x, rp.y);
                if (!ProtectPlan.IsUnderPressure(enemyToR)) continue;
                float tx, ty;
                ProtectPlan.ChooseRetreatTarget(ec.x, ec.y, ic.x, ic.y, clearance, out tx, out ty);
                IssueMove(r, tx, ty, mission);
            }

            // ② 步兵殿后档：站到敌→弓连线之间（挡在弓手前面）
            foreach (Formation inf in _infantry)
            {
                float tx, ty;
                ProtectPlan.ChooseScreenTarget(ec.x, ec.y, rc.x, rc.y, out tx, out ty);
                IssueMove(inf, tx, ty, mission);
            }

            // ③ 骑兵回援：敌距弓 ≤40m 才干预（阈值未达交给 F8 波次指挥）
            foreach (Formation cav in _cavalry)
            {
                float enemyToR = VecMath.Dist(ec.x, ec.y, rc.x, rc.y);
                if (!ProtectPlan.ShouldReturnCavalry(enemyToR)) continue;
                IssueMove(cav, rc.x, rc.y, mission);
            }
        }

        // 下令：3m 防抖 → 界内校验 → CachedMedianPosition 复制 + SetVec2 重定向 → 导航点校验 → MoveOrder
        // （SetVec2/GetNavMesh 校验顺序同 RBM RBMBehaviorArcherFlank.cs:149-150 / OverrideBehaviorProtectFlank.cs:186）
        private void IssueMove(Formation f, float tx, float ty, Mission mission)
        {
            Vec2 cur = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // P1-④ 统一入口
            if (!ProtectPlan.ShouldIssueOrder(VecMath.Dist(cur.x, cur.y, tx, ty))) return; // 防抖
            Vec2 target = new Vec2(tx, ty);
            if (!mission.IsPositionInsideBoundaries(target)) return; // 界外不下令（RBM 同款）
            // P1-④ 注：此处需完整 WorldPosition（SetVec2 重定向 + 导航点校验），非 Vec2 口径范畴，保留直读
            WorldPosition wp = f.CachedMedianPosition;
            wp.SetVec2(target);
            if (wp.GetNavMesh() == UIntPtr.Zero) return; // 无导航点（RBM 同款）
            f.SetMovementOrder(MovementOrder.MovementOrderMove(wp));
        }

        // 弓手受压（队级预检）：任意己方远程编队的最近敌编队距其 ≤45m（引擎缓存，只读位置）。
        // X6 感知接线：isEnemyFormationKnown 非空时，未知敌编队（KnowledgeView 未见/超期）不视为受压——
        // 保守降级=不激活保护（交原版），避免对看不见的威胁过度响应；null=不过滤=现状等价。
        private static bool HasPressedRanged(Team team, Func<Team, Formation, bool> isEnemyFormationKnown)
        {
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (!f.QuerySystem.IsRangedFormation && !f.QuerySystem.IsRangedCavalryFormation) continue;
                FormationQuerySystem enemy = f.QuerySystem.ClosestSignificantlyLargeEnemyFormation;
                if (enemy == null || enemy.Formation == null) continue;
                if (isEnemyFormationKnown != null && !isEnemyFormationKnown(team, enemy.Formation)) continue; // X6：未知敌编队不算受压
                // P1-④ 统一入口：跨子系统敌我对比，双方显式同口径（CachedMedian）
                Vec2 a = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                Vec2 b = TeamGeometry.GetFormationPosition(enemy.Formation, TeamPosKind.CachedMedian);
                if (ProtectPlan.IsUnderPressure(VecMath.Dist(a.x, a.y, b.x, b.y))) return true;
            }
            return false;
        }

        // 敌队中心：全部非本队编队位置均值（编队级聚合，可观察；无 → false）。
        private static bool TryEnemyCenter(Team own, Mission mission, out Vec2 ec)
        {
            ec = Vec2.Zero;
            float sx = 0f, sy = 0f;
            int n = 0;
            foreach (Team team in mission.Teams)
            {
                if (team == null || team == own || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队不纳入敌队聚合（MCM 可开关 t66：豁免关闭=玩家队正常纳入敌队聚合）
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0) continue;
                    Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // P1-④ 统一入口
                    sx += p.x; sy += p.y; n++;
                }
            }
            if (n == 0) return false;
            ec = new Vec2(sx / n, sy / n);
            return true;
        }

        private static Vec2 TeamCenter(Team team)
        {
            // P1-④ 口径收敛：几何聚合进 TeamGeometry（CachedMedian 口径单一实现，原私有重复版删除）
            Vec2 c;
            return TeamGeometry.TryGetTeamCenter(team, TeamPosKind.CachedMedian, out c) ? c : Vec2.Zero;
        }

        private static Vec2 CenterOf(List<Formation> list)
        {
            // P1-④ 口径收敛：列表为已过滤编队集合（仅跳 null，与原实现一致）
            Vec2 c;
            return TeamGeometry.TryCenterOf(list, TeamPosKind.CachedMedian, out c) ? c : Vec2.Zero;
        }

        // 编队离散度代理（阵宽）：中心平均距离。
        private static float SpreadOf(List<Formation> list)
        {
            Vec2 c = CenterOf(list);
            float sum = 0f;
            int n = 0;
            foreach (Formation f in list)
            {
                if (f == null) continue;
                Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // P1-④ 统一入口
                sum += VecMath.Dist(p.x, p.y, c.x, c.y);
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }
    }
}
