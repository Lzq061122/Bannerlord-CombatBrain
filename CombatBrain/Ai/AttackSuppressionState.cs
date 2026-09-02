using System.Collections.Generic;

namespace CombatBrain.Ai
{
    // T3 攻击抑制/恢复核心状态（纯逻辑，供 offline 回归与游戏侧 AttackSuppression 复用）。
    // 参照 OF 报告 3.2.2：存原值→置 0→恢复→重判 循环的"存原值"状态表（AIDecisionManager _attackChance）。
    // 键用 agent.Index（int），非 Agent 引用：防 GC/引用泄漏，与 CombatBrain 现有每 agent 字典风格一致。
    public sealed class AttackSuppressionState
    {
        private readonly Dictionary<int, float> _saved = new Dictionary<int, float>();

        public int Count { get { return _saved.Count; } }

        public bool IsSuppressed(int agentIndex)
        {
            return _saved.ContainsKey(agentIndex);
        }

        /// <summary>仅当未压缩时保存现值；已压缩返回 false（不重复覆盖原值，保证恢复回最初值）。</summary>
        public bool TrySave(int agentIndex, float currentValue)
        {
            if (_saved.ContainsKey(agentIndex)) return false;
            _saved[agentIndex] = currentValue;
            return true;
        }

        /// <summary>恢复：取出保存值并清除条目。未压缩返回 false。</summary>
        public bool TryRestore(int agentIndex, out float savedValue)
        {
            if (!_saved.TryGetValue(agentIndex, out savedValue))
            {
                return false;
            }
            _saved.Remove(agentIndex);
            return true;
        }

        /// <summary>Agent 生命周期清理：移除条目（不产生任何副作用）。</summary>
        public void Clear(int agentIndex)
        {
            _saved.Remove(agentIndex);
        }

        public void ClearAll()
        {
            _saved.Clear();
        }
    }
}