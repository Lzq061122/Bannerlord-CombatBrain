namespace CombatBrain.Ai
{
    using System.Collections.Generic;
    using TaleWorlds.Library;
    using TaleWorlds.MountAndBlade;

    // 编队位置口径（P1-④ 收敛决策）：
    //   - SmoothedAverage：f.SmoothedAverageUnitPosition（引擎平滑均值，连续无跳变）——
    //     主逻辑/感知/对手建模系：意图态势(CombatAiMissionLogic)、F6 走位(MovementGlue)、
    //     F7 求生(SurvivalGlue)、F9 远程(RangedGlue)、F2 对手建模(OpponentModelGlue)。
    //   - CachedMedian：f.CachedMedianPosition.AsVec2（引擎缓存中位数，抗离群、阵地锚点风格）——
    //     骑兵/编队层下令系：F8 骑兵波次(CavalryGlue)、F11 士气(MoraleGlue)、
    //     F3 保护(ProtectGlue)、F4 架枪(BraceGlue)。
    // 收敛规则：同一子系统内部同口径（维持现状）；跨子系统对比处（F8/F11/F3 的敌我编队距离、
    // 队中心 vs 编队位置）经本单一入口取数，双方口径在调用点显式声明，杜绝 4+ 套散装实现漂移。
    // 行为语义零变化：各口径数值与原直读逐字节一致；本类只收敛代码不改数值。
    // 反作弊闸：全部只读引擎公开可观察缓存属性（SmoothedAverageUnitPosition /
    // CachedMedianPosition @ Formation.cs L247），无越权信息。
    public enum TeamPosKind
    {
        SmoothedAverage,
        CachedMedian
    }

    // 队中心/编队位置统一工具（review-findings-2026-09-01 #10：原 4+ 套实现口径不一）
    public static class TeamGeometry
    {
        // 编队位置单一入口：口径由 kind 决定；null 编队 → Zero（调用方条件判断语义不变）
        public static Vec2 GetFormationPosition(Formation f, TeamPosKind kind)
        {
            if (f == null) return Vec2.Zero;
            return kind == TeamPosKind.CachedMedian
                ? f.CachedMedianPosition.AsVec2
                : f.SmoothedAverageUnitPosition;
        }

        // 编队集合中心聚合（供预过滤列表，如 ProtectGlue.CenterOf：仅跳 null，与原实现一致）
        public static bool TryCenterOf(IEnumerable<Formation> formations, TeamPosKind kind, out Vec2 center)
        {
            float sx = 0f, sy = 0f;
            int n = 0;
            if (formations != null)
            {
                foreach (Formation f in formations)
                {
                    if (f == null) continue;
                    Vec2 p = GetFormationPosition(f, kind);
                    sx += p.x;
                    sy += p.y;
                    n++;
                }
            }
            if (n == 0) { center = Vec2.Zero; return false; }
            center = new Vec2(sx / n, sy / n);
            return true;
        }

        // 队中心聚合（遍历 FormationsIncludingEmpty，跳 null 与空编队；team==null 或无一兵 → false）
        // 调用方如需「无一兵保持上次中心」等胶水状态回退，请在本返回 false 分支自行处理。
        public static bool TryGetTeamCenter(Team team, TeamPosKind kind, out Vec2 center)
        {
            if (team == null) { center = Vec2.Zero; return false; }
            float sx = 0f, sy = 0f;
            int n = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                Vec2 p = GetFormationPosition(f, kind);
                sx += p.x;
                sy += p.y;
                n++;
            }
            if (n == 0) { center = Vec2.Zero; return false; }
            center = new Vec2(sx / n, sy / n);
            return true;
        }
    }
}
