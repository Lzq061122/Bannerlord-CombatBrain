using CombatBrain.Compliance;

namespace CombatBrain.Ai
{
    // X0 决策延迟开关（观感补强派单文档 §X0）—— 纯逻辑缩放（可离线测）。
    // DecisionDelayStrength ∈ [0,1]：0=果断（无犹豫/无延迟/无走形、意图 hold 最短）、
    //   1=现状拟人（F12 抖动 + 意图防抖 6s/15%）、中间线性。只缩放参数不改逻辑结构；
    //   反作弊闸不变（延迟是行为参数，非信息获取）。
    public static class DecisionDelay
    {
        public const float HoldMin = 1.5f;    // 果断档意图 hold（更果断）
        public const float HoldMax = 6f;      // 拟人档意图 hold（现状）
        public const float MarginMax = 0.15f; // 拟人档切换余量 15%（现状）

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>意图防抖 hold：6s(拟人) → 1.5s(果断)，按强度线性。</summary>
        public static float HoldTime(float strength)
        {
            return HoldMin + (HoldMax - HoldMin) * Clamp01(strength);
        }

        /// <summary>意图切换余量：15%(拟人) → 0%(果断)，按强度线性。</summary>
        public static float SwitchMargin(float strength)
        {
            return MarginMax * Clamp01(strength);
        }

        /// <summary>个体抖动系数按强度缩放（0=无犹豫/无延迟/无走形，1=现状）。</summary>
        public static ComplianceProfile ScaleProfile(ComplianceProfile p, float strength)
        {
            float s = Clamp01(strength);
            p.HesitateProb *= s;
            p.DelayBase *= s;
            p.Wobble *= s;
            return p;
        }
    }
}
