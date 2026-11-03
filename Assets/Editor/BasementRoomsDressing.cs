using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KnocksFromBeneath;

// Обставляет залы за вентиляцией (B и блок D с нишами) настоящими моделями Poly Haven вместо кубиков-заглушек BasementLevelBuilder.
// Логику не трогает: стены и блокеры остаются теми же объектами с теми же коллайдерами, меняются только их меш и видимость.
// Повторный запуск пересобирает объект "Basement Dressing" целиком.
public static class BasementRoomsDressing
{
    private const string RootName = "Basement Dressing";
    private const string Models = "Assets/ThirdParty/PolyHaven/";
    private const string Rooms = "Assets/Basement/Rooms/";
    private const string ShellAsset = Rooms + "RoomShell.asset";
    private const float Floor = BasementLayout.FloorY, Ceiling = BasementLayout.CeilingY;
    private const float Tile = 2.4f; // метров на повтор текстуры стен и пола
    private const float LeaseTableTop = -3.505f; // чуть выше столешницы WoodenTable_03 в блоке D: лист плоский
    private const float LeaseAspect = 1103f / 1426f; // пропорции картинки договора
    private const string LeaseTexture = Rooms + "Lease/LeaseContract.png";
    private const string LeaseMaterialAsset = Rooms + "Lease/LeaseContract.mat";
    private const string LeaseMeshAsset = Rooms + "Lease/LeaseContractSheet.asset";

    private const string ShelterGroup = "Shelter";
    private const string ShelterFolder = "Shelter/";
    private const string ShelterMeshAsset = Rooms + ShelterFolder + "ShelterMeshes.asset";
    private const float CanRadius = 0.0375f, CanHeight = 0.11f;

    private static readonly Vector3 CartonSize = new Vector3(0.4f, 0.24f, 0.28f);
    private static readonly Color BulbColor = new Color(1f, 0.74f, 0.48f);

