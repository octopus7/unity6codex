using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CodexSix.UiKit.Runtime
{
    public sealed class UiModalService : IUiModalService
    {
        private sealed class ModalEntry
        {
            public PopupRequest Request;
            public TaskCompletionSource<PopupResult> Completion;
            public CancellationTokenRegistration CancellationRegistration;
        }

        private readonly object _gate = new();
        private readonly List<ModalEntry> _stack = new();

        public int ModalDepth
        {
            get
            {
                lock (_gate)
                {
                    return _stack.Count;
                }
            }
        }

        public PopupRequest? TopRequest
        {
            get
            {
                lock (_gate)
                {
                    if (_stack.Count == 0)
                    {
                        return null;
                    }

                    return _stack[^1].Request;
                }
            }
        }

        public event Action<int> ModalDepthChanged;
        public event Action<PopupRequest?> TopRequestChanged;

        public Task<PopupResult> ShowAsync(PopupRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                throw new ArgumentException("Popup id must be provided.", nameof(request));
            }

            var entry = new ModalEntry
            {
                Request = request,
                Completion = new TaskCompletionSource<PopupResult>(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            lock (_gate)
            {
                _stack.Add(entry);
            }

            if (ct.CanBeCanceled)
            {
                entry.CancellationRegistration = ct.Register(() => TryDismiss(request.Id, PopupDismissReason.Programmatic));
            }

            NotifyStateChanged();
            return entry.Completion.Task;
        }

        public bool TryConfirmTop()
        {
            return TryResolveTop(PopupResultKind.Confirmed);
        }

        public bool TryCancelTop()
        {
            return TryResolveTop(PopupResultKind.Cancelled);
        }

        public bool TryDismissTop(PopupDismissReason reason = PopupDismissReason.Back)
        {
            var resultKind = reason == PopupDismissReason.Back ? PopupResultKind.Cancelled : PopupResultKind.Dismissed;
            return TryResolveTop(resultKind);
        }

        public bool TryDismiss(string popupId, PopupDismissReason reason = PopupDismissReason.Programmatic)
        {
            if (string.IsNullOrWhiteSpace(popupId))
            {
                return false;
            }

            var resultKind = reason == PopupDismissReason.Back ? PopupResultKind.Cancelled : PopupResultKind.Dismissed;
            ModalEntry found = null;
            lock (_gate)
            {
                for (var i = _stack.Count - 1; i >= 0; i--)
                {
                    if (_stack[i].Request.Id != popupId)
                    {
                        continue;
                    }

                    found = _stack[i];
                    _stack.RemoveAt(i);
                    break;
                }
            }

            if (found == null)
            {
                return false;
            }

            found.CancellationRegistration.Dispose();
            found.Completion.TrySetResult(new PopupResult(resultKind, found.Request.Id));
            NotifyStateChanged();
            return true;
        }

        private bool TryResolveTop(PopupResultKind kind)
        {
            ModalEntry entry;
            lock (_gate)
            {
                if (_stack.Count == 0)
                {
                    return false;
                }

                entry = _stack[^1];
                _stack.RemoveAt(_stack.Count - 1);
            }

            entry.CancellationRegistration.Dispose();
            entry.Completion.TrySetResult(new PopupResult(kind, entry.Request.Id));
            NotifyStateChanged();
            return true;
        }

        private void NotifyStateChanged()
        {
            ModalDepthChanged?.Invoke(ModalDepth);
            TopRequestChanged?.Invoke(TopRequest);
        }
    }
}
