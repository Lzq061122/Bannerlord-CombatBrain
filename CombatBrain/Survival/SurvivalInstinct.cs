using System.Collections.Generic;

namespace CombatBrain.Survival
{
    // F7 求生本能 —— 挂在个体状态机最高优先级分支。
    // 纯逻辑：hpRatio / 是否被集火 / 背后有无安全区 → 决定是否退向友军。
    public enum SurvivalAction
    {
        Fight,
        Hold,
        RetreatToAllies
    }

    public struct SurvivalContext
    {
        public float HpRatio;     // 0..1
        public bool BeingFocused; // 被集火
        public bool SafeBehind;   // 背后有安全区/友军阵线
    }

    public static class SurvivalInstinct
    {
        public const float HpThreshold = 0.30f;

        public static SurvivalAction Decide(SurvivalContext c)
        {
            if (c.HpRatio < HpThreshold && c.BeingFocused && !c.SafeBehind)
                return SurvivalAction.RetreatToAllies;
            if (c.HpRatio < HpThreshold)
                return SurvivalAction.Hold; // 低血但背后安全 → 稳住不跑
            return SurvivalAction.Fight;
        }
    }

    // 编队层：统计"重伤脱队"事件在时间窗内的数量，超阈值上报 REFORM 意图。
    public sealed class CasualtyReformSignal
    {
        private readonly float _windowSeconds;
        private readonly int _threshold;
        private readonly Queue<float> _events = new Queue<float>();

        public CasualtyReformSignal(float windowSeconds = 5f, int threshold = 8)
        {
            _windowSeconds = windowSeconds;
            _threshold = threshold;
        }

        public int CriticalCount { get { return _events.Count; } }

        // 每发生一次重伤脱队调用一次；返回 true 表示应触发 REFORM。
        public bool OnCriticalCasualty(float now)
        {
            _events.Enqueue(now);
            while (_events.Count > 0 && now - _events.Peek() > _windowSeconds)
                _events.Dequeue();
            return _events.Count >= _threshold;
        }
    }
}