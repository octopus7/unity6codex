using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CodexSix.TerrainMeshMovementLab
{
    public sealed class TerrainLabChunkHeightData
    {
        public TerrainLabChunkHeightData(int size, float[,] heights)
        {
            PaddedSize = size;
            Heights = heights;
        }

        public int PaddedSize { get; }
        public float[,] Heights { get; }
    }

    public static class TerrainLabHeightChunkStore
    {
        private readonly struct ChunkCacheKey : IEquatable<ChunkCacheKey>
        {
            public ChunkCacheKey(int seed, int chunkX, int chunkZ, int chunkVertices)
            {
                Seed = seed;
                ChunkX = chunkX;
                ChunkZ = chunkZ;
                ChunkVertices = chunkVertices;
            }

            public readonly int Seed;
            public readonly int ChunkX;
            public readonly int ChunkZ;
            public readonly int ChunkVertices;

            public bool Equals(ChunkCacheKey other)
            {
                return Seed == other.Seed
                    && ChunkX == other.ChunkX
                    && ChunkZ == other.ChunkZ
                    && ChunkVertices == other.ChunkVertices;
            }

            public override bool Equals(object obj)
            {
                return obj is ChunkCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Seed, ChunkX, ChunkZ, ChunkVertices);
            }
        }

        private static readonly Dictionary<ChunkCacheKey, TerrainLabChunkHeightData> Cache = new();

        public static string GetChunkFileName(int seed, Vector2Int chunkCoord, int chunkVertices)
        {
            return $"height_s{seed}_cx{chunkCoord.x}_cz{chunkCoord.y}_v{chunkVertices}_p1.png";
        }

        public static string GetChunkAssetPath(TerrainLabWorldConfig config, int seed, Vector2Int chunkCoord)
        {
            config.ValidateInPlace();
            var folder = config.HeightmapAssetFolder.TrimEnd('/', '\\');
            return folder + "/" + GetChunkFileName(seed, chunkCoord, config.ChunkVertices);
        }

        public static bool ChunkAssetExists(TerrainLabWorldConfig config, int seed, Vector2Int chunkCoord)
        {
            var absolutePath = ResolveAbsolutePath(GetChunkAssetPath(config, seed, chunkCoord));
            return File.Exists(absolutePath);
        }

        public static TerrainLabChunkHeightData LoadOrCreateChunk(
            TerrainLabWorldConfig config,
            int seed,
            Vector2Int chunkCoord,
            bool forceRegenerate,
            bool autoGenerateMissing)
        {
            config.ValidateInPlace();

            var cacheKey = BuildChunkCacheKey(seed, chunkCoord, config.ChunkVertices);
            if (!forceRegenerate && Cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var assetPath = GetChunkAssetPath(config, seed, chunkCoord);
            var absolutePath = ResolveAbsolutePath(assetPath);
            if (forceRegenerate || (!File.Exists(absolutePath) && autoGenerateMissing))
            {
                var generated = TerrainLabHeightGenerator.GenerateChunkHeightTexture(config, seed, chunkCoord);
                SaveChunkTexture(config, seed, chunkCoord, generated);
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    UnityEngine.Object.DestroyImmediate(generated);
                }
                else
                {
                    UnityEngine.Object.Destroy(generated);
                }
#else
                UnityEngine.Object.Destroy(generated);
#endif
            }

            if (!File.Exists(absolutePath))
            {
                return null;
            }

            var loaded = ReadChunkFromDisk(config, absolutePath);
            if (loaded != null)
            {
                Cache[cacheKey] = loaded;
            }

            return loaded;
        }

        public static void SaveChunkTexture(TerrainLabWorldConfig config, int seed, Vector2Int chunkCoord, Texture2D texture)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            var assetPath = GetChunkAssetPath(config, seed, chunkCoord);
            var absolutePath = ResolveAbsolutePath(assetPath);

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var png = texture.EncodeToPNG();
            File.WriteAllBytes(absolutePath, png);

            var cacheKey = BuildChunkCacheKey(seed, chunkCoord, config.ChunkVertices);
            Cache.Remove(cacheKey);

#if UNITY_EDITOR
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            ApplyImporterSettings(assetPath);
            AssetDatabase.SaveAssets();
#endif
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

        public static bool TrySampleChunkHeightBilinear(
            TerrainLabWorldConfig config,
            int seed,
            Vector2Int chunkCoord,
            float localVertexX,
            float localVertexZ,
            bool autoGenerateMissing,
            out float height)
        {
            var data = LoadOrCreateChunk(config, seed, chunkCoord, forceRegenerate: false, autoGenerateMissing: autoGenerateMissing);
            if (data == null)
            {
                height = 0f;
                return false;
            }

            var clampedX = Mathf.Clamp(localVertexX, 0f, config.ChunkCells);
            var clampedZ = Mathf.Clamp(localVertexZ, 0f, config.ChunkCells);

            var x0 = Mathf.FloorToInt(clampedX);
            var z0 = Mathf.FloorToInt(clampedZ);
            var x1 = Mathf.Min(x0 + 1, config.ChunkCells);
            var z1 = Mathf.Min(z0 + 1, config.ChunkCells);

            var tx = clampedX - x0;
            var tz = clampedZ - z0;

            var heights = data.Heights;
            var h00 = heights[x0 + 1, z0 + 1];
            var h10 = heights[x1 + 1, z0 + 1];
            var h01 = heights[x0 + 1, z1 + 1];
            var h11 = heights[x1 + 1, z1 + 1];

            var hx0 = Mathf.Lerp(h00, h10, tx);
            var hx1 = Mathf.Lerp(h01, h11, tx);
            height = Mathf.Lerp(hx0, hx1, tz);
            return true;
        }

        private static TerrainLabChunkHeightData ReadChunkFromDisk(TerrainLabWorldConfig config, string absolutePath)
        {
            var bytes = File.ReadAllBytes(absolutePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!texture.LoadImage(bytes, markNonReadable: false))
            {
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                else
                {
                    UnityEngine.Object.Destroy(texture);
                }
#else
                UnityEngine.Object.Destroy(texture);
#endif
                return null;
            }

            var expectedSize = config.PaddedChunkVertices;
            if (texture.width != expectedSize || texture.height != expectedSize)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"Terrain Lab: invalid chunk texture size at '{absolutePath}'. " +
                    $"Expected {expectedSize}x{expectedSize}, got {texture.width}x{texture.height}.");
                if (Application.isEditor)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                else
                {
                    UnityEngine.Object.Destroy(texture);
                }
#else
                UnityEngine.Object.Destroy(texture);
#endif
                return null;
            }

            var pixels = texture.GetPixels32();
            var heights = new float[expectedSize, expectedSize];
            for (var z = 0; z < expectedSize; z++)
            {
                for (var x = 0; x < expectedSize; x++)
                {
                    var color = pixels[(z * expectedSize) + x];
                    heights[x, z] = TerrainLabHeightCodec.DecodeHeight(color, config.HeightMin, config.HeightMax);
                }
            }

#if UNITY_EDITOR
            if (Application.isEditor)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            else
            {
                UnityEngine.Object.Destroy(texture);
            }
#else
            UnityEngine.Object.Destroy(texture);
#endif

            return new TerrainLabChunkHeightData(expectedSize, heights);
        }

        private static ChunkCacheKey BuildChunkCacheKey(int seed, Vector2Int chunkCoord, int chunkVertices)
        {
            return new ChunkCacheKey(seed, chunkCoord.x, chunkCoord.y, chunkVertices);
        }

        private static string ResolveAbsolutePath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relative);
        }

#if UNITY_EDITOR
        private static void ApplyImporterSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;

            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                changed = true;
            }

            if (importer.textureShape != TextureImporterShape.Texture2D)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                changed = true;
            }

            if (importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            importer.SaveAndReimport();
        }
#endif
    }
}
