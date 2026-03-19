#nullable enable
using System;

namespace CodexSix.UguiRuntime
{
    public interface IUiScreenService
    {
        string? CurrentScreenId { get; }
        event Action<string?> ScreenChanged;
        void Show(string screenId);
        bool TryGoBack();
    }
}
