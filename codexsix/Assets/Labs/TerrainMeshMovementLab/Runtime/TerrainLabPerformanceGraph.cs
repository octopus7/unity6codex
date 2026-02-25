using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    public sealed class TerrainLabPerformanceGraph : MonoBehaviour
    {
        [Header("Display")]
        public bool ShowOverlay = true;
        public Rect ScreenRect = new(16f, 270f, 320f, 130f);
        [Min(20f)] public float GraphMaxFrameMs = 50f;
        [Range(30, 600)] public int MaxSamples = 240;
        public KeyCode ToggleKey = KeyCode.F3;

        [Header("Stats")]
        [Range(10, 240)] public int StatsWindowSamples = 120;

        private float[] _frameMsSamples;
        private int _sampleWriteIndex;
        private int _sampleCount;
        private Texture2D _whitePixel;

        private void Awake()
        {
            EnsureSampleCapacity();
            EnsureWhitePixel();
        }

        private void OnValidate()
        {
            if (GraphMaxFrameMs < 20f)
            {
                GraphMaxFrameMs = 20f;
            }

            if (MaxSamples < 30)
            {
                MaxSamples = 30;
            }

            if (StatsWindowSamples < 10)
            {
                StatsWindowSamples = 10;
            }

            EnsureSampleCapacity();
        }

        private void Update()
        {
            if (IsTogglePressedThisFrame())
            {
                ShowOverlay = !ShowOverlay;
            }

            var frameMs = Mathf.Max(0.0001f, Time.unscaledDeltaTime) * 1000f;
            RecordSample(frameMs);
        }

        private bool IsTogglePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (TryReadInputSystemToggle())
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(ToggleKey);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryReadInputSystemToggle()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            if (!TryMapToInputSystemKey(ToggleKey, out var key))
            {
                return false;
            }

            return keyboard[key].wasPressedThisFrame;
        }

        private static bool TryMapToInputSystemKey(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.F1: key = Key.F1; return true;
                case KeyCode.F2: key = Key.F2; return true;
                case KeyCode.F3: key = Key.F3; return true;
                case KeyCode.F4: key = Key.F4; return true;
                case KeyCode.F5: key = Key.F5; return true;
                case KeyCode.F6: key = Key.F6; return true;
                case KeyCode.F7: key = Key.F7; return true;
                case KeyCode.F8: key = Key.F8; return true;
                case KeyCode.F9: key = Key.F9; return true;
                case KeyCode.F10: key = Key.F10; return true;
                case KeyCode.F11: key = Key.F11; return true;
                case KeyCode.F12: key = Key.F12; return true;
                case KeyCode.A: key = Key.A; return true;
                case KeyCode.B: key = Key.B; return true;
                case KeyCode.C: key = Key.C; return true;
                case KeyCode.D: key = Key.D; return true;
                case KeyCode.E: key = Key.E; return true;
                case KeyCode.F: key = Key.F; return true;
                case KeyCode.G: key = Key.G; return true;
                case KeyCode.H: key = Key.H; return true;
                case KeyCode.I: key = Key.I; return true;
                case KeyCode.J: key = Key.J; return true;
                case KeyCode.K: key = Key.K; return true;
                case KeyCode.L: key = Key.L; return true;
                case KeyCode.M: key = Key.M; return true;
                case KeyCode.N: key = Key.N; return true;
                case KeyCode.O: key = Key.O; return true;
                case KeyCode.P: key = Key.P; return true;
                case KeyCode.Q: key = Key.Q; return true;
                case KeyCode.R: key = Key.R; return true;
                case KeyCode.S: key = Key.S; return true;
                case KeyCode.T: key = Key.T; return true;
                case KeyCode.U: key = Key.U; return true;
                case KeyCode.V: key = Key.V; return true;
                case KeyCode.W: key = Key.W; return true;
                case KeyCode.X: key = Key.X; return true;
                case KeyCode.Y: key = Key.Y; return true;
                case KeyCode.Z: key = Key.Z; return true;
                case KeyCode.Alpha0: key = Key.Digit0; return true;
                case KeyCode.Alpha1: key = Key.Digit1; return true;
                case KeyCode.Alpha2: key = Key.Digit2; return true;
                case KeyCode.Alpha3: key = Key.Digit3; return true;
                case KeyCode.Alpha4: key = Key.Digit4; return true;
                case KeyCode.Alpha5: key = Key.Digit5; return true;
                case KeyCode.Alpha6: key = Key.Digit6; return true;
                case KeyCode.Alpha7: key = Key.Digit7; return true;
                case KeyCode.Alpha8: key = Key.Digit8; return true;
                case KeyCode.Alpha9: key = Key.Digit9; return true;
                case KeyCode.Space: key = Key.Space; return true;
                case KeyCode.Tab: key = Key.Tab; return true;
                case KeyCode.Return: key = Key.Enter; return true;
                case KeyCode.KeypadEnter: key = Key.NumpadEnter; return true;
                case KeyCode.Backspace: key = Key.Backspace; return true;
                case KeyCode.Escape: key = Key.Escape; return true;
                case KeyCode.UpArrow: key = Key.UpArrow; return true;
                case KeyCode.DownArrow: key = Key.DownArrow; return true;
                case KeyCode.LeftArrow: key = Key.LeftArrow; return true;
                case KeyCode.RightArrow: key = Key.RightArrow; return true;
                default:
                    key = default;
                    return false;
            }
        }
