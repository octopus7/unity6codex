using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            BuildDebugHud(generatedRoot.transform, client, camera);
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
            ApplyMaterialColor(ground.GetComponent<Renderer>(), new Color(0.23f, 0.26f, 0.24f));

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
            ApplyMaterialColor(zone.GetComponent<Renderer>(), new Color(0.16f, 0.35f, 0.19f));
            var box = zone.GetComponent<BoxCollider>();
            box.isTrigger = true;
            zone.AddComponent<ShopZoneMarker>().Size = new Vector2(12f, 12f);

            var healPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            healPad.name = "ShopItem_Heal50";
            healPad.transform.SetParent(parent, false);
            healPad.transform.position = new Vector3(-2f, 0.5f, 28f);
            healPad.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            ApplyMaterialColor(healPad.GetComponent<Renderer>(), new Color(0.8f, 0.25f, 0.25f));
            var healMarker = healPad.AddComponent<ShopItemMarker>();
            healMarker.ItemId = 1;
            healMarker.Cost = 5;
            healMarker.ItemName = "Heal50";

            var speedPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            speedPad.name = "ShopItem_Speed20";
            speedPad.transform.SetParent(parent, false);
            speedPad.transform.position = new Vector3(2f, 0.5f, 28f);
            speedPad.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            ApplyMaterialColor(speedPad.GetComponent<Renderer>(), new Color(0.25f, 0.45f, 0.9f));
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
            ApplyMaterialColor(portal.GetComponent<Renderer>(), color);
            UnityEngine.Object.DestroyImmediate(portal.GetComponent<CapsuleCollider>());
            var collider = portal.AddComponent<SphereCollider>();
            collider.radius = 1.2f;
            collider.isTrigger = true;

            var marker = portal.AddComponent<PortalMarker>();
            marker.PortalId = portalId;
            marker.PortalType = portalType;

            BuildPortalGroundIndicator(parent, name, position, portalType);
        }

        private static void BuildPortalGroundIndicator(Transform parent, string portalName, Vector3 position, PortalType portalType)
        {
            switch (portalType)
            {
                case PortalType.Entry:
                    BuildEntryPortalIndicator(parent, portalName, position);
                    break;
                case PortalType.Exit:
                    BuildExitPortalIndicator(parent, portalName, position);
                    break;
            }
        }

        private static void BuildEntryPortalIndicator(Transform parent, string portalName, Vector3 position)
        {
            var root = new GameObject($"{portalName}_Indicator");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, 0.02f, position.z);

            var ringOuterColor = new Color(0.08f, 0.75f, 1f);
            var ringInnerColor = new Color(0.23f, 0.26f, 0.24f);

            CreateIndicatorPrimitive(
                PrimitiveType.Cylinder,
                root.transform,
                "RingOuter",
                Vector3.zero,
                Quaternion.identity,
                new Vector3(2.35f, 0.018f, 2.35f),
                ringOuterColor,
                emissionStrength: 1.4f);

            CreateIndicatorPrimitive(
                PrimitiveType.Cylinder,
                root.transform,
                "RingInner",
                new Vector3(0f, 0.002f, 0f),
                Quaternion.identity,
                new Vector3(1.75f, 0.019f, 1.75f),
                ringInnerColor);

            AddIndicatorParticles(root.transform, "RingParticles", ringOuterColor, radius: 1.3f);
        }

        private static void BuildExitPortalIndicator(Transform parent, string portalName, Vector3 position)
        {
            var toMapCenter = new Vector3(-position.x, 0f, -position.z);
            if (toMapCenter.sqrMagnitude < 0.0001f)
            {
                toMapCenter = Vector3.forward;
            }

            var root = new GameObject($"{portalName}_Indicator");
            root.transform.SetParent(parent, false);
            // Exit portal sits inside the shop floor slab (y 0~0.2), so lift the marker above it.
            root.transform.position = new Vector3(position.x, 0.215f, position.z);
            root.transform.rotation = Quaternion.LookRotation(toMapCenter.normalized, Vector3.up);

            CreateIndicatorPrimitive(
                PrimitiveType.Cylinder,
                root.transform,
                "BaseDisc",
                Vector3.zero,
                Quaternion.identity,
                new Vector3(2.2f, 0.018f, 2.2f),
                new Color(0.62f, 0.25f, 0.07f),
                emissionStrength: 0.55f);

            var arrowColor = new Color(1f, 0.56f, 0.1f);
            CreateIndicatorPrimitive(
                PrimitiveType.Cube,
                root.transform,
                "ArrowShaft",
                new Vector3(0f, 0.02f, -0.35f),
                Quaternion.identity,
                new Vector3(0.34f, 0.03f, 1.0f),
                arrowColor,
                emissionStrength: 1.25f);

            CreateIndicatorPrimitive(
                PrimitiveType.Cube,
                root.transform,
                "ArrowHeadLeft",
                new Vector3(-0.22f, 0.02f, 0.28f),
                Quaternion.Euler(0f, 35f, 0f),
                new Vector3(0.27f, 0.03f, 0.62f),
                arrowColor,
                emissionStrength: 1.25f);

            CreateIndicatorPrimitive(
                PrimitiveType.Cube,
                root.transform,
                "ArrowHeadRight",
                new Vector3(0.22f, 0.02f, 0.28f),
                Quaternion.Euler(0f, -35f, 0f),
                new Vector3(0.27f, 0.03f, 0.62f),
                arrowColor,
                emissionStrength: 1.25f);

            AddIndicatorParticles(root.transform, "ArrowParticles", arrowColor, radius: 1.2f);
        }

        private static void CreateIndicatorPrimitive(
            PrimitiveType type,
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Color color,
            float emissionStrength = 0f)
        {
            var indicator = GameObject.CreatePrimitive(type);
            indicator.name = name;
            indicator.transform.SetParent(parent, worldPositionStays: false);
            indicator.transform.localPosition = localPosition;
            indicator.transform.localRotation = localRotation;
            indicator.transform.localScale = localScale;

            var renderer = indicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                ApplyMaterialColor(renderer, color, emissionStrength);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var collider = indicator.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void AddIndicatorParticles(Transform parent, string name, Color color, float radius)
        {
            var particlesObject = new GameObject(name);
            particlesObject.transform.SetParent(parent, worldPositionStays: false);
            particlesObject.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            // Circle emitter defaults to XY plane; rotate so particles spawn on ground (XZ plane).
            particlesObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var particleSystem = particlesObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = 1.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.95f, 1.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
            main.startColor = new Color(color.r, color.g, color.b, 0.82f);
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 34f;

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.22f;

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            // Keep emitter laid on the ground, but force drift direction upward in world space.
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(color.r, color.g, color.b), 0f),
                    new GradientColorKey(new Color(color.r, color.g, color.b), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.78f, 0.1f),
                    new GradientAlphaKey(0.54f, 0.74f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = CreateAdditiveParticleMaterial(color);
        }

        private static Material CreateAdditiveParticleMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Additive")
                         ?? Shader.Find("Legacy Shaders/Particles/Additive")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");

            var material = new Material(shader);
            if (shader != null && shader.name == "Universal Render Pipeline/Particles/Unlit")
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_Blend"))
                {
                    material.SetFloat("_Blend", 2f);
                }

                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                }

                if (material.HasProperty("_DstBlend"))
                {
                    material.SetFloat("_DstBlend", (float)BlendMode.One);
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetFloat("_ZWrite", 0f);
                }

                if (material.HasProperty("_Cull"))
                {
                    material.SetFloat("_Cull", (float)CullMode.Off);
                }

                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (material.HasProperty("_Color"))
            {
                material.color = new Color(color.r, color.g, color.b, 0.88f);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.88f));
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
            }

            return material;
        }

        private static void ApplyMaterialColor(Renderer renderer, Color color, float emissionStrength = 0f)
        {
            if (renderer == null)
            {
                return;
            }

            var sourceMaterial = renderer.sharedMaterial;
            var shader = sourceMaterial != null && sourceMaterial.shader != null
                ? sourceMaterial.shader
                : Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard")
                  ?? Shader.Find("Unlit/Color");

            var material = new Material(shader);
            if (material.HasProperty("_Color"))
            {
                material.color = color;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (emissionStrength > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionStrength);
            }

            renderer.sharedMaterial = material;
        }

        private static void AddBoundaryWall(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            ApplyMaterialColor(wall.GetComponent<Renderer>(), new Color(0.27f, 0.27f, 0.29f));
        }

        private static void AddObstacle(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
            ApplyMaterialColor(obstacle.GetComponent<Renderer>(), new Color(0.36f, 0.37f, 0.4f));
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
            var itemDataManager = AddComponentByName(runtimeRoot, "CodexSix.TopdownShooter.Game.ItemDataManager");
            var inventoryManager = AddComponentByName(runtimeRoot, "CodexSix.TopdownShooter.Game.PlayerInventoryManager");

            var playerContainer = new GameObject("Players").transform;
            playerContainer.SetParent(runtimeRoot.transform, false);
            var projectileContainer = new GameObject("Projectiles").transform;
            projectileContainer.SetParent(runtimeRoot.transform, false);
            var coinContainer = new GameObject("CoinStacks").transform;
            coinContainer.SetParent(runtimeRoot.transform, false);
            var itemDropContainer = new GameObject("ItemDrops").transform;
            itemDropContainer.SetParent(runtimeRoot.transform, false);

            client.Transport = transport;
            client.PlayerContainer = playerContainer;
            client.ProjectileContainer = projectileContainer;
            client.CoinContainer = coinContainer;
            client.ItemDropContainer = itemDropContainer;
            client.RespawnBurstEffectPrefab = RespawnBurstEffectPrefabUtility.EnsurePrefabAsset();
            client.RespawnBurstResourcesPath = "Effects/RespawnBurstEffect";
            client.RespawnBurstPoolInitialSize = 16;
            client.RespawnBurstPoolMaxSize = 48;
            client.RespawnBurstLifetimeSeconds = 1.1f;

            inputSender.Client = client;
            inputSender.WorldCamera = camera;
            inputSender.SendRateHz = 30f;

            SetComponentMember(itemDataManager, "ResourcesCatalogPath", "Items/item_catalog");
            SetComponentMember(itemDataManager, "LoadOnAwake", true);
            SetComponentMember(inventoryManager, "ItemDataManager", itemDataManager);
            SetComponentMember(inventoryManager, "DefaultSlotCount", 24);
            SetComponentMember(client, "ItemDataManager", itemDataManager);
            SetComponentMember(client, "InventoryManager", inventoryManager);

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

        private static Component AddComponentByName(GameObject host, string typeName)
        {
            if (host == null || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var type = Type.GetType($"{typeName}, Assembly-CSharp");
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogWarning($"TopDownShooterBootstrap could not find component type: {typeName}");
                return null;
            }

            return host.AddComponent(type);
        }

        private static void SetComponentMember(Component component, string memberName, object value)
        {
            if (component == null || string.IsNullOrWhiteSpace(memberName))
            {
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = component.GetType();

            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                field.SetValue(component, value);
                return;
            }

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(component, value);
            }
        }

        private static void BuildDebugHud(Transform parent, NetworkGameClient client, Camera camera)
        {
            var hudRoot = new GameObject("Hud");
            hudRoot.transform.SetParent(parent, false);

            var connectionObject = new GameObject("ConnectionPanel");
            connectionObject.transform.SetParent(hudRoot.transform, false);
            var connection = connectionObject.AddComponent<ConnectionPanelHud>();
            connection.Client = client;
            connection.UiScale = 2f;
            connection.HideWhenConnected = true;

            var shopObject = new GameObject("ShopPanel");
            shopObject.transform.SetParent(hudRoot.transform, false);
            var shop = shopObject.AddComponent<ShopPanelHud>();
            shop.Client = client;
            shop.HealItemId = 1;
            shop.SpeedItemId = 2;
            shop.UiScale = 2f;

            var statusObject = new GameObject("StatusPanel");
            statusObject.transform.SetParent(hudRoot.transform, false);
            var status = statusObject.AddComponent<StatusPanelHud>();
            status.Client = client;
            status.UiScale = 2f;

            var leaderboardObject = new GameObject("LeaderboardPanel");
            leaderboardObject.transform.SetParent(hudRoot.transform, false);
            var leaderboard = leaderboardObject.AddComponent<LeaderboardPanelHud>();
            leaderboard.Client = client;
            leaderboard.UiScale = 2f;

            var inventoryObject = new GameObject("InventoryPanel");
            inventoryObject.transform.SetParent(hudRoot.transform, false);
            var inventory = inventoryObject.AddComponent<InventoryPanelHud>();
            inventory.Client = client;
            inventory.InventoryManager = client.GetComponent<PlayerInventoryManager>();
            inventory.ItemDataManager = client.GetComponent<ItemDataManager>();
            inventory.UiScale = 2f;

            var coinDispenserObject = new GameObject("CoinDispenserPanel");
            coinDispenserObject.transform.SetParent(hudRoot.transform, false);
            var coinDispenser = coinDispenserObject.AddComponent<CoinDispenserHud>();
            coinDispenser.Client = client;
            coinDispenser.WorldCamera = camera;
            coinDispenser.UiScale = 2f;

            var overheadHpObject = new GameObject("OverheadHealthBars");
            overheadHpObject.transform.SetParent(hudRoot.transform, false);
            var overheadHp = overheadHpObject.AddComponent<PlayerOverheadHealthHud>();
            overheadHp.Client = client;
            overheadHp.WorldCamera = camera;
            overheadHp.UiScale = 2f;
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
