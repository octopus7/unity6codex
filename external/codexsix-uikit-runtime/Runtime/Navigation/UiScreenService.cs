using System;
using System.Collections.Generic;

namespace CodexSix.UiKit.Runtime
{
    public sealed class UiScreenService : IUiScreenService
    {
        private readonly Stack<string> _history = new();

        public string? CurrentScreenId { get; private set; }

        public event Action<string?> ScreenChanged;

        public void ShowScreen(string screenId)
        {
            if (string.IsNullOrWhiteSpace(screenId))
            {
                throw new ArgumentException("Screen id must be provided.", nameof(screenId));
            }

            if (CurrentScreenId == screenId)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(CurrentScreenId))
            {
                _history.Push(CurrentScreenId);
            }

            CurrentScreenId = screenId;
            ScreenChanged?.Invoke(CurrentScreenId);
        }

        public bool TryGoBack()
        {
            while (_history.Count > 0)
            {
                var previous = _history.Pop();
                if (string.IsNullOrWhiteSpace(previous) || previous == CurrentScreenId)
                {
                    continue;
                }

                CurrentScreenId = previous;
                ScreenChanged?.Invoke(CurrentScreenId);
                return true;
            }

            return false;
        }
    }
}
