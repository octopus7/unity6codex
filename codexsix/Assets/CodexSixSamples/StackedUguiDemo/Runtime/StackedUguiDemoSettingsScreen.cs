#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoSettingsScreen : UiScreenView
    {
        private StackedUguiDemoController? _controller;
        private Text? _statusLabel;

        private void Awake()
        {
            var root = StackedUguiDemoUiFactory.EnsureStretchRoot(gameObject);
            var panel = StackedUguiDemoUiFactory.CreateSidebarPanel(root, "SettingsPanel");

            StackedUguiDemoUiFactory.CreateLabel(panel, "Title", "Settings Screen", 28, FontStyle.Bold);
            StackedUguiDemoUiFactory.CreateLabel(panel, "Body", "Use Enter to confirm the top modal.\nUse Escape to cancel the top modal or go back when no modal exists.", 15);
            _statusLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Status", string.Empty, 16);

            StackedUguiDemoUiFactory.CreateButton(panel, "BackButton", "Back", () => ResolveController()?.GoBack());
            StackedUguiDemoUiFactory.CreateButton(panel, "HudButton", "Show HUD", () => ResolveController()?.ShowHud());
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
            _statusLabel.text = _controller == null
                ? "Controller not found."
                : $"Current Screen: settings\nCancelled Popups: {_controller.CancelledPopupCount}\nDismissed Popups: {_controller.DismissedPopupCount}";
        }
    }
}
