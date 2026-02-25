using System.IO;
using System;
using System.Collections;
using System.Reflection;
using CodexSix.TerrainMeshMovementLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CodexSix.TerrainMeshMovementLab.Editor
{
    public static class TerrainLabMenu
    {
        private const string ScenePath = "Assets/Labs/TerrainMeshMovementLab/Scenes/TerrainMeshMovementLab.unity";
        private const string ReadmePath = "Assets/Labs/TerrainMeshMovementLab/README.md";
        private const string ConfigAssetPath = "Assets/Labs/TerrainMeshMovementLab/TerrainLabWorldConfig.asset";
        private const string InputActionsPath = "Assets/Labs/TerrainMeshMovementLab/Runtime/TerrainLabInputActions.inputactions";
        private const string PostProcessProfilePath = "Assets/Labs/TerrainMeshMovementLab/Generated/PostProcessing/TerrainLabPostProcessProfile.asset";

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
            var mainCamera = cameraObject.GetComponent<Camera>();
            EnsurePostProcessingBloom(root.transform, cameraObject, mainCamera);

            var minimapObject = FindOrCreateChild(root.transform, "Minimap");
            var minimap = EnsureComponent<TerrainLabMinimapController>(minimapObject.gameObject);
            minimap.World = world;
            minimap.PlayerTarget = player;
            world.MinimapController = minimap;

            var waterObject = FindOrCreateChild(root.transform, "Water");
            var water = EnsureComponent<TerrainLabWaterController>(waterObject.gameObject);
            water.World = world;
            water.PlayerTarget = player;
            water.TargetCamera = mainCamera;
            world.WaterController = water;

            var debugCubeObject = FindOrCreateChild(root.transform, "HeightRangeDebugCube");
            RemoveColliderIfExists(debugCubeObject.gameObject);
            var debugCube = EnsureComponent<TerrainLabHeightRangeDebugCube>(debugCubeObject.gameObject);
            debugCube.WorldConfig = config;
            debugCube.PlayerTarget = player;
            debugCube.FollowPlayer = false;
            debugCube.CubeSizeMeters = 1f;
            debugCubeObject.localPosition = new Vector3(5f, 0f, 0f);
            world.HeightRangeDebugCube = debugCube;

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

        private static void RemoveColliderIfExists(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            var collider = gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            Object.DestroyImmediate(collider);
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

        private static void EnsurePostProcessingBloom(Transform root, GameObject cameraObject, Camera cameraComponent)
        {
            if (root == null || cameraObject == null || cameraComponent == null)
            {
                return;
            }

            cameraComponent.allowHDR = true;

            var postProcessLayerType = FindType("UnityEngine.Rendering.PostProcessing.PostProcessLayer");
            var postProcessVolumeType = FindType("UnityEngine.Rendering.PostProcessing.PostProcessVolume");
            var postProcessProfileType = FindType("UnityEngine.Rendering.PostProcessing.PostProcessProfile");
            var bloomType = FindType("UnityEngine.Rendering.PostProcessing.Bloom");

            if (postProcessLayerType == null
                || postProcessVolumeType == null
                || postProcessProfileType == null
                || bloomType == null)
            {
                Debug.LogWarning(
                    "Terrain Lab: Bloom setup skipped. Install package 'com.unity.postprocessing' to enable camera bloom in Built-in pipeline.");
                return;
            }

            var postProcessLayer = cameraObject.GetComponent(postProcessLayerType) ?? cameraObject.AddComponent(postProcessLayerType);
            SetFieldOrProperty(postProcessLayer, "volumeTrigger", cameraObject.transform);
            SetFieldOrProperty(postProcessLayer, "volumeLayer", new LayerMask { value = ~0 });
            SetEnumByName(postProcessLayer, "antialiasingMode", "None");
            SetFieldOrProperty(postProcessLayer, "enabled", true);

            EnsureAssetDirectoryForPath(PostProcessProfilePath);
            var profileAsset = AssetDatabase.LoadAssetAtPath(PostProcessProfilePath, postProcessProfileType);
            if (profileAsset == null)
            {
                var createdProfile = ScriptableObject.CreateInstance(postProcessProfileType);
                createdProfile.name = "TerrainLabPostProcessProfile";
                AssetDatabase.CreateAsset(createdProfile, PostProcessProfilePath);
                AssetDatabase.SaveAssets();
                profileAsset = createdProfile;
            }

            EnsureBloomSetting(profileAsset, postProcessProfileType, bloomType);

            var volumeRoot = FindOrCreateChild(root, "PostProcessing");
            var postProcessVolume = volumeRoot.gameObject.GetComponent(postProcessVolumeType)
                                    ?? volumeRoot.gameObject.AddComponent(postProcessVolumeType);
            SetFieldOrProperty(postProcessVolume, "isGlobal", true);
            SetFieldOrProperty(postProcessVolume, "priority", 0f);
            SetFieldOrProperty(postProcessVolume, "weight", 1f);
            SetFieldOrProperty(postProcessVolume, "sharedProfile", profileAsset);

            EditorUtility.SetDirty(cameraObject);
            EditorUtility.SetDirty(volumeRoot.gameObject);
            if (profileAsset != null)
            {
                EditorUtility.SetDirty(profileAsset);
            }
        }

        private static void EnsureBloomSetting(Object profileAsset, Type profileType, Type bloomType)
        {
            if (profileAsset == null || profileType == null || bloomType == null)
            {
                return;
            }

            var bloom = TryFindProfileSetting(profileAsset, profileType, bloomType);
            if (bloom == null)
            {
                var addSettings = FindAddSettingsMethod(profileType);
                if (addSettings != null)
                {
                    bloom = addSettings.MakeGenericMethod(bloomType).Invoke(profileAsset, null);
                }
            }

            if (bloom == null)
            {
                return;
            }

            SetParameterValue(bloom, "enabled", true);
            SetParameterValue(bloom, "intensity", 2.6f);
            SetParameterValue(bloom, "threshold", 1.05f);
            SetParameterValue(bloom, "softKnee", 0.55f);
            SetParameterValue(bloom, "diffusion", 5.5f);
            SetParameterValue(bloom, "fastMode", false);
        }

        private static MethodInfo FindAddSettingsMethod(Type profileType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var methods = profileType.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (!method.IsGenericMethodDefinition)
                {
                    continue;
                }

                if (!string.Equals(method.Name, "AddSettings", StringComparison.Ordinal))
                {
                    continue;
                }

                if (method.GetParameters().Length == 0)
                {
                    return method;
                }
            }

            return null;
        }

        private static object TryFindProfileSetting(object profileAsset, Type profileType, Type settingType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var settingsField = profileType.GetField("settings", flags);
            if (settingsField?.GetValue(profileAsset) is not IList list)
            {
                return null;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var candidate = list[i];
                if (candidate != null && settingType.IsInstanceOfType(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void SetParameterValue(object effectSettings, string fieldName, object value)
        {
            if (effectSettings == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var settingField = effectSettings.GetType().GetField(fieldName, flags);
            if (settingField == null)
            {
                return;
            }

            var parameter = settingField.GetValue(effectSettings);
            if (parameter == null)
            {
                return;
            }

            var overrideStateField = parameter.GetType().GetField("overrideState", flags);
            overrideStateField?.SetValue(parameter, true);

            var valueField = parameter.GetType().GetField("value", flags);
            if (valueField == null)
            {
                return;
            }

            var converted = ConvertValue(valueField.FieldType, value);
            if (converted != null || !valueField.FieldType.IsValueType)
            {
                valueField.SetValue(parameter, converted);
            }
        }

        private static void SetEnumByName(object instance, string memberName, string enumName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName) || string.IsNullOrWhiteSpace(enumName))
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null && field.FieldType.IsEnum)
            {
                try
                {
                    var parsed = Enum.Parse(field.FieldType, enumName, ignoreCase: true);
                    field.SetValue(instance, parsed);
                }
                catch
                {
                    // Keep existing value.
                }

                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
            {
                return;
            }

            try
            {
                var parsed = Enum.Parse(property.PropertyType, enumName, ignoreCase: true);
                property.SetValue(instance, parsed);
            }
            catch
            {
                // Keep existing value.
            }
        }

        private static void SetFieldOrProperty(object instance, string memberName, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName))
            {
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = instance.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                var convertedFieldValue = ConvertValue(field.FieldType, value);
                if (convertedFieldValue != null || !field.FieldType.IsValueType)
                {
                    field.SetValue(instance, convertedFieldValue);
                }

                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            var convertedPropertyValue = ConvertValue(property.PropertyType, value);
            if (convertedPropertyValue != null || !property.PropertyType.IsValueType)
            {
                property.SetValue(instance, convertedPropertyValue);
            }
        }

        private static object ConvertValue(Type targetType, object value)
        {
            if (targetType == null)
            {
                return null;
            }

            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
            {
                return value;
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }

            if (targetType == typeof(string))
            {
                return Convert.ToString(value);
            }

            if (targetType.IsEnum)
            {
                return value is string name
                    ? Enum.Parse(targetType, name, ignoreCase: true)
                    : Enum.ToObject(targetType, value);
            }

            if (targetType == typeof(LayerMask))
            {
                return new LayerMask { value = Convert.ToInt32(value) };
            }

            return null;
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var type = Type.GetType(fullName);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void EnsureAssetDirectoryForPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            var absoluteDirectory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(absoluteDirectory))
            {
                Directory.CreateDirectory(absoluteDirectory);
            }
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
