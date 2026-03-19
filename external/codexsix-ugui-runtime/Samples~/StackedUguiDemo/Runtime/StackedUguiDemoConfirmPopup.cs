#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoConfirmPopup : UiPopupView
    {
        private Text? _titleLabel;
        private Text? _bodyLabel;
        private Button? _confirmButton;
        private Button? _cancelButton;
        private Text? _confirmButtonLabel;
        private Text? _cancelButtonLabel;

        private void Awake()
        {
            var root = StackedUguiDemoUiFactory.EnsureStretchRoot(gameObject);
            var panel = StackedUguiDemoUiFactory.CreateCenteredPanel(root, "PopupPanel", new Vector2(520f, 320f), new Color(0.11f, 0.13f, 0.17f, 0.98f));

            _titleLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Title", "Confirm", 28, FontStyle.Bold);
            _bodyLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Body", string.Empty, 16);
            StackedUguiDemoUiFactory.CreateButton(panel, "NestedNoticeButton", "Open Nested Notice", OpenNestedNotice);
            _confirmButton = StackedUguiDemoUiFactory.CreateButton(panel, "ConfirmButton", "Confirm", () => Handle.Confirm());
            _cancelButton = StackedUguiDemoUiFactory.CreateButton(panel, "CancelButton", "Cancel", () => Handle.Cancel());
            _confirmButtonLabel = _confirmButton.GetComponentInChildren<Text>();
            _cancelButtonLabel = _cancelButton.GetComponentInChildren<Text>();
        }

        protected override void OnBound()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = Request.Title;
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = Request.Message;
            }

            if (_confirmButtonLabel != null)
            {
                _confirmButtonLabel.text = string.IsNullOrWhiteSpace(Request.ConfirmText) ? "Confirm" : Request.ConfirmText;
            }

            if (_cancelButtonLabel != null)
            {
                _cancelButtonLabel.text = string.IsNullOrWhiteSpace(Request.CancelText) ? "Cancel" : Request.CancelText;
            }

            if (_cancelButton != null)
            {
                _cancelButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(Request.CancelText));
            }
        }

        private void OpenNestedNotice()
        {
            var controller = Context != null ? Context.GetComponent<StackedUguiDemoController>() : null;
            controller?.ShowDismissibleNotice();
        }
    }
}
