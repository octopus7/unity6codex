using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo
{
    internal static class StackedUguiDemoUiFactory
    {
        private static Font _font;

        public static Font DefaultFont => _font != null ? _font : _font = LoadDefaultFont();

        private static Font LoadDefaultFont()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return _font;
        }

        public static RectTransform EnsureStretchRoot(GameObject gameObject)
        {
            var rect = gameObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static RectTransform CreateSidebarPanel(Transform parent, string name)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(24f, 24f);
            rect.offsetMax = new Vector2(380f, -24f);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.12f, 0.15f, 0.92f);

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return rect;
        }

        public static Text CreateLabel(Transform parent, string name, string text, int fontSize, FontStyle fontStyle = FontStyle.Normal)
        {
            var rect = CreateRect(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = fontSize + 14f;
            layout.preferredHeight = fontSize + 18f;

            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text;
            return label;
        }

        public static Button CreateButton(Transform parent, string name, string labelText, UnityAction onClick)
        {
            var rect = CreateRect(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 42f;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.78f, 1f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var textRect = CreateRect("Label", rect);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            var text = textRect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = labelText;
            return button;
        }

        public static RectTransform CreateCenteredPanel(Transform parent, string name, Vector2 size, Color color)
        {
            var rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return rect;
        }

        public static Canvas ConfigureOverlayCanvas(GameObject gameObject, int sortingOrder)
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            EnsureStretchRoot(gameObject);
            return canvas;
        }
    }
}
