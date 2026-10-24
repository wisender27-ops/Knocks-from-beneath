#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PiePortionMeshBuilder
{
    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
        public Vector4 Tangent;

        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            return new Vertex
            {
                Position = Vector3.LerpUnclamped(a.Position, b.Position, t),
                Normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized,
                UV = Vector2.LerpUnclamped(a.UV, b.UV, t),
                Tangent = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t)
            };
        }
    }

    // Order: +X/+Y, -X/+Y, -X/-Y, +X/-Y. Cut faces use the final submesh.
    public static Mesh[] Bake(Mesh source, string outputFolder)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        outputFolder = outputFolder?.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(outputFolder) || !outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException("Output must be a folder inside Assets.", nameof(outputFolder));

        var sourceTriangles = new List<int[]>();
        Vertex[] sourceVertices;
        bool hasNormals;
        bool hasTangents;
        using (var meshData = MeshUtility.AcquireReadOnlyMeshData(source))
        {
            var data = meshData[0];
            hasNormals = data.HasVertexAttribute(VertexAttribute.Normal);
            hasTangents = data.HasVertexAttribute(VertexAttribute.Tangent);
            using (var positions = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            using (var normals = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
            using (var uvs = new NativeArray<Vector2>(data.vertexCount, Allocator.Temp))
            using (var tangents = new NativeArray<Vector4>(data.vertexCount, Allocator.Temp))
            {
                data.GetVertices(positions);
                if (hasNormals) data.GetNormals(normals);
                if (data.HasVertexAttribute(VertexAttribute.TexCoord0)) data.GetUVs(0, uvs);
                if (hasTangents) data.GetTangents(tangents);
                sourceVertices = new Vertex[data.vertexCount];
                for (int i = 0; i < data.vertexCount; i++)
                    sourceVertices[i] = new Vertex { Position = positions[i], Normal = normals[i], UV = uvs[i], Tangent = tangents[i] };
            }
            for (int submesh = 0; submesh < data.subMeshCount; submesh++)
            {
                var descriptor = data.GetSubMesh(submesh);
                if (descriptor.topology != MeshTopology.Triangles)
                    throw new ArgumentException("Pie source must use triangle submeshes.", nameof(source));
                using (var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp))
                {
                    data.GetIndices(indices, submesh, true);
                    sourceTriangles.Add(indices.ToArray());
                }
            }
        }

        EnsureFolder(outputFolder);
        var result = new Mesh[4];
        int[] xSigns = { 1, -1, -1, 1 };
        int[] ySigns = { 1, 1, -1, -1 };
        float epsilon = Mathf.Max(source.bounds.size.magnitude * 0.00001f, 0.000000001f);
        for (int part = 0; part < result.Length; part++)
        {
            Mesh mesh = Build(sourceVertices, sourceTriangles, xSigns[part], ySigns[part], epsilon, hasNormals, hasTangents);
            mesh.name = "PiePortion_" + (part + 1);
            string path = outputFolder + "/" + mesh.name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                result[part] = existing;
            }
            else
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                    throw new InvalidOperationException("A non-mesh asset already exists at " + path);
                }
                AssetDatabase.CreateAsset(mesh, path);
                result[part] = mesh;
            }
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static Mesh Build(Vertex[] source, List<int[]> sourceTriangles, int xSign, int ySign,
        float epsilon, bool hasNormals, bool hasTangents)
    {
        var vertices = new List<Vertex>();
        var triangles = new List<int>[sourceTriangles.Count + 1];
        var xBoundary = new List<Vector3>();
        var yBoundary = new List<Vector3>();
        for (int submesh = 0; submesh < sourceTriangles.Count; submesh++)
        {
            triangles[submesh] = new List<int>();
            int[] indices = sourceTriangles[submesh];
            for (int i = 0; i < indices.Length; i += 3)
            {
                var polygon = new List<Vertex> { source[indices[i]], source[indices[i + 1]], source[indices[i + 2]] };
                polygon = Clip(polygon, 0, xSign);
                polygon = Clip(polygon, 1, ySign);
                if (polygon.Count < 3) continue;
                foreach (var vertex in polygon)
                {
                    if (Mathf.Abs(vertex.Position.x) <= epsilon) xBoundary.Add(vertex.Position);
                    if (Mathf.Abs(vertex.Position.y) <= epsilon) yBoundary.Add(vertex.Position);
                }
                for (int p = 1; p < polygon.Count - 1; p++)
                    AddTriangle(vertices, triangles[submesh], polygon[0], polygon[p], polygon[p + 1], epsilon);
            }
        }
        triangles[sourceTriangles.Count] = new List<int>();
        AddCap(vertices, triangles[sourceTriangles.Count], xBoundary, 0, -xSign, epsilon);
        AddCap(vertices, triangles[sourceTriangles.Count], yBoundary, 1, -ySign, epsilon);

        var positions = new List<Vector3>(vertices.Count);
        var normals = new List<Vector3>(vertices.Count);
        var uvs = new List<Vector2>(vertices.Count);
        var tangents = new List<Vector4>(vertices.Count);
        foreach (var vertex in vertices)
        {
            positions.Add(vertex.Position);
            normals.Add(vertex.Normal);
            uvs.Add(vertex.UV);
            tangents.Add(vertex.Tangent);
        }
        var mesh = new Mesh { indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        if (hasTangents) mesh.SetTangents(tangents);
        mesh.subMeshCount = triangles.Length;
        for (int submesh = 0; submesh < triangles.Length; submesh++)
            mesh.SetTriangles(triangles[submesh], submesh, false);
        if (!hasNormals) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Vertex> Clip(List<Vertex> input, int axis, int sign)
    {
        var output = new List<Vertex>();
        if (input.Count == 0) return output;
        Vertex previous = input[input.Count - 1];
        float previousDistance = previous.Position[axis] * sign;
        foreach (var current in input)
        {
            float currentDistance = current.Position[axis] * sign;
            if ((currentDistance >= 0f) != (previousDistance >= 0f))
            {
                Vertex intersection = Vertex.Lerp(previous, current, previousDistance / (previousDistance - currentDistance));
                intersection.Position[axis] = 0f;
                output.Add(intersection);
            }
            if (currentDistance >= 0f) output.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }
        return output;
    }

    private static void AddTriangle(List<Vertex> vertices, List<int> indices, Vertex a, Vertex b, Vertex c, float epsilon)
    {
        if (Vector3.Cross(b.Position - a.Position, c.Position - a.Position).sqrMagnitude <= epsilon * epsilon * epsilon * epsilon)
            return;
        int first = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        indices.Add(first);
        indices.Add(first + 1);
        indices.Add(first + 2);
    }

    private static Vector2 Project(Vector3 point, int axis)
    {
        return axis == 0 ? new Vector2(point.y, point.z) : new Vector2(point.z, point.x);
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;
        return ab.x * ac.y - ab.y * ac.x;
    }

    private static void AddCap(List<Vertex> vertices, List<int> indices, List<Vector3> boundary, int axis, int sign, float epsilon)
    {
        boundary.Sort((a, b) =>
        {
            Vector2 pa = Project(a, axis), pb = Project(b, axis);
            int comparison = pa.x.CompareTo(pb.x);
            return comparison != 0 ? comparison : pa.y.CompareTo(pb.y);
        });
        var unique = new List<Vector3>();
        foreach (Vector3 point in boundary)
            if (unique.Count == 0 || (point - unique[unique.Count - 1]).sqrMagnitude > epsilon * epsilon)
                unique.Add(point);
        if (unique.Count < 3) return;

        // The cake is convex. A monotone hull removes duplicate seam vertices and
        // collinear triangle intersections before triangulating each closed cut.
        var hull = new List<Vector3>();
        foreach (Vector3 point in unique)
        {
            while (hull.Count >= 2 && Cross(Project(hull[hull.Count - 2], axis), Project(hull[hull.Count - 1], axis), Project(point, axis)) <= epsilon * epsilon)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }
        int lowerCount = hull.Count;
        for (int i = unique.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount && Cross(Project(hull[hull.Count - 2], axis), Project(hull[hull.Count - 1], axis), Project(unique[i], axis)) <= epsilon * epsilon)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(unique[i]);
        }
        hull.RemoveAt(hull.Count - 1);
        Vector3 normal = axis == 0 ? Vector3.right * sign : Vector3.up * sign;
        for (int i = 1; i < hull.Count - 1; i++)
        {
            Vector3 a = hull[0], b = hull[i], c = hull[i + 1];
            if (sign < 0) { Vector3 swap = b; b = c; c = swap; }
            AddTriangle(vertices, indices,
                new Vertex { Position = a, Normal = normal, UV = Project(a, axis) },
                new Vertex { Position = b, Normal = normal, UV = Project(b, axis) },
                new Vertex { Position = c, Normal = normal, UV = Project(c, axis) }, epsilon);
        }
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string child = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(current, parts[i]);
            current = child;
        }
    }
}
#endif