    [MenuItem("Tools/Basement/Dress Rooms")]
    public static void Build()
    {
        var level = UnityEngine.Object.FindFirstObjectByType<BasementLevel>(FindObjectsInactive.Include);
        if (level == null) throw new InvalidOperationException("В сцене нет BasementLevel.");
        Transform old = level.transform.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        var root = new GameObject(RootName).transform;
        root.SetParent(level.transform, false);
        var shell = new ShellMeshes();
        Retexture(level, shell);
        HideStubs(level);
        Furnish(root, level);
        Joists(root, shell);
        FloorDirt(root, shell);
        Atmosphere(root, level);
        shell.Save();

        var renderers = new List<Renderer>(root.GetComponentsInChildren<Renderer>(true));
        foreach (string room in new[] { "Room A", "Room B", "Room C", "Room D", "Room E", "Room F", "Room G" })
            renderers.AddRange(level.transform.Find(room).GetComponentsInChildren<Renderer>(true));
        var ambient = root.gameObject.AddComponent<UndergroundAmbientOverride>();
        var serialized = new SerializedObject(ambient);
        SerializedProperty list = serialized.FindProperty("renderers");
        list.arraySize = renderers.Count;
        for (int i = 0; i < renderers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ambient.Apply();
        EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
    }

    // -------------------------------------------------------------------------------------------------
    // Стены, пол, потолок: те же объекты, но меш с UV в метрах, иначе текстура растянута вместе с кубом
    // -------------------------------------------------------------------------------------------------

    private static void Retexture(BasementLevel level, ShellMeshes shell)
    {
        Material wall = Surface("RoomWall", "dirty_concrete", 0.12f), floor = Surface("RoomFloor", "concrete_floor_worn_001", 0.22f),
            ceiling = Surface("RoomCeiling", "weathered_planks", 0.08f);
        var rust = AssetDatabase.LoadAssetAtPath<Material>("Assets/Basement/Materials/RustSteel.mat");
        foreach (string room in new[] { "Room A", "Room B", "Room C", "Room D", "Room E", "Room F", "Room G" })
            foreach (MeshRenderer part in level.transform.Find(room).GetComponentsInChildren<MeshRenderer>(true))
            {
                part.GetComponent<MeshFilter>().sharedMesh = shell.Box(part.transform.position, part.transform.lossyScale);
                part.sharedMaterial = part.name == "Floor" ? floor : part.name == "Ceiling" ? ceiling : wall;
            }
        Transform eastWall = level.transform.Find("Cell east wall");
        eastWall.GetComponent<MeshFilter>().sharedMesh = shell.Box(eastWall.position, eastWall.lossyScale);
        eastWall.GetComponent<MeshRenderer>().sharedMaterial = wall;
        // Колонны зала: первые две записи Hall props.
        Transform props = level.transform.Find("Hall props");
        for (int i = 0; i < 2; i++)
        {
            Transform pillar = props.GetChild(i);
            pillar.GetComponent<MeshFilter>().sharedMesh = shell.Box(pillar.position, pillar.lossyScale);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = wall;
        }
        // Решётка остаётся по форме: на прутьях висят коллайдер и логика двери.
        foreach (MeshRenderer bar in level.transform.Find("Cell door").GetComponentsInChildren<MeshRenderer>(true))
            bar.sharedMaterial = rust;
    }

    private static void HideStubs(BasementLevel level)
    {
        // Блокеры Junk остаются: на них настроены проходы, обход монстра и его линия зрения.
        // Одеяло остаётся: на нём сидит хозяин, а кровать в камере в него бы врезалась.
        foreach (Transform junk in level.transform.Find("Junk"))
            if (junk.name != "Junk Blanket")
                foreach (Transform part in junk) part.gameObject.SetActive(false);
        Transform props = level.transform.Find("Hall props");
        props.GetChild(2).GetComponent<MeshRenderer>().enabled = false; // ящик
        props.GetChild(3).GetComponent<MeshRenderer>().enabled = false; // бойлер
        level.transform.Find("Cloned props").gameObject.SetActive(false);
        // Укрытие: вместо двух висящих досок — коробка на полу. Объект с взаимодействием остаётся, его коллайдер садится по ней.
        Transform hide = level.transform.Find("Hide behind crates");
        hide.Find("Lower boards").gameObject.SetActive(false);
        hide.Find("Upper boards").gameObject.SetActive(false);
        hide.position = BasementLayout.HideCrateCenter;
        hide.localScale = new Vector3(1.22f, 0.8f, 0.98f);
    }

    // -------------------------------------------------------------------------------------------------
    // Обстановка. Зал B — кладовка-прачечная (первый референс), блок D — склад с верстаком (второй)
    // -------------------------------------------------------------------------------------------------

    private static void Furnish(Transform root, BasementLevel level)
    {
        var hall = Group(root, "Hall B");
        // Западная полоса и кольцо вокруг колонн свободны: по ним крадётся игрок и ходит монстр.
        Clone(hall, level, "WaterHeater", -33.0f, 117.6f, 0f, new Vector3(1.5f, 1.5f, 0.92f));
        Model(hall, "metal_tool_chest", -34.3f, 118.05f, 180f, 0f, true);
        Appliance(hall, "washer", "washer", -33.55f, 106.95f, 0f);
        Appliance(hall, "dryer", "dryer", -32.82f, 106.95f, 0f);
        Model(hall, "multi_cleaner_5_litre", -33.7f, 106.85f, 25f, 0f, false, 1.1f);
        Model(hall, "plastic_bottle_gallon", -33.4f, 107.05f, -30f, 0f, false, 1.1f);
        Model(hall, "wicker_basket_01", -34.5f, 106.95f, 40f, 0.28f, false);
        Model(hall, "steel_frame_shelves_01", -32.47f, 111.85f, 90f, 0f, false);
        Model(hall, "steel_frame_shelves_01", -32.47f, 112.97f, 90f, 0f, false);
        Model(hall, "cardboard_box_01", -32.47f, 111.6f, 92f, 0f, false, 0.72f);
        Model(hall, "plastic_crate_01", -32.47f, 112.1f, 90f, 0f, false, 0.72f);
        Model(hall, "cardboard_box_01", -32.47f, 113.1f, 84f, 0f, false, 0.08f);
        Model(hall, "wooden_crate_01", -32.47f, 112.0f, 90f, 0f, false, 0.08f);
        Model(hall, "metal_toolbox", -32.47f, 112.8f, 80f, 0f, false, 1.36f);
        Model(hall, "can_rusted", -32.45f, 113.3f, 10f, 0f, false, 1.36f);
        Model(hall, "oil_tin", -32.5f, 111.5f, 0f, 0f, false, 1.36f);
        Model(hall, "barrel_03", -36.45f, 107.0f, 12f, 0f, false);
        Model(hall, "barrel_03", -35.8f, 106.95f, 95f, 0f, false);
        Model(hall, "barrel_03", -36.4f, 107.7f, 200f, 0f, false);
        Model(hall, "cement_bag", -32.75f, 109.6f, 90f, 0f, false);
        Model(hall, "cement_bag", -32.72f, 110.2f, 84f, 0f, false);
        Model(hall, "cement_bag", -32.74f, 109.9f, 93f, 0f, false, 0.17f);
        Model(hall, "old_tyre", -36.55f, 113.1f, 0f, 0f, false, 0f, 88f);
        Model(hall, "trashbag", -36.45f, 113.75f, 70f, 0f, false);
        Model(hall, "WoodenChair_01", -34.0f, 112.4f, 200f, 0.92f, false);
        Model(hall, "wooden_bucket_01", -36.6f, 109.0f, 0f, 0.3f, false);
        Model(hall, "metal_jerrycan", -36.68f, 112.6f, 90f, 0f, false);
        WallModel(hall, "hanging_picture_frame_01", new Vector3(-32.22f, Floor + 1.65f, 115.2f), Vector3.left);

        var block = Group(root, "Block D");
        Model(block, "worn_metal_rack", -44.15f, 111.62f, 0f, 0f, false);
        Model(block, "worn_metal_rack", -43.15f, 111.62f, 0f, 0f, false);
        Model(block, "cardboard_box_01", -44.2f, 111.6f, 92f, 0f, false, 0.08f);
        Model(block, "wooden_crate_01", -43.15f, 111.6f, 0f, 0f, false, 0.08f);
        Model(block, "plastic_crate_01", -43.3f, 111.6f, 90f, 0f, false, 0.72f);
        Model(block, "barrel_03", -45.25f, 111.4f, 40f, 0f, false);
        Model(block, "barrel_03", -45.2f, 110.65f, 130f, 0f, false);
        Model(block, "WoodenTable_03", -40.8f, 111.5f, 180f, 0f, true);
        Model(block, "metal_toolbox", -41.15f, 111.45f, 15f, 0f, false, 0.83f);
        Model(block, "cross_pein_hammer", -40.5f, 111.3f, 70f, 0f, false, 0.83f, 90f);
        Model(block, "can_rusted", -40.25f, 111.6f, 30f, 0f, false, 0.83f);
        Model(block, "wooden_crate_02", -39.3f, 109.85f, 95f, 0f, false);
        Model(block, "wooden_crate_01", -39.3f, 109.75f, 80f, 0f, false, 0.46f);
        Model(block, "cardboard_box_01", -39.3f, 109.8f, 30f, 0f, false, 0.81f);
        Model(block, "cardboard_box_01", -43.0f, 108.0f, 6f, 0.5f, false);
        Model(block, "cardboard_box_01", -43.02f, 108.02f, 28f, 0.42f, false, 0.5f);
        Model(block, "cardboard_box_01", -40.7f, 107.95f, -4f, 0.5f, false);
        Model(block, "cardboard_box_01", -40.72f, 107.97f, 15f, 0.5f, false, 0.5f);
        Model(block, "wooden_ladder", -39.2f, 108.55f, 100f, 0f, false);
        WallModel(block, "garden_hose_wall_mounted_01", new Vector3(-45.68f, Floor + 1.5f, 108.8f), Vector3.right);
        // Ниши и камера.
        Model(block, "cardboard_box_01", BasementLayout.HideCrateCenter.x, BasementLayout.HideCrateCenter.z, 90f, 0.8f, false);
        Model(block, "cardboard_box_01", -42.25f, 106.35f, 12f, 0f, false);
        Keep(Model(block, "metal_trash_can", -39.1f, 106.25f, 0f, 0f, false), "metal_trash_can_rust", "metal_trash_can_rust_handle_left", "metal_trash_can_rust_handle_right", "metal_trash_can_rust_lid");
        Model(block, "wooden_bucket_01", -43.8f, 106.3f, 60f, 0.3f, false);
        Keep(Model(block, "rubber_boots", -44.1f, 107.5f, 30f, 0f, false), "rubber_boots_dirt_r_LOD0", "rubber_boots_dirty_l_LOD0");

        Litter(hall, block);
        Shelter(root);

        Bulb(hall, new Vector3(-34.2f, Ceiling, 116.3f));
        Bulb(hall, new Vector3(-35.1f, Ceiling, 108.7f));
        Bulb(block, BasementLayout.CellLampPosition + Vector3.up * 0.4f);
    }

    // Мелкий мусор на полу: не мешает ходьбе и обзору, коллайдеров нет.
    private static void Litter(Transform hall, Transform block)
    {
        Model(hall, "cigarette_pack", -35.9f, 110.3f, 35f, 0f, false);
        Model(hall, "russian_food_cans_01", -36.5f, 111.9f, 70f, 0f, false);
        Model(hall, "can_rusted", -35.3f, 113.4f, 20f, 0f, false, 0.06f, 92f);
        Stone(hall, -36.1f, 114.4f, 0f, 0.09f);
        Stone(hall, -36.35f, 114.1f, 140f, 0.06f);
        Stone(hall, -33.6f, 109.2f, 60f, 0.05f);
        Model(hall, "mousetrap", -32.75f, 108.6f, 25f, 0f, false);
        Model(hall, "wooden_broom", -36.72f, 114.9f, 90f, 0f, false, 0f, 12f);
        Model(hall, "can_rusted", -33.5f, 115.6f, 140f, 0f, false, 0.06f, 88f);
        Stone(hall, -35.7f, 108.4f, 90f, 0.07f);
        Model(hall, "cigarette_pack", -32.9f, 116.5f, 80f, 0f, false);
        Model(block, "dustpan", -45.1f, 109.9f, 110f, 0f, false);
        Model(block, "office_notepads", -41.6f, 110.9f, 40f, 0f, false);
        Stone(block, -42.4f, 109.1f, 30f, 0.08f);
        Stone(block, -42.1f, 108.9f, 200f, 0.05f);
        Model(block, "cigarette_pack", -44.6f, 110.8f, 150f, 0f, false);
        Model(block, "can_rusted", -38.0f, 110.75f, 60f, 0f, false);
        PlaceLeaseContract(block);
    }

    // S-05: договор аренды на столе блока D — лист A4 из того же файла, что и бумаги на полу.
    public static InspectableClue PlaceLeaseContract(Transform block)
    {
        GameObject pack = Instantiate("office_notepads", block);
        PrefabUtility.UnpackPrefabInstance(pack, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Transform sheet = null;
        foreach (Transform part in pack.GetComponentsInChildren<Transform>(true))
            if (part.name == "office_notepads_a4_a") sheet = part;
        if (sheet == null) throw new InvalidOperationException("Нет листа A4 в office_notepads");
        sheet.SetParent(block, true);
        UnityEngine.Object.DestroyImmediate(pack);
        sheet.name = "Lease contract";

        sheet.rotation = Quaternion.Euler(0f, 18f, 0f) * sheet.rotation;
        Bounds bounds = WorldBounds(sheet.gameObject);
        sheet.position += new Vector3(-40.62f - bounds.center.x, LeaseTableTop - bounds.min.y, 111.6f - bounds.center.z);

        Mesh mesh = sheet.GetComponent<MeshFilter>().sharedMesh;
        Vector3 size = mesh.bounds.size;
        int thin = size.x < size.y ? (size.x < size.z ? 0 : 2) : (size.y < size.z ? 1 : 2);
        Vector3 normal = Vector3.zero;
        normal[thin] = 1f;
        if (sheet.TransformDirection(normal).y < 0f) normal = -normal;
        int longAxis = thin == 0 ? (size.y > size.z ? 1 : 2) : thin == 1 ? (size.x > size.z ? 0 : 2) : (size.x > size.y ? 0 : 1);
        Vector3 up = Vector3.zero;
        up[longAxis] = 1f;

        // Текст и подписи — на самой текстуре листа, поэтому наклейка с текстом не нужна.
        Vector3 right = Vector3.Cross(normal, up);
        mesh = LeasePageMesh(mesh, normal, up, right, LeaseAspect);
        sheet.GetComponent<MeshFilter>().sharedMesh = mesh;
        sheet.GetComponent<MeshRenderer>().sharedMaterial = LeasePageMaterial();
        size = mesh.bounds.size;

        var hit = sheet.gameObject.AddComponent<BoxCollider>();
        hit.center = mesh.bounds.center;
        Vector3 hitSize = size;
        hitSize[thin] = 0.03f / Mathf.Max(0.0001f, Mathf.Abs(sheet.lossyScale[thin]));
        hit.size = hitSize;

        var clue = sheet.gameObject.AddComponent<InspectableClue>();
        Vector2 page = new Vector2(size[3 - thin - longAxis] * Mathf.Abs(sheet.lossyScale[3 - thin - longAxis]),
            size[longAxis] * Mathf.Abs(sheet.lossyScale[longAxis]));
        clue.Configure("clue-lease", "Договор аренды", false, normal, up, page, false, string.Empty,
            "Договор аренды старого дома. Арендодатель: Уолтер Хейл. Арендатор: Дэниел Росс.",
            new[]
            {
                "Это мой договор. Мой старый дом.",
                "Я платил через агентство и ни разу не видел хозяина. Хейл.",
                "Его сын сам нашёл меня и отдал этот дом почти даром. Это не совпадение."
            });
        return clue;
    }

    // Копия листа A4: ширина под пропорции картинки, вся картинка на лицевой стороне, изнанка — чистая бумага из её края.
    private static Mesh LeasePageMesh(Mesh source, Vector3 normal, Vector3 up, Vector3 right, float aspect)
    {
        var mesh = UnityEngine.Object.Instantiate(source);
        mesh.name = "LeaseContractSheet";
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Bounds bounds = source.bounds;
        float height = Mathf.Abs(Vector3.Dot(bounds.size, up));
        float width = height * aspect;
        float sourceWidth = Mathf.Abs(Vector3.Dot(bounds.size, right));
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 local = vertices[i] - bounds.center;
            float x = Vector3.Dot(local, right) * width / sourceWidth;
            float y = Vector3.Dot(local, up);
            vertices[i] = bounds.center + right * x + up * y + normal * Vector3.Dot(local, normal);
            uv[i] = Vector3.Dot(normals[i], normal) < 0f ? new Vector2(0.03f, 0.55f) : new Vector2(x / width + 0.5f, y / height + 0.5f);
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.RecalculateBounds();
        AssetDatabase.DeleteAsset(LeaseMeshAsset);
        AssetDatabase.CreateAsset(mesh, LeaseMeshAsset);
        return mesh;
    }

    private static Material LeasePageMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(LeaseMaterialAsset);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, LeaseMaterialAsset);
        }
        var page = AssetDatabase.LoadAssetAtPath<Texture2D>(LeaseTexture);
        if (page == null) throw new InvalidOperationException("Нет текстуры " + LeaseTexture);
        material.SetTexture("_BaseMap", page);
        material.SetTexture("_MainTex", page);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // -------------------------------------------------------------------------------------------------
    // S-07. Камера как убежище: запасы на четыре месяца, засечки дней, следы бетонных работ сына
    // -------------------------------------------------------------------------------------------------

