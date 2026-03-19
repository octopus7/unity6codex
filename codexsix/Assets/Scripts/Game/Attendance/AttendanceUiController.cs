using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodexSix.TopdownShooter.Net;
using CodexSix.UguiRuntime;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    [DefaultExecutionOrder(-200)]
    public sealed class AttendanceUiController : MonoBehaviour
    {
        public const string LauncherScreenId = "attendance-launcher";
        public const string ModalPopupId = "attendance-modal";

        public NetworkGameClient Client;
        public LocalInputSender InputSender;
        public GrowthProgressionManager GrowthProgressionManager;
        public PlayerInventoryManager InventoryManager;
        public ItemDataManager ItemDataManager;
        public UiRuntimeInstaller UiRuntimeInstaller;
        public UiCatalog UiCatalogAsset;
        public AttendanceEventCatalog EventCatalog;
        public bool ShowDebugControls = true;
        [Range(-30, 30)] public int DebugDayOffsetDays;

        private readonly Dictionary<string, AttendanceEventProgressRecord> _progressByEventId = new(StringComparer.Ordinal);

        private AttendanceProfileStore _profileStore;
        private AttendanceProfileDocument _profile;
        private AttendanceRewardApplier _rewardApplier;
        private AttendanceAutoOpenCoordinator _autoOpenCoordinator;
        private UiContext _uiContext;
        private ConnectionState _lastConnectionState = ConnectionState.Disconnected;
        private bool _autoOpenedThisSession;
        private Task<UiPopupResult> _openModalTask;

        public event Action StateChanged;

        public UiContext Context => _uiContext;
        public bool DebugControlsVisible => ShowDebugControls && (Application.isEditor || Debug.isDebugBuild);

        private void Awake()
        {
            BindReferences();

            if (UiRuntimeInstaller != null && UiCatalogAsset != null)
            {
                UiRuntimeInstaller.Catalog = UiCatalogAsset;
            }

            _profileStore = new AttendanceProfileStore();
            _profile = _profileStore.Load();
            SyncProgressLookup();
            _rewardApplier = new AttendanceRewardApplier(Client, GrowthProgressionManager, InventoryManager, ItemDataManager);
            _autoOpenCoordinator = new AttendanceAutoOpenCoordinator();
            ApplyStoredBonusCoins();
        }

        private void OnEnable()
        {
            BindReferences();
            if (Client != null)
            {
                Client.SessionReady += HandleSessionReady;
            }
        }

        private void Start()
        {
            BindReferences();
            _uiContext = GetComponent<UiContext>();
            if (_uiContext != null)
            {
                _uiContext.ScreenService.Show(LauncherScreenId);
                _uiContext.InputBlockService.GameplayBlockChanged += HandleGameplayBlockChanged;
                HandleGameplayBlockChanged(_uiContext.InputBlockService.IsGameplayInputBlocked);
            }
        }

        private void Update()
        {
            if (Client == null)
            {
                return;
            }

            var currentState = Client.CurrentConnectionState;
            if (currentState == _lastConnectionState)
            {
                return;
            }

            if (currentState == ConnectionState.Disconnected)
            {
                _autoOpenedThisSession = false;
            }

            _lastConnectionState = currentState;
            StateChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (Client != null)
            {
                Client.SessionReady -= HandleSessionReady;
            }

            if (_uiContext != null)
            {
                _uiContext.InputBlockService.GameplayBlockChanged -= HandleGameplayBlockChanged;
            }

            if (InputSender != null)
            {
                InputSender.ExternalUiFireBlock = false;
            }
        }

        public IReadOnlyList<AttendanceEventDefinition> GetOrderedEvents()
        {
            return EventCatalog != null ? EventCatalog.Events : Array.Empty<AttendanceEventDefinition>();
        }

        public string GetTodayDateKey()
        {
            return DateTime.Now.Date.AddDays(DebugDayOffsetDays).ToString("yyyy-MM-dd");
        }

        public string GetFirstClaimableEventId()
        {
            if (Client == null || Client.CurrentConnectionState != ConnectionState.Connected || Client.LocalPlayerId <= 0)
            {
                return string.Empty;
            }

            if (EventCatalog == null || EventCatalog.Events == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < EventCatalog.Events.Count; i++)
            {
                var definition = EventCatalog.Events[i];
                if (definition == null)
                {
                    continue;
                }

                var snapshot = BuildTrackSnapshot(definition);
                if (snapshot.CanClaimToday)
                {
                    return definition.EventId;
                }
            }

            return string.Empty;
        }

        public AttendanceTrackSnapshot BuildTrackSnapshot(AttendanceEventDefinition definition)
        {
            var snapshot = new AttendanceTrackSnapshot
            {
                EventId = definition != null ? definition.EventId : string.Empty,
                DisplayName = definition != null ? definition.DisplayName : string.Empty,
                PresentationMode = definition != null ? definition.PresentationMode : AttendancePresentationMode.Fixed,
                DaySnapshots = new List<AttendanceDaySnapshot>()
            };

            if (definition == null || definition.Days == null || definition.Days.Count == 0)
            {
                snapshot.StatusText = "No attendance days configured.";
                snapshot.ClaimButtonText = "Claim";
                return snapshot;
            }

            var progress = GetOrCreateProgress(definition.EventId);
            var claimedDayCount = Mathf.Clamp(progress.ClaimedDayCount, 0, definition.TotalDays);
            var todayKey = GetTodayDateKey();
            var alreadyClaimedToday = string.Equals(progress.LastClaimDateKey, todayKey, StringComparison.Ordinal);
            var isCompleted = claimedDayCount >= definition.TotalDays;
            var nextDayNumber = Mathf.Clamp(claimedDayCount + 1, 1, definition.TotalDays);
            var isConnected = Client != null &&
                              Client.CurrentConnectionState == ConnectionState.Connected &&
                              Client.LocalPlayerId > 0;
            var canClaimToday = isConnected && !alreadyClaimedToday && !isCompleted;

            snapshot.ClaimedDayCount = claimedDayCount;
            snapshot.AlreadyClaimedToday = alreadyClaimedToday;
            snapshot.IsCompleted = isCompleted;
            snapshot.CanClaimToday = canClaimToday;
            snapshot.CurrentDayNumber = nextDayNumber;
            snapshot.ClaimButtonText = canClaimToday ? $"Claim Day {nextDayNumber}" : "Claim Complete";

            if (!isConnected)
            {
                snapshot.StatusText = "Connect to the server to claim attendance rewards.";
                snapshot.ClaimButtonText = "Connect Required";
            }
            else if (isCompleted)
            {
                snapshot.StatusText = "All rewards claimed.";
            }
            else if (alreadyClaimedToday)
            {
                snapshot.StatusText = $"Today's reward already claimed. Next day unlocks tomorrow ({todayKey}).";
            }
            else
            {
                snapshot.StatusText = $"Day {nextDayNumber} reward is ready.";
            }

            for (var i = 0; i < definition.Days.Count; i++)
            {
                var day = definition.Days[i];
                if (day == null)
                {
                    continue;
                }

                var dayState = AttendanceDayVisualState.Locked;
                if (day.DayNumber <= claimedDayCount)
                {
                    dayState = AttendanceDayVisualState.Claimed;
                }
                else if (day.DayNumber == nextDayNumber && canClaimToday)
                {
                    dayState = AttendanceDayVisualState.Claimable;
                }

                snapshot.DaySnapshots.Add(new AttendanceDaySnapshot
                {
                    DayNumber = day.DayNumber,
                    Title = string.IsNullOrWhiteSpace(day.Title) ? $"Day {day.DayNumber}" : day.Title,
                    RewardText = !string.IsNullOrWhiteSpace(day.RewardLabel)
                        ? day.RewardLabel
                        : _rewardApplier.FormatRewardSummary(day.Rewards),
                    VisualState = dayState
                });
            }

            return snapshot;
        }

        public void OpenAttendanceModal(string preferredEventId = null, bool autoOpen = false)
        {
            if (_uiContext == null)
            {
                _uiContext = GetComponent<UiContext>();
            }

            if (_uiContext == null || _openModalTask != null)
            {
                return;
            }

            var selectedEventId = !string.IsNullOrWhiteSpace(preferredEventId)
                ? preferredEventId
                : GetFirstEventOrFallback();

            if (autoOpen && string.IsNullOrWhiteSpace(selectedEventId))
            {
                return;
            }

            if (autoOpen)
            {
                _autoOpenedThisSession = true;
            }

            var payload = new AttendanceModalPayload
            {
                PreferredEventId = selectedEventId,
                AutoOpened = autoOpen
            };

            _openModalTask = _uiContext.ModalService.ShowAsync(
                new UiPopupRequest(
                    ModalPopupId,
                    "Attendance Event",
                    string.Empty,
                    "Close",
                    "Close",
                    dismissOnBackdrop: false,
                    payload: payload));

            AwaitPopupClose();
            StateChanged?.Invoke();
        }

        public bool TryClaimReward(string eventId, out string resultMessage)
        {
            resultMessage = "Claim failed.";
            if (EventCatalog == null || !EventCatalog.TryGetEvent(eventId, out var definition) || definition == null)
            {
                resultMessage = "Attendance event is missing.";
                return false;
            }

            var snapshot = BuildTrackSnapshot(definition);
            if (!snapshot.CanClaimToday)
            {
                resultMessage = snapshot.StatusText;
                return false;
            }

            var nextDayIndex = snapshot.CurrentDayNumber - 1;
            if (nextDayIndex < 0 || nextDayIndex >= definition.Days.Count)
            {
                resultMessage = "Claim day is invalid.";
                return false;
            }

            var day = definition.Days[nextDayIndex];
            var progress = GetOrCreateProgress(eventId);
            progress.ClaimedDayCount = Mathf.Max(progress.ClaimedDayCount, snapshot.CurrentDayNumber);
            progress.LastClaimDateKey = GetTodayDateKey();

            _rewardApplier.ApplyRewards(day.Rewards, out var summary);
            _profile.BonusCoins = Client != null ? Client.LocalBonusCoins : 0;
            SaveProfile();

            resultMessage = string.IsNullOrWhiteSpace(summary)
                ? $"{definition.DisplayName} day {snapshot.CurrentDayNumber} claimed."
                : $"{definition.DisplayName} day {snapshot.CurrentDayNumber} claimed: {summary}";

            StateChanged?.Invoke();
            return true;
        }

        public void ResetDemoData()
        {
            var currentBonusCoins = Client != null ? Client.LocalBonusCoins : 0;
            if (Client != null && currentBonusCoins != 0)
            {
                Client.AddLocalBonusCoins(-currentBonusCoins);
            }

            _profile = new AttendanceProfileDocument();
            _progressByEventId.Clear();
            SaveProfile();
            StateChanged?.Invoke();
        }

        public void AdjustDebugDayOffset(int delta)
        {
            DebugDayOffsetDays = Mathf.Clamp(DebugDayOffsetDays + delta, -30, 30);
            StateChanged?.Invoke();
        }

        public string GetLauncherStatusText()
        {
            var claimableCount = 0;
            if (Client == null || Client.CurrentConnectionState != ConnectionState.Connected || Client.LocalPlayerId <= 0)
            {
                return "Connect required";
            }

            var events = GetOrderedEvents();
            for (var i = 0; i < events.Count; i++)
            {
                var definition = events[i];
                if (definition == null)
                {
                    continue;
                }

                if (BuildTrackSnapshot(definition).CanClaimToday)
                {
                    claimableCount++;
                }
            }

            return claimableCount > 0 ? $"{claimableCount} ready" : "All claimed";
        }

        private void BindReferences()
        {
            if (Client == null)
            {
                Client = FindFirstObjectByType<NetworkGameClient>();
            }

            if (InputSender == null)
            {
                InputSender = FindFirstObjectByType<LocalInputSender>();
            }

            if (GrowthProgressionManager == null)
            {
                GrowthProgressionManager = FindFirstObjectByType<GrowthProgressionManager>();
            }

            if (InventoryManager == null)
            {
                InventoryManager = FindFirstObjectByType<PlayerInventoryManager>();
            }

            if (ItemDataManager == null)
            {
                ItemDataManager = FindFirstObjectByType<ItemDataManager>();
            }

            if (UiRuntimeInstaller == null)
            {
                UiRuntimeInstaller = GetComponent<UiRuntimeInstaller>();
            }
        }

        private void HandleSessionReady()
        {
            if (_autoOpenedThisSession)
            {
                return;
            }

            if (_autoOpenCoordinator == null || !_autoOpenCoordinator.TryGetAutoOpenEventId(this, out var claimableEventId))
            {
                return;
            }

            OpenAttendanceModal(claimableEventId, autoOpen: true);
        }

        private void HandleGameplayBlockChanged(bool blocked)
        {
            if (InputSender != null)
            {
                InputSender.ExternalUiFireBlock = blocked;
            }

            StateChanged?.Invoke();
        }

        private async void AwaitPopupClose()
        {
            var task = _openModalTask;
            if (task == null)
            {
                return;
            }

            try
            {
                await task;
            }
            finally
            {
                if (ReferenceEquals(_openModalTask, task))
                {
                    _openModalTask = null;
                    StateChanged?.Invoke();
                }
            }
        }

        private string GetFirstEventOrFallback()
        {
            var claimable = GetFirstClaimableEventId();
            if (!string.IsNullOrWhiteSpace(claimable))
            {
                return claimable;
            }

            var events = GetOrderedEvents();
            for (var i = 0; i < events.Count; i++)
            {
                var definition = events[i];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.EventId))
                {
                    return definition.EventId;
                }
            }

            return string.Empty;
        }

        private AttendanceEventProgressRecord GetOrCreateProgress(string eventId)
        {
            if (_progressByEventId.TryGetValue(eventId, out var existingRecord))
            {
                return existingRecord;
            }

            var createdRecord = new AttendanceEventProgressRecord
            {
                EventId = eventId ?? string.Empty,
                ClaimedDayCount = 0,
                LastClaimDateKey = string.Empty
            };

            _profile.Events.Add(createdRecord);
            _progressByEventId.Add(createdRecord.EventId, createdRecord);
            return createdRecord;
        }

        private void SyncProgressLookup()
        {
            _progressByEventId.Clear();
            _profile ??= new AttendanceProfileDocument();
            _profile.Events ??= new List<AttendanceEventProgressRecord>();

            for (var i = 0; i < _profile.Events.Count; i++)
            {
                var record = _profile.Events[i];
                if (record == null || string.IsNullOrWhiteSpace(record.EventId))
                {
                    continue;
                }

                _progressByEventId[record.EventId] = record;
            }
        }

        private void ApplyStoredBonusCoins()
        {
            if (_profile == null || Client == null)
            {
                return;
            }

            var difference = _profile.BonusCoins - Client.LocalBonusCoins;
            if (difference != 0)
            {
                Client.AddLocalBonusCoins(difference);
            }
        }

        private void SaveProfile()
        {
            _profileStore.Save(_profile);
        }
    }

    public enum AttendanceDayVisualState
    {
        Locked = 1,
        Claimable = 2,
        Claimed = 3
    }

    public sealed class AttendanceTrackSnapshot
    {
        public string EventId = string.Empty;
        public string DisplayName = string.Empty;
        public AttendancePresentationMode PresentationMode = AttendancePresentationMode.Fixed;
        public int ClaimedDayCount;
        public int CurrentDayNumber;
        public bool AlreadyClaimedToday;
        public bool CanClaimToday;
        public bool IsCompleted;
        public string StatusText = string.Empty;
        public string ClaimButtonText = string.Empty;
        public List<AttendanceDaySnapshot> DaySnapshots = new();
    }

    public sealed class AttendanceDaySnapshot
    {
        public int DayNumber;
        public string Title = string.Empty;
        public string RewardText = string.Empty;
        public AttendanceDayVisualState VisualState = AttendanceDayVisualState.Locked;
    }

    [Serializable]
    public sealed class AttendanceModalPayload
    {
        public string PreferredEventId = string.Empty;
        public bool AutoOpened;
    }
}
