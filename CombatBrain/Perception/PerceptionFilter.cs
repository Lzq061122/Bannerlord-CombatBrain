namespace CombatBrain.Perception
{
    // X6 感知过滤判定（PerceptionFilter）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 职责：把「迷雾开关 × KnowledgeView 查询结果」折叠成 Glue 可直接消费的布尔
    //   （敌编队位置/分类是否可视为已知）。X6 接线后各 Glue 的敌编队读取改为：
    //   未见/超期（maxAge=25f、confidenceDecayPerSecond=0.08f，KnowledgeView 构造参数）→
    //   IsKnown=false → Glue 退回保守行为（有降级路径，不得退化成「什么都不敢做」）。
    // 语义：
    //   * 开关关闭（perceptionEnabled=false）→ 恒 true = 现状（全可见，与 X6 前行为一致，
    //     断言「开关关闭时行为与现状一致」）；
    //   * 开关开启 → 依赖 KnowledgeView 查询（visible=true 才 true；未见/超期 → false → 保守降级）。
    // 本类只做判定折叠，不碰 KnowledgeView 内部（查询由调用方完成）。
    public static class PerceptionFilter
    {
        /// <summary>
        /// 折叠判定：perceptionEnabled=false 恒 true（现状等价）；开启时 visible（KnowledgeView.TryGet 成功）。
        /// </summary>
        public static bool IsKnown(bool perceptionEnabled, bool visible)
        {
            if (!perceptionEnabled) return true; // 关闭 = 全可见 = 现状（X6 只减少信息，开关关不改变行为）
            return visible;
        }
    }
}