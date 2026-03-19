#nullable enable
using System;
using UnityEngine;

namespace CodexSix.UguiRuntime
{
    [Serializable]
    public sealed class UiScreenDefinition
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private UiScreenView? _prefab;
        [SerializeField] private bool _cacheInstance = true;

        public string Id
        {
            get => _id;
            set => _id = value ?? string.Empty;
        }

        public UiScreenView? Prefab
        {
            get => _prefab;
            set => _prefab = value;
        }

        public bool CacheInstance
        {
            get => _cacheInstance;
            set => _cacheInstance = value;
        }
    }

    [Serializable]
    public sealed class UiPopupDefinition
    {
        [SerializeField] private string _id = string.Empty;
        [SerializeField] private UiPopupView? _prefab;

        public string Id
        {
            get => _id;
            set => _id = value ?? string.Empty;
        }

        public UiPopupView? Prefab
        {
            get => _prefab;
            set => _prefab = value;
        }
    }
}
