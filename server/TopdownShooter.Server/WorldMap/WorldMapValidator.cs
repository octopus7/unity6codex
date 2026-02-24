using TopdownShooter.Server.Domain;

namespace TopdownShooter.Server.WorldMap;

public static class WorldMapValidator
{
    public static bool TryValidate(WorldMapConfig? mapConfig, out List<string> errors)
    {
        errors = [];
        if (mapConfig is null)
        {
            errors.Add("Map config is null.");
            return false;
        }

        if (mapConfig.SchemaVersion != 1)
        {
            errors.Add($"Unsupported schemaVersion: {mapConfig.SchemaVersion}. Expected 1.");
        }

        if (string.IsNullOrWhiteSpace(mapConfig.MapId))
        {
            errors.Add("mapId is required.");
        }

        if (!IsBoundsWellFormed(mapConfig.BattleBounds))
        {
            errors.Add("battleBounds is invalid: min must be less than max for both axes.");
        }

        if (!IsBoundsWellFormed(mapConfig.ShopZone))
        {
            errors.Add("shopZone is invalid: min must be less than max for both axes.");
        }

        if (errors.Count > 0)
        {
            return false;
        }

        var obstacleBounds = ValidateObstacles(mapConfig, errors);
        ValidatePlayerSpawns(mapConfig, obstacleBounds, errors);
        ValidateCoinSpawners(mapConfig, obstacleBounds, errors);
        ValidatePortals(mapConfig, obstacleBounds, errors);

        return errors.Count == 0;
    }

    private static List<MapBounds> ValidateObstacles(WorldMapConfig mapConfig, List<string> errors)
    {
        var obstacleBounds = new List<MapBounds>(mapConfig.Obstacles.Count);
        var obstacleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < mapConfig.Obstacles.Count; index++)
        {
            var obstacle = mapConfig.Obstacles[index];
            var label = $"obstacles[{index}]";

            if (string.IsNullOrWhiteSpace(obstacle.ObstacleId))
            {
                errors.Add($"{label}.id is required.");
            }
            else if (!obstacleIds.Add(obstacle.ObstacleId))
            {
                errors.Add($"{label}.id '{obstacle.ObstacleId}' is duplicated.");
            }

            var bounds = obstacle.ToBounds();
            if (!IsBoundsWellFormed(bounds))
            {
                errors.Add($"{label} has invalid bounds.");
                continue;
            }

            if (!ContainsBounds(mapConfig.BattleBounds, bounds))
            {
                errors.Add($"{label} must be inside battleBounds.");
                continue;
            }

            obstacleBounds.Add(bounds);
        }

