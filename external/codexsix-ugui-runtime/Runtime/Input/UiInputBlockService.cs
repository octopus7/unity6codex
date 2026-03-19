using System;

namespace CodexSix.UguiRuntime
{
    public sealed class UiInputBlockService : IUiInputBlockService
    {
        public bool IsGameplayInputBlocked { get; private set; }

        public event Action<bool> GameplayBlockChanged;

        internal void SetGameplayBlocked(bool blocked)
        {
            if (IsGameplayInputBlocked == blocked)
            {
                return;
            }

            IsGameplayInputBlocked = blocked;
            GameplayBlockChanged?.Invoke(IsGameplayInputBlocked);
        }
    }
}
