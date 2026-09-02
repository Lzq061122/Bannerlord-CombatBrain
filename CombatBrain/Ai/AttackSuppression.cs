using TaleWorlds.MountAndBlade;

namespace CombatBrain.Ai
{
    // T3 攻击抑制/恢复模式工具（供 H3/H4 复用）。
    // 参照 OF 报告 3.2.2（AIDecisionManager.cs:62-98 SuppressAttackDecision/RestoreAttackDecision）：
    //   存原值 → 置 AIAttackOnDecideChance=0 → 每帧开头恢复 → 重新判定可能再抑制，一圈循环。
    // 用法（逐帧兜底写法）：
    //   AttackSuppression observer 挂在胶水层；每帧开头对相关兵 RestoreAll/针对索引 Restore，
    //   决策后若需禁攻则 Suppress(agent)，agent 消亡经 Remove(agent.Index) 清条目。
    //
    // 值来源：Agent.AgentDrivenProperties.AIAttackOnDecideChance（v1.4.8 AgentDrivenProperties.cs:972，
    // get 读 GetStat / set 走 SetStat）。本工具只做"保存/置零/恢复"，读值在决策点由 H3/H4 持有。
    //
    // 单线程说明：CombatBrain 全走 MissionBehavior 主线程事件钩子，无并发，故不带 OF 双锁；
    // 若未来接入多线程回调需补锁（参照 AIDecisionManager 双锁写法）。
    public sealed class AttackSuppression
    {
        private readonly AttackSuppressionState _state = new AttackSuppressionState();

        public AttackSuppression()
        {
        }

        public AttackSuppressionState State { get { return _state; } }

        /// <summary>抑制：存原 AIAttackOnDecideChance 并置 0。已抑制则原样保持。空/无驱动属性 agent 忽略。</summary>
        public void Suppress(Agent agent)
        {
            if (agent == null || agent.AgentDrivenProperties == null) return;
            if (_state.TrySave(agent.Index, agent.AgentDrivenProperties.AIAttackOnDecideChance))
            {
                agent.AgentDrivenProperties.AIAttackOnDecideChance = 0f;
            }
        }

        /// <summary>恢复：把保存的 AIAttackOnDecideChance 还原（若该 agent 处于抑制态）。幂等。</summary>
        public void Restore(Agent agent)
        {
            if (agent == null || agent.AgentDrivenProperties == null) return;
            float saved;
            if (_state.TryRestore(agent.Index, out saved))
            {
                agent.AgentDrivenProperties.AIAttackOnDecideChance = saved;
            }
        }

        /// <summary>查询某 agent 是否处于抑制态。</summary>
        public bool IsSuppressed(int agentIndex)
        {
            return _state.IsSuppressed(agentIndex);
        }

        /// <summary>Agent 生命周期清理：OnAgentRemoved 联动，清条目（不读/写 Agent 属性，安全）。</summary>
        public void Remove(int agentIndex)
        {
            _state.Clear(agentIndex);
        }

        public void ClearAll()
        {
            _state.ClearAll();
        }
    }
}