#nullable enable
using System.Collections;
using CodexSix.UguiRuntime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CodexSix.UguiRuntime.Tests.Runtime
{
    public sealed class UiRuntimeInstallerPlayModeTests
    {
        [UnityTest]
        public IEnumerator Installer_CreatesCoreComponentsAndLayers()
        {
            var runtimeRoot = new GameObject("UiRuntime");
            runtimeRoot.AddComponent<UiRuntimeInstaller>();

            yield return null;

            var installer = runtimeRoot.GetComponent<UiRuntimeInstaller>();
            Assert.NotNull(installer);
            Assert.NotNull(runtimeRoot.GetComponent<Canvas>());
            Assert.NotNull(runtimeRoot.GetComponent<UnityEngine.UI.GraphicRaycaster>());
            Assert.NotNull(runtimeRoot.GetComponent<UiRoot>());
            Assert.NotNull(runtimeRoot.GetComponent<UiContext>());
            Assert.NotNull(Object.FindFirstObjectByType<EventSystem>());

            var uiRoot = runtimeRoot.GetComponent<UiRoot>();
            Assert.NotNull(uiRoot.ScreenLayer);
            Assert.NotNull(uiRoot.BlockerLayer);
            Assert.NotNull(uiRoot.ModalLayer);
            Assert.NotNull(uiRoot.OverlayLayer);

            Object.Destroy(runtimeRoot);
            yield return null;
        }
    }
}