    // Пересобирает только группу "Shelter": остальная обстановка и стены не трогаются.
    [MenuItem("Tools/Basement/Dress Shelter")]
    public static void BuildShelter()
    {
        var level = UnityEngine.Object.FindFirstObjectByType<BasementLevel>(FindObjectsInactive.Include);
        if (level == null) throw new InvalidOperationException("В сцене нет BasementLevel.");
        Transform root = level.transform.Find(RootName);
        if (root == null) throw new InvalidOperationException("Нет обстановки: сначала Tools/Basement/Dress Rooms.");
        Transform old = root.Find(ShelterGroup);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        Transform shelter = Shelter(root);

        var ambient = root.GetComponent<UndergroundAmbientOverride>();
        var serialized = new SerializedObject(ambient);
        SerializedProperty list = serialized.FindProperty("renderers");
        var renderers = new List<Renderer>();
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is Renderer kept) renderers.Add(kept);
        renderers.AddRange(shelter.GetComponentsInChildren<Renderer>(true));
        list.arraySize = renderers.Count;
        for (int i = 0; i < renderers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        ambient.Apply();
        EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
    }

    private static Transform Shelter(Transform root)
    {
        var shelter = Group(root, ShelterGroup);
        var meshes = new ShelterMeshes();
        CellSupplies(Group(shelter, "Cell"), meshes);
        BlockStock(Group(shelter, "Block"), meshes);
        SonWork(Group(shelter, "Sealed door"), meshes);
        meshes.Save();
        return shelter;
    }

