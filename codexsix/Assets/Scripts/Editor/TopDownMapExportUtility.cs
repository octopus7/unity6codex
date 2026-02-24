using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodexSix.TopdownShooter.Game;
using UnityEditor;
using UnityEngine;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class TopDownMapExportUtility
    {
        public const string DefaultMapAssetPath = "Assets/MapAuthoring/MainMap.asset";
        private const string ServerMapRelativePath = "server/TopdownShooter.Server/Content/Maps/main.map.json";

        [MenuItem("Tools/TopDownShooter/Export Map To Server JSON")]
        public static void ExportDefaultMapToServerJsonMenu()
        {
            var mapAsset = LoadOrCreateDefaultAsset();
            if (TryExportToServerJson(mapAsset, out var outputPath, out var errors))
            {
                Debug.Log($"Map exported: {outputPath}");
                return;
            }

            Debug.LogError("Map export failed:\n" + string.Join("\n", errors));
        }

        public static TopDownMapAuthoringAsset LoadOrCreateDefaultAsset()
        {
            var mapAsset = AssetDatabase.LoadAssetAtPath<TopDownMapAuthoringAsset>(DefaultMapAssetPath);
            if (mapAsset != null)
            {
                return mapAsset;
            }

            EnsureFolder("Assets", "MapAuthoring");
            mapAsset = TopDownMapAuthoringAsset.CreateDefaultInMemory();
            AssetDatabase.CreateAsset(mapAsset, DefaultMapAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created default map asset: {DefaultMapAssetPath}");
            return mapAsset;
        }

        public static bool TryExportToServerJson(TopDownMapAuthoringAsset mapAsset, out string outputPath, out List<string> errors)
        {
            errors = new List<string>();
            outputPath = ResolveServerMapPath();

            if (!TopDownMapAuthoringValidator.TryValidate(mapAsset, out var validationErrors))
            {
                errors.AddRange(validationErrors);
                return false;
            }

            var document = BuildDocument(mapAsset);
            var json = JsonUtility.ToJson(document, prettyPrint: true) + Environment.NewLine;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
                File.WriteAllText(outputPath, json);
                return true;
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to write map json: {exception.Message}");
                return false;
            }
        }

        private static MapDocument BuildDocument(TopDownMapAuthoringAsset mapAsset)
        {
            var document = new MapDocument
            {
                schemaVersion = mapAsset.SchemaVersion,
                mapId = mapAsset.MapId,
                battleBounds = new BoundsDocument(mapAsset.BattleBounds),
                shopZone = new BoundsDocument(mapAsset.ShopZone),
                obstacles = mapAsset.Obstacles
                    .OrderBy(obstacle => obstacle.ObstacleId, StringComparer.OrdinalIgnoreCase)
                    .Select(obstacle => new ObstacleDocument
                    {
                        id = obstacle.ObstacleId,
                        minX = obstacle.MinX,
                        minY = obstacle.MinY,
                        maxX = obstacle.MaxX,
                        maxY = obstacle.MaxY
                    })
                    .ToList(),
                playerSpawns = mapAsset.PlayerSpawns
                    .Select(point => new PointDocument(point))
                    .ToList(),
                coinSpawners = mapAsset.CoinSpawners
                    .OrderBy(spawner => spawner.SpawnerId)
                    .Select(spawner => new CoinSpawnerDocument
                    {
                        id = spawner.SpawnerId,
                        position = new PointDocument(spawner.Position),
                        intervalTicks = spawner.IntervalTicks,
                        spawnAmount = spawner.SpawnAmount
                    })
                    .ToList(),
                portals = mapAsset.Portals
                    .OrderBy(portal => portal.PortalId)
                    .Select(portal => new PortalDocument
                    {
                        id = portal.PortalId,
                        type = portal.PortalType.ToString(),
                        position = new PointDocument(portal.Position),
                        radius = portal.Radius,
                        target = new PointDocument(portal.Target)
                    })
                    .ToList()
            };

            // TODO(gem-branch): Include gem spawn export fields when gem branch is merged.
            return document;
        }

        private static string ResolveServerMapPath()
        {
            var unityProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var repositoryRoot = Path.GetFullPath(Path.Combine(unityProjectRoot, ".."));
            var relativePath = ServerMapRelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(repositoryRoot, relativePath);
        }

        private static void EnsureFolder(string parentPath, string folderName)
        {
            var targetPath = parentPath + "/" + folderName;
            if (AssetDatabase.IsValidFolder(targetPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        [Serializable]
        private sealed class MapDocument
        {
            public int schemaVersion;
            public string mapId = "main";
            public BoundsDocument battleBounds = new();
            public BoundsDocument shopZone = new();
            public List<ObstacleDocument> obstacles = new();
            public List<PointDocument> playerSpawns = new();
            public List<CoinSpawnerDocument> coinSpawners = new();
            public List<PortalDocument> portals = new();
        }

        [Serializable]
        private sealed class BoundsDocument
        {
            public float minX;
            public float minY;
            public float maxX;
            public float maxY;

            public BoundsDocument()
            {
            }

            public BoundsDocument(TopDownMapBounds bounds)
            {
                minX = bounds.MinX;
                minY = bounds.MinY;
                maxX = bounds.MaxX;
                maxY = bounds.MaxY;
            }
        }

        [Serializable]
        private sealed class ObstacleDocument
        {
            public string id = string.Empty;
            public float minX;
            public float minY;
            public float maxX;
            public float maxY;
        }

        [Serializable]
        private sealed class PointDocument
        {
            public float x;
            public float y;

            public PointDocument()
            {
            }

            public PointDocument(Vector2 point)
            {
                x = point.x;
                y = point.y;
            }
        }

        [Serializable]
        private sealed class CoinSpawnerDocument
        {
            public int id;
            public PointDocument position = new();
            public int intervalTicks;
            public int spawnAmount;
        }

        [Serializable]
        private sealed class PortalDocument
        {
            public int id;
            public string type = "Entry";
            public PointDocument position = new();
            public float radius;
            public PointDocument target = new();
        }
    }
}
