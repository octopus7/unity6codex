using System.Collections.Generic;
using System.IO;
using BeltScroll;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeltScroll.Editor
{
    public static class BeltScrollSceneBootstrapper
    {
        private const string ScenePath = "Assets/Scenes/BeltScrollScene.unity";
        private const string GeneratedRootName = "__BootstrapGenerated";
        private const string FadeShaderPath = "Assets/__BootstrapGenerated/BeltScroll/Materials/SpriteRightFade.shader";
        private const string FadeMaterialPath = "Assets/__BootstrapGenerated/BeltScroll/Materials/SpriteRightFade.mat";
        private const string CharacterPath = "Assets/Characters/placeholder_hero.png";
        private const string MotionSetPath = "Assets/Characters/PlaceholderHeroMotionSet.asset";
        private const float BackgroundPixelsPerUnit = 100f;
        private const float CharacterPixelsPerUnit = 320f;

        private static readonly string[] BackgroundPaths =
        {
            "Assets/Backgrounds/bg01.PNG",
            "Assets/Backgrounds/bg02.PNG",
            "Assets/Backgrounds/bg03.PNG",
            "Assets/Backgrounds/bg04.PNG",
            "Assets/Backgrounds/bg05.PNG"
        };

        private static readonly int[] OverlapPixels =
        {
            728,
            568,
            355,
            520,
            0
        };

        [MenuItem("Tools/BeltScroll/Bootstrap Scroll Scene (Safe)")]
        public static void BootstrapSafe()
        {
            EnsureFolders();
            AssetDatabase.Refresh();

            var backgroundSprites = ImportBackgroundSprites();
            var characterSprite = ImportSprite(CharacterPath, CharacterPixelsPerUnit, SpriteAlignment.Custom, new Vector2(0.5f, 0f), true);
            var fadeMaterial = EnsureFadeMaterial();
            var motionSet = EnsureMotionSet(characterSprite);

            var scene = OpenOrCreateScene();
            ClearGeneratedRoot(scene);
            BuildScene(backgroundSprites, characterSprite, fadeMaterial, motionSet);
            EnsureSceneInBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Safe Bootstrap: BeltScrollScene generated inside __BootstrapGenerated.");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Characters");
            Directory.CreateDirectory("Assets/__BootstrapGenerated/BeltScroll/Materials");
        }

        private static Sprite[] ImportBackgroundSprites()
        {
            var sprites = new Sprite[BackgroundPaths.Length];
            for (var i = 0; i < BackgroundPaths.Length; i++)
            {
                sprites[i] = ImportSprite(BackgroundPaths[i], BackgroundPixelsPerUnit, SpriteAlignment.Center, new Vector2(0.5f, 0.5f), false);
            }

            return sprites;
        }

        private static Sprite ImportSprite(string path, float pixelsPerUnit, SpriteAlignment alignment, Vector2 pivot, bool alphaIsTransparency)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException($"Texture asset not found at {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)alignment;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidDataException($"Sprite import failed for {path}");
            }

            return sprite;
        }

        private static Material EnsureFadeMaterial()
        {
            var shader = Shader.Find("BeltScroll/SpriteRightFade");
            if (shader == null)
            {
                AssetDatabase.ImportAsset(FadeShaderPath, ImportAssetOptions.ForceUpdate);
                shader = Shader.Find("BeltScroll/SpriteRightFade");
            }

            if (shader == null)
            {
                throw new InvalidDataException("BeltScroll/SpriteRightFade shader could not be loaded.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "SpriteRightFade"
                };
                AssetDatabase.CreateAsset(material, FadeMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static CharacterMotionSet EnsureMotionSet(Sprite fallbackSprite)
        {
            var motionSet = AssetDatabase.LoadAssetAtPath<CharacterMotionSet>(MotionSetPath);
            if (motionSet == null)
            {
                motionSet = ScriptableObject.CreateInstance<CharacterMotionSet>();
                motionSet.fallbackSprite = fallbackSprite;
                AssetDatabase.CreateAsset(motionSet, MotionSetPath);
            }
            else if (motionSet.fallbackSprite == null)
            {
                motionSet.fallbackSprite = fallbackSprite;
                EditorUtility.SetDirty(motionSet);
            }

            return motionSet;
        }

        private static Scene OpenOrCreateScene()
        {
            if (File.Exists(ScenePath))
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static void ClearGeneratedRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == GeneratedRootName)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void BuildScene(Sprite[] backgroundSprites, Sprite characterSprite, Material fadeMaterial, CharacterMotionSet motionSet)
        {
            var root = new GameObject(GeneratedRootName);
            var stage = new GameObject("Stage");
            stage.transform.SetParent(root.transform);

            var bounds = CreateBackgrounds(stage.transform, backgroundSprites, fadeMaterial, out var backgroundHeight);
            var camera = CreateCamera(root.transform, backgroundHeight);
            var player = CreatePlayer(root.transform, characterSprite, fadeMaterial, motionSet, bounds);

            var follow = camera.gameObject.AddComponent<BeltScrollCameraFollow>();
            follow.Configure(player.transform, bounds, 0f);

            var cameraPosition = camera.transform.position;
            cameraPosition.x = Mathf.Clamp(player.transform.position.x, bounds.x, bounds.y);
            camera.transform.position = cameraPosition;
        }

        private static Vector2 CreateBackgrounds(Transform parent, Sprite[] sprites, Material fadeMaterial, out float backgroundHeight)
        {
            var cursor = 0f;
            var centers = new float[sprites.Length];
            var widths = new float[sprites.Length];
            backgroundHeight = 0f;

            for (var i = 0; i < sprites.Length; i++)
            {
                widths[i] = sprites[i].bounds.size.x;
                centers[i] = cursor + widths[i] * 0.5f;
                backgroundHeight = Mathf.Max(backgroundHeight, sprites[i].bounds.size.y);
                cursor += widths[i];
                if (i < sprites.Length - 1)
                {
                    cursor -= OverlapPixels[i] / BackgroundPixelsPerUnit;
                }
            }

            var totalWidth = cursor;
            var offset = -totalWidth * 0.5f;

            for (var i = 0; i < sprites.Length; i++)
            {
                var go = new GameObject($"bg0{i + 1}");
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(centers[i] + offset, 0f, 0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sharedMaterial = fadeMaterial;
                renderer.sortingOrder = 100 - i;

                var fade = go.AddComponent<BackgroundRightEdgeFade>();
                fade.RightFadeWidth = i < sprites.Length - 1 ? OverlapPixels[i] / sprites[i].rect.width : 0f;
            }

            return new Vector2(offset, offset + totalWidth);
        }

        private static Camera CreateCamera(Transform parent, float backgroundHeight)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = backgroundHeight * 0.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            return camera;
        }

        private static GameObject CreatePlayer(Transform parent, Sprite characterSprite, Material fadeMaterial, CharacterMotionSet motionSet, Vector2 bounds)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(bounds.x + 8.35f, -4.18f, 0f);
            go.transform.localScale = Vector3.one * 1.15f;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = characterSprite;
            renderer.sharedMaterial = fadeMaterial;
            renderer.sortingOrder = 500;

            var motionDriver = go.AddComponent<CharacterMotionDriver>();
            motionDriver.SetMotionSet(motionSet);

            var controller = go.AddComponent<PlayerBeltScrollController>();
            controller.XBounds = new Vector2(bounds.x + 0.75f, bounds.y - 0.75f);

            return go;
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in scenes)
            {
                if (scene.path == ScenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
