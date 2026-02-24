using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CodexSix.UiKit.Runtime.Tests.Runtime
{
    public sealed class UiRuntimeLifetimePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return DestroyAllContexts();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyAllContexts();
        }

        [UnityTest]
        public IEnumerator SceneScopedContext_IsDestroyed_WhenSceneUnloaded()
        {
            var scene = SceneManager.CreateScene("UiKitSceneScopedTest");
            var runtimeRoot = new GameObject("UiRuntime_SceneScoped");
            SceneManager.MoveGameObjectToScene(runtimeRoot, scene);
            runtimeRoot.AddComponent<UiRuntimeInstaller>();

            yield return null;

            var contextsBeforeUnload = Object.FindObjectsByType<UiContext>(FindObjectsSortMode.None);
            Assert.AreEqual(1, contextsBeforeUnload.Length);

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (!unload.isDone)
            {
                yield return null;
            }

            yield return null;

            var contextsAfterUnload = Object.FindObjectsByType<UiContext>(FindObjectsSortMode.None);
            Assert.AreEqual(0, contextsAfterUnload.Length);
        }

        [UnityTest]
        public IEnumerator PersistentToastChannel_PersistsAcrossContextRecreation()
        {
            var runtimeA = new GameObject("UiRuntime_A");
            runtimeA.AddComponent<UiRuntimeInstaller>();
            yield return null;

            var contextA = runtimeA.GetComponent<UiContext>();
            Assert.IsNotNull(contextA);

            var persistentA = contextA.GetToastService(UiToastChannels.Persistent) as ToastService;
            Assert.IsNotNull(persistentA);
            ClearToasts(persistentA);

            persistentA.Enqueue(new ToastRequest("persist.test", "Persistent toast", 10f, ToastPriority.High));
            Assert.AreEqual(1, persistentA.SnapshotActiveToasts().Count);

            Object.Destroy(runtimeA);
            yield return null;

            var runtimeB = new GameObject("UiRuntime_B");
            runtimeB.AddComponent<UiRuntimeInstaller>();
            yield return null;

            var contextB = runtimeB.GetComponent<UiContext>();
            var persistentB = contextB.GetToastService(UiToastChannels.Persistent) as ToastService;
            Assert.IsNotNull(persistentB);

            var active = persistentB.SnapshotActiveToasts();
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual("Persistent toast", active[0].Message);

            ClearToasts(persistentB);
        }

        private static IEnumerator DestroyAllContexts()
        {
            var contexts = Object.FindObjectsByType<UiContext>(FindObjectsSortMode.None);
            for (var i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] != null)
                {
                    Object.Destroy(contexts[i].gameObject);
                }
            }

            yield return null;
        }

        private static void ClearToasts(ToastService service)
        {
            var snapshot = service.SnapshotActiveToasts();
            for (var i = 0; i < snapshot.Count; i++)
            {
                service.Dismiss(snapshot[i].Handle);
            }
        }
    }
}