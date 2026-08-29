using System.Collections.Generic;

namespace CombatBrain.Perception
{
    // 迷雾知识视图：一切智能的上限。先让 AI“瞎”，再谈聪明。
    // seen 表按发现时间做置信度衰减并加噪声；Tick 合并新视界、衰减旧值、丢弃超时。
    public sealed class KnowledgeView
    {
        public struct SightedInfo
        {
            public float X;
            public float Y;
            public float SeenTime;
            public float Confidence;
        }

        private readonly Dictionary<int, SightedInfo> _seen = new Dictionary<int, SightedInfo>();
        private readonly float _maxAge;
        private readonly float _confidenceDecayPerSecond;
        private readonly float _noiseScale;

        public KnowledgeView(float maxAge = 25f, float confidenceDecayPerSecond = 0.08f, float noiseScale = 0.15f)
        {
            _maxAge = maxAge;
            _confidenceDecayPerSecond = confidenceDecayPerSecond;
            _noiseScale = noiseScale;
        }

        public int SeenCount { get { return _seen.Count; } }

        public void Observe(int id, float x, float y, float now)
        {
            SightedInfo info;
            if (!_seen.TryGetValue(id, out info)) info = new SightedInfo();
            info.X = x;
            info.Y = y;
            info.SeenTime = now;
            info.Confidence = 1f;
            _seen[id] = info;
        }

        public bool TryGet(int id, float now, out SightedInfo info)
        {
            if (!_seen.TryGetValue(id, out info)) return false;

            float age = now - info.SeenTime;
            if (age < 0f) age = 0f;
            info.Confidence = 1f - age * _confidenceDecayPerSecond;
            if (info.Confidence < 0f) info.Confidence = 0f;

            if (age > _maxAge)
            {
                _seen.Remove(id);
                return false;
            }

            // 旧值加噪声：越旧越飘，模拟记忆模糊
            float n = _noiseScale * age;
            info.X += (HashNoise(id, now) - 0.5f) * 2f * n;
            info.Y += (HashNoise(id + 7919, now) - 0.5f) * 2f * n;
            return true;
        }

        public void Tick(float now)
        {
            var stale = new List<int>();
            foreach (KeyValuePair<int, SightedInfo> kv in _seen)
            {
                if (now - kv.Value.SeenTime > _maxAge) stale.Add(kv.Key);
            }
            foreach (int id in stale) _seen.Remove(id);
        }

        private static float HashNoise(int seed, float t)
        {
            uint h = (uint)seed * 2654435761u;
            h ^= (uint)(t * 1000f);
            h *= 2654435761u;
            return (h & 0xFFFFu) / 65535f;
        }
    }
}