using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class InteractionHandGraphic : MaskableGraphic
    {
        public bool Closed;
        private static readonly Vector2[] OpenOutline = Open();
        private static readonly Vector2[] ClosedOutline = Fist();

        private static readonly Vector2[] OpenCrease = { new Vector2(25, 38), new Vector2(31, 34), new Vector2(40, 34) };
        private static readonly Vector2[] ClosedCrease = { new Vector2(19, 31), new Vector2(29, 36), new Vector2(37, 35) };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2[] points = Closed ? ClosedOutline : OpenOutline;
            Stroke(vh, points, 6f, new Color(0.025f, 0.02f, 0.015f, color.a * .9f), new Vector2(0, -1));
            Stroke(vh, points, 2.8f, color, Vector2.zero);
            Vector2[] crease = Closed ? ClosedCrease : OpenCrease;
            Stroke(vh, crease, 2.2f, color, Vector2.zero);
        }

        private void Stroke(VertexHelper vh, Vector2[] points, float width, Color tint, Vector2 offset)
        {
            Rect r = rectTransform.rect;
            float scale = Mathf.Min(r.width, r.height) / 64f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = (points[i] - new Vector2(32, 32)) * scale + r.center + offset;
                a.y = r.center.y - (points[i].y - 32) * scale + offset.y;
                if (i + 1 < points.Length)
                {
                    Vector2 b = (points[i + 1] - new Vector2(32, 32)) * scale + r.center + offset;
                    b.y = r.center.y - (points[i + 1].y - 32) * scale + offset.y;
                    Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * scale * .5f;
                    int start = vh.currentVertCount;
                    vh.AddVert(a - normal, tint, Vector2.zero);
                    vh.AddVert(a + normal, tint, Vector2.zero);
                    vh.AddVert(b + normal, tint, Vector2.zero);
                    vh.AddVert(b - normal, tint, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start, start + 2, start + 3);
                }
                Round(vh, a, width * scale * .5f, tint);
            }
        }

        private static void Round(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void Curve(List<Vector2> p, Vector2 controlA, Vector2 controlB, Vector2 end)
        {
            Vector2 start = p[p.Count - 1];
            for (int i = 1; i <= 6; i++)
            {
                float t = i / 6f, u = 1f - t;
                p.Add(u * u * u * start + 3 * u * u * t * controlA + 3 * u * t * t * controlB + t * t * t * end);
            }
        }

        private static Vector2[] Open()
        {
            var p = new List<Vector2> { new Vector2(25, 33), new Vector2(25, 11) };
            Curve(p, new Vector2(25, 4), new Vector2(32, 4), new Vector2(32, 11));
            p.Add(new Vector2(32, 26)); p.Add(new Vector2(34, 8));
            Curve(p, new Vector2(34, 1), new Vector2(41, 1), new Vector2(41, 8));
            p.Add(new Vector2(41, 26)); p.Add(new Vector2(43, 12));
            Curve(p, new Vector2(43, 5), new Vector2(50, 5), new Vector2(50, 12));
            p.Add(new Vector2(50, 29)); p.Add(new Vector2(52, 20));
            Curve(p, new Vector2(52, 14), new Vector2(58, 14), new Vector2(58, 20));
            p.Add(new Vector2(58, 38));
            Curve(p, new Vector2(58, 49), new Vector2(51, 57), new Vector2(41, 58));
            p.Add(new Vector2(31, 58));
            Curve(p, new Vector2(23, 53), new Vector2(19, 47), new Vector2(14, 39));
            p.Add(new Vector2(9, 33));
            Curve(p, new Vector2(4, 27), new Vector2(11, 21), new Vector2(16, 28));
            p.Add(new Vector2(25, 38));
            return p.ToArray();
        }

        private static Vector2[] Fist()
        {
            var p = new List<Vector2> { new Vector2(17, 30), new Vector2(17, 24) };
            Curve(p, new Vector2(17, 17), new Vector2(25, 17), new Vector2(25, 24));
            p.Add(new Vector2(25, 18));
            Curve(p, new Vector2(25, 11), new Vector2(33, 11), new Vector2(33, 18));
            p.Add(new Vector2(33, 16));
            Curve(p, new Vector2(33, 9), new Vector2(41, 9), new Vector2(41, 16));
            p.Add(new Vector2(41, 20));
            Curve(p, new Vector2(41, 13), new Vector2(49, 13), new Vector2(49, 20));
            p.Add(new Vector2(49, 26));
            Curve(p, new Vector2(49, 20), new Vector2(56, 20), new Vector2(56, 26));
            p.Add(new Vector2(56, 37));
            Curve(p, new Vector2(56, 48), new Vector2(49, 56), new Vector2(40, 58));
            p.Add(new Vector2(29, 57));
            Curve(p, new Vector2(22, 52), new Vector2(17, 45), new Vector2(12, 38));
            Curve(p, new Vector2(7, 31), new Vector2(12, 26), new Vector2(18, 31));
            p.Add(new Vector2(29, 37));
            return p.ToArray();
        }
    }

    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class InteractionRingGraphic : MaskableGraphic
    {
        private float progress;
        public float Progress { get => progress; set { if (Mathf.Abs(progress - value) < .005f) return; progress = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float fraction = Mathf.Clamp01(progress);
            Rect r = rectTransform.rect;
            float outer = Mathf.Min(r.width, r.height) * .5f;
            const int segments = 64;
            for (int i = 0; i < Mathf.CeilToInt(segments * fraction); i++)
            {
                float a = i / (float)segments * Mathf.PI * 2;
                float b = Mathf.Min((i + 1) / (float)segments, fraction) * Mathf.PI * 2;
                int v = vh.currentVertCount;
                vh.AddVert(r.center + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * outer, color, Vector2.zero);
                vh.AddVert(r.center + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (outer - 2f), color, Vector2.zero);
                vh.AddVert(r.center + new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * (outer - 2f), color, Vector2.zero);
                vh.AddVert(r.center + new Vector2(Mathf.Sin(b), Mathf.Cos(b)) * outer, color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v, v + 2, v + 3);
            }
        }
    }
}
