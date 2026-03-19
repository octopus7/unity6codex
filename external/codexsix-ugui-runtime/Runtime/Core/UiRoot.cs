#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime
{
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class UiRoot : MonoBehaviour
    {
        [SerializeField] private Canvas? _canvas;
        [SerializeField] private CanvasScaler? _canvasScaler;
        [SerializeField] private GraphicRaycaster? _graphicRaycaster;
        [SerializeField] private RectTransform? _screenLayer;
        [SerializeField] private RectTransform? _blockerLayer;
        [SerializeField] private RectTransform? _modalLayer;
        [SerializeField] private RectTransform? _overlayLayer;
        [SerializeField] private Image? _blockerImage;
        [SerializeField] private Button? _blockerButton;

        public Canvas Canvas => _canvas!;
        public CanvasScaler CanvasScaler => _canvasScaler!;
        public GraphicRaycaster GraphicRaycaster => _graphicRaycaster!;
        public RectTransform ScreenLayer => _screenLayer!;
        public RectTransform BlockerLayer => _blockerLayer!;
        public RectTransform ModalLayer => _modalLayer!;
        public RectTransform OverlayLayer => _overlayLayer!;
        public Image BlockerImage => _blockerImage!;
        public Button BlockerButton => _blockerButton!;

        public void EnsureHierarchy()
        {
            _canvas = GetComponent<Canvas>();
            _canvasScaler = GetComponent<CanvasScaler>();
            _graphicRaycaster = GetComponent<GraphicRaycaster>();

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;
            _canvas.overrideSorting = false;

            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            _canvasScaler.matchWidthOrHeight = 0.5f;

            _screenLayer = EnsureLayer(UiLayerNames.ScreenLayer, 0);
            _blockerLayer = EnsureLayer(UiLayerNames.BlockerLayer, 1);
            _modalLayer = EnsureLayer(UiLayerNames.ModalLayer, 2);
            _overlayLayer = EnsureLayer(UiLayerNames.OverlayLayer, 3);

            _blockerImage = EnsureBlockerImage(_blockerLayer);
            _blockerButton = EnsureBlockerButton(_blockerLayer, _blockerImage);
            SetBlockerVisible(false);
        }

        public void SetBlockerVisible(bool visible)
        {
            if (_blockerLayer == null)
            {
                return;
            }

            _blockerLayer.gameObject.SetActive(visible);
        }

        private RectTransform EnsureLayer(string layerName, int siblingIndex)
        {
            var child = transform.Find(layerName) as RectTransform;
            if (child == null)
            {
                var layerObject = new GameObject(layerName, typeof(RectTransform));
                child = layerObject.GetComponent<RectTransform>();
                child.SetParent(transform, false);
            }

            child.SetSiblingIndex(siblingIndex);
            StretchToParent(child);
            return child;
        }

        private static Image EnsureBlockerImage(RectTransform blockerLayer)
        {
            var image = blockerLayer.GetComponent<Image>();
            if (image == null)
            {
                image = blockerLayer.gameObject.AddComponent<Image>();
            }

            image.color = new Color(0f, 0f, 0f, 0.38f);
            image.raycastTarget = true;
            return image;
        }

        private static Button EnsureBlockerButton(RectTransform blockerLayer, Image blockerImage)
        {
            var button = blockerLayer.GetComponent<Button>();
            if (button == null)
            {
                button = blockerLayer.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = blockerImage;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localPosition = Vector3.zero;
        }
    }
}
