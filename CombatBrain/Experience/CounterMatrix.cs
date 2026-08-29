using System;
using System.Collections.Generic;

namespace CombatBrain.Experience
{
    // F10a 反制矩阵 + 置信度磨损 —— 纯逻辑，纯查表。
    // 结构 profile → vector<TacticScore>；本模块负责"吃瘪后调整"：连续被反制 N 次 → 置信度下降 → 改试次优打法。
    // 双向性：OnCountered/OnSuccess 不校验输入真伪，玩家佯动喂进的错误教训也是合法输入。
    public struct TacticReputation
    {
        public float Confidence; // 0..1
        public int FailStreak;   // 连续被反制次数
    }

    public sealed class CounterMatrix
    {
        public const float DecayFactor = 0.70f; // 每次被反制置信度乘以此值
        public const float RecoverStep = 0.15f; // 每次成功置信度回补

        private readonly int _failCap;
        private readonly Dictionary<int, TacticReputation> _rep = new Dictionary<int, TacticReputation>();

        public CounterMatrix(int failCap = 3)
        {
            _failCap = failCap;
        }

        public float GetConfidence(int tacticId)
        {
            TacticReputation r;
            return _rep.TryGetValue(tacticId, out r) ? r.Confidence : 1.0f;
        }

        // 连续被反制达到 failCap 即"打入冷宫"，本轮不再选用。
        public bool IsShunned(int tacticId)
        {
            TacticReputation r;
            return _rep.TryGetValue(tacticId, out r) && r.FailStreak >= _failCap;
        }

        public void OnCountered(int tacticId)
        {
            TacticReputation r = Get(tacticId);
            r.FailStreak++;
            r.Confidence = Math.Max(0f, r.Confidence * DecayFactor);
            _rep[tacticId] = r;
        }

        public void OnSuccess(int tacticId)
        {
            TacticReputation r = Get(tacticId);
            r.FailStreak = 0;
            r.Confidence = Math.Min(1f, r.Confidence + RecoverStep);
            _rep[tacticId] = r;
        }

        // 在候选战术中按置信度挑最佳；被冷宫的跳过；全被冷宫/空 → -1。
        public int PickBest(IEnumerable<int> candidates)
        {
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int id in candidates)
            {
                if (IsShunned(id)) continue;
                float s = GetConfidence(id);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = id;
                }
            }
            return best;
        }

        // 清空本场记忆(换一场战斗重新开始)。
        public void Reset()
        {
            _rep.Clear();
        }

        private TacticReputation Get(int tacticId)
        {
            TacticReputation r;
            if (!_rep.TryGetValue(tacticId, out r))
                r = new TacticReputation { Confidence = 1.0f, FailStreak = 0 };
            return r;
        }
    }
}