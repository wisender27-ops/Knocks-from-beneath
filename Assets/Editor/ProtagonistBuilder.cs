#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnocksFromBeneath;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ProtagonistBuilder
{
    public const string Folder = "Assets/Characters/Protagonist";
    private const string PrefabPath = Folder + "/Protagonist.prefab";
    private static readonly string[] Names = { "Hips", "Spine", "Chest", "Neck", "Head",
        "LeftUpperArm", "LeftForearm", "LeftHand", "RightUpperArm", "RightForearm", "RightHand",
        "LeftThigh", "LeftShin", "LeftFoot", "RightThigh", "RightShin", "RightFoot" };
    private static readonly int[] Parents = { -1, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 0, 11, 12, 0, 14, 15 };
    private static readonly Vector3[] Positions = {
        new Vector3(0, 0.98f, 0), new Vector3(0, 1.17f, 0), new Vector3(0, 1.37f, 0),
        new Vector3(0, 1.51f, 0), new Vector3(0, 1.63f, 0),
        new Vector3(-0.245f, 1.425f, 0), new Vector3(-0.31f, 1.135f, 0.015f), new Vector3(-0.35f, 0.885f, 0.025f),
        new Vector3(0.245f, 1.425f, 0), new Vector3(0.31f, 1.135f, 0.015f), new Vector3(0.35f, 0.885f, 0.025f),
        new Vector3(-0.115f, 0.945f, 0), new Vector3(-0.12f, 0.535f, 0.015f), new Vector3(-0.12f, 0.105f, 0),
        new Vector3(0.115f, 0.945f, 0), new Vector3(0.12f, 0.535f, 0.015f), new Vector3(0.12f, 0.105f, 0) };

    private sealed class Geometry
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<BoneWeight> weights = new List<BoneWeight>();
        private readonly List<int> triangles = new List<int>();
        private readonly System.Random random = new System.Random(149);

        public void Face(Vector3 a, Vector3 b, Vector3 c, int tile, int bone,
            int other = -1, float blend = 0f)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            int tx = tile % 4, ty = tile / 4;
            float x = (tx * 64 + random.Next(4, 45)) / 256f;
            float y = (ty * 64 + random.Next(4, 45)) / 256f;
            uv.Add(new Vector2(x, y)); uv.Add(new Vector2(x + 12f / 256f, y));
            uv.Add(new Vector2(x + 5f / 256f, y + 14f / 256f));
            var w = new BoneWeight { boneIndex0 = bone, weight0 = 1f - blend,
                boneIndex1 = other < 0 ? bone : other, weight1 = blend };
            weights.Add(w); weights.Add(w); weights.Add(w);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int tile, int bone)
        { Face(a, b, c, tile, bone); Face(a, c, d, tile, bone); }

        private void SkinFace(Vector3 a, Vector3 b, Vector3 c, int tile, int boneA, int boneB, int boneC)
        {
            Face(a, b, c, tile, boneA);
            weights[weights.Count - 2] = new BoneWeight { boneIndex0 = boneB, weight0 = 1f };
            weights[weights.Count - 1] = new BoneWeight { boneIndex0 = boneC, weight0 = 1f };
        }

        public void Rings(Vector3[] centers, Vector2[] radii, int tile, int[] bones,
            int sides = 10, float opening = 0f)
        {
            var rings = new Vector3[centers.Length][];
            for (int j = 0; j < centers.Length; j++)
            {
                rings[j] = new Vector3[sides + 1];
                for (int i = 0; i <= sides; i++)
                {
                    float angle = opening + i * (Mathf.PI * 2f - opening * 2f) / sides;
                    rings[j][i] = centers[j] + new Vector3(Mathf.Sin(angle) * radii[j].x, 0f, Mathf.Cos(angle) * radii[j].y);
                }
            }
            for (int j = 0; j < centers.Length - 1; j++)
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = rings[j][i], b = rings[j + 1][i], c = rings[j + 1][i + 1], d = rings[j][i + 1];
                    SkinFace(a, c, b, tile, bones[j], bones[j + 1], bones[j + 1]);
                    SkinFace(a, d, c, tile, bones[j], bones[j], bones[j + 1]);
                }
            if (opening == 0f)
                for (int i = 0; i < sides; i++)
                {
                    Face(centers[0], rings[0][i + 1], rings[0][i], tile, bones[0]);
                    int end = centers.Length - 1;
                    Face(centers[end], rings[end][i], rings[end][i + 1], tile, bones[end]);
                }
        }

        public void Ellipsoid(Vector3 center, Vector3 radius, int tile, int bone, int sides = 10, int rows = 5)
        {
            var centers = new Vector3[rows + 1]; var radii = new Vector2[rows + 1]; var bones = new int[rows + 1];
            for (int j = 0; j <= rows; j++)
            {
                float angle = j * Mathf.PI / rows;
                centers[j] = center + Vector3.up * (-Mathf.Cos(angle) * radius.y);
                radii[j] = new Vector2(Mathf.Sin(angle) * radius.x, Mathf.Sin(angle) * radius.z);
                bones[j] = bone;
            }
            Rings(centers, radii, tile, bones, sides);
        }

        public void Tube(Vector3 from, Vector3 to, float radius, int tile, int bone, int sides = 6)
        {
            Vector3 direction = (to - from).normalized;
            Vector3 u = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(direction, u).normalized;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 p = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius;
                Vector3 q = (u * Mathf.Cos(b) + v * Mathf.Sin(b)) * radius;
                Quad(from + p, from + q, to + q, to + p, tile, bone);
                Face(to, to + p, to + q, tile, bone);
            }
        }

        public Mesh Bake(string name, Transform root, Transform[] bones)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
    }

    [MenuItem("Tools/Knocks From Beneath/Protagonist/Build Assets")]
    public static void BuildAssets()
    {
        Directory.CreateDirectory(Folder + "/Animations");
        AssetDatabase.Refresh();
        var hero = new GameObject("Protagonist");
        try
        {
            Transform[] bones = BuildSkeleton(hero.transform);
            Material material = BuildMaterial();
            var body = new Geometry(); var head = new Geometry();
            BuildBody(body); BuildHead(head);
            Mesh bodyMesh = Save(body.Bake("Protagonist Body", hero.transform, bones), Folder + "/Body.asset");
            Mesh headMesh = Save(head.Bake("Protagonist Head", hero.transform, bones), Folder + "/Head.asset");
            Skin(hero.transform, "Body", bodyMesh, material, bones);
            SkinnedMeshRenderer headRenderer = Skin(hero.transform, "HeadMesh", headMesh, material, bones);
            var animator = hero.AddComponent<Animator>();
            animator.runtimeAnimatorController = BuildAnimator(hero.transform, bones);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            hero.AddComponent<ProtagonistBody>().Configure(animator, headRenderer, bones);
            PrefabUtility.SaveAsPrefabAsset(hero, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Protagonist assets: " + (bodyMesh.vertexCount + headMesh.vertexCount) / 3 + " triangles; 17 bones; one 256px atlas.");
        }
        finally { UnityEngine.Object.DestroyImmediate(hero); }
    }

    [MenuItem("Tools/Knocks From Beneath/Protagonist/Add To Player")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Install outside Play Mode.");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null) throw new InvalidOperationException("PlayerController missing in active scene.");
        if (player.GetComponentInChildren<ProtagonistBody>() != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Build assets first.");
        var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        Undo.RegisterCreatedObjectUndo(hero, "Add protagonist");
        hero.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        hero.transform.localRotation = Quaternion.identity;
        hero.transform.localScale = Vector3.one * player.standingHeight / 1.8f;
        SetLayer(hero.transform, player.gameObject.layer);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = hero;
    }

    private static void SetLayer(Transform t, int layer)
    { t.gameObject.layer = layer; foreach (Transform c in t) SetLayer(c, layer); }

    public static string RenderPreview()
    {
        string output = Path.GetFullPath(".codex/verification/hero");
        Directory.CreateDirectory(output);
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        try
        {
            var hero = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hero, scene);
            SetLayer(hero.transform, 31);
            hero.GetComponent<Animator>().enabled = false;
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Idle.anim").SampleAnimation(hero, 0f);
            var lightObject = new GameObject("Preview Key");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2.2f; light.color = new Color(1f, .91f, .79f); light.cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            var fillObject = new GameObject("Preview Fill");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fillObject, scene);
            var fill = fillObject.AddComponent<Light>(); fill.type = LightType.Directional;
            fill.intensity = .9f; fill.color = new Color(.67f, .78f, 1f); fill.cullingMask = 1 << 31;
            fill.transform.rotation = Quaternion.Euler(15f, -45f, 0f);
            var cameraObject = new GameObject("Preview Camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .049f, .043f);
            camera.orthographic = true; camera.orthographicSize = 1.04f;
            camera.nearClipPlane = .05f; camera.farClipPlane = 12f;
            target = new RenderTexture(720, 900, 24); camera.targetTexture = target;
            string[] views = { "front", "side", "back" };
            for (int i = 0; i < views.Length; i++)
            {
                float angle = i == 0 ? 0f : i == 1 ? 90f : 180f;
                camera.transform.position = Quaternion.Euler(0, angle, 0) * new Vector3(0f, 0f, 4f) + Vector3.up * .91f;
                camera.transform.LookAt(new Vector3(0f, .91f, 0f));
                camera.Render();
                camera.Render();
                var texture = new Texture2D(720, 900, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 720, 900), 0, 0); texture.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(output + "/" + views[i] + ".png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return output;
    }

    private static Transform[] BuildSkeleton(Transform root)
    {
        var bones = new Transform[Names.Length];
        var rig = new GameObject("Rig").transform; rig.SetParent(root, false);
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i] = new GameObject(Names[i]).transform;
            bones[i].SetParent(Parents[i] < 0 ? rig : bones[Parents[i]], false);
            bones[i].position = root.TransformPoint(Positions[i]);
        }
        return bones;
    }

    private static SkinnedMeshRenderer Skin(Transform root, string name, Mesh mesh, Material mat, Transform[] bones)
    {
        var child = new GameObject(name); child.transform.SetParent(root, false);
        var skin = child.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = mesh; skin.sharedMaterial = mat; skin.bones = bones; skin.rootBone = bones[0];
        skin.localBounds = new Bounds(new Vector3(0f, 0.90f, 0f), new Vector3(1.7f, 2.15f, 1.7f));
        skin.updateWhenOffscreen = true; skin.quality = SkinQuality.Bone2;
        skin.shadowCastingMode = ShadowCastingMode.On; skin.receiveShadows = true;
        return skin;
    }

    private static T Save<T>(T asset, string path) where T : UnityEngine.Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
        EditorUtility.CopySerialized(asset, existing);
        UnityEngine.Object.DestroyImmediate(asset); return existing;
    }

    private static Material BuildMaterial()
    {
        var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
        var random = new System.Random(7281);
        Color[] palette = { new Color(.22f,.195f,.165f), new Color(.235f,.265f,.32f),
            new Color(.51f,.48f,.41f), new Color(.60f,.44f,.32f), new Color(.105f,.085f,.07f),
            new Color(.33f,.29f,.23f), new Color(.23f,.205f,.17f), new Color(.14f,.125f,.11f),
            new Color(.065f,.055f,.05f), new Color(.42f,.28f,.21f), new Color(.33f,.28f,.22f),
            new Color(.56f,.48f,.37f), new Color(.72f,.65f,.50f), new Color(.10f,.085f,.075f),
            new Color(.14f,.17f,.22f), new Color(.39f,.37f,.31f) };
        for (int tile = 0; tile < 16; tile++)
            for (int y = 0; y < 64; y += 2)
                for (int x = 0; x < 64; x += 2)
                {
                    float noise = (float)random.NextDouble() * .22f - .11f;
                    float weave = (x % 6 == 0 ? .02f : 0f) - (y % 8 == 0 ? .015f : 0f);
                    Color color = palette[tile] * (1f + noise + weave);
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                        texture.SetPixel(tile % 4 * 64 + x + dx, tile / 4 * 64 + y + dy, color);
                }
        texture.Apply();
        string path = Folder + "/ProtagonistAtlas.png";
        File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256; importer.SaveAndReimport();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader unavailable.");
        var mat = new Material(shader) { name = "Protagonist Fabric" };
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", .08f); mat.SetFloat("_Metallic", 0f);
        return Save(mat, Folder + "/Protagonist.mat");
    }

    private static void BuildBody(Geometry g)
    {
        // Слой футболки находится внутри раскрытой куртки; оба скиннятся теми же костями.
        g.Rings(new[] { new Vector3(0,.96f,0), new Vector3(0,1.15f,0), new Vector3(0,1.34f,0), new Vector3(0,1.445f,0) },
            new[] { new Vector2(.165f,.105f), new Vector2(.17f,.11f), new Vector2(.195f,.11f), new Vector2(.13f,.085f) },
            2, new[] { 0,1,2,2 }, 12);
        g.Rings(new[] { new Vector3(0,.93f,0), new Vector3(0,1.01f,0), new Vector3(0,1.18f,0), new Vector3(0,1.37f,0), new Vector3(0,1.43f,0) },
            new[] { new Vector2(.205f,.125f), new Vector2(.211f,.13f), new Vector2(.21f,.14f), new Vector2(.242f,.14f), new Vector2(.205f,.115f) },
            0, new[] { 0,0,1,2,2 }, 14, .39f);
        g.Rings(new[] { new Vector3(0,.835f,0),new Vector3(0,.96f,0),new Vector3(0,.985f,0) },
            new[] { new Vector2(.18f,.11f),new Vector2(.18f,.105f),new Vector2(.165f,.095f) }, 1, new[] {0,0,0},12);
        g.Rings(new[] { new Vector3(0,1.43f,0),new Vector3(0,1.55f,0) },
            new[] { new Vector2(.065f,.06f),new Vector2(.059f,.054f) },3,new[] {3,3},10);
        g.Rings(new[] { new Vector3(0,1.435f,0),new Vector3(0,1.45f,0) },
            new[] { new Vector2(.085f,.08f),new Vector2(.083f,.077f) },15,new[] {2,2},12);

        for (int side = -1; side <= 1; side += 2)
        {
            int arm = side < 0 ? 5 : 8, leg = side < 0 ? 11 : 14;
            g.Rings(new[] { new Vector3(side*.235f,1.43f,0),new Vector3(side*.275f,1.30f,.006f),
                new Vector3(side*.31f,1.14f,.015f),new Vector3(side*.335f,1.00f,.02f),new Vector3(side*.35f,.885f,.025f) },
                new[] {new Vector2(.099f,.107f),new Vector2(.089f,.09f),new Vector2(.076f,.076f),new Vector2(.065f,.07f),new Vector2(.052f,.055f)},
                0,new[] {arm,arm,arm,arm+1,arm+1},10);
            g.Rings(new[] {new Vector3(side*.349f,.875f,.025f),new Vector3(side*.345f,.923f,.025f)},
                new[] {new Vector2(.053f,.057f),new Vector2(.058f,.06f)},7,new[] {arm+1,arm+1},10);
            g.Ellipsoid(new Vector3(side*.356f,.833f,.028f),new Vector3(.041f,.062f,.024f),3,arm+2,8,4);
            for (int finger = 0; finger < 4; finger++)
            {
                float x = side * (.329f + finger * .016f);
                float length = finger == 0 || finger == 3 ? .037f : .047f;
                Vector3 start = new Vector3(x,.803f,.03f);
                Vector3 knuckle = start + new Vector3(0,-length*.65f,.009f);
                g.Tube(start,knuckle,.009f,3,arm+2);
                g.Tube(knuckle,knuckle+new Vector3(-side*.004f,-length*.35f,.012f),.008f,3,arm+2);
            }
            g.Tube(new Vector3(side*.324f,.846f,.03f),new Vector3(side*.31f,.821f,.056f),.013f,3,arm+2);
            g.Tube(new Vector3(side*.31f,.821f,.056f),new Vector3(side*.318f,.80f,.06f),.011f,3,arm+2);

            g.Rings(new[] {new Vector3(side*.112f,.927f,0),new Vector3(side*.115f,.79f,0),new Vector3(side*.12f,.56f,.015f),
                new Vector3(side*.12f,.49f,.01f),new Vector3(side*.12f,.285f,0),new Vector3(side*.12f,.105f,0)},
                new[] {new Vector2(.096f,.105f),new Vector2(.091f,.10f),new Vector2(.075f,.084f),new Vector2(.077f,.081f),new Vector2(.067f,.068f),new Vector2(.079f,.072f)},
                1,new[] {leg,leg,leg,leg+1,leg+1,leg+1},10);
            g.Rings(new[] {new Vector3(side*.12f,.10f,0),new Vector3(side*.12f,.135f,0)},
                new[] {new Vector2(.08f,.073f),new Vector2(.076f,.071f)},14,new[] {leg+1,leg+1},10);
            g.Rings(new[] {new Vector3(side*.12f,.012f,.055f),new Vector3(side*.12f,.043f,.055f),new Vector3(side*.12f,.07f,.055f)},
                new[] {new Vector2(.087f,.17f),new Vector2(.092f,.173f),new Vector2(.088f,.162f)},5,new[] {leg+2,leg+2,leg+2},12);
            g.Rings(new[] {new Vector3(side*.12f,.055f,.055f),new Vector3(side*.12f,.095f,.06f),new Vector3(side*.12f,.145f,0)},
                new[] {new Vector2(.087f,.16f),new Vector2(.078f,.142f),new Vector2(.065f,.067f)},6,new[] {leg+2,leg+2,leg+2},12);
            for (int lace=0;lace<4;lace++)
            {
                float z=.048f+lace*.025f,y=.137f-lace*.009f;
                g.Tube(new Vector3(side*.12f-.045f,y,z),new Vector3(side*.12f+.045f,y-.003f,z+.009f),.004f,11,leg+2,4);
            }

            // Карманы, молния, шнурки капюшона и швы добавляют читаемый силуэт без отдельных материалов.
            g.Quad(new Vector3(side*.115f,1.14f,.129f),new Vector3(side*.18f,1.13f,.102f),
                new Vector3(side*.18f,1.01f,.106f),new Vector3(side*.113f,1.013f,.13f),7,1);
            g.Tube(new Vector3(side*.082f,.951f,.123f),new Vector3(side*.089f,1.38f,.132f),.006f,10,1);
            g.Tube(new Vector3(side*.087f,1.43f,.113f),new Vector3(side*.092f,1.24f,.143f),.004f,11,2);
            g.Tube(new Vector3(side*.094f,1.245f,.143f),new Vector3(side*.094f,1.225f,.143f),.006f,7,2);
            g.Quad(new Vector3(side*.07f,.885f,-.102f),new Vector3(side*.16f,.885f,-.075f),
                new Vector3(side*.16f,.79f,-.08f),new Vector3(side*.07f,.79f,-.105f),14,leg);
        }
        // Капюшон лежит на спине, а не повторяет форму головы.
        g.Rings(new[] {new Vector3(0,1.27f,-.08f),new Vector3(0,1.35f,-.10f),new Vector3(0,1.46f,-.063f),new Vector3(0,1.50f,-.055f)},
            new[] {new Vector2(.09f,.055f),new Vector2(.155f,.095f),new Vector2(.142f,.083f),new Vector2(.077f,.062f)},0,new[] {2,2,2,2},12,.85f);
    }

    private static void BuildHead(Geometry g)
    {
        g.Rings(new[] {new Vector3(0,1.525f,.018f),new Vector3(0,1.55f,.016f),new Vector3(0,1.60f,.01f),
            new Vector3(0,1.665f,0),new Vector3(0,1.72f,-.005f),new Vector3(0,1.755f,-.009f)},
            new[] {new Vector2(.045f,.063f),new Vector2(.07f,.078f),new Vector2(.087f,.087f),
                new Vector2(.087f,.092f),new Vector2(.079f,.081f),new Vector2(.048f,.055f)},3,new[] {4,4,4,4,4,4},12);
        for(int side=-1;side<=1;side+=2)
        {
            g.Ellipsoid(new Vector3(side*.09f,1.634f,0),new Vector3(.019f,.031f,.021f),3,4,6,4);
            g.Ellipsoid(new Vector3(side*.102f,1.634f,.009f),new Vector3(.004f,.016f,.011f),9,4,6,3);
            // Узкие глаза под тяжёлыми бровями вместо крупных мультяшных глаз.
            float x=side*.035f;
            g.Quad(new Vector3(x-.019f,1.66f,.082f),new Vector3(x-.019f,1.649f,.086f),
                new Vector3(x+.018f,1.649f,.086f),new Vector3(x+.018f,1.66f,.082f),9,4);
            g.Quad(new Vector3(x-.012f,1.655f,.087f),new Vector3(x-.012f,1.65f,.089f),
                new Vector3(x+.012f,1.65f,.089f),new Vector3(x+.012f,1.655f,.087f),11,4);
            g.Quad(new Vector3(x-.004f,1.657f,.091f),new Vector3(x-.004f,1.649f,.092f),
                new Vector3(x+.004f,1.649f,.092f),new Vector3(x+.004f,1.657f,.091f),8,4);
            g.Tube(new Vector3(x-.021f,1.67f,.083f),new Vector3(x+.021f,1.674f,.08f),.005f,4,4,4);
        }
        Vector3 tip = new Vector3(0,1.624f,.127f);
        g.Face(new Vector3(-.013f,1.659f,.086f),new Vector3(-.017f,1.621f,.089f),tip,3,4);
        g.Face(new Vector3(.017f,1.621f,.089f),new Vector3(.013f,1.659f,.086f),tip,3,4);
        g.Face(new Vector3(-.017f,1.621f,.089f),new Vector3(.017f,1.621f,.089f),tip,9,4);
        g.Tube(new Vector3(-.024f,1.587f,.087f),new Vector3(.024f,1.588f,.087f),.0028f,9,4,4);
        g.Tube(new Vector3(-.019f,1.579f,.088f),new Vector3(.019f,1.58f,.088f),.003f,3,4,4);
        g.Rings(new[] {new Vector3(0,1.613f,-.012f),new Vector3(0,1.733f,-.007f),new Vector3(0,1.775f,-.01f),new Vector3(0,1.794f,-.012f)},
            new[] {new Vector2(.092f,.096f),new Vector2(.098f,.098f),new Vector2(.079f,.078f),new Vector2(.025f,.03f)},4,new[] {4,4,4,4},12,.84f);
        var random=new System.Random(213);
        for(int i=0;i<52;i++)
        {
            float angle=(float)random.NextDouble()*Mathf.PI*2f;
            float y=1.70f+(float)random.NextDouble()*.08f;
            float radius=Mathf.Lerp(.098f,.067f,(y-1.70f)/.08f);
            Vector3 center=new Vector3(Mathf.Sin(angle)*radius,y,Mathf.Cos(angle)*radius-.012f);
            Vector3 tangent=new Vector3(Mathf.Cos(angle),0,-Mathf.Sin(angle))*.019f;
            Vector3 end=center+new Vector3(Mathf.Sin(angle)*.008f,-.031f-(float)random.NextDouble()*.024f,Mathf.Cos(angle)*.014f);
            Vector3 peak=center+new Vector3(0,.013f,0);
            g.Face(center-tangent,peak,end,4,4);g.Face(peak,center+tangent,end,4,4);
            g.Face(center+tangent,center-tangent,end,4,4);
        }
        // Чёлка оставляет лицо открытым, но делает причёску неровной как на референсе.
        for(int i=0;i<7;i++)
        {
            float x=-.068f+i*.021f;
            Vector3 a=new Vector3(x,1.739f,.07f),b=new Vector3(x+.022f,1.742f,.071f);
            Vector3 end=new Vector3(x+.005f,1.681f+(i%3)*.008f,.088f);
            g.Face(a,end,b,4,4);g.Face(a,b,new Vector3(x+.01f,1.766f,.061f),4,4);
        }
    }

    private static AnimatorController BuildAnimator(Transform root, Transform[] bones)
    {
        string path=Folder+"/Protagonist.controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller!=null) return controller;
        controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("MoveX",AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveZ",AnimatorControllerParameterType.Float);
        controller.AddParameter("Crouch",AnimatorControllerParameterType.Float);
        var standing=LocomotionTree(controller,root,bones,false);
        var crouching=LocomotionTree(controller,root,bones,true);
        var blend=new BlendTree {name="Standing Crouching",blendType=BlendTreeType.Simple1D,blendParameter="Crouch",useAutomaticThresholds=false};
        AssetDatabase.AddObjectToAsset(blend,controller);
        blend.AddChild(standing,0f);blend.AddChild(crouching,1f);
        AnimatorState state=controller.layers[0].stateMachine.AddState("Locomotion");
        state.motion=blend;controller.layers[0].stateMachine.defaultState=state;
        return controller;
    }

    private static BlendTree LocomotionTree(AnimatorController controller,Transform root,Transform[] bones,bool crouch)
    {
        var tree=new BlendTree {name=crouch?"Crouch Locomotion":"Standing Locomotion",
            blendType=BlendTreeType.FreeformCartesian2D,blendParameter="MoveX",blendParameterY="MoveZ",useAutomaticThresholds=false};
        AssetDatabase.AddObjectToAsset(tree,controller);
        tree.AddChild(Clip(root,bones,crouch?"CrouchIdle":"Idle",0,0,crouch,false),Vector2.zero);
        float speed=crouch?1.2f:2f;
        tree.AddChild(Clip(root,bones,crouch?"CrouchForward":"WalkForward",0,1,crouch,false),new Vector2(0,speed));
        tree.AddChild(Clip(root,bones,crouch?"CrouchBackward":"WalkBackward",0,-1,crouch,false),new Vector2(0,-speed));
        tree.AddChild(Clip(root,bones,crouch?"CrouchLeft":"StrafeLeft",-1,0,crouch,false),new Vector2(-speed,0));
        tree.AddChild(Clip(root,bones,crouch?"CrouchRight":"StrafeRight",1,0,crouch,false),new Vector2(speed,0));
        if(!crouch)
        {
            tree.AddChild(Clip(root,bones,"RunForward",0,1,false,true),new Vector2(0,4));
            tree.AddChild(Clip(root,bones,"RunBackward",0,-1,false,true),new Vector2(0,-4));
            tree.AddChild(Clip(root,bones,"RunLeft",-1,0,false,true),new Vector2(-4,0));
            tree.AddChild(Clip(root,bones,"RunRight",1,0,false,true),new Vector2(4,0));
        }
        return tree;
    }

    private static AnimationClip Clip(Transform root,Transform[] bones,string name,int x,int z,bool crouch,bool run)
    {
        float duration=x==0&&z==0?3f:run?.60f:crouch?1.15f:.92f;
        var clip=new AnimationClip {name=name,frameRate=30f,legacy=false};
        bool moving=x!=0||z!=0;
        var rotations=new Vector3[17,31];var hips=new float[31];
        for(int frame=0;frame<=30;frame++)
        {
            float phase=frame/30f*Mathf.PI*2f,s=Mathf.Sin(phase),c=Mathf.Cos(phase);
            float stride=moving?(run?42f:crouch?17f:27f):0f;
            float breath=Mathf.Sin(phase)*.6f;
            hips[frame]=Positions[0].y-(crouch?.44f:0f)+(moving?Mathf.Cos(phase*2f)*(run?.025f:.012f):Mathf.Sin(phase)*.0025f);
            rotations[1,frame]=new Vector3((crouch?19f:run?7f:0f)+breath,0,0);
            rotations[2,frame]=new Vector3(breath,moving?s*2.2f:0,moving?c*1.1f:0);
            rotations[3,frame]=new Vector3(crouch?-14f:run?-5f:0,0,0);
            rotations[4,frame]=new Vector3(breath*.3f,0,0);
            for(int side=-1;side<=1;side+=2)
            {
                int leg=side<0?11:14,arm=side<0?5:8;
                float wave=s*side;
                rotations[leg,frame]=new Vector3((crouch?-48f:0f)-wave*stride*z,0,wave*stride*x*.6f);
                rotations[leg+1,frame]=new Vector3((crouch?92f:4f)+Mathf.Max(0f,wave)*(run?65f:32f)*(moving?1f:0f),0,0);
                rotations[leg+2,frame]=new Vector3((crouch?-44f:-4f)-Mathf.Max(0f,-wave)*stride*.35f,0,-wave*stride*x*.35f);
                rotations[arm,frame]=new Vector3((run?-15f:0f)+wave*stride*.65f*z,0,side*2f-wave*x*8f);
                rotations[arm+1,frame]=new Vector3(run?-55f:-8f-Mathf.Max(0f,-wave)*12f*(moving?1f:0f),0,0);
            }
        }
        for(int bone=0;bone<bones.Length;bone++)
        {
            string bonePath=AnimationUtility.CalculateTransformPath(bones[bone],root);
            for(int axis=0;axis<3;axis++)
            {
                var keys=new Keyframe[31];
                for(int frame=0;frame<=30;frame++)keys[frame]=new Keyframe(frame/30f*duration,rotations[bone,frame][axis]);
                var curve=new AnimationCurve(keys);
                for(int i=0;i<keys.Length;i++)AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                for(int i=0;i<keys.Length;i++)AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(bonePath,typeof(Transform),"localEulerAnglesRaw."+"xyz"[axis]),curve);
            }
        }
        var hipKeys=new Keyframe[31];for(int frame=0;frame<=30;frame++)hipKeys[frame]=new Keyframe(frame/30f*duration,hips[frame]);
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[0],root),typeof(Transform),"m_LocalPosition.y"),new AnimationCurve(hipKeys));
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
        return Save(clip,Folder+"/Animations/"+name+".anim");
    }
}
#endif
