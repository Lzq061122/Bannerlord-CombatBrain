namespace CombatBrain.Formations
{
    // X5-6 执行策略（X5ExecutionPolicy）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 职责：编队级槽位快照的可测门控集中地。
    //
    // CB-PROMPT-0038 §五.7（X5 逐兵执行腿退役）后保留内容：
    //   ③ Discipline 门控：MaintainSlots=false（Severe/Collapsed）→ 跳过指派/刷新
    //      （x5-discipline-assigner-redundancy 接线落点）。
    // 已退役（本类不再提供）：① Bench 回退闸 IsIndividualOverBudget、
    //   ② L3 个体补位开关 ShouldRunIndividualLeg、④ X5SlotCooldown 逐 agent 冷却——
    //   三者都服务于「逐兵向 Slot 目标 SetMovementDirection」的执行腿，随该腿一并删除
    //   （无消费者状态不得保留，见审计表 §2.5）。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public static class X5ExecutionPolicy
    {
        /// <summary>③ Discipline 门控：MaintainSlots=false（Severe/Collapsed）→ 跳过指派/刷新。</summary>
        public static bool ShouldRefreshSlots(DisciplineOutput discipline)
        {
            return discipline.MaintainSlots;
        }
    }
}
