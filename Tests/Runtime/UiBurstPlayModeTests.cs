using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CodexSix.UiKit.Runtime.Tests.Runtime
{
    public sealed class UiBurstPlayModeTests
    {
        [UnityTest]
        public IEnumerator SignalBus_BurstDispatch_DeliversAllEvents()
        {
            var runtimeRoot = new GameObject("UiRuntime_Burst");
            runtimeRoot.AddComponent<UiRuntimeInstaller>();
            yield return null;

            var context = runtimeRoot.GetComponent<UiContext>();
            Assert.IsNotNull(context);

            var observedCount = 0;
            using var sub = context.SignalBus.Subscribe<int>(_ => observedCount++);

            const int burstCount = 150;
            for (var i = 0; i < burstCount; i++)
            {
                context.SignalBus.Publish(i);
            }

            yield return null;

            Assert.AreEqual(burstCount, observedCount);

            var toastService = context.ToastService;
            for (var i = 0; i < 120; i++)
            {
                toastService.Enqueue(new ToastRequest($"burst.{i}", $"Burst {i}", 2f, ToastPriority.Low));
            }

            toastService.Tick();
            Assert.LessOrEqual(toastService.SnapshotActiveToasts().Count, 3);

            Object.Destroy(runtimeRoot);
            yield return null;
        }
    }
}