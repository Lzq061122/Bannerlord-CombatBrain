using System.Collections.Generic;

namespace CombatBrain.Events
{
    // 全场事件录制：既是 debug 工具、F10 学习数据源，也是下游高光回放的素材。
    // 从第一天就建（见交接文档第十一节）。
    public sealed class ReplayRecorder
    {
        private readonly CombatEventBus _bus;
        private readonly List<CombatEvent> _events = new List<CombatEvent>();
        private readonly int _maxEvents;

        public ReplayRecorder(CombatEventBus bus, int maxEvents = 200000)
        {
            _bus = bus;
            _maxEvents = maxEvents;
            _bus.SubscribeAll(OnEvent);
        }

        private void OnEvent(CombatEvent e)
        {
            Record(e);
        }

        public void Record(CombatEvent e)
        {
            if (e == null) return;
            _events.Add(e);
            if (_events.Count > _maxEvents)
            {
                _events.RemoveRange(0, _events.Count - _maxEvents);
            }
        }

        public int Count { get { return _events.Count; } }

        public IReadOnlyList<CombatEvent> Events { get { return _events; } }

        public List<CombatEvent> GetWindow(float fromTime, float toTime)
        {
            var result = new List<CombatEvent>();
            foreach (CombatEvent e in _events)
            {
                if (e.Time >= fromTime && e.Time <= toTime) result.Add(e);
            }
            return result;
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}