using System;
using System.Collections.Generic;
using Game.Services;

namespace Game.ViewModels
{
    /// <summary>
    /// MVVM-lite base: plain C#, raises <see cref="Changed"/> when state the view shows changes. Views call commands on the
    /// view model; view models call services. Subscriptions added with <see cref="Track"/> are released on <see cref="Dispose"/>.
    /// </summary>
    public abstract class ViewModelBase : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _disposed;

        /// <summary>Raised with the property name (or null = everything) when the view should refresh.</summary>
        public event Action<string> Changed;

        protected void Raise(string property = null)
        {
            if (!_disposed) Changed?.Invoke(property);
        }

        protected bool Set<T>(ref T field, T value, string property)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(property);
            return true;
        }

        protected void Track(IDisposable subscription) => _subscriptions.Add(subscription);

        /// <summary>Subscribes to a bus event for the lifetime of this view model.</summary>
        protected void On<T>(EventBus bus, Action<T> handler) => Track(bus.Subscribe(handler));

        public bool IsDisposed => _disposed;

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
            Changed = null;
        }
    }

    /// <summary>Outcome of a view-model command for the view: success, or a localized error message to show.</summary>
    public readonly struct CommandResult
    {
        public readonly bool Ok;
        public readonly string MessageKey;
        public CommandResult(bool ok, string messageKey) { Ok = ok; MessageKey = messageKey; }
        public static CommandResult Success => new CommandResult(true, null);
        public static CommandResult From(Game.Core.Result r) => r.Ok ? Success : new CommandResult(false, ErrorKeys.For(r.Code));
    }

    /// <summary>Friendly localization keys for <see cref="Game.Core.ErrorCode"/>s.</summary>
    public static class ErrorKeys
    {
        public static string For(Game.Core.ErrorCode code) => "error_" + code.ToString().ToLowerInvariant();
    }
}
