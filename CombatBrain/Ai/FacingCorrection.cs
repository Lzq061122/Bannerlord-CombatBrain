namespace CombatBrain.Ai
{
    // X3 编队朝向修正（FacingCorrection）—— 纯逻辑判定（可离线测），零 TaleWorlds 依赖。
    // 语义：接敌时让编队正面朝向敌方编队中心（玩家可见区"有脑子"的朝向治理，杜绝侧背对敌）。
    //   RBM Frontline.cs:179 同款思路（20m 接敌朝向修正）；本模块只产出朝向方向，不直接下令。
    // 输入：己方编队中心、目标点（t93 FormationTargeting 目标编队方向优先；无目标时胶水层用
    //   TeamGeometry 最近敌编队中心兜底）、上次下发方向、当前时刻（0.5s 缓存）。
    // 判定：
    //   ① 目标无效（距己方 < 1m，即目标==己方中心）→ 不修正（None 降级）；
    //   ② 剧场半径外（> TheaterRadius=150m）→ 不修正（剧场效应，远处交原版）；
    //   ③ 0.5s 缓存窗内 → 不重算（沿用上次方向，低频）；
    //   ④ 方向迟滞：与上次方向夹角 < AngleHysteresis（0.2rad，RBM BehaviorArcherFlank 同款阈值）
    //      → 不下发（防抖，避免朝向抖动）；
    //   ⑤ 以上通过 → 输出归一化方向 (dx, dy) 供胶水层 SetFacingOrder(FacingOrderLookAtDirection)。
    // 反作弊闸：己方/敌方编队中心均为引擎公开可观察位置（TeamGeometry 口径），无越权。
    // 分域：只产出朝向，不动 MovementOrder/ArrangementOrder（与 t79 阵型三态 / t89 接管独立）。
    // 玩家队豁免：ShouldExempt(teamIsPlayer, exemptionEnabled) 由胶水层按 MCM 开关调用（P0-④ 保留）。
    public static class FacingCorrection
    {
        public const float TheaterRadius = 150f;    // 剧场半径（沿用 150m 先例）
        public const float CacheSeconds = 0.5f;     // 编队级缓存窗（契约④ 0.5s 低频）
        public const float MinTargetDistance = 1f;  // 目标距己方 <1m = 目标==己方 → 无效
        public const float AngleHysteresis = 0.2f;  // 方向迟滞（弧度，RBM BehaviorArcherFlank 同款 0.2rad）

        /// <summary>玩家队豁免判定（P0-④ MCM 开关；true=该队跳过朝向修正）。</summary>
        public static bool ShouldExempt(bool teamIsPlayer, bool exemptionEnabled)
        {
            return exemptionEnabled && teamIsPlayer;
        }

        /// <summary>
        /// 朝向判定：needUpdate=true 时输出归一化方向 (dx,dy) 供 FacingOrderLookAtDirection。
        /// now-lastTime &lt; CacheSeconds → 缓存命中不重算；目标无效/剧场外/方向未变 → 不更新。
        /// </summary>
        public static bool DecideFacing(float ownX, float ownY, float targetX, float targetY,
            float lastDx, float lastDy, float now, float lastTime,
            out float dx, out float dy, out float dist)
        {
            dx = 0f; dy = 0f; dist = 0f;
            if (now - lastTime < CacheSeconds) return false; // ③ 0.5s 缓存窗

            float vx = targetX - ownX;
            float vy = targetY - ownY;
            dist = (float)System.Math.Sqrt(vx * vx + vy * vy);
            if (dist < MinTargetDistance) return false;      // ① 目标无效（==己方）
            if (dist > TheaterRadius) return false;          // ② 剧场半径外

            float ndx = vx / dist;
            float ndy = vy / dist;
            // ④ 方向迟滞：与上次方向夹角 < 0.2rad → 不下发（点积 > cos(0.2)≈0.98）
            if ((lastDx != 0f || lastDy != 0f) && (lastDx * ndx + lastDy * ndy) > 0.98f)
                return false;

            dx = ndx;
            dy = ndy;
            return true;
        }
    }
}
