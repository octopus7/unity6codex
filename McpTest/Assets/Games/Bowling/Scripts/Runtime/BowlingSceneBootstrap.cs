#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace McpTest.Bowling
{
    public static class BowlingSceneBootstrap
    {
        public const string ScenePath = "Assets/Games/Bowling/Scenes/BowlingGame.unity";
        const string SceneName = "BowlingGame";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapLoadedScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!IsBowlingScene(scene))
            {
                return;
            }

            DestroyRuntimeMcpDemoIfPresent();

            if (Object.FindAnyObjectByType<BowlingGameController>() != null)
            {
                return;
            }

            var root = new GameObject(nameof(BowlingGameController));
            root.AddComponent<BowlingGameController>();
        }

        static bool IsBowlingScene(Scene scene)
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
