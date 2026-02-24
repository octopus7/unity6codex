using UnityEngine;
using UnityEngine.UIElements;

namespace CodexSix.UiKit.Runtime
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class UiRuntimeInstaller : MonoBehaviour
    {
        [SerializeField] private UiRootLifetime _rootLifetime = UiRootLifetime.SceneScoped;
        [SerializeField] private bool _enablePersistentToastChannel;
        [SerializeField] private bool _includeDefaultActionKeys = true;

        [Header("UI Toolkit Assets")]
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private VisualTreeAsset _rootVisualTree;
        [SerializeField] private StyleSheet _rootStyleSheet;
        [SerializeField] private VisualTreeAsset _popupVisualTree;
        [SerializeField] private VisualTreeAsset _toastVisualTree;

        public static UiRuntimeInstaller Install(GameObject host, UiRootLifetime lifetime = UiRootLifetime.SceneScoped)
        {
            var installer = host.GetComponent<UiRuntimeInstaller>();
            if (installer == null)
            {
                installer = host.AddComponent<UiRuntimeInstaller>();
            }

            installer._rootLifetime = lifetime;
            return installer;
        }

        private void Awake()
        {
            var context = GetComponent<UiContext>();
            if (context == null)
            {
                context = gameObject.AddComponent<UiContext>();
            }

            context.Configure(_rootLifetime, _enablePersistentToastChannel, _includeDefaultActionKeys);

            var uiRoot = GetComponent<UiRoot>();
            if (uiRoot == null)
            {
                uiRoot = gameObject.AddComponent<UiRoot>();
            }

            uiRoot.Configure(_panelSettings, _rootVisualTree, _rootStyleSheet);

            var binder = GetComponent<UiDocumentBinder>();
            if (binder == null)
            {
                binder = gameObject.AddComponent<UiDocumentBinder>();
            }

            var presenter = GetComponent<UiToolkitPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<UiToolkitPresenter>();
            }

            presenter.Configure(_popupVisualTree, _toastVisualTree);
        }
    }
}
