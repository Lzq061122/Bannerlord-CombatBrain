using System;
using System.Collections.Generic;

namespace CombatBrain.Events
{
    public sealed class CombatEventBus
    {
        private readonly Dictionary<CombatEventKind, Action<CombatEvent>> _handlers =
            new Dictionary<CombatEventKind, Action<CombatEvent>>();

        private Action<CombatEvent> _allHandlers;

        public void Subscribe(CombatEventKind kind, Action<CombatEvent> handler)
        {
            if (handler == null) return;
            Action<CombatEvent> existing;
            if (_handlers.TryGetValue(kind, out existing)) _handlers[kind] = existing + handler;
            else _handlers[kind] = handler;
        }

        public void Unsubscribe(CombatEventKind kind, Action<CombatEvent> handler)
        {
            if (handler == null) return;
            Action<CombatEvent> existing;
            if (_handlers.TryGetValue(kind, out existing))
            {
                existing -= handler;
                if (existing == null) _handlers.Remove(kind);
                else _handlers[kind] = existing;
            }
        }

        public void SubscribeAll(Action<CombatEvent> handler)
        {
            if (handler == null) return;
            _allHandlers += handler;
        }

        public void UnsubscribeAll(Action<CombatEvent> handler)
        {
            if (handler == null) return;
            _allHandlers -= handler;
        }

        public void Publish(CombatEvent e)
        {
            if (e == null) return;
            if (_allHandlers != null) _allHandlers(e);
            Action<CombatEvent> h;
            if (_handlers.TryGetValue(e.Kind, out h) && h != null) h(e);
        }
    }
}