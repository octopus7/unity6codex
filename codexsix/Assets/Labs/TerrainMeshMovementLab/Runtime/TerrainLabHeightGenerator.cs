using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    public static class TerrainLabHeightGenerator
    {
        public static Texture2D GenerateChunkHeightTexture(TerrainLabWorldConfig config, int seed, Vector2Int chunkCoord)
        {
            config.ValidateInPlace();

            var size = config.PaddedChunkVertices;
            var colors = new Color32[size * size];

            for (var z = 0; z < size; z++)
            {
                var localZWithPadding = z - 1;
                var globalVertexZ = GetGlobalVertexIndex(config, chunkCoord.y, localZWithPadding);

                for (var x = 0; x < size; x++)
                {
                    var localXWithPadding = x - 1;
                    var globalVertexX = GetGlobalVertexIndex(config, chunkCoord.x, localXWithPadding);

                    var height = SampleDeterministicHeight(config, seed, globalVertexX, globalVertexZ);
                    colors[(z * size) + x] = TerrainLabHeightCodec.EncodeHeight(height, config.HeightMin, config.HeightMax);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = $"TerrainLabHeight_{seed}_{chunkCoord.x}_{chunkCoord.y}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixels32(colors);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        public static float SampleDeterministicHeight(TerrainLabWorldConfig config, int seed, int globalVertexX, int globalVertexZ)
        {
            config.ValidateInPlace();

            var cellSize = config.CellSize;
            var worldX = globalVertexX * cellSize;
            var worldZ = globalVertexZ * cellSize;

            HashSeed(seed, out var offsetX, out var offsetZ);

            var amplitude = 1f;
            var frequency = 1f;
            var total = 0f;
            var weight = 0f;

            for (var octave = 0; octave < config.Octaves; octave++)
            {
                var sampleX = ((worldX + offsetX) / config.NoiseScale) * frequency;
                var sampleZ = ((worldZ + offsetZ) / config.NoiseScale) * frequency;
                var value = Mathf.PerlinNoise(sampleX, sampleZ);

                total += value * amplitude;
                weight += amplitude;

                amplitude *= config.Persistence;
                frequency *= config.Lacunarity;
            }

            var normalized = weight > 0f ? total / weight : 0f;
            return Mathf.Lerp(config.HeightMin, config.HeightMax, normalized);
        }

        private static int GetGlobalVertexIndex(TerrainLabWorldConfig config, int chunkIndex, int localVertexWithPadding)
        {
            var half = config.ChunkCells / 2;
            return (chunkIndex * config.ChunkCells) + localVertexWithPadding - half;
        }

        private static void HashSeed(int seed, out float offsetX, out float offsetZ)
        {
            unchecked
            {
                uint state = (uint)seed;
                state ^= 0x9E3779B9u;
                state *= 0x85EBCA6Bu;
                state ^= state >> 13;

                var xBits = state & 0xFFFFu;
                var zBits = (state >> 16) & 0xFFFFu;

                offsetX = ((xBits / 65535f) * 200000f) - 100000f;
                offsetZ = ((zBits / 65535f) * 200000f) - 100000f;
            }
        }
    }
}
