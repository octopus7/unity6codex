#nullable enable
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CodexSix.UguiRuntime
{
    public sealed class UiContext : MonoBehaviour
    {
        [SerializeField] private UiCatalog? _catalog;
        [SerializeField] private UiRoot? _root;

        private bool _initialized;
        private bool _ownsRuntimeCatalog;
        private UnityAction? _backdropClickHandler;
        private UiInputDriver? _inputDriver;

        public UiCatalog Catalog => _catalog!;
        public UiRoot Root => _root!;
        public UiScreenService ScreenService { get; private set; } = null!;
        public UiModalService ModalService { get; private set; } = null!;
        public UiInputBlockService InputBlockService { get; private set; } = null!;

        public void Initialize(UiRoot root, UiCatalog? catalog)
        {
            if (_initialized)
            {
                return;
            }

            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _catalog = catalog;
            if (_catalog == null)
            {
                _catalog = ScriptableObject.CreateInstance<UiCatalog>();
                _catalog.name = "RuntimeUiCatalog";
                _ownsRuntimeCatalog = true;
            }

            ScreenService = new UiScreenService(this, _catalog, _root.ScreenLayer);
            ModalService = new UiModalService(this, _catalog, _root.ModalLayer);
            InputBlockService = new UiInputBlockService();
            _inputDriver = new UiInputDriver(ModalService, ScreenService);

            ModalService.ModalDepthChanged += HandleModalDepthChanged;
            _backdropClickHandler = HandleBackdropClicked;
            _root.BlockerButton.onClick.AddListener(_backdropClickHandler);

            HandleModalDepthChanged(ModalService.ModalDepth);
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || _inputDriver == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                _inputDriver.ProcessEscape();
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                _inputDriver.ProcessConfirm();
            }
        }

        private void OnDestroy()
        {
            if (!_initialized)
            {
                return;
            }

            ModalService.ModalDepthChanged -= HandleModalDepthChanged;
            if (_root != null && _backdropClickHandler != null)
            {
                _root.BlockerButton.onClick.RemoveListener(_backdropClickHandler);
            }

            if (_ownsRuntimeCatalog && _catalog != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_catalog);
                }
                else
                {
                    DestroyImmediate(_catalog);
                }
            }
        }

        private void HandleModalDepthChanged(int depth)
        {
            var blocked = depth > 0;
            _root?.SetBlockerVisible(blocked);
            InputBlockService?.SetGameplayBlocked(blocked);
        }

        private void HandleBackdropClicked()
        {
            if (ModalService.ModalDepth <= 0)
            {
                return;
            }

            var request = ModalService.TopRequest;
            if (request.HasValue && request.Value.DismissOnBackdrop)
            {
                ModalService.TryCancelTop();
            }
        }
    }
}
