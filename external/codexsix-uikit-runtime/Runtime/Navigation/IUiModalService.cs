using System.Threading;
using System.Threading.Tasks;

namespace CodexSix.UiKit.Runtime
{
    public interface IUiModalService
    {
        int ModalDepth { get; }
        Task<PopupResult> ShowAsync(PopupRequest request, CancellationToken ct = default);
        bool TryDismissTop(PopupDismissReason reason = PopupDismissReason.Back);
    }
}