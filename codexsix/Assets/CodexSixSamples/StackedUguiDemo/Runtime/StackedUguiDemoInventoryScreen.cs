#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoInventoryScreen : UiScreenView
    {
        private StackedUguiDemoController? _controller;
        private Text? _statusLabel;

        private void Awake()
        {
            var root = StackedUguiDemoUiFactory.EnsureStretchRoot(gameObject);
            var panel = StackedUguiDemoUiFactory.CreateSidebarPanel(root, "InventoryPanel");

            StackedUguiDemoUiFactory.CreateLabel(panel, "Title", "Inventory Screen", 28, FontStyle.Bold);
            StackedUguiDemoUiFactory.CreateLabel(panel, "Body", "This screen sits on the same screen layer.\nUse Escape or Back to return through history.", 15);
            _statusLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Status", string.Empty, 16);

            StackedUguiDemoUiFactory.CreateButton(panel, "BackButton", "Back", () => ResolveController()?.GoBack());
            StackedUguiDemoUiFactory.CreateButton(panel, "SettingsButton", "Open Settings", () => ResolveController()?.ShowSettings());
            StackedUguiDemoUiFactory.CreateButton(panel, "ConfirmPopupButton", "Open Confirm Popup", () => ResolveController()?.ShowConfirmPopup());
        }

        protected override void OnAttached()
        {
            _controller = ResolveController();
            if (_controller != null)
            {
                _controller.StateChanged += Refresh;
            }
        }

        protected override void OnShow()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }
        }

        private StackedUguiDemoController? ResolveController()
        {
            return Context != null ? Context.GetComponent<StackedUguiDemoController>() : FindFirstObjectByType<StackedUguiDemoController>();
        }

        private void Refresh()
        {
            if (_statusLabel == null)
            {
                return;
            }

            _controller ??= ResolveController();
            _statusLabel.text = _controller == null
                ? "Controller not found."
                : $"Current Screen: inventory\nGameplay Actions: {_controller.GameplayActionCount}\nConfirmed Popups: {_controller.ConfirmedPopupCount}";
        }
    }
}
