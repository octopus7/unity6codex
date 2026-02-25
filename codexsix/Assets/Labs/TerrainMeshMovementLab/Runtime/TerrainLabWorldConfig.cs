using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    [CreateAssetMenu(
        fileName = "TerrainLabWorldConfig",
        menuName = "Terrain Lab/World Config",
        order = 1000)]
    public sealed class TerrainLabWorldConfig : ScriptableObject
    {
        [Header("World Seed")]
        [Min(0)] public int DefaultSeed = 12345;

        [Header("Chunk Grid")]
        [Min(2)] public int ChunkCells = 64;
        [Min(0.1f)] public float CellSize = 1f;

        [Header("Height")]
        public float HeightMin = -4f;
        public float HeightMax = 12f;

        [Header("Noise")]
        [Min(0.001f)] public float NoiseScale = 28f;
        [Range(1, 8)] public int Octaves = 4;
        [Range(0.01f, 1f)] public float Persistence = 0.5f;
        [Min(1f)] public float Lacunarity = 2f;

        [Header("Generated Assets")]
        public string HeightmapAssetFolder = "Assets/Labs/TerrainMeshMovementLab/Generated/Heightmaps";

        [Header("Viewer + Minimap")]
        [Min(1)] public int ViewerDefaultGridSize = 3;
        [Min(64)] public int MinimapTextureSize = 256;
        [Min(8f)] public float MinimapWindowWorldSize = 80f;

        public int ChunkVertices => ChunkCells + 1;
        public int PaddedChunkVertices => ChunkVertices + 2;

        public void ValidateInPlace()
        {
            if (ChunkCells < 2)
            {
                ChunkCells = 2;
            }

            if ((ChunkCells & 1) != 0)
            {
                ChunkCells += 1;
            }

            CellSize = Mathf.Max(0.1f, CellSize);
            NoiseScale = Mathf.Max(0.001f, NoiseScale);
            Octaves = Mathf.Clamp(Octaves, 1, 8);
            Persistence = Mathf.Clamp(Persistence, 0.01f, 1f);
            Lacunarity = Mathf.Max(1f, Lacunarity);

            if (HeightMax < HeightMin)
            {
                HeightMax = HeightMin;
            }

            ViewerDefaultGridSize = Mathf.Max(1, ViewerDefaultGridSize);
            if ((ViewerDefaultGridSize & 1) == 0)
            {
                ViewerDefaultGridSize += 1;
            }

            MinimapTextureSize = Mathf.Max(64, MinimapTextureSize);
            MinimapWindowWorldSize = Mathf.Max(8f, MinimapWindowWorldSize);

            if (string.IsNullOrWhiteSpace(HeightmapAssetFolder))
            {
                HeightmapAssetFolder = "Assets/Labs/TerrainMeshMovementLab/Generated/Heightmaps";
            }
        }

        private void OnValidate()
        {
            ValidateInPlace();
        }
    }
}
