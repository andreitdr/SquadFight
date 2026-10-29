using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Minimal interface→instance registry built by the composition root (Boot scene). Not a static singleton.</summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
        }

        public bool TryGet<T>(out T instance) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) { instance = (T)s; return true; }
            instance = null;
            return false;
        }

        public bool Contains<T>() where T : class => _services.ContainsKey(typeof(T));

        public void Clear() => _services.Clear();
    }
}
