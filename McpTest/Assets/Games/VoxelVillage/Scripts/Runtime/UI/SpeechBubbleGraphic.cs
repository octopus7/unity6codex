#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace McpTest.VoxelVillage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpeechBubbleGraphic : MaskableGraphic
    {
        [SerializeField]
        [Min(0f)]
        float _cornerRadius = 24f;

        [SerializeField]
        [Min(0f)]
        float _tailWidth = 34f;

        [SerializeField]
        [Min(0f)]
        float _tailHeight = 18f;

        [SerializeField]
        float _tailOffsetX;

        [SerializeField]
        [Range(1, 12)]
        int _cornerSegments = 6;

        public float CornerRadius
        {
            get => _cornerRadius;
            set
            {
                var next = Mathf.Max(0f, value);
                if (Mathf.Approximately(_cornerRadius, next))
                {
                    return;
                }

                _cornerRadius = next;
                SetVerticesDirty();
            }
        }

        public float TailWidth
        {
            get => _tailWidth;
            set
            {
                var next = Mathf.Max(0f, value);
                if (Mathf.Approximately(_tailWidth, next))
                {
                    return;
                }

                _tailWidth = next;
                SetVerticesDirty();
            }
        }

        public float TailHeight
        {
            get => _tailHeight;
            set
            {
                var next = Mathf.Max(0f, value);
                if (Mathf.Approximately(_tailHeight, next))
                {
                    return;
                }

                _tailHeight = next;
                SetVerticesDirty();
            }
        }

        public float TailOffsetX
        {
            get => _tailOffsetX;
            set
            {
                if (Mathf.Approximately(_tailOffsetX, value))
                {
                    return;
                }

                _tailOffsetX = value;
                SetVerticesDirty();
            }
        }

        public int CornerSegments
        {
            get => _cornerSegments;
            set
            {
                var next = Mathf.Clamp(value, 1, 12);
                if (_cornerSegments == next)
                {
                    return;
                }

                _cornerSegments = next;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var tailHeight = Mathf.Min(_tailHeight, rect.height * 0.45f);
            var bodyRect = new Rect(rect.xMin, rect.yMin + tailHeight, rect.width, rect.height - tailHeight);
            if (bodyRect.width <= 0f || bodyRect.height <= 0f)
            {
                AddQuad(vh, rect.min, new Vector2(rect.xMin, rect.yMax), rect.max, new Vector2(rect.xMax, rect.yMin), color);
                return;
            }

            var radius = Mathf.Min(_cornerRadius, bodyRect.width * 0.5f, bodyRect.height * 0.5f);
            var points = new List<Vector2>(4 * (_cornerSegments + 2));
            BuildRoundedRect(points, bodyRect, radius, _cornerSegments);
            AddConvexPolygon(vh, points, color);

            if (_tailWidth > 0f && tailHeight > 0f)
            {
                AddTail(vh, bodyRect, rect.yMin, radius, color);
            }
        }

        static void BuildRoundedRect(List<Vector2> points, Rect rect, float radius, int segments)
        {
            points.Clear();
            if (radius <= 0.01f)
            {
                points.Add(new Vector2(rect.xMax, rect.yMin));
                points.Add(rect.max);
                points.Add(new Vector2(rect.xMin, rect.yMax));
                points.Add(rect.min);
                return;
            }

            points.Add(new Vector2(rect.xMax - radius, rect.yMin));
            AppendArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, -90f, 0f, segments, false);
            points.Add(new Vector2(rect.xMax, rect.yMax - radius));
            AppendArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, segments, false);
            points.Add(new Vector2(rect.xMin + radius, rect.yMax));
            AppendArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, segments, false);
            points.Add(new Vector2(rect.xMin, rect.yMin + radius));
            AppendArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, segments, false);
        }

        static void AppendArc(
            List<Vector2> points,
            Vector2 center,
            float radius,
            float startDegrees,
            float endDegrees,
            int segments,
            bool includeStart)
        {
            var startIndex = includeStart ? 0 : 1;
            for (var i = startIndex; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        void AddTail(VertexHelper vh, Rect bodyRect, float tipY, float radius, Color32 color32)
        {
            var flatHalfWidth = Mathf.Max(4f, bodyRect.width * 0.5f - radius - 4f);
            var tailHalfWidth = Mathf.Min(_tailWidth * 0.5f, flatHalfWidth);
            if (tailHalfWidth <= 0f)
            {
                return;
            }

            var centerX = Mathf.Clamp(bodyRect.center.x + _tailOffsetX, bodyRect.center.x - flatHalfWidth + tailHalfWidth, bodyRect.center.x + flatHalfWidth - tailHalfWidth);
            var baseY = bodyRect.yMin + 0.5f;
            var baseLeft = new Vector2(centerX - tailHalfWidth, baseY);
            var baseRight = new Vector2(centerX + tailHalfWidth, baseY);
            var tip = new Vector2(centerX, tipY);

            var startIndex = vh.currentVertCount;
            AddVertex(vh, baseLeft, color32);
            AddVertex(vh, tip, color32);
            AddVertex(vh, baseRight, color32);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        }

        static void AddConvexPolygon(VertexHelper vh, List<Vector2> points, Color32 color32)
        {
            if (points.Count < 3)
            {
                return;
            }

            var center = Vector2.zero;
            for (var i = 0; i < points.Count; i++)
            {
                center += points[i];
            }

            center /= points.Count;

            var startIndex = vh.currentVertCount;
            AddVertex(vh, center, color32);
            for (var i = 0; i < points.Count; i++)
            {
                AddVertex(vh, points[i], color32);
            }

            for (var i = 0; i < points.Count; i++)
            {
                var current = startIndex + 1 + i;
                var next = startIndex + 1 + ((i + 1) % points.Count);
                vh.AddTriangle(startIndex, current, next);
            }
        }

        static void AddQuad(VertexHelper vh, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight, Vector2 bottomRight, Color32 color32)
        {
            var startIndex = vh.currentVertCount;
            AddVertex(vh, bottomLeft, color32);
            AddVertex(vh, topLeft, color32);
            AddVertex(vh, topRight, color32);
            AddVertex(vh, bottomRight, color32);
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        static void AddVertex(VertexHelper vh, Vector2 position, Color32 color32)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color32;
            vertex.position = position;
            vh.AddVert(vertex);
        }
    }
}
