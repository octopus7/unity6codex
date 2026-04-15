#nullable enable

using System;
using UnityEngine;

namespace McpTest.VoxelVillage
{
    [Serializable]
    public sealed class VillageLayoutData
    {
        public int seed;
        public int townSize;
        public Vector2Int plazaCenter;
        public VillageRoadPath[] roads = Array.Empty<VillageRoadPath>();
        public VillageBuildingLayout[] buildings = Array.Empty<VillageBuildingLayout>();
        public VillageDoorLayout[] doors = Array.Empty<VillageDoorLayout>();
        public VillageFencePath[] fences = Array.Empty<VillageFencePath>();
        public VillageFoliagePlacement[] foliage = Array.Empty<VillageFoliagePlacement>();
        public VillageNpcSpawnPoint[] npcSpawnPoints = Array.Empty<VillageNpcSpawnPoint>();
    }

    [Serializable]
    public sealed class VillageRoadPath
    {
        public string id = string.Empty;
        public Vector2Int[] cells = Array.Empty<Vector2Int>();
    }

    [Serializable]
    public sealed class VillageBuildingLayout
    {
        public string id = string.Empty;
        public Vector2Int origin;
        public Vector2Int size;
        public int height;
    }

    [Serializable]
    public sealed class VillageDoorLayout
    {
        public string id = string.Empty;
        public string buildingId = string.Empty;
        public Vector2Int cell;
        public Vector2Int facing;
        public bool startsOpen;
    }

    [Serializable]
    public sealed class VillageFencePath
    {
        public string id = string.Empty;
        public Vector2Int[] cells = Array.Empty<Vector2Int>();
    }

    [Serializable]
    public sealed class VillageFoliagePlacement
    {
        public string id = string.Empty;
        public VillageFoliageKind kind;
        public Vector2Int cell;
        public int scale = 1;
    }

    [Serializable]
    public sealed class VillageNpcSpawnPoint
    {
        public string npcId = string.Empty;
        public Vector2Int cell;
        public Vector2Int facing;
    }

    public enum VillageFoliageKind
    {
        Tree,
        Shrub,
        Flower,
        Rock
    }

    public enum VillageCellKind
    {
        Empty,
        Road,
        Plaza,
        Building,
        DoorClosed,
        DoorOpen,
        Fence,
        Foliage,
        NpcSpawn
    }
}
