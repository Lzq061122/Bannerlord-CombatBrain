namespace CombatBrain.Ai
{
    // X0 决策延迟开关（观感补强派单文档 §X0）—— 纯逻辑（可离线测）。
    // DecisionDelayStrength ∈ [0,1]：0=果断（意图 hold 最短、切换余量 0%）、1=现状拟人
    //   （意图防抖 6s / 15% 余量），中间线性。只缩放参数不改逻辑结构。
    //
    // CB-PROMPT-0038 §五.4（F12 个体抖动退役）：本类**只保留指挥层迟滞**——意图切换 hold 与
    //   切换余量（编队任务切换/编队命令防抖口径）。原个体抖动缩放 ScaleProfile（Hesitate/
    //   Delay/Wobble）随 DecisionJitterGlue / Compliance.DecisionJitter 一并删除，不再挂在
    //   单兵移动与格挡入口。
    public static class DecisionDelay
    {
        public const float HoldMin = 1.5f;    // 果断档意图 hold（更果断）
        public const float HoldMax = 6f;      // 拟人档意图 hold（现状）
        public const float MarginMax = 0.15f; // 拟人档切换余量 15%（现状）

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>意图防抖 hold：6s(拟人) → 1.5s(果断)，按强度线性（指挥层）。</summary>
        public static float HoldTime(float strength)
        {
            return HoldMin + (HoldMax - HoldMin) * Clamp01(strength);
        }

        /// <summary>意图切换余量：15%(拟人) → 0%(果断)，按强度线性（指挥层）。</summary>
        public static float SwitchMargin(float strength)
        {
            return MarginMax * Clamp01(strength);
        }
    }
}
