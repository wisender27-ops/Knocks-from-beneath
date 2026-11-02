using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Амбарный навесной замок: литой корпус с плечами, дужка, накладка скважины и заклёпки. Меш строится кодом,
    // грани плоские, как у остальной геометрии подвала. Начало координат — верх дужки, лицо смотрит в +Z.
    public static class BarnPadlock
    {
        private const float BodyWidth = 0.1f, BodyHeight = 0.09f, BodyDepth = 0.036f, Bevel = 0.006f;
        private const float ShackleRadius = 0.0065f, ShackleSpan = 0.03f, ShackleHeight = 0.05f;

        private sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Metal = new List<int>(), Dark = new List<int>();

            // Грань смотрит туда же, куда outward: порядок вершин не важен.
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, List<int> target)
            {
                // Грани мельче миллиметра: без множителя normalized вернул бы ноль.
                Vector3 normal = Vector3.Cross((b - a) * 100f, (c - a) * 100f).normalized;
                if (Vector3.Dot(normal, outward) < 0f)
                {
                    normal = -normal;
                    (b, d) = (d, b);
                }
                int start = Vertices.Count;
                foreach (Vector3 point in new[] { a, b, c, d })
                {
                    Vertices.Add(point);
                    Normals.Add(normal);
                    Uv.Add(new Vector2(point.x + point.z, point.y) * 4f);
                }
                target.Add(start); target.Add(start + 1); target.Add(start + 2);
                target.Add(start); target.Add(start + 2); target.Add(start + 3);
            }

            public void Fan(IList<Vector3> ring, Vector3 center, Vector3 outward, List<int> target)
            {
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector3 next = ring[(i + 1) % ring.Count];
                    Quad(center, ring[i], next, (ring[i] + next) * 0.5f, outward, target);
                }
            }

            public void Band(IList<Vector3> near, IList<Vector3> far, List<int> target)
            {
                Vector3 pivot = Vector3.zero;
                foreach (Vector3 point in near) pivot += point;
                foreach (Vector3 point in far) pivot += point;
                pivot /= near.Count + far.Count;
                for (int i = 0; i < near.Count; i++)
                {
                    int next = (i + 1) % near.Count;
                    Quad(near[i], far[i], far[next], near[next], near[i] + far[next] - pivot * 2f, target);
                }
            }
        }

        public static Mesh BuildMesh()
        {
            var builder = new Builder();
            float top = -ShackleHeight;
            Body(builder, top);
            Shackle(builder, top);
            Plate(builder, new Vector3(0f, top - BodyHeight * 0.48f, BodyDepth * 0.5f), 0.017f, 0.0015f, builder.Metal);
            Keyhole(builder, top - BodyHeight * 0.48f);
            foreach (float x in new[] { -0.036f, 0.036f })
                foreach (float y in new[] { top - 0.016f, top - BodyHeight + 0.012f })
                    Plate(builder, new Vector3(x, y, BodyDepth * 0.5f), 0.0035f, 0.0015f, builder.Metal);

            var mesh = new Mesh { name = "BarnPadlock" };
            mesh.SetVertices(builder.Vertices);
            mesh.SetNormals(builder.Normals);
            mesh.SetUVs(0, builder.Uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(builder.Metal, 0);
            mesh.SetTriangles(builder.Dark, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Контур корпуса: скруглённое дно и срезанные плечи, против часовой при взгляде спереди.
        private static List<Vector2> Outline(float top)
        {
            float w = BodyWidth * 0.5f, bottom = top - BodyHeight;
            var points = new List<Vector2>
            {
                new Vector2(w * 0.72f, top), new Vector2(-w * 0.72f, top),
                new Vector2(-w, top - 0.016f), new Vector2(-w, bottom + 0.022f)
            };
            for (int i = 1; i < 6; i++)
            {
                float angle = Mathf.PI + i / 6f * Mathf.PI;
                points.Add(new Vector2(Mathf.Cos(angle) * w, bottom + 0.022f + Mathf.Sin(angle) * 0.022f));
            }
            points.Add(new Vector2(w, bottom + 0.022f));
            points.Add(new Vector2(w, top - 0.016f));
            return points;
        }

        private static List<Vector3> Ring(List<Vector2> outline, float scale, float z, Vector2 center)
        {
            var ring = new List<Vector3>(outline.Count);
            foreach (Vector2 point in outline)
            {
                Vector2 shifted = center + (point - center) * scale;
                ring.Add(new Vector3(shifted.x, shifted.y, z));
            }
            return ring;
        }

        private static void Body(Builder builder, float top)
        {
            List<Vector2> outline = Outline(top);
            var center = new Vector2(0f, top - BodyHeight * 0.5f);
            float d = BodyDepth * 0.5f, inset = 1f - Bevel / (BodyWidth * 0.5f);
            List<Vector3> frontFace = Ring(outline, inset, d, center), front = Ring(outline, 1f, d - Bevel, center);
            List<Vector3> back = Ring(outline, 1f, Bevel - d, center), backFace = Ring(outline, inset, -d, center);
            builder.Fan(frontFace, new Vector3(center.x, center.y, d), Vector3.forward, builder.Metal);
            builder.Band(frontFace, front, builder.Metal);
            builder.Band(front, back, builder.Metal);
            builder.Band(back, backFace, builder.Metal);
            builder.Fan(backFace, new Vector3(center.x, center.y, -d), Vector3.back, builder.Metal);
        }

        // Труба по U: две ноги из корпуса и полукруг сверху.
        private static void Shackle(Builder builder, float top)
        {
            var path = new List<Vector3> { new Vector3(-ShackleSpan, top - 0.004f, 0f) };
            float legTop = -ShackleSpan;
            path.Add(new Vector3(-ShackleSpan, legTop, 0f));
            for (int i = 1; i < 8; i++)
            {
                float angle = Mathf.PI - i / 8f * Mathf.PI;
                path.Add(new Vector3(Mathf.Cos(angle) * ShackleSpan, legTop + Mathf.Sin(angle) * ShackleSpan, 0f));
            }
            path.Add(new Vector3(ShackleSpan, legTop, 0f));
            path.Add(new Vector3(ShackleSpan, top - 0.004f, 0f));

            List<Vector3> previous = null;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(tangent, Vector3.forward).normalized;
                var ring = new List<Vector3>(8);
                for (int k = 0; k < 8; k++)
                {
                    float angle = k / 8f * Mathf.PI * 2f;
                    ring.Add(path[i] + (side * Mathf.Cos(angle) + Vector3.forward * Mathf.Sin(angle)) * ShackleRadius);
                }
                if (previous != null) builder.Band(ring, previous, builder.Metal);
                previous = ring;
            }
        }

        // Низкая круглая шайба на лице корпуса.
        private static void Plate(Builder builder, Vector3 center, float radius, float height, List<int> target)
        {
            var low = new List<Vector3>(12);
            var high = new List<Vector3>(12);
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                low.Add(center + offset);
                high.Add(center + offset + Vector3.forward * height);
            }
            builder.Band(high, low, target);
            builder.Fan(high, center + Vector3.forward * height, Vector3.forward, target);
        }

        // Скважина: круг и щель вниз.
        private static void Keyhole(Builder builder, float y)
        {
            float z = BodyDepth * 0.5f + 0.0018f;
            Plate(builder, new Vector3(0f, y + 0.003f, z), 0.0048f, 0.0002f, builder.Dark);
            builder.Quad(new Vector3(0.0022f, y + 0.002f, z + 0.0002f), new Vector3(-0.0022f, y + 0.002f, z + 0.0002f),
                new Vector3(-0.0022f, y - 0.011f, z + 0.0002f), new Vector3(0.0022f, y - 0.011f, z + 0.0002f), Vector3.forward, builder.Dark);
        }
    }
}
