#nullable enable
namespace CodexSix.UguiRuntime
{
    public readonly struct UiModalHandle
    {
        private readonly UiModalService? _service;
        private readonly long _entryId;

        internal UiModalHandle(UiModalService service, long entryId)
        {
            _service = service;
            _entryId = entryId;
        }

        public bool Confirm()
        {
            return _service != null && _service.TryConfirm(_entryId);
        }

        public bool Cancel()
        {
            return _service != null && _service.TryCancel(_entryId);
        }

        public bool Dismiss(UiPopupDismissReason reason = UiPopupDismissReason.Programmatic)
        {
            return _service != null && _service.TryDismiss(_entryId, reason);
        }
    }
}