#endif

        private void OnGUI()
        {
            if (!ShowOverlay)
            {
                return;
            }

            EnsureSampleCapacity();
            EnsureWhitePixel();

            DrawPanel();
        }

        private void OnDestroy()
        {
            if (_whitePixel == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                DestroyImmediate(_whitePixel);
            }
            else
            {
                Destroy(_whitePixel);
            }
#else
            Destroy(_whitePixel);
#endif
            _whitePixel = null;
        }

        private void DrawPanel()
        {
            var outer = ScreenRect;
            GUI.Box(outer, GUIContent.none);

            ComputeStats(out var currentMs, out var avgMs, out var worstMs);
            var currentFps = 1000f / Mathf.Max(0.0001f, currentMs);
            var avgFps = 1000f / Mathf.Max(0.0001f, avgMs);

            var titleRect = new Rect(outer.x + 8f, outer.y + 4f, outer.width - 16f, 18f);
            GUI.Label(titleRect, $"Perf FPS {currentFps:0.0} (avg {avgFps:0.0})");

            var graphRect = new Rect(outer.x + 8f, outer.y + 24f, outer.width - 16f, outer.height - 44f);
            DrawGraphBackground(graphRect);
            DrawGuideLine(graphRect, 16.67f, new Color(0.24f, 0.62f, 0.26f, 0.7f));
            DrawGuideLine(graphRect, 33.33f, new Color(0.72f, 0.62f, 0.2f, 0.7f));
            DrawFrameBars(graphRect);

            var footerRect = new Rect(outer.x + 8f, outer.yMax - 18f, outer.width - 16f, 16f);
            GUI.Label(footerRect, $"ms {currentMs:0.00} / avg {avgMs:0.00} / worst {worstMs:0.00} | F3");
        }

        private void DrawGraphBackground(Rect graphRect)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.42f);
            GUI.DrawTexture(graphRect, _whitePixel, ScaleMode.StretchToFill, alphaBlend: true);
            GUI.color = previous;
        }

        private void DrawGuideLine(Rect graphRect, float ms, Color color)
        {
            var normalized = Mathf.Clamp01(ms / GraphMaxFrameMs);
            var y = graphRect.yMax - (normalized * graphRect.height);

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(graphRect.x, y, graphRect.width, 1f), _whitePixel, ScaleMode.StretchToFill, alphaBlend: true);
            GUI.color = previous;
        }

        private void DrawFrameBars(Rect graphRect)
        {
            if (_sampleCount <= 0)
            {
                return;
            }

            var columns = Mathf.Min(_sampleCount, Mathf.Max(1, Mathf.FloorToInt(graphRect.width)));
            var startOrderedIndex = _sampleCount - columns;

            var previous = GUI.color;
            for (var i = 0; i < columns; i++)
            {
                var frameMs = GetSampleByOrder(startOrderedIndex + i);
                var normalized = Mathf.Clamp01(frameMs / GraphMaxFrameMs);
                var height = Mathf.Max(1f, normalized * graphRect.height);
                var x = graphRect.x + i;
                var y = graphRect.yMax - height;

                GUI.color = EvaluateBarColor(frameMs);
                GUI.DrawTexture(new Rect(x, y, 1f, height), _whitePixel, ScaleMode.StretchToFill, alphaBlend: true);
            }

            GUI.color = previous;
        }

        private static Color EvaluateBarColor(float frameMs)
        {
            if (frameMs <= 16.67f)
            {
                return new Color(0.26f, 0.84f, 0.33f, 0.95f);
            }

            if (frameMs <= 33.33f)
            {
                return new Color(0.95f, 0.82f, 0.24f, 0.95f);
            }

            return new Color(0.9f, 0.24f, 0.2f, 0.95f);
        }

        private void ComputeStats(out float currentMs, out float avgMs, out float worstMs)
        {
            currentMs = 0f;
            avgMs = 0f;
            worstMs = 0f;

            if (_sampleCount <= 0)
            {
                return;
            }

            currentMs = GetSampleByOrder(_sampleCount - 1);

            var window = Mathf.Min(_sampleCount, Mathf.Max(1, StatsWindowSamples));
            var start = _sampleCount - window;
            var sum = 0f;
            for (var i = 0; i < window; i++)
            {
                var ms = GetSampleByOrder(start + i);
                sum += ms;
                if (ms > worstMs)
                {
                    worstMs = ms;
                }
            }

            avgMs = sum / window;
        }

        private void RecordSample(float frameMs)
        {
            if (_frameMsSamples == null || _frameMsSamples.Length == 0)
            {
                return;
            }

            _frameMsSamples[_sampleWriteIndex] = frameMs;
            _sampleWriteIndex = (_sampleWriteIndex + 1) % _frameMsSamples.Length;
            _sampleCount = Mathf.Min(_sampleCount + 1, _frameMsSamples.Length);
        }

        private float GetSampleByOrder(int orderedIndex)
        {
            if (_sampleCount < _frameMsSamples.Length)
            {
                return _frameMsSamples[orderedIndex];
            }

            var actualIndex = (_sampleWriteIndex + orderedIndex) % _frameMsSamples.Length;
            return _frameMsSamples[actualIndex];
        }

        private void EnsureSampleCapacity()
        {
            var clamped = Mathf.Clamp(MaxSamples, 30, 600);
            if (_frameMsSamples != null && _frameMsSamples.Length == clamped)
            {
                return;
            }

            _frameMsSamples = new float[clamped];
            _sampleWriteIndex = 0;
            _sampleCount = 0;
        }

        private void EnsureWhitePixel()
        {
            if (_whitePixel != null)
            {
                return;
            }

            _whitePixel = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "TerrainLabPerformanceGraphPixel",
                hideFlags = HideFlags.HideAndDontSave
            };

            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }
    }
}
