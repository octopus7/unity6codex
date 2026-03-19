namespace CodexSix.UguiRuntime
{
    public sealed class UiInputDriver
    {
        private readonly IUiModalService _modalService;
        private readonly IUiScreenService _screenService;

        public UiInputDriver(IUiModalService modalService, IUiScreenService screenService)
        {
            _modalService = modalService;
            _screenService = screenService;
        }

        public bool ProcessEscape()
        {
            if (_modalService.ModalDepth > 0)
            {
                return _modalService.TryCancelTop();
            }

            return _screenService.TryGoBack();
        }

        public bool ProcessConfirm()
        {
            if (_modalService.ModalDepth <= 0)
            {
                return false;
            }

            return _modalService.TryConfirmTop();
        }
    }
}
