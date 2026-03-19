#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoNoticePopup : UiPopupView
    {
        private Text? _titleLabel;
        private Text? _bodyLabel;
        private Button? _confirmButton;
        private Text? _confirmButtonLabel;
        private Button? _cancelButton;
        private Text? _cancelButtonLabel;

        private void Awake()
        {
            var root = StackedUguiDemoUiFactory.EnsureStretchRoot(gameObject);
            var panel = StackedUguiDemoUiFactory.CreateCenteredPanel(root, "NoticePanel", new Vector2(480f, 260f), new Color(0.15f, 0.18f, 0.24f, 0.98f));

            _titleLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Title", "Notice", 26, FontStyle.Bold);
            _bodyLabel = StackedUguiDemoUiFactory.CreateLabel(panel, "Body", string.Empty, 16);
            _confirmButton = StackedUguiDemoUiFactory.CreateButton(panel, "OkButton", "OK", () => Handle.Confirm());
            _confirmButtonLabel = _confirmButton.GetComponentInChildren<Text>();
            _cancelButton = StackedUguiDemoUiFactory.CreateButton(panel, "DismissButton", "Dismiss", () => Handle.Cancel());
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
                _confirmButtonLabel.text = string.IsNullOrWhiteSpace(Request.ConfirmText) ? "OK" : Request.ConfirmText;
            }

            if (_cancelButtonLabel != null)
            {
                _cancelButtonLabel.text = string.IsNullOrWhiteSpace(Request.CancelText) ? "Dismiss" : Request.CancelText;
            }

            if (_cancelButton != null)
            {
                _cancelButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(Request.CancelText));
            }
        }
    }
}
