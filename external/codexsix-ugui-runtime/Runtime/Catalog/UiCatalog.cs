#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.UguiRuntime
{
    [CreateAssetMenu(fileName = "UiCatalog", menuName = "CodexSix/uGUI Runtime/UI Catalog")]
    public sealed class UiCatalog : ScriptableObject
    {
        [SerializeField] private List<UiScreenDefinition> _screens = new();
        [SerializeField] private List<UiPopupDefinition> _popups = new();

        public List<UiScreenDefinition> Screens => _screens;
        public List<UiPopupDefinition> Popups => _popups;

        public bool TryGetScreenDefinition(string screenId, out UiScreenDefinition? definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(screenId))
            {
                return false;
            }

            for (var i = 0; i < _screens.Count; i++)
            {
                var candidate = _screens[i];
                if (candidate == null || !string.Equals(candidate.Id, screenId, StringComparison.Ordinal))
                {
                    continue;
                }

                definition = candidate;
                return true;
            }

            return false;
        }

        public bool TryGetPopupDefinition(string popupId, out UiPopupDefinition? definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(popupId))
            {
                return false;
            }

            for (var i = 0; i < _popups.Count; i++)
            {
                var candidate = _popups[i];
                if (candidate == null || !string.Equals(candidate.Id, popupId, StringComparison.Ordinal))
                {
                    continue;
                }

                definition = candidate;
                return true;
            }

            return false;
        }
    }
}
