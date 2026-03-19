#nullable enable
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    public sealed class StackedUguiDemoController : MonoBehaviour
    {
        private UiContext? _context;

        public event Action? StateChanged;

        public UiContext? Context => _context;
        public int GameplayActionCount { get; private set; }
        public int ConfirmedPopupCount { get; private set; }
        public int CancelledPopupCount { get; private set; }
        public int DismissedPopupCount { get; private set; }
        public bool IsGameplayBlocked => _context != null && _context.InputBlockService.IsGameplayInputBlocked;

        private void Start()
        {
            _context = FindFirstObjectByType<UiContext>();
            if (_context == null)
            {
                Debug.LogWarning("StackedUguiDemoController could not find UiContext.");
                return;
            }

            _context.InputBlockService.GameplayBlockChanged += HandleGameplayBlockChanged;
            _context.ScreenService.Show("hud");
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryGameplayAction();
            }
        }

        private void OnDestroy()
        {
            if (_context != null)
            {
                _context.InputBlockService.GameplayBlockChanged -= HandleGameplayBlockChanged;
            }
        }

        public void ShowHud()
        {
            _context?.ScreenService.Show("hud");
        }

        public void ShowInventory()
        {
            _context?.ScreenService.Show("inventory");
        }

        public void ShowSettings()
        {
            _context?.ScreenService.Show("settings");
        }

        public void GoBack()
        {
            _context?.ScreenService.TryGoBack();
        }

        public void TryGameplayAction()
        {
            if (IsGameplayBlocked)
            {
                StateChanged?.Invoke();
                return;
            }

            GameplayActionCount++;
            StateChanged?.Invoke();
        }

        public async void ShowConfirmPopup()
        {
            if (_context == null)
            {
                return;
            }

            var result = await _context.ModalService.ShowAsync(
                new UiPopupRequest(
                    "confirm",
                    "Confirm Action",
                    "This popup blocks gameplay input and can open a nested notice.",
                    "Confirm",
                    "Cancel",
                    dismissOnBackdrop: false));

            ApplyPopupResult(result);
        }

        public async void ShowDismissibleNotice()
        {
            if (_context == null)
            {
                return;
            }

            var result = await _context.ModalService.ShowAsync(
                new UiPopupRequest(
                    "nested-notice",
                    "Dismissible Notice",
                    "This popup can be cancelled by clicking the backdrop or pressing Escape.",
                    "OK",
                    string.Empty,
                    dismissOnBackdrop: true));

            ApplyPopupResult(result);
        }

        public void ApplyPopupResult(UiPopupResult result)
        {
            switch (result.Kind)
            {
                case UiPopupResultKind.Confirmed:
                    ConfirmedPopupCount++;
                    break;
                case UiPopupResultKind.Cancelled:
                    CancelledPopupCount++;
                    break;
                case UiPopupResultKind.Dismissed:
                    DismissedPopupCount++;
                    break;
            }

            StateChanged?.Invoke();
        }

        private void HandleGameplayBlockChanged(bool _)
        {
            StateChanged?.Invoke();
        }
    }
}
