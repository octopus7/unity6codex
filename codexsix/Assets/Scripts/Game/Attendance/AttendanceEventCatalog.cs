using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public enum AttendancePresentationMode
    {
        Fixed = 1,
        HorizontalScroll = 2
    }

    public enum AttendanceRewardKind
    {
        Coins = 1,
        Gems = 2,
        Item = 3
    }

    [CreateAssetMenu(fileName = "AttendanceEventCatalog", menuName = "CodexSix/Attendance/Event Catalog")]
    public sealed class AttendanceEventCatalog : ScriptableObject
    {
        [SerializeField] private List<AttendanceEventDefinition> _events = new();

        public List<AttendanceEventDefinition> Events => _events;

        public bool TryGetEvent(string eventId, out AttendanceEventDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return false;
            }

            for (var i = 0; i < _events.Count; i++)
            {
                var candidate = _events[i];
                if (candidate == null || !string.Equals(candidate.EventId, eventId, StringComparison.Ordinal))
                {
                    continue;
                }

                definition = candidate;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public sealed class AttendanceEventDefinition
    {
        [SerializeField] private string _eventId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private AttendancePresentationMode _presentationMode = AttendancePresentationMode.Fixed;
        [SerializeField] private List<AttendanceDayDefinition> _days = new();

        public string EventId
        {
            get => _eventId;
            set => _eventId = value ?? string.Empty;
        }

        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value ?? string.Empty;
        }

        public AttendancePresentationMode PresentationMode
        {
            get => _presentationMode;
            set => _presentationMode = value;
        }

        public List<AttendanceDayDefinition> Days => _days;
        public int TotalDays => _days != null ? _days.Count : 0;
    }

    [Serializable]
    public sealed class AttendanceDayDefinition
    {
        [SerializeField] private int _dayNumber = 1;
        [SerializeField] private string _title = string.Empty;
        [SerializeField] private string _rewardLabel = string.Empty;
        [SerializeField] private List<AttendanceRewardDefinition> _rewards = new();

        public int DayNumber
        {
            get => _dayNumber;
            set => _dayNumber = Mathf.Max(1, value);
        }

        public string Title
        {
            get => _title;
            set => _title = value ?? string.Empty;
        }

        public string RewardLabel
        {
            get => _rewardLabel;
            set => _rewardLabel = value ?? string.Empty;
        }

        public List<AttendanceRewardDefinition> Rewards => _rewards;
    }

    [Serializable]
    public sealed class AttendanceRewardDefinition
    {
        [SerializeField] private AttendanceRewardKind _kind = AttendanceRewardKind.Coins;
        [SerializeField] private int _amount = 1;
        [SerializeField] private int _itemId;
        [SerializeField] private string _overrideLabel = string.Empty;

        public AttendanceRewardKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public int Amount
        {
            get => _amount;
            set => _amount = Mathf.Max(0, value);
        }

        public int ItemId
        {
            get => _itemId;
            set => _itemId = Mathf.Max(0, value);
        }

        public string OverrideLabel
        {
            get => _overrideLabel;
            set => _overrideLabel = value ?? string.Empty;
        }
    }
}
