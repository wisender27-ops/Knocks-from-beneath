using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KnocksFromBeneath;

// Кладовая отвёртки (ScrewdriverHunt): верхняя ванная (дверь Door_005) освобождается от сантехники и забивается хламом,
// отвёртка переезжает туда с верстака, на верстаке остаётся ключ. Идемпотентно: хлам, ключ и контроллер пересоздаются.
// Сцену не сохраняет — только помечает изменённой.
public static class StorageRoomBuilder
{
    private const string RootName = "Storage room clutter", KeyName = "House key", HuntName = "Screwdriver hunt";
    private const string Dir = "Assets/Basement/StorageRoom/", PolyHaven = "Assets/ThirdParty/PolyHaven/";
    private const int InteractableLayer = 6;
    // Верхняя ванная: пол 4.10, стены x −47…−43.3, z 110.5…114.5.
    private static readonly Vector3 RoomMin = new Vector3(-47.1f, 4.2f, 110.5f), RoomMax = new Vector3(-43.3f, 7f, 114.6f);
    private static readonly string[] KeepFixtures = { "Glass_004", "Window_frame_009", "lightswitchUpperBathroom" };

    private struct Prop
    {
        public string Folder; public float Size, X, Z, Yaw, TiltX, TiltZ; public bool Collider, Lie;
        public Prop(string folder, float size, float x, float z, float yaw, float tiltX = 0f, float tiltZ = 0f, bool collider = false, bool lie = false)
        { Folder = folder; Size = size; X = x; Z = z; Yaw = yaw; TiltX = tiltX; TiltZ = tiltZ; Collider = collider; Lie = lie; }
    }

