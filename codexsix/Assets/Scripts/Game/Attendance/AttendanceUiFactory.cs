using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CodexSix.TopdownShooter.Game
{
    internal static class AttendanceUiFactory
    {
        internal readonly struct ButtonParts
        {
            public ButtonParts(Button button, Image background, Text label)
            {
                Button = button;
                Background = background;
                Label = label;
            }

            public Button Button { get; }
            public Image Background { get; }
            public Text Label { get; }
        }

        internal readonly struct ScrollViewParts
        {
            public ScrollViewParts(ScrollRect scrollRect, RectTransform viewport, RectTransform content)
            {
                ScrollRect = scrollRect;
                Viewport = viewport;
                Content = content;
            }

            public ScrollRect ScrollRect { get; }
            public RectTransform Viewport { get; }
            public RectTransform Content { get; }
        }

        private static Font _font;

        public static Font DefaultFont => _font != null ? _font : _font = LoadDefaultFont();

        public static RectTransform EnsureStretchRoot(GameObject gameObject)
        {
            var rect = gameObject.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = gameObject.AddComponent<RectTransform>();
            }

            StretchToParent(rect);
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

        public static Image AddImage(GameObject gameObject, Color color)
        {
            var image = gameObject.GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            image.color = color;
            return image;
        }

        public static Text CreateLabel(
            Transform parent,
            string name,
            string text,
            int fontSize,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle fontStyle = FontStyle.Normal,
            Color? color = null)
        {
            var rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text;
            return label;
        }

        public static ButtonParts CreateButton(
            Transform parent,
            string name,
            string labelText,
            UnityAction onClick,
            Color backgroundColor,
            int fontSize = 18,
            float minHeight = 44f)
        {
            var rect = CreateRect(name, parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = minHeight;
            layout.preferredHeight = minHeight;

            var background = AddImage(rect.gameObject, backgroundColor);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var textRect = CreateRect("Label", rect);
            StretchToParent(textRect);
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            var label = textRect.gameObject.AddComponent<Text>();
            label.font = DefaultFont;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = labelText;
            return new ButtonParts(button, background, label);
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject gameObject, int left, int right, int top, int bottom, float spacing)
        {
            var layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject gameObject, int left, int right, int top, int bottom, float spacing)
        {
            var layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.padding = new RectOffset(left, right, top, bottom);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        public static ScrollViewParts CreateHorizontalScrollView(Transform parent, string name)
        {
            var root = CreateRect(name, parent);
            AddImage(root.gameObject, new Color(0.1f, 0.1f, 0.12f, 0.6f));

            var viewport = CreateRect("Viewport", root);
            StretchToParent(viewport);
            var viewportImage = AddImage(viewport.gameObject, new Color(0f, 0f, 0f, 0.02f));
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = new Vector2(20f, 0f);
            content.offsetMax = new Vector2(20f, 0f);
            var contentLayout = AddHorizontalLayout(content.gameObject, 0, 0, 20, 20, 16f);
            contentLayout.childForceExpandWidth = false;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 36f;
            return new ScrollViewParts(scrollRect, viewport, content);
        }

        public static void StretchToParent(RectTransform rectTransform)
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

        private static Font LoadDefaultFont()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return _font;
        }
    }
}
