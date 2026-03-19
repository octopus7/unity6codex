#nullable enable
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CodexSix.UguiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiRoot))]
    [RequireComponent(typeof(UiContext))]
    public sealed class UiRuntimeInstaller : MonoBehaviour
    {
        [SerializeField] private UiCatalog? _catalog;
        [SerializeField] private UiRoot? _uiRoot;
        [SerializeField] private UiContext? _uiContext;

        public UiCatalog? Catalog
        {
            get => _catalog;
            set => _catalog = value;
        }

        public UiRoot UiRoot => _uiRoot!;
        public UiContext UiContext => _uiContext!;

        private void Awake()
        {
            EnsureInfrastructure();
            _uiContext.Initialize(_uiRoot, _catalog);
        }

        private void Reset()
        {
            EnsureInfrastructure();
        }

        private void EnsureInfrastructure()
        {
            _uiRoot = GetComponent<UiRoot>();
            _uiContext = GetComponent<UiContext>();
            _uiRoot.EnsureHierarchy();
            EnsureEventSystem();
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
    }
}
