using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Services
{
    /// <summary>Typed synchronous pub/sub. Handler exceptions are logged and never break the publisher.</summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        private readonly IGameLogger _log;

        public EventBus(IGameLogger log = null) => _log = log ?? NullLogger.Instance;

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var list)) _handlers[typeof(T)] = list = new List<Delegate>();
            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (_handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            var snapshot = list.ToArray();
            foreach (var d in snapshot)
            {
                try { ((Action<T>)d)(evt); }
                catch (Exception e) { _log.Error($"[EventBus] {typeof(T).Name} handler threw: {e}"); }
            }
        }

        public int HandlerCount<T>() => _handlers.TryGetValue(typeof(T), out var l) ? l.Count : 0;

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;
            public Subscription(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