    // Порядок важен: следующие предметы встают на верх предыдущих (луч вниз по коллайдерам).
    private static readonly Prop[] Props =
    {
        new Prop("steel_frame_shelves_01", 1.9f, -46.15f, 114.05f, 180f, collider: true),
        new Prop("WoodenTable_03", 1.1f, -46.3f, 112.45f, 90f, collider: true),
        new Prop("wooden_crate_01", 0.6f, -44.0f, 113.9f, 10f, collider: true),
        new Prop("wooden_crate_02", 0.55f, -43.95f, 113.8f, 40f, collider: true),
        new Prop("cardboard_box_01", 0.5f, -44.0f, 113.85f, 25f, tiltZ: 8f, collider: true),
        new Prop("cardboard_box_01", 0.45f, -44.8f, 113.9f, -10f, collider: true),
        new Prop("cardboard_box_01", 0.4f, -46.3f, 112.45f, 70f, collider: true),
        new Prop("trashbag", 0.6f, -45.2f, 113.3f, 80f, collider: true),
        new Prop("trashbag", 0.55f, -44.85f, 112.95f, 20f, 12f, collider: true),
        new Prop("trashbag", 0.5f, -45.6f, 110.95f, 140f, collider: true),
        new Prop("cement_bag", 0.6f, -46.3f, 111.4f, 100f, 90f, collider: true),
        new Prop("barrel_03", 0.9f, -46.45f, 110.95f, 0f, collider: true),
        new Prop("old_tyre", 0.65f, -45.4f, 111.7f, 30f, 90f, collider: true),
        new Prop("WoodenChair_01", 0.9f, -45.3f, 112.15f, 50f, 85f, collider: true),
        new Prop("plastic_crate_01", 0.5f, -44.2f, 112.95f, 15f, collider: true),
        new Prop("wicker_basket_01", 0.45f, -45.15f, 114.0f, 0f, collider: true),
        new Prop("wooden_ladder", 2.0f, -45.0f, 110.95f, 0f, lie: true),
        new Prop("wooden_broom", 1.4f, -44.6f, 113.4f, 20f, lie: true),
        new Prop("wooden_crate_02", 0.5f, -43.8f, 110.9f, 20f, collider: true),
        new Prop("wooden_bucket_01", 0.35f, -43.8f, 110.9f, 0f, 80f),
        new Prop("oil_tin", 0.3f, -45.65f, 112.9f, 0f, 85f),
        new Prop("can_rusted", 0.15f, -44.6f, 111.9f, 0f, 90f),
        new Prop("can_rusted", 0.15f, -45.4f, 111.3f, 40f),
        new Prop("plastic_bottle_gallon", 0.3f, -44.5f, 112.5f, 60f, 90f),
        new Prop("multi_cleaner_5_litre", 0.35f, -44.8f, 111.2f, 30f),
        new Prop("rubber_boots", 0.35f, -46.0f, 113.4f, 0f, 0f, 90f),
        new Prop("dustpan", 0.3f, -44.3f, 111.5f, 120f),
        new Prop("office_notepads", 0.2f, -45.6f, 112.4f, 15f, 3f),
        new Prop("office_notepads", 0.2f, -44.9f, 113.6f, 100f, 5f),
        new Prop("office_notepads", 0.2f, -45.7f, 111.9f, 200f, 4f),
        new Prop("russian_food_cans_01", 0.2f, -44.3f, 112.2f, 0f),
        new Prop("mousetrap", 0.1f, -45.3f, 112.6f, 60f),
        new Prop("hanging_picture_frame_01", 0.5f, -43.8f, 113.5f, 200f, 0f, 15f),
        new Prop("cardboard_box_01", 0.55f, -46.5f, 113.6f, 5f, collider: true),
        new Prop("cardboard_box_01", 0.42f, -46.45f, 113.6f, 30f, 0f, 6f, collider: true),
        new Prop("trashbag", 0.5f, -43.75f, 112.85f, 60f, collider: true),
        new Prop("office_notepads", 0.2f, -44.2f, 111.7f, 70f, 2f),
        new Prop("office_notepads", 0.2f, -46.0f, 113.0f, 160f, 3f),
        new Prop("office_notepads", 0.2f, -45.1f, 110.9f, 20f, 4f),
        new Prop("office_notepads", 0.2f, -44.5f, 113.0f, 250f, 2f),
        new Prop("can_rusted", 0.15f, -44.4f, 112.7f, 10f, 90f),
        new Prop("can_rusted", 0.15f, -46.1f, 111.9f, 90f, 90f),
        new Prop("plastic_bottle_gallon", 0.3f, -45.9f, 113.7f, 15f, 90f),
        new Prop("cigarette_pack", 0.08f, -45.0f, 112.0f, 45f),
        new Prop("rubber_boots", 0.35f, -44.7f, 111.6f, 100f, 0f, 90f),
    };

    [MenuItem("Tools/House/Build Storage Room")]
    public static void Build()
    {
        Directory.EnsureDir(Dir);
        HideBathroomFixtures();
        Transform root = Fresh(RootName);
        var rng = new System.Random(7);
        foreach (Prop prop in Props) Place(prop, root, rng);
        DressSurfaces(root);

        Transform tableTop = null;
        Physics.SyncTransforms();
        ToolPickup screwdriver = MoveScrewdriver(out tableTop);
        ToolPickup key = BuildKey();
        BuildHunt(key, screwdriver);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("[StorageRoom] Готово: кладовая, ключ на верстаке, контроллер. Сохраните сцену (Ctrl+S).");
    }

    private static class Directory
    {
        public static void EnsureDir(string path)
        {
            string[] parts = path.TrimEnd('/').Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + parts[i])) AssetDatabase.CreateFolder(current, parts[i]);
                current += "/" + parts[i];
            }
        }
    }

    private static Transform Fresh(string name)
    {
        foreach (GameObject g in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name == name).Select(t => t.gameObject).ToArray())
            Object.DestroyImmediate(g);
        return new GameObject(name).transform;
    }

    private static bool InRoom(Vector3 p) => p.x > RoomMin.x && p.x < RoomMax.x && p.y > RoomMin.y && p.y < RoomMax.y && p.z > RoomMin.z && p.z < RoomMax.z;

    // Сантехника и мелочи ванной уходят вместе с коллайдерами мебели; окно, стекло и выключатель остаются.
    private static void HideBathroomFixtures()
    {
        var toHide = new HashSet<GameObject>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!InRoom(r.bounds.center)) continue;
            Transform top = r.transform;
            while (top.parent != null && top.parent.name != "House" && top.parent.name != "House_Colliders") top = top.parent;
            if (top.parent == null || KeepFixtures.Contains(top.name) || top.name.Contains("Door") || top.name.Contains("Window") || top.name.Contains("light")) continue;
            if (top.parent.name == "House_Colliders" && r.bounds.size.y > 2.2f) continue; // стены и перекрытия не трогаем
            toHide.Add(top.gameObject);
        }
        foreach (GameObject g in toHide) g.SetActive(false);
        Debug.Log("[StorageRoom] Скрыто объектов ванной: " + toHide.Count);
    }

    private static void Place(Prop prop, Transform parent, System.Random rng)
    {
        string path = PolyHaven + prop.Folder + "/" + prop.Folder + ".fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("[StorageRoom] Нет модели " + path); return; }
        var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        item.name = prop.Folder;
        var material = AssetDatabase.LoadAssetAtPath<Material>(PolyHaven + prop.Folder + "/" + prop.Folder + ".mat");
        foreach (Renderer part in item.GetComponentsInChildren<Renderer>(true))
            if (material != null && part.sharedMaterials.Length == 1) part.sharedMaterial = material;

        item.transform.rotation = Quaternion.identity;
        item.transform.localScale = Vector3.one;
        Bounds raw = RendererBounds(item);
        item.transform.localScale = Vector3.one * (prop.Size / Mathf.Max(raw.size.x, raw.size.y, raw.size.z));
        if (prop.Lie)
        {
            Vector3 size = RendererBounds(item).size;
            Vector3 axis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
            item.transform.rotation = Quaternion.FromToRotation(axis, Vector3.right);
            item.transform.rotation = Quaternion.AngleAxis(prop.Yaw, Vector3.up) * item.transform.rotation;
        }
        else
            item.transform.rotation = Quaternion.AngleAxis(prop.Yaw, Vector3.up) * Quaternion.Euler(prop.TiltX, 0f, prop.TiltZ);

        Bounds b = RendererBounds(item);
        Vector3 origin = new Vector3(prop.X, RoomMax.y - 0.6f, prop.Z);
        float floor = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 4.1f;
        item.transform.position += new Vector3(prop.X - b.center.x, floor - b.min.y + 0.002f, prop.Z - b.center.z);
        foreach (Transform t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        if (prop.Collider)
        {
            Bounds world = RendererBounds(item);
            var box = item.AddComponent<BoxCollider>();
            box.center = item.transform.InverseTransformPoint(world.center);
            Vector3 local = item.transform.InverseTransformVector(world.size);
            box.size = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            Physics.SyncTransforms();
        }
    }

    // ---- Стены, пол и декали: поверх существующих поверхностей, на 3-7 мм внутрь комнаты ----

    private const string TexDir = Dir + "Textures/", MatDir = Dir + "Mats/";
    private const float FloorY = 4.10f, CeilY = 7.238f, WestX = -46.696f, EastX = -43.36f, SouthZ = 110.57f, NorthZ = 114.28f;
    private const float WallLift = 0.003f, DecalLift = 0.007f;
    private const float WindowLatMin = 1.39f, WindowLatMax = 2.36f, WindowY0 = 4.99f, WindowY1 = 6.54f;
    private const float DoorLatMin = 1.49f, DoorLatMax = 2.53f, DoorTopY = 6.65f;

    private struct Wall
    {
        public Vector3 Origin, Right, Normal;
        public float Width;
        public Vector3 At(float lat, float y, float lift) => Origin + Right * lat + Vector3.up * (y - FloorY) + Normal * lift;
    }

    // Right — вправо при взгляде на стену из комнаты, lat отсчитывается от её левого края.
    private static readonly Wall North = new Wall { Origin = new Vector3(WestX, FloorY, NorthZ), Right = Vector3.right, Normal = Vector3.back, Width = EastX - WestX };
    private static readonly Wall South = new Wall { Origin = new Vector3(EastX, FloorY, SouthZ), Right = Vector3.left, Normal = Vector3.forward, Width = EastX - WestX };
    private static readonly Wall West = new Wall { Origin = new Vector3(WestX, FloorY, SouthZ), Right = Vector3.forward, Normal = Vector3.right, Width = NorthZ - SouthZ };
    private static readonly Wall East = new Wall { Origin = new Vector3(EastX, FloorY, NorthZ), Right = Vector3.back, Normal = Vector3.left, Width = NorthZ - SouthZ };

    private static Material Surface(string name, string texture, bool transparent, bool repeat)
    {
        string texPath = TexDir + texture;
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (importer != null)
        {
            importer.alphaIsTransparency = transparent;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        Directory.EnsureDir(MatDir);
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_SpecularHighlights", 0f);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Углы по часовой стрелке при взгляде спереди: BL, BR, TR, TL. UV идут в том же порядке.
    private static void Quad(string name, Transform parent, Material material, Vector3[] c, Vector2[] uv, Vector3 normal)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        var mesh = new Mesh { name = name, vertices = c, uv = uv, triangles = new[] { 0, 2, 1, 0, 3, 2 }, normals = new[] { normal, normal, normal, normal } };
        mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Кусок стены: UV считаются от всей стены, куски вокруг окна и двери продолжают друг друга.
    private static void WallPiece(Transform parent, Material material, Wall w, float lat0, float lat1, float y0, float y1)
    {
        float v0 = (y0 - FloorY) / (CeilY - FloorY), v1 = (y1 - FloorY) / (CeilY - FloorY);
        Quad("Wall " + w.Normal, parent, material,
            new[] { w.At(lat0, y0, WallLift), w.At(lat1, y0, WallLift), w.At(lat1, y1, WallLift), w.At(lat0, y1, WallLift) },
            new[] { new Vector2(lat0 / w.Width, v0), new Vector2(lat1 / w.Width, v0), new Vector2(lat1 / w.Width, v1), new Vector2(lat0 / w.Width, v1) }, w.Normal);
    }

    private static void Decal(Transform parent, Material material, Wall w, string name, float lat, float y, float width, float height, bool flip = false)
    {
        float l0 = lat - width * 0.5f, l1 = lat + width * 0.5f, y0 = y - height * 0.5f, y1 = y + height * 0.5f;
        float u0 = flip ? 1f : 0f, u1 = flip ? 0f : 1f;
        Quad(name, parent, material,
            new[] { w.At(l0, y0, DecalLift), w.At(l1, y0, DecalLift), w.At(l1, y1, DecalLift), w.At(l0, y1, DecalLift) },
            new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) }, w.Normal);
    }

    private static void DressSurfaces(Transform root)
    {
        AssetDatabase.Refresh();
        Material wall = Surface("StorageWall", "wall.png", false, true);
        Material floor = Surface("StorageFloor", "floor.png", false, true);
        Material web = Surface("StorageWeb", "cobweb.png", true, false);
        Material stain = Surface("StorageStain", "stain.png", true, false);
        Material paper = Surface("StorageWallpaper", "wallpaper.png", true, false);
        Material debris = Surface("StorageDebris", "debris.png", true, false);
        var group = new GameObject("Surfaces").transform;
        group.SetParent(root, false);

        WallPiece(group, wall, North, 0f, North.Width, FloorY, CeilY);
        WallPiece(group, wall, South, 0f, South.Width, FloorY, CeilY);
        // Западная стена: окно между рамой и потолком/полом.
        WallPiece(group, wall, West, 0f, West.Width, FloorY, WindowY0);
        WallPiece(group, wall, West, 0f, West.Width, WindowY1, CeilY);
        WallPiece(group, wall, West, 0f, WindowLatMin, WindowY0, WindowY1);
        WallPiece(group, wall, West, WindowLatMax, West.Width, WindowY0, WindowY1);
        // Восточная: дверная коробка до DoorTopY.
        WallPiece(group, wall, East, 0f, DoorLatMin, FloorY, CeilY);
        WallPiece(group, wall, East, DoorLatMax, East.Width, FloorY, CeilY);
        WallPiece(group, wall, East, DoorLatMin, DoorLatMax, DoorTopY, CeilY);

        // Пол: 2x2 плитки на всю комнату; хлам лежит чуть выше.
        float fy = FloorY + 0.0015f;
        Quad("Floor", group, floor,
            new[] { new Vector3(WestX, fy, SouthZ), new Vector3(EastX, fy, SouthZ), new Vector3(EastX, fy, NorthZ), new Vector3(WestX, fy, NorthZ) },
            new[] { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2f, 2f), new Vector2(0f, 2f) }, Vector3.up);
        float dy = FloorY + 0.0025f;
        Quad("Floor debris", group, debris,
            new[] { new Vector3(-46.5f, dy, 111.3f), new Vector3(-43.5f, dy, 111.3f), new Vector3(-43.5f, dy, 113.5f), new Vector3(-46.5f, dy, 113.5f) },
            new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }, Vector3.up);

        // Подтёки у потолка, облезшие обои, паутина в верхних углах (текстура паутины — угол слева сверху).
        Decal(group, stain, North, "Stain N", 1.0f, 6.3f, 1.5f, 1.0f);
        Decal(group, stain, South, "Stain S", 2.0f, 6.2f, 1.1f, 0.73f, true);
        Decal(group, paper, North, "Wallpaper N", 2.4f, 5.2f, 1.3f, 0.43f);
        Decal(group, paper, South, "Wallpaper S", 0.9f, 5.0f, 1.0f, 0.33f, true);
        Decal(group, web, North, "Web NW", 0.45f, CeilY - 0.3f, 0.9f, 0.6f);
        Decal(group, web, North, "Web NE", North.Width - 0.45f, CeilY - 0.3f, 0.9f, 0.6f, true);
        Decal(group, web, South, "Web SE", 0.45f, CeilY - 0.3f, 0.9f, 0.6f);
        Decal(group, web, West, "Web SW", 0.45f, CeilY - 0.3f, 0.9f, 0.6f);
    }

    private static Bounds RendererBounds(GameObject item)
    {
        Renderer[] parts = item.GetComponentsInChildren<Renderer>(true);
        Bounds b = parts[0].bounds;
        foreach (Renderer r in parts) b.Encapsulate(r.bounds);
        return b;
    }

    // Отвёртка в кладовой: на столе под окном, рядом с коробкой.
    private static ToolPickup MoveScrewdriver(out Transform tableTop)
    {
        tableTop = null;
        Transform t = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.name == "Workbench screwdriver" || x.name == "Storage screwdriver");
        if (t == null) { Debug.LogError("[StorageRoom] Отвёртки нет в сцене: сначала Tools/Basement/Install Screwdriver."); return null; }
        t.name = "Storage screwdriver";
        t.gameObject.SetActive(true);
        Renderer renderer = t.GetComponentInChildren<Renderer>();
        Vector3 spot = new Vector3(-46.25f, RoomMax.y - 0.6f, 112.0f);
        float top = Physics.Raycast(spot, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 4.1f;
        t.position += new Vector3(spot.x - renderer.bounds.center.x, top + 0.003f - renderer.bounds.min.y, spot.z - renderer.bounds.center.z);
        var pickup = t.GetComponent<ToolPickup>();
        Physics.SyncTransforms();
        EditorUtility.SetDirty(t.gameObject);
        return pickup;
    }

    private static Material KeyMaterial()
    {
        string path = Dir + "HouseKey.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.72f, 0.56f, 0.24f));
        m.SetFloat("_Metallic", 0.85f);
        m.SetFloat("_Smoothness", 0.45f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // Ключ собран из примитивов: кольцо, стержень, два зубца. Лежит плашмя, длиной около 12 см.
    private static GameObject KeyModel(string name)
    {
        Material metal = KeyMaterial();
        var root = new GameObject(name).transform;
        void Part(PrimitiveType type, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = metal;
        }
        Part(PrimitiveType.Cylinder, new Vector3(-0.05f, 0f, 0f), new Vector3(0.042f, 0.0025f, 0.042f));
        Part(PrimitiveType.Cube, new Vector3(0.012f, 0f, 0f), new Vector3(0.08f, 0.005f, 0.008f));
        Part(PrimitiveType.Cube, new Vector3(0.045f, 0f, -0.0085f), new Vector3(0.008f, 0.005f, 0.014f));
        Part(PrimitiveType.Cube, new Vector3(0.028f, 0f, -0.0075f), new Vector3(0.008f, 0.005f, 0.011f));
        var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(hole.GetComponent<Collider>());
        hole.transform.SetParent(root, false);
        hole.transform.localPosition = new Vector3(-0.05f, 0.0003f, 0f);
        hole.transform.localScale = new Vector3(0.016f, 0.0027f, 0.016f);
        var dark = AssetDatabase.LoadAssetAtPath<Material>(Dir + "KeyHole.mat");
        if (dark == null)
        {
            dark = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            dark.SetColor("_BaseColor", Color.black);
            AssetDatabase.CreateAsset(dark, Dir + "KeyHole.mat");
        }
        hole.GetComponent<Renderer>().sharedMaterial = dark;
        return root.gameObject;
    }

    // Ключ на верстаке в подвале: на месте прежней отвёртки.
    private static ToolPickup BuildKey()
    {
        const string prefabPath = "Assets/Inventory/Prefabs/HouseKey.prefab", itemPath = "Assets/Inventory/Items/HouseKey.asset";
        GameObject model = KeyModel("HouseKey");
        var prefab = PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
        Object.DestroyImmediate(model);

        var item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            AssetDatabase.CreateAsset(item, itemPath);
        }
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = ScrewdriverHunt.KeyId;
        so.FindProperty("displayName").stringValue = "КЛЮЧ";
        so.FindProperty("description").stringValue = "Старый ключ с верстака в подвале. Что-то в этом доме заперто.";
        so.FindProperty("category").intValue = (int)InventoryItemCategory.Item;
        so.FindProperty("previewPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        var database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>("Assets/Inventory/InventoryDatabase.asset");
        if (database.Find(ScrewdriverHunt.KeyId) == null)
        {
            var dbSo = new SerializedObject(database);
            SerializedProperty items = dbSo.FindProperty("items");
            items.arraySize++;
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Transform old = Fresh(KeyName);
        Object.DestroyImmediate(old.gameObject);
        var world = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        world.name = KeyName;
        Vector3 spot = new Vector3(-39.5f, -2f, 113.2f);
        Physics.Raycast(spot, Vector3.down, out RaycastHit top, 3f, ~0, QueryTriggerInteraction.Ignore);
        world.transform.SetPositionAndRotation(new Vector3(spot.x, top.point.y + 0.004f, spot.z), Quaternion.Euler(0f, 35f, 0f));
        var box = world.AddComponent<BoxCollider>();
        box.center = new Vector3(-0.01f, 0f, 0f);
        box.size = new Vector3(0.16f, 0.03f, 0.06f);
        var pickup = world.AddComponent<ToolPickup>();
        var pickupSo = new SerializedObject(pickup);
        pickupSo.FindProperty("itemId").stringValue = ScrewdriverHunt.KeyId;
        pickupSo.FindProperty("prompt").stringValue = "Взять ключ";
        pickupSo.ApplyModifiedPropertiesWithoutUndo();
        foreach (Transform child in world.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = InteractableLayer;
        AssetDatabase.SaveAssets();
        return pickup;
    }

    private static void BuildHunt(ToolPickup key, ToolPickup screwdriver)
    {
        Transform hunt = Fresh(HuntName);
        var component = hunt.gameObject.AddComponent<ScrewdriverHunt>();
        var door = Object.FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(d => d.name == "Door_005");
        component.storageDoor = door;
        component.key = key;
        component.screwdriver = screwdriver;
        EditorUtility.SetDirty(component);
    }
}
