#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ClapClapArmsImporter
{
    [Serializable] private sealed class Node { public long id; public string name; public int parent; public Vector3 position, scale; public Quaternion rotation; }
    [Serializable] private sealed class Matrix { public float[] values; }
    [Serializable] private sealed class Submesh { public int[] indices; }
    [Serializable] private sealed class Weight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
        public BoneWeight ToBoneWeight() => new BoneWeight { boneIndex0 = boneIndex0, boneIndex1 = boneIndex1, boneIndex2 = boneIndex2, boneIndex3 = boneIndex3, weight0 = weight0, weight1 = weight1, weight2 = weight2, weight3 = weight3 };
    }
    [Serializable] private sealed class Data
    {
        public Node[] nodes; public long rendererId; public long[] boneIds;
        public Vector3[] vertices, normals; public Vector2[] uv; public Weight[] weights;
        public Matrix[] bindPoses; public Submesh[] submeshes;
    }

    public static string Import(string sourceDirectory)
    {
        const string folder = "Assets/Characters/ClapClapArms";
        if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("ClapClapArms already exists; refusing to overwrite.");
        Data data = JsonUtility.FromJson<Data>(File.ReadAllText(Path.Combine(sourceDirectory, "arms.json")));
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(sourceDirectory, "Hands-BaseColor.png"), folder + "/Hands-BaseColor.png");
        AssetDatabase.Refresh();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Hands-BaseColor.png");
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Clap Clap Arms" };
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Smoothness", 0f);
        AssetDatabase.CreateAsset(material, folder + "/Arms.mat");
        var mesh = new Mesh { name = "Clap Clap Arms", vertices = data.vertices, normals = data.normals, uv = data.uv, boneWeights = data.weights.Select(w => w.ToBoneWeight()).ToArray() };
        mesh.bindposes = data.bindPoses.Select(value =>
        {
            Matrix4x4 matrix = new Matrix4x4();
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) matrix[row, column] = value.values[row * 4 + column];
            return matrix;
        }).ToArray();
        mesh.subMeshCount = data.submeshes.Length;
        for (int i = 0; i < data.submeshes.Length; i++) mesh.SetTriangles(data.submeshes[i].indices, i);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, folder + "/Arms.asset");
        var nodes = new Transform[data.nodes.Length];
        try
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                Node node = data.nodes[i]; nodes[i] = new GameObject(node.name).transform;
                if (node.parent >= 0) nodes[i].SetParent(nodes[node.parent], false);
                nodes[i].localPosition = node.position; nodes[i].localRotation = node.rotation; nodes[i].localScale = node.scale;
            }
            var lookup = data.nodes.Select((node, index) => new { node.id, transform = nodes[index] }).ToDictionary(x => x.id, x => x.transform);
            var renderer = lookup[data.rendererId].gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
            renderer.bones = data.boneIds.Select(id => lookup[id]).ToArray(); renderer.rootBone = nodes[0];
            renderer.updateWhenOffscreen = true;
            string path = folder + "/ClapClapArms.prefab";
            PrefabUtility.SaveAsPrefabAsset(nodes[0].gameObject, path);
            AssetDatabase.SaveAssets();
            return path;
        }
        finally { if (nodes[0] != null) UnityEngine.Object.DestroyImmediate(nodes[0].gameObject); }
    }

    public static void RestoreWeights(string sourceDirectory)
    {
        Data data = JsonUtility.FromJson<Data>(File.ReadAllText(Path.Combine(sourceDirectory, "arms.json")));
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Characters/ClapClapArms/Arms.asset");
        mesh.boneWeights = data.weights.Select(w => w.ToBoneWeight()).ToArray();
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
    }
}
#endif
