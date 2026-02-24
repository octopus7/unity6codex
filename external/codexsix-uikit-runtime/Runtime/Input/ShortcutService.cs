using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace CodexSix.UiKit.Runtime
{
    public sealed class ShortcutService : IShortcutService
    {
        private readonly List<ShortcutRegistration> _registrations = new();
        private readonly Dictionary<string, int> _indexById = new(StringComparer.Ordinal);

        public event Action CloseRequested;
        public event Action ConfirmRequested;

        public ShortcutService(bool includeDefaultActionKeys = true)
        {
            if (!includeDefaultActionKeys)
            {
                return;
            }

            Register(
                new ShortcutBinding("__builtin.close", "ui.close", Key.Escape, ShortcutScope.Global, ShortcutTrigger.PressedThisFrame),
                () => CloseRequested?.Invoke());

            Register(
                new ShortcutBinding("__builtin.confirm", "ui.confirm", Key.Enter, ShortcutScope.Global, ShortcutTrigger.PressedThisFrame),
                () => ConfirmRequested?.Invoke());
        }

        public void Register(ShortcutBinding binding, Action handler)
        {
            if (string.IsNullOrWhiteSpace(binding.BindingId))
            {
                throw new ArgumentException("Binding id must be provided.", nameof(binding));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_indexById.TryGetValue(binding.BindingId, out var existingIndex))
            {
                _registrations[existingIndex] = new ShortcutRegistration(binding, handler);
                return;
            }

            _indexById.Add(binding.BindingId, _registrations.Count);
            _registrations.Add(new ShortcutRegistration(binding, handler));
        }

        public void Unregister(string bindingId)
        {
            if (string.IsNullOrWhiteSpace(bindingId))
            {
                return;
            }

            if (!_indexById.TryGetValue(bindingId, out var index))
            {
                return;
            }

            _registrations.RemoveAt(index);
            _indexById.Remove(bindingId);

            for (var i = index; i < _registrations.Count; i++)
            {
                _indexById[_registrations[i].Binding.BindingId] = i;
            }
        }

        public bool Process(InputContext context)
        {
            var handled = false;
            for (var i = 0; i < _registrations.Count; i++)
            {
                var registration = _registrations[i];
                if (!ShouldEvaluateBinding(registration.Binding, context))
                {
                    continue;
                }

                var triggered = registration.Binding.Trigger switch
                {
                    ShortcutTrigger.PressedThisFrame => context.WasPressedThisFrame(registration.Binding.Key),
                    ShortcutTrigger.Held => context.IsHeld(registration.Binding.Key),
                    _ => false
                };

                if (!triggered)
                {
                    continue;
                }

                registration.Handler();
                handled = true;
            }

            return handled;
        }

        private static bool ShouldEvaluateBinding(ShortcutBinding binding, InputContext context)
        {
            if (context.FocusState == UiFocusState.TextInput && binding.Scope == ShortcutScope.Global)
            {
                return false;
            }

            if (context.IsModalOpen && binding.Scope == ShortcutScope.Gameplay)
            {
                return false;
            }

            return true;
        }

        private readonly struct ShortcutRegistration
        {
            public ShortcutBinding Binding { get; }
            public Action Handler { get; }

            public ShortcutRegistration(ShortcutBinding binding, Action handler)
            {
                Binding = binding;
                Handler = handler;
            }
        }
    }
}