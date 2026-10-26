using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// M-07: своя анимация монстра «заглянуть под кровать» (как у Granny) — humanoid-клип MonsterPeekUnderBed.
// Позы задаются поворотами костей в осях самого монстра (бедро вперёд, колено согнуть, голову набок), а в мышцы
// Mecanim их переводит HumanPoseHandler на временной копии монстра — так клип ложится на любой humanoid.
// Фазы: стоит → рывком на колени, ладони в пол → корпус вниз, голова у щели → рука тянется под кровать.
// Голову на игрока и набок (с подёргиванием) доворачивает в игре CornerStalkerRig.AimHead: мышцы Mecanim
// такой разворот головы не передают. Пересборка — меню KFB/M-07; Preview — поза без Play Mode.
public static class MonsterPeekClipBuilder
{
    public const string ClipPath = "Assets/Animations/Monster/MonsterPeekUnderBed.anim";
    private const string MonsterPrefab = "Scares/CornerPeekMonster";

    // Поворот кости вокруг оси монстра: x — вправо (плюс — корпус вперёд/вниз), z — вперёд (плюс — набок влево).
    private struct Turn
    {
        public HumanBodyBones bone;
        public Vector3 axis;
        public float degrees;
        public Turn(HumanBodyBones bone, Vector3 axis, float degrees) { this.bone = bone; this.axis = axis; this.degrees = degrees; }
    }

    private sealed class Key
    {
        public readonly float time;
        public readonly List<Turn> turns = new List<Turn>();
        // Куда приходят ладони (в осях монстра, от пола под ним; null — рука как в позе): локоть сгибает IK.
        public Vector3? leftHand, rightHand;
        public Key(float time) { this.time = time; }
        public Key Hands(Vector3? left, Vector3? right) { leftHand = left; rightHand = right; return this; }
        // Плюс — сгиб вперёд (корпус, шея, голова: наклон вниз; руки: вперёд).
        public Key Pitch(HumanBodyBones bone, float degrees) { turns.Add(new Turn(bone, Vector3.right, degrees)); return this; }
        public Key Roll(HumanBodyBones bone, float degrees) { turns.Add(new Turn(bone, Vector3.forward, degrees)); return this; }
        public Key Yaw(HumanBodyBones bone, float degrees) { turns.Add(new Turn(bone, Vector3.up, degrees)); return this; }
        // Нога: бедро вперёд-вверх, голень назад (сгиб колена).
        public Key Legs(float thighForward, float kneeBend, float spread = 0f)
        {
            Pitch(HumanBodyBones.LeftUpperLeg, -thighForward).Pitch(HumanBodyBones.RightUpperLeg, -thighForward);
            Pitch(HumanBodyBones.LeftLowerLeg, kneeBend).Pitch(HumanBodyBones.RightLowerLeg, kneeBend);
            if (spread != 0f) Roll(HumanBodyBones.LeftUpperLeg, -spread).Roll(HumanBodyBones.RightUpperLeg, spread);
            return this;
        }
        public Key Torso(float spine, float chest, float upperChest) =>
            Pitch(HumanBodyBones.Spine, spine).Pitch(HumanBodyBones.Chest, chest).Pitch(HumanBodyBones.UpperChest, upperChest);
    }

    // Стойка (база всех ключей) — в мышцах: в ней монстр стоит ровно, чуть сутулясь, руки висят.
    private static readonly Dictionary<string, float> Stand = new Dictionary<string, float>
    {
        { "Spine Front-Back", 0.1f }, { "Chest Front-Back", 0.1f }, { "UpperChest Front-Back", 0.1f },
        { "Neck Nod Down-Up", 0.1f },
        { "Left Upper Leg Front-Back", 0.6f }, { "Right Upper Leg Front-Back", 0.6f },
        { "Left Upper Leg In-Out", 0.1f }, { "Right Upper Leg In-Out", 0.1f },
        { "Left Lower Leg Stretch", 1f }, { "Right Lower Leg Stretch", 1f },
        { "Left Arm Down-Up", -0.6f }, { "Right Arm Down-Up", -0.6f },
        { "Left Arm Front-Back", 0.1f }, { "Right Arm Front-Back", 0.1f },
        { "Left Forearm Stretch", 0.9f }, { "Right Forearm Stretch", 0.9f },
    };

    private static List<Key> Keys()
    {
        // Общая поза «на коленях, голова в щели» — от неё пауза и рука.
        Func<float, Key> peek = t => new Key(t)
            .Legs(thighForward: 60f, kneeBend: 150f, spread: 18f)
            .Torso(65f, 55f, 45f)
            .Pitch(HumanBodyBones.Neck, -30f).Pitch(HumanBodyBones.Head, -20f)
            .Hands(new Vector3(-0.32f, 0.04f, 0.5f), new Vector3(0.32f, 0.04f, 0.5f));
        return new List<Key>
        {
            new Key(0f),                                       // стоит
            new Key(0.14f)                                     // провал на колени, руки к полу
                .Legs(thighForward: 30f, kneeBend: 130f, spread: 12f)
                .Torso(30f, 25f, 15f)
                .Pitch(HumanBodyBones.Neck, 10f)
                .Hands(new Vector3(-0.26f, 0.04f, 0.42f), new Vector3(0.26f, 0.04f, 0.42f)),
            peek(0.26f),                                       // корпус вниз — голова у щели (доворот и завал — риг)
            peek(0.75f),
            peek(1.15f).Hands(new Vector3(-0.32f, 0.04f, 0.5f), new Vector3(0.08f, 0.16f, 0.8f)), // рука под кровать к лицу
            peek(1.6f).Hands(new Vector3(-0.32f, 0.04f, 0.5f), new Vector3(0.08f, 0.16f, 0.8f)),
        };
    }

    [MenuItem("KFB/M-07/Build Monster Peek Clip")]
    public static void Build()
    {
        Dictionary<string, AnimationCurve> curves = Bake();
        if (curves == null) return;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        bool created = clip == null;
        if (created) clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(ClipPath) };
        clip.ClearCurves();
        foreach (var pair in curves)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), pair.Key), pair.Value);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.loopBlendOrientation = true;  // наклон тела — в позе
        settings.loopBlendPositionY = true;    // высота таза — в позе (ступни к полу прижимает CornerStalkerRig)
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (created)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ClipPath));
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    // Поза клипа в момент time на объекте с humanoid-аниматором (без Play Mode), ступни — на пол; голова — как в игре:
    // на lookAt и набок на roll (faceLocal — морда в осях кости головы, снятая до позы, как в CornerStalkerRig.Awake).
    public static void Preview(GameObject target, float time, Vector3 lookAt, float roll, Vector3 faceLocal)
    {
        Preview(target, time);
        var animator = target.GetComponentInChildren<Animator>();
        if (animator == null) return;
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        KnocksFromBeneath.CornerStalkerRig.AimHead(chest, animator.GetBoneTransform(HumanBodyBones.Neck),
            animator.GetBoneTransform(HumanBodyBones.Head), faceLocal, lookAt, time >= 0.2f ? 1f : 0f,
            KnocksFromBeneath.CornerStalkerRig.PeekLookShares, 145f, roll);
    }

    public static void Preview(GameObject target, float time)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var animator = target.GetComponentInChildren<Animator>();
        if (clip == null || animator == null || animator.avatar == null || !animator.avatar.isHuman) return;
        var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
        for (int i = 0; i < HumanTrait.MuscleCount; i++) pose.muscles[i] = Sample(clip, HumanTrait.MuscleName[i], time, 0f);
        pose.bodyPosition = new Vector3(Sample(clip, "RootT.x", time, 0f), Sample(clip, "RootT.y", time, 1f),
            Sample(clip, "RootT.z", time, 0f)) * animator.humanScale;
        pose.bodyRotation = new Quaternion(Sample(clip, "RootQ.x", time, 0f), Sample(clip, "RootQ.y", time, 0f),
            Sample(clip, "RootQ.z", time, 0f), Sample(clip, "RootQ.w", time, 1f)).normalized;
        using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            handler.SetHumanPose(ref pose);
        KnocksFromBeneath.CornerStalkerRig.GroundFeet(animator, target.transform.position.y);
    }

    private static float Sample(AnimationClip clip, string property, float time, float fallback)
    {
        var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), property));
        return curve != null ? curve.Evaluate(time) : fallback;
    }

    // Ключи → мышцы: на временной копии монстра ставится стойка, поверх — повороты костей ключа, и
    // HumanPoseHandler читает получившуюся позу как мышцы и положение тела.
    private static Dictionary<string, AnimationCurve> Bake()
    {
        var prefab = Resources.Load<GameObject>(MonsterPrefab);
        if (prefab == null) { Debug.LogError("[M-07] Нет префаба " + MonsterPrefab); return null; }
        var go = UnityEngine.Object.Instantiate(prefab);
        go.name = "__KFB_TMP_M07_BAKE__";
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            go.SetActive(true);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = go.GetComponentInChildren<Animator>();
            var curves = new Dictionary<string, AnimationCurve>();
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                foreach (var key in Keys())
                {
                    var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount], bodyPosition = Vector3.up * animator.humanScale, bodyRotation = Quaternion.identity };
                    for (int i = 0; i < HumanTrait.MuscleCount; i++)
                        pose.muscles[i] = Stand.TryGetValue(HumanTrait.MuscleName[i], out float v) ? v : 0f;
                    handler.SetHumanPose(ref pose);
                    foreach (var turn in key.turns)
                    {
                        Transform bone = animator.GetBoneTransform(turn.bone);
                        if (bone == null) continue;
                        Vector3 axis = go.transform.TransformDirection(turn.axis);
                        bone.rotation = Quaternion.AngleAxis(turn.degrees, axis) * bone.rotation;
                    }
                    // Опора на пол — как в игре (CornerStalkerRig), и только потом ладони: цели заданы от пола.
                    KnocksFromBeneath.CornerStalkerRig.GroundFeet(animator, 0f);
                    if (key.leftHand.HasValue) Reach(animator, true, go.transform.TransformPoint(key.leftHand.Value));
                    if (key.rightHand.HasValue) Reach(animator, false, go.transform.TransformPoint(key.rightHand.Value));
                    handler.GetHumanPose(ref pose);
                    for (int i = 0; i < HumanTrait.MuscleCount; i++) AddKey(curves, HumanTrait.MuscleName[i], key.time, pose.muscles[i]);
                    Vector3 body = pose.bodyPosition / animator.humanScale;
                    AddKey(curves, "RootT.x", key.time, body.x);
                    AddKey(curves, "RootT.y", key.time, body.y);
                    AddKey(curves, "RootT.z", key.time, body.z);
                    AddKey(curves, "RootQ.x", key.time, pose.bodyRotation.x);
                    AddKey(curves, "RootQ.y", key.time, pose.bodyRotation.y);
                    AddKey(curves, "RootQ.z", key.time, pose.bodyRotation.z);
                    AddKey(curves, "RootQ.w", key.time, pose.bodyRotation.w);
                }
            }
            return curves;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // Двухзвенный IK руки (плечо — локоть — кисть): локоть сгибается так, чтобы кисть пришла в target,
    // плоскость сгиба — как в текущей позе.
    private static void Reach(Animator animator, bool left, Vector3 target)
    {
        Transform upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        Transform lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (upper == null || lower == null || hand == null) return;
        Vector3 a = upper.position, b = lower.position, c = hand.position;
        float ab = (b - a).magnitude, bc = (c - b).magnitude;
        float at = Mathf.Clamp((target - a).magnitude, 0.01f, ab + bc - 0.001f);
        float shoulderNow = Vector3.Angle(c - a, b - a), elbowNow = Vector3.Angle(a - b, c - b);
        float shoulderWant = Mathf.Acos(Mathf.Clamp((bc * bc - ab * ab - at * at) / (-2f * ab * at), -1f, 1f)) * Mathf.Rad2Deg;
        float elbowWant = Mathf.Acos(Mathf.Clamp((at * at - ab * ab - bc * bc) / (-2f * ab * bc), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 axis = Vector3.Cross(c - a, b - a);
        if (axis.sqrMagnitude < 1e-6f) axis = animator.transform.right;
        axis.Normalize();
        upper.rotation = Quaternion.AngleAxis(shoulderWant - shoulderNow, axis) * upper.rotation;
        lower.rotation = Quaternion.AngleAxis(elbowWant - elbowNow, axis) * lower.rotation;
        upper.rotation = Quaternion.FromToRotation(hand.position - a, target - a) * upper.rotation;
    }

    // Плоские касательные: движение рвётся с места и тормозит в позе — резко, без «плавания».
    private static void AddKey(Dictionary<string, AnimationCurve> curves, string name, float time, float value)
    {
        if (!curves.TryGetValue(name, out var curve)) curves[name] = curve = new AnimationCurve();
        curve.AddKey(new Keyframe(time, value, 0f, 0f));
    }
}
