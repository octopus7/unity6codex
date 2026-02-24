using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.WorldMap;

public static class WorldMapDefaults
{
    public static WorldMapConfig Create()
    {
        return new WorldMapConfig
        {
            SchemaVersion = 1,
            MapId = "main",
            BattleBounds = new MapBounds(-20f, -20f, 20f, 20f),
            ShopZone = new MapBounds(-6f, 22f, 6f, 34f),
            Obstacles =
            [
                new MapObstacle { ObstacleId = "Obstacle_Center_SW", MinX = -2f, MinY = -2f, MaxX = -0.6f, MaxY = -0.6f },
                new MapObstacle { ObstacleId = "Obstacle_Center_SE", MinX = 0.6f, MinY = -2f, MaxX = 2f, MaxY = -0.6f },
                new MapObstacle { ObstacleId = "Obstacle_Center_NW", MinX = -2f, MinY = 0.6f, MaxX = -0.6f, MaxY = 2f },
                new MapObstacle { ObstacleId = "Obstacle_Center_NE", MinX = 0.6f, MinY = 0.6f, MaxX = 2f, MaxY = 2f },
                new MapObstacle { ObstacleId = "Obstacle_East", MinX = 8.5f, MinY = -1f, MaxX = 11.5f, MaxY = 1f },
                new MapObstacle { ObstacleId = "Obstacle_West", MinX = -11.5f, MinY = -1f, MaxX = -8.5f, MaxY = 1f },
                new MapObstacle { ObstacleId = "Obstacle_North", MinX = -1f, MinY = 8.5f, MaxX = 1f, MaxY = 11.5f },
                new MapObstacle { ObstacleId = "Obstacle_South", MinX = -1f, MinY = -11.5f, MaxX = 1f, MaxY = -8.5f }
            ],
            PlayerSpawns =
            [
                new MapPoint(-16f, -16f),
                new MapPoint(-16f, 16f),
                new MapPoint(16f, -16f),
                new MapPoint(16f, 16f),
                new MapPoint(0f, -16f),
                new MapPoint(0f, 16f),
                new MapPoint(-16f, 0f),
                new MapPoint(16f, 0f)
            ],
            CoinSpawners =
            [
                new MapCoinSpawner
                {
                    SpawnerId = 1,
                    Position = new MapPoint(0f, 0f),
                    IntervalTicks = GameRules.CoinDispenserIntervalTicks,
                    SpawnAmount = GameRules.CoinDispenserSpawnAmount
                }
            ],
            Portals =
            [
                new MapPortal
                {
                    PortalId = 1,
                    PortalType = PortalType.Entry,
                    Position = new MapPoint(-18f, 0f),
                    Radius = 1.2f,
                    Target = new MapPoint(0f, 28f)
                },
                new MapPortal
                {
                    PortalId = 2,
                    PortalType = PortalType.Entry,
                    Position = new MapPoint(18f, 0f),
                    Radius = 1.2f,
                    Target = new MapPoint(0f, 28f)
                },
                new MapPortal
                {
                    PortalId = 3,
                    PortalType = PortalType.Exit,
                    Position = new MapPoint(0f, 23f),
                    Radius = 1.2f,
                    Target = new MapPoint(0f, 16f)
                }
            ]
        };
    }
}
