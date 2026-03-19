#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CodexSix.UguiRuntime
{
    public sealed class UiModalService : IUiModalService
    {
        private sealed class ModalEntry
        {
            public long EntryId;
            public UiPopupRequest Request;
            public UiPopupView? View;
            public CanvasGroup? CanvasGroup;
            public TaskCompletionSource<UiPopupResult>? Completion;
            public CancellationTokenRegistration CancellationRegistration;
        }

        private readonly UiContext _context;
        private readonly UiCatalog _catalog;
        private readonly RectTransform _modalLayer;
        private readonly List<ModalEntry> _stack = new();

        private long _nextEntryId = 1;

        public UiModalService(UiContext context, UiCatalog catalog, RectTransform modalLayer)
        {
            _context = context;
            _catalog = catalog;
            _modalLayer = modalLayer;
        }

        public int ModalDepth => _stack.Count;

        public UiPopupRequest? TopRequest => _stack.Count > 0 ? _stack[^1].Request : null;

        public event Action<int>? ModalDepthChanged;

        public Task<UiPopupResult> ShowAsync(UiPopupRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.PopupId))
            {
                throw new ArgumentException("Popup id must be provided.", nameof(request));
            }

            if (!_catalog.TryGetPopupDefinition(request.PopupId, out var definition) || definition?.Prefab == null)
            {
                throw new InvalidOperationException($"Popup '{request.PopupId}' is not registered in the UI catalog.");
            }

            var instance = UnityEngine.Object.Instantiate(definition.Prefab, _modalLayer, false);
            instance.name = $"Popup_{request.PopupId}_{_nextEntryId}";
            var canvasGroup = instance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = instance.gameObject.AddComponent<CanvasGroup>();
            }

            var entry = new ModalEntry
            {
                EntryId = _nextEntryId++,
                Request = request,
                View = instance,
                CanvasGroup = canvasGroup,
                Completion = new TaskCompletionSource<UiPopupResult>(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            instance.Attach(_context);
            instance.Bind(request, new UiModalHandle(this, entry.EntryId));
            _stack.Add(entry);

            if (ct.CanBeCanceled)
            {
                entry.CancellationRegistration = ct.Register(() => TryDismiss(entry.EntryId, UiPopupDismissReason.Programmatic));
            }

            RefreshInteractivity();
            return entry.Completion.Task;
        }

        public bool TryConfirmTop()
        {
            return TryResolveTop(UiPopupResultKind.Confirmed);
        }

        public bool TryCancelTop()
        {
            return TryResolveTop(UiPopupResultKind.Cancelled);
        }

        public bool TryDismissTop(UiPopupDismissReason reason = UiPopupDismissReason.Back)
        {
            return TryResolveTop(ToResultKind(reason));
        }

        internal bool TryConfirm(long entryId)
        {
            return TryResolve(entryId, UiPopupResultKind.Confirmed, requireTop: true);
        }

        internal bool TryCancel(long entryId)
        {
            return TryResolve(entryId, UiPopupResultKind.Cancelled, requireTop: true);
        }

        internal bool TryDismiss(long entryId, UiPopupDismissReason reason = UiPopupDismissReason.Programmatic)
        {
            return TryResolve(entryId, ToResultKind(reason), requireTop: false);
        }

        private bool TryResolveTop(UiPopupResultKind resultKind)
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            var entryId = _stack[^1].EntryId;
            return TryResolve(entryId, resultKind, requireTop: true);
        }

        private bool TryResolve(long entryId, UiPopupResultKind resultKind, bool requireTop)
        {
            var index = FindEntryIndex(entryId);
            if (index < 0)
            {
                return false;
            }

            if (requireTop && index != _stack.Count - 1)
            {
                return false;
            }

            var entry = _stack[index];
            _stack.RemoveAt(index);

            entry.CancellationRegistration.Dispose();
            if (entry.View != null)
            {
                UnityEngine.Object.Destroy(entry.View.gameObject);
            }

            entry.Completion?.TrySetResult(new UiPopupResult(resultKind, entry.Request.PopupId));
            RefreshInteractivity();
            return true;
        }

        private int FindEntryIndex(long entryId)
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].EntryId == entryId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void RefreshInteractivity()
        {
            for (var i = 0; i < _stack.Count; i++)
            {
                var entry = _stack[i];
                if (entry.CanvasGroup == null)
                {
                    continue;
                }

                var isTop = i == _stack.Count - 1;
                entry.CanvasGroup.interactable = isTop;
                entry.CanvasGroup.blocksRaycasts = isTop;
                entry.CanvasGroup.alpha = 1f;
            }

            ModalDepthChanged?.Invoke(_stack.Count);
        }

        private static UiPopupResultKind ToResultKind(UiPopupDismissReason reason)
        {
            return reason == UiPopupDismissReason.Back
                ? UiPopupResultKind.Cancelled
                : UiPopupResultKind.Dismissed;
        }
    }
}
