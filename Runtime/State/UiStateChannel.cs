using System;
using System.Collections.Generic;

namespace CodexSix.UiKit.Runtime
{
    public sealed class UiStateChannel<T> : IUiStateChannel<T>
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, Action<T>> _listeners = new();

        private int _nextSubscriptionId;
        private T _value;
        private bool _hasValue;

        public T Value
        {
            get
            {
                lock (_gate)
                {
                    return _value;
                }
            }
        }

        public void Publish(T value)
        {
            Action<T>[] listeners;
            lock (_gate)
            {
                _value = value;
                _hasValue = true;
                listeners = new Action<T>[_listeners.Count];
                _listeners.Values.CopyTo(listeners, 0);
            }

            for (var i = 0; i < listeners.Length; i++)
            {
                listeners[i]?.Invoke(value);
            }
        }

        public IDisposable Subscribe(Action<T> listener, bool replayLatest = true)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            int subscriptionId;
            T snapshot = default;
            var shouldReplay = false;

            lock (_gate)
            {
                subscriptionId = ++_nextSubscriptionId;
                _listeners.Add(subscriptionId, listener);

                if (replayLatest && _hasValue)
                {
                    snapshot = _value;
                    shouldReplay = true;
                }
            }

            if (shouldReplay)
            {
                listener(snapshot);
            }

            return new DelegateSubscription(() =>
            {
                lock (_gate)
                {
                    _listeners.Remove(subscriptionId);
                }
            });
        }
    }
}