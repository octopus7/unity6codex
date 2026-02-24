using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodexSix.UiKit.Runtime
{
    public sealed class UiContext : MonoBehaviour
    {
        [SerializeField] private UiRootLifetime _rootLifetime = UiRootLifetime.SceneScoped;
        [SerializeField] private bool _enablePersistentToastChannel;
        [SerializeField] private bool _includeDefaultActionKeys = true;
        [SerializeField] private int _toastMaxVisible = 3;
        [SerializeField] private float _toastQueueTtlSeconds = 8f;

        private bool _initialized;

        private UiSignalBus _signalBus;
        private UiStateChannel<UiFocusState> _focusState;
        private UiScreenService _screenService;
        private UiModalService _modalService;
        private ToastService _toastService;
        private ShortcutService _shortcutService;
        private IInputBlockPolicy _inputBlockPolicy;
        private PopupService _popupService;

        public UiRootLifetime RootLifetime => _rootLifetime;
        public IUiSignalBus Signals => _signalBus;
        public IUiStateChannel<UiFocusState> FocusState => _focusState;
        public IUiScreenService Screens => _screenService;
        public IUiModalService Modals => _modalService;
        public IToastService Toasts => _toastService;
        public IShortcutService Shortcuts => _shortcutService;
        public IInputBlockPolicy InputBlockPolicy => _inputBlockPolicy;

        public UiSignalBus SignalBus => _signalBus;
        public UiStateChannel<UiFocusState> FocusStateChannel => _focusState;
        public UiScreenService ScreenService => _screenService;
        public UiModalService ModalService => _modalService;
        public ToastService ToastService => _toastService;
        public ShortcutService ShortcutService => _shortcutService;
        public PopupService PopupService => _popupService;
        public ToastService PersistentToastService => UiPersistentChannels.PersistentToasts;

        public void Configure(UiRootLifetime rootLifetime, bool enablePersistentToastChannel, bool includeDefaultActionKeys)
        {
            if (_initialized)
            {
                return;
            }

            _rootLifetime = rootLifetime;
            _enablePersistentToastChannel = enablePersistentToastChannel;
            _includeDefaultActionKeys = includeDefaultActionKeys;
        }

        public void SetFocusState(UiFocusState state)
        {
            EnsureInitialized();
            _focusState.Publish(state);
        }

        public IToastService GetToastService(string channelId)
        {
            EnsureInitialized();
            if (string.Equals(channelId, UiToastChannels.Persistent, StringComparison.OrdinalIgnoreCase))
            {
                return PersistentToastService;
            }

            return _toastService;
        }

        private void Awake()
        {
            EnsureInitialized();

            if (_rootLifetime == UiRootLifetime.Persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            _shortcutService.CloseRequested -= HandleCloseRequested;
            _shortcutService.ConfirmRequested -= HandleConfirmRequested;
        }

        private void Update()
        {
            EnsureInitialized();

            _toastService.Tick();
            if (_enablePersistentToastChannel)
            {
                PersistentToastService.Tick();
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var context = InputContext.FromKeyboard(_focusState.Value, _modalService.ModalDepth > 0, keyboard);
            _shortcutService.Process(context);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _signalBus = new UiSignalBus();
            _focusState = new UiStateChannel<UiFocusState>();
            _focusState.Publish(UiFocusState.None);

            _screenService = new UiScreenService();
            _modalService = new UiModalService();
            _toastService = new ToastService(
                new ToastServiceOptions(Math.Max(1, _toastMaxVisible), Mathf.Max(0.1f, _toastQueueTtlSeconds)),
                () => Time.unscaledTimeAsDouble);

            _shortcutService = new ShortcutService(_includeDefaultActionKeys);
            _shortcutService.CloseRequested += HandleCloseRequested;
            _shortcutService.ConfirmRequested += HandleConfirmRequested;

            _inputBlockPolicy = new DefaultInputBlockPolicy();
            _popupService = new PopupService(_modalService);

            _initialized = true;
        }

        private void HandleCloseRequested()
        {
            if (_modalService.ModalDepth > 0)
            {
                _modalService.TryDismissTop(PopupDismissReason.Back);
                return;
            }

            _screenService.TryGoBack();
        }

        private void HandleConfirmRequested()
        {
            if (_modalService.ModalDepth <= 0)
            {
                return;
            }

            _modalService.TryConfirmTop();
        }
    }
}