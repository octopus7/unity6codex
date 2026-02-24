using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodexSix.TopdownShooter.Game
{
    public enum TopDownMapPortalType : byte
    {
        Entry = 1,
        Exit = 2
    }

    [Serializable]
    public struct TopDownMapBounds
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;

        public TopDownMapBounds(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);
        public Vector2 Size => new(Mathf.Max(0f, MaxX - MinX), Mathf.Max(0f, MaxY - MinY));

        public bool Contains(Vector2 point)
        {
            return point.x >= MinX && point.x <= MaxX &&
                   point.y >= MinY && point.y <= MaxY;
        }
    }

    [Serializable]
    public struct TopDownMapObstacle
    {
        public string ObstacleId;
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;

        public TopDownMapBounds Bounds => new(MinX, MinY, MaxX, MaxY);
    }

    [Serializable]
    public struct TopDownMapCoinSpawner
    {
        public int SpawnerId;
        public Vector2 Position;
        public int IntervalTicks;
        public int SpawnAmount;
    }

    [Serializable]
    public struct TopDownMapPortal
    {
        public int PortalId;
        public TopDownMapPortalType PortalType;
        public Vector2 Position;
        public float Radius;
        public Vector2 Target;
    }

    [CreateAssetMenu(menuName = "TopDownShooter/Map Authoring Asset", fileName = "MainMap")]
    public sealed class TopDownMapAuthoringAsset : ScriptableObject
    {
        public int SchemaVersion = 1;
        public string MapId = "main";

        public TopDownMapBounds BattleBounds = new(-20f, -20f, 20f, 20f);
        public TopDownMapBounds ShopZone = new(-6f, 22f, 6f, 34f);

        public List<TopDownMapObstacle> Obstacles = new();
        public List<Vector2> PlayerSpawns = new();
        public List<TopDownMapCoinSpawner> CoinSpawners = new();
        public List<TopDownMapPortal> Portals = new();

        // TODO(gem-branch): Add gem spawn authoring data once gem branch is merged.

        public static TopDownMapAuthoringAsset CreateDefaultInMemory()
        {
            var asset = CreateInstance<TopDownMapAuthoringAsset>();
            asset.ResetToDefaults();
            return asset;
        }

        public void ResetToDefaults()
        {
            SchemaVersion = 1;
            MapId = "main";
            BattleBounds = new TopDownMapBounds(-20f, -20f, 20f, 20f);
            ShopZone = new TopDownMapBounds(-6f, 22f, 6f, 34f);

            Obstacles = new List<TopDownMapObstacle>
            {
                new() { ObstacleId = "Obstacle_Center_SW", MinX = -2f, MinY = -2f, MaxX = -0.6f, MaxY = -0.6f },
                new() { ObstacleId = "Obstacle_Center_SE", MinX = 0.6f, MinY = -2f, MaxX = 2f, MaxY = -0.6f },
                new() { ObstacleId = "Obstacle_Center_NW", MinX = -2f, MinY = 0.6f, MaxX = -0.6f, MaxY = 2f },
                new() { ObstacleId = "Obstacle_Center_NE", MinX = 0.6f, MinY = 0.6f, MaxX = 2f, MaxY = 2f },
                new() { ObstacleId = "Obstacle_East", MinX = 8.5f, MinY = -1f, MaxX = 11.5f, MaxY = 1f },
                new() { ObstacleId = "Obstacle_West", MinX = -11.5f, MinY = -1f, MaxX = -8.5f, MaxY = 1f },
                new() { ObstacleId = "Obstacle_North", MinX = -1f, MinY = 8.5f, MaxX = 1f, MaxY = 11.5f },
                new() { ObstacleId = "Obstacle_South", MinX = -1f, MinY = -11.5f, MaxX = 1f, MaxY = -8.5f }
            };

            PlayerSpawns = new List<Vector2>
            {
                new(-16f, -16f),
                new(-16f, 16f),
                new(16f, -16f),
                new(16f, 16f),
                new(0f, -16f),
                new(0f, 16f),
                new(-16f, 0f),
                new(16f, 0f)
            };

            CoinSpawners = new List<TopDownMapCoinSpawner>
            {
                new()
                {
                    SpawnerId = 1,
                    Position = Vector2.zero,
                    IntervalTicks = 150,
                    SpawnAmount = 1
                }
            };

            Portals = new List<TopDownMapPortal>
            {
                new()
                {
                    PortalId = 1,
                    PortalType = TopDownMapPortalType.Entry,
                    Position = new Vector2(-18f, 0f),
                    Radius = 1.2f,
                    Target = new Vector2(0f, 28f)
                },
                new()
                {
                    PortalId = 2,
                    PortalType = TopDownMapPortalType.Entry,
                    Position = new Vector2(18f, 0f),
                    Radius = 1.2f,
                    Target = new Vector2(0f, 28f)
                },
                new()
                {
                    PortalId = 3,
                    PortalType = TopDownMapPortalType.Exit,
                    Position = new Vector2(0f, 23f),
                    Radius = 1.2f,
                    Target = new Vector2(0f, 16f)
                }
            };
        }

        private void OnValidate()
        {
            Obstacles ??= new List<TopDownMapObstacle>();
            PlayerSpawns ??= new List<Vector2>();
            CoinSpawners ??= new List<TopDownMapCoinSpawner>();
            Portals ??= new List<TopDownMapPortal>();
        }
    }
}