    // Запасы лежат внутри камеры: Хейл заперт и до блока D не дотянется. Середина и проём свободны, по ним идёт монстр (S-09).
    private static void CellSupplies(Transform cell, ShelterMeshes meshes)
    {
        var mattress = new GameObject("Mattress", typeof(MeshFilter), typeof(MeshRenderer));
        mattress.transform.SetParent(cell, false);
        mattress.transform.SetPositionAndRotation(new Vector3(-45.26f, Floor, 106.96f), Quaternion.Euler(0f, 181.5f, 0f));
        mattress.GetComponent<MeshFilter>().sharedMesh = meshes.Mattress(0.86f, 1.88f, 0.13f);
        mattress.GetComponent<MeshRenderer>().sharedMaterials = new[] { ShelterMaterial("ShelterMattress", 0.05f), ShelterMaterial("ShelterMattressSide", 0.05f) };

        // Засечки на южной стене: их видно сквозь решётку поверх головы сидящего Хейла, и они попадают в кадр камеры на кронштейне.
        Material tally = Decal("ShelterTally", "ShelterTally", 0.05f, ShelterFolder);
        tally.SetColor("_BaseColor", new Color(0.3f, 0.27f, 0.24f)); // картинка светлая: на бетоне стены засечки терялись
        Card(cell, "Day tally", tally, new Vector3(-44.72f, Floor + 1.42f, BasementLayout.SouthRoomsZMin + 0.006f),
            Vector3.forward, Vector3.up, new Vector2(1.38f, 0.92f));

        Material carton = ShelterMaterial("ShelterCarton", 0.08f);
        Carton(cell, meshes, carton, -43.68f, 107.86f, 0f, 5f);
        Carton(cell, meshes, carton, -43.7f, 107.84f, CartonSize.y, -7f);
        foreach (Vector3 can in new[]
        {
            new Vector3(-43.8f, 107.9f, 0f), new Vector3(-43.7f, 107.78f, 1f), new Vector3(-43.6f, 107.92f, 2f), new Vector3(-43.82f, 107.76f, 1f)
        })
            Can(cell, meshes, new Vector3(can.x, Floor + CartonSize.y * 2f, can.y), (int)can.z, false, Quaternion.Euler(0f, can.x * 900f, 0f));
        foreach (Vector3 can in new[]
        {
            new Vector3(-43.58f, 107.4f, 2f), new Vector3(-43.66f, 107.28f, 0f), new Vector3(-43.55f, 107.2f, 1f)
        })
            Can(cell, meshes, new Vector3(can.x, Floor, can.y), (int)can.z, false, Quaternion.Euler(0f, can.y * 700f, 0f));

        // Вода вдоль восточной стены; ведро остаётся в дальнем углу, подальше от еды.
        Model(cell, "plastic_bottle_gallon", -43.56f, 106.98f, 90f, 0f, false);
        Model(cell, "plastic_bottle_gallon", -43.56f, 106.8f, 80f, 0f, false);
        Model(cell, "plastic_bottle_gallon", -43.74f, 106.9f, 100f, 0f, false);
        Model(cell, "plastic_bottle_gallon", -44.08f, 106.08f, 15f, 0.13f, false, 0f, 88f); // пустая, на боку

        // Пустые банки сметены к южной стене: за четыре месяца их набралась гора.
        var random = new System.Random(107);
        for (int i = 0; i < 24; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f, reach = Mathf.Sqrt((float)random.NextDouble());
            var at = new Vector3(-44.5f + Mathf.Cos(angle) * reach * 0.27f, Floor + CanRadius, 106.1f + Mathf.Abs(Mathf.Sin(angle)) * reach * 0.26f);
            bool upper = i >= 17;
            if (upper) at += Vector3.up * CanRadius * 1.7f;
            Can(cell, meshes, at, i % 3, true, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Quaternion.Euler(90f, 0f, 0f));
        }
    }

    // Запас, собранный одним человеком за один раз. Полоса у решётки свободна: там ходит её створка.
    private static void BlockStock(Transform block, ShelterMeshes meshes)
    {
        Material carton = ShelterMaterial("ShelterCarton", 0.08f);
        Carton(block, meshes, carton, -42.2f, 111.62f, 0f, 3f);
        Carton(block, meshes, carton, -41.98f, 111.62f, 0f, -4f, 0.42f);
        Carton(block, meshes, carton, -42.2f, 111.6f, CartonSize.y, -8f);
        Carton(block, meshes, carton, -42.02f, 111.64f, CartonSize.y, 6f, 0.42f);
        Carton(block, meshes, carton, -42.1f, 111.62f, CartonSize.y * 2f, 14f);
        Model(block, "plastic_bottle_gallon", -44.42f, 111.6f, 90f, 0f, false, 0.72f);
        Model(block, "plastic_bottle_gallon", -44.2f, 111.62f, 80f, 0f, false, 0.72f);
        Model(block, "plastic_bottle_gallon", -43.98f, 111.58f, 100f, 0f, false, 0.72f);
    }

    // Сын бетонировал со стороны лестницы и ушёл наверх: инструменты брошены там, с обратной стороны только наплывы бетона.
    private static void SonWork(Transform work, ShelterMeshes meshes)
    {
        float centerZ = DepthsLayout.SealedDoor.z;
        Card(work, "Sealed door back", ShelterMaterial("SealedDoorBack", 0.1f), new Vector3(-36.944f, Floor + BasementLayout.DoorHeight * 0.5f, centerZ),
            Vector3.right, Vector3.up, new Vector2(DepthsLayout.SealedDoorSize.z, BasementLayout.DoorHeight));
        Card(work, "Cement splatter", Decal("CementSplatter", "CementSplatter", 0.08f, ShelterFolder), new Vector3(-38.62f, Floor + 0.009f, centerZ - 0.1f),
            Vector3.up, Quaternion.Euler(0f, 74f, 0f) * Vector3.forward, new Vector2(1.3f, 1.05f));

        var tub = new GameObject("Mortar tub", typeof(MeshFilter), typeof(MeshRenderer));
        tub.transform.SetParent(work, false);
        tub.transform.SetPositionAndRotation(new Vector3(-38.42f, Floor, centerZ - 0.62f), Quaternion.Euler(0f, 97f, 0f));
        tub.GetComponent<MeshFilter>().sharedMesh = meshes.Tub(new Vector3(0.72f, 0.2f, 0.48f));
        tub.GetComponent<MeshRenderer>().sharedMaterial = ShelterMaterial("MortarTub", 0.12f);
        AddCollider(tub);

        Card(work, "Trowel", Decal("Trowel", "Trowel", 0.25f, ShelterFolder), new Vector3(-38.42f, Floor + 0.203f, centerZ - 0.62f),
            Vector3.up, Quaternion.Euler(0f, 38f, 0f) * Vector3.forward, new Vector2(0.1f, 0.3f));
        Model(work, "cement_bag", -38.2f, centerZ + 0.46f, 88f, 0f, false);
    }

    private static void Carton(Transform parent, ShelterMeshes meshes, Material material, float x, float z, float lift, float yaw, float length = 0f)
    {
        Vector3 size = length > 0f ? new Vector3(length, CartonSize.y, CartonSize.z) : CartonSize;
        var box = new GameObject("Carton", typeof(MeshFilter), typeof(MeshRenderer));
        box.transform.SetParent(parent, false);
        box.transform.SetPositionAndRotation(new Vector3(x, Floor + lift, z), Quaternion.Euler(0f, yaw, 0f));
        box.GetComponent<MeshFilter>().sharedMesh = meshes.Box(size);
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void Can(Transform parent, ShelterMeshes meshes, Vector3 at, int label, bool empty, Quaternion rotation)
    {
        var can = new GameObject(empty ? "Empty can" : "Can", typeof(MeshFilter), typeof(MeshRenderer));
        can.transform.SetParent(parent, false);
        can.transform.SetPositionAndRotation(at, rotation);
        can.GetComponent<MeshFilter>().sharedMesh = meshes.Can(label, empty);
        var renderer = can.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { ShelterMaterial("ShelterCanLabels", 0.2f), AssetDatabase.LoadAssetAtPath<Material>("Assets/Basement/Duct/Materials/DuctSteel.mat") };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Плоская картинка на стене или на полу.
    private static void Card(Transform parent, string name, Material material, Vector3 at, Vector3 normal, Vector3 up, Vector2 size)
    {
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        card.name = name;
        UnityEngine.Object.DestroyImmediate(card.GetComponent<Collider>());
        card.transform.SetParent(parent, false);
        card.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-normal, up));
        card.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = card.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material ShelterMaterial(string texture, float smoothness)
    {
        string path = Rooms + "Materials/" + texture + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Rooms + ShelterFolder + texture + ".png"));
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // В файле камня лежат все три LOD сразу: достаточно самого лёгкого, камни мелкие.
    private static void Stone(Transform parent, float x, float z, float yaw, float height) =>
        Keep(Model(parent, "stone_01", x, z, yaw, height, false), "stone_01_LOD2");

