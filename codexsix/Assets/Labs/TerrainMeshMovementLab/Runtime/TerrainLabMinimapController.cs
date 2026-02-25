using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    [DisallowMultipleComponent]
    public sealed class TerrainLabMinimapController : MonoBehaviour
    {
        [Header("References")]
        public TerrainLabSingleChunkWorld World;
        public Transform PlayerTarget;

        [Header("Display")]
        public bool ShowMinimap = true;
        public Rect ScreenRect = new(16f, 36f, 220f, 220f);
        [Range(0.01f, 1f)] public float RefreshIntervalSeconds = 0.12f;

        private Texture2D _minimapTexture;
        private Color32[] _pixels;
        private float _nextRefreshAt;

        private void Awake()
        {
            if (World == null)
            {
                World = FindFirstObjectByType<TerrainLabSingleChunkWorld>();
            }

            if (PlayerTarget == null)
            {
                var controller = FindFirstObjectByType<TerrainLabCharacterController>();
                if (controller != null)
                {
                    PlayerTarget = controller.transform;
                }
            }
        }

        private void OnDestroy()
        {
            if (_minimapTexture == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                DestroyImmediate(_minimapTexture);
            }
            else
            {
                Destroy(_minimapTexture);
            }
#else
            Destroy(_minimapTexture);
#endif

            _minimapTexture = null;
            _pixels = null;
        }

        public void BindWorld(TerrainLabSingleChunkWorld world)
        {
            World = world;
        }

        public void RequestImmediateRefresh()
        {
            _nextRefreshAt = -1f;
        }

        private void Update()
        {
            if (!ShowMinimap || World == null || PlayerTarget == null || World.WorldConfig == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextRefreshAt)
            {
                return;
            }

            _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.01f, RefreshIntervalSeconds);
            RefreshMinimapTexture();
        }

        private void OnGUI()
        {
            if (!ShowMinimap || _minimapTexture == null)
            {
                return;
            }

            var outer = ScreenRect;
            GUI.Box(outer, GUIContent.none);

            var inner = new Rect(outer.x + 6f, outer.y + 6f, outer.width - 12f, outer.height - 12f);
            GUI.DrawTexture(inner, _minimapTexture, ScaleMode.StretchToFill, alphaBlend: false);

            var center = new Vector2(inner.x + (inner.width * 0.5f), inner.y + (inner.height * 0.5f));
            var marker = new Rect(center.x - 3f, center.y - 3f, 6f, 6f);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 0.12f, 0.12f, 0.95f);
            GUI.DrawTexture(marker, Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (World != null)
            {
                GUI.Label(new Rect(outer.x + 8f, outer.y + outer.height + 2f, 300f, 20f), $"Seed: {World.CurrentSeed}");
            }
        }

        private void RefreshMinimapTexture()
        {
            var config = World.WorldConfig;
            config.ValidateInPlace();

            var size = config.MinimapTextureSize;
            EnsureTexture(size);

            var worldSize = config.MinimapWindowWorldSize;
            var half = worldSize * 0.5f;
            var center = PlayerTarget.position;

            for (var py = 0; py < size; py++)
            {
                var v = py / (float)(size - 1);
                var worldZ = Mathf.Lerp(center.z - half, center.z + half, v);

                for (var px = 0; px < size; px++)
                {
                    var u = px / (float)(size - 1);
                    var worldX = Mathf.Lerp(center.x - half, center.x + half, u);

                    var sampled = TerrainLabHeightSampler.TrySampleHeight(
                        config,
                        World.CurrentSeed,
                        new Vector2(worldX, worldZ),
                        autoGenerateMissing: true,
                        out var height);

                    var color = sampled
                        ? EvaluateHeightColor(config, height)
                        : new Color32(18, 20, 24, 255);

                    _pixels[(py * size) + px] = color;
                }
            }

            _minimapTexture.SetPixels32(_pixels);
            _minimapTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private void EnsureTexture(int size)
        {
            if (_minimapTexture != null && _minimapTexture.width == size && _minimapTexture.height == size)
            {
                return;
            }

            if (_minimapTexture != null)
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    DestroyImmediate(_minimapTexture);
                }
                else
                {
                    Destroy(_minimapTexture);
                }
#else
                Destroy(_minimapTexture);
#endif
            }

            _minimapTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "TerrainLabMinimapRuntime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            _pixels = new Color32[size * size];
        }

        private static Color32 EvaluateHeightColor(TerrainLabWorldConfig config, float height)
        {
            if (Mathf.Approximately(config.HeightMin, config.HeightMax))
            {
                return new Color32(124, 170, 124, 255);
            }

            var t = Mathf.InverseLerp(config.HeightMin, config.HeightMax, height);
            if (t < 0.30f)
            {
                return Color32.Lerp(new Color32(35, 90, 42, 255), new Color32(84, 130, 62, 255), t / 0.30f);
            }

            if (t < 0.7f)
            {
                return Color32.Lerp(new Color32(84, 130, 62, 255), new Color32(160, 150, 95, 255), (t - 0.30f) / 0.40f);
            }

            return Color32.Lerp(new Color32(160, 150, 95, 255), new Color32(238, 236, 220, 255), (t - 0.7f) / 0.3f);
        }
    }
}
