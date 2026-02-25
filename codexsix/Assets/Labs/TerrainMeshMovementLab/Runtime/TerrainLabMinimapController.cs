using System.Collections.Generic;
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
        [Min(0f)] public float MinWorldMoveBeforeRefresh = 0.4f;

        private Texture2D _minimapTexture;
        private Color32[] _pixels;
        private float _nextRefreshAt;
        private bool _hasLastRefreshCenter;
        private Vector3 _lastRefreshCenter;
        private readonly Dictionary<Vector2Int, TerrainLabChunkHeightData> _chunkDataFrameCache = new();

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
            _hasLastRefreshCenter = false;
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

            if (MinWorldMoveBeforeRefresh > 0f && _hasLastRefreshCenter)
            {
                var sqrDistance = (PlayerTarget.position - _lastRefreshCenter).sqrMagnitude;
                var minSqrDistance = MinWorldMoveBeforeRefresh * MinWorldMoveBeforeRefresh;
                if (sqrDistance < minSqrDistance)
                {
                    _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.01f, RefreshIntervalSeconds);
                    return;
                }
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
            _chunkDataFrameCache.Clear();

            for (var py = 0; py < size; py++)
            {
                var v = py / (float)(size - 1);
                var worldZ = Mathf.Lerp(center.z - half, center.z + half, v);

                for (var px = 0; px < size; px++)
                {
                    var u = px / (float)(size - 1);
                    var worldX = Mathf.Lerp(center.x - half, center.x + half, u);

                    var sampled = TrySampleHeightCached(config, new Vector2(worldX, worldZ), out var height);
                    var color = sampled
                        ? TerrainLabHeightColorRamp.Evaluate(config, height)
                        : new Color32(18, 20, 24, 255);

                    _pixels[(py * size) + px] = color;
                }
            }

            _minimapTexture.SetPixels32(_pixels);
            _minimapTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _lastRefreshCenter = center;
            _hasLastRefreshCenter = true;
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

        private bool TrySampleHeightCached(TerrainLabWorldConfig config, Vector2 worldXZ, out float height)
        {
            var chunkCoord = TerrainLabHeightSampler.GetChunkCoordFromWorld(config, worldXZ);
            if (!_chunkDataFrameCache.TryGetValue(chunkCoord, out var chunkData))
            {
                chunkData = World.GetOrCreateChunkData(chunkCoord);
                _chunkDataFrameCache[chunkCoord] = chunkData;
            }

            if (chunkData == null)
            {
                height = 0f;
                return false;
            }

            var local = TerrainLabHeightSampler.GetLocalVertexCoordinates(config, worldXZ, chunkCoord);
            return TrySampleChunkHeightBilinear(config, chunkData, local.x, local.y, out height);
        }

        private static bool TrySampleChunkHeightBilinear(
            TerrainLabWorldConfig config,
            TerrainLabChunkHeightData chunkData,
            float localVertexX,
            float localVertexZ,
            out float height)
        {
            var clampedX = Mathf.Clamp(localVertexX, 0f, config.ChunkCells);
            var clampedZ = Mathf.Clamp(localVertexZ, 0f, config.ChunkCells);

            var x0 = Mathf.FloorToInt(clampedX);
            var z0 = Mathf.FloorToInt(clampedZ);
            var x1 = Mathf.Min(x0 + 1, config.ChunkCells);
            var z1 = Mathf.Min(z0 + 1, config.ChunkCells);

            var tx = clampedX - x0;
            var tz = clampedZ - z0;

            var heights = chunkData.Heights;
            var h00 = heights[x0 + 1, z0 + 1];
            var h10 = heights[x1 + 1, z0 + 1];
            var h01 = heights[x0 + 1, z1 + 1];
            var h11 = heights[x1 + 1, z1 + 1];

            var hx0 = Mathf.Lerp(h00, h10, tx);
            var hx1 = Mathf.Lerp(h01, h11, tx);
            height = Mathf.Lerp(hx0, hx1, tz);
            return true;
        }

    }
}
