using CodexSix.UiKit.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexSix.UiKit.Runtime.Samples.MockArena.Editor
{
    public static class MockArenaSampleSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/MockArenaUiSample.unity";

        [MenuItem("Tools/CodexSix/UI Kit/Create Mock Arena Sample Scene")]
        public static void CreateSampleScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "MockArenaUiSample";

            var runtimeRoot = new GameObject("UiRuntime");
            runtimeRoot.AddComponent<UiRuntimeInstaller>();
            runtimeRoot.AddComponent<UiDocumentBinder>();
            runtimeRoot.AddComponent<MockArenaAdapter>();
            runtimeRoot.AddComponent<MockArenaUiBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"MockArena sample scene created: {ScenePath}");
        }
    }
}