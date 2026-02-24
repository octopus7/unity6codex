using System.IO;
using CodexSix.UiToolkit.HandsOn;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CodexSix.UiToolkit.HandsOn.Editor
{
    public static class UiToolkitHandsOnMenu
    {
        private const string ScenePath = "Assets/Labs/UiToolkitHandsOn/Scenes/UiToolkitHandsOn.unity";
        private const string ReadmePath = "Assets/Labs/UiToolkitHandsOn/README.md";

        [MenuItem("Tools/UI Toolkit Lab/Create Practice Scene (Safe)")]
        public static void CreatePracticeSceneSafe()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                FocusOrCreateLabObject();
                Debug.Log("UI Toolkit Lab: opened existing practice scene (safe).");
                return;
            }

            EnsureSceneDirectory();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera();
            FocusOrCreateLabObject();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("UI Toolkit Lab: created dedicated practice scene (safe).");
        }

        [MenuItem("Tools/UI Toolkit Lab/Open README")]
        public static void OpenReadme()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(ReadmePath);
            if (asset == null)
            {
                Debug.LogWarning("UI Toolkit Lab: README asset not found.");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void FocusOrCreateLabObject()
        {
            var existing = Object.FindFirstObjectByType<UiToolkitHandsOnController>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                return;
            }

            var labObject = new GameObject("UI Toolkit HandsOn", typeof(UIDocument), typeof(UiToolkitHandsOnController));
            Selection.activeObject = labObject;
        }

        private static void CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f, 1f);
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
