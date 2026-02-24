using System;

namespace CodexSix.UiKit.Runtime
{
    public interface IUiStateChannel<T>
    {
        T Value { get; }
        void Publish(T value);
        IDisposable Subscribe(Action<T> listener, bool replayLatest = true);
    }
}