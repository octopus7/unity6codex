#nullable enable
using System.Collections.Generic;
using System.Threading;
using CodexSix.UguiRuntime;
using NUnit.Framework;
using UnityEngine;

namespace CodexSix.UguiRuntime.Tests.Editor
{
    public sealed class UiModalServiceTests
    {
        private readonly List<Object> _cleanup = new();

        [TearDown]
        public void TearDown()
        {
            TestPopupView.Reset();
            for (var i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null)
                {
                    Object.DestroyImmediate(_cleanup[i]);
                }
            }

            _cleanup.Clear();
        }

        [Test]
        public void ConfirmAndCancel_ResolveStackInLifoOrder()
        {
            var service = CreateService();
            var firstTask = service.ShowAsync(new UiPopupRequest("confirm", "A", "A"));
            var secondTask = service.ShowAsync(new UiPopupRequest("confirm", "B", "B"));

            Assert.AreEqual(2, service.ModalDepth);
            Assert.IsTrue(service.TryCancelTop());
            Assert.IsTrue(secondTask.IsCompleted);
            Assert.AreEqual(UiPopupResultKind.Cancelled, secondTask.Result.Kind);
            Assert.AreEqual(1, service.ModalDepth);

            Assert.IsTrue(service.TryConfirmTop());
            Assert.IsTrue(firstTask.IsCompleted);
            Assert.AreEqual(UiPopupResultKind.Confirmed, firstTask.Result.Kind);
            Assert.AreEqual(0, service.ModalDepth);
        }

        [Test]
        public void CancellationToken_DismissesPopup_AndBindIsCalled()
        {
            var service = CreateService();
            using var cts = new CancellationTokenSource();

            var task = service.ShowAsync(new UiPopupRequest("confirm", "Title", "Body"), cts.Token);
            Assert.AreEqual(1, TestPopupView.BindCount);

            cts.Cancel();

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(UiPopupResultKind.Dismissed, task.Result.Kind);
            Assert.AreEqual(0, service.ModalDepth);
        }

        private UiModalService CreateService()
        {
            var contextObject = new GameObject("UiContext");
            _cleanup.Add(contextObject);
            var context = contextObject.AddComponent<UiContext>();

            var layerObject = new GameObject("ModalLayer", typeof(RectTransform));
            _cleanup.Add(layerObject);
            var modalLayer = layerObject.GetComponent<RectTransform>();

            var popupPrefab = new GameObject("ConfirmPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(TestPopupView));
            _cleanup.Add(popupPrefab);

            var catalog = ScriptableObject.CreateInstance<UiCatalog>();
            _cleanup.Add(catalog);
            catalog.Popups.Add(new UiPopupDefinition { Id = "confirm", Prefab = popupPrefab.GetComponent<TestPopupView>() });

            return new UiModalService(context, catalog, modalLayer);
        }

        private sealed class TestPopupView : UiPopupView
        {
            public static int BindCount { get; private set; }

            public static void Reset()
            {
                BindCount = 0;
            }

            public override void Bind(UiPopupRequest request, UiModalHandle handle)
            {
                base.Bind(request, handle);
                BindCount++;
            }
        }
    }
}
