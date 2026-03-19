#nullable enable
using System.Collections;
using CodexSix.UguiRuntime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CodexSix.UguiRuntime.Tests.Runtime
{
    public sealed class UiContextIntegrationPlayModeTests
    {
        [UnityTest]
        public IEnumerator ModalOpen_UpdatesBlockerAndGameplayInputBlock_AndBackdropDismissWorks()
        {
            TestPopupView.BindCount = 0;
            var popupPrefab = new GameObject("NoticePopup", typeof(RectTransform), typeof(CanvasGroup), typeof(TestPopupView));
            var popupView = popupPrefab.GetComponent<TestPopupView>();

            var catalog = ScriptableObject.CreateInstance<UiCatalog>();
            catalog.Popups.Add(new UiPopupDefinition { Id = "notice", Prefab = popupView });

            var runtimeRoot = new GameObject("UiRuntime");
            var installer = runtimeRoot.AddComponent<UiRuntimeInstaller>();
            installer.Catalog = catalog;

            yield return null;

            var context = runtimeRoot.GetComponent<UiContext>();
            var uiRoot = runtimeRoot.GetComponent<UiRoot>();
            Assert.IsFalse(context.InputBlockService.IsGameplayInputBlocked);

            var stickyTask = context.ModalService.ShowAsync(new UiPopupRequest("notice", "Sticky", "Blocked", dismissOnBackdrop: false));
            yield return null;

            Assert.AreEqual(1, context.ModalService.ModalDepth);
            Assert.IsTrue(context.InputBlockService.IsGameplayInputBlocked);
            Assert.IsTrue(uiRoot.BlockerLayer.gameObject.activeSelf);
            Assert.AreEqual(1, TestPopupView.BindCount);

            uiRoot.BlockerButton.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, context.ModalService.ModalDepth);
            Assert.IsFalse(stickyTask.IsCompleted);

            var dismissibleTask = context.ModalService.ShowAsync(new UiPopupRequest("notice", "Dismissible", "Tap backdrop", dismissOnBackdrop: true));
            yield return null;
            uiRoot.BlockerButton.onClick.Invoke();
            yield return null;

            Assert.IsTrue(dismissibleTask.IsCompleted);
            Assert.AreEqual(UiPopupResultKind.Cancelled, dismissibleTask.Result.Kind);
            Assert.AreEqual(1, context.ModalService.ModalDepth);

            context.ModalService.TryConfirmTop();
            yield return null;

            Assert.AreEqual(0, context.ModalService.ModalDepth);
            Assert.IsFalse(context.InputBlockService.IsGameplayInputBlocked);
            Assert.IsFalse(uiRoot.BlockerLayer.gameObject.activeSelf);

            Object.Destroy(popupPrefab);
            Object.Destroy(catalog);
            Object.Destroy(runtimeRoot);
            yield return null;
        }

        private sealed class TestPopupView : UiPopupView
        {
            public static int BindCount { get; set; }

            public override void Bind(UiPopupRequest request, UiModalHandle handle)
            {
                base.Bind(request, handle);
                BindCount++;
            }
        }
    }
}
