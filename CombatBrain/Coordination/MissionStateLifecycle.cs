namespace CombatBrain.Coordination
{
    // atc4-snapshot-optimization OPT-4：Mission 状态生命周期（MissionStateLifecycle）——跨 Mission 静态/实例
    //   状态幂等清理的单一入口（有限拆分自 CombatAiMissionLogic.ClearMissionStaticState）。
    //   Mission 初始化与结束均须调用（Init + OnMissionStateFinalized/OnClearScene 双钩子，幂等）；
    //   CommandProvenance 为静态单例（串场防护）、InGameFeedback 静态去重，
    //   以及对手模型实例级一并清空——Agent/Formation 索引复用不得继承旧状态。
    //   CB-PROMPT-0038：_suppressedAttack 静态抑制集合与 AttackSuppression 已随 X2/F4 退役并移出本入口。
    public static class MissionStateLifecycle
    {
        /// <summary>跨 Mission 静态状态幂等清理（先聚合后清理；传实例级清理对象，静态单例内部处理）。
        /// ATC-5R：租约管理器纳入清理（leaseGlue.ReleaseAll 幂等释放全部 CB 租约并交还原生 AI）。</summary>
        public static void ClearStatic(CombatBrain.Ai.OpponentModelGlue opponent,
            CombatBrain.Ai.FormationControlLeaseGlue leaseGlue = null, TaleWorlds.MountAndBlade.Mission mission = null)
        {
            CommandProvenance.Instance.Clear();
            CombatBrain.Ai.InGameFeedback.ClearAll();
            if (opponent != null) opponent.ClearAll();
            if (leaseGlue != null) leaseGlue.ReleaseAll(mission, LeaseReleaseReason.MissionEnding); // ATC-5R 租约幂等释放
        }
    }
}
