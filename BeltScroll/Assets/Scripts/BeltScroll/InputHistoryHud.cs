using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BeltScroll
{
    public sealed class InputHistoryHud : MonoBehaviour
    {
        private enum DirectionInput
        {
            Neutral,
            Up,
            Down,
            Left,
            Right,
            UpLeft,
            UpRight,
            DownLeft,
            DownRight
        }

        private sealed class InputEntry
        {
            public DirectionInput direction;
            public float createdAt;
            public GameObject root;
            public Image background;
            public Text label;
        }

        [SerializeField] private int maxEntries = 10;
        [SerializeField] private float entryLifetimeSeconds = 2.5f;
        [SerializeField] private float fadeOutSeconds = 0.35f;
        [SerializeField] private bool newestOnTop = true;
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-32f, 0f);
        [SerializeField] private Vector2 entrySize = new Vector2(46f, 46f);
        [SerializeField] private float entrySpacing = 6f;
        [SerializeField] private Color entryColor = new Color(0f, 0f, 0f, 0.58f);
        [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.95f);

        private readonly List<InputEntry> entries = new List<InputEntry>();
        private DirectionInput currentDirection = DirectionInput.Neutral;
        private GameObject canvasObject;
        private RectTransform listRoot;
        private Font builtInFont;

        private void Awake()
        {
            BuildHud();
        }

        private void OnEnable()
        {
            BuildHud();
        }

        private void OnDisable()
        {
            ClearEntries();
        }

        private void OnValidate()
        {
            maxEntries = Mathf.Max(1, maxEntries);
            entryLifetimeSeconds = Mathf.Max(0.05f, entryLifetimeSeconds);
            fadeOutSeconds = Mathf.Clamp(fadeOutSeconds, 0f, entryLifetimeSeconds);
            entrySize.x = Mathf.Max(20f, entrySize.x);
            entrySize.y = Mathf.Max(20f, entrySize.y);
            entrySpacing = Mathf.Max(0f, entrySpacing);
        }

        private void Update()
        {
            BuildHud();

            var direction = ReadDirection();
            if (direction != currentDirection)
            {
                currentDirection = direction;
                if (direction != DirectionInput.Neutral)
                {
                    Enqueue(direction);
                }
            }

            RemoveExpired();
            RefreshVisuals();
        }

        private void BuildHud()
        {
            if (listRoot != null)
            {
                return;
            }

            builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("InputHistoryHudCanvas", typeof(Canvas), typeof(CanvasScaler));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var listObject = new GameObject("InputHistoryQueue", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listObject.transform.SetParent(canvasObject.transform, false);

            listRoot = listObject.GetComponent<RectTransform>();
            listRoot.anchorMin = new Vector2(1f, 0.5f);
            listRoot.anchorMax = new Vector2(1f, 0.5f);
            listRoot.pivot = new Vector2(1f, 0.5f);
            listRoot.anchoredPosition = anchoredPosition;
            listRoot.sizeDelta = new Vector2(entrySize.x, maxEntries * entrySize.y + Mathf.Max(0, maxEntries - 1) * entrySpacing);

            var layout = listObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = newestOnTop ? TextAnchor.UpperRight : TextAnchor.LowerRight;
            layout.spacing = entrySpacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void Enqueue(DirectionInput direction)
        {
            while (entries.Count >= maxEntries)
            {
                RemoveOldest();
            }

            var entry = CreateEntry(direction);
            entries.Add(entry);

            if (newestOnTop)
            {
                entry.root.transform.SetSiblingIndex(0);
            }
            else
            {
                entry.root.transform.SetAsLastSibling();
            }
        }

        private InputEntry CreateEntry(DirectionInput direction)
        {
            var root = new GameObject($"Input_{direction}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(listRoot, false);

            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = entrySize;

            var background = root.GetComponent<Image>();
            background.color = entryColor;
            background.raycastTarget = false;

            var labelObject = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.raycastTarget = false;
            label.font = builtInFont;
            label.text = ToSymbol(direction);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = Mathf.RoundToInt(entrySize.y * 0.66f);
            label.color = textColor;

            return new InputEntry
            {
                direction = direction,
                createdAt = Time.unscaledTime,
                root = root,
                background = background,
                label = label
            };
        }

        private void RemoveExpired()
        {
            var now = Time.unscaledTime;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (now - entries[i].createdAt >= entryLifetimeSeconds)
                {
                    RemoveAt(i);
                }
            }
        }

        private void RefreshVisuals()
        {
            var now = Time.unscaledTime;
            foreach (var entry in entries)
            {
                var age = now - entry.createdAt;
                var remaining = entryLifetimeSeconds - age;
                var alphaMultiplier = fadeOutSeconds <= 0f ? 1f : Mathf.Clamp01(remaining / fadeOutSeconds);

                var bg = entryColor;
                bg.a *= alphaMultiplier;
                entry.background.color = bg;

                var text = textColor;
                text.a *= alphaMultiplier;
                entry.label.color = text;
            }
        }

        private void RemoveOldest()
        {
            if (entries.Count > 0)
            {
                RemoveAt(0);
            }
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= entries.Count)
            {
                return;
            }

            var entry = entries[index];
            entries.RemoveAt(index);

            if (entry.root != null)
            {
                DestroyUnityObject(entry.root);
            }
        }

        private void ClearEntries()
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].root != null)
                {
                    DestroyUnityObject(entries[i].root);
                }
            }

            entries.Clear();
            currentDirection = DirectionInput.Neutral;
        }

        private void OnDestroy()
        {
            ClearEntries();

            if (canvasObject != null)
            {
                DestroyUnityObject(canvasObject);
            }
        }

        private static void DestroyUnityObject(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static string ToSymbol(DirectionInput direction)
        {
            return direction switch
            {
                DirectionInput.Up => "↑",
                DirectionInput.Down => "↓",
                DirectionInput.Left => "←",
                DirectionInput.Right => "→",
                DirectionInput.UpLeft => "↖",
                DirectionInput.UpRight => "↗",
                DirectionInput.DownLeft => "↙",
                DirectionInput.DownRight => "↘",
                _ => string.Empty
            };
        }

        private static DirectionInput ReadDirection()
        {
            var up = false;
            var down = false;
            var left = false;
            var right = false;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                up |= keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                down |= keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                left |= keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                right |= keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            up |= Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            down |= Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            left |= Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            right |= Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
#endif

            var x = (right ? 1 : 0) - (left ? 1 : 0);
            var y = (up ? 1 : 0) - (down ? 1 : 0);

            return (x, y) switch
            {
                (0, 1) => DirectionInput.Up,
                (0, -1) => DirectionInput.Down,
                (-1, 0) => DirectionInput.Left,
                (1, 0) => DirectionInput.Right,
                (-1, 1) => DirectionInput.UpLeft,
                (1, 1) => DirectionInput.UpRight,
                (-1, -1) => DirectionInput.DownLeft,
                (1, -1) => DirectionInput.DownRight,
                _ => DirectionInput.Neutral
            };
        }
    }
}
