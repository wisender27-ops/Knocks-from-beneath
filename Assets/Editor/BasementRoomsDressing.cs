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

        var hit = sheet.gameObject.AddComponent<BoxCollider>();
        hit.center = mesh.bounds.center;
        Vector3 hitSize = size;
        hitSize[thin] = 0.03f / Mathf.Max(0.0001f, Mathf.Abs(sheet.lossyScale[thin]));
        hit.size = hitSize;

        var clue = sheet.gameObject.AddComponent<InspectableClue>();
        Vector2 page = new Vector2(size[3 - thin - longAxis] * Mathf.Abs(sheet.lossyScale[3 - thin - longAxis]),
            size[longAxis] * Mathf.Abs(sheet.lossyScale[longAxis])) * 0.92f;
        clue.Configure("clue-lease", "Договор аренды", false, normal, up, page, true,
            "RESIDENTIAL LEASE\n\nLANDLORD: W. HALE\nTENANT: DANIEL ROSS",
            "Договор аренды старого дома. Арендодатель: У. Хейл. Арендатор: Дэниел Росс.",
            new[]
            {
                "Это мой договор. Мой старый дом.",
                "Я платил через контору и ни разу его не видел. Хейл.",
                "Его сын сам нашёл меня и отдал этот дом почти даром. Это не совпадение."
            });
        return clue;
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
    private static Material Decal(string name, string texture, float smoothness)
    {
        string path = Rooms + "Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        string texturePath = Rooms + "Decals/" + texture + ".png";
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
