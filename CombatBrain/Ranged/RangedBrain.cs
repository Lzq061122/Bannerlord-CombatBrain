namespace CombatBrain.Ranged
{
    // F9 远程编队级弹药取舍 —— 纯逻辑（CB-PROMPT-0038 §五.6 退役后形态）。
    //
    // 退役内容（本文件原有，已删除）：
    //   * RangedBrain.SelectTarget / Score / RangedTarget / ArmorClass / AmmoKind
    //     —— 逐兵选敌打分是「单兵战术决策」，与产品边界冲突（选谁打交还原版 AI）。
    // 保留内容：
    //   * RangedFallback + DecideFallback：**编队级**取舍（编队是否继续射击 / 是否整体后撤 /
    //     是否转换任务），输入是编队聚合量（弹药余量）与引擎公开编队旗标（是否已转为近战编队态）。
    //     该结果只供编队级消费（日志/诊断/后续合法编队命令通道），不驱动任何单兵 API。
    public enum RangedFallback
    {
        KeepShooting,
        SwitchToMelee,
        Retreat
    }

    public static class RangedBrain
    {
        public const float AmmoLowThreshold = 0.15f;

        /// <summary>编队级取舍：弹药见底 + 已近战 → 转换任务；弹药见底 + 未近战 → 整体后撤。</summary>
        public static RangedFallback DecideFallback(float ammoRatio, bool inMeleeRange)
        {
            if (ammoRatio <= AmmoLowThreshold)
                return inMeleeRange ? RangedFallback.SwitchToMelee : RangedFallback.Retreat;
            return RangedFallback.KeepShooting;
        }
    }
}
