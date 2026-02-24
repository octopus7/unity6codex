using System;
using System.Collections.Generic;
using CodexSix.TopdownShooter.Game;
using UnityEngine;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class TopDownMapAuthoringValidator
    {
        public static bool TryValidate(TopDownMapAuthoringAsset mapAsset, out List<string> errors)
        {
            errors = new List<string>();
            if (mapAsset == null)
            {
                errors.Add("Map asset is null.");
                return false;
            }

            if (mapAsset.SchemaVersion != 1)
            {
                errors.Add($"Unsupported schemaVersion: {mapAsset.SchemaVersion}. Expected 1.");
            }

            if (string.IsNullOrWhiteSpace(mapAsset.MapId))
            {
                errors.Add("mapId is required.");
            }

            if (!IsBoundsWellFormed(mapAsset.BattleBounds))
            {
                errors.Add("battleBounds is invalid.");
            }

            if (!IsBoundsWellFormed(mapAsset.ShopZone))
            {
                errors.Add("shopZone is invalid.");
            }

            if (errors.Count > 0)
            {
                return false;
            }

            var obstacleBounds = ValidateObstacles(mapAsset, errors);
            ValidatePlayerSpawns(mapAsset, obstacleBounds, errors);
            ValidateCoinSpawners(mapAsset, obstacleBounds, errors);
            ValidatePortals(mapAsset, obstacleBounds, errors);

            return errors.Count == 0;
        }

        private static List<TopDownMapBounds> ValidateObstacles(TopDownMapAuthoringAsset mapAsset, List<string> errors)
        {
            var obstacleBounds = new List<TopDownMapBounds>(mapAsset.Obstacles.Count);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < mapAsset.Obstacles.Count; index++)
            {
                var obstacle = mapAsset.Obstacles[index];
                var label = $"obstacles[{index}]";

                if (string.IsNullOrWhiteSpace(obstacle.ObstacleId))
                {
                    errors.Add($"{label}.id is required.");
                }
                else if (!ids.Add(obstacle.ObstacleId))
                {
                    errors.Add($"{label}.id '{obstacle.ObstacleId}' is duplicated.");
                }

                var bounds = obstacle.Bounds;
                if (!IsBoundsWellFormed(bounds))
                {
                    errors.Add($"{label} bounds are invalid.");
                    continue;
                }

                if (!ContainsBounds(mapAsset.BattleBounds, bounds))
                {
                    errors.Add($"{label} must be inside battleBounds.");
                    continue;
                }

                obstacleBounds.Add(bounds);
            }

            return obstacleBounds;
        }

        private static void ValidatePlayerSpawns(TopDownMapAuthoringAsset mapAsset, IReadOnlyList<TopDownMapBounds> obstacleBounds, List<string> errors)
        {
            if (mapAsset.PlayerSpawns.Count == 0)
            {
                errors.Add("playerSpawns must contain at least one point.");
                return;
            }

            for (var index = 0; index < mapAsset.PlayerSpawns.Count; index++)
            {
                var spawn = mapAsset.PlayerSpawns[index];
                var label = $"playerSpawns[{index}]";

                if (!mapAsset.BattleBounds.Contains(spawn))
                {
                    errors.Add($"{label} must be inside battleBounds.");
                    continue;
                }

                if (mapAsset.ShopZone.Contains(spawn))
                {
                    errors.Add($"{label} must not be inside shopZone.");
                }

                if (IsInsideObstacle(spawn, obstacleBounds))
                {
                    errors.Add($"{label} must not be inside an obstacle.");
                }
            }
        }

        private static void ValidateCoinSpawners(TopDownMapAuthoringAsset mapAsset, IReadOnlyList<TopDownMapBounds> obstacleBounds, List<string> errors)
        {
            if (mapAsset.CoinSpawners.Count == 0)
            {
                errors.Add("coinSpawners must contain at least one entry.");
                return;
            }

            var ids = new HashSet<int>();
            for (var index = 0; index < mapAsset.CoinSpawners.Count; index++)
            {
                var spawner = mapAsset.CoinSpawners[index];
                var label = $"coinSpawners[{index}]";

                if (spawner.SpawnerId <= 0)
                {
                    errors.Add($"{label}.id must be positive.");
                }
                else if (!ids.Add(spawner.SpawnerId))
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

                if (!mapAsset.BattleBounds.Contains(spawner.Position))
                {
                    errors.Add($"{label}.position must be inside battleBounds.");
                    continue;
                }

                if (mapAsset.ShopZone.Contains(spawner.Position))
                {
                    errors.Add($"{label}.position must not be inside shopZone.");
                }

                if (IsInsideObstacle(spawner.Position, obstacleBounds))
                {
                    errors.Add($"{label}.position must not be inside an obstacle.");
                }
            }

            // TODO(gem-branch): Add gem spawn validation when gem schema lands.
        }

        private static void ValidatePortals(TopDownMapAuthoringAsset mapAsset, IReadOnlyList<TopDownMapBounds> obstacleBounds, List<string> errors)
        {
            if (mapAsset.Portals.Count == 0)
            {
                errors.Add("portals must contain at least one entry.");
                return;
            }

            var ids = new HashSet<int>();
            var entryCount = 0;
            var exitCount = 0;

            for (var index = 0; index < mapAsset.Portals.Count; index++)
            {
                var portal = mapAsset.Portals[index];
                var label = $"portals[{index}]";

                if (portal.PortalId <= 0 || portal.PortalId > byte.MaxValue)
                {
                    errors.Add($"{label}.id must be in range 1..{byte.MaxValue}.");
                }
                else if (!ids.Add(portal.PortalId))
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
                    case TopDownMapPortalType.Entry:
                        entryCount++;
                        if (!mapAsset.BattleBounds.Contains(portal.Position) || mapAsset.ShopZone.Contains(portal.Position))
                        {
                            errors.Add($"{label}.position for Entry must be in battle and outside shop.");
                        }

                        if (!mapAsset.ShopZone.Contains(portal.Target))
                        {
                            errors.Add($"{label}.target for Entry must be inside shop.");
                        }

                        break;

                    case TopDownMapPortalType.Exit:
                        exitCount++;
                        if (!mapAsset.ShopZone.Contains(portal.Position))
                        {
                            errors.Add($"{label}.position for Exit must be inside shop.");
                        }

                        if (!mapAsset.BattleBounds.Contains(portal.Target) || mapAsset.ShopZone.Contains(portal.Target))
                        {
                            errors.Add($"{label}.target for Exit must be in battle and outside shop.");
                        }

                        break;

                    default:
                        errors.Add($"{label}.type is unsupported.");
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

        private static bool IsBoundsWellFormed(TopDownMapBounds bounds)
        {
            return bounds.MinX < bounds.MaxX && bounds.MinY < bounds.MaxY;
        }

        private static bool ContainsBounds(TopDownMapBounds outer, TopDownMapBounds inner)
        {
            return inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX &&
                   inner.MinY >= outer.MinY && inner.MaxY <= outer.MaxY;
        }

        private static bool IsInsideObstacle(Vector2 point, IReadOnlyList<TopDownMapBounds> obstacleBounds)
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
}
