#nullable enable
using System.Collections.Generic;
using CodexSix.UguiRuntime;
using NUnit.Framework;
using UnityEngine;

namespace CodexSix.UguiRuntime.Tests.Editor
{
    public sealed class UiScreenServiceTests
    {
        private readonly List<Object> _cleanup = new();

        [TearDown]
        public void TearDown()
        {
            TestScreenView.Reset();
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
        public void ShowAndBack_RestoresPreviousScreen_AndSameScreenIsNoOp()
        {
            var service = CreateService(out _);

            service.Show("hud");
            service.Show("hud");

            var hud = TestScreenView.Instances["hud"];
            Assert.AreEqual(1, hud.ShowCount);
            Assert.AreEqual(1, hud.FocusCount);

            service.Show("inventory");
            var inventory = TestScreenView.Instances["inventory"];

            Assert.AreEqual("inventory", service.CurrentScreenId);
            Assert.AreEqual(1, hud.HideCount);
            Assert.AreEqual(1, hud.BlurCount);
            Assert.AreEqual(1, inventory.ShowCount);
            Assert.AreEqual(1, inventory.FocusCount);

            Assert.IsTrue(service.TryGoBack());
            Assert.AreEqual("hud", service.CurrentScreenId);
            Assert.AreEqual(2, hud.ShowCount);
            Assert.AreEqual(2, hud.FocusCount);
        }

        [Test]
        public void CacheInstanceFalse_RecreatesScreenAfterHide()
        {
            var service = CreateService(out var catalog);
            catalog.Screens.Clear();
            catalog.Screens.Add(new UiScreenDefinition { Id = "ephemeral", Prefab = CreateScreenPrefab("Ephemeral"), CacheInstance = false });
            catalog.Screens.Add(new UiScreenDefinition { Id = "other", Prefab = CreateScreenPrefab("Other"), CacheInstance = true });

            service.Show("ephemeral");
            var firstInstance = TestScreenView.Instances["ephemeral"];

            service.Show("other");
            service.TryGoBack();

            var secondInstance = TestScreenView.Instances["ephemeral"];
            Assert.AreNotSame(firstInstance, secondInstance);
        }

        private UiScreenService CreateService(out UiCatalog catalog)
        {
            var contextObject = new GameObject("UiContext");
            _cleanup.Add(contextObject);
            var context = contextObject.AddComponent<UiContext>();

            var layerObject = new GameObject("ScreenLayer", typeof(RectTransform));
            _cleanup.Add(layerObject);
            var screenLayer = layerObject.GetComponent<RectTransform>();

            catalog = ScriptableObject.CreateInstance<UiCatalog>();
            _cleanup.Add(catalog);
            catalog.Screens.Add(new UiScreenDefinition { Id = "hud", Prefab = CreateScreenPrefab("Hud"), CacheInstance = true });
            catalog.Screens.Add(new UiScreenDefinition { Id = "inventory", Prefab = CreateScreenPrefab("Inventory"), CacheInstance = true });

            return new UiScreenService(context, catalog, screenLayer);
        }

        private TestScreenView CreateScreenPrefab(string name)
        {
            var screenPrefab = new GameObject(name, typeof(RectTransform), typeof(TestScreenView));
            _cleanup.Add(screenPrefab);
            return screenPrefab.GetComponent<TestScreenView>();
        }

        private sealed class TestScreenView : UiScreenView
        {
            public static readonly Dictionary<string, TestScreenView> Instances = new();

            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public int FocusCount { get; private set; }
            public int BlurCount { get; private set; }

            public static void Reset()
            {
                Instances.Clear();
            }

            protected override void OnAttached()
            {
                if (!string.IsNullOrWhiteSpace(ScreenId))
                {
                    Instances[ScreenId] = this;
                }
            }

            protected override void OnShow()
            {
                ShowCount++;
            }

            protected override void OnHide()
            {
                HideCount++;
            }

            protected override void OnFocus()
            {
                FocusCount++;
            }

            protected override void OnBlur()
            {
                BlurCount++;
            }
        }
    }
}
