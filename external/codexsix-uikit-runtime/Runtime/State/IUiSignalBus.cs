using System;

namespace CodexSix.UiKit.Runtime
{
    public interface IUiSignalBus
    {
        void Publish<TSignal>(TSignal signal);
        IDisposable Subscribe<TSignal>(Action<TSignal> listener);
    }
}