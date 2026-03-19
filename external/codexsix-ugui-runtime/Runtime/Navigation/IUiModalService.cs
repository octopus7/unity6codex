#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodexSix.UguiRuntime
{
    public interface IUiModalService
    {
        int ModalDepth { get; }
        UiPopupRequest? TopRequest { get; }
        event Action<int> ModalDepthChanged;
        Task<UiPopupResult> ShowAsync(UiPopupRequest request, CancellationToken ct = default);
        bool TryConfirmTop();
        bool TryCancelTop();
        bool TryDismissTop(UiPopupDismissReason reason = UiPopupDismissReason.Back);
    }
}
