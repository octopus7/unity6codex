using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.TopdownShooter.Game
{
    public sealed class AttendanceTrackView : MonoBehaviour
    {
        private sealed class DayCell
        {
            public GameObject Root;
            public Image Background;
            public Text DayLabel;
            public Text RewardLabel;
            public Text StateLabel;
            public LayoutElement LayoutElement;
        }

        private AttendanceUiController _controller;
        private AttendanceEventDefinition _definition;
        private Text _headlineLabel;
        private Text _statusLabel;
        private AttendanceUiFactory.ButtonParts _claimButton;
        private RectTransform _fixedLayoutRoot;
        private RectTransform _scrollLayoutRoot;
        private RectTransform _scrollContent;
        private ScrollRect _scrollRect;
        private readonly List<DayCell> _fixedCells = new();
        private readonly List<DayCell> _scrollCells = new();

        private void Awake()
        {
            var root = AttendanceUiFactory.EnsureStretchRoot(gameObject);
            AttendanceUiFactory.AddVerticalLayout(root.gameObject, 0, 0, 0, 0, 14f);

            _headlineLabel = AttendanceUiFactory.CreateLabel(root, "Headline", "Attendance", 24, TextAnchor.MiddleLeft, FontStyle.Bold);
            _statusLabel = AttendanceUiFactory.CreateLabel(root, "Status", string.Empty, 15, TextAnchor.MiddleLeft);

            var contentHost = AttendanceUiFactory.CreateRect("ContentHost", root);
            var contentLayout = contentHost.gameObject.AddComponent<LayoutElement>();
            contentLayout.preferredHeight = 460f;
            contentLayout.flexibleHeight = 1f;

            _fixedLayoutRoot = AttendanceUiFactory.CreateRect("FixedLayoutRoot", contentHost);
            AttendanceUiFactory.StretchToParent(_fixedLayoutRoot);
            AttendanceUiFactory.AddHorizontalLayout(_fixedLayoutRoot.gameObject, 0, 0, 0, 0, 14f);
            for (var i = 0; i < 14; i++)
            {
                _fixedCells.Add(CreateDayCell(_fixedLayoutRoot, $"FixedDay_{i + 1}", fixedWidth: 156f));
            }

            var scrollParts = AttendanceUiFactory.CreateHorizontalScrollView(contentHost, "ScrollLayoutRoot");
            _scrollLayoutRoot = scrollParts.ScrollRect.GetComponent<RectTransform>();
            _scrollContent = scrollParts.Content;
            _scrollRect = scrollParts.ScrollRect;

            var footer = AttendanceUiFactory.CreateRect("Footer", root);
            AttendanceUiFactory.AddHorizontalLayout(footer.gameObject, 0, 0, 0, 0, 12f);
            _claimButton = AttendanceUiFactory.CreateButton(
                footer,
                "ClaimButton",
                "Claim",
                HandleClaimClicked,
                new Color(0.77f, 0.56f, 0.15f, 1f),
                fontSize: 18,
                minHeight: 46f);
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }
        }

        public void Bind(AttendanceUiController controller, AttendanceEventDefinition definition)
        {
            if (_controller != null)
            {
                _controller.StateChanged -= Refresh;
            }

            _controller = controller;
            _definition = definition;

            if (_controller != null)
            {
                _controller.StateChanged += Refresh;
            }

            Refresh();
        }

        private void HandleClaimClicked()
        {
            if (_controller == null || _definition == null)
            {
                return;
            }

            _controller.TryClaimReward(_definition.EventId, out var message);
            if (_statusLabel != null && !string.IsNullOrWhiteSpace(message))
            {
                _statusLabel.text = message;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_controller == null || _definition == null)
            {
                return;
            }

            var snapshot = _controller.BuildTrackSnapshot(_definition);
            _headlineLabel.text = snapshot.DisplayName;
            _statusLabel.text = snapshot.StatusText;
            _claimButton.Button.interactable = snapshot.CanClaimToday;
            _claimButton.Label.text = snapshot.ClaimButtonText;

            var useScroll = snapshot.PresentationMode == AttendancePresentationMode.HorizontalScroll;
            _fixedLayoutRoot.gameObject.SetActive(!useScroll);
            _scrollLayoutRoot.gameObject.SetActive(useScroll);

            if (useScroll)
            {
                RenderScrollCells(snapshot);
            }
            else
            {
                RenderFixedCells(snapshot);
            }
        }

        private void RenderFixedCells(AttendanceTrackSnapshot snapshot)
        {
            for (var i = 0; i < _fixedCells.Count; i++)
            {
                var cell = _fixedCells[i];
                var isVisible = i < snapshot.DaySnapshots.Count;
                cell.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                ApplyDaySnapshot(cell, snapshot.DaySnapshots[i]);
            }
        }

        private void RenderScrollCells(AttendanceTrackSnapshot snapshot)
        {
            EnsureScrollCellCount(snapshot.DaySnapshots.Count);

            for (var i = 0; i < _scrollCells.Count; i++)
            {
                var cell = _scrollCells[i];
                var isVisible = i < snapshot.DaySnapshots.Count;
                cell.Root.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                ApplyDaySnapshot(cell, snapshot.DaySnapshots[i]);
            }

            if (_scrollRect != null && snapshot.DaySnapshots.Count > 1)
            {
                var targetIndex = Mathf.Clamp(snapshot.CurrentDayNumber - 1, 0, snapshot.DaySnapshots.Count - 1);
                _scrollRect.horizontalNormalizedPosition = targetIndex / (float)(snapshot.DaySnapshots.Count - 1);
            }
        }

        private void EnsureScrollCellCount(int requiredCount)
        {
            while (_scrollCells.Count < requiredCount)
            {
                _scrollCells.Add(CreateDayCell(_scrollContent, $"ScrollDay_{_scrollCells.Count + 1}", fixedWidth: 180f));
            }
        }

        private static DayCell CreateDayCell(Transform parent, string name, float fixedWidth)
        {
            var rect = AttendanceUiFactory.CreateRect(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = fixedWidth;
            layout.preferredWidth = fixedWidth;
            layout.minHeight = 240f;
            layout.preferredHeight = 240f;

            var background = AttendanceUiFactory.AddImage(rect.gameObject, new Color(0.18f, 0.19f, 0.22f, 0.96f));
            AttendanceUiFactory.AddVerticalLayout(rect.gameObject, 14, 14, 14, 14, 10f);

            var dayLabel = AttendanceUiFactory.CreateLabel(rect, "DayLabel", string.Empty, 20, TextAnchor.MiddleLeft, FontStyle.Bold);
            var rewardLabel = AttendanceUiFactory.CreateLabel(rect, "RewardLabel", string.Empty, 16, TextAnchor.UpperLeft);
            var rewardLayout = rewardLabel.gameObject.AddComponent<LayoutElement>();
            rewardLayout.flexibleHeight = 1f;
            rewardLayout.preferredHeight = 120f;
            var stateLabel = AttendanceUiFactory.CreateLabel(rect, "StateLabel", string.Empty, 14, TextAnchor.MiddleCenter, FontStyle.Bold);

            return new DayCell
            {
                Root = rect.gameObject,
                Background = background,
                DayLabel = dayLabel,
                RewardLabel = rewardLabel,
                StateLabel = stateLabel,
                LayoutElement = layout
            };
        }

        private static void ApplyDaySnapshot(DayCell cell, AttendanceDaySnapshot snapshot)
        {
            cell.DayLabel.text = string.IsNullOrWhiteSpace(snapshot.Title) ? $"Day {snapshot.DayNumber}" : snapshot.Title;
            cell.RewardLabel.text = snapshot.RewardText;

            switch (snapshot.VisualState)
            {
                case AttendanceDayVisualState.Claimed:
                    cell.Background.color = new Color(0.16f, 0.43f, 0.27f, 0.98f);
                    cell.StateLabel.text = "Claimed";
                    break;
                case AttendanceDayVisualState.Claimable:
                    cell.Background.color = new Color(0.63f, 0.48f, 0.12f, 0.98f);
                    cell.StateLabel.text = "Ready";
                    break;
                default:
                    cell.Background.color = new Color(0.19f, 0.2f, 0.24f, 0.96f);
                    cell.StateLabel.text = "Locked";
                    break;
            }
        }
    }
}
