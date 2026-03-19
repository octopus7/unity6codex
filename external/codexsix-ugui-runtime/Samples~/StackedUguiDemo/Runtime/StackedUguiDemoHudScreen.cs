#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoHudScreen : UiScreenView
    {
        private StackedUguiDemoController? _controller;
        private Text? _statusLabel;

        private void Awake()
        {
            var root = StackedUguiDemoUiFactory.EnsureStretchRoot(gameObject);
            var panel = StackedUguiDemoUiFactory.CreateSidebarPanel(root, "HudPanel");

            StackedUguiDemoUiFactory.CreateLabel(panel, "Title", "HUD Screen", 28, FontStyle.Bold);
            StackedUguiDemoUiFactory.CreateLabel(panel, "Body", "Single active screen with back history.\nButtons below use only UiContext services.", 15);
            _statusLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Status", string.Empty, 16);

            StackedUguiDemoUiFactory.CreateButton(panel, "InventoryButton", "Open Inventory", () => ResolveController()?.ShowInventory());
            StackedUguiDemoUiFactory.CreateButton(panel, "SettingsButton", "Open Settings", () => ResolveController()?.ShowSettings());
            StackedUguiDemoUiFactory.CreateButton(panel, "ConfirmPopupButton", "Open Confirm Popup", () => ResolveController()?.ShowConfirmPopup());
            StackedUguiDemoUiFactory.CreateButton(panel, "NoticePopupButton", "Open Dismissible Notice", () => ResolveController()?.ShowDismissibleNotice());
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
            if (_controller == null)
            {
                _statusLabel.text = "Controller not found.";
                return;
            }

            _statusLabel.text =
                $"Current Screen: hud\n" +
                $"Gameplay Blocked: {_controller.IsGameplayBlocked}\n" +
                $"Gameplay Actions: {_controller.GameplayActionCount}";
        }
    }
}