    private static Transform Group(Transform root, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(root, false);
        return group;
    }

    // Ставит модель основанием на пол (или на высоту lift над полом). height > 0 — привести к этой высоте, иначе масштаб файла: модели в метрах.
    private static GameObject Model(Transform parent, string id, float x, float z, float yaw, float height, bool solid, float lift = 0f, float tilt = 0f)
    {
        GameObject model = Instantiate(id, parent);
        model.transform.rotation = Quaternion.Euler(tilt, yaw, 0f) * model.transform.rotation; // у FBX бывает свой поворот корня
        Bounds bounds = WorldBounds(model);
        if (height > 0f) model.transform.localScale *= height / bounds.size.y;
        bounds = WorldBounds(model);
        model.transform.position += new Vector3(x - bounds.center.x, Floor + lift - bounds.min.y, z - bounds.center.z);
        if (solid) AddCollider(model);
        return model;
    }

    // В файле несколько вариантов предмета рядом: остаётся только нужный и встаёт на то же место.
    private static void Keep(GameObject model, params string[] parts)
    {
        var keep = new HashSet<string>(parts);
        Vector3 before = WorldBounds(model).center;
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>(true))
            part.gameObject.SetActive(keep.Contains(part.name));
        Bounds after = WorldBounds(model);
        model.transform.position += new Vector3(before.x - after.center.x, Floor - after.min.y, before.z - after.center.z);
    }

    private static void WallModel(Transform parent, string id, Vector3 at, Vector3 outward)
    {
        GameObject model = Instantiate(id, parent);
        model.transform.rotation = Quaternion.LookRotation(outward) * model.transform.rotation;
        model.transform.position += at - WorldBounds(model).center;
    }

    private static GameObject Instantiate(string id, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models + id + "/" + id + ".fbx");
        if (prefab == null) throw new InvalidOperationException("Нет модели " + id);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Material material = ModelMaterial(id);
        foreach (Renderer part in model.GetComponentsInChildren<Renderer>(true))
        {
            var materials = new Material[part.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            part.sharedMaterials = materials;
        }
        return model;
    }

    // Стиральная и сушильная машины — из того же PSX-пакета, что и бойлер.
    private static void Appliance(Transform parent, string mesh, string texture, float x, float z, float yaw)
    {
        Mesh source = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/home-interior-pack/home-interior-pack.blend"))
            if (asset is Mesh candidate && candidate.name == mesh) source = candidate;
        if (source == null) throw new InvalidOperationException("Нет меша " + mesh);
        string path = Rooms + "Materials/" + mesh + "_PSX.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/home-interior-pack/textures/" + texture + ".png"));
            material.SetColor("_BaseColor", new Color(0.6f, 0.58f, 0.52f)); // пожелтевшая эмаль
            material.SetFloat("_Smoothness", 0.3f);
            Directory.CreateDirectory(Rooms + "Materials");
            AssetDatabase.CreateAsset(material, path);
        }
        var obj = new GameObject(mesh, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<MeshFilter>().sharedMesh = source;
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        // Меш из Blender: Z вверх, «лицом» к +Y.
        obj.transform.rotation = Quaternion.Euler(-90f, yaw, 0f);
        Bounds bounds = WorldBounds(obj);
        obj.transform.position += new Vector3(x - bounds.center.x, Floor - bounds.min.y, z - bounds.center.z);
        AddCollider(obj);
    }

    private static void Clone(Transform parent, BasementLevel level, string template, float x, float z, float yaw, Vector3 scale)
    {
        Transform source = null;
        foreach (Transform t in level.transform.parent.GetComponentsInChildren<Transform>(true))
            if (t.name == template) { source = t; break; }
        if (source == null) throw new InvalidOperationException("Нет объекта " + template);
        GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, parent);
        copy.name = template;
        copy.SetActive(true);
        copy.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * source.rotation;
        copy.transform.localScale = Vector3.Scale(source.lossyScale, scale);
        foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        Bounds bounds = WorldBounds(copy);
        copy.transform.position += new Vector3(x - bounds.center.x, Floor - bounds.min.y, z - bounds.center.z);
    }

    // Голая лампочка на патроне под балкой. Свет ставит Atmosphere, здесь — только модель.
    private static void Bulb(Transform parent, Vector3 ceiling)
    {
        GameObject socket = Instantiate("pull_chain_light_socket", parent);
        Bounds bounds = WorldBounds(socket);
        socket.transform.localScale *= 0.13f / bounds.size.y;
        bounds = WorldBounds(socket);
        socket.transform.position += ceiling - new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + Vector3.down * 0.2f;
        bounds = WorldBounds(socket);

        GameObject bulb = Instantiate("lightbulb_01", parent);
        Material glow = BulbMaterial();
        foreach (Renderer part in bulb.GetComponentsInChildren<Renderer>(true)) part.sharedMaterial = glow;
        Bounds glass = WorldBounds(bulb);
        bulb.transform.localScale *= 0.11f / glass.size.y;
        glass = WorldBounds(bulb);
        bulb.transform.position += new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - new Vector3(glass.center.x, glass.max.y, glass.center.z);
        // Источник света стоит внутри колбы: тень от самой лампы закрыла бы всю комнату.
        foreach (Renderer part in socket.GetComponentsInChildren<Renderer>(true)) part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (Renderer part in bulb.GetComponentsInChildren<Renderer>(true)) part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Bounds WorldBounds(GameObject obj)
    {
        Renderer[] parts = obj.GetComponentsInChildren<Renderer>(false);
        if (parts.Length == 0) throw new InvalidOperationException("Без рендереров: " + obj.name);
        Bounds bounds = parts[0].bounds;
        foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
        return bounds;
    }

    private static void AddCollider(GameObject model)
    {
        Bounds bounds = WorldBounds(model);
        var collider = new GameObject("Collider", typeof(BoxCollider));
        collider.transform.SetParent(model.transform.parent, false);
        collider.transform.position = bounds.center;
        collider.GetComponent<BoxCollider>().size = bounds.size;
    }

    // -------------------------------------------------------------------------------------------------
    // Балки перекрытия: одним мешем на комнату, под потолком, выше трубы хозяина
    // -------------------------------------------------------------------------------------------------

    private static void Joists(Transform root, ShellMeshes shell)
    {
        var mesh = new GameObject("Ceiling joists", typeof(MeshFilter), typeof(MeshRenderer));
        mesh.transform.SetParent(root, false);
        var builder = new JoistMesh();
        const float width = 0.14f, depth = 0.2f, step = 0.8f;
        for (float z = BasementLayout.HallB.ZMin + 0.3f; z < BasementLayout.HallB.ZMax; z += step)
            builder.AddAlongX(BasementLayout.HallB.XMin, BasementLayout.HallB.XMax, z, Ceiling, width, depth);
        for (float x = BasementLayout.CellBlockD.XMin + 0.3f; x < BasementLayout.CellBlockD.XMax; x += step)
            builder.AddAlongZ(BasementLayout.CellBlockD.ZMin, BasementLayout.CellBlockD.ZMax, x, Ceiling, width, depth);
        Mesh result = builder.Build();
        result.name = "Joists";
        shell.Add(result);
        mesh.GetComponent<MeshFilter>().sharedMesh = result;
        mesh.GetComponent<MeshRenderer>().sharedMaterial = Surface("RoomCeiling", "weathered_planks", 0.08f);
    }

    // -------------------------------------------------------------------------------------------------
    // Грязь на полу: плоские пятна чуть выше пола. Decal-проекторы потребовали бы менять URP-рендерер
    // -------------------------------------------------------------------------------------------------

    private static void FloorDirt(Transform root, ShellMeshes shell)
    {
        var random = new System.Random(4242);
        Rect[] floors =
        {
            Rect.MinMaxRect(-36.9f, 106.5f, -32.2f, 118.4f), Rect.MinMaxRect(-45.7f, 107.6f, -38.8f, 111.9f),
            Rect.MinMaxRect(-38.8f, 110.5f, -36.9f, 111.9f), Rect.MinMaxRect(-45.7f, 105.9f, -43.4f, 107.6f),
            Rect.MinMaxRect(-42.6f, 105.9f, -41.0f, 107.6f), Rect.MinMaxRect(-40.4f, 105.9f, -38.8f, 107.6f)
        };
        var grime = new FloorQuads(random);
        var dust = new FloorQuads(random);
        foreach (Rect floor in floors)
        {
            int count = Mathf.CeilToInt(floor.width * floor.height * 0.55f);
            for (int i = 0; i < count; i++)
            {
                Vector2 at = new Vector2(floor.xMin + floor.width * (float)random.NextDouble(), floor.yMin + floor.height * (float)random.NextDouble());
                float size = 0.9f + 2.2f * (float)random.NextDouble();
                grime.Add(at, new Vector2(size, size * (0.7f + 0.3f * (float)random.NextDouble())));
                if (i % 2 == 0)
                {
                    float scatter = 0.5f + (float)random.NextDouble();
                    dust.Add(at + new Vector2(0.4f, -0.3f), new Vector2(scatter, scatter));
                }
            }
        }
        // Вдоль стен грязь гуще: туда сметают и там не ходят.
        for (float z = 107f; z < 118f; z += 0.9f)
        {
            grime.Add(new Vector2(-36.7f, z), new Vector2(1.3f, 1.5f));
            grime.Add(new Vector2(-32.4f, z), new Vector2(1.3f, 1.5f));
        }
        var puddles = new FloorQuads(random);
        puddles.Add(new Vector2(-36.1f, 109.2f), new Vector2(1.5f, 1.05f), 0);
        puddles.Add(new Vector2(-34.4f, 113.0f), new Vector2(0.9f, 1.3f), 1);
        puddles.Add(new Vector2(-41.6f, 109.9f), new Vector2(1.6f, 1.1f), 2);
        puddles.Add(new Vector2(-45.0f, 109.3f), new Vector2(0.8f, 0.7f), 3);
        var papers = new FloorQuads(random, 5);
        foreach (Vector2 at in new[]
        {
            new Vector2(-35.7f, 111.2f), new Vector2(-36.2f, 115.2f), new Vector2(-33.4f, 108.9f), new Vector2(-32.9f, 115.7f),
            new Vector2(-35.2f, 107.9f), new Vector2(-40.3f, 110.4f), new Vector2(-43.2f, 109.7f), new Vector2(-39.6f, 108.6f), new Vector2(-44.9f, 111.0f),
            new Vector2(-34.9f, 116.9f), new Vector2(-36.4f, 110.6f), new Vector2(-33.0f, 111.2f), new Vector2(-42.3f, 111.1f), new Vector2(-38.1f, 111.3f)
        })
            papers.Add(at, new Vector2(0.3f, 0.42f));

        Layer(root, shell, "Floor grime", grime, Decal("FloorGrime", "Grime", 0.05f), 0.004f);
        Layer(root, shell, "Floor dust", dust, Decal("FloorDust", "Dust", 0.05f), 0.006f);
        Layer(root, shell, "Puddles", puddles, Decal("FloorPuddle", "Puddle", 0.92f), 0.008f);
        Layer(root, shell, "Papers", papers, Decal("FloorPaper", "Paper", 0.1f), 0.012f);
    }

    private static void Layer(Transform root, ShellMeshes shell, string name, FloorQuads quads, Material material, float height)
    {
        var layer = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        layer.transform.SetParent(root, false);
        Mesh mesh = quads.Build(Floor + height);
        mesh.name = name;
        shell.Add(mesh);
        layer.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = layer.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Прозрачный Lit: пятно берёт свет ламп и фонарика, а не светится само.
    private static Material Decal(string name, string texture, float smoothness, string folder = "Decals/")
    {
        string path = Rooms + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        string texturePath = Rooms + folder + texture + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.SetShaderPassEnabled("DepthOnly", false);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        Directory.CreateDirectory(Rooms + "Materials");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // -------------------------------------------------------------------------------------------------
    // Свет и звук: очень тусклые тёплые лампы с тенями, чтобы свет не проходил сквозь перекрытие в дом
    // -------------------------------------------------------------------------------------------------

    private static void Atmosphere(Transform root, BasementLevel level)
    {
        var atmosphere = root.gameObject.AddComponent<BasementRoomAtmosphere>();
        Light north = Lamp(root, "Hall lamp north", new Vector3(-34.2f, Ceiling - 0.42f, 116.3f), 0.85f, 6.5f);
        Light south = Lamp(root, "Hall lamp south", new Vector3(-35.1f, Ceiling - 0.42f, 108.7f), 0.5f, 5.5f);
        level.cellLamp.color = BulbColor;
        level.cellLamp.intensity = 0.7f;
        level.cellLamp.range = 6.5f;
        level.cellLamp.shadows = LightShadows.Hard;

        var mixer = AudioRouting.Group(AudioBus.World);
        AudioSource heater = Loop(root, "Water heater hum", new Vector3(-33.0f, Floor + 0.6f, 117.4f), Clip("GasWaterHeater.ogg"), mixer, 0.32f, 1.2f, 10f);
        AudioSource drops = Loop(root, "Water drops", new Vector3(-36.2f, Floor + 0.3f, 109.2f), Clip("WaterDrops.ogg"), mixer, 0.4f, 1f, 8f);
        AudioSource buzzNorth = Loop(root, "Lamp buzz north", north.transform.position, AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Game/Ambience/LampBuzz.ogg"), mixer, 0.12f, 0.6f, 5f);
        AudioSource buzzCell = Loop(root, "Lamp buzz block", level.cellLamp.transform.position, buzzNorth.clip, mixer, 0.1f, 0.6f, 5f);
        AudioSource flicker = Loop(root, "Lamp flicker", south.transform.position, Clip("BulbFlicker.ogg"), mixer, 0.3f, 0.8f, 7f);
        flicker.loop = false;

        var serialized = new SerializedObject(atmosphere);
        SetArray(serialized.FindProperty("lamps"), north, south);
        SetArray(serialized.FindProperty("loops"), heater, drops, buzzNorth, buzzCell);
        serialized.FindProperty("flickerLamp").objectReferenceValue = south;
        serialized.FindProperty("flickerSound").objectReferenceValue = flicker;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        level.atmosphere = atmosphere;
        EditorUtility.SetDirty(level);
    }

    private static void SetArray(SerializedProperty property, params UnityEngine.Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static Light Lamp(Transform root, string name, Vector3 at, float intensity, float range)
    {
        var lamp = new GameObject(name).AddComponent<Light>();
        lamp.transform.SetParent(root, false);
        lamp.transform.position = at;
        lamp.type = LightType.Point;
        lamp.color = BulbColor;
        lamp.intensity = intensity;
        lamp.range = range;
        lamp.shadows = LightShadows.Hard;
        lamp.enabled = false;
        return lamp;
    }

    private static AudioSource Loop(Transform root, string name, Vector3 at, AudioClip clip, UnityEngine.Audio.AudioMixerGroup mixer, float volume, float minDistance, float maxDistance)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.position = at;
        var source = obj.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        if (mixer != null) source.outputAudioMixerGroup = mixer;
        return source;
    }

    private static AudioClip Clip(string file)
    {
        string path = Rooms + "Audio/" + file;
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        if (!importer.forceToMono) { importer.forceToMono = true; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // -------------------------------------------------------------------------------------------------
    // Материалы
    // -------------------------------------------------------------------------------------------------

    private static Material ModelMaterial(string id)
    {
        string folder = Models + id + "/";
        string path = folder + id + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = Lit(folder + "Textures/" + id, 0.15f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material Surface(string name, string texture, float smoothness)
    {
        string path = Rooms + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = Lit(Rooms + "Textures/" + texture, smoothness);
        Directory.CreateDirectory(Rooms + "Materials");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material Lit(string texturePrefix, float smoothness)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", Texture(texturePrefix + "_diff_1k.jpg", false));
        Texture2D normal = Texture(texturePrefix + "_nor_gl_1k.jpg", true);
        if (normal != null) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
        Texture2D occlusion = Texture(texturePrefix + "_ao_1k.jpg", false);
        if (occlusion != null) { material.SetTexture("_OcclusionMap", occlusion); material.EnableKeyword("_OCCLUSIONMAP"); }
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", 0f);
        return material;
    }

    private static Texture2D Texture(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.maxTextureSize != 512 || (normal && importer.textureType != TextureImporterType.NormalMap))
        {
            importer.maxTextureSize = 512;
            if (normal) importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material BulbMaterial()
    {
        string path = Rooms + "Materials/RoomBulb.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(1f, 0.8f, 0.5f));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", BulbColor * 2.2f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        Directory.CreateDirectory(Rooms + "Materials");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // -------------------------------------------------------------------------------------------------
    // Меши
    // -------------------------------------------------------------------------------------------------

    // Единичный куб с UV в мировых метрах: подставляется вместо Cube без изменения масштаба и коллайдера.
    private sealed class ShellMeshes
    {
        private readonly List<Mesh> meshes = new List<Mesh>();

        public void Add(Mesh mesh) => meshes.Add(mesh);

        public Mesh Box(Vector3 position, Vector3 size)
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tangents = new List<Vector4>(24);
            var triangles = new List<int>(36);
            Face(Vector3.right, Vector3.forward, Vector3.up); Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.forward, Vector3.left, Vector3.up); Face(Vector3.back, Vector3.right, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward); Face(Vector3.down, Vector3.left, Vector3.forward);

            void Face(Vector3 normal, Vector3 u, Vector3 v)
            {
                int start = vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    float su = i == 1 || i == 2 ? 0.5f : -0.5f, sv = i >= 2 ? 0.5f : -0.5f;
                    Vector3 local = normal * 0.5f + u * su + v * sv;
                    Vector3 world = position + Vector3.Scale(local, size);
                    vertices.Add(local);
                    normals.Add(normal);
                    tangents.Add(new Vector4(u.x, u.y, u.z, Vector3.Dot(Vector3.Cross(normal, u), v) > 0f ? 1f : -1f));
                    uvs.Add(new Vector2(Vector3.Dot(world, u), Vector3.Dot(world, v)) / Tile);
                }
                bool front = Vector3.Dot(Vector3.Cross(u, v), normal) > 0f;
                if (front) triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            var mesh = new Mesh { name = "Shell " + meshes.Count };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        public void Save()
        {
            AssetDatabase.DeleteAsset(ShellAsset);
            var holder = new Mesh { name = "RoomShell" };
            AssetDatabase.CreateAsset(holder, ShellAsset);
            foreach (Mesh mesh in meshes) AssetDatabase.AddObjectToAsset(mesh, holder);
            AssetDatabase.SaveAssets();
        }
    }

    // Меши убежища: основание каждого стоит на нуле, повторы кэшируются и лежат в одном файле.
    private sealed class ShelterMeshes
    {
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public Mesh Box(Vector3 size) => Cached("Carton " + size.x, () => BoxMesh(size, false));

        public Mesh Tub(Vector3 size) => Cached("Mortar tub", () => BoxMesh(size, true));

        public Mesh Can(int label, bool empty) => Cached((empty ? "Empty can " : "Can ") + label, () => CanMesh(label, empty));

        public Mesh Mattress(float width, float length, float height) => Cached("Mattress", () => MattressMesh(width, length, height));

        private Mesh Cached(string name, Func<Mesh> build)
        {
            if (meshes.TryGetValue(name, out Mesh mesh)) return mesh;
            mesh = build();
            mesh.name = name;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            meshes.Add(name, mesh);
            return mesh;
        }

        public void Save()
        {
            AssetDatabase.DeleteAsset(ShelterMeshAsset);
            var holder = new Mesh { name = "ShelterMeshes" };
            AssetDatabase.CreateAsset(holder, ShelterMeshAsset);
            foreach (Mesh mesh in meshes.Values) AssetDatabase.AddObjectToAsset(mesh, holder);
            AssetDatabase.SaveAssets();
        }

        // Коробка: картинка на каждой грани надписью вверх. edgeSides — боковины берут цвет с края картинки (корыто видно в основном сверху).
        private static Mesh BoxMesh(Vector3 size, bool edgeSides)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float x = size.x * 0.5f, z = size.z * 0.5f;
            Vector3 a = new Vector3(-x, 0f, -z), b = new Vector3(x, 0f, -z), c = new Vector3(x, 0f, z), d = new Vector3(-x, 0f, z), up = Vector3.up * size.y;
            Side(a, b); Side(b, c); Side(c, d); Side(d, a);
            int top = vertices.Count;
            vertices.AddRange(new[] { a + up, d + up, c + up, b + up });
            uvs.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            Quad(triangles, top);

            void Side(Vector3 from, Vector3 to)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { from, from + up, to + up, to });
                if (edgeSides) uvs.AddRange(new[] { new Vector2(0.015f, 0.4f), new Vector2(0.015f, 0.45f), new Vector2(0.015f, 0.55f), new Vector2(0.015f, 0.6f) });
                else uvs.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                Quad(triangles, start);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        // Банка: бумажная этикетка (полоса атласа) и жесть. Открытая — без крышки, с тёмным нутром.
        private static Mesh CanMesh(int label, bool empty)
        {
            const int sides = 14;
            float bandFrom = 1f - (label + 1) / 3f, bandTo = 1f - label / 3f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var paper = new List<int>();
            var steel = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 from = Rim(i), to = Rim(i + 1), up = Vector3.up * CanHeight;
                float u = (float)i / sides, next = (float)(i + 1) / sides;
                int start = vertices.Count;
                vertices.AddRange(new[] { from, from + up, to + up, to });
                uvs.AddRange(new[] { new Vector2(u, bandFrom), new Vector2(u, bandTo), new Vector2(next, bandTo), new Vector2(next, bandFrom) });
                Quad(paper, start);

                if (empty)
                {
                    start = vertices.Count;
                    vertices.AddRange(new[] { to, to + up, from + up, from });
                    uvs.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                    Quad(steel, start);
                }
                Vector3 center = empty ? Vector3.up * 0.004f : up;
                start = vertices.Count;
                vertices.AddRange(new[] { center, (empty ? center + to : to + up), (empty ? center + from : from + up) });
                uvs.AddRange(new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), new Vector2(0.6f, 0.5f) });
                steel.AddRange(new[] { start, start + 1, start + 2 });
            }

            Vector3 Rim(int i)
            {
                float angle = i * Mathf.PI * 2f / sides;
                return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * CanRadius;
            }

            var mesh = new Mesh { subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(paper, 0);
            mesh.SetTriangles(steel, 1);
            return mesh;
        }

        // Матрас: продавленный верх сеткой и боковины. Голова — у южного торца, там на картинке тёмное пятно.
        private static Mesh MattressMesh(float width, float length, float height)
        {
            const int across = 8, along = 16;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var top = new List<int>();
            var sides = new List<int>();
            for (int j = 0; j <= along; j++)
                for (int i = 0; i <= across; i++)
                {
                    float u = (float)i / across, v = (float)j / along;
                    vertices.Add(new Vector3((u - 0.5f) * width, TopHeight(u, v), (0.5f - v) * length));
                    uvs.Add(new Vector2(u, 1f - v));
                }
            for (int j = 0; j < along; j++)
                for (int i = 0; i < across; i++)
                {
                    int a = j * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                    top.AddRange(new[] { a, b, d, a, d, c });
                }

            for (int i = 0; i < across; i++)
            {
                Wall((float)(i + 1) / across, 0f, (float)i / across, 0f);
                Wall((float)i / across, 1f, (float)(i + 1) / across, 1f);
            }
            for (int j = 0; j < along; j++)
            {
                Wall(1f, (float)(j + 1) / along, 1f, (float)j / along);
                Wall(0f, (float)j / along, 0f, (float)(j + 1) / along);
            }

            float TopHeight(float u, float v)
            {
                float edge = Mathf.Min(Mathf.Min(u, 1f - u) * width, Mathf.Min(v, 1f - v) * length);
                float round = Mathf.Sqrt(Mathf.Clamp01(edge / 0.05f));
                float dent = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.28f, 2f) - Mathf.Pow((v - 0.5f) / 0.3f, 2f));
                return height * (0.72f + 0.28f * round) - 0.035f * dent;
            }

            void Wall(float u0, float v0, float u1, float v1)
            {
                Vector3 from = new Vector3((u0 - 0.5f) * width, 0f, (0.5f - v0) * length), to = new Vector3((u1 - 0.5f) * width, 0f, (0.5f - v1) * length);
                float texFrom = (from.x * 1.3f + from.z * 0.7f) / 0.3f, texTo = (to.x * 1.3f + to.z * 0.7f) / 0.3f;
                int start = vertices.Count;
                vertices.AddRange(new[] { from, from + Vector3.up * TopHeight(u0, v0), to + Vector3.up * TopHeight(u1, v1), to });
                uvs.AddRange(new[] { new Vector2(texFrom, 0f), new Vector2(texFrom, 1f), new Vector2(texTo, 1f), new Vector2(texTo, 0f) });
                Quad(sides, start);
            }

            var mesh = new Mesh { subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(top, 0);
            mesh.SetTriangles(sides, 1);
            return mesh;
        }

        private static void Quad(List<int> triangles, int start) =>
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    // Плоские прямоугольники на полу с ячейками атласа 2×2; grid > 1 — лист с лёгкими изгибами.
    private sealed class FloorQuads
    {
        private readonly System.Random random;
        private readonly int grid;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public FloorQuads(System.Random random, int grid = 1) { this.random = random; this.grid = grid; }

        public void Add(Vector2 center, Vector2 size, int cell = -1)
        {
            if (cell < 0) cell = random.Next(0, 4);
            float yaw = (float)random.NextDouble() * Mathf.PI * 2f, cos = Mathf.Cos(yaw), sin = Mathf.Sin(yaw);
            var corner = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
            int start = vertices.Count;
            for (int j = 0; j <= grid; j++)
                for (int i = 0; i <= grid; i++)
                {
                    float u = (float)i / grid, v = (float)j / grid;
                    float x = (u - 0.5f) * size.x, z = (v - 0.5f) * size.y;
                    float lift = grid > 1 && i > 0 && i < grid && j > 0 && j < grid ? (float)random.NextDouble() * 0.012f : 0f;
                    vertices.Add(new Vector3(center.x + x * cos - z * sin, lift, center.y + x * sin + z * cos));
                    uvs.Add(corner + new Vector2(u, v) * 0.5f);
                }
            for (int j = 0; j < grid; j++)
                for (int i = 0; i < grid; i++)
                {
                    int a = start + j * (grid + 1) + i, b = a + 1, c = a + grid + 1, d = c + 1;
                    triangles.AddRange(new[] { a, c, d, a, d, b });
                }
        }

        public Mesh Build(float height)
        {
            for (int i = 0; i < vertices.Count; i++) vertices[i] += Vector3.up * height;
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // Брусья со скошенными рёбрами; длина набирается из одной текстуры досок.
    private sealed class JoistMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public void AddAlongX(float from, float to, float z, float top, float width, float depth) =>
            Add(new Vector3(from, top, z), new Vector3(to, top, z), Vector3.forward, width, depth);

        public void AddAlongZ(float from, float to, float x, float top, float width, float depth) =>
            Add(new Vector3(x, top, to), new Vector3(x, top, from), Vector3.right, width, depth);

        private void Add(Vector3 a, Vector3 b, Vector3 side, float width, float depth)
        {
            float w = width * 0.5f, bevel = 0.015f;
            // Сечение: верх уходит в потолок, низ со скосами.
            Vector2[] section =
            {
                new Vector2(-w, 0f), new Vector2(-w, -depth + bevel), new Vector2(-w + bevel, -depth),
                new Vector2(w - bevel, -depth), new Vector2(w, -depth + bevel), new Vector2(w, 0f)
            };
            float length = (b - a).magnitude, v = 0f;
            for (int i = 0; i < section.Length - 1; i++)
            {
                float edge = (section[i + 1] - section[i]).magnitude;
                int start = vertices.Count;
                vertices.Add(a + side * section[i].x + Vector3.up * section[i].y);
                vertices.Add(b + side * section[i].x + Vector3.up * section[i].y);
                vertices.Add(b + side * section[i + 1].x + Vector3.up * section[i + 1].y);
                vertices.Add(a + side * section[i + 1].x + Vector3.up * section[i + 1].y);
                float offset = a.x * 0.37f + a.z * 0.61f;
                uvs.Add(new Vector2(offset, v)); uvs.Add(new Vector2(offset + length / Tile, v));
                uvs.Add(new Vector2(offset + length / Tile, v + edge / Tile)); uvs.Add(new Vector2(offset, v + edge / Tile));
                v += edge / Tile;
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        public Mesh Build()
        {
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
