#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace McpTest.VoxelVillage
{
    public static class VoxelVillageSceneBootstrap
    {
        public const string ScenePath = "Assets/Games/VoxelVillage/Scenes/VoxelVillage.unity";
        const string SceneName = "VoxelVillage";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapLoadedScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!IsVoxelVillageScene(scene))
            {
                return;
            }

            DestroyRuntimeMcpDemoIfPresent();

            if (Object.FindAnyObjectByType<VoxelVillageGameController>() != null)
            {
                return;
            }

            var root = new GameObject(nameof(VoxelVillageGameController));
            root.AddComponent<VoxelVillageGameController>();
        }

        static bool IsVoxelVillageScene(Scene scene)
        {
            return scene.path == ScenePath || scene.name == SceneName;
        }

        static void DestroyRuntimeMcpDemoIfPresent()
        {
            var runtimeDemo = GameObject.Find("RuntimeMcpGridDemo");
            if (runtimeDemo != null)
            {
                Object.Destroy(runtimeDemo);
            }
        }
    }
}
