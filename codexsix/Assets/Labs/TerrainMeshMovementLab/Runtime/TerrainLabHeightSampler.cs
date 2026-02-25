using UnityEngine;

namespace CodexSix.TerrainMeshMovementLab
{
    public static class TerrainLabHeightSampler
    {
        public static Vector2Int GetChunkCoordFromWorld(TerrainLabWorldConfig config, Vector2 worldXZ)
        {
            config.ValidateInPlace();

            var half = config.ChunkCells * 0.5f;
            var gridX = worldXZ.x / config.CellSize;
            var gridZ = worldXZ.y / config.CellSize;

            var chunkX = Mathf.FloorToInt((gridX + half) / config.ChunkCells);
            var chunkZ = Mathf.FloorToInt((gridZ + half) / config.ChunkCells);
            return new Vector2Int(chunkX, chunkZ);
        }

        public static Vector2 GetLocalVertexCoordinates(TerrainLabWorldConfig config, Vector2 worldXZ, Vector2Int chunkCoord)
        {
            config.ValidateInPlace();

            var gridX = worldXZ.x / config.CellSize;
            var gridZ = worldXZ.y / config.CellSize;
            var half = config.ChunkCells * 0.5f;

            var localX = gridX - ((chunkCoord.x * config.ChunkCells) - half);
            var localZ = gridZ - ((chunkCoord.y * config.ChunkCells) - half);
            return new Vector2(localX, localZ);
        }

        public static Vector3 GetChunkOriginWorld(TerrainLabWorldConfig config, Vector2Int chunkCoord)
        {
            config.ValidateInPlace();
            var half = config.ChunkCells * 0.5f;
            var x = ((chunkCoord.x * config.ChunkCells) - half) * config.CellSize;
            var z = ((chunkCoord.y * config.ChunkCells) - half) * config.CellSize;
            return new Vector3(x, 0f, z);
        }

        public static bool TrySampleHeight(
            TerrainLabWorldConfig config,
            int seed,
            Vector2 worldXZ,
            bool autoGenerateMissing,
            out float height)
        {
            var chunkCoord = GetChunkCoordFromWorld(config, worldXZ);
            var local = GetLocalVertexCoordinates(config, worldXZ, chunkCoord);

            return TerrainLabHeightChunkStore.TrySampleChunkHeightBilinear(
                config,
                seed,
                chunkCoord,
                local.x,
                local.y,
                autoGenerateMissing,
                out height);
        }
    }
}
