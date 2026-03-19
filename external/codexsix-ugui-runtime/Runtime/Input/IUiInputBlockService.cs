using System;

namespace CodexSix.UguiRuntime
{
    public interface IUiInputBlockService
    {
        bool IsGameplayInputBlocked { get; }
        event Action<bool> GameplayBlockChanged;
    }
}
