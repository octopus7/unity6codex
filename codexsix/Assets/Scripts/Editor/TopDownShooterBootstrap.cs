using System.Collections.Generic;
using CodexSix.TopdownShooter.Game;
using CodexSix.TopdownShooter.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class TopDownShooterBootstrap
    {
        private const string ScenePath = "Assets/Scenes/MainScene.unity";

        [MenuItem("Tools/TopDownShooter/Bootstrap MVP Scene")]
        public static void BootstrapScene()
        {
            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildEnvironment();
            var camera = BuildCamera();
            var client = BuildRuntime(camera);
            BuildDebugHud(client);
            AddSpawnPoints();
            SaveAndRegisterScene(scene);

            Debug.Log("Bootstrap complete: Assets/Scenes/MainScene.unity");
        }

        [MenuItem("Tools/TopDownShooter/Add Lighting To Current Scene")]
        public static void AddLightingToCurrentScene()
        {
            BuildLighting();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("TopDownShooter lighting added to current scene.");
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
        }

        private static void BuildEnvironment()
        {
            var environmentRoot = new GameObject("Environment");

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

        private static void BuildLighting()
        {
            var existing = Object.FindObjectOfType<Light>();
            if (existing != null && existing.type == LightType.Directional)
            {
                RenderSettings.sun = existing;
            }
            else
            {
                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.97f, 0.9f);
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;
                lightObject.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
                RenderSettings.sun = light;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.42f, 0.47f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.26f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.12f);
        }

        private static void BuildShopArea(Transform parent)
        {
            var zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.name = "ShopZone";
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(0f, 0.1f, 28f);
            zone.transform.localScale = new Vector3(12f, 0.2f, 12f);
            var zoneRenderer = zone.GetComponent<Renderer>();
            zoneRenderer.sharedMaterial.color = new Color(0.16f, 0.35f, 0.19f);
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
            Object.DestroyImmediate(portal.GetComponent<CapsuleCollider>());
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

        private static Camera BuildCamera()
        {
            var cameraObject = new GameObject("Main Camera");
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

        private static NetworkGameClient BuildRuntime(Camera camera)
        {
            var runtimeRoot = new GameObject("GameRuntime");
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

            var follow = camera.gameObject.AddComponent<TopDownCameraFollow>();
            follow.Client = client;
            follow.Offset = new Vector3(0f, 18f, -10f);
            follow.FixedEuler = new Vector3(60f, 0f, 0f);

            return client;
        }

        private static void BuildDebugHud(NetworkGameClient client)
        {
            var hudObject = new GameObject("DebugHud");
            var hud = hudObject.AddComponent<SimpleDebugHud>();
            hud.Client = client;
            hud.HealItemId = 1;
            hud.SpeedItemId = 2;
        }

        private static void AddSpawnPoints()
        {
            var root = new GameObject("SpawnPoints");
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
