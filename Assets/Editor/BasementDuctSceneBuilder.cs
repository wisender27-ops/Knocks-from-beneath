using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using KnocksFromBeneath;

// Собирает вентиляционную трубу прямо в открытой сцене: объект "Basement Duct" (по DepthsLayout.DuctPath). Вместо кубиков: настоящие листы жести с вмятинами и швами-фланцами,
// ржавые/сырые текстуры ambientCG (CC0), прозрачные потёки/плесень/лужи/паутина, оборванные листы, кабели, доски, капель и звуки (BigSoundBank, CC0).
// Идемпотентно: прежний объект "Basement Duct" пересоздаётся. Сцену не сохраняет — только помечает изменённой.
public static class BasementDuctSceneBuilder
{
    private const string Dir = "Assets/Basement/Duct/";
    private const string RootName = "Basement Duct";
    private const int InteractableLayer = 6;
    private const float S = DepthsLayout.DuctSize, W = DepthsLayout.DuctWidth, H = W * 0.5f, HY = S * 0.5f, T = 0.05f, Period = 1.2f;

    private const float Y0 = DepthsLayout.DuctFloorY;
    private const float WallX = -37.83f; // грань стены, в которую уходит короб
    private const float PanelTile = 1.725f, SteelTile = 0.6f; // метров на текстуру панели и стали
    private const float StrapWidth = 0.1f, StrapThick = 0.014f;

    private struct Seg
    {
        public Vector3 start, d, n, prevD, nextD;
        public float len, a0;
        public bool first, last;
    }

    // Накопитель геометрии: все грани смотрят в заданную сторону (внутрь трубы), UV в метрах мира.
    private sealed class MB
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> I = new List<int>();

