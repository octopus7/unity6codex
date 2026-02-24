using System;

namespace CodexSix.UiKit.Runtime
{
    internal sealed class DelegateSubscription : IDisposable
    {
        private Action _dispose;

        public DelegateSubscription(Action dispose)
        {
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        }

        public void Dispose()
        {
            var dispose = _dispose;
            if (dispose == null)
            {
                return;
            }

            _dispose = null;
            dispose();
        }
    }
}