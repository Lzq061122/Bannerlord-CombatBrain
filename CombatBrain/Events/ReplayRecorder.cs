using System;
using System.Collections.Generic;

namespace CombatBrain.Events
{
    // 全场事件录制：既是 debug 工具、F10 学习数据源，也是下游高光回放的素材。
    // 从第一天就建（见交接文档第十一节）。
    // P0-⑥（review #5）：内部从 List+RemoveRange 改为固定容量环形缓冲——破顶后 Record 由 O(n)
    // memmove（每事件搬移约 20 万元素，AttackObserved 每命中触发）降为 O(1) 覆盖最旧槽位；
    // 对外 API（Count/Events/GetWindow/Clear）语义不变。Events/GetWindow 仅诊断与回放消费
    // （当前仓库无每帧调用点，见 CombatAiMissionLogic.Init 仅构造），返回拷贝可接受。
    public sealed class ReplayRecorder
    {
        private readonly CombatEventBus _bus;
        private readonly CombatEvent[] _ring; // 固定容量环形缓冲（物理序 ≠ 时间序）
        private readonly int _maxEvents;
        private int _head;  // 下一写入槽位（环绕；时间序旧→新 = _tail .. _head-1）
        private int _count; // 有效事件数（0.._maxEvents）

        public ReplayRecorder(CombatEventBus bus, int maxEvents = 200000)
        {
            _bus = bus;
            _maxEvents = maxEvents < 1 ? 1 : maxEvents; // 防御：0 容量会除零（默认 200000 不受影响）
            _ring = new CombatEvent[_maxEvents];
            _bus.SubscribeAll(OnEvent);
        }

        private void OnEvent(CombatEvent e)
        {
            Record(e);
        }

        public void Record(CombatEvent e)
        {
            if (e == null) return;
            _ring[_head] = e; // O(1)：满时覆盖最旧槽位（无 RemoveRange memmove）
            _head = (_head + 1) % _maxEvents;
            if (_count < _maxEvents) _count++;
        }

        public int Count { get { return _count; } }

        // 按时间序（旧→新）的拷贝视图：物理序从 _tail 环绕到 _head-1。
        // 调用频率评估：仅诊断/回放消费（当前无每帧调用点），非热路径，可接受拷贝。
        public IReadOnlyList<CombatEvent> Events
        {
            get
            {
                var result = new List<CombatEvent>(_count);
                int tail = TailIndex;
                for (int i = 0; i < _count; i++)
                    result.Add(_ring[(tail + i) % _maxEvents]);
                return result;
            }
        }

        public List<CombatEvent> GetWindow(float fromTime, float toTime)
        {
            var result = new List<CombatEvent>();
            int tail = TailIndex;
            for (int i = 0; i < _count; i++)
            {
                CombatEvent e = _ring[(tail + i) % _maxEvents];
                if (e.Time >= fromTime && e.Time <= toTime) result.Add(e);
            }
            return result;
        }

        public void Clear()
        {
            Array.Clear(_ring, 0, _ring.Length); // 释放事件引用（同 List.Clear 语义）
            _head = 0;
            _count = 0;
        }

        // 最旧有效事件的物理槽位（_count=0 时无意义但算术安全）
        private int TailIndex
        {
            get { return (_head - _count + _maxEvents) % _maxEvents; }
        }
    }
}
