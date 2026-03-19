using CodexSix.UguiRuntime;
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class AttendanceLauncherScreen : UiScreenView
    {
        private AttendanceUiController _controller;
        private AttendanceUiFactory.ButtonParts _openButton;
        private Text _statusLabel;

        private void Awake()
        {
            var root = AttendanceUiFactory.EnsureStretchRoot(gameObject);

            var anchor = AttendanceUiFactory.CreateRect("LauncherAnchor", root);
            anchor.anchorMin = new Vector2(1f, 1f);
            anchor.anchorMax = new Vector2(1f, 1f);
            anchor.pivot = new Vector2(1f, 1f);
            anchor.sizeDelta = new Vector2(240f, 96f);
            anchor.anchoredPosition = new Vector2(-24f, -24f);

            AttendanceUiFactory.AddImage(anchor.gameObject, new Color(0.06f, 0.08f, 0.12f, 0.92f));
            AttendanceUiFactory.AddVerticalLayout(anchor.gameObject, 16, 16, 14, 14, 8f);

            AttendanceUiFactory.CreateLabel(anchor, "Title", "Attendance Event", 18, TextAnchor.MiddleLeft, FontStyle.Bold);
            _statusLabel = AttendanceUiFactory.CreateLabel(anchor, "Status", "Loading...", 13, TextAnchor.MiddleLeft);

            _openButton = AttendanceUiFactory.CreateButton(
                anchor,
                "OpenButton",
                "Open Attendance",
                HandleOpenClicked,
                new Color(0.77f, 0.56f, 0.15f, 1f),
                fontSize: 16,
                minHeight: 34f);
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

        private void HandleOpenClicked()
        {
            ResolveController()?.OpenAttendanceModal();
        }

        private AttendanceUiController ResolveController()
        {
            return Context != null ? Context.GetComponent<AttendanceUiController>() : FindFirstObjectByType<AttendanceUiController>();
        }

        private void Refresh()
        {
            _controller ??= ResolveController();
            if (_controller == null)
            {
                if (_statusLabel != null)
                {
                    _statusLabel.text = "Controller not found.";
                }

                return;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = _controller.GetLauncherStatusText();
            }

            if (_openButton.Label != null)
            {
                _openButton.Label.text = "Open Attendance";
            }
        }
    }
}