        return obstacleBounds;
    }

    private static void ValidatePlayerSpawns(WorldMapConfig mapConfig, IReadOnlyList<MapBounds> obstacleBounds, List<string> errors)
    {
        if (mapConfig.PlayerSpawns.Count == 0)
        {
            errors.Add("playerSpawns must contain at least one point.");
            return;
        }

        for (var index = 0; index < mapConfig.PlayerSpawns.Count; index++)
        {
            var spawn = mapConfig.PlayerSpawns[index];
            var label = $"playerSpawns[{index}]";

            if (!mapConfig.BattleBounds.Contains(spawn))
            {
                errors.Add($"{label} must be inside battleBounds.");
                continue;
            }

            if (mapConfig.ShopZone.Contains(spawn))
            {
                errors.Add($"{label} must not be inside shopZone.");
            }

            if (IsInsideObstacle(spawn, obstacleBounds))
            {
                errors.Add($"{label} must not be inside an obstacle.");
            }
        }
    }

    private static void ValidateCoinSpawners(WorldMapConfig mapConfig, IReadOnlyList<MapBounds> obstacleBounds, List<string> errors)
    {
        if (mapConfig.CoinSpawners.Count == 0)
        {
            errors.Add("coinSpawners must contain at least one entry.");
            return;
        }

        var spawnerIds = new HashSet<int>();
        for (var index = 0; index < mapConfig.CoinSpawners.Count; index++)
        {
            var spawner = mapConfig.CoinSpawners[index];
            var label = $"coinSpawners[{index}]";

            if (spawner.SpawnerId <= 0)
            {
                errors.Add($"{label}.id must be positive.");
            }
            else if (!spawnerIds.Add(spawner.SpawnerId))
            {
                errors.Add($"{label}.id '{spawner.SpawnerId}' is duplicated.");
            }

            if (spawner.IntervalTicks <= 0)
            {
                errors.Add($"{label}.intervalTicks must be positive.");
            }

            if (spawner.SpawnAmount <= 0)
            {
                errors.Add($"{label}.spawnAmount must be positive.");
            }

            if (!mapConfig.BattleBounds.Contains(spawner.Position))
            {
                errors.Add($"{label}.position must be inside battleBounds.");
                continue;
            }

            if (mapConfig.ShopZone.Contains(spawner.Position))
            {
                errors.Add($"{label}.position must not be inside shopZone.");
            }

            if (IsInsideObstacle(spawner.Position, obstacleBounds))
            {
                errors.Add($"{label}.position must not be inside an obstacle.");
            }
        }

        // TODO(gem-branch): Add gem spawn validation once gem schema lands.
    }

    private static void ValidatePortals(WorldMapConfig mapConfig, IReadOnlyList<MapBounds> obstacleBounds, List<string> errors)
    {
        if (mapConfig.Portals.Count == 0)
        {
            errors.Add("portals must contain at least one entry.");
            return;
        }

        var portalIds = new HashSet<int>();
        var entryCount = 0;
        var exitCount = 0;

        for (var index = 0; index < mapConfig.Portals.Count; index++)
        {
            var portal = mapConfig.Portals[index];
            var label = $"portals[{index}]";

            if (portal.PortalId <= 0 || portal.PortalId > byte.MaxValue)
            {
                errors.Add($"{label}.id must be in range 1..{byte.MaxValue}.");
            }
            else if (!portalIds.Add(portal.PortalId))
            {
                errors.Add($"{label}.id '{portal.PortalId}' is duplicated.");
            }

            if (portal.Radius <= 0f)
            {
                errors.Add($"{label}.radius must be positive.");
            }

            if (IsInsideObstacle(portal.Position, obstacleBounds))
            {
                errors.Add($"{label}.position must not be inside an obstacle.");
            }

            switch (portal.PortalType)
            {
                case PortalType.Entry:
                    entryCount++;
                    if (!mapConfig.BattleBounds.Contains(portal.Position) || mapConfig.ShopZone.Contains(portal.Position))
                    {
                        errors.Add($"{label}.position for Entry must be inside battleBounds and outside shopZone.");
                    }

                    if (!mapConfig.ShopZone.Contains(portal.Target))
                    {
                        errors.Add($"{label}.target for Entry must be inside shopZone.");
                    }

                    break;

                case PortalType.Exit:
                    exitCount++;
                    if (!mapConfig.ShopZone.Contains(portal.Position))
                    {
                        errors.Add($"{label}.position for Exit must be inside shopZone.");
                    }

                    if (!mapConfig.BattleBounds.Contains(portal.Target) || mapConfig.ShopZone.Contains(portal.Target))
                    {
                        errors.Add($"{label}.target for Exit must be inside battleBounds and outside shopZone.");
                    }

                    break;

                default:
                    errors.Add($"{label}.type '{portal.PortalType}' is unsupported.");
                    break;
            }
        }

        if (entryCount == 0)
        {
            errors.Add("portals must include at least one Entry portal.");
        }

        if (exitCount == 0)
        {
            errors.Add("portals must include at least one Exit portal.");
        }
    }

    private static bool IsBoundsWellFormed(MapBounds bounds)
    {
        return bounds.MinX < bounds.MaxX && bounds.MinY < bounds.MaxY;
    }

    private static bool ContainsBounds(MapBounds outer, MapBounds inner)
    {
        return inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX &&
               inner.MinY >= outer.MinY && inner.MaxY <= outer.MaxY;
    }

    private static bool IsInsideObstacle(MapPoint point, IReadOnlyList<MapBounds> obstacleBounds)
    {
        for (var index = 0; index < obstacleBounds.Count; index++)
        {
            if (obstacleBounds[index].Contains(point))
            {
                return true;
            }
        }

        return false;
    }
}
