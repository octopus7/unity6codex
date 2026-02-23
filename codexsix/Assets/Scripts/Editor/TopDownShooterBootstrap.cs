using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodexSix.TopdownShooter.Game;
using CodexSix.TopdownShooter.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class TopDownShooterBootstrap
    {
        private const string ScenePath = "Assets/Scenes/MainScene.unity";
        private const string BackupFolderPath = "Assets/Scenes/Backups";
        private const string GeneratedRootName = "__BootstrapGenerated";

        [MenuItem("Tools/TopDownShooter/Bootstrap MVP Scene (Safe)")]
        public static void BootstrapSceneSafe()
        {
            BootstrapScene(destructiveReset: false);
        }

        [MenuItem("Tools/TopDownShooter/Bootstrap MVP Scene (Destructive Reset)")]
        public static void BootstrapSceneDestructive()
        {
            if (File.Exists(ToAbsoluteProjectPath(ScenePath)))
            {
                EditorUtility.DisplayDialog(
                    "Destructive Reset Blocked",
                    "Policy: Destructive Reset is only allowed for initial scene creation.\n" +
                    "Use 'Bootstrap MVP Scene (Safe)' for normal updates.",
                    "OK");
                return;
            }

            var confirm = EditorUtility.DisplayDialog(
                "Destructive Reset",
                "This will rebuild MainScene and can overwrite scene-level setup.\n" +
                "A backup scene file will be created first.\n\nContinue?",
                "Continue",
                "Cancel");

            if (!confirm)
            {
                return;
            }

            BootstrapScene(destructiveReset: true);
        }

        [MenuItem("Tools/TopDownShooter/Add Lighting To Current Scene")]
        public static void AddLightingToCurrentScene()
        {
            EnsureDefaultLighting();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("TopDownShooter lighting applied to current scene.");
        }

        private static void BootstrapScene(bool destructiveReset)
        {
            EnsureFolders();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var sceneExists = File.Exists(ToAbsoluteProjectPath(ScenePath));
            Scene scene;

            if (!sceneExists)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else if (destructiveReset)
            {
                BackupSceneAsset();
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var generatedRoot = RecreateGeneratedRoot();

            EnsureDefaultLighting(parentForNewLight: generatedRoot.transform, preferExternalDirectionalLight: true);
            BuildEnvironment(generatedRoot.transform);
            var camera = BuildCamera(generatedRoot.transform, preferExternalCamera: true);
            var client = BuildRuntime(generatedRoot.transform, camera);
            BuildDebugHud(generatedRoot.transform, client);
            AddSpawnPoints(generatedRoot.transform);

            SaveAndRegisterScene(scene);
            Debug.Log(
                $"Bootstrap complete ({(destructiveReset ? "destructive reset" : "safe rebuild of generated root")}): {ScenePath}");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            {
                AssetDatabase.CreateFolder("Assets", "Scripts");
            }

            if (!AssetDatabase.IsValidFolder(BackupFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Scenes", "Backups");
            }
        }

        private static void BackupSceneAsset()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = $"{BackupFolderPath}/MainScene_backup_{timestamp}.unity";

            if (!File.Exists(ToAbsoluteProjectPath(ScenePath)))
            {
                return;
            }

            if (AssetDatabase.CopyAsset(ScenePath, backupPath))
            {
                Debug.Log($"Created scene backup: {backupPath}");
            }
            else
            {
                Debug.LogWarning("Failed to create scene backup before destructive reset.");
            }
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static GameObject RecreateGeneratedRoot()
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var rootObject in activeScene.GetRootGameObjects())
            {
                if (rootObject.name != GeneratedRootName)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(rootObject);
                break;
            }

            return new GameObject(GeneratedRootName);
        }

        private static void EnsureDefaultLighting(Transform parentForNewLight = null, bool preferExternalDirectionalLight = false)
        {
            Light directional = null;

            if (preferExternalDirectionalLight)
            {
                directional = FindExternalDirectionalLight();
            }

            if (directional == null)
            {
                directional = FindAnyDirectionalLight();
            }

            if (directional == null)
            {
                var lightObject = new GameObject("Directional Light");
                if (parentForNewLight != null)
                {
                    lightObject.transform.SetParent(parentForNewLight, worldPositionStays: false);
                }

                directional = lightObject.AddComponent<Light>();
                directional.type = LightType.Directional;
                directional.color = new Color(1f, 0.97f, 0.9f);
                directional.intensity = 1.15f;
                directional.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            }

            RenderSettings.sun = directional;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.42f, 0.47f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.26f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.12f);
        }

        private static Light FindExternalDirectionalLight()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type != LightType.Directional)
                {
                    continue;
                }

                if (IsUnderGeneratedRoot(light.transform))
                {
                    continue;
                }

                return light;
            }

            return null;
        }

        private static Light FindAnyDirectionalLight()
        {
            return UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(light => light.type == LightType.Directional);
        }

        private static bool IsUnderGeneratedRoot(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name == GeneratedRootName)
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static void BuildEnvironment(Transform parent)
        {
            var environmentRoot = new GameObject("Environment");
            environmentRoot.transform.SetParent(parent, worldPositionStays: false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(environmentRoot.transform, false);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            ground.transform.position = Vector3.zero;
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.23f, 0.26f, 0.24f);

            AddBoundaryWall(environmentRoot.transform, "Wall_North", new Vector3(0f, 1f, 20.5f), new Vector3(42f, 2f, 1f));
            AddBoundaryWall(environmentRoot.transform, "Wall_South", new Vector3(0f, 1f, -20.5f), new Vector3(42f, 2f, 1f));
            AddBoundaryWall(environmentRoot.transform, "Wall_East", new Vector3(20.5f, 1f, 0f), new Vector3(1f, 2f, 42f));
            AddBoundaryWall(environmentRoot.transform, "Wall_West", new Vector3(-20.5f, 1f, 0f), new Vector3(1f, 2f, 42f));

            AddObstacle(environmentRoot.transform, "Obstacle_Center", new Vector3(0f, 0.75f, 0f), new Vector3(4f, 1.5f, 4f));
            AddObstacle(environmentRoot.transform, "Obstacle_East", new Vector3(10f, 0.75f, 0f), new Vector3(3f, 1.5f, 2f));
            AddObstacle(environmentRoot.transform, "Obstacle_West", new Vector3(-10f, 0.75f, 0f), new Vector3(3f, 1.5f, 2f));
            AddObstacle(environmentRoot.transform, "Obstacle_North", new Vector3(0f, 0.75f, 10f), new Vector3(2f, 1.5f, 3f));
            AddObstacle(environmentRoot.transform, "Obstacle_South", new Vector3(0f, 0.75f, -10f), new Vector3(2f, 1.5f, 3f));

            BuildShopArea(environmentRoot.transform);
            BuildPortals(environmentRoot.transform);
        }

        private static void BuildShopArea(Transform parent)
        {
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = "ShopZone";
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(0f, 0.1f, 28f);
            zone.transform.localScale = new Vector3(12f, 0.2f, 12f);
            zone.GetComponent<Renderer>().sharedMaterial.color = new Color(0.16f, 0.35f, 0.19f);
            var box = zone.GetComponent<BoxCollider>();
            box.isTrigger = true;
            zone.AddComponent<ShopZoneMarker>().Size = new Vector2(12f, 12f);

            var healPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healPad.name = "ShopItem_Heal50";
            healPad.transform.SetParent(parent, false);
            healPad.transform.position = new Vector3(-2f, 0.5f, 28f);
            healPad.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            healPad.GetComponent<Renderer>().sharedMaterial.color = new Color(0.8f, 0.25f, 0.25f);
            var healMarker = healPad.AddComponent<ShopItemMarker>();
            healMarker.ItemId = 1;
            healMarker.Cost = 5;
            healMarker.ItemName = "Heal50";

            var speedPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            speedPad.name = "ShopItem_Speed20";
            speedPad.transform.SetParent(parent, false);
            speedPad.transform.position = new Vector3(2f, 0.5f, 28f);
            speedPad.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            speedPad.GetComponent<Renderer>().sharedMaterial.color = new Color(0.25f, 0.45f, 0.9f);
            var speedMarker = speedPad.AddComponent<ShopItemMarker>();
            speedMarker.ItemId = 2;
            speedMarker.Cost = 8;
            speedMarker.ItemName = "MoveSpeed+20%";
        }

        private static void BuildPortals(Transform parent)
        {
            AddPortal(parent, "Portal_Entry_Left", new Vector3(-18f, 0.1f, 0f), 1, PortalType.Entry, new Color(0.1f, 0.6f, 1f));
            AddPortal(parent, "Portal_Entry_Right", new Vector3(18f, 0.1f, 0f), 2, PortalType.Entry, new Color(0.1f, 0.6f, 1f));
            AddPortal(parent, "Portal_Exit_Shop", new Vector3(0f, 0.1f, 23f), 3, PortalType.Exit, new Color(1f, 0.55f, 0.1f));
        }

        private static void AddPortal(Transform parent, string name, Vector3 position, byte portalId, PortalType portalType, Color color)
        {
            var portal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            portal.name = name;
            portal.transform.SetParent(parent, false);
            portal.transform.position = position;
            portal.transform.localScale = new Vector3(1.2f, 0.1f, 1.2f);
            portal.GetComponent<Renderer>().sharedMaterial.color = color;
            UnityEngine.Object.DestroyImmediate(portal.GetComponent<CapsuleCollider>());
            var collider = portal.AddComponent<SphereCollider>();
            collider.radius = 1.2f;
            collider.isTrigger = true;

            var marker = portal.AddComponent<PortalMarker>();
            marker.PortalId = portalId;
            marker.PortalType = portalType;
        }

        private static void AddBoundaryWall(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.27f, 0.27f, 0.29f);
        }

        private static void AddObstacle(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
            obstacle.GetComponent<Renderer>().sharedMaterial.color = new Color(0.36f, 0.37f, 0.4f);
        }

        private static Camera BuildCamera(Transform parent, bool preferExternalCamera)
        {
            if (preferExternalCamera)
            {
                var externalMainCamera = FindExternalMainCamera();
                if (externalMainCamera != null)
                {
                    return externalMainCamera;
                }
            }

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.11f, 0.11f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 60f;

            cameraObject.transform.position = new Vector3(0f, 18f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            return camera;
        }

        private static Camera FindExternalMainCamera()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (IsUnderGeneratedRoot(camera.transform))
                {
                    continue;
                }

                if (camera.CompareTag("MainCamera"))
                {
                    return camera;
                }
            }

            return null;
        }

        private static NetworkGameClient BuildRuntime(Transform parent, Camera camera)
        {
            var runtimeRoot = new GameObject("GameRuntime");
            runtimeRoot.transform.SetParent(parent, false);

            var transport = runtimeRoot.AddComponent<TcpGameTransport>();
            var client = runtimeRoot.AddComponent<NetworkGameClient>();
            var inputSender = runtimeRoot.AddComponent<LocalInputSender>();

            var playerContainer = new GameObject("Players").transform;
            playerContainer.SetParent(runtimeRoot.transform, false);
            var projectileContainer = new GameObject("Projectiles").transform;
            projectileContainer.SetParent(runtimeRoot.transform, false);
            var coinContainer = new GameObject("CoinStacks").transform;
            coinContainer.SetParent(runtimeRoot.transform, false);

            client.Transport = transport;
            client.PlayerContainer = playerContainer;
            client.ProjectileContainer = projectileContainer;
            client.CoinContainer = coinContainer;

            inputSender.Client = client;
            inputSender.WorldCamera = camera;
            inputSender.SendRateHz = 30f;

            var follow = camera.GetComponent<TopDownCameraFollow>();
            if (follow == null)
            {
                follow = camera.gameObject.AddComponent<TopDownCameraFollow>();
            }

            follow.Client = client;
            follow.Offset = new Vector3(0f, 18f, -10f);
            follow.FixedEuler = new Vector3(60f, 0f, 0f);

            return client;
        }

        private static void BuildDebugHud(Transform parent, NetworkGameClient client)
        {
            var hudObject = new GameObject("DebugHud");
            hudObject.transform.SetParent(parent, false);

            var hud = hudObject.AddComponent<SimpleDebugHud>();
            hud.Client = client;
            hud.HealItemId = 1;
            hud.SpeedItemId = 2;
            hud.UiScale = 2f;
        }

        private static void AddSpawnPoints(Transform parent)
        {
            var root = new GameObject("SpawnPoints");
            root.transform.SetParent(parent, false);

            var points = new[]
            {
                new Vector3(-16f, 0f, -16f),
                new Vector3(-16f, 0f, 16f),
                new Vector3(16f, 0f, -16f),
                new Vector3(16f, 0f, 16f),
                new Vector3(0f, 0f, -16f),
                new Vector3(0f, 0f, 16f),
                new Vector3(-16f, 0f, 0f),
                new Vector3(16f, 0f, 0f)
            };

            for (var i = 0; i < points.Length; i++)
            {
                var spawn = new GameObject($"Spawn_{i + 1}");
                spawn.transform.SetParent(root.transform, false);
                spawn.transform.position = points[i];
            }
        }

        private static void SaveAndRegisterScene(Scene scene)
        {
            EditorSceneManager.SaveScene(scene, ScenePath, saveAsCopy: false);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(existing => existing.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, enabled: true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
