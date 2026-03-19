#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoGameplayPanel : MonoBehaviour
    {
        private StackedUguiDemoController? _controller;
        private Text? _statusLabel;

        private void Awake()
        {
            StackedUguiDemoUiFactory.ConfigureOverlayCanvas(gameObject, sortingOrder: -10);

            var root = StackedUguiDemoUiFactory.EnsureStretchRoot(gameObject);
            var panel = StackedUguiDemoUiFactory.CreateSidebarPanel(root, "GameplayPanel");
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-380f, 24f);
            panel.offsetMax = new Vector2(-24f, -24f);

            StackedUguiDemoUiFactory.CreateLabel(panel, "Title", "Gameplay Mock", 26, FontStyle.Bold);
            StackedUguiDemoUiFactory.CreateLabel(panel, "Hint", "Click the button or press Space.\nWhen a modal is open, clicks are raycast-blocked and Space is ignored through InputBlockService.", 15);
            _statusLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Status", string.Empty, 16);
            StackedUguiDemoUiFactory.CreateButton(panel, "ActionButton", "Gameplay Action +1", HandleGameplayButton);
        }

        private void Start()
        {
            _controller = FindFirstObjectByType<StackedUguiDemoController>();
            if (_controller != null)
            {
                _controller.StateChanged += Refresh;
                Refresh();
            }
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }
        }

        private void HandleGameplayButton()
        {
            _controller?.TryGameplayAction();
        }

        private void Refresh()
        {
            if (_controller == null || _statusLabel == null)
            {
                return;
            }

            _statusLabel.text =
                $"Gameplay Actions: {_controller.GameplayActionCount}\n" +
                $"Blocked: {_controller.IsGameplayBlocked}\n" +
                $"Confirmed: {_controller.ConfirmedPopupCount} / Cancelled: {_controller.CancelledPopupCount} / Dismissed: {_controller.DismissedPopupCount}";
        }
    }
}
