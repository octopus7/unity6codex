using System.IO;
using System.Reflection;
using CodexSix.TerrainMeshMovementLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexSix.TerrainMeshMovementLab.Editor
{
    public static class TerrainLabMenu
    {
        private const string ScenePath = "Assets/Labs/TerrainMeshMovementLab/Scenes/TerrainMeshMovementLab.unity";
        private const string ReadmePath = "Assets/Labs/TerrainMeshMovementLab/README.md";
        private const string ConfigAssetPath = "Assets/Labs/TerrainMeshMovementLab/TerrainLabWorldConfig.asset";
        private const string InputActionsPath = "Assets/Labs/TerrainMeshMovementLab/Runtime/TerrainLabInputActions.inputactions";

        [MenuItem("Tools/Terrain Lab/Create Terrain Movement Scene (Safe)")]
        public static void CreateTerrainMovementSceneSafe()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureSceneDirectory();

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EnsureLabObjectsInOpenScene();
                Debug.Log("🟢 Safe Bootstrap: Terrain Lab scene opened and verified.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureLabObjectsInOpenScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("🟢 Safe Bootstrap: Terrain Lab scene created (non-destructive).");
        }

        [MenuItem("Tools/Terrain Lab/Height Map Viewer")]
        public static void OpenViewerWindow()
        {
            TerrainLabMapViewerWindow.Open();
        }

        [MenuItem("Tools/Terrain Lab/Open README")]
        public static void OpenReadme()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(ReadmePath);
            if (asset == null)
            {
                Debug.LogWarning("Terrain Lab: README asset not found.");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        internal static TerrainLabWorldConfig LoadOrCreateWorldConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLabWorldConfig>(ConfigAssetPath);
            if (existing != null)
            {
                existing.ValidateInPlace();
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<TerrainLabWorldConfig>();
            asset.ValidateInPlace();
            AssetDatabase.CreateAsset(asset, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        internal static Object LoadInputActionsAsset()
        {
            return AssetDatabase.LoadAssetAtPath<Object>(InputActionsPath);
        }

        private static void EnsureLabObjectsInOpenScene()
        {
            var config = LoadOrCreateWorldConfig();
            var inputActions = LoadInputActionsAsset();

            var root = GameObject.Find("__TerrainLabGenerated");
            if (root == null)
            {
                root = new GameObject("__TerrainLabGenerated");
            }

            var terrain = FindOrCreateChild(root.transform, "Terrain");
            var meshGenerator = EnsureComponent<TerrainLabChunkMeshGenerator>(terrain.gameObject);
            meshGenerator.WorldConfig = config;

            var player = FindOrCreateChild(root.transform, "Player");
            player.position = new Vector3(0f, 8f, 0f);
            var capsule = EnsureComponent<CharacterController>(player.gameObject);
            capsule.radius = 0.45f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            var playerController = EnsureComponent<TerrainLabCharacterController>(player.gameObject);
            SetOptionalInputActionsAsset(playerController, inputActions);

            var worldRoot = FindOrCreateChild(root.transform, "World");
            var world = EnsureComponent<TerrainLabSingleChunkWorld>(worldRoot.gameObject);
            world.WorldConfig = config;
            world.FixedSeed = config.DefaultSeed;
            world.GeneratedGridSize = Mathf.Max(3, config.ViewerDefaultGridSize);
            if ((world.GeneratedGridSize & 1) == 0)
            {
                world.GeneratedGridSize += 1;
            }
            world.FollowPlayerChunk = true;
            world.ChunkCenterCheckIntervalSeconds = 0.2f;
            world.ChunkMeshGenerator = meshGenerator;
            world.PlayerRoot = player;
            world.PlayerCharacterController = capsule;

            var cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0f, 28f, -24f);
            }

            var cameraFollow = EnsureComponent<TerrainLabCameraFollow>(cameraObject);
            cameraFollow.Target = player;
            cameraFollow.Offset = new Vector3(0f, 28f, -24f);

            var minimapObject = FindOrCreateChild(root.transform, "Minimap");
            var minimap = EnsureComponent<TerrainLabMinimapController>(minimapObject.gameObject);
            minimap.World = world;
            minimap.PlayerTarget = player;
            world.MinimapController = minimap;

            var performance = EnsureComponent<TerrainLabPerformanceGraph>(world.gameObject);
            performance.ScreenRect = new Rect(16f, 270f, 320f, 130f);
            world.PerformanceGraph = performance;

            playerController.World = world;
            playerController.MovementCamera = cameraObject.transform;

            var light = GameObject.Find("Directional Light");
            if (light == null)
            {
                light = new GameObject("Directional Light", typeof(Light));
                var lightComponent = light.GetComponent<Light>();
                lightComponent.type = LightType.Directional;
                lightComponent.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            Selection.activeObject = world.gameObject;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static T EnsureComponent<T>(GameObject gameObject)
            where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return gameObject.AddComponent<T>();
        }

        private static void SetOptionalInputActionsAsset(TerrainLabCharacterController controller, Object inputActionsAsset)
        {
            if (controller == null || inputActionsAsset == null)
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = typeof(TerrainLabCharacterController).GetField("InputActions", flags);
            if (field == null)
            {
                return;
            }

            if (!field.FieldType.IsInstanceOfType(inputActionsAsset))
            {
                return;
            }

            field.SetValue(controller, inputActionsAsset);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureSceneDirectory()
        {
            var absoluteDirectory = Path.GetDirectoryName(Path.Combine(Directory.GetCurrentDirectory(), ScenePath));
            if (!string.IsNullOrWhiteSpace(absoluteDirectory))
            {
                Directory.CreateDirectory(absoluteDirectory);
            }
        }
    }
}
