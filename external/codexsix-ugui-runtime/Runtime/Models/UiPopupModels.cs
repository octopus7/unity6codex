#nullable enable
namespace CodexSix.UguiRuntime
{
    public enum UiPopupResultKind
    {
        Confirmed = 1,
        Cancelled = 2,
        Dismissed = 3
    }

    public enum UiPopupDismissReason
    {
        Back = 1,
        Programmatic = 2
    }

    public readonly struct UiPopupRequest
    {
        public UiPopupRequest(
            string popupId,
            string title,
            string message,
            string confirmText = "OK",
            string cancelText = "Cancel",
            bool dismissOnBackdrop = false,
            object? payload = null)
        {
            PopupId = popupId;
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            DismissOnBackdrop = dismissOnBackdrop;
            Payload = payload;
        }

        public string PopupId { get; }
        public string Title { get; }
        public string Message { get; }
        public string ConfirmText { get; }
        public string CancelText { get; }
        public bool DismissOnBackdrop { get; }
        public object? Payload { get; }
    }

    public readonly struct UiPopupResult
    {
        public UiPopupResult(UiPopupResultKind kind, string popupId)
        {
            Kind = kind;
            PopupId = popupId;
        }

        public UiPopupResultKind Kind { get; }
        public string PopupId { get; }
    }
}
