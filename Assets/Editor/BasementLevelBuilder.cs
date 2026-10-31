using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KnocksFromBeneath;

// Собирает подвал по BasementLayout прямо в открытой сцене, дочерним объектом Basement_Reference.
// Существующий объект не перезаписывается: ручные правки живут в сцене.
public sealed class BasementLevelBuilder
{
    private const string RootName = "Basement Level";
    private const string OwnerPrefabPath = "Assets/Resources/Basement/BasementOwner.prefab";
    private const string CamcorderPrefabPath = "Assets/Resources/Phase4/Camcorder.prefab";
    private const int InteractableLayer = 6;
    private GameObject extension, cellDoorCollider, lockVisual;
    private BasementCellLock cellLock;
    private Transform reference, cellDoor;
    private BasementCellLock hideSpot;
    private Light cellLamp;
    private Material wallMaterial, floorMaterial, ceilingMaterial, metalMaterial, woodMaterial, rugMaterial, pipeMaterial;

    [MenuItem("Tools/Basement/Build Level In Scene")]
    public static void Build()
    {
        Transform reference = null;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Basement_Reference") reference = root.transform;
        if (reference == null) throw new InvalidOperationException("Basement_Reference не найден в открытой сцене.");
        if (reference.Find(RootName) != null)
            throw new InvalidOperationException(RootName + " уже есть в сцене; builder не перезаписывает ручные изменения.");
        if (reference.lossyScale != Vector3.one || reference.rotation != Quaternion.identity)
            throw new InvalidOperationException("Basement_Reference повёрнут или масштабирован: план подвала в мировых координатах не совпадёт.");

        var builder = new BasementLevelBuilder { reference = reference };
        builder.extension = new GameObject(RootName);
        builder.extension.transform.SetParent(reference, true);
        builder.extension.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        try
        {
            builder.LoadMaterials();
            builder.OpenExistingWall();
            foreach (BasementRoom room in BasementLayout.AllRooms) builder.BuildRoom(room);
            builder.BuildProps(); builder.BuildJunk(); builder.BuildHideSpot(); builder.BuildClonedProps();
            builder.BuildPipe(); builder.BuildCell(); builder.BuildLights();
            foreach (var renderer in builder.extension.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && !EditorUtility.IsPersistent(material))
                        throw new InvalidOperationException("Временный материал на объекте: " + renderer.name);
            var ownerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OwnerPrefabPath);
            if (ownerPrefab == null) throw new InvalidOperationException("BasementOwner.prefab не найден.");
            var owner = (GameObject)PrefabUtility.InstantiatePrefab(ownerPrefab, builder.extension.transform);
            owner.transform.SetPositionAndRotation(BasementLayout.OwnerPosition, Quaternion.identity);

            var level = builder.extension.AddComponent<BasementLevel>();
            level.cellDoor = builder.cellDoor;
            level.cellDoorCollider = builder.cellDoorCollider;
            level.lockVisual = builder.lockVisual;
            level.cellLock = builder.cellLock;
            level.hideSpot = builder.hideSpot;
            level.owner = owner.GetComponent<BasementOwner>();
            level.cellLamp = builder.cellLamp;
            AddTapeCamera(level);
            EditorSceneManager.MarkSceneDirty(reference.gameObject.scene);
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(builder.extension);
            throw;
        }
    }

    // C-04: кронштейн на северной стене блока D, на нём старая камера хозяина; tapeView — её объектив.
    public static void AddTapeCamera(BasementLevel level)
    {
        if (level.tapeView != null) throw new InvalidOperationException("Камера хозяина уже есть.");
        var root = new GameObject("Walter's camcorder").transform;
        root.SetParent(level.transform, true);
        Vector3 lens = BasementLayout.TapeCameraPosition;
        var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shelf.name = "Bracket";
        shelf.transform.SetParent(root, false);
        shelf.transform.position = new Vector3(lens.x, lens.y - 0.17f, lens.z + 0.05f);
        shelf.transform.localScale = new Vector3(0.4f, 0.04f, 0.4f);
        shelf.GetComponent<Renderer>().sharedMaterial = Material("OldTimber");
        var camcorderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CamcorderPrefabPath);
        if (camcorderPrefab == null) throw new InvalidOperationException("Camcorder.prefab не найден.");
        Vector3 forward = BasementLayout.TapeCameraLook - lens;
        var camcorder = (GameObject)PrefabUtility.InstantiatePrefab(camcorderPrefab, root);
        // Объектив модели смотрит вдоль её −Z.
        camcorder.transform.SetPositionAndRotation(lens, Quaternion.LookRotation(-forward));
        Bounds bounds = camcorder.GetComponentInChildren<Renderer>().bounds;
        foreach (Renderer part in camcorder.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
        camcorder.transform.localScale *= 0.32f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        var view = new GameObject("Lens").transform;
        view.SetParent(root, false);
        view.SetPositionAndRotation(lens, Quaternion.LookRotation(forward));
        level.tapeView = view;
    }

    private void LoadMaterials()
    {
        wallMaterial = Material("AgedConcrete"); floorMaterial = Material("ConcreteFloor");
        ceilingMaterial = Material("OldTimber"); metalMaterial = Material("DarkMetal");
        woodMaterial = Material("OldTimber"); rugMaterial = Material("OldRug");
        pipeMaterial = Material("AgedPipe");
    }

    private void BuildHideSpot()
    {
        Vector3 center = BasementLayout.HideCrateCenter;
        float boardsBase = BasementLayout.FloorY;
        var crate = Box("Hide behind crates", extension.transform, center, new Vector3(1.3f, 1.1f, 0.45f), woodMaterial);
        crate.layer = InteractableLayer;
        hideSpot = crate.AddComponent<BasementCellLock>();
        crate.GetComponent<Renderer>().enabled = false;
        var lower = Box("Lower boards", crate.transform, new Vector3(center.x, boardsBase + 0.15f, center.z), Vector3.one, woodMaterial, false);
        lower.transform.localScale = new Vector3(1f, 0.3f / 1.1f, 1f);
        var upper = Box("Upper boards", crate.transform, new Vector3(center.x, boardsBase + 0.95f, center.z), Vector3.one, woodMaterial, false);
        upper.transform.localScale = new Vector3(1f, 0.7f / 1.1f, 1f);
    }

    private static Material Material(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Basement/Materials/" + name + ".mat");
        if (material == null) throw new InvalidOperationException("Материал не найден: " + name);
        return material;
    }

        private GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent != null ? parent : extension.transform, false);
            box.transform.position = center;
            box.transform.localScale = size;
            var renderer = box.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        private GameObject Cylinder(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = center;
            obj.transform.localScale = size;   // у цилиндра Unity высота — 2 × scale.y
            if (material != null) obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        // Восточную стену существующей комнаты заменяем на стену с проёмом в коридор A.
        private void OpenExistingWall()
        {
            if (reference == null) { Debug.LogWarning("[P-03] Basement_Reference не найден: проём в стене не сделан."); return; }
            foreach (Transform t in reference.GetComponentsInChildren<Transform>(true))
                if (t.name == "LeftWall" || t.name == "LeftWall_Masonry") t.gameObject.SetActive(false);

            float h = BasementLayout.CeilingY - BasementLayout.FloorY - 0.05f;
            float x = BasementLayout.ExistingWallX, thick = 0.25f;
            float zMin = BasementLayout.ExistingRoomZMin, zMax = BasementLayout.ExistingRoomZMax;
            float gapFrom = BasementLayout.EntranceFrom, gapTo = BasementLayout.EntranceTo;
            float baseY = BasementLayout.FloorY;
            Box("Wall south of entrance", extension.transform, new Vector3(x, baseY + h * 0.5f, (zMin + gapFrom) * 0.5f),
                new Vector3(thick, h, gapFrom - zMin), wallMaterial);
            Box("Wall north of entrance", extension.transform, new Vector3(x, baseY + h * 0.5f, (gapTo + zMax) * 0.5f),
                new Vector3(thick, h, zMax - gapTo), wallMaterial);
            float lintel = h - BasementLayout.DoorHeight;
            Box("Entrance lintel", extension.transform, new Vector3(x, baseY + BasementLayout.DoorHeight + lintel * 0.5f, (gapFrom + gapTo) * 0.5f),
                new Vector3(thick, lintel, gapTo - gapFrom), wallMaterial);
        }

        private void BuildRoom(BasementRoom room)
        {
            float t = BasementLayout.WallThickness;
            float x0 = room.XMin - (room.IsFullyOpen(BasementSide.West) ? 0f : t), x1 = room.XMax + (room.IsFullyOpen(BasementSide.East) ? 0f : t);
            float z0 = room.ZMin - (room.IsFullyOpen(BasementSide.South) ? 0f : t), z1 = room.ZMax + (room.IsFullyOpen(BasementSide.North) ? 0f : t);
            var parent = new GameObject("Room " + room.Name).transform;
            parent.SetParent(extension.transform, false);
            Vector3 center = new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f);
            Box("Floor", parent, new Vector3(center.x, BasementLayout.FloorY - 0.15f, center.z), new Vector3(x1 - x0, 0.3f, z1 - z0), floorMaterial);
            Box("Ceiling", parent, new Vector3(center.x, BasementLayout.CeilingY + 0.15f, center.z), new Vector3(x1 - x0, 0.3f, z1 - z0), ceilingMaterial);
            foreach (BasementBox wall in BasementLayout.Walls(room))
                Box("Wall", parent, wall.Center, wall.Size, wallMaterial);
        }

        private void BuildProps()
        {
            var parent = new GameObject("Hall props").transform;
            parent.SetParent(extension.transform, false);
            foreach (BasementLayout.Prop prop in BasementLayout.HallProps)
            {
                GameObject obj = prop.Cylinder
                    ? Cylinder("Boiler", parent, prop.Center, new Vector3(prop.Size.x, prop.Size.y * 0.5f, prop.Size.z), metalMaterial, true)
                    : Box("Pillar or crate", parent, prop.Center, prop.Size, wallMaterial);
                if (!prop.Cylinder && prop.Size.y < 2f) obj.GetComponent<Renderer>().sharedMaterial = woodMaterial;
            }
        }

        // -------------------------------------------------------------------------------------------------
        // Хлам: ящики, бочки, доски, щебень, матрас, стеллажи, вёдра
        // -------------------------------------------------------------------------------------------------

        private void BuildJunk()
        {
            var parent = new GameObject("Junk").transform;
            parent.SetParent(extension.transform, false);
            int seed = 0;
            foreach (BasementLayout.Clutter item in BasementLayout.Junk)
            {
                var root = new GameObject("Junk " + item.Kind).transform;
                root.SetParent(parent, false);
                root.position = new Vector3(item.Position.x, BasementLayout.FloorY, item.Position.y);
                root.rotation = Quaternion.Euler(0f, item.Yaw, 0f);
                var random = new System.Random(seed++ * 7919 + 13);
                switch (item.Kind)
                {
                    case BasementLayout.ClutterKind.Crate: BuildCrate(root, item.Size); break;
                    case BasementLayout.ClutterKind.Barrel: BuildBarrel(root, item.Size); break;
                    case BasementLayout.ClutterKind.Planks: BuildPlanks(root, item.Size, random); break;
                    case BasementLayout.ClutterKind.Rubble: BuildRubble(root, item.Size, random); break;
                    case BasementLayout.ClutterKind.Mattress:
                    case BasementLayout.ClutterKind.Blanket: BuildMat(root, item.Size, item.Kind == BasementLayout.ClutterKind.Blanket); break;
                    case BasementLayout.ClutterKind.Shelf: BuildShelf(root, item.Size, random); break;
                    default: BuildBucket(root, item.Size); break;
                }
                if (item.Blocks)
                {
                    var blocker = root.gameObject.AddComponent<BoxCollider>();
                    blocker.center = new Vector3(0f, item.Size.y * 0.5f, 0f);
                    blocker.size = item.Size;
                }
            }
        }

        private void BuildCrate(Transform root, Vector3 size)
        {
            var box = Box("Crate", root, root.position + Vector3.up * size.y * 0.5f, size, woodMaterial, false);
            box.transform.rotation = root.rotation;
            // Рейки по рёбрам — чтобы не был просто кубом.
            var strap = Box("Strap", root, root.position + Vector3.up * size.y * 0.5f, new Vector3(size.x * 1.03f, size.y * 0.12f, size.z * 1.03f), metalMaterial, false);
            strap.transform.rotation = root.rotation;
        }

        private void BuildBarrel(Transform root, Vector3 size)
        {
            Cylinder("Barrel", root, root.position + Vector3.up * size.y * 0.5f, new Vector3(size.x, size.y * 0.5f, size.z), metalMaterial, false);
            foreach (float k in new[] { 0.18f, 0.82f })
                Cylinder("Hoop", root, root.position + Vector3.up * size.y * k, new Vector3(size.x * 1.06f, size.y * 0.025f, size.z * 1.06f), pipeMaterial, false);
        }

        private void BuildPlanks(Transform root, Vector3 size, System.Random random)
        {
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                float yaw = (float)(random.NextDouble() * 24 - 12);
                var plank = Box("Plank", root, root.position + Vector3.up * (0.05f + i * (size.y / count)), new Vector3(size.x, size.y / count * 0.9f, size.z * 0.55f), woodMaterial, false);
                plank.transform.rotation = root.rotation * Quaternion.Euler(0f, yaw, 0f);
                plank.transform.position += root.right * (float)(random.NextDouble() * 0.12 - 0.06);
            }
        }

        private void BuildRubble(Transform root, Vector3 size, System.Random random)
        {
            for (int i = 0; i < 6; i++)
            {
                float s = 0.18f + (float)random.NextDouble() * 0.25f;
                Vector3 offset = new Vector3((float)(random.NextDouble() - 0.5) * size.x * 0.8f, s * 0.35f * (i % 3 + 1) * 0.5f, (float)(random.NextDouble() - 0.5) * size.z * 0.8f);
                var chunk = Box("Chunk", root, root.position + root.rotation * offset, new Vector3(s, s * 0.7f, s * 0.9f), wallMaterial, false);
                chunk.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 40f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 40f);
            }
        }

        private void BuildMat(Transform root, Vector3 size, bool blanket)
        {
            var mat = Box(blanket ? "Blanket" : "Mattress", root, root.position + Vector3.up * size.y * 0.5f, size, rugMaterial, false);
            mat.transform.rotation = root.rotation;
        }

        private void BuildBucket(Transform root, Vector3 size)
        {
            Cylinder("Bucket", root, root.position + Vector3.up * size.y * 0.5f, new Vector3(size.x, size.y * 0.5f, size.z), metalMaterial, false);
        }

        private void BuildShelf(Transform root, Vector3 size, System.Random random)
        {
            // Длинная сторона — вдоль стены; доски и коробки на них.
            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z, depth = alongX ? size.z : size.x;
            Vector3 along = alongX ? Vector3.right : Vector3.forward, across = alongX ? Vector3.forward : Vector3.right;
            Vector3 baseCenter = root.position;
            foreach (float k in new[] { -0.5f, 0.5f })
                foreach (float d in new[] { -0.5f, 0.5f })
                    Box("Post", root, baseCenter + along * (length * 0.5f - 0.05f) * k * 2f + across * (depth * 0.5f - 0.04f) * d * 2f + Vector3.up * size.y * 0.5f,
                        new Vector3(0.07f, size.y, 0.07f), woodMaterial, false);
            for (int level = 0; level < 3; level++)
            {
                float y = 0.08f + level * (size.y - 0.15f) / 2f;
                Box("Board", root, baseCenter + Vector3.up * y, alongX ? new Vector3(length, 0.05f, depth) : new Vector3(depth, 0.05f, length), woodMaterial, false);
                int items = 3 + random.Next(0, 3);
                for (int i = 0; i < items; i++)
                {
                    float u = ((float)i / items - 0.5f) * length * 0.85f + (float)random.NextDouble() * 0.1f;
                    float h = 0.12f + (float)random.NextDouble() * 0.2f;
                    Material material = random.Next(0, 3) == 0 ? metalMaterial : woodMaterial;
                    Box("Item", root, baseCenter + along * u + Vector3.up * (y + 0.025f + h * 0.5f), new Vector3(0.18f, h, 0.2f), material, false);
                }
            }
        }

        // Клоны мебели существующей комнаты подвала: коробки и складной стул. Ставятся на пол по нижней границе.
        private void BuildClonedProps()
        {
            if (reference == null) return;
            var parent = new GameObject("Cloned props").transform;
            parent.SetParent(extension.transform, false);
            PlaceClone(parent, "StoredBox_0", -35.3f, 106.95f, 25f);
            PlaceClone(parent, "StoredBox_2", -32.65f, 108.6f, -15f);
            PlaceClone(parent, "FoldingChair", -34.0f, 112.4f, 200f);
        }

        private void PlaceClone(Transform parent, string templateName, float x, float z, float yaw)
        {
            Transform template = null;
            foreach (Transform t in reference.GetComponentsInChildren<Transform>(true))
                if (t.name == templateName) { template = t; break; }
            if (template == null) return;
            GameObject copy = UnityEngine.Object.Instantiate(template.gameObject, parent);
            copy.name = templateName + " (clone)";
            copy.SetActive(true);
            copy.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            copy.transform.position = new Vector3(x, template.position.y, z);
            var renderers = copy.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            copy.transform.position += Vector3.up * (BasementLayout.FloorY - bounds.min.y);
        }

        // Труба хозяина: от решётки камеры под потолком через блок, переход C, зал и проход A — вверх, в дом.
        private void BuildPipe()
        {
            var parent = new GameObject("Walter's pipe").transform;
            parent.SetParent(extension.transform, false);
            float y = BasementLayout.PipeY;
            Vector2[] route = BasementLayout.PipeRoute;
            Vector3 start = new Vector3(route[0].x, BasementLayout.FloorY, route[0].y);
            Vector3 top = new Vector3(route[0].x, y, route[0].y);
            PipeSegment(parent, start, top);
            Vector3 previous = top;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 next = new Vector3(route[i].x, y, route[i].y);
                PipeSegment(parent, previous, next);
                var elbow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                elbow.name = "Elbow";
                elbow.transform.SetParent(parent, false);
                elbow.transform.position = previous;
                elbow.transform.localScale = Vector3.one * 0.19f;
                elbow.GetComponent<Renderer>().sharedMaterial = pipeMaterial;
                UnityEngine.Object.DestroyImmediate(elbow.GetComponent<Collider>());
                previous = next;
            }
            // Конец уходит вверх через перекрытие — к стоякам первого этажа.
            PipeSegment(parent, previous, new Vector3(previous.x, BasementLayout.CeilingY + 0.5f, previous.z));
        }

        private void PipeSegment(Transform parent, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f) return;
            var pipe = Cylinder("Pipe", parent, (a + b) * 0.5f, new Vector3(0.14f, length * 0.5f, 0.14f), pipeMaterial, false);
            pipe.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        }

        // Камера: стены даёт комната G; здесь — выступ восточной стены внутрь блока D, решётка и замок.
        private void BuildCell()
        {
            float floor = BasementLayout.FloorY, height = BasementLayout.CeilingY - floor;
            Rect cell = BasementLayout.CellArea;
            float wallFrom = BasementLayout.CellBlockD.ZMin;
            Box("Cell east wall", extension.transform, new Vector3(cell.xMax + 0.15f, floor + height * 0.5f, (wallFrom + cell.yMax) * 0.5f),
                new Vector3(0.3f, height, cell.yMax - wallFrom), wallMaterial);

            var door = new GameObject("Cell door");
            door.transform.SetParent(extension.transform, false);
            door.transform.position = new Vector3(cell.xMax, floor, cell.yMax);
            cellDoor = door.transform;
            float width = cell.width;
            for (int i = 0; i < 17; i++)
            {
                float x = -0.08f - i * ((width - 0.16f) / 16f);
                var bar = Box("Bar", door.transform, Vector3.zero, new Vector3(0.05f, 2.3f, 0.05f), metalMaterial, false);
                bar.transform.localPosition = new Vector3(x, 1.15f, 0f);
            }
            foreach (float y in new[] { 0.15f, 1.15f, 2.25f })
            {
                var rail = Box("Rail", door.transform, Vector3.zero, new Vector3(width, 0.06f, 0.06f), metalMaterial, false);
                rail.transform.localPosition = new Vector3(-width * 0.5f, y, 0f);
            }
            cellDoorCollider = Box("Cell door collider", door.transform, Vector3.zero, new Vector3(width, 2.4f, 0.12f), null);
            cellDoorCollider.transform.localPosition = new Vector3(-width * 0.5f, 1.2f, 0f);
            UnityEngine.Object.DestroyImmediate(cellDoorCollider.GetComponent<Renderer>());
            UnityEngine.Object.DestroyImmediate(cellDoorCollider.GetComponent<MeshFilter>());

            lockVisual = Box("Cell lock", door.transform, Vector3.zero, new Vector3(0.16f, 0.22f, 0.1f), metalMaterial);
            lockVisual.transform.position = BasementLayout.CellLock;
            UnityEngine.Object.DestroyImmediate(lockVisual.GetComponent<Collider>());
            var lockTrigger = new GameObject("Cell lock interaction", typeof(BoxCollider), typeof(BasementCellLock));
            lockTrigger.transform.SetParent(lockVisual.transform, false);
            lockTrigger.transform.position = BasementLayout.CellLock + new Vector3(0f, 0f, 0.1f);
            lockTrigger.layer = InteractableLayer;
            lockTrigger.GetComponent<BoxCollider>().size = new Vector3(0.9f, 0.9f, 0.5f);
            cellLock = lockTrigger.GetComponent<BasementCellLock>();

        }

        private void BuildLights()
        {
            // Единственный свет — тусклая красная лампа у камеры: ориентир в конце пути. Остальное — фонарь игрока.
            // Выключена: без теней свет прошёл бы сквозь перекрытие в дом, её включает BasementChapter на спуске.
            var lamp = new GameObject("Cell lamp").AddComponent<Light>();
            lamp.transform.SetParent(extension.transform, false);
            lamp.transform.position = BasementLayout.CellLampPosition;
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.25f, 0.15f);
            lamp.intensity = 0.8f;
            lamp.range = 5.5f;
            lamp.shadows = LightShadows.None;
            lamp.enabled = false;
            cellLamp = lamp;
        }

}
