using System;
using System.Collections.Generic;

namespace CombatBrain.Morale
{
    // F11a 士气传导 —— 纯逻辑。
    // 士气不是纯个体量：邻队士气互相拉扯，邻队脱阵(崩溃)会传染拖低本方士气。
    public struct FormationMorale
    {
        public float Moral;      // 0..1 本队士气(滑动平均)
        public float BreakRate;  // 0..1 本队脱阵/倒地斜率
    }

    public static class MoralePropagation
    {
        public const float DragWeight = 0.15f;        // 邻队士气拉扯强度
        public const float ContagionWeight = 0.40f;   // 邻队脱阵传染强度
        public const float CollapseThreshold = 0.30f; // 低于此值视为崩溃临界
        public const float BreakRateCollapse = 0.50f; // 脱阵斜率高于此值视为崩溃临界

        // 邻队对某队士气的净影响，返回新的士气 0..1。
        public static float Propagate(float ownMoral, float neighborMoralAvg, float neighborBreakRate)
        {
            float drag = (neighborMoralAvg - ownMoral) * DragWeight;
            float contagion = neighborBreakRate * ContagionWeight;
            return Clamp01(ownMoral + drag - contagion);
        }

        // 是否已到崩溃临界(触发请求撤退/REFORM)。
        public static bool AtCollapse(float moral, float breakRate)
        {
            return moral < CollapseThreshold || breakRate > BreakRateCollapse;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }

    // 滑动窗口聚合：统计窗口内脱阵事件，输出归一化脱阵率(pen_b 的采集原语)。
    public sealed class BreakRateWindow
    {
        private readonly float _windowSeconds;
        private readonly int _maxEvents;
        private readonly Queue<float> _events = new Queue<float>();

        public BreakRateWindow(float windowSeconds = 5f, int maxEvents = 10)
        {
            _windowSeconds = windowSeconds;
            _maxEvents = maxEvents;
        }

        public int Count { get { return _events.Count; } }

        public void Record(float now)
        {
            _events.Enqueue(now);
            Trim(now);
        }

        // 归一化脱阵率 0..1：达到 maxEvents 即满额。
        public float Rate(float now)
        {
            Trim(now);
            return Math.Min(1f, (float)_events.Count / _maxEvents);
        }

        private void Trim(float now)
        {
            while (_events.Count > 0 && now - _events.Peek() > _windowSeconds)
                _events.Dequeue();
        }
    }
}