#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.UguiRuntime
{
    public sealed class UiScreenService : IUiScreenService
    {
        private sealed class ScreenEntry
        {
            public UiScreenDefinition? Definition;
            public UiScreenView? View;
            public bool IsAttached;
        }

        private readonly UiContext _context;
        private readonly UiCatalog _catalog;
        private readonly RectTransform _screenLayer;
        private readonly Dictionary<string, ScreenEntry> _entries = new(StringComparer.Ordinal);
        private readonly Stack<string> _history = new();

        public UiScreenService(UiContext context, UiCatalog catalog, RectTransform screenLayer)
        {
            _context = context;
            _catalog = catalog;
            _screenLayer = screenLayer;
        }

        public string? CurrentScreenId { get; private set; }

        public event Action<string?>? ScreenChanged;

        public void Show(string screenId)
        {
            if (string.IsNullOrWhiteSpace(screenId))
            {
                throw new ArgumentException("Screen id must be provided.", nameof(screenId));
            }

            if (string.Equals(CurrentScreenId, screenId, StringComparison.Ordinal))
            {
                return;
            }

            if (!_catalog.TryGetScreenDefinition(screenId, out var definition) || definition?.Prefab == null)
            {
                throw new InvalidOperationException($"Screen '{screenId}' is not registered in the UI catalog.");
            }

            if (!string.IsNullOrWhiteSpace(CurrentScreenId))
            {
                _history.Push(CurrentScreenId);
            }

            ShowResolved(definition);
        }

        public bool TryGoBack()
        {
            while (_history.Count > 0)
            {
                var previousId = _history.Pop();
                if (string.IsNullOrWhiteSpace(previousId) ||
                    string.Equals(previousId, CurrentScreenId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_catalog.TryGetScreenDefinition(previousId, out var definition) || definition?.Prefab == null)
                {
                    continue;
                }

                ShowResolved(definition);
                return true;
            }

            return false;
        }

        private void ShowResolved(UiScreenDefinition definition)
        {
            var previousId = CurrentScreenId;
            var previousEntry = GetCurrentEntry();
            if (previousEntry?.View != null)
            {
                previousEntry.View.NotifyBlur();
                previousEntry.View.NotifyHide();

                if (previousEntry.Definition != null && !previousEntry.Definition.CacheInstance)
                {
                    UnityEngine.Object.Destroy(previousEntry.View.gameObject);
                    previousEntry.View = null;
                    previousEntry.IsAttached = false;
                }
                else
                {
                    previousEntry.View.gameObject.SetActive(false);
                }
            }

            var entry = GetOrCreateEntry(definition);
            if (entry.View == null)
            {
                throw new InvalidOperationException($"Screen '{definition.Id}' could not be instantiated.");
            }

            entry.View.gameObject.SetActive(true);
            entry.View.transform.SetAsLastSibling();
            entry.View.NotifyShow();
            entry.View.NotifyFocus();

            CurrentScreenId = definition.Id;
            if (!string.Equals(previousId, CurrentScreenId, StringComparison.Ordinal))
            {
                ScreenChanged?.Invoke(CurrentScreenId);
            }
        }

        private ScreenEntry? GetCurrentEntry()
        {
            if (string.IsNullOrWhiteSpace(CurrentScreenId))
            {
                return null;
            }

            _entries.TryGetValue(CurrentScreenId, out var entry);
            return entry;
        }

        private ScreenEntry GetOrCreateEntry(UiScreenDefinition definition)
        {
            if (!_entries.TryGetValue(definition.Id, out var entry))
            {
                entry = new ScreenEntry();
                _entries.Add(definition.Id, entry);
            }

            entry.Definition = definition;
            if (entry.View != null)
            {
                return entry;
            }

            var instance = UnityEngine.Object.Instantiate(definition.Prefab, _screenLayer, false);
            instance.name = $"Screen_{definition.Id}";
            EnsureRectTransform(instance.transform as RectTransform);
            entry.View = instance;
            if (!entry.IsAttached)
            {
                entry.View.Attach(_context, definition.Id);
                entry.IsAttached = true;
            }

            entry.View.gameObject.SetActive(false);
            return entry;
        }

        private static void EnsureRectTransform(RectTransform? rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
    }
}
