using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CodexSix.UiKit.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiContext))]
    [RequireComponent(typeof(UiRoot))]
    public sealed class UiToolkitPresenter : MonoBehaviour
    {
        [SerializeField] private UiContext _context;
        [SerializeField] private UiRoot _uiRoot;
        [SerializeField] private VisualTreeAsset _popupVisualTree;
        [SerializeField] private VisualTreeAsset _toastVisualTree;

        private bool _subscribed;
        private bool _focusCallbacksRegistered;

        public void Configure(VisualTreeAsset popupVisualTree, VisualTreeAsset toastVisualTree)
        {
            _popupVisualTree = popupVisualTree;
            _toastVisualTree = toastVisualTree;
        }

        private void Awake()
        {
            BindReferences();
        }

        private void OnEnable()
        {
            BindReferences();
            if (_context == null || _uiRoot == null)
            {
                return;
            }

            _uiRoot.EnsureBuilt();
            Subscribe();
            RegisterFocusCallbacks();
            RenderAll();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnregisterFocusCallbacks();
        }

        private void BindReferences()
        {
            if (_context == null)
            {
                _context = GetComponent<UiContext>();
            }

            if (_uiRoot == null)
            {
                _uiRoot = GetComponent<UiRoot>();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _context.ScreenService.ScreenChanged += OnScreenChanged;
            _context.ModalService.TopRequestChanged += OnTopRequestChanged;
            _context.ModalService.ModalDepthChanged += OnModalDepthChanged;
            _context.ToastService.ToastsChanged += OnToastsChanged;
            _context.PersistentToastService.ToastsChanged += OnToastsChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _context == null)
            {
                return;
            }

            _context.ScreenService.ScreenChanged -= OnScreenChanged;
            _context.ModalService.TopRequestChanged -= OnTopRequestChanged;
            _context.ModalService.ModalDepthChanged -= OnModalDepthChanged;
            _context.ToastService.ToastsChanged -= OnToastsChanged;
            _context.PersistentToastService.ToastsChanged -= OnToastsChanged;
            _subscribed = false;
        }

        private void RegisterFocusCallbacks()
        {
            if (_focusCallbacksRegistered || _uiRoot?.RootElement == null)
            {
                return;
            }

            _uiRoot.RootElement.RegisterCallback<FocusInEvent>(OnFocusIn);
            _uiRoot.RootElement.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _focusCallbacksRegistered = true;
        }

        private void UnregisterFocusCallbacks()
        {
            if (!_focusCallbacksRegistered || _uiRoot?.RootElement == null)
            {
                return;
            }

            _uiRoot.RootElement.UnregisterCallback<FocusInEvent>(OnFocusIn);
            _uiRoot.RootElement.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            _focusCallbacksRegistered = false;
        }

        private void OnScreenChanged(string? _)
        {
            RenderScreen();
        }

        private void OnTopRequestChanged(PopupRequest? _)
        {
            RenderModal();
        }

        private void OnModalDepthChanged(int _)
        {
            RenderModal();
        }

        private void OnToastsChanged()
        {
            RenderToasts();
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (_context == null)
            {
                return;
            }

            if (evt.target is TextField)
            {
                _context.SetFocusState(UiFocusState.TextInput);
                return;
            }

            _context.SetFocusState(UiFocusState.PointerCapture);
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (_context == null)
            {
                return;
            }

            if (evt.relatedTarget is TextField)
            {
                _context.SetFocusState(UiFocusState.TextInput);
                return;
            }

            _context.SetFocusState(UiFocusState.None);
        }

        private void RenderAll()
        {
            RenderScreen();
            RenderModal();
            RenderToasts();
        }

        private void RenderScreen()
        {
            var layer = _uiRoot?.ScreenLayer;
            if (layer == null)
            {
                return;
            }

            layer.Clear();
            var screenId = _context.ScreenService.CurrentScreenId;
            if (string.IsNullOrWhiteSpace(screenId))
            {
                screenId = "none";
            }

            var label = new Label($"Screen: {screenId}") { name = "current-screen-label" };
            label.AddToClassList("cs6-screen-label");
            layer.Add(label);
        }

        private void RenderModal()
        {
            var layer = _uiRoot?.ModalLayer;
            if (layer == null)
            {
                return;
            }

            layer.Clear();
            var request = _context.ModalService.TopRequest;
            if (request == null)
            {
                return;
            }

            var popup = ClonePopupVisual() ?? BuildFallbackPopupVisual();
            var title = popup.Q<Label>("popup-title");
            var body = popup.Q<Label>("popup-body");
            var confirm = popup.Q<Button>("confirm-button");
            var cancel = popup.Q<Button>("cancel-button");

            if (title != null)
            {
                title.text = request.Value.Title;
            }

            if (body != null)
            {
                body.text = request.Value.Body;
            }

            if (confirm != null)
            {
                confirm.text = string.IsNullOrWhiteSpace(request.Value.ConfirmText) ? "Confirm" : request.Value.ConfirmText;
                confirm.clicked += () => _context.ModalService.TryConfirmTop();
            }

            if (cancel != null)
            {
                cancel.text = string.IsNullOrWhiteSpace(request.Value.CancelText) ? "Cancel" : request.Value.CancelText;
                cancel.clicked += () => _context.ModalService.TryCancelTop();
            }

            layer.Add(popup);
        }

        private void RenderToasts()
        {
            var layer = _uiRoot?.OverlayLayer;
            if (layer == null)
            {
                return;
            }

            layer.Clear();

            var combined = new List<ActiveToast>();
            combined.AddRange(_context.ToastService.SnapshotActiveToasts());
            combined.AddRange(_context.PersistentToastService.SnapshotActiveToasts());

            combined.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            for (var i = 0; i < combined.Count; i++)
            {
                var toast = combined[i];
                var element = CloneToastVisual() ?? BuildFallbackToastVisual();
                var label = element.Q<Label>("toast-message");
                if (label != null)
                {
                    label.text = toast.Message;
                }

                element.AddToClassList($"cs6-toast-priority-{toast.Priority.ToString().ToLowerInvariant()}");
                layer.Add(element);
            }
        }

        private VisualElement ClonePopupVisual()
        {
            var template = _popupVisualTree != null ? _popupVisualTree : Resources.Load<VisualTreeAsset>("CodexSixUiKit/Popup");
            if (template == null)
            {
                return null;
            }

            var container = new VisualElement();
            template.CloneTree(container);
            return container.childCount > 0 ? container[0] : container;
        }

        private VisualElement CloneToastVisual()
        {
            var template = _toastVisualTree != null ? _toastVisualTree : Resources.Load<VisualTreeAsset>("CodexSixUiKit/Toast");
            if (template == null)
            {
                return null;
            }

            var container = new VisualElement();
            template.CloneTree(container);
            return container.childCount > 0 ? container[0] : container;
        }

        private static VisualElement BuildFallbackPopupVisual()
        {
            var popup = new VisualElement();
            popup.AddToClassList("cs6-popup");

            var title = new Label("Popup") { name = "popup-title" };
            title.AddToClassList("cs6-popup-title");
            popup.Add(title);

            var body = new Label("-") { name = "popup-body" };
            body.AddToClassList("cs6-popup-body");
            popup.Add(body);

            var buttons = new VisualElement();
            buttons.AddToClassList("cs6-popup-buttons");

            var cancel = new Button { name = "cancel-button", text = "Cancel" };
            var confirm = new Button { name = "confirm-button", text = "Confirm" };
            buttons.Add(cancel);
            buttons.Add(confirm);
            popup.Add(buttons);

            return popup;
        }

        private static VisualElement BuildFallbackToastVisual()
        {
            var toast = new VisualElement();
            toast.AddToClassList("cs6-toast-item");
            var label = new Label { name = "toast-message", text = "toast" };
            toast.Add(label);
            return toast;
        }
    }
}
