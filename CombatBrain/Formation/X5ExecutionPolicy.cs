namespace CombatBrain.Formations
{
    // X5-6 执行策略（X5ExecutionPolicy）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 职责：胶水层 FormationSlotGlue 的可测门控/策略集中地：
    //   ① Bench 回退闸（铁律②）：500 人 [Bench] 个体段 avgAi 超 5ms → 回退个体补位腿
    //     （仅编队纪律内手段保留——集成轮实测清 x5-runtime-bench-debt）；
    //   ② L3 个体补位开关：仅 L3（玩家可见区）执行，且未豁免/未解阵/未超预算；
    //   ③ Discipline 门控：MaintainSlots=false（Severe/Collapsed）→ 跳过指派/刷新
    //     （x5-discipline-assigner-redundancy 接线落点：胶水按 Evaluate().MaintainSlots 门控，
    //      X5-4 SlotAssigner 不再自行折叠 Grade——冗余消除）；
    //   ④ X5 个体补位冷却（复用 X4 FrameReleaseCooldown 模式：键=agent.Index、秒数制、Remove 清理）。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public static class X5ExecutionPolicy
    {
        public const float IndividualLegBudgetMs = 5f; // 500 人红线：个体段 avgAi 超 5ms 回退个体腿

        /// <summary>① Bench 回退判定：avgAi（个体段）超 5ms → 回退。</summary>
        public static bool IsIndividualOverBudget(float avgAiMs)
        {
            return avgAiMs > IndividualLegBudgetMs;
        }

        /// <summary>
        /// ② L3 个体补位开关：仅 L3（玩家可见区）执行；玩家队豁免（P0-④）、X4 已解阵、
        /// Bench 超预算任一命中 → 跳过（交编队纪律内手段/原版）。
        /// </summary>
        public static bool ShouldRunIndividualLeg(int lodLevel, bool exempted, bool frameDisabled, bool budgetOver)
        {
            if (lodLevel < 3) return false;      // 剧场效应：L0-L2 交原版
            if (exempted) return false;          // 玩家队豁免（P0-④ MCM）
            if (frameDisabled) return false;     // X4 已解阵 agent 跳过（X4 语义独立保持）
            if (budgetOver) return false;        // Bench 回退闸：超预算回退个体腿
            return true;
        }

        /// <summary>③ Discipline 门控：MaintainSlots=false（Severe/Collapsed）→ 跳过指派/刷新。</summary>
        public static bool ShouldRefreshSlots(DisciplineOutput discipline)
        {
            return discipline.MaintainSlots;
        }
    }

    // X5 个体补位冷却字典（p3-1 设计落地：键=agent.Index，秒数制；RBM Frontline.cs:909 同款
    // aiDecisionCooldownDict；与 X4 FrameReleaseCooldown 同模式——SetMovementDirection 是
    // 逐 agent 个体腿的唯一例外，冷却窗压调用频率）。
    public sealed class X5SlotCooldown
    {
        public const float CooldownSeconds = 2f; // 冷却窗（秒）：个体补位后 2s 内不重发

        private readonly System.Collections.Generic.Dictionary<int, float> _last =
            new System.Collections.Generic.Dictionary<int, float>();

        public bool CanRelease(int agentIndex, float now)
        {
            float last;
            if (!_last.TryGetValue(agentIndex, out last)) return true;
            return now - last >= CooldownSeconds;
        }

        public void MarkReleased(int agentIndex, float now)
        {
            _last[agentIndex] = now;
        }

        public void Remove(int agentIndex)
        {
            _last.Remove(agentIndex);
        }
    }
}
