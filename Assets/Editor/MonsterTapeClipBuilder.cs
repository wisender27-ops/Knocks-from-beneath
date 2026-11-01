using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using KnocksFromBeneath;

// C-04: две humanoid-анимации монстра для плёнки подвала — ползание на четвереньках (по трубе и из неё) и удар по камере.
// Делается так же, как MonsterPeekClipBuilder: позы задаются поворотами костей в осях монстра, HumanPoseHandler переводит их в мышцы,
// клип ложится на любой humanoid. Ползание — цикл из восьми поз (диагональный ход, корпус виляет), удар — замах, удар рукой
// по объективу (в момент VentTape.StrikeImpact), проводка. Пересборка — меню KFB/C-04; Capture — снимки поз без Play Mode.
public static class MonsterTapeClipBuilder
{
    public const string CrawlPath = "Assets/Resources/Monster/MonsterCrawl.anim";
    public const string StrikePath = "Assets/Resources/Monster/MonsterStrike.anim";
    private const string MonsterPrefab = "Scares/CornerPeekMonster";
    private const string TempPrefix = "KFB_Tmp_C04_";
    private const float CrawlCycle = 1.4f;
    private const int CrawlPhases = 8;

    // Поворот кости вокруг оси монстра (x — вправо, плюс — корпус вперёд/вниз; z — вперёд; y — вверх).
    private sealed class Rig
    {
        public readonly Animator Animator;
        private readonly Transform root;
        public Rig(Animator animator, Transform root) { Animator = animator; this.root = root; }
        public Rig Pitch(HumanBodyBones bone, float degrees) => Turn(bone, root.right, degrees);
        public Rig Roll(HumanBodyBones bone, float degrees) => Turn(bone, root.forward, degrees);
        public Rig Yaw(HumanBodyBones bone, float degrees) => Turn(bone, root.up, degrees);
        private Rig Turn(HumanBodyBones bone, Vector3 axis, float degrees)
        {
            Transform t = Animator.GetBoneTransform(bone);
            if (t != null) t.rotation = Quaternion.AngleAxis(degrees, axis) * t.rotation;
            return this;
        }
        public Vector3 Point(Vector3 local) => root.TransformPoint(local);
    }

    private enum Ground { Feet, Crawl }

    private sealed class Frame
    {
        public float Time;
        public Action<Rig> Pose;
        public Ground Ground = Ground.Feet;
        public Vector3? LeftHand, RightHand; // куда приходят ладони (от пола под монстром, в его осях)
    }

    // ------------------------------------------------------------------------------------------------
    // Ползание: корпус почти горизонтально, диагональный ход (левая рука с правой ногой), корпус вилюет.
    // ------------------------------------------------------------------------------------------------

    private static List<Frame> CrawlFrames()
    {
        var frames = new List<Frame>();
        for (int i = 0; i <= CrawlPhases; i++)
        {
            float p = (float)i / CrawlPhases;
            frames.Add(new Frame { Time = p * CrawlCycle, Pose = CrawlPose(p % 1f), Ground = Ground.Crawl });
        }
        return frames;
    }

    private static Action<Rig> CrawlPose(float p)
    {
        return r =>
        {
            float a = p * Mathf.PI * 2f, s = Mathf.Sin(a), c = Mathf.Cos(a);
            const float lean = 80f, swingLeg = 24f, swingArm = 28f;
            r.Pitch(HumanBodyBones.Hips, lean);
            r.Pitch(HumanBodyBones.Spine, 8f).Pitch(HumanBodyBones.Chest, 6f).Pitch(HumanBodyBones.UpperChest, 4f);
            r.Yaw(HumanBodyBones.Spine, 9f * s).Yaw(HumanBodyBones.Chest, -6f * s);
            r.Pitch(HumanBodyBones.Neck, -36f).Pitch(HumanBodyBones.Head, -34f + 6f * c);
            float rightLegForward = swingLeg * s, leftLegForward = -swingLeg * s;
            float leftArmForward = swingArm * s, rightArmForward = -swingArm * s;
            // Бёдра после наклона корпуса смотрят назад, возвращаем их вниз и качаем: вперёд — колено поднимается.
            r.Pitch(HumanBodyBones.LeftUpperLeg, -lean - leftLegForward).Pitch(HumanBodyBones.RightUpperLeg, -lean - rightLegForward);
            r.Roll(HumanBodyBones.LeftUpperLeg, -10f).Roll(HumanBodyBones.RightUpperLeg, 10f);
            r.Pitch(HumanBodyBones.LeftLowerLeg, 95f + 32f * Mathf.Max(0f, leftLegForward / swingLeg));
            r.Pitch(HumanBodyBones.RightLowerLeg, 95f + 32f * Mathf.Max(0f, rightLegForward / swingLeg));
            const float torso = lean + 18f;
            r.Pitch(HumanBodyBones.LeftUpperArm, -torso - leftArmForward).Pitch(HumanBodyBones.RightUpperArm, -torso - rightArmForward);
            r.Roll(HumanBodyBones.LeftUpperArm, 14f).Roll(HumanBodyBones.RightUpperArm, -14f);
            r.Pitch(HumanBodyBones.LeftLowerArm, -10f - 28f * Mathf.Max(0f, leftArmForward / swingArm));
            r.Pitch(HumanBodyBones.RightLowerArm, -10f - 28f * Mathf.Max(0f, rightArmForward / swingArm));
        };
    }

    // ------------------------------------------------------------------------------------------------
    // Удар по камере. Монстр стоит в 0.95 м от объектива (VentTape.Striker), объектив — на высоте 1.3 м.
    // ------------------------------------------------------------------------------------------------

    private static List<Frame> StrikeFrames()
    {
        float impact = VentTape.StrikeImpact;
        Vector3 lens = new Vector3(0f, 1.3f, VentTape.StrikeDistance);
        Action<Rig> ready = r => r.Pitch(HumanBodyBones.Spine, 4f).Pitch(HumanBodyBones.Neck, 8f);
        return new List<Frame>
        {
            new Frame { Time = 0f, Pose = ready },
            // Замах: корпус назад, правая рука высоко за плечом, левая для равновесия.
            new Frame
            {
                Time = impact * 0.6f,
                Pose = r =>
                {
                    r.Pitch(HumanBodyBones.Spine, -10f).Pitch(HumanBodyBones.Chest, -8f).Pitch(HumanBodyBones.UpperChest, -6f);
                    r.Pitch(HumanBodyBones.Neck, 6f).Roll(HumanBodyBones.Spine, 5f);
                    r.Pitch(HumanBodyBones.LeftUpperLeg, -10f).Pitch(HumanBodyBones.RightUpperLeg, 12f).Pitch(HumanBodyBones.RightLowerLeg, 12f);
                },
                LeftHand = new Vector3(-0.5f, 1.3f, 0.15f),
                RightHand = new Vector3(0.32f, 2.0f, -0.15f)
            },
            // Удар: корпус бросается вперёд, шаг правой, рука в объектив.
            new Frame
            {
                Time = impact,
                Pose = r =>
                {
                    r.Pitch(HumanBodyBones.Hips, 8f);
                    r.Pitch(HumanBodyBones.Spine, 20f).Pitch(HumanBodyBones.Chest, 16f).Pitch(HumanBodyBones.UpperChest, 12f);
                    r.Pitch(HumanBodyBones.Neck, 10f);
                    r.Pitch(HumanBodyBones.RightUpperLeg, -38f).Pitch(HumanBodyBones.RightLowerLeg, 24f).Pitch(HumanBodyBones.LeftUpperLeg, 12f);
                },
                LeftHand = new Vector3(-0.4f, 1.1f, 0.4f),
                RightHand = lens
            },
            // Проводка: рука уходит вниз и в сторону — камера уже летит.
            new Frame
            {
                Time = impact + 0.22f,
                Pose = r =>
                {
                    r.Pitch(HumanBodyBones.Spine, 20f).Pitch(HumanBodyBones.Chest, 16f).Pitch(HumanBodyBones.UpperChest, 14f);
                    r.Pitch(HumanBodyBones.Neck, 14f).Yaw(HumanBodyBones.Spine, -12f);
                    r.Pitch(HumanBodyBones.RightUpperLeg, -32f).Pitch(HumanBodyBones.RightLowerLeg, 26f).Pitch(HumanBodyBones.LeftUpperLeg, 10f);
                },
                LeftHand = new Vector3(-0.4f, 0.9f, 0.5f),
                RightHand = new Vector3(-0.35f, 0.85f, 0.85f)
            },
            new Frame
            {
                Time = impact + 1.1f,
                Pose = r =>
                {
                    r.Pitch(HumanBodyBones.Spine, 20f).Pitch(HumanBodyBones.Chest, 16f).Pitch(HumanBodyBones.UpperChest, 14f);
                    r.Pitch(HumanBodyBones.Neck, 14f).Yaw(HumanBodyBones.Spine, -12f);
                    r.Pitch(HumanBodyBones.RightUpperLeg, -32f).Pitch(HumanBodyBones.RightLowerLeg, 26f).Pitch(HumanBodyBones.LeftUpperLeg, 10f);
                },
                LeftHand = new Vector3(-0.4f, 0.9f, 0.5f),
                RightHand = new Vector3(-0.35f, 0.85f, 0.85f)
            }
        };
    }

    [MenuItem("KFB/C-04/Build Monster Crawl And Strike Clips")]
    public static void Build()
    {
        BuildClip(CrawlPath, CrawlFrames(), loop: true);
        BuildClip(StrikePath, StrikeFrames(), loop: false);
        AssetDatabase.SaveAssets();
    }

    private static void BuildClip(string path, List<Frame> frames, bool loop)
    {
        Dictionary<string, AnimationCurve> curves = Bake(frames);
        if (curves == null) return;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool created = clip == null;
        if (created) clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
        clip.ClearCurves();
        foreach (var pair in curves)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), pair.Key), pair.Value);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(clip, path);
        }
        EditorUtility.SetDirty(clip);
    }

    private static Dictionary<string, AnimationCurve> Bake(List<Frame> frames)
    {
        var prefab = Resources.Load<GameObject>(MonsterPrefab);
        if (prefab == null) { Debug.LogError("[C-04] Нет префаба " + MonsterPrefab); return null; }
        var go = UnityEngine.Object.Instantiate(prefab);
        go.name = TempPrefix + "Bake";
        try
        {
            go.SetActive(true);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = go.GetComponentInChildren<Animator>();
            var curves = new Dictionary<string, AnimationCurve>();
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                foreach (Frame frame in frames)
                {
                    var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount], bodyPosition = Vector3.up * animator.humanScale, bodyRotation = Quaternion.identity };
                    for (int i = 0; i < HumanTrait.MuscleCount; i++)
                        pose.muscles[i] = MonsterPeekClipBuilder.Stand.TryGetValue(HumanTrait.MuscleName[i], out float v) ? v : 0f;
                    handler.SetHumanPose(ref pose);
                    var rig = new Rig(animator, go.transform);
                    frame.Pose(rig);
                    if (frame.Ground == Ground.Feet) CornerStalkerRig.GroundFeet(animator, 0f);
                    else GroundCrawl(animator);
                    if (frame.LeftHand.HasValue) MonsterPeekClipBuilder.Reach(animator, true, rig.Point(frame.LeftHand.Value));
                    if (frame.RightHand.HasValue) MonsterPeekClipBuilder.Reach(animator, false, rig.Point(frame.RightHand.Value));
                    handler.GetHumanPose(ref pose);
                    // Что осталось от цели после мышечных пределов: прочитанная поза ставится обратно и меряется рука.
                    handler.SetHumanPose(ref pose);
                    if (frame.RightHand.HasValue)
                    {
                        float miss = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position, rig.Point(frame.RightHand.Value));
                        if (miss > 0.2f) Debug.LogWarning("[C-04] Правая рука в кадре " + frame.Time.ToString("0.00") + " не дошла до цели на " + miss.ToString("0.00") + " м.");
                    }
                    for (int i = 0; i < HumanTrait.MuscleCount; i++) AddKey(curves, HumanTrait.MuscleName[i], frame.Time, pose.muscles[i]);
                    Vector3 body = pose.bodyPosition / animator.humanScale;
                    AddKey(curves, "RootT.x", frame.Time, body.x);
                    AddKey(curves, "RootT.y", frame.Time, body.y);
                    AddKey(curves, "RootT.z", frame.Time, body.z);
                    AddKey(curves, "RootQ.x", frame.Time, pose.bodyRotation.x);
                    AddKey(curves, "RootQ.y", frame.Time, pose.bodyRotation.y);
                    AddKey(curves, "RootQ.z", frame.Time, pose.bodyRotation.z);
                    AddKey(curves, "RootQ.w", frame.Time, pose.bodyRotation.w);
                }
            }
            return curves;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // Самая низкая из ладоней, колен, ступней и головы встаёт на пол: ползущий монстр опирается на то, что ниже.
    private static void GroundCrawl(Animator animator)
    {
        float lowest = float.MaxValue;
        void Probe(HumanBodyBones bone, float radius)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null) lowest = Mathf.Min(lowest, t.position.y - radius);
        }
        Probe(HumanBodyBones.LeftHand, 0.05f); Probe(HumanBodyBones.RightHand, 0.05f);
        Probe(HumanBodyBones.LeftLowerLeg, 0.07f); Probe(HumanBodyBones.RightLowerLeg, 0.07f);
        Probe(HumanBodyBones.LeftFoot, 0.07f); Probe(HumanBodyBones.RightFoot, 0.07f);
        Probe(HumanBodyBones.Head, 0.12f);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips != null && lowest < float.MaxValue) hips.position -= Vector3.up * lowest;
    }

    private static void AddKey(Dictionary<string, AnimationCurve> curves, string name, float time, float value)
    {
        if (!curves.TryGetValue(name, out var curve)) curves[name] = curve = new AnimationCurve();
        curve.AddKey(new Keyframe(time, value, 0f, 0f));
    }

    // ------------------------------------------------------------------------------------------------
    // Снимки поз без Play Mode (проверка глазами): монстр на временной сцене, боковой вид, PNG в папку outputDir.
    // ------------------------------------------------------------------------------------------------

    public static void Capture(string clipPath, float[] times, string outputDir, bool front)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        var prefab = Resources.Load<GameObject>(MonsterPrefab);
        if (clip == null || prefab == null) { Debug.LogError("[C-04] Нет клипа или префаба для снимка."); return; }
        Directory.CreateDirectory(outputDir);
        var temp = new List<GameObject>();
        RenderTexture rt = null;
        try
        {
            var cameraObject = new GameObject(TempPrefix + "Camera");
            temp.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            camera.fieldOfView = 40f;
            camera.enabled = false;
            var lightObject = new GameObject(TempPrefix + "Light");
            temp.Add(lightObject);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightObject.transform.rotation = Quaternion.Euler(40f, front ? 160f : 250f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = TempPrefix + "Floor";
            temp.Add(floor);
            floor.transform.position = new Vector3(0f, 499.95f, 0f);
            floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
            rt = new RenderTexture(640, 480, 24);
            camera.targetTexture = rt;
            foreach (float time in times)
            {
                // Каждая поза — на свежем экземпляре: скиннинг в Editor между вызовами не обновляется.
                var monster = UnityEngine.Object.Instantiate(prefab);
                monster.name = TempPrefix + "Monster";
                temp.Add(monster);
                monster.SetActive(true);
                monster.transform.SetPositionAndRotation(new Vector3(0f, 500f, 0f), Quaternion.identity);
                var animator = monster.GetComponentInChildren<Animator>();
                ApplyClipPose(animator, clip, time);
                // Центр кадра — на теле: сбоку по x или спереди по z.
                Vector3 look = monster.transform.position + Vector3.up * 0.8f;
                camera.transform.position = look + (front ? new Vector3(0.4f, 0.3f, 3.6f) : new Vector3(3.6f, 0.3f, 0.4f));
                camera.transform.LookAt(look);
                camera.Render();
                camera.Render(); // скиннинг догоняет позу со второго кадра
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var shot = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                shot.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(outputDir, Path.GetFileNameWithoutExtension(clipPath) + "_" + time.ToString("0.00") + (front ? "_f" : "_s") + ".png"), shot.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(shot);
                UnityEngine.Object.DestroyImmediate(monster);
            }
        }
        finally
        {
            foreach (GameObject obj in temp)
                if (obj != null && obj.name.StartsWith(TempPrefix, StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(obj);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }

    private static void ApplyClipPose(Animator animator, AnimationClip clip, float time)
    {
        var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
        for (int i = 0; i < HumanTrait.MuscleCount; i++) pose.muscles[i] = Sample(clip, HumanTrait.MuscleName[i], time, 0f);
        pose.bodyPosition = new Vector3(Sample(clip, "RootT.x", time, 0f), Sample(clip, "RootT.y", time, 1f), Sample(clip, "RootT.z", time, 0f)) * animator.humanScale;
        pose.bodyRotation = new Quaternion(Sample(clip, "RootQ.x", time, 0f), Sample(clip, "RootQ.y", time, 0f),
            Sample(clip, "RootQ.z", time, 0f), Sample(clip, "RootQ.w", time, 1f)).normalized;
        using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
        {
            handler.SetHumanPose(ref pose);
        }
    }

    private static float Sample(AnimationClip clip, string property, float time, float fallback)
    {
        var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), property));
        return curve != null ? curve.Evaluate(time) : fallback;
    }
}
