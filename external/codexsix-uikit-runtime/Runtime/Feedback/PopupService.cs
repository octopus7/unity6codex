using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodexSix.UiKit.Runtime
{
    public sealed class PopupService
    {
        private readonly UiModalService _modalService;
        private readonly Func<int, CancellationToken, Task> _delayAsync;

        public PopupService(UiModalService modalService, Func<int, CancellationToken, Task> delayAsync = null)
        {
            _modalService = modalService ?? throw new ArgumentNullException(nameof(modalService));
            _delayAsync = delayAsync ?? ((milliseconds, ct) => Task.Delay(milliseconds, ct));
        }

        public Task<PopupResult> ShowAsync(PopupRequest request, CancellationToken ct = default)
        {
            return _modalService.ShowAsync(request, ct);
        }

        public async Task<PopupResult> ShowWithTimeoutAsync(PopupRequest request, float timeoutSeconds, CancellationToken ct = default)
        {
            if (timeoutSeconds <= 0f)
            {
                return await _modalService.ShowAsync(request, ct);
            }

            var timeoutMs = Math.Max(1, (int)Math.Ceiling(timeoutSeconds * 1000f));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var popupTask = _modalService.ShowAsync(request, ct);
            var timeoutTask = _delayAsync(timeoutMs, timeoutCts.Token);
            var completed = await Task.WhenAny(popupTask, timeoutTask);

            if (completed == popupTask)
            {
                timeoutCts.Cancel();
                return await popupTask;
            }

            _modalService.TryDismiss(request.Id, PopupDismissReason.Programmatic);
            return new PopupResult(PopupResultKind.Timeout, request.Id);
        }
    }
}