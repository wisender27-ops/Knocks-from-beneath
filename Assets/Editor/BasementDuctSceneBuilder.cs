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
    private const float S = DepthsLayout.DuctSize, H = S * 0.5f, T = 0.05f, Period = 1.2f;

    private const float Y0 = DepthsLayout.DuctFloorY;
    // Часть западной стены первой секции, которую занимает проём решётки, и верх кожуха трубы в углу зала B.
    private const float VentHoleLength = 0.325f, CasingTopY = -3.04f;

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
        MB grime = new MB(), puddle = new MB(), web = new MB(), casing = new MB();
        var dripPoints = new List<Vector3>();
        var creakPoints = new List<Vector3>();

        for (int i = 0; i < segs.Count; i++)
        {
            Seg s = segs[i];
            BuildShell(s, shell, floor);
            BuildFlanges(s, flange);
            BuildDecals(s, i, rng, grime, puddle, dripPoints);
            BuildDebris(s, i, rng, wood, sheet, web);
            if (i == 1) BuildCables(s, cable);
            creakPoints.Add(P(s, s.len * 0.5f, 0f, S - 0.1f));
        }
        BuildOutlet(segs[segs.Count - 1], flange);
        BuildStartCap(segs[0], shell);
        BuildCasing(casing);
        foreach (Vector3 point in dripPoints) puddle.Quad(point + new Vector3(-0.3f, 0.016f, -0.3f), point + new Vector3(0.3f, 0.016f, -0.3f), point + new Vector3(-0.3f, 0.016f, 0.3f),
            Vector3.up, new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));

        var renderers = new List<Renderer>();
        renderers.Add(AddMesh(visual, "Walls and ceiling", shell, m.wall, renderers));
        renderers.Add(AddMesh(visual, "Floor", floor, m.floor, renderers));
        renderers.Add(AddMesh(visual, "Flanges and grate", flange, m.flange, renderers));
        renderers.Add(AddMesh(visual, "Loose boards", wood, m.wood, renderers));
        renderers.Add(AddMesh(visual, "Torn sheets", sheet, m.wall, renderers, cullOff: true));
        renderers.Add(AddMesh(visual, "Casing in hall", casing, m.wall, renderers));
        renderers.Add(AddMesh(visual, "Cables", cable, m.cable, renderers));
        renderers.Add(AddMesh(visual, "Grime decals", grime, m.grime, renderers, noShadow: true));
        renderers.Add(AddMesh(visual, "Puddles", puddle, m.puddle, renderers, noShadow: true));
        renderers.Add(AddMesh(visual, "Cobwebs", web, m.web, renderers, noShadow: true));
        renderers.RemoveAll(r => r == null);

        BuildColliders(segs, duct.transform);

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
        Vector3 mid = P(segs[1], segs[1].len * 0.5f, 0f, H);
        loops.Add(Source(audio, "Air hum", mid, hum, mixer, 0.22f, 0.35f, 4f, 16f, 1f));
        AudioSource creak = Source(audio, "Metal creak", mid, null, mixer, 1f, 1f, 1.5f, 11f, 1f);
        creak.loop = false;

        var scene = root.AddComponent<BasementDuctScene>();
        BasementDepths depths = UnityEngine.Object.FindFirstObjectByType<BasementDepths>(FindObjectsInactive.Include);
        if (depths == null) throw new InvalidOperationException("В сцене нет BasementDepths.");
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
        Renderer[] all = duct.GetComponentsInChildren<Renderer>(true);
        SetArray(aso.FindProperty("renderers"), all);
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
        floor.Panel(P(s, a0, -H, 0f), s.d, a1 - a0, s.n, S, Vector3.up, 0.12f, 0.006f, 0.005f, 0f, s.len);
        shell.Panel(P(s, a0, -H, S), s.d, a1 - a0, s.n, S, Vector3.down, 0.12f, 0.008f, 0.007f, 0f, s.len + 3f);
        foreach (float side in new[] { -1f, 1f })
        {
            WallRange(s, side, out float from, out float to);
            if (s.first && side > 0f) from = VentHoleLength; // запад первой секции: здесь проём решётки
            shell.Panel(P(s, from, side * H, 0f), s.d, to - from, Vector3.up, S, -s.n * side, 0.12f, 0.009f, 0.008f, from - a0, s.len + side * 5f);
        }
    }

    private static void BuildStartCap(Seg f, MB shell)
    {
        shell.Panel(P(f, -H, -H, 0f), f.n, S, Vector3.up, S, f.d, 0.12f, 0.008f, 0f, 0f, 1f);
    }

    // Кожух: снаружи, в углу зала B, виден короб первой секции (внутри трубы эти грани отсечены).
    private static void BuildCasing(MB mb)
    {
        const float west = -36.9f, east = -36.345f, south = 117.195f, north = 118.4f, y0 = BasementLayout.FloorY;
        float height = CasingTopY - y0;
        mb.Panel(new Vector3(east, y0, south), Vector3.forward, north - south, Vector3.up, height, Vector3.right, 0.15f, 0.006f);
        mb.Panel(new Vector3(west, CasingTopY, south), Vector3.right, east - west, Vector3.forward, north - south, Vector3.up, 0.15f, 0.006f);
        mb.Panel(new Vector3(west, y0, south), Vector3.right, east - west, Vector3.up, height, Vector3.back, 0.15f, 0.006f);
    }

    // Швы листов: рамки-фланцы раз в Period метров с болтами.
    private static void BuildFlanges(Seg s, MB mb)
    {
        float end = s.last ? s.len + H - 0.1f : s.len - H - 0.12f;
        for (float a = s.a0 + Period; a < end; a += Period)
        {
            const float w = 0.07f, p = 0.03f;
            Vector3 up = Vector3.up;
            mb.Box(P(s, a, 0f, p * 0.5f), s.n, up, s.d, new Vector3(S, p, w));
            mb.Box(P(s, a, 0f, S - p * 0.5f), s.n, up, s.d, new Vector3(S, p, w));
            foreach (float side in new[] { -1f, 1f })
                mb.Box(P(s, a, side * (H - p * 0.5f), H), s.n, up, s.d, new Vector3(p, S, w));
            for (float x = -H + 0.12f; x < H - 0.05f; x += 0.2f)
            {
                mb.Box(P(s, a, x, p + 0.008f), s.n, up, s.d, new Vector3(0.035f, 0.016f, 0.035f));
                mb.Box(P(s, a, x, S - p - 0.008f), s.n, up, s.d, new Vector3(0.035f, 0.016f, 0.035f));
                foreach (float side in new[] { -1f, 1f })
                    mb.Box(P(s, a, side * (H - p - 0.008f), x + H), s.n, up, s.d, new Vector3(0.016f, 0.035f, 0.035f));
            }
        }
    }

    // Потёки, пятна, плесень, царапины на стенах; лужи на полу. Атлас 2x2: 0 пятно, 1 потёки, 2 плесень, 3 когти.
    private static void BuildDecals(Seg s, int index, System.Random rng, MB grime, MB puddle, List<Vector3> dripPoints)
    {
        const float off = 0.018f;
        if (s.last) return; // короткое устье: наклейки вылезли бы в зал
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
            Cell(grime, P(s, a, R(-0.3f, 0.3f), S - off), right, fwd, R(0.5f, 0.9f), R(0.5f, 0.9f), 0, Vector3.down);
        }
        // Лужи и слизь на полу.
        for (float a = a0 + 0.1f; a < a1 - 0.2f; a += R(0.6f, 1.2f))
        {
            Vector3 right = Quaternion.AngleAxis(R(0f, 360f), Vector3.up) * Vector3.right;
            Vector3 fwd = Vector3.Cross(right, Vector3.up);
            float size = R(0.45f, 0.95f);
            Vector3 c = P(s, a, R(-0.3f, 0.3f), 0.012f);
            int cellP = rng.Next(0, 2);
            puddle.Quad(c - right * (size * 0.5f) - fwd * (size * 0.5f), c + right * (size * 0.5f) - fwd * (size * 0.5f), c - right * (size * 0.5f) + fwd * (size * 0.5f),
                Vector3.up, new Vector2(cellP * 0.5f, 0f), new Vector2(cellP * 0.5f + 0.5f, 0f), new Vector2(cellP * 0.5f, 1f));
        }
        if (index == 0) dripPoints.Add(P(s, 0.9f, 0.18f, 0f));
        if (index == 1) dripPoints.Add(P(s, 1.2f, -0.2f, 0f));
    }

    // Оторванные листы жести, доски, паутина.
    private static void BuildDebris(Seg s, int index, System.Random rng, MB wood, MB sheet, MB web)
    {
        if (s.last) return;
        float R(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
        // Лист, отставший от потолка: рваный нижний край, висит наклонно, двусторонний.
        const int flaps = 1;
        for (int k = 0; k < flaps; k++)
        {
            float a = Mathf.Lerp(s.a0 + 0.5f, s.len - H - 0.2f, (k + 0.5f) / flaps) + R(-0.15f, 0.15f);
            float across = R(-0.28f, 0.28f), width = R(0.45f, 0.7f), length = R(0.4f, 0.62f), tilt = R(18f, 32f) * Mathf.Deg2Rad;
            float sgn = rng.Next(0, 2) == 0 ? 1f : -1f;
            Vector3 dir = s.d * (Mathf.Cos(tilt) * sgn) - Vector3.up * Mathf.Sin(tilt);
            Vector3 root = P(s, a, across, S - 0.025f), side = s.n * width * 0.5f;
            int cols = 5;
            for (int c = 0; c < cols; c++)
            {
                float t0 = (float)c / cols, t1 = (float)(c + 1) / cols;
                Vector3 b0 = root - side + side * 2f * t0, b1 = root - side + side * 2f * t1;
                float l0 = length * (0.7f + 0.3f * (float)rng.NextDouble());
                Vector3 e0 = b0 + dir * l0 + Vector3.down * 0.02f * c;
                Vector2 uv0 = new Vector2(t0 * width, 0f), uv1 = new Vector2(t1 * width, 0f);
                Vector3 nrm = Vector3.Cross(b1 - b0, e0 - b0).normalized;
                sheet.Quad(b0, b1, e0, nrm, uv0, uv1, new Vector2(uv0.x, l0));
                sheet.Quad(b0, b1, e0, -nrm, uv0, uv1, new Vector2(uv0.x, l0));
            }
        }
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

    // Устье в зале: рама в стене и решётка, которую выбили изнутри.
    private static void BuildOutlet(Seg last, MB mb)
    {
        const float bar = 0.05f, depth = 0.05f;
        Vector3 mouth = last.start + last.d * (last.len + H);
        foreach (float side in new[] { -1f, 1f })
        {
            mb.Box(mouth + Vector3.right * (side * (H + bar * 0.5f)) + Vector3.up * H, Vector3.right, Vector3.up, Vector3.forward, new Vector3(bar, S + bar * 2f, depth));
            mb.Box(mouth + Vector3.up * (H + side * (H + bar * 0.5f)), Vector3.right, Vector3.up, Vector3.forward, new Vector3(S, bar, depth));
        }
        Quaternion q = Quaternion.Euler(0f, 24f, 0f);
        Vector3 at = new Vector3(mouth.x + 0.7f, BasementLayout.FloorY + 0.03f, mouth.z - 0.75f);
        Vector3 right = q * Vector3.right, forward = q * Vector3.forward;
        foreach (float side in new[] { -1f, 1f })
        {
            mb.Box(at + right * (side * 0.5f), right, Vector3.up, forward, new Vector3(0.05f, 0.05f, 1.05f));
            mb.Box(at + forward * (side * 0.5f), right, Vector3.up, forward, new Vector3(1.05f, 0.05f, 0.05f));
        }
        for (int i = -3; i <= 3; i++)
            mb.Box(at + right * (i * 0.12f), right, Vector3.up, forward, new Vector3(0.035f, 0.035f, 1f));
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
            Vector3 span = Sized(s.d, toA - fromA, s.n, S + T * 2f, T);
            Block("Floor", mid + Vector3.up * (-T * 0.5f), span);
            Block("Top", mid + Vector3.up * (S + T * 0.5f), span);
            foreach (float side in new[] { -1f, 1f })
            {
                WallRange(s, side, out float from, out float to);
                if (s.first && side > 0f) from = VentHoleLength; // проём решётки свободен: сюда игрок залезает сам
                Vector3 c = s.start + s.d * ((from + to) * 0.5f) + s.n * side * (H + T * 0.5f) + Vector3.up * H;
                Block("Wall", c, Sized(s.d, to - from, s.n, T, S));
            }
        }
        Seg f = segs[0];
        Block("Start cap", f.start - f.d * (H + T * 0.5f) + Vector3.up * H, Sized(f.d, T, f.n, S + T * 2f, S));
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
        public Material wall, floor, flange, wood, cable, grime, puddle, web, drip;
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
            wall = Mat("DuctWall", lit, new Color(0.5f, 0.47f, 0.45f), "Metal021_Color.jpg", "Metal021_NormalGL.jpg", "Metal021_MS.png", 0f, false, false),
            floor = Mat("DuctFloor", lit, new Color(0.36f, 0.33f, 0.3f), "Metal024_Color.jpg", "Metal024_NormalGL.jpg", "Metal024_MS.png", 0f, false, false),
            flange = Mat("DuctFlange", lit, new Color(0.3f, 0.24f, 0.21f), "Metal024_Color.jpg", "Metal024_NormalGL.jpg", "Metal024_MS.png", 0f, false, false),
            wood = Mat("DuctWood", lit, new Color(0.33f, 0.29f, 0.26f), "Planks023A_Color.jpg", "Planks023A_NormalGL.jpg", "Planks023A_MS.png", 0f, false, false),
            cable = Mat("DuctCable", lit, new Color(0.03f, 0.03f, 0.032f), null, null, null, 0.35f, false, false),
            grime = Mat("DuctGrime", lit, Color.white, "DuctGrime.png", null, null, 0.15f, true, false),
            puddle = Mat("DuctPuddle", lit, Color.white, "DuctPuddle.png", null, null, 0.97f, true, false),
            web = Mat("DuctWeb", lit, Color.white, "DuctWeb.png", null, null, 0f, true, true),
            drip = Mat("DuctDrip", particle, Color.white, "DuctDrop.png", null, null, 0f, true, false)
        };
        m.wall.SetTextureScale("_BaseMap", Vector2.one);
        AssetDatabase.SaveAssets();
        return m;
    }
}