        public void Panel(Vector3 o, Vector3 u, float ul, Vector3 w, float wl, Vector3 facing,
            float step = 100f, float amp = 0f, float crease = 0f, float cellPhase = 0f, float seed = 0f)
        {
            int nu = Mathf.Max(1, Mathf.CeilToInt(ul / step)), nw = Mathf.Max(1, Mathf.CeilToInt(wl / step));
            int b = V.Count;
            bool flip = Vector3.Dot(Vector3.Cross(u, w), facing) < 0f;
            for (int j = 0; j <= nw; j++)
                for (int i = 0; i <= nu; i++)
                {
                    float su = ul * i / nu, sw = wl * j / nw;
                    Vector3 p = o + u * su + w * sw;
                    float edge = Mathf.Clamp01(Mathf.Min(Mathf.Min(su, ul - su), Mathf.Min(sw, wl - sw)) / 0.1f);
                    edge = edge * edge * (3f - 2f * edge);
                    float d = 0f;
                    if (amp > 0f) d += (Mathf.PerlinNoise(p.x * 1.9f + p.y * 1.3f + seed, p.z * 1.9f + p.y * 0.7f + seed) - 0.5f) * 2f * amp;
                    if (crease > 0f)
                    {
                        float a = Mathf.Repeat((su + cellPhase) / Period, 1f), c = Mathf.Clamp01(sw / wl);
                        float diag = Mathf.Min(Mathf.Abs(a - c), Mathf.Abs(a + c - 1f));
                        d -= crease * Mathf.Clamp01(1f - diag * 12f);
                    }
                    V.Add(p + facing * (d * edge));
                    UV.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w)));
                }
            int stride = nu + 1;
            for (int j = 0; j < nw; j++)
                for (int i = 0; i < nu; i++)
                {
                    int p00 = b + j * stride + i, p10 = p00 + 1, p01 = p00 + stride, p11 = p01 + 1;
                    if (!flip) { I.Add(p00); I.Add(p10); I.Add(p01); I.Add(p10); I.Add(p11); I.Add(p01); }
                    else { I.Add(p00); I.Add(p01); I.Add(p10); I.Add(p10); I.Add(p01); I.Add(p11); }
                }
        }

        // Произвольный четырёхугольник a-b-c-(b+c-a) с заданными UV по углам и стороной лица.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Vector2 uva, Vector2 uvb, Vector2 uvc)
        {
            int s = V.Count;
            Vector3 d = b + c - a;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            UV.Add(uva); UV.Add(uvb); UV.Add(uvc); UV.Add(uvb + uvc - uva);
            bool ok = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) >= 0f;
            if (ok) { I.Add(s); I.Add(s + 1); I.Add(s + 2); I.Add(s + 1); I.Add(s + 3); I.Add(s + 2); }
            else { I.Add(s); I.Add(s + 2); I.Add(s + 1); I.Add(s + 1); I.Add(s + 2); I.Add(s + 3); }
        }

        public void Box(Vector3 c, Vector3 r, Vector3 up, Vector3 f, Vector3 size)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            Panel(c + r * hx - f * hz - up * hy, f, size.z, up, size.y, r);
            Panel(c - r * hx - f * hz - up * hy, f, size.z, up, size.y, -r);
            Panel(c + up * hy - r * hx - f * hz, r, size.x, f, size.z, up);
            Panel(c - up * hy - r * hx - f * hz, r, size.x, f, size.z, -up);
            Panel(c + f * hz - r * hx - up * hy, r, size.x, up, size.y, f);
            Panel(c - f * hz - r * hx - up * hy, r, size.x, up, size.y, -f);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = V.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(V);
            m.SetUVs(0, UV);
            m.SetTriangles(I, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    [MenuItem("Tools/Basement/Build Duct In Scene")]
    public static void Build()
    {
        Scene();
    }

    private static void Scene()
    {
        foreach (string folder in new[] { "Textures", "Materials", "Meshes" })
            if (!AssetDatabase.IsValidFolder(Dir + folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), folder);

        BuildTextures();
        Materials m = BuildMaterials();
        AudioClip drips = Clip("DuctDrips.ogg", true), hum = Clip("DuctAirHum.ogg", false), grind = Clip("DuctMetalGrind.ogg", true);

        GameObject old = GameObject.Find(RootName);
        if (old != null && old.transform.parent == null) UnityEngine.Object.DestroyImmediate(old);

        var root = new GameObject(RootName);
        var duct = new GameObject("Duct");
        duct.transform.SetParent(root.transform, false);
        var visual = new GameObject("Visual").transform;
        visual.SetParent(duct.transform, false);

        List<Seg> segs = Segments();
        var rng = new System.Random(41);
        MB shell = new MB(), floor = new MB(), flange = new MB(), wood = new MB(), sheet = new MB(), cable = new MB();
        MB grime = new MB(), puddle = new MB(), web = new MB(), panels = new MB(), steel = new MB();
        var dripPoints = new List<Vector3>();
        var creakPoints = new List<Vector3>();

        for (int i = 0; i < segs.Count; i++)
        {
            Seg s = segs[i];
            BuildShell(s, shell, floor);
            BuildFlanges(s, flange);
            BuildDecals(s, i, rng, grime, puddle, dripPoints);
            BuildDebris(s, i, rng, wood, sheet, web);
            if (i == 0) BuildCables(s, cable);
            creakPoints.Add(P(s, s.len * 0.5f, 0f, S - 0.1f));
        }
        BuildOutlet(segs[segs.Count - 1], steel);
        BuildExterior(segs[0], panels, steel);
        foreach (Vector3 point in dripPoints) puddle.Quad(point + new Vector3(-0.3f, 0.016f, -0.3f), point + new Vector3(0.3f, 0.016f, -0.3f), point + new Vector3(-0.3f, 0.016f, 0.3f),
            Vector3.up, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));

        var renderers = new List<Renderer>();
        renderers.Add(AddMesh(visual, "Walls and ceiling", shell, m.wall, renderers));
        renderers.Add(AddMesh(visual, "Floor", floor, m.floor, renderers));
        renderers.Add(AddMesh(visual, "Flanges and grate", flange, m.flange, renderers));
        renderers.Add(AddMesh(visual, "Loose boards", wood, m.wood, renderers));
        
        renderers.Add(AddMesh(visual, "Outer panels", panels, m.panel, renderers));
        renderers.Add(AddMesh(visual, "Steel fittings", steel, m.steel, renderers));
        renderers.Add(AddMesh(visual, "Cables", cable, m.cable, renderers));
        renderers.Add(AddMesh(visual, "Grime decals", grime, m.grime, renderers, noShadow: true));
        renderers.Add(AddMesh(visual, "Puddles", puddle, m.puddle, renderers, noShadow: true));
        renderers.Add(AddMesh(visual, "Cobwebs", web, m.web, renderers, noShadow: true));
        renderers.RemoveAll(r => r == null);

        BuildColliders(segs, duct.transform);
        BasementDepths depthsObject = UnityEngine.Object.FindFirstObjectByType<BasementDepths>(FindObjectsInactive.Include);
        if (depthsObject == null) throw new InvalidOperationException("В сцене нет BasementDepths.");
        RebuildWalls(depthsObject, duct.transform);
        renderers.AddRange(MoveVentParts(depthsObject, m));
        renderers.AddRange(BuildCrates(duct.transform));

        var audio = new GameObject("Audio").transform;
        audio.SetParent(duct.transform, false);
        var loops = new List<AudioSource>();
        var mixer = AudioRouting.Group(AudioBus.World);
        for (int i = 0; i < dripPoints.Count; i++)
        {
            Vector3 at = dripPoints[i] + Vector3.up * (S - 0.05f);
            loops.Add(Source(audio, "Drip " + (i + 1), at, drips, mixer, 0.5f, 1f, 1.5f, 8f, 0.92f + i * 0.1f));
            BuildDrip(duct.transform, "Drip particles " + (i + 1), at, m.drip);
        }
        Vector3 mid = P(segs[0], segs[0].len * 0.5f, 0f, HY);
        loops.Add(Source(audio, "Air hum", mid, hum, mixer, 0.22f, 0.35f, 4f, 16f, 1f));
        AudioSource creak = Source(audio, "Metal creak", mid, null, mixer, 1f, 1f, 1.5f, 11f, 1f);
        creak.loop = false;

        var scene = root.AddComponent<BasementDuctScene>();
        BasementDepths depths = depthsObject;
        depths.duct = scene;
        EditorUtility.SetDirty(depths);
        var so = new SerializedObject(scene);
        SetArray(so.FindProperty("loops"), loops.ToArray());
        so.FindProperty("creak").objectReferenceValue = creak;
        SetArray(so.FindProperty("creakClips"), new[] { grind });
        SerializedProperty points = so.FindProperty("creakPoints");
        points.arraySize = creakPoints.Count;
        for (int i = 0; i < creakPoints.Count; i++) points.GetArrayElementAtIndex(i).vector3Value = creakPoints[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        var ambient = root.AddComponent<UndergroundAmbientOverride>();
        var aso = new SerializedObject(ambient);
        var all = new List<Renderer>(duct.GetComponentsInChildren<Renderer>(true));
        all.AddRange(depths.grillePivot.GetComponentsInChildren<Renderer>(false));
        SetArray(aso.FindProperty("renderers"), all.ToArray());
        aso.ApplyModifiedPropertiesWithoutUndo();
        ambient.Apply();

        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Труба] Собрана в сцене: " + RootName + ". Сцену не сохраняли.");
    }

    // ───────────── геометрия ─────────────

    private static List<Seg> Segments()
    {
        Vector2[] path = DepthsLayout.DuctPath;
        var list = new List<Seg>();
        for (int i = 1; i < path.Length; i++)
        {
            Vector2 a = path[i - 1], b = path[i], dir = (b - a).normalized;
            var s = new Seg
            {
                start = new Vector3(a.x, Y0, a.y), d = new Vector3(dir.x, 0f, dir.y), len = (b - a).magnitude,
                first = i == 1, last = i == path.Length - 1
            };
            s.n = new Vector3(-s.d.z, 0f, s.d.x);
            s.a0 = s.first ? -H : H;
            if (!s.first) { Vector2 pd = (a - path[i - 2]).normalized; s.prevD = new Vector3(pd.x, 0f, pd.y); }
            if (!s.last) { Vector2 nd = (path[i + 1] - b).normalized; s.nextD = new Vector3(nd.x, 0f, nd.y); }
            list.Add(s);
        }
        return list;
    }

    private static Vector3 P(Seg s, float a, float across, float y) => s.start + s.d * a + s.n * across + Vector3.up * y;

    // Диапазон стены по оси трубы: у внутренней стороны колена стена обрывается (там уходит следующая секция).
    private static void WallRange(Seg s, float side, out float from, out float to)
    {
        from = -H;
        to = s.len + H;
        if (!s.first && Vector3.Dot(-s.prevD, s.n * side) > 0.5f) from = H;
        if (!s.last && Vector3.Dot(s.nextD, s.n * side) > 0.5f) to = s.len - H;
    }

    private static void BuildShell(Seg s, MB shell, MB floor)
    {
        float a0 = s.a0, a1 = s.len + H;
        floor.Panel(P(s, a0, -H, 0f), s.d, a1 - a0, s.n, W, Vector3.up, 0.12f, 0.006f, 0.005f, 0f, s.len);
        shell.Panel(P(s, a0, -H, S), s.d, a1 - a0, s.n, W, Vector3.down, 0.12f, 0.008f, 0.007f, 0f, s.len + 3f);
        foreach (float side in new[] { -1f, 1f })
        {
            WallRange(s, side, out float from, out float to);
            shell.Panel(P(s, from, side * H, 0f), s.d, to - from, Vector3.up, S, -s.n * side, 0.12f, 0.009f, 0.008f, from - a0, s.len + side * 5f);
        }
    }

    // Снаружи: листы короба с заклёпками по стыкам, хомуты, кронштейны и фланцы. Видимая часть — от решётки до стены.
    private static void BuildExterior(Seg s, MB panels, MB steel)
    {
        float from = s.a0, to = WallX - s.start.x, half = H + T, top = S + T;
        Vector2 Uv(float along, float height) => new Vector2(along / PanelTile, height / (PanelTile * 2f / 3f) + 0.02f);
        foreach (float side in new[] { -1f, 1f })
            panels.Quad(P(s, from, side * half, -T), P(s, to, side * half, -T), P(s, from, side * half, top), s.n * side, Uv(from, 0f), Uv(to, 0f), Uv(from, S + 2f * T));
        panels.Quad(P(s, from, -half, -T), P(s, to, -half, -T), P(s, from, half, -T), Vector3.down, Uv(from, 0f), Uv(to, 0f), Uv(from, S));
        panels.Quad(P(s, from, -half, top), P(s, to, -half, top), P(s, from, half, top), Vector3.up, Uv(from, 0f), Uv(to, 0f), Uv(from, S));

        float front = half + 0.006f;
        for (float a = from + 0.06f; a < to; a += PanelTile * 0.5f)
            foreach (float y in new[] { -T + 0.05f, top - 0.05f })
            {
                Rivet(steel, P(s, a, -front, y), -s.n, 0.016f);
                Rivet(steel, P(s, a + 0.08f, -front, y), -s.n, 0.016f);
            }

        // Торцевая рама у решётки и фланец у стены: ближняя к полу комнаты сторона видна целиком.
        Ring(steel, P(s, from + 0.015f, 0f, HY), s.d, s.n, half + 0.05f, HY + T + 0.05f, 0.03f, 0.08f);
        Ring(steel, P(s, to - 0.015f, 0f, HY), s.d, s.n, half + 0.05f, HY + T + 0.05f, 0.03f, 0.08f);

        // Три подвеса: хомут снаружи короба, пластина на балке, полка снизу и косынка к стене за коробом.
        float span = to - from;
        for (int i = 0; i < 3; i++)
        {
            float a = from + span * (0.17f + 0.33f * i);
            float strapY = top + 0.03f + (-T - 0.04f - top - 0.03f) * 0.5f, strapLength = top + T + 0.07f;
            Vector3 strapFace = P(s, a, -front - StrapThick * 0.5f, strapY);
            steel.Box(strapFace, s.d, Vector3.up, s.n, new Vector3(StrapWidth, strapLength, StrapThick));
            for (float y = -T + 0.1f; y < top; y += 0.26f) Rivet(steel, P(s, a, -front - StrapThick, y), -s.n, 0.02f);
            // Пластина к балке.
            steel.Box(P(s, a, -front + 0.05f, top + 0.04f), s.d, Vector3.up, s.n, new Vector3(0.22f, 0.012f, 0.16f));
            foreach (float x in new[] { -0.08f, 0.08f }) Rivet(steel, P(s, a + x, -front + 0.05f, top + 0.046f), Vector3.up, 0.016f);
            // Полка под коробом.
            steel.Box(P(s, a, 0f, -T - 0.02f), s.d, Vector3.up, s.n, new Vector3(StrapWidth, 0.04f, half * 2f + 0.03f));
            // Косынка: от полки вниз и наискось к стене, стена — на севере.
            Vector3 wallFoot = P(s, a, half + 0.02f, -T - 0.32f), mid = P(s, a, 0.1f, -T - 0.04f);
            Vector3 axis = (mid - wallFoot).normalized, right = s.d, up = Vector3.Cross(axis, right).normalized;
            steel.Box((mid + wallFoot) * 0.5f, right, up, axis, new Vector3(0.07f, 0.02f, (mid - wallFoot).magnitude));
            steel.Box(P(s, a, half + 0.03f, -T - 0.2f), right, Vector3.up, s.n, new Vector3(0.12f, 0.34f, 0.012f));
            Rivet(steel, P(s, a, half + 0.02f, -T - 0.34f), -s.n, 0.016f);
        }
    }

    // Выпуклая шляпка: восьмигранный купол, основанием на поверхности.
    private static void Rivet(MB mb, Vector3 at, Vector3 outward, float radius)
    {
        Vector3 u = Vector3.Cross(outward, Mathf.Abs(outward.y) > 0.9f ? Vector3.right : Vector3.up).normalized, v = Vector3.Cross(outward, u);
        Vector3 tip = at + outward * radius * 0.7f;
        Vector2 uv = new Vector2(at.x + at.z, at.y) * 0.07f;
        for (int i = 0; i < 8; i++)
        {
            float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
            Vector3 p0 = at + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius, p1 = at + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
            int start = mb.V.Count;
            mb.V.Add(p0); mb.V.Add(p1); mb.V.Add(tip);
            mb.UV.Add(uv); mb.UV.Add(uv + new Vector2(0.02f, 0f)); mb.UV.Add(uv + new Vector2(0.01f, 0.02f));
            bool ok = Vector3.Dot(Vector3.Cross(p1 - p0, tip - p0), outward) >= 0f;
            mb.I.Add(start); mb.I.Add(ok ? start + 1 : start + 2); mb.I.Add(ok ? start + 2 : start + 1);
        }
    }

    // Прямоугольная рама в плоскости, перпендикулярной d: полосы шириной bar, толщиной depth.
    private static void Ring(MB mb, Vector3 center, Vector3 d, Vector3 across, float halfAcross, float halfUp, float depth, float bar)
    {
        foreach (float sign in new[] { -1f, 1f })
        {
            mb.Box(center + across * (sign * (halfAcross - bar * 0.5f)), across, Vector3.up, d, new Vector3(bar, halfUp * 2f, depth));
            mb.Box(center + Vector3.up * (sign * (halfUp - bar * 0.5f)), across, Vector3.up, d, new Vector3(halfAcross * 2f, bar, depth));
        }
    }

    // Стены: старая дыра у пола заделана, новая — под коробом. Старые куски выключены, а не удалены: ссылки сцены целы.
    private static void RebuildWalls(BasementDepths depths, Transform parent)
    {
        Transform vent = depths.grillePivot.parent;
        foreach (string name in new[] { "Wall above hole", "Sill" })
            if (vent.Find(name) != null) vent.Find(name).gameObject.SetActive(false);
        foreach (Transform part in vent)
            if (part.name.StartsWith("Frame")) part.gameObject.SetActive(false);
        Transform hall = UnityEngine.Object.FindFirstObjectByType<BasementLevel>(FindObjectsInactive.Include).transform.Find("Room B");
        foreach (Transform part in hall)
            if (part.name == "Wall" && Mathf.Abs(part.position.x + 37.05f) < 0.02f && part.position.z > 117f) part.gameObject.SetActive(false);
        Material stairWall = vent.Find("Wall above hole").GetComponent<MeshRenderer>().sharedMaterial;

        var holder = new GameObject("Wall closures").transform;
        holder.SetParent(parent, false);
        float floor = BasementLayout.FloorY, ceiling = BasementLayout.CeilingY, low = Y0 - T, high = Y0 + S + T;
        float south = 117.72f - H - T, north = 117.72f + H + T;
        var hallMesh = new MB();
        void HallWall(float x, float thick, float zMin, float zMax, float yMin, float yMax)
        {
            var c = new Vector3(x, 0.5f * (yMin + yMax), 0.5f * (zMin + zMax));
            var size = new Vector3(thick, yMax - yMin, zMax - zMin);
            hallMesh.Box(c, Vector3.right, Vector3.up, Vector3.forward, size);
            var hit = new GameObject("Closure", typeof(BoxCollider));
            hit.transform.SetParent(holder, false);
            hit.transform.position = c;
            hit.GetComponent<BoxCollider>().size = size;
        }
        void Wall(float x, float thick, float zMin, float zMax, float yMin, float yMax)
        {
            var c = new Vector3(x, 0.5f * (yMin + yMax), 0.5f * (zMin + zMax));
            var size = new Vector3(thick, yMax - yMin, zMax - zMin);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Closure";
            wall.transform.SetParent(holder, false);
            wall.transform.position = c;
            wall.transform.localScale = size;
            wall.GetComponent<MeshRenderer>().sharedMaterial = stairWall;
        }
        // Стена комнаты под лестницей: снизу и сверху короба, южный косяк.
        Wall(-37.705f, 0.25f, 117.03f, 118.18f, floor, low);
        Wall(-37.705f, 0.25f, 117.03f, 118.18f, high, ceiling);
        Wall(-37.705f, 0.25f, 117.03f, south, low, high);
        // Западная стена зала B.
        HallWall(-37.05f, 0.3f, 116.95f, 118.4f, floor, low);
        HallWall(-37.05f, 0.3f, 116.95f, 118.4f, high, ceiling);
        HallWall(-37.05f, 0.3f, 116.95f, south, low, high);
        HallWall(-37.05f, 0.3f, north, 118.4f, low, high);
        // Северная стена зала: дыра старого устья.
        HallWall(-36.0f, 2.2f, 118.4f, 118.7f, floor, -3.04f);
        for (int i = 0; i < hallMesh.UV.Count; i++) hallMesh.UV[i] /= 2.4f; // тот же шаг текстуры, что у стен залов
        AddMesh(holder, "Hall closures", hallMesh, AssetDatabase.LoadAssetAtPath<Material>("Assets/Basement/Rooms/Materials/RoomWall.mat"), null);
    }

    // Решётка, её рама и точка взаимодействия переезжают на торец короба. Петли — на южном краю, как раньше.
    private static List<Renderer> MoveVentParts(BasementDepths depths, Materials m)
    {
        Transform pivot = depths.grillePivot;
        var old = pivot.Find("Grille");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (Transform part in pivot) part.gameObject.SetActive(false);
        pivot.SetPositionAndRotation(new Vector3(DepthsLayout.Vent.x, DepthsLayout.Vent.y, 117.72f - H), Quaternion.identity);

        MB frame = new MB(), slats = new MB();
        const float width = W, height = S, bar = 0.075f, depth = 0.04f;
        Vector3 center = new Vector3(-depth * 0.5f, 0f, width * 0.5f);
        Ring(frame, center, Vector3.right, Vector3.forward, width * 0.5f, height * 0.5f, depth, bar);
        const int count = 8;
        float step = (height - bar * 2f) / count, slatLength = width - bar * 2f;
        for (int i = 0; i < count; i++)
        {
            Vector3 c = center + Vector3.up * (-height * 0.5f + bar + step * (i + 0.5f));
            Quaternion q = Quaternion.AngleAxis(-38f, Vector3.forward);
            Vector3 chord = q * Vector3.left, normal = q * Vector3.up;
            slats.Quad(c - Vector3.forward * (slatLength * 0.5f) - chord * step * 0.62f, c + Vector3.forward * (slatLength * 0.5f) - chord * step * 0.62f,
                c - Vector3.forward * (slatLength * 0.5f) + chord * step * 0.62f, normal, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0.9f));
            slats.Quad(c - Vector3.forward * (slatLength * 0.5f) - chord * step * 0.62f, c + Vector3.forward * (slatLength * 0.5f) - chord * step * 0.62f,
                c - Vector3.forward * (slatLength * 0.5f) + chord * step * 0.62f, -normal, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0.9f));
        }
        var result = new List<Renderer>();
        var grille = new GameObject("Grille").transform;
        grille.SetParent(pivot, false);
        result.Add(AddMesh(grille, "Grille frame", frame, m.steel, result));
        result.Add(AddMesh(grille, "Grille slats", slats, m.slat, result));
        result.AddRange(BuildHatch(depths, grille, center, width * 0.5f - bar * 0.5f, height * 0.5f - bar * 0.5f, m));

        depths.vent.transform.position = new Vector3(DepthsLayout.Vent.x - 0.08f, DepthsLayout.Vent.y, DepthsLayout.Vent.z);
        depths.tapeView.position = DepthsLayout.TapeCameraPosition;
        depths.tapeView.rotation = Quaternion.LookRotation(DepthsLayout.TapeCameraLook - DepthsLayout.TapeCameraPosition);
        depths.tapeCamera.transform.position = DepthsLayout.TapeCameraPosition;
        return result;
    }

    // Болт со шлицем: ось +Z уходит в раму, шляпка — со стороны −Z.
    private static Mesh BoltMesh()
    {
        const int sides = 12;
        const float head = 0.03f, shank = 0.009f, headDepth = 0.012f, length = 0.05f, slotHalf = 0.0028f, slotDepth = 0.005f;
        var mb = new MB();
        void Tube(float r0, float z0, float r1, float z1)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 e0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), e1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                mb.Quad(e0 * r0 + Vector3.forward * z0, e1 * r0 + Vector3.forward * z0, e0 * r1 + Vector3.forward * z1, e0 + e1 + Vector3.back * Mathf.Sign(r0 - r1),
                    new Vector2(i * 0.03f, 0f), new Vector2((i + 1) * 0.03f, 0f), new Vector2(i * 0.03f, 0.05f));
            }
        }
        Tube(head * 0.72f, -headDepth, head, -headDepth * 0.35f);
        Tube(head, -headDepth * 0.35f, head, 0f);
        Tube(shank, 0f, shank, length);
        // Шляпка: две половины по сторонам шлица и дно шлица.
        foreach (float side in new[] { -1f, 1f })
            mb.Quad(new Vector3(-head * 0.72f, slotHalf * side, -headDepth), new Vector3(head * 0.72f, slotHalf * side, -headDepth),
                new Vector3(-head * 0.72f, head * 0.72f * side, -headDepth), Vector3.back, Vector2.zero, new Vector2(0.05f, 0f), new Vector2(0f, 0.05f));
        mb.Quad(new Vector3(-head * 0.72f, -slotHalf, -headDepth + slotDepth), new Vector3(head * 0.72f, -slotHalf, -headDepth + slotDepth),
            new Vector3(-head * 0.72f, slotHalf, -headDepth + slotDepth), Vector3.back, new Vector2(0.2f, 0f), new Vector2(0.25f, 0f), new Vector2(0.2f, 0.01f));
        foreach (float side in new[] { -1f, 1f })
            mb.Quad(new Vector3(-head * 0.72f, slotHalf * side, -headDepth), new Vector3(head * 0.72f, slotHalf * side, -headDepth),
                new Vector3(-head * 0.72f, slotHalf * side, -headDepth + slotDepth), Vector3.down * side, new Vector2(0.2f, 0f), new Vector2(0.25f, 0f), new Vector2(0.2f, 0.01f));
        return SaveMesh(mb.ToMesh("Duct_Vent_bolt"));
    }

    // Отдельный объект «решётка»: четыре болта по углам рамы, отвёртка для мини-игры и её звуки.
    private static List<Renderer> BuildHatch(BasementDepths depths, Transform grille, Vector3 center, float halfWidth, float halfHeight, Materials m)
    {
        var result = new List<Renderer>();
        var hatch = depths.grillePivot.GetComponent<VentHatch>();
        if (hatch == null) hatch = depths.grillePivot.gameObject.AddComponent<VentHatch>();
        depths.hatch = hatch;

        Mesh boltMesh = BoltMesh();
        var so = new SerializedObject(hatch);
        SerializedProperty bolts = so.FindProperty("bolts");
        bolts.arraySize = VentBolts.Count;
        int index = 0;
        foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                var bolt = new GameObject("Bolt " + (index + 1), typeof(MeshFilter), typeof(MeshRenderer));
                bolt.transform.SetParent(grille, false);
                bolt.transform.localPosition = center + new Vector3(-0.022f, y * halfHeight, z * halfWidth);
                bolt.transform.localRotation = Quaternion.LookRotation(Vector3.right);
                bolt.GetComponent<MeshFilter>().sharedMesh = boltMesh;
                var part = bolt.GetComponent<MeshRenderer>();
                part.sharedMaterial = m.bolt;
                result.Add(part);
                bolts.GetArrayElementAtIndex(index++).objectReferenceValue = bolt.transform;
            }

        // Отвёртка: корень крутится вокруг +Z, жало смотрит вперёд.
        const string id = "screwdriver", folder = "Assets/ThirdParty/PolyHaven/" + id + "/";
        var driver = new GameObject("Vent screwdriver").transform;
        driver.SetParent(grille, false);
        Transform model = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + id + ".fbx"), driver)).transform;
        Material material = ScrewdriverMaterial();
        Renderer[] parts = model.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer part in parts) part.sharedMaterial = material;
        MeshFilter shape = model.GetComponentInChildren<MeshFilter>();
        Mesh mesh = shape.sharedMesh;
        // В файле инструмент лежит вдоль самой длинной оси; короткий конец — жало, он на стороне, где меш уже.
        Vector3 size = mesh.bounds.size;
        Vector3 axis = size.x > size.y ? (size.x > size.z ? Vector3.right : Vector3.forward) : (size.y > size.z ? Vector3.up : Vector3.forward);
        model.rotation = Quaternion.FromToRotation(shape.transform.TransformDirection(TipSide(mesh, axis)), Vector3.forward) * model.rotation;
        model.localScale *= ScrewdriverLength / parts[0].bounds.size.magnitude;
        Bounds bounds = parts[0].bounds;
        model.position += driver.position - new Vector3(bounds.center.x, bounds.center.y, driver.position.z); // ось вращения идёт по оси инструмента
        bounds = parts[0].bounds;
        var tip = new GameObject("Tip").transform;
        tip.SetParent(driver, false);
        tip.position = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
        result.AddRange(parts);

        var audio = new GameObject("Vent hatch audio").transform;
        audio.SetParent(grille, false);
        audio.position = DepthsLayout.Vent;
        var mixer = AudioRouting.Group(AudioBus.World);
        AudioSource turn = Source(audio, "Screw turn", DepthsLayout.Vent, Clip("ScrewTurn.ogg", true), mixer, 0.75f, 0.9f, 1f, 7f, 1f);
        AudioSource impact = Source(audio, "Hatch impact", DepthsLayout.Vent, null, mixer, 1f, 0.9f, 1.5f, 12f, 1f);
        impact.loop = false;

        so.FindProperty("driver").objectReferenceValue = driver;
        so.FindProperty("driverTip").objectReferenceValue = tip;
        so.FindProperty("turnSource").objectReferenceValue = turn;
        so.FindProperty("impactSource").objectReferenceValue = impact;
        so.FindProperty("seatClip").objectReferenceValue = Clip("ScrewSeat.ogg", true);
        so.FindProperty("hatchOffClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Horror/impactMetal_heavy_001.ogg");
        SetArray(so.FindProperty("dropClips"), new UnityEngine.Object[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Game/Foley/Items/MetalDrop1.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Game/Foley/Items/MetalDrop2.ogg"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Game/Foley/Items/MetalDrop3.ogg")
        });
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(depths);
        return result;
    }

    // Жало тоньше рукояти: сравниваем толщину меша у двух концов длинной оси.
    private static Vector3 TipSide(Mesh mesh, Vector3 axis)
    {
        Vector3 center = mesh.bounds.center;
        float half = Vector3.Dot(mesh.bounds.size, axis) * 0.5f, plus = 0f, minus = 0f;
        foreach (Vector3 vertex in mesh.vertices)
        {
            Vector3 local = vertex - center;
            float along = Vector3.Dot(local, axis), radius = (local - axis * along).magnitude;
            if (along > half * 0.7f) plus = Mathf.Max(plus, radius);
            else if (along < -half * 0.7f) minus = Mathf.Max(minus, radius);
        }
        return plus < minus ? axis : -axis;
    }

    private const float ScrewdriverLength = 0.25f;

    public static Material ScrewdriverMaterial()
    {
        const string path = "Assets/ThirdParty/PolyHaven/screwdriver/screwdriver.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/PolyHaven/screwdriver/Textures/screwdriver_diff_1k.jpg"));
        material.SetFloat("_Smoothness", 0.4f);
        material.SetFloat("_Metallic", 0.3f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Отвёртка в игре: предмет инвентаря, отвёртка на верстаке и она же в руке.
    [MenuItem("Tools/Basement/Install Screwdriver")]
    public static void InstallScrewdriver()
    {
        const string fbx = "Assets/ThirdParty/PolyHaven/screwdriver/screwdriver.fbx", itemPath = "Assets/Inventory/Items/Screwdriver.asset";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        Material material = ScrewdriverMaterial();
        var item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(itemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
            AssetDatabase.CreateAsset(item, itemPath);
        }
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("id").stringValue = "Screwdriver";
        itemSo.FindProperty("displayName").stringValue = "ОТВЁРТКА";
        itemSo.FindProperty("description").stringValue = "Плоская отвёртка с верстака в подвале.";
        itemSo.FindProperty("category").intValue = (int)InventoryItemCategory.Tool;
        itemSo.FindProperty("previewPrefab").objectReferenceValue = prefab;
        itemSo.FindProperty("previewTextureOverride").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/PolyHaven/screwdriver/Textures/screwdriver_diff_1k.jpg");
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        var database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>("Assets/Inventory/InventoryDatabase.asset");
        var dbSo = new SerializedObject(database);
        SerializedProperty items = dbSo.FindProperty("items");
        if (database.Find("Screwdriver") == null)
        {
            items.arraySize++;
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
            dbSo.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject Copy(string name, Transform parent)
        {
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            copy.name = name;
            foreach (Renderer part in copy.GetComponentsInChildren<Renderer>(true)) part.sharedMaterial = material;
            return copy;
        }

        GameObject bench = GameObject.Find("Workbench screwdriver");
        if (bench != null) UnityEngine.Object.DestroyImmediate(bench);
        bench = Copy("Workbench screwdriver", null);
        bench.layer = InteractableLayer;
        Mesh mesh = bench.GetComponentInChildren<MeshFilter>().sharedMesh;
        Vector3 size = mesh.bounds.size;
        Vector3 axis = size.x > size.y ? (size.x > size.z ? Vector3.right : Vector3.forward) : (size.y > size.z ? Vector3.up : Vector3.forward);
        bench.transform.rotation = Quaternion.AngleAxis(32f, Vector3.up) * Quaternion.FromToRotation(bench.transform.TransformDirection(axis), Vector3.right) * bench.transform.rotation;
        Renderer renderer = bench.GetComponentInChildren<Renderer>();
        bench.transform.localScale *= ScrewdriverLength / renderer.bounds.size.magnitude;
        Physics.Raycast(new Vector3(-39.5f, -2f, 113.2f), Vector3.down, out RaycastHit top, 3f);
        bench.transform.position += new Vector3(-39.5f, top.point.y + 0.003f, 113.2f) - new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, renderer.bounds.center.z);
        var box = bench.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = bench.transform.InverseTransformPoint(renderer.bounds.center);
        Vector3 localSize = bench.transform.InverseTransformVector(renderer.bounds.size + Vector3.one * 0.03f);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        bench.AddComponent<ToolPickup>();
        foreach (Transform child in bench.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = InteractableLayer;

        var inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
        if (inventory.screwdriverInHand != null) UnityEngine.Object.DestroyImmediate(inventory.screwdriverInHand);
        GameObject hand = Copy("ScrewdriverInHand", inventory.hammerInHand.transform.parent);
        hand.transform.localPosition = new Vector3(0.22f, -0.2f, 0.42f);
        hand.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        hand.transform.localRotation = Quaternion.FromToRotation(axis, Quaternion.Euler(-25f, -12f, 0f) * Vector3.forward) * hand.transform.localRotation;
        hand.transform.localScale *= ScrewdriverLength / hand.GetComponentInChildren<Renderer>().bounds.size.magnitude / inventory.hammerInHand.transform.parent.lossyScale.x;
        hand.SetActive(false);
        inventory.screwdriverInHand = hand;
        EditorUtility.SetDirty(inventory);
        EditorSceneManager.MarkSceneDirty(inventory.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    // Ящики под торцом: по ним видно, откуда герой лезет наверх.
    private static List<Renderer> BuildCrates(Transform parent)
    {
        const string id = "wooden_crate_01", folder = "Assets/ThirdParty/PolyHaven/" + id + "/";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + id + ".fbx");
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + id + ".mat");
        if (prefab == null || material == null) throw new InvalidOperationException("Нет модели " + id);
        var result = new List<Renderer>();
        const float CrateHeight = 0.42f;
        var points = new[] { new Vector3(-41.05f, BasementLayout.FloorY, 117.62f), new Vector3(-41.0f, BasementLayout.FloorY + CrateHeight, 117.68f) };
        for (int i = 0; i < points.Length; i++)
        {
            var crate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            crate.transform.rotation = Quaternion.Euler(0f, i * 9f + 4f, 0f) * crate.transform.rotation;
            Renderer[] parts = crate.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer part in parts) part.sharedMaterial = material;
            Bounds bounds = parts[0].bounds;
            foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
            crate.transform.localScale *= CrateHeight / bounds.size.y;
            bounds = parts[0].bounds;
            foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
            crate.transform.position += points[i] - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            result.AddRange(parts);
            var hit = new GameObject("Crate collider", typeof(BoxCollider));
            hit.transform.SetParent(parent, false);
            hit.transform.position = points[i] + Vector3.up * (CrateHeight * 0.5f);
            hit.GetComponent<BoxCollider>().size = new Vector3(bounds.size.x, CrateHeight, bounds.size.z);
        }
        return result;
    }

    // Швы листов: рамки-фланцы раз в Period метров с болтами.
    private static void BuildFlanges(Seg s, MB mb)
    {
        float end = s.last ? s.len + H - 0.1f : s.len - H - 0.12f;
        for (float a = s.a0 + Period; a < end; a += Period)
        {
            const float w = 0.07f, p = 0.03f;
            Vector3 up = Vector3.up;
            mb.Box(P(s, a, 0f, p * 0.5f), s.n, up, s.d, new Vector3(W, p, w));
            mb.Box(P(s, a, 0f, S - p * 0.5f), s.n, up, s.d, new Vector3(W, p, w));
            foreach (float side in new[] { -1f, 1f })
                mb.Box(P(s, a, side * (H - p * 0.5f), HY), s.n, up, s.d, new Vector3(p, S, w));
            for (float x = -H + 0.12f; x < H - 0.05f; x += 0.2f)
            {
                mb.Box(P(s, a, x, p + 0.008f), s.n, up, s.d, new Vector3(0.035f, 0.016f, 0.035f));
                mb.Box(P(s, a, x, S - p - 0.008f), s.n, up, s.d, new Vector3(0.035f, 0.016f, 0.035f));
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(P(s, a, side * (H - p - 0.008f), x + HY), s.n, up, s.d, new Vector3(0.016f, 0.035f, 0.035f));
            }
        }
    }

    // Потёки, пятна, плесень, царапины на стенах; лужи на полу. Атлас 2x2: 0 пятно, 1 потёки, 2 плесень, 3 когти.
    private static void BuildDecals(Seg s, int index, System.Random rng, MB grime, MB puddle, List<Vector3> dripPoints)
    {
        const float off = 0.018f;
        float a0 = s.a0 + 0.15f, a1 = s.len + H - 0.15f;
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        void Cell(MB mb, Vector3 center, Vector3 right, Vector3 up, float w, float h, int cell, Vector3 facing)
        {
            Vector2 min = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
            mb.Quad(center - right * (w * 0.5f) - up * (h * 0.5f), center + right * (w * 0.5f) - up * (h * 0.5f), center - right * (w * 0.5f) + up * (h * 0.5f),
                facing, min, min + new Vector2(0.5f, 0f), min + new Vector2(0f, 0.5f));
        }

        foreach (float side in new[] { -1f, 1f })
        {
            WallRange(s, side, out float from, out float to);
            int count = Mathf.CeilToInt((to - from) / 0.75f);
            for (int k = 0; k < count; k++)
            {
                float a = R(from + 0.2f, to - 0.2f);
                int kind = rng.Next(0, 3);
                Vector3 facing = -s.n * side;
                float across = side * (H - off);
                if (kind == 0) Cell(grime, P(s, a, across, R(0.3f, 0.9f)), s.d, Vector3.up, R(0.5f, 0.95f), R(0.4f, 0.8f), 0, facing);
                else if (kind == 1) { float h = R(0.6f, 1.05f); Cell(grime, P(s, a, across, S - h * 0.5f - 0.02f), s.d, Vector3.up, R(0.35f, 0.6f), h, 1, facing); }
                else Cell(grime, P(s, a, across, R(0.1f, 0.4f)), s.d, Vector3.up, R(0.4f, 0.75f), R(0.35f, 0.6f), 2, facing);
            }
        }
        // Царапины когтей на второй секции.
        if (index == 1) Cell(grime, P(s, s.len * 0.45f, H - off, 0.58f), s.d, Vector3.up, 0.7f, 0.7f, 3, -s.n);
        // Пятна на потолке.
        for (float a = a0 + 0.3f; a < a1 - 0.3f; a += R(0.9f, 1.5f))
        {
            Vector3 right = Quaternion.AngleAxis(R(0f, 360f), Vector3.up) * Vector3.right;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            Cell(grime, P(s, a, R(-0.1f, 0.1f), S - off), right, fwd, R(0.4f, 0.55f), R(0.4f, 0.55f), 0, Vector3.down);
        }
        // Лужи и слизь на полу.
        for (float a = a0 + 0.1f; a < a1 - 0.2f; a += R(0.6f, 1.2f))
        {
            Vector3 right = Quaternion.AngleAxis(R(0f, 360f), Vector3.up) * Vector3.right;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            float size = R(0.35f, 0.55f);
            Vector3 c = P(s, a, R(-0.1f, 0.1f), 0.012f);
            int cellP = rng.Next(0, 2);
            puddle.Quad(c - right * (size * 0.5f) - fwd * (size * 0.5f), c + right * (size * 0.5f) - fwd * (size * 0.5f), c - right * (size * 0.5f) + fwd * (size * 0.5f),
                Vector3.up, new Vector2(cellP * 0.5f, 0f), new Vector2(cellP * 0.5f + 0.5f, 0f), new Vector2(cellP * 0.5f, 1f));
        }
        if (index == 0) dripPoints.Add(P(s, 0.9f, 0.18f, 0f));
        if (index == 0) dripPoints.Add(P(s, 2.1f, -0.2f, 0f));
    }

    // Оторванные листы жести, доски, паутина.
    private static void BuildDebris(Seg s, int index, System.Random rng, MB wood, MB sheet, MB web)
    {
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        // Доски: лежат на полу вразнобой, одна подпирает стену.
        if (index != 1)
        {
            for (int k = 0; k < 2; k++)
            {
                Quaternion q = Quaternion.AngleAxis(R(-30f, 30f) + (k == 0 ? 0f : 80f), Vector3.up) * Quaternion.LookRotation(s.d, Vector3.up);
                float len = R(0.7f, 1.1f);
                wood.Box(P(s, Mathf.Lerp(s.a0 + 0.5f, s.len - 0.4f, (k + 0.3f) / 2f), R(-0.25f, 0.25f), 0.012f), q * Vector3.right, q * Vector3.up, q * Vector3.forward, new Vector3(0.14f, 0.024f, len));
            }
        }
        else
        {
            Quaternion q = Quaternion.LookRotation(s.d, Vector3.up) * Quaternion.Euler(-48f, 0f, 0f) * Quaternion.Euler(0f, 0f, 12f);
            Vector3 foot = P(s, s.len * 0.35f, 0.3f, 0.1f);
            wood.Box(foot + q * Vector3.forward * 0.5f, q * Vector3.right, q * Vector3.up, q * Vector3.forward, new Vector3(0.16f, 0.026f, 1.0f));
        }
        // Паутина в углах потолка: четверть-круг в плоскости сечения, видна, когда смотришь вдоль трубы.
        float wa = s.len * 0.55f + R(-0.2f, 0.2f);
        foreach (float side in new[] { -1f, 1f })
        {
            if (rng.NextDouble() < 0.4) continue;
            Vector3 corner = P(s, wa + side * 0.2f, side * (H - 0.02f), S - 0.02f);
            float size = R(0.4f, 0.6f);
            web.Quad(corner, corner - s.n * side * size, corner + Vector3.down * size, -s.d, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f));
            web.Quad(corner, corner - s.n * side * size, corner + Vector3.down * size, s.d, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f));
        }
    }

    // Старый кабель под потолком: провисает между скобами.
    private static void BuildCables(Seg s, MB mb)
    {
        foreach (float across in new[] { -0.3f, -0.22f })
        {
            const float clipEvery = 0.9f, radius = 0.012f;
            const int sides = 6;
            var points = new List<Vector3>();
            for (float a = -0.2f; a <= s.len - H; a += 0.08f)
            {
                float t = Mathf.Repeat(a / clipEvery, 1f);
                float sag = 0.07f * 4f * t * (1f - t);
                points.Add(P(s, a, across, S - 0.06f - sag));
            }
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 axis = (points[i + 1] - points[i]).normalized, r = Vector3.Cross(axis, Vector3.up).normalized, u = Vector3.Cross(r, axis);
                for (int k = 0; k < sides; k++)
                {
                    float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                    Vector3 n0 = r * Mathf.Cos(a0) + u * Mathf.Sin(a0), n1 = r * Mathf.Cos(a1) + u * Mathf.Sin(a1);
                    Vector3 o = points[i] + n0 * radius;
                    mb.Quad(o, points[i + 1] + n0 * radius, points[i] + n1 * radius, (n0 + n1) * 0.5f, Vector2.zero, new Vector2(0.1f, 0f), new Vector2(0f, 0.1f));
                }
            }
            for (float a = 0.1f; a < s.len - H; a += clipEvery)
                mb.Box(P(s, a, across, S - 0.035f), s.n, Vector3.up, s.d, new Vector3(0.035f, 0.035f, 0.05f));
        }
    }

    // Устье в зале: рама на стене, у пола короба открытый край, с него герой спрыгивает в зал.
    private static void BuildOutlet(Seg last, MB mb)
    {
        Vector3 mouth = last.start + last.d * (last.len + H) + Vector3.up * HY;
        Ring(mb, mouth + last.d * 0.015f, last.d, last.n, H + 0.1f, HY + 0.1f, 0.03f, 0.1f);
    }

    private static void BuildColliders(List<Seg> segs, Transform parent)
    {
        var holder = new GameObject("Colliders").transform;
        holder.SetParent(parent, false);
        void Block(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }
        Vector3 Sized(Vector3 d, float along, Vector3 n, float across, float height) =>
            new Vector3(Mathf.Abs(d.x) * along + Mathf.Abs(n.x) * across, height, Mathf.Abs(d.z) * along + Mathf.Abs(n.z) * across);

        foreach (Seg s in segs)
        {
            // Последняя секция кончается на устье и не вылезает в зал.
            float fromA = -H - T, toA = s.last ? s.len + H : s.len + H + T;
            Vector3 mid = s.start + s.d * ((fromA + toA) * 0.5f);
            Vector3 span = Sized(s.d, toA - fromA, s.n, W + T * 2f, T);
            Block("Floor", mid + Vector3.up * (-T * 0.5f), span);
            Block("Top", mid + Vector3.up * (S + T * 0.5f), span);
            foreach (float side in new[] { -1f, 1f })
            {
                WallRange(s, side, out float from, out float to);
                Vector3 c = s.start + s.d * ((from + to) * 0.5f) + s.n * side * (H + T * 0.5f) + Vector3.up * HY;
                Block("Wall", c, Sized(s.d, to - from, s.n, T, S));
            }
        }
    }

    // ───────────── звук и капель ─────────────

    private static AudioClip Clip(string file, bool mono)
    {
        string path = Dir + "Audio/" + file;
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null && importer.forceToMono != mono) { importer.forceToMono = mono; importer.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static AudioSource Source(Transform parent, string name, Vector3 at, AudioClip clip, UnityEngine.Audio.AudioMixerGroup mixer,
        float volume, float spatial, float minDistance, float maxDistance, float pitch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = volume;
        src.spatialBlend = spatial;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.dopplerLevel = 0f;
        src.pitch = pitch;
        if (mixer != null) src.outputAudioMixerGroup = mixer;
        return src;
    }

    private static void BuildDrip(Transform parent, string name, Vector3 at, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.45f;
        main.startSpeed = 0f;
        main.startSize = 0.014f;
        main.gravityModifier = 1f;
        main.maxParticles = 6;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0.5f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.015f, 0.01f, 0.015f);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 1.5f;
        rend.velocityScale = 0.06f;
        rend.sharedMaterial = material;
        rend.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void SetArray(SerializedProperty prop, UnityEngine.Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static Renderer AddMesh(Transform parent, string name, MB mb, Material material, List<Renderer> list, bool noShadow = false, bool cullOff = false)
    {
        if (mb.V.Count == 0) return null;
        Mesh mesh = SaveMesh(mb.ToMesh("Duct_" + name.Replace(' ', '_')));
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.allowOcclusionWhenDynamic = false; // запечённое occlusion ничего не знает о трубе и прятало её изнутри
        if (noShadow) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        return r;
    }

    private static Mesh SaveMesh(Mesh mesh)
    {
        string path = Dir + "Meshes/" + mesh.name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        existing.Clear();
        existing.indexFormat = mesh.indexFormat;
        existing.SetVertices(mesh.vertices);
        existing.SetUVs(0, mesh.uv);
        existing.SetTriangles(mesh.triangles, 0);
        existing.RecalculateNormals();
        existing.RecalculateBounds();
        existing.RecalculateTangents();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // ───────────── текстуры и материалы ─────────────

    private sealed class Materials
    {
        public Material wall, floor, flange, wood, cable, grime, puddle, web, drip, panel, steel, slat, bolt;
    }

    private static string FullPath(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);

    private static Texture2D Import(string assetPath, bool normal, bool srgb, bool clamp = false, bool alpha = false)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        bool changed = false;
        TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (ti.textureType != type) { ti.textureType = type; changed = true; }
        if (!normal && ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; changed = true; }
        TextureWrapMode wrap = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        if (ti.wrapMode != wrap) { ti.wrapMode = wrap; changed = true; }
        if (ti.alphaIsTransparency != alpha) { ti.alphaIsTransparency = alpha; changed = true; }
        if (changed) ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static Texture2D ReadJpg(string assetPath)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        tex.LoadImage(File.ReadAllBytes(FullPath(assetPath)));
        return tex;
    }

    // Металл/гладкость в одной карте: R = металличность (приглушена: под землёй нет отражений), A = гладкость из шероховатости.
    private static void MakeMetalSmooth(string set, bool hasMetal, float metalScale, float smoothScale)
    {
        string outPath = Dir + "Textures/" + set + "_MS.png";
        Texture2D rough = ReadJpg(Dir + "Textures/" + set + "_Roughness.jpg");
        Texture2D metal = hasMetal ? ReadJpg(Dir + "Textures/" + set + "_Metalness.jpg") : null;
        Color32[] r = rough.GetPixels32(), mt = metal != null ? metal.GetPixels32() : null;
        var o = new Color32[r.Length];
        for (int i = 0; i < r.Length; i++)
        {
            byte m = mt != null ? (byte)(mt[i].r * metalScale) : (byte)0;
            byte sm = (byte)Mathf.Clamp((255 - r[i].r) * smoothScale, 0, 255);
            o[i] = new Color32(m, m, m, sm);
        }
        var result = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
        result.SetPixels32(o);
        File.WriteAllBytes(FullPath(outPath), result.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(rough);
        if (metal != null) UnityEngine.Object.DestroyImmediate(metal);
        UnityEngine.Object.DestroyImmediate(result);
        Import(outPath, false, false);
    }

    private static void BuildTextures()
    {
        // Текстуры автора: копия *_Height становится нормалью из яркости.
        foreach (string gen in new[] { "GenBlockWall", "GenDuctInner", "GenDuctSlat", "GenDuctSteel", "GenDuctPanel" })
        {
            Import(Dir + "Textures/" + gen + ".png", false, true);
            string height = Dir + "Textures/" + gen + "_Height.png";
            AssetDatabase.ImportAsset(height, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(height);
            if (importer.textureType != TextureImporterType.NormalMap || !importer.convertToNormalmap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = true;
                importer.heightmapScale = 0.04f;
                importer.SaveAndReimport();
            }
        }
        foreach (string set in new[] { "Metal021", "Metal024", "Planks023A" })
        {
            Import(Dir + "Textures/" + set + "_Color.jpg", false, true);
            Import(Dir + "Textures/" + set + "_NormalGL.jpg", true, false);
            AssetDatabase.ImportAsset(Dir + "Textures/" + set + "_Roughness.jpg");
        }
        // Готовые карты не пересобираем (генерация долгая); чтобы перегенерировать, удалить PNG.
        if (!File.Exists(FullPath(Dir + "Textures/Metal021_MS.png"))) MakeMetalSmooth("Metal021", true, 0.35f, 0.9f);
        if (!File.Exists(FullPath(Dir + "Textures/Metal024_MS.png"))) MakeMetalSmooth("Metal024", true, 0.35f, 0.9f);
        if (!File.Exists(FullPath(Dir + "Textures/Planks023A_MS.png"))) MakeMetalSmooth("Planks023A", false, 0f, 0.55f);
        if (!File.Exists(FullPath(Dir + "Textures/DuctGrime.png"))) WritePng("DuctGrime.png", GenGrime(), true);
        if (!File.Exists(FullPath(Dir + "Textures/DuctPuddle.png"))) WritePng("DuctPuddle.png", GenPuddle(), true);
        if (!File.Exists(FullPath(Dir + "Textures/DuctWeb.png"))) WritePng("DuctWeb.png", GenWeb(), true);
        if (!File.Exists(FullPath(Dir + "Textures/DuctDrop.png"))) WritePng("DuctDrop.png", GenDrop(), true);
    }

    private static void WritePng(string file, Texture2D tex, bool alpha)
    {
        string path = Dir + "Textures/" + file;
        File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Import(path, false, true, true, alpha);
    }

    private static float Fbm(float x, float y, int oct = 5)
    {
        float a = 0.5f, sum = 0f, f = 1f;
        for (int i = 0; i < oct; i++) { sum += a * Mathf.PerlinNoise(x * f + 17.3f * i, y * f + 9.1f * i); f *= 2f; a *= 0.5f; }
        return sum / 0.96875f;
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static Texture2D GenGrime()
    {
        const int N = 1024, C = 512;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int cell = x / C + 2 * (y / C);
                float lx = (x % C) / (float)C, ly = (y % C) / (float)C;
                float edge = Smooth(0f, 0.07f, Mathf.Min(Mathf.Min(lx, 1f - lx), Mathf.Min(ly, 1f - ly)));
                float r = Mathf.Sqrt((lx - 0.5f) * (lx - 0.5f) + (ly - 0.5f) * (ly - 0.5f)) * 2f;
                Color c = Color.clear;
                if (cell == 0)
                {
                    float n = Fbm(lx * 4f + 3f, ly * 4f + 3f), v = n + (1f - r) * 0.6f;
                    float a = Smooth(0.78f, 1.08f, v) * 0.85f;
                    c = Color.Lerp(new Color(0.02f, 0.018f, 0.015f), new Color(0.1f, 0.055f, 0.025f), Fbm(lx * 9f, ly * 9f));
                    c.a = a * edge;
                }
                else if (cell == 1)
                {
                    float best = 0f;
                    float[] xs = { 0.15f, 0.32f, 0.5f, 0.7f, 0.86f }, ls = { 0.9f, 0.55f, 0.75f, 0.4f, 0.65f }, ws = { 0.02f, 0.03f, 0.024f, 0.018f, 0.028f };
                    for (int k = 0; k < 5; k++)
                    {
                        float t = (1f - ly) / ls[k];
                        if (t > 1f || t < 0f) continue;
                        float cx = xs[k] + 0.012f * Mathf.Sin(ly * 17f + k * 3f);
                        float wd = ws[k] * (1f + 1.1f * Smooth(0.82f, 1f, t)) * (0.7f + 0.6f * Fbm(ly * 6f + k, k));
                        float g = Mathf.Exp(-((lx - cx) / wd) * ((lx - cx) / wd));
                        best = Mathf.Max(best, g * (1f - t * t * 0.7f) * (0.65f + 0.35f * Fbm(lx * 12f, ly * 5f + k)));
                    }
                    c = Color.Lerp(new Color(0.1f, 0.055f, 0.03f), new Color(0.04f, 0.06f, 0.035f), Mathf.Clamp01(1f - ly));
                    c.a = best * 0.85f * edge;
                }
                else if (cell == 2)
                {
                    float n = Fbm(lx * 10f, ly * 10f), m = Fbm(lx * 3f + 8f, ly * 3f + 8f) - r * 0.5f;
                    float a = Smooth(0.28f, 0.5f, m) * Smooth(0.38f, 0.68f, n);
                    c = Color.Lerp(new Color(0.08f, 0.12f, 0.06f), new Color(0.22f, 0.28f, 0.17f), n);
                    c.a = a * 0.85f * edge;
                }
                else
                {
                    float best = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        float cx = 0.25f + 0.17f * k + 0.1f * (ly - 0.5f) + 0.015f * Mathf.Sin(ly * 9f + k);
                        float taper = Smooth(0.08f, 0.3f, ly) * Smooth(0.95f, 0.65f, ly);
                        float wd = 0.004f + 0.008f * taper;
                        best = Mathf.Max(best, Mathf.Clamp01(1f - Mathf.Abs(lx - cx) / wd) * taper);
                    }
                    c = new Color(0.62f, 0.56f, 0.5f, best * 0.9f * edge);
                }
                px[y * N + x] = c;
            }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.SetPixels32(px);
        return tex;
    }

    private static Texture2D GenPuddle()
    {
        const int W = 1024, Hh = 512;
        var px = new Color32[W * Hh];
        for (int y = 0; y < Hh; y++)
            for (int x = 0; x < W; x++)
            {
                int cell = x / 512;
                float lx = (x % 512) / 512f, ly = y / 512f;
                float edge = Smooth(0f, 0.06f, Mathf.Min(Mathf.Min(lx, 1f - lx), Mathf.Min(ly, 1f - ly)));
                float r = Mathf.Sqrt((lx - 0.5f) * (lx - 0.5f) + (ly - 0.5f) * (ly - 0.5f)) * 2f;
                float v = Fbm(lx * 3f + cell * 5f, ly * 3f + cell * 2f) + (1f - r) * 0.9f;
                Color c = Color.Lerp(new Color(0.012f, 0.026f, 0.02f), new Color(0.03f, 0.05f, 0.035f), Fbm(lx * 6f, ly * 6f));
                c.a = Smooth(0.95f, 1.15f, v) * 0.92f * edge;
                px[y * W + x] = c;
            }
        var tex = new Texture2D(W, Hh, TextureFormat.RGBA32, true);
        tex.SetPixels32(px);
        return tex;
    }

    // Четверть паутины от угла (0,0): лучи и провисающие дуги.
    private static Texture2D GenWeb()
    {
        const int N = 512;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N, r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float a = 0f;
                if (r < 1f)
                {
                    const int strands = 6;
                    float step = Mathf.PI * 0.5f / strands, nearest = Mathf.Round(th / step) * step;
                    a = Mathf.Max(a, 1f - Smooth(0.0015f, 0.005f, Mathf.Abs(r * Mathf.Sin(th - nearest))));
                    foreach (float rk in new[] { 0.16f, 0.3f, 0.46f, 0.64f, 0.84f })
                    {
                        float sag = rk * (1f - 0.14f * Mathf.Sin(th * 2f * strands / 3f));
                        a = Mathf.Max(a, (1f - Smooth(0.001f, 0.004f, Mathf.Abs(r - sag))) * 0.8f);
                    }
                    a *= 0.6f * (1f - Smooth(0.85f, 1f, r)) * (0.7f + 0.3f * Fbm(u * 20f, v * 20f));
                }
                px[y * N + x] = new Color(0.82f, 0.82f, 0.78f, a);
            }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.SetPixels32(px);
        return tex;
    }

    private static Texture2D GenDrop()
    {
        const int N = 32;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 15.5f;
                px[y * N + x] = new Color(0.7f, 0.78f, 0.76f, 1f - Smooth(0.55f, 1f, d));
            }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        return tex;
    }

    private static Texture2D Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "Textures/" + file);

    private static Material Mat(string name, string shader, Color tint, string baseTex, string normalTex, string msTex, float smooth, bool transparent, bool cullOff)
    {
        string path = Dir + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find(shader);
        m.SetColor("_BaseColor", tint);
        m.SetTexture("_BaseMap", baseTex != null ? Tex(baseTex) : null);
        if (normalTex != null) { m.SetTexture("_BumpMap", Tex(normalTex)); m.SetFloat("_BumpScale", 1.2f); m.EnableKeyword("_NORMALMAP"); }
        if (msTex != null)
        {
            m.SetTexture("_MetallicGlossMap", Tex(msTex));
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_Metallic", 1f);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else { m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f); }
        m.SetFloat("_Cull", cullOff ? 0f : 2f);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f); // иначе прозрачная часть наклейки блестит светлым прямоугольником
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Materials BuildMaterials()
    {
        const string lit = "Universal Render Pipeline/Lit", particle = "Universal Render Pipeline/Particles/Unlit";
        var m = new Materials
        {
            wall = Mat("DuctWall", lit, Color.white, "GenDuctInner.png", "GenDuctInner_Height.png", null, 0.12f, false, false),
            floor = Mat("DuctFloor", lit, new Color(0.8f, 0.78f, 0.76f), "GenDuctInner.png", "GenDuctInner_Height.png", null, 0.12f, false, false),
            panel = Mat("DuctPanel", lit, Color.white, "GenDuctPanel.png", "GenDuctPanel_Height.png", null, 0.18f, false, false),
            steel = Mat("DuctSteel", lit, Color.white, "GenDuctSteel.png", "GenDuctSteel_Height.png", null, 0.2f, false, false),
            bolt = Mat("DuctBolt", lit, new Color(0.78f, 0.74f, 0.68f), "Metal024_Color.jpg", "Metal024_NormalGL.jpg", "Metal024_MS.png", 0f, false, false),
            slat = Mat("DuctSlat", lit, Color.white, "GenDuctSlat.png", "GenDuctSlat_Height.png", null, 0.15f, false, true),
            flange = Mat("DuctFlange", lit, new Color(0.3f, 0.24f, 0.21f), "Metal024_Color.jpg", "Metal024_NormalGL.jpg", "Metal024_MS.png", 0f, false, false),
            wood = Mat("DuctWood", lit, new Color(0.33f, 0.29f, 0.26f), "Planks023A_Color.jpg", "Planks023A_NormalGL.jpg", "Planks023A_MS.png", 0f, false, false),
            cable = Mat("DuctCable", lit, new Color(0.03f, 0.03f, 0.032f), null, null, null, 0.08f, false, false),
            grime = Mat("DuctGrime", lit, Color.white, "DuctGrime.png", null, null, 0.15f, true, false),
            puddle = Mat("DuctPuddle", lit, Color.white, "DuctPuddle.png", null, null, 0.97f, true, false),
            web = Mat("DuctWeb", lit, Color.white, "DuctWeb.png", null, null, 0f, true, true),
            drip = Mat("DuctDrip", particle, Color.white, "DuctDrop.png", null, null, 0f, true, false)
        };
        m.wall.SetTextureScale("_BaseMap", Vector2.one * 0.8f);
        m.floor.SetTextureScale("_BaseMap", Vector2.one * 0.8f);
        m.steel.SetTextureScale("_BaseMap", Vector2.one / SteelTile);
        AssetDatabase.SaveAssets();
        return m;
    }
}
