#nullable enable

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace McpTest.VoxelVillage.Tests
{
    public sealed class SpeechBubbleGraphicTests
    {
        [Test]
        public void OnPopulateMesh_CreatesRoundedBubbleWithTail()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var bubbleObject = new GameObject("Bubble", typeof(RectTransform), typeof(SpeechBubbleGraphic));

            try
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                bubbleObject.transform.SetParent(canvasObject.transform, false);

                var rectTransform = bubbleObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(320f, 140f);

                var graphic = bubbleObject.GetComponent<SpeechBubbleGraphic>();
                graphic.CornerRadius = 24f;
                graphic.TailWidth = 34f;
                graphic.TailHeight = 18f;
                graphic.CornerSegments = 6;

                var method = typeof(SpeechBubbleGraphic).GetMethod(
                    "OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(VertexHelper) },
                    null);
                Assert.That(method, Is.Not.Null);

                using var vertexHelper = new VertexHelper();
                method!.Invoke(graphic, new object[] { vertexHelper });

                var mesh = new Mesh();
                try
                {
                    vertexHelper.FillMesh(mesh);

                    Assert.That(mesh.vertexCount, Is.GreaterThan(20));
                    Assert.That(mesh.bounds.size.x, Is.GreaterThan(300f));
                    Assert.That(mesh.bounds.size.y, Is.GreaterThan(130f));
                    Assert.That(mesh.bounds.min.y, Is.LessThan(-60f));
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(bubbleObject);
                Object.DestroyImmediate(canvasObject);
            }
        }
    }
}
