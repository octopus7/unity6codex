using System;
using System.Collections.Generic;

namespace CodexSix.UiKit.Runtime
{
    public sealed class UiSignalBus : IUiSignalBus
    {
        private readonly object _gate = new();
        private readonly Dictionary<Type, Dictionary<int, Delegate>> _listenersByType = new();
        private int _nextSubscriptionId;

        public void Publish<TSignal>(TSignal signal)
        {
            Delegate[] listeners;
            lock (_gate)
            {
                if (!_listenersByType.TryGetValue(typeof(TSignal), out var listenersForType) || listenersForType.Count == 0)
                {
                    return;
                }

                listeners = new Delegate[listenersForType.Count];
                listenersForType.Values.CopyTo(listeners, 0);
            }

            for (var i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] is Action<TSignal> listener)
                {
                    listener(signal);
                }
            }
        }

        public IDisposable Subscribe<TSignal>(Action<TSignal> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            var signalType = typeof(TSignal);
            int subscriptionId;

            lock (_gate)
            {
                if (!_listenersByType.TryGetValue(signalType, out var listenersForType))
                {
                    listenersForType = new Dictionary<int, Delegate>();
                    _listenersByType.Add(signalType, listenersForType);
                }

                subscriptionId = ++_nextSubscriptionId;
                listenersForType.Add(subscriptionId, listener);
            }

            return new DelegateSubscription(() =>
            {
                lock (_gate)
                {
                    if (!_listenersByType.TryGetValue(signalType, out var listenersForType))
                    {
                        return;
                    }

                    listenersForType.Remove(subscriptionId);
                    if (listenersForType.Count == 0)
                    {
                        _listenersByType.Remove(signalType);
                    }
                }
            });
        }
    }
}