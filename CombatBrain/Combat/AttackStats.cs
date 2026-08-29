using System.Collections.Generic;

namespace CombatBrain.Combat
{
    // 攻击历史统计：滑动窗口内最近 N 次方向分布，用于预测下一刀方向。
    public sealed class AttackStats
    {
        private readonly int _window;
        private readonly Queue<AttackDir> _queue = new Queue<AttackDir>();
        private readonly int[] _counts = new int[4];

        public AttackStats(int window = 8)
        {
            _window = window;
        }

        public int Total { get; private set; }

        public int Window { get { return _window; } }

        public void Record(AttackDir dir)
        {
            if (dir == AttackDir.None) return;
            int di = (int)dir;
            if (di < 0 || di > 3) return;

            _queue.Enqueue(dir);
            _counts[di]++;
            Total++;

            while (_queue.Count > _window)
            {
                AttackDir popped = _queue.Dequeue();
                _counts[(int)popped]--;
                Total--;
            }
        }

        public AttackDir Predict(out float rawConfidence)
        {
            if (Total == 0)
            {
                rawConfidence = 0f;
                return AttackDir.Right;
            }

            int best = 0;
            for (int i = 1; i < 4; i++)
            {
                if (_counts[i] > _counts[best]) best = i;
            }
            rawConfidence = (float)_counts[best] / (float)Total;
            return (AttackDir)best;
        }
    }
}