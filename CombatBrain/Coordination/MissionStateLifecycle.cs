namespace CombatBrain.Coordination
{
    // atc4-snapshot-optimization OPT-4：Mission 状态生命周期（MissionStateLifecycle）——跨 Mission 静态/实例
    //   状态幂等清理的单一入口（有限拆分自 CombatAiMissionLogic.ClearMissionStaticState）。
    //   Mission 初始化与结束均须调用（Init + OnMissionStateFinalized/OnClearScene 双钩子，幂等）；
    //   CommandProvenance 为静态单例（串场防护）、InGameFeedback 静态去重、_suppressedAttack 静态抑制集合，
    //   以及对手模型/攻击抑制实例级一并清空——Agent/Formation 索引复用不得继承旧状态。
    public static class MissionStateLifecycle
    {
        /// <summary>跨 Mission 静态状态幂等清理（先聚合后清理；传实例级清理对象，静态单例内部处理）。</summary>
        public static void ClearStatic(CombatBrain.Ai.OpponentModelGlue opponent, CombatBrain.Ai.AttackSuppression attackSuppression)
        {
            CommandProvenance.Instance.Clear();
            CombatBrain.Ai.InGameFeedback.ClearAll();
            if (opponent != null) opponent.ClearAll();
            if (attackSuppression != null) attackSuppression.ClearAll(); // 联动清 _suppressedAttack（静态集合）
        }
    }
}
