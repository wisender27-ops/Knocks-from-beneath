using System;
using UnityEditor;
using UnityEngine;
using KnocksFromBeneath;

// Собирает префаб Resources/Basement/BasementDepths по DepthsLayout: вентрешётка и замурованный проём в подвале, квадратная труба, тоннели.
// Сцену не меняет: временный объект живёт только на время сохранения и удаляется в finally.
public sealed class BasementDepthsBuilder
{
    private const string PrefabPath = "Assets/Resources/Basement/BasementDepths.prefab";
    private const string TempRootName = "__KFB_BasementDepthsBuild";
    private const int InteractableLayer = 6;

    private Transform root, tunnels, duct;
    private Material wall, floor, ceiling, metal, sheet, rust, pipe, mortar, wood, bulb;

    [MenuItem("Tools/Basement/Build Depths Prefab")]
    public static void Build()
    {
        var builder = new BasementDepthsBuilder();
        var rootObject = new GameObject(TempRootName);
        try
        {
            builder.root = rootObject.transform;
            builder.Assemble(rootObject);
            PrefabUtility.SaveAsPrefabAsset(rootObject, PrefabPath, out bool success);
            if (!success) throw new InvalidOperationException("Префаб не сохранён: " + PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    private void Assemble(GameObject rootObject)
    {
        LoadMaterials();
        var depths = rootObject.AddComponent<BasementDepths>();
        BuildVent(depths);
        BuildSealedDoor(depths);
        BuildTapeCameraPoint(depths);
        BuildDuct(depths);

        tunnels = new GameObject("Tunnels").transform;
        tunnels.SetParent(root, false);
        depths.tunnels = tunnels.gameObject;
        foreach (BasementRoom room in DepthsLayout.Rooms) BuildRoom(room);
        BuildProps();
        BuildCollectorDetails();
        BuildPipeRun();
        BuildLadders(depths);
        BuildLights();
        BuildWingGrate();

        var ambient = rootObject.AddComponent<UndergroundAmbientOverride>();
        var serialized = new SerializedObject(ambient);
        SerializedProperty list = serialized.FindProperty("renderers");
        Renderer[] renderers = rootObject.GetComponentsInChildren<Renderer>(true);
        list.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void LoadMaterials()
    {
        wall = Material("AgedConcrete"); floor = Material("ConcreteFloor"); ceiling = Material("AgedConcrete");
        metal = Material("DarkMetal"); sheet = Material("IvoryMetal"); rust = Material("RustSteel"); pipe = Material("AgedPipe");
        mortar = Material("ConcreteMortar"); wood = Material("OldTimber"); bulb = Material("Bulb");
    }

    private static Material Material(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Basement/Materials/" + name + ".mat");
        if (material == null) throw new InvalidOperationException("Материал не найден: " + name);
        return material;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 center, Vector3 scale, Material material, bool collider)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = center;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true) =>
        Primitive(PrimitiveType.Cube, name, parent, center, size, material, collider);

    private static GameObject Cylinder(string name, Transform parent, Vector3 center, float diameter, float height, Material material, bool collider = false) =>
        Primitive(PrimitiveType.Cylinder, name, parent, center, new Vector3(diameter, height * 0.5f, diameter), material, collider);

    // Точка взаимодействия: невидимый коллайдер на слое Interactable.
    private static BasementCellLock Point(string name, Transform parent, Vector3 center, Vector3 size)
    {
        var obj = new GameObject(name, typeof(BoxCollider), typeof(BasementCellLock));
        obj.layer = InteractableLayer;
        obj.transform.SetParent(parent, false);
        obj.transform.position = center;
        obj.GetComponent<BoxCollider>().size = size;
        return obj.GetComponent<BasementCellLock>();
    }

    // Квадратный проём в стене: чёрная глубина, рама и решётка на петле. Стена сцены цела, в трубу уходят под затемнением.
    private void BuildVent(BasementDepths depths)
    {
        var parent = new GameObject("Vent").transform;
        parent.SetParent(root, false);
        float size = DepthsLayout.DuctSize, half = size * 0.5f;
        Vector3 center = DepthsLayout.Vent + Vector3.up * (half + 0.08f);
        Box("Dark opening", parent, center, new Vector3(0.01f, size, size), metal, false);
        foreach (float side in new[] { -1f, 1f })
        {
            Box("Frame side", parent, center + new Vector3(-0.04f, 0f, side * (half + 0.04f)), new Vector3(0.08f, size + 0.16f, 0.08f), rust, false);
            Box("Frame edge", parent, center + new Vector3(-0.04f, side * (half + 0.04f), 0f), new Vector3(0.08f, 0.08f, size), rust, false);
        }

        // Решётка висит на вертикальной петле у ближнего к проёму края.
        var pivot = new GameObject("Grille pivot").transform;
        pivot.SetParent(parent, false);
        pivot.position = center + new Vector3(-0.06f, 0f, -half);
        depths.grillePivot = pivot;
        Vector3 grille = center + Vector3.left * 0.06f;
        for (int i = 0; i < 9; i++)
        {
            float y = -half + 0.06f + i * (size - 0.12f) / 8f;
            Box("Slat", pivot, grille + Vector3.up * y, new Vector3(0.05f, 0.05f, size - 0.04f), rust, false);
        }
        foreach (float side in new[] { -1f, 1f })
            Box("Stile", pivot, grille + Vector3.forward * side * (half - 0.03f), new Vector3(0.06f, size, 0.06f), rust, false);

        depths.vent = Point("Vent interaction", parent, center + Vector3.left * 0.15f, new Vector3(0.1f, size, size));
    }

    private void BuildSealedDoor(BasementDepths depths)
    {
        var parent = new GameObject("Sealed doorway").transform;
        parent.SetParent(root, false);
        Box("Brickwork", parent, DepthsLayout.SealedDoor, DepthsLayout.SealedDoorSize, mortar);
        depths.sealedWall = Point("Sealed wall interaction", parent, DepthsLayout.SealedDoor + Vector3.left * 0.16f,
            new Vector3(0.05f, DepthsLayout.SealedDoorSize.y, DepthsLayout.SealedDoorSize.z));
    }

    private void BuildTapeCameraPoint(BasementDepths depths)
    {
        var view = new GameObject("Tape view").transform;
        view.SetParent(root, false);
        view.SetPositionAndRotation(DepthsLayout.TapeCameraPosition,
            Quaternion.LookRotation(DepthsLayout.TapeCameraLook - DepthsLayout.TapeCameraPosition));
        depths.tapeView = view;
        depths.tapeCamera = Point("Tape camera interaction", root, DepthsLayout.TapeCameraPosition, new Vector3(0.5f, 0.7f, 0.5f));
    }

    // Труба из листа: стены по углам обрезаны так, чтобы поворот оставался открытым, рёбра жёсткости через метр.
    private void BuildDuct(BasementDepths depths)
    {
        duct = new GameObject("Duct").transform;
        duct.SetParent(root, false);
        depths.duct = duct.gameObject;
        Vector2[] path = DepthsLayout.DuctPath;
        float h = DepthsLayout.DuctSize * 0.5f, t = 0.08f, y0 = DepthsLayout.DuctFloorY, size = DepthsLayout.DuctSize;
        for (int i = 1; i < path.Length; i++)
        {
            Vector2 a = path[i - 1], delta = path[i] - a, dir = delta.normalized, normal = new Vector2(-dir.y, dir.x);
            float length = delta.magnitude;
            Vector3 mid = Flat(a + dir * length * 0.5f, 0f);
            Vector3 span = Spread(dir, length + size + t * 2f, normal, size + t * 2f, t);
            Box("Duct floor", duct, mid + Vector3.up * (y0 - t * 0.5f), span, sheet);
            Box("Duct top", duct, mid + Vector3.up * (y0 + size + t * 0.5f), span, sheet);
            foreach (float side in new[] { -1f, 1f })
            {
                float from = -h, to = length + h;
                if (i > 1 && Vector2.Dot(-(path[i - 1] - path[i - 2]).normalized, normal * side) > 0.5f) from = h;
                if (i < path.Length - 1 && Vector2.Dot((path[i + 1] - path[i]).normalized, normal * side) > 0.5f) to = length - h;
                Vector2 wall = a + dir * (from + to) * 0.5f + normal * side * (h + t * 0.5f);
                Box("Duct wall", duct, Flat(wall, y0 + h), Spread(dir, to - from, normal, t, size), sheet);
            }
            for (float d = 1f; d < length; d += 1f)
            {
                Vector2 at = a + dir * d;
                foreach (float side in new[] { -1f, 1f })
                    Box("Rib", duct, Flat(at + normal * side * (h - 0.01f), y0 + h), Spread(dir, 0.05f, normal, 0.02f, size), rust, false);
                Box("Rib", duct, Flat(at, y0 + size - 0.01f), Spread(dir, 0.05f, normal, size, 0.02f), rust, false);
            }
        }
        Vector2 first = (path[1] - path[0]).normalized, last = (path[3] - path[2]).normalized;
        Box("Start cap", duct, Flat(path[0] - first * (h + t * 0.5f), y0 + h), Spread(first, t, new Vector2(-first.y, first.x), size + t * 2f, size), rust);
        Box("End cap", duct, Flat(path[3] + last * (h + t * 0.5f), y0 + h), Spread(last, t, new Vector2(-last.y, last.x), size + t * 2f, size), rust);

        // В конце трубы — решётка в полу над шахтой тоннелей.
        Vector3 end = DepthsLayout.DuctEnd;
        for (int i = -3; i <= 3; i++)
            Box("Floor grate bar", duct, end + new Vector3(i * 0.13f, 0.015f, 0f), new Vector3(0.05f, 0.03f, size - 0.1f), rust, false);
        // Точка — поперёк конца трубы: видна с четверенек прямо перед собой и не мешает доползти.
        depths.ductExit = Point("Duct exit interaction", duct, end + Vector3.up * h + Vector3.back * 0.45f, new Vector3(size - 0.1f, size - 0.1f, 0.1f));
        // Свет из щелей в стыках: вперёд видно на пару секций, остальное — фонарь.
        foreach (Vector2 slit in new[] { path[0] + Vector2.up * 2.5f, path[1], path[1] + Vector2.left * 5f, path[2] })
            Lamp(duct, Flat(slit, 0f), new Color(0.8f, 0.85f, 0.75f), 1.6f, 5f, y0 + size - 0.12f);
    }

    private static Vector3 Flat(Vector2 point, float y) => new Vector3(point.x, y, point.y);

    private static Vector3 Spread(Vector2 dir, float along, Vector2 normal, float across, float height) =>
        new Vector3(Mathf.Abs(dir.x) * along + Mathf.Abs(normal.x) * across, height, Mathf.Abs(dir.y) * along + Mathf.Abs(normal.y) * across);

    private void BuildRoom(BasementRoom room)
    {
        float t = BasementLayout.WallThickness, drop = DepthsLayout.Drop;
        float x0 = room.XMin - (room.IsFullyOpen(BasementSide.West) ? 0f : t), x1 = room.XMax + (room.IsFullyOpen(BasementSide.East) ? 0f : t);
        float z0 = room.ZMin - (room.IsFullyOpen(BasementSide.South) ? 0f : t), z1 = room.ZMax + (room.IsFullyOpen(BasementSide.North) ? 0f : t);
        var parent = new GameObject("Room " + room.Name).transform;
        parent.SetParent(tunnels, false);
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        Box("Floor", parent, new Vector3(cx, DepthsLayout.FloorY - 0.15f, cz), new Vector3(x1 - x0, 0.3f, z1 - z0), floor);
        Box("Ceiling", parent, new Vector3(cx, DepthsLayout.CeilingY + 0.15f, cz), new Vector3(x1 - x0, 0.3f, z1 - z0), ceiling);
        foreach (BasementBox segment in BasementLayout.Walls(room))
            Box("Wall", parent, segment.Center + Vector3.up * drop, segment.Size, wall);
    }

    private void BuildProps()
    {
        var parent = new GameObject("Machinery").transform;
        parent.SetParent(tunnels, false);
        foreach (BasementLayout.Prop prop in DepthsLayout.Props())
        {
            if (prop.Cylinder)
            {
                Cylinder("Tank", parent, prop.Center, prop.Size.x, prop.Size.y, rust, true);
                foreach (float k in new[] { -0.35f, 0.35f })
                    Cylinder("Hoop", parent, prop.Center + Vector3.up * prop.Size.y * k, prop.Size.x * 1.05f, 0.06f, metal);
            }
            else Box("Cabinet", parent, prop.Center, prop.Size, metal);
        }
    }

    // Коллектор: канал с тёмной водой посередине, рёбра жёсткости, настил поверх канала.
    private void BuildCollectorDetails()
    {
        var parent = new GameObject("Collector details").transform;
        parent.SetParent(tunnels, false);
        BasementRoom c = DepthsLayout.Collector;
        float zMid = 0.5f * (c.ZMin + c.ZMax), length = c.XMax - c.XMin;
        Box("Water channel", parent, new Vector3(c.XMin + length * 0.5f, DepthsLayout.FloorY + 0.012f, zMid + 0.85f),
            new Vector3(length, 0.02f, 0.8f), metal, false);
        for (float x = c.XMin + 2f; x < c.XMax; x += 4f)
        {
            Box("Rib north", parent, new Vector3(x, DepthsLayout.FloorY + 1.42f, c.ZMax - 0.1f), new Vector3(0.35f, 2.85f, 0.2f), wall);
            Box("Rib south", parent, new Vector3(x, DepthsLayout.FloorY + 1.42f, c.ZMin + 0.1f), new Vector3(0.35f, 2.85f, 0.2f), wall);
            Box("Arch", parent, new Vector3(x, DepthsLayout.CeilingY - 0.12f, zMid), new Vector3(0.35f, 0.25f, c.ZMax - c.ZMin), wall, false);
        }
        BasementRoom g = DepthsLayout.Gallery;
        for (float x = g.XMin + 2f; x < g.XMax; x += 5f)
            Box("Gallery arch", parent, new Vector3(x, DepthsLayout.CeilingY - 0.12f, 0.5f * (g.ZMin + g.ZMax)), new Vector3(0.3f, 0.25f, g.ZMax - g.ZMin), wall, false);
        Box("Old crate", parent, new Vector3(-19.2f, DepthsLayout.FloorY + 0.4f, 122f), new Vector3(0.9f, 0.8f, 0.9f), wood);
        Box("Old planks", parent, new Vector3(-22.9f, DepthsLayout.FloorY + 0.1f, 122f), new Vector3(1.6f, 0.2f, 0.5f), wood);
    }

    // Труба вдоль всего пути: та же, что наверху, по ней и идёт стук.
    private void BuildPipeRun()
    {
        var parent = new GameObject("Pipe run").transform;
        parent.SetParent(tunnels, false);
        float y = DepthsLayout.CeilingY - 0.45f;
        Vector2[] route =
        {
            new Vector2(-39f, 114f), new Vector2(-39f, 118.25f), new Vector2(-8f, 118.25f), new Vector2(-8f, 104.3f),
            new Vector2(-37.7f, 104.3f), new Vector2(-37.7f, 116.3f)
        };
        for (int i = 1; i < route.Length; i++)
        {
            Vector3 a = new Vector3(route[i - 1].x, y, route[i - 1].y), b = new Vector3(route[i].x, y, route[i].y);
            var segment = Cylinder("Pipe", parent, (a + b) * 0.5f, 0.16f, Vector3.Distance(a, b), pipe);
            segment.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            Primitive(PrimitiveType.Sphere, "Elbow", parent, b, Vector3.one * 0.22f, pipe, false);
        }
        Vector3 top = new Vector3(route[5].x, y, route[5].y);
        Cylinder("Riser up", parent, top + Vector3.up * 2f, 0.16f, 4f, pipe);
    }

    private void BuildLadders(BasementDepths depths)
    {
        var parent = new GameObject("Ladders").transform;
        parent.SetParent(tunnels, false);
        BuildLadder(parent, new Vector3(-40.2f, DepthsLayout.FloorY, 120.2f), Vector3.forward, DepthsLayout.CeilingY);
        Vector3 up = DepthsLayout.LadderUp;
        BuildLadder(parent, up, Vector3.forward, DepthsLayout.CeilingY);
        depths.ladder = Point("Ladder interaction", tunnels, up + Vector3.up * 1.2f, new Vector3(0.9f, 2.2f, 0.5f));
    }

    private static void BuildLadder(Transform parent, Vector3 foot, Vector3 wallward, float top)
    {
        Vector3 side = Vector3.Cross(Vector3.up, wallward).normalized * 0.22f;
        float height = top - foot.y;
        Vector3 mid = foot + Vector3.up * height * 0.5f;
        Box("Rail", parent, mid + side, new Vector3(0.05f, height, 0.05f), Material("RustSteel"), false);
        Box("Rail", parent, mid - side, new Vector3(0.05f, height, 0.05f), Material("RustSteel"), false);
        for (float y = 0.3f; y < height; y += 0.3f)
            Box("Rung", parent, foot + Vector3.up * y, new Vector3(Mathf.Abs(side.x) * 2f + 0.05f, 0.03f, Mathf.Abs(side.z) * 2f + 0.05f), Material("RustSteel"), false);
    }

    // Только дежурные лампы: по залу на помещение, основной свет — фонарь игрока.
    private void BuildLights()
    {
        var parent = new GameObject("Service lamps").transform;
        parent.SetParent(tunnels, false);
        Lamp(parent, new Vector3(-30f, 0f, 118.3f), new Color(0.75f, 0.85f, 0.7f), 0.9f, 6f);
        Lamp(parent, new Vector3(-14f, 0f, 120.7f), new Color(0.75f, 0.85f, 0.7f), 0.7f, 6f);
        Lamp(parent, new Vector3(-4f, 0f, 113f), new Color(1f, 0.7f, 0.4f), 1.4f, 9f);
        Lamp(parent, new Vector3(-20f, 0f, 104.4f), new Color(0.75f, 0.85f, 0.7f), 0.7f, 6f);
        Lamp(parent, new Vector3(-34.5f, 0f, 104f), new Color(1f, 0.25f, 0.15f), 1.2f, 7f);
    }

    private void Lamp(Transform parent, Vector3 at, Color color, float intensity, float range, float y = DepthsLayout.CeilingY - 0.35f)
    {
        Vector3 position = new Vector3(at.x, y, at.z);
        var shade = Primitive(PrimitiveType.Sphere, "Lamp", parent, position, Vector3.one * 0.14f, bulb, false);
        var lamp = shade.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.color = color;
        lamp.intensity = intensity;
        lamp.range = range;
        lamp.shadows = LightShadows.None;
    }

    // Решётка в полу прохода A: сюда выводит лестница. Видна уже с той стороны, в отрезанной половине.
    private void BuildWingGrate()
    {
        var parent = new GameObject("Wing grate").transform;
        parent.SetParent(root, false);
        Vector3 at = new Vector3(DepthsLayout.WingStart.x, BasementLayout.FloorY, DepthsLayout.WingStart.z);
        Box("Frame", parent, at + Vector3.up * 0.006f, new Vector3(0.72f, 0.012f, 0.8f), metal, false);
        for (int i = -2; i <= 2; i++)
            Box("Bar", parent, at + new Vector3(i * 0.12f, 0.018f, 0f), new Vector3(0.04f, 0.012f, 0.78f), rust, false);
    }
}
