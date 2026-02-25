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
        [Min(9)] public int MaxCachedChunkTextures = 225;
        [Min(1)] public int CacheDiscardChunkDistance = 12;
        [Min(0)] public int PrefetchChunkPadding = 1;

        private float _nextRefreshAt;
        private bool _hasLastRefreshCenter;
        private Vector3 _lastRefreshCenter;
        private readonly Dictionary<Vector2Int, ChunkColorCacheEntry> _chunkColorCache = new();
        private int _cachedSeed = int.MinValue;
        private float _cachedHeightMin = float.NaN;
        private float _cachedHeightMax = float.NaN;
        private float _cachedWaterLevel = float.NaN;
        private readonly List<Vector2Int> _pruneKeysBuffer = new();

        private sealed class ChunkColorCacheEntry
        {
            public Texture2D Texture;
            public int LastTouchedFrame;
        }

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
            ClearChunkColorCache();
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

            var config = World.WorldConfig;
            config.ValidateInPlace();
            EnsureCacheSignature(config);

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
            WarmVisibleChunkCache(config, PlayerTarget.position, includePadding: true);
        }

        private void OnGUI()
        {
            if (!ShowMinimap || World == null || PlayerTarget == null || World.WorldConfig == null)
            {
                return;
            }

            var config = World.WorldConfig;
            config.ValidateInPlace();
            EnsureCacheSignature(config);

            var outer = ScreenRect;
            GUI.Box(outer, GUIContent.none);

            var inner = new Rect(outer.x + 6f, outer.y + 6f, outer.width - 12f, outer.height - 12f);
            DrawStitchedChunks(inner, config, PlayerTarget.position);

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

        private void DrawStitchedChunks(Rect inner, TerrainLabWorldConfig config, Vector3 center)
        {
            var worldSize = config.MinimapWindowWorldSize;
            var half = worldSize * 0.5f;
            var minX = center.x - half;
            var maxX = center.x + half;
            var minZ = center.z - half;
            var maxZ = center.z + half;

            var previous = GUI.color;
            GUI.color = new Color(0.07f, 0.08f, 0.1f, 1f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: false);
            GUI.color = previous;

            GetVisibleChunkRange(config, minX, maxX, minZ, maxZ, out var minChunkX, out var maxChunkX, out var minChunkZ, out var maxChunkZ);
            for (var cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                for (var cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    var chunkCoord = new Vector2Int(cx, cz);
                    var texture = GetOrCreateChunkColorTexture(config, chunkCoord);
                    if (texture == null)
                    {
                        continue;
                    }

                    DrawChunkIntersection(inner, config, chunkCoord, texture, minX, maxX, minZ, maxZ);
                }
            }
        }

        private void DrawChunkIntersection(
            Rect inner,
            TerrainLabWorldConfig config,
            Vector2Int chunkCoord,
            Texture2D texture,
            float windowMinX,
            float windowMaxX,
            float windowMinZ,
            float windowMaxZ)
        {
            var chunkOrigin = TerrainLabHeightSampler.GetChunkOriginWorld(config, chunkCoord);
            var chunkSizeWorld = config.ChunkCells * config.CellSize;
            var chunkMinX = chunkOrigin.x;
            var chunkMaxX = chunkOrigin.x + chunkSizeWorld;
            var chunkMinZ = chunkOrigin.z;
            var chunkMaxZ = chunkOrigin.z + chunkSizeWorld;

            var intersectMinX = Mathf.Max(windowMinX, chunkMinX);
            var intersectMaxX = Mathf.Min(windowMaxX, chunkMaxX);
            var intersectMinZ = Mathf.Max(windowMinZ, chunkMinZ);
            var intersectMaxZ = Mathf.Min(windowMaxZ, chunkMaxZ);
            if (intersectMinX >= intersectMaxX || intersectMinZ >= intersectMaxZ)
            {
                return;
            }

            var windowSizeX = windowMaxX - windowMinX;
            var windowSizeZ = windowMaxZ - windowMinZ;
            if (windowSizeX <= 0.0001f || windowSizeZ <= 0.0001f)
            {
                return;
            }

            var dstMinX = inner.x + ((intersectMinX - windowMinX) / windowSizeX) * inner.width;
            var dstMaxX = inner.x + ((intersectMaxX - windowMinX) / windowSizeX) * inner.width;
            var dstMinY = inner.y + ((windowMaxZ - intersectMaxZ) / windowSizeZ) * inner.height;
            var dstMaxY = inner.y + ((windowMaxZ - intersectMinZ) / windowSizeZ) * inner.height;

            const float seamOverlapPixels = 1f;
            if (intersectMinX > windowMinX)
            {
                dstMinX -= seamOverlapPixels;
            }

            if (intersectMaxX < windowMaxX)
            {
                dstMaxX += seamOverlapPixels;
            }

            if (intersectMaxZ < windowMaxZ)
            {
                dstMinY -= seamOverlapPixels;
            }

            if (intersectMinZ > windowMinZ)
            {
                dstMaxY += seamOverlapPixels;
            }

            dstMinX = Mathf.Max(inner.xMin, dstMinX);
            dstMaxX = Mathf.Min(inner.xMax, dstMaxX);
            dstMinY = Mathf.Max(inner.yMin, dstMinY);
            dstMaxY = Mathf.Min(inner.yMax, dstMaxY);

            var destination = Rect.MinMaxRect(dstMinX, dstMinY, dstMaxX, dstMaxY);
            if (destination.width <= 0.1f || destination.height <= 0.1f)
            {
                return;
            }

            var uvMinX = Mathf.InverseLerp(chunkMinX, chunkMaxX, intersectMinX);
            var uvMaxX = Mathf.InverseLerp(chunkMinX, chunkMaxX, intersectMaxX);
            var uvMinY = Mathf.InverseLerp(chunkMinZ, chunkMaxZ, intersectMinZ);
            var uvMaxY = Mathf.InverseLerp(chunkMinZ, chunkMaxZ, intersectMaxZ);

            var halfTexelU = 0.5f / Mathf.Max(1, texture.width);
            var halfTexelV = 0.5f / Mathf.Max(1, texture.height);
            if (intersectMinX > chunkMinX)
            {
                uvMinX = Mathf.Min(uvMaxX, uvMinX + halfTexelU);
            }

            if (intersectMaxX < chunkMaxX)
            {
                uvMaxX = Mathf.Max(uvMinX, uvMaxX - halfTexelU);
            }

            if (intersectMinZ > chunkMinZ)
            {
                uvMinY = Mathf.Min(uvMaxY, uvMinY + halfTexelV);
            }

            if (intersectMaxZ < chunkMaxZ)
            {
                uvMaxY = Mathf.Max(uvMinY, uvMaxY - halfTexelV);
            }

            var textureCoords = Rect.MinMaxRect(uvMinX, uvMinY, uvMaxX, uvMaxY);

            GUI.DrawTextureWithTexCoords(destination, texture, textureCoords, alphaBlend: false);
        }

        private void WarmVisibleChunkCache(TerrainLabWorldConfig config, Vector3 center, bool includePadding)
        {
            var worldSize = config.MinimapWindowWorldSize;
            var half = worldSize * 0.5f;
            var paddingWorld = includePadding
                ? Mathf.Max(0, PrefetchChunkPadding) * config.ChunkCells * config.CellSize
                : 0f;

            var minX = center.x - half - paddingWorld;
            var maxX = center.x + half + paddingWorld;
            var minZ = center.z - half - paddingWorld;
            var maxZ = center.z + half + paddingWorld;

            GetVisibleChunkRange(config, minX, maxX, minZ, maxZ, out var minChunkX, out var maxChunkX, out var minChunkZ, out var maxChunkZ);

            for (var cz = minChunkZ; cz <= maxChunkZ; cz++)
            {
                for (var cx = minChunkX; cx <= maxChunkX; cx++)
                {
                    GetOrCreateChunkColorTexture(config, new Vector2Int(cx, cz));
                }
            }

            _lastRefreshCenter = center;
            _hasLastRefreshCenter = true;
            PruneChunkColorCache(config, center);
        }

        private Texture2D GetOrCreateChunkColorTexture(TerrainLabWorldConfig config, Vector2Int chunkCoord)
        {
            if (_chunkColorCache.TryGetValue(chunkCoord, out var existing))
            {
                if (existing.Texture != null)
                {
                    existing.LastTouchedFrame = Time.frameCount;
                    return existing.Texture;
                }

                _chunkColorCache.Remove(chunkCoord);
            }

            var chunkData = World.GetOrCreateChunkData(chunkCoord);
            if (chunkData == null)
            {
                return null;
            }

            var texture = BuildChunkColorTexture(config, chunkCoord, chunkData);
            if (texture == null)
            {
                return null;
            }

            _chunkColorCache[chunkCoord] = new ChunkColorCacheEntry
            {
                Texture = texture,
                LastTouchedFrame = Time.frameCount
            };

            return texture;
        }

        private static Texture2D BuildChunkColorTexture(
            TerrainLabWorldConfig config,
            Vector2Int chunkCoord,
            TerrainLabChunkHeightData chunkData)
        {
            if (chunkData == null || chunkData.Heights == null)
            {
                return null;
            }

            var resolution = config.ChunkCells;
            var heights = chunkData.Heights;
            var colors = new Color32[resolution * resolution];

            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var height = heights[x + 1, z + 1];
                    colors[(z * resolution) + x] = TerrainLabHeightColorRamp.Evaluate(config, height);
                }
            }

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = $"TerrainLabMinimapChunk_{chunkCoord.x}_{chunkCoord.y}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixels32(colors);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        private void PruneChunkColorCache(TerrainLabWorldConfig config, Vector3 center)
        {
            var centerChunk = TerrainLabHeightSampler.GetChunkCoordFromWorld(config, new Vector2(center.x, center.z));
            var maxChunkDistance = Mathf.Max(1, CacheDiscardChunkDistance);

            _pruneKeysBuffer.Clear();
            foreach (var pair in _chunkColorCache)
            {
                if (pair.Value == null || pair.Value.Texture == null)
                {
                    _pruneKeysBuffer.Add(pair.Key);
                    continue;
                }

                var dx = Mathf.Abs(pair.Key.x - centerChunk.x);
                var dz = Mathf.Abs(pair.Key.y - centerChunk.y);
                if (Mathf.Max(dx, dz) > maxChunkDistance)
                {
                    _pruneKeysBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < _pruneKeysBuffer.Count; i++)
            {
                DestroyChunkColorTexture(_pruneKeysBuffer[i]);
            }

            var maxCount = Mathf.Max(9, MaxCachedChunkTextures);
            while (_chunkColorCache.Count > maxCount)
            {
                var found = false;
                var oldestFrame = int.MaxValue;
                var oldestCoord = Vector2Int.zero;

                foreach (var pair in _chunkColorCache)
                {
                    if (pair.Value == null || pair.Value.Texture == null)
                    {
                        oldestCoord = pair.Key;
                        found = true;
                        break;
                    }

                    if (pair.Value.LastTouchedFrame < oldestFrame)
                    {
                        oldestFrame = pair.Value.LastTouchedFrame;
                        oldestCoord = pair.Key;
                        found = true;
                    }
                }

                if (!found)
                {
                    break;
                }

                DestroyChunkColorTexture(oldestCoord);
            }
        }

        private void EnsureCacheSignature(TerrainLabWorldConfig config)
        {
            var seed = World != null ? World.CurrentSeed : int.MinValue;
            var signatureChanged = seed != _cachedSeed
                || !Mathf.Approximately(_cachedHeightMin, config.HeightMin)
                || !Mathf.Approximately(_cachedHeightMax, config.HeightMax)
                || !Mathf.Approximately(_cachedWaterLevel, config.WaterLevel);

            if (!signatureChanged)
            {
                return;
            }

            ClearChunkColorCache();
            _cachedSeed = seed;
            _cachedHeightMin = config.HeightMin;
            _cachedHeightMax = config.HeightMax;
            _cachedWaterLevel = config.WaterLevel;
        }

        private void GetVisibleChunkRange(
            TerrainLabWorldConfig config,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            out int minChunkX,
            out int maxChunkX,
            out int minChunkZ,
            out int maxChunkZ)
        {
            var epsilon = Mathf.Max(0.0001f, config.CellSize * 0.0001f);
            var minCoord = TerrainLabHeightSampler.GetChunkCoordFromWorld(config, new Vector2(minX, minZ));
            var maxCoord = TerrainLabHeightSampler.GetChunkCoordFromWorld(config, new Vector2(maxX - epsilon, maxZ - epsilon));

            minChunkX = Mathf.Min(minCoord.x, maxCoord.x);
            maxChunkX = Mathf.Max(minCoord.x, maxCoord.x);
            minChunkZ = Mathf.Min(minCoord.y, maxCoord.y);
            maxChunkZ = Mathf.Max(minCoord.y, maxCoord.y);
        }

        private void ClearChunkColorCache()
        {
            if (_chunkColorCache.Count == 0)
            {
                return;
            }

            var keys = new List<Vector2Int>(_chunkColorCache.Keys);
            for (var i = 0; i < keys.Count; i++)
            {
                DestroyChunkColorTexture(keys[i]);
            }

            _chunkColorCache.Clear();
        }

        private void DestroyChunkColorTexture(Vector2Int chunkCoord)
        {
            if (!_chunkColorCache.TryGetValue(chunkCoord, out var entry))
            {
                return;
            }

            if (entry != null && entry.Texture != null)
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    DestroyImmediate(entry.Texture);
                }
                else
                {
                    Destroy(entry.Texture);
                }
#else
                Destroy(entry.Texture);
#endif
            }

            _chunkColorCache.Remove(chunkCoord);
        }

    }
}
