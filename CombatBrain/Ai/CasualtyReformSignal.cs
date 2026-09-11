using System.Collections.Generic;

namespace CombatBrain.Survival
{
    // F7 编队级重整信号（CasualtyReformSignal）—— 纯逻辑，零 TaleWorlds 依赖。
    //
    // CB-PROMPT-0038 §五.5（F7 拆分）后的保留半边：统计「新跌破濒死线」事件在时间窗内的数量，
    //   超阈值 → 由胶水层上报 IntentType.Reform（编队重整信号）。
    //   统计口径 = **事件累计**（每次事件 O(1) 入队 + 窗口过期出队），不做逐拍逐兵比例扫描，
    //   也不产出任何单兵动作/移动决策。
    // 退役半边（已删除，见 docs/agent-control-surface-audit.md §2.8）：单兵低血量自行寻找友军并
    //   调用 Agent.SetMovementDirection 后撤（原 SurvivalInstinct.Decide + SurvivalGlue.ApplyRetreat）。
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
