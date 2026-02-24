using UnityEngine;
using UnityEngine.UIElements;

namespace CodexSix.UiKit.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class UiRoot : MonoBehaviour
    {
        public const string ScreenLayerName = "screen-layer";
        public const string ModalLayerName = "modal-layer";
        public const string OverlayLayerName = "overlay-layer";

        [SerializeField] private UIDocument _document;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private VisualTreeAsset _rootVisualTree;
        [SerializeField] private StyleSheet _rootStyleSheet;
        private PanelSettings _runtimePanelSettings;

        public UIDocument Document => _document;
        public VisualElement RootElement { get; private set; }
        public VisualElement ScreenLayer { get; private set; }
        public VisualElement ModalLayer { get; private set; }
        public VisualElement OverlayLayer { get; private set; }

        public void Configure(PanelSettings panelSettings, VisualTreeAsset rootVisualTree, StyleSheet rootStyleSheet)
        {
            _panelSettings = panelSettings;
            _rootVisualTree = rootVisualTree;
            _rootStyleSheet = rootStyleSheet;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (_document == null)
            {
                _document = GetComponent<UIDocument>();
            }

            if (_panelSettings != null)
            {
                _document.panelSettings = _panelSettings;
            }
            else if (_document.panelSettings == null)
            {
                _runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _runtimePanelSettings.clearColor = true;
                _document.panelSettings = _runtimePanelSettings;
            }

            RootElement = _document.rootVisualElement;
            if (RootElement == null)
            {
                return;
            }

            if (RootElement.Q<VisualElement>(ScreenLayerName) != null)
            {
                BindLayers();
                return;
            }

            RootElement.Clear();

            var visualTree = _rootVisualTree != null ? _rootVisualTree : Resources.Load<VisualTreeAsset>("CodexSixUiKit/UiRoot");
            if (visualTree != null)
            {
                visualTree.CloneTree(RootElement);
            }
            else
            {
                BuildFallbackRoot(RootElement);
            }

            var style = _rootStyleSheet != null ? _rootStyleSheet : Resources.Load<StyleSheet>("CodexSixUiKit/UiRoot");
            if (style != null)
            {
                RootElement.styleSheets.Add(style);
            }

            BindLayers();
        }

        private void BindLayers()
        {
            ScreenLayer = RootElement.Q<VisualElement>(ScreenLayerName);
            ModalLayer = RootElement.Q<VisualElement>(ModalLayerName);
            OverlayLayer = RootElement.Q<VisualElement>(OverlayLayerName);

            if (ScreenLayer == null || ModalLayer == null || OverlayLayer == null)
            {
                RootElement.Clear();
                BuildFallbackRoot(RootElement);
                ScreenLayer = RootElement.Q<VisualElement>(ScreenLayerName);
                ModalLayer = RootElement.Q<VisualElement>(ModalLayerName);
                OverlayLayer = RootElement.Q<VisualElement>(OverlayLayerName);
            }
        }

        private void OnDestroy()
        {
            if (_runtimePanelSettings == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimePanelSettings);
            }
            else
            {
                DestroyImmediate(_runtimePanelSettings);
            }
        }

        private static void BuildFallbackRoot(VisualElement root)
        {
            root.AddToClassList("cs6-root");

            var screen = new VisualElement { name = ScreenLayerName };
            screen.AddToClassList("cs6-screen-layer");

            var modal = new VisualElement { name = ModalLayerName };
            modal.AddToClassList("cs6-modal-layer");

            var overlay = new VisualElement { name = OverlayLayerName };
            overlay.AddToClassList("cs6-overlay-layer");

            root.Add(screen);
            root.Add(modal);
            root.Add(overlay);
        }
    }
}
