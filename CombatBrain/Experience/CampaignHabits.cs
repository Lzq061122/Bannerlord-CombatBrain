using System.Collections.Generic;

namespace CombatBrain.Experience
{
    // F10b 战役级经验(持久套路记忆) —— 纯逻辑。
    // 记录玩家惯用套路(如"每次开局先左翼冲骑射")的频次，下次开战部分预判，逼玩家换招。
    // 落盘由胶水层负责；本类只管频次统计与预测。
    public sealed class CampaignHabits
    {
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

        public void Record(int playId)
        {
            int c;
            _counts.TryGetValue(playId, out c);
            _counts[playId] = c + 1;
        }

        public int CountOf(int playId)
        {
            int c;
            return _counts.TryGetValue(playId, out c) ? c : 0;
        }

        // 预测最可能复现的套路；平局取 id 小者；无记录返回 -1。
        public int Predict()
        {
            int best = -1;
            int bestCount = 0;
            foreach (var kv in _counts)
            {
                if (kv.Value > bestCount || (kv.Value == bestCount && (best == -1 || kv.Key < best)))
                {
                    best = kv.Key;
                    bestCount = kv.Value;
                }
            }
            return best;
        }

        public void Clear()
        {
            _counts.Clear();
        }
    }
}