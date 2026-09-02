using System;
using System.Collections.Generic;
using System.Text;

namespace CombatBrain.Ai
{
    // T1 决策分支计数器（参照 OF FrontlineDebug：Count + 周期导出）。纯逻辑零依赖，被 OfflineTests 链接回归。
    public sealed class DecisionBranchTally
    {
        private readonly List<string> _names;
        private readonly Dictionary<string, int> _index;
        private readonly int[] _counts;
        private readonly List<int> _order;
        private readonly StringBuilder _sb;
        private int _total;

        public DecisionBranchTally(string[] branches)
        {
            _names = new List<string>(branches);
            _index = new Dictionary<string, int>(branches.Length, StringComparer.Ordinal);
            _counts = new int[branches.Length];
            _order = new List<int>(branches.Length);
            _sb = new StringBuilder(160);
            for (int i = 0; i < branches.Length; i++) _index[branches[i]] = i;
        }

        public int BranchCount { get { return _counts.Length; } }
        public int TotalObserved { get { return _total; } }

        public int DistinctObserved
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _counts.Length; i++) if (_counts[i] > 0) n++;
                return n;
            }
        }

        public void Observe(string branch)
        {
            int idx;
            if (!_index.TryGetValue(branch, out idx)) return;
            _counts[idx]++;
            _total++;
        }

        public string SnapshotAndReset()
        {
            _order.Clear();
            for (int i = 0; i < _counts.Length; i++) _order.Add(i);
            _order.Sort((a, b) => string.CompareOrdinal(_names[a], _names[b]));

            _sb.Length = 0;
            bool first = true;
            for (int k = 0; k < _order.Count; k++)
            {
                int i = _order[k];
                int c = _counts[i];
                if (c == 0) continue;
                if (!first) _sb.Append(' ');
                first = false;
                _sb.Append(_names[i]);
                _sb.Append('=');
                _sb.Append(c);
                _counts[i] = 0;
            }
            _total = 0;
            return first ? string.Empty : _sb.ToString();
        }

        public void Reset()
        {
            Array.Clear(_counts, 0, _counts.Length);
            _total = 0;
        }
    }
}