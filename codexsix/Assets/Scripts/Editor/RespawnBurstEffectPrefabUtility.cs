using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CodexSix.TopdownShooter.EditorTools
{
    public static class RespawnBurstEffectPrefabUtility
    {
        private const string PrefabFolderPath = "Assets/Resources/Effects";
        private const string PrefabAssetPath = PrefabFolderPath + "/RespawnBurstEffect.prefab";
        private const string MaterialAssetPath = PrefabFolderPath + "/RespawnBurstEffect.mat";

        [MenuItem("Tools/TopDownShooter/Rebuild Respawn Burst Prefab")]
        public static void RebuildPrefab()
        {
            var prefab = CreateOrLoadPrefab(forceRebuild: true);
            if (prefab != null)
            {
                Debug.Log($"Respawn burst prefab rebuilt: {PrefabAssetPath}");
            }
        }

        public static GameObject EnsurePrefabAsset()
        {
            return CreateOrLoadPrefab(forceRebuild: false);
        }

        [InitializeOnLoadMethod]
        private static void EnsurePrefabOnEditorLoad()
        {
            EnsurePrefabAsset();
        }

        private static GameObject CreateOrLoadPrefab(bool forceRebuild)
        {
            EnsureFolderPath("Assets/Resources");
            EnsureFolderPath(PrefabFolderPath);

            if (forceRebuild && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(PrefabAssetPath);
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("RespawnBurstEffect");
            root.SetActive(false);
            ConfigureBurstSystem(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabAssetPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return prefab;
        }

        private static void ConfigureBurstSystem(GameObject root)
        {
            var particleObject = new GameObject("BurstParticles");
            particleObject.transform.SetParent(root.transform, worldPositionStays: false);
            particleObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            var particleSystem = particleObject.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.95f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.8f, 8.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startColor = new Color(0.36f, 0.95f, 1f, 0.95f);
            main.gravityModifier = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 140;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 30, 42)
            });

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            shape.radiusThickness = 1f;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.7f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.2f, 0.8f, 1f), 0.45f),
                    new GradientColorKey(new Color(0.1f, 0.45f, 0.95f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.06f),
                    new GradientAlphaKey(0.45f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.1f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = EnsureMaterialAsset();

            particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static Material EnsureMaterialAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Additive")
                         ?? Shader.Find("Legacy Shaders/Particles/Additive")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Standard");

            var material = new Material(shader)
            {
                renderQueue = (int)RenderQueue.Transparent
            };

            if (material.HasProperty("_Color"))
            {
                material.color = new Color(0.38f, 0.95f, 1f, 0.92f);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.38f, 0.95f, 1f, 0.92f));
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.45f, 0.95f, 1f) * 2.4f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 2f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.One);
            }

            AssetDatabase.CreateAsset(material, MaterialAssetPath);
            return material;
        }

        private static void EnsureFolderPath(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var normalizedPath = folderPath.Replace("\\", "/");
            var parentPath = Path.GetDirectoryName(normalizedPath)?.Replace("\\", "/");
            var folderName = Path.GetFileName(normalizedPath);
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(folderName))
            {
                return;
            }

            EnsureFolderPath(parentPath);
            if (!AssetDatabase.IsValidFolder(normalizedPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }
    }
}
