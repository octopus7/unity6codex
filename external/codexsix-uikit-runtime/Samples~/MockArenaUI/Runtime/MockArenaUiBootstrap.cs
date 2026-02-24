using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodexSix.UiKit.Runtime.Samples.MockArena
{
    public sealed class MockArenaUiBootstrap : MonoBehaviour
    {
        [SerializeField] private UiContext _context;
        [SerializeField] private MockArenaAdapter _adapter;

        private IDisposable _stateSubscription;
        private IDisposable _achievementSubscription;
        private bool _inventoryOpen;

        private const string InventoryShortcutId = "sample.inventory.toggle";

        private void Awake()
        {
            if (_context == null)
            {
                _context = FindFirstObjectByType<UiContext>();
            }

            if (_adapter == null)
            {
                _adapter = FindFirstObjectByType<MockArenaAdapter>();
            }
        }

        private void OnEnable()
        {
            if (_context == null || _adapter == null)
            {
                return;
            }

            _context.ScreenService.ShowScreen("hud");
            _stateSubscription = _adapter.HudState.Subscribe(HandleHudStateChanged, replayLatest: true);
            _achievementSubscription = _context.SignalBus.Subscribe<MockAchievementUnlocked>(HandleAchievement);

            _context.ShortcutService.Register(
                new ShortcutBinding(
                    BindingId: InventoryShortcutId,
                    ActionId: "sample.inventory.toggle",
                    Key: Key.I,
                    Scope: ShortcutScope.Ui,
                    Trigger: ShortcutTrigger.PressedThisFrame),
                ToggleInventoryScreen);

            _context.ToastService.Enqueue(new ToastRequest("sample.start", "Mock Arena sample started.", 2.4f, ToastPriority.Normal));
        }

        private void OnDisable()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;

            _achievementSubscription?.Dispose();
            _achievementSubscription = null;

            _context?.ShortcutService.Unregister(InventoryShortcutId);
        }

        private void HandleHudStateChanged(MockArenaHudState state)
        {
            if (_context == null)
            {
                return;
            }

            _context.ToastService.Enqueue(
                new ToastRequest("hud.coins", $"Coins: {state.Coins} | Combo: {state.Combo}", 1.6f, ToastPriority.Low));

            if (state.InShop)
            {
                _context.ScreenService.ShowScreen("shop");
            }
            else if (!_inventoryOpen)
            {
                _context.ScreenService.ShowScreen("hud");
            }

            if (state.Hp <= 20 && _context.ModalService.ModalDepth == 0)
            {
                _ = _context.PopupService.ShowWithTimeoutAsync(
                    new PopupRequest(
                        Id: "low_hp_warning",
                        Title: "Low HP",
                        Body: "HP is critically low. Step back and recover.",
                        ConfirmText: "Acknowledge",
                        CancelText: "Ignore"),
                    timeoutSeconds: 3f);
            }
        }

        private void HandleAchievement(MockAchievementUnlocked achievement)
        {
            if (_context == null)
            {
                return;
            }

            var persistentChannel = _context.GetToastService(UiToastChannels.Persistent);
            persistentChannel.Enqueue(
                new ToastRequest(
                    Key: $"achievement.{achievement.AchievementId}",
                    Message: $"{achievement.Title}: {achievement.Description}",
                    DurationSeconds: 4.5f,
                    Priority: ToastPriority.High));
        }

        private void ToggleInventoryScreen()
        {
            if (_context == null)
            {
                return;
            }

            _inventoryOpen = !_inventoryOpen;
            _context.ScreenService.ShowScreen(_inventoryOpen ? "inventory" : "hud");
            _context.ToastService.Enqueue(
                new ToastRequest("sample.inventory", _inventoryOpen ? "Inventory opened" : "Inventory closed", 1.2f, ToastPriority.Normal));
        }
    }
}