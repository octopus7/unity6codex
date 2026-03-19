using CodexSix.UguiRuntime;
using CodexSix.UguiRuntime.Samples.StackedUguiDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodexSix.UguiRuntime.Samples.StackedUguiDemo.Editor
{
    public static class StackedUguiDemoSampleSceneMenu
    {
        private const string RootFolder = "Assets/CodexSixSamples/StackedUguiDemo";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string CatalogPath = RootFolder + "/StackedUguiDemoCatalog.asset";
        private const string ScenePath = "Assets/Scenes/StackedUguiDemo.unity";

        public static void CreateSampleScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolder("Assets", "Scenes");
            EnsureFolder("Assets", "CodexSixSamples");
            EnsureFolder("Assets/CodexSixSamples", "StackedUguiDemo");
            EnsureFolder(RootFolder, "Prefabs");

            var hudPrefab = CreateOrUpdatePrefab<StackedUguiDemoHudScreen>(PrefabsFolder + "/HudScreen.prefab", "HudScreen");
            var inventoryPrefab = CreateOrUpdatePrefab<StackedUguiDemoInventoryScreen>(PrefabsFolder + "/InventoryScreen.prefab", "InventoryScreen");
            var settingsPrefab = CreateOrUpdatePrefab<StackedUguiDemoSettingsScreen>(PrefabsFolder + "/SettingsScreen.prefab", "SettingsScreen");
            var confirmPrefab = CreateOrUpdatePrefab<StackedUguiDemoConfirmPopup>(PrefabsFolder + "/ConfirmPopup.prefab", "ConfirmPopup");
            var noticePrefab = CreateOrUpdatePrefab<StackedUguiDemoNoticePopup>(PrefabsFolder + "/NestedNoticePopup.prefab", "NestedNoticePopup");

            var catalog = LoadOrCreateCatalog();
            catalog.Screens.Clear();
            catalog.Screens.Add(new UiScreenDefinition { Id = "hud", Prefab = hudPrefab, CacheInstance = true });
            catalog.Screens.Add(new UiScreenDefinition { Id = "inventory", Prefab = inventoryPrefab, CacheInstance = true });
            catalog.Screens.Add(new UiScreenDefinition { Id = "settings", Prefab = settingsPrefab, CacheInstance = true });

            catalog.Popups.Clear();
            catalog.Popups.Add(new UiPopupDefinition { Id = "confirm", Prefab = confirmPrefab });
            catalog.Popups.Add(new UiPopupDefinition { Id = "nested-notice", Prefab = noticePrefab });

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "StackedUguiDemo";

            var runtimeRoot = new GameObject("UiRuntime");
            var installer = runtimeRoot.AddComponent<UiRuntimeInstaller>();
            installer.Catalog = catalog;
            runtimeRoot.AddComponent<StackedUguiDemoController>();

            var gameplayCanvas = new GameObject("GameplayCanvas");
            gameplayCanvas.AddComponent<StackedUguiDemoGameplayPanel>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"Stacked uGUI demo scene created: {ScenePath}");
        }

        private static T CreateOrUpdatePrefab<T>(string assetPath, string prefabName) where T : Component
        {
            var prefabRoot = new GameObject(prefabName, typeof(RectTransform), typeof(T));
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            Object.DestroyImmediate(prefabRoot);

            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            return savedPrefab.GetComponent<T>();
        }

        private static UiCatalog LoadOrCreateCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UiCatalog>(CatalogPath);
            if (existing != null)
            {
                return existing;
            }

            var catalog = ScriptableObject.CreateInstance<UiCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static void EnsureFolder(string parent, string child)
        {
            var fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
