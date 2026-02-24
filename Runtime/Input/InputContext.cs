using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace CodexSix.UiKit.Runtime
{
    public readonly struct InputContext
    {
        private readonly Keyboard _keyboard;
        private readonly HashSet<Key> _pressedThisFrame;
        private readonly HashSet<Key> _heldKeys;

        public UiFocusState FocusState { get; }
        public bool IsModalOpen { get; }

        public InputContext(
            UiFocusState focusState,
            bool isModalOpen,
            IEnumerable<Key> pressedThisFrame = null,
            IEnumerable<Key> heldKeys = null,
            Keyboard keyboard = null)
        {
            FocusState = focusState;
            IsModalOpen = isModalOpen;
            _keyboard = keyboard;
            _pressedThisFrame = pressedThisFrame != null ? new HashSet<Key>(pressedThisFrame) : null;
            _heldKeys = heldKeys != null ? new HashSet<Key>(heldKeys) : null;
        }

        public static InputContext FromKeyboard(UiFocusState focusState, bool isModalOpen, Keyboard keyboard)
        {
            return new InputContext(focusState, isModalOpen, keyboard: keyboard);
        }

        public bool WasPressedThisFrame(Key key)
        {
            if (_pressedThisFrame != null)
            {
                return _pressedThisFrame.Contains(key);
            }

            if (_keyboard == null)
            {
                return false;
            }

            var keyControl = _keyboard[key];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        public bool IsHeld(Key key)
        {
            if (_heldKeys != null)
            {
                return _heldKeys.Contains(key);
            }

            if (_keyboard == null)
            {
                return false;
            }

            var keyControl = _keyboard[key];
            return keyControl != null && keyControl.isPressed;
        }
    }
}