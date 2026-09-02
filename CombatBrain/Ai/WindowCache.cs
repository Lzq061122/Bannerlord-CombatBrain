using System;
using System.Collections.Generic;

namespace CombatBrain.Ai
{
    // T4 性能工具箱：0.5s 窗口缓存（带锁，参照 OF 报告 3.3 Utilities.cs 带锁缓存模式）。
    // 对反复计算的编队层聚合，缓存 key→value 在 Window 秒窗口内复用；窗口过期（或时间回拨）
    // 时整体清空重算。全程共享字典操作在 lock 内（T0 红线：禁无锁共享字典）。
    //
    // 典型用法（OF 式双检，compute 在锁外）：
    //   if (cache.TryGet(key, now, out v)) return v;
    //   v = Compute(key);                // 昂贵聚合，单线程主线程执行
    //   cache.Set(key, v, now);
    //   return v;
    // 线程安全说明：TryGet/Set/Clear 各自整方法持锁；重复 key 并发 compute 容忍 last-write-wins。
    public sealed class WindowCache<T>
    {
        public const float DefaultWindow = 0.5f;

        private readonly Dictionary<int, T> _cache = new Dictionary<int, T>();
        private readonly object _lock = new object();
        private readonly float _window;
        private float _stamp;

        public WindowCache(float window = DefaultWindow)
        {
            _window = window;
            _stamp = float.MinValue; // 首读必过期
        }

        public int Count { get { lock (_lock) return _cache.Count; } }

        /// <summary>按当前时间取缓存。窗口内命中返回 true；过期/回拨则清空并置新窗口起点，返回 false。</summary>
        public bool TryGet(int key, float now, out T value)
        {
            lock (_lock)
            {
                if (now - _stamp > _window || now < _stamp)
                {
                    _cache.Clear();
                    _stamp = now;
                    value = default(T);
                    return false;
                }
                return _cache.TryGetValue(key, out value);
            }
        }

        /// <summary>写入缓存（更新窗口起点语义：只在命中路径更新过 stamp；此处保持幂等）。</summary>
        public void Set(int key, T value)
        {
            lock (_lock)
            {
                _cache[key] = value;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _stamp = float.MinValue;
            }
        }
    }
}