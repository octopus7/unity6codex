using System.Collections.Generic;
using CodexSix.UguiRuntime;
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class AttendanceModalPopup : UiPopupView
    {
        private sealed class TabEntry
        {
            public string EventId;
            public AttendanceUiFactory.ButtonParts Button;
        }

        private readonly List<TabEntry> _tabs = new();

        private AttendanceUiController _controller;
        private AttendanceTrackView _trackView;
        private Text _subtitleLabel;
        private Text _debugLabel;
        private RectTransform _tabBar;
        private RectTransform _debugBar;
        private string _selectedEventId = string.Empty;

        private void Awake()
        {
            var root = AttendanceUiFactory.EnsureStretchRoot(gameObject);

            var panel = AttendanceUiFactory.CreateRect("Panel", root);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1500f, 840f);
            AttendanceUiFactory.AddImage(panel.gameObject, new Color(0.07f, 0.08f, 0.11f, 0.98f));
            AttendanceUiFactory.AddVerticalLayout(panel.gameObject, 24, 24, 24, 24, 16f);

            var header = AttendanceUiFactory.CreateRect("Header", panel);
            AttendanceUiFactory.AddHorizontalLayout(header.gameObject, 0, 0, 0, 0, 12f);
            var title = AttendanceUiFactory.CreateLabel(header, "Title", "Attendance Event", 32, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            AttendanceUiFactory.CreateButton(
                header,
                "CloseButton",
                "Close",
                HandleCloseClicked,
                new Color(0.34f, 0.2f, 0.2f, 1f),
                fontSize: 16,
                minHeight: 40f);

            _subtitleLabel = AttendanceUiFactory.CreateLabel(panel, "Subtitle", string.Empty, 15, TextAnchor.MiddleLeft);

            _tabBar = AttendanceUiFactory.CreateRect("TabBar", panel);
            AttendanceUiFactory.AddHorizontalLayout(_tabBar.gameObject, 0, 0, 0, 0, 10f);

            var trackHost = AttendanceUiFactory.CreateRect("TrackHost", panel);
            var trackHostLayout = trackHost.gameObject.AddComponent<LayoutElement>();
            trackHostLayout.flexibleHeight = 1f;
            trackHostLayout.preferredHeight = 560f;
            AttendanceUiFactory.AddImage(trackHost.gameObject, new Color(0.12f, 0.13f, 0.16f, 0.94f));

            var trackRoot = AttendanceUiFactory.CreateRect("TrackView", trackHost);
            AttendanceUiFactory.StretchToParent(trackRoot);
            _trackView = trackRoot.gameObject.AddComponent<AttendanceTrackView>();

            _debugBar = AttendanceUiFactory.CreateRect("DebugBar", panel);
            AttendanceUiFactory.AddHorizontalLayout(_debugBar.gameObject, 0, 0, 0, 0, 10f);
            _debugLabel = AttendanceUiFactory.CreateLabel(_debugBar, "DebugLabel", string.Empty, 14, TextAnchor.MiddleLeft);
            _debugLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            AttendanceUiFactory.CreateButton(
                _debugBar,
                "DebugMinusButton",
                "-1 Day",
                () => ResolveController()?.AdjustDebugDayOffset(-1),
                new Color(0.2f, 0.26f, 0.34f, 1f),
                fontSize: 14,
                minHeight: 34f);
            AttendanceUiFactory.CreateButton(
                _debugBar,
                "DebugPlusButton",
                "+1 Day",
                () => ResolveController()?.AdjustDebugDayOffset(1),
                new Color(0.2f, 0.26f, 0.34f, 1f),
                fontSize: 14,
                minHeight: 34f);
            AttendanceUiFactory.CreateButton(
                _debugBar,
                "ResetDataButton",
                "Reset Data",
                () => ResolveController()?.ResetDemoData(),
                new Color(0.45f, 0.22f, 0.18f, 1f),
                fontSize: 14,
                minHeight: 34f);
        }

        public override void Bind(UiPopupRequest request, UiModalHandle handle)
        {
            base.Bind(request, handle);

            _controller = ResolveController();
            if (_controller == null)
            {
                if (_subtitleLabel != null)
                {
                    _subtitleLabel.text = "Attendance controller not found.";
                }

                return;
            }

            _controller.StateChanged -= Refresh;
            _controller.StateChanged += Refresh;

            RebuildTabs();

            var payload = request.Payload as AttendanceModalPayload;
            _selectedEventId = payload != null ? payload.PreferredEventId : string.Empty;
            if (string.IsNullOrWhiteSpace(_selectedEventId))
            {
                _selectedEventId = _controller.GetFirstClaimableEventId();
            }

            if (string.IsNullOrWhiteSpace(_selectedEventId))
            {
                var events = _controller.GetOrderedEvents();
                if (events.Count > 0 && events[0] != null)
                {
                    _selectedEventId = events[0].EventId;
                }
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }
        }

        private AttendanceUiController ResolveController()
        {
            return Context != null ? Context.GetComponent<AttendanceUiController>() : FindFirstObjectByType<AttendanceUiController>();
        }

        private void RebuildTabs()
        {
            _tabs.Clear();
            for (var i = _tabBar.childCount - 1; i >= 0; i--)
            {
                Destroy(_tabBar.GetChild(i).gameObject);
            }

            var events = _controller.GetOrderedEvents();
            for (var i = 0; i < events.Count; i++)
            {
                var definition = events[i];
                if (definition == null)
                {
                    continue;
                }

                var eventId = definition.EventId;
                var buttonParts = AttendanceUiFactory.CreateButton(
                    _tabBar,
                    $"Tab_{eventId}",
                    definition.DisplayName,
                    () => SelectTab(eventId),
                    new Color(0.17f, 0.21f, 0.29f, 1f),
                    fontSize: 16,
                    minHeight: 40f);

                _tabs.Add(new TabEntry
                {
                    EventId = eventId,
                    Button = buttonParts
                });
            }
        }

        private void SelectTab(string eventId)
        {
            _selectedEventId = eventId ?? string.Empty;
            Refresh();
        }

        private void HandleCloseClicked()
        {
            if (Handle.Cancel())
            {
                return;
            }

            Context?.ModalService.TryCancelTop();
        }

        private void Refresh()
        {
            if (_controller == null)
            {
                return;
            }

            var events = _controller.GetOrderedEvents();
            AttendanceEventDefinition selectedDefinition = null;
            for (var i = 0; i < events.Count; i++)
            {
                var definition = events[i];
                if (definition != null && string.Equals(definition.EventId, _selectedEventId, System.StringComparison.Ordinal))
                {
                    selectedDefinition = definition;
                    break;
                }
            }

            if (selectedDefinition == null && events.Count > 0)
            {
                selectedDefinition = events[0];
                _selectedEventId = selectedDefinition != null ? selectedDefinition.EventId : string.Empty;
            }

            for (var i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                if (tab == null)
                {
                    continue;
                }

                var isSelected = string.Equals(tab.EventId, _selectedEventId, System.StringComparison.Ordinal);
                tab.Button.Background.color = isSelected
                    ? new Color(0.72f, 0.52f, 0.16f, 1f)
                    : new Color(0.17f, 0.21f, 0.29f, 1f);
            }

            if (selectedDefinition != null)
            {
                _trackView.Bind(_controller, selectedDefinition);
                var snapshot = _controller.BuildTrackSnapshot(selectedDefinition);
                _subtitleLabel.text =
                    $"{selectedDefinition.DisplayName}  |  Today: {_controller.GetTodayDateKey()}  |  " +
                    (snapshot.CanClaimToday ? "Reward Ready" : snapshot.StatusText);
            }
            else
            {
                _subtitleLabel.text = "No attendance events configured.";
            }

            _debugBar.gameObject.SetActive(_controller.DebugControlsVisible);
            if (_controller.DebugControlsVisible)
            {
                _debugLabel.text = $"Debug day offset: {_controller.DebugDayOffsetDays:+#;-#;0}";
            }
        }
    }
}
