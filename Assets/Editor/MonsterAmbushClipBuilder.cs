using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Two non-looping humanoid performances authored for the B-03 attacks.
public static class MonsterAmbushClipBuilder
{
    private const string Folder = "Assets/Resources/Animations/Monster";

    [MenuItem("KFB/B-03/Build Ambush Animations")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Animations"))
            AssetDatabase.CreateFolder("Assets/Resources", "Animations");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources/Animations", "Monster");
        Create("AmbushBehind", true);
        Create("AmbushFront", false);
    }

    private static void Create(string name, bool behind)
    {
        string path = Folder + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool created = clip == null;
        if (created) clip = new AnimationClip { name = name };
        clip.ClearCurves();

        // Humanoid muscles use the same standing baseline as the existing custom peek clip.
        var pose = new Dictionary<string, float>
        {
            ["Spine Front-Back"] = 0.1f, ["Chest Front-Back"] = 0.1f,
            ["UpperChest Front-Back"] = 0.1f, ["Neck Nod Down-Up"] = 0.1f,
            ["Left Upper Leg Front-Back"] = 0.6f, ["Right Upper Leg Front-Back"] = 0.6f,
            ["Left Upper Leg In-Out"] = 0.1f, ["Right Upper Leg In-Out"] = 0.1f,
            ["Left Lower Leg Stretch"] = 1f, ["Right Lower Leg Stretch"] = 1f,
            ["Left Arm Down-Up"] = -0.6f, ["Right Arm Down-Up"] = -0.6f,
            ["Left Arm Front-Back"] = 0.1f, ["Right Arm Front-Back"] = 0.1f,
            ["Left Forearm Stretch"] = 0.9f, ["Right Forearm Stretch"] = 0.9f
        };
        float[] times = behind ? new[] { 0f, 0.14f, 0.32f, 0.65f, 1.05f }
                               : new[] { 0f, 0.16f, 0.36f, 0.7f, 1.05f };
        for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
        {
            string property = HumanTrait.MuscleName[muscle];
            float baseline = pose.TryGetValue(property, out float value) ? value : 0f;
            var curve = new AnimationCurve();
            for (int i = 0; i < times.Length; i++)
                curve.AddKey(new Keyframe(times[i], AttackValue(property, baseline, behind, i), 0f, 0f));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), curve);
        }
        foreach (var root in new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" })
        {
            float value = root == "RootT.y" || root == "RootQ.w" ? 1f : 0f;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), root),
                AnimationCurve.Constant(0f, times[times.Length - 1], value));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        if (created) AssetDatabase.CreateAsset(clip, path);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
    }

    private static float AttackValue(string muscle, float baseline, bool behind, int key)
    {
        float reach = key == 0 ? 0f : key == 1 ? 0.4f : key == 2 ? 1f : key == 3 ? 0.85f : 0.95f;
        switch (muscle)
        {
            case "Spine Front-Back": return Mathf.Lerp(baseline, behind ? 0.65f : 0.4f, reach);
            case "Chest Front-Back": return Mathf.Lerp(baseline, behind ? 0.55f : 0.35f, reach);
            case "UpperChest Left-Right": return (behind ? -0.3f : 0.2f) * reach;
            case "Head Tilt Left-Right": return (behind ? 0.4f : -0.35f) * reach;
            case "Left Arm Down-Up": return Mathf.Lerp(baseline, behind ? 0.8f : 0.55f, reach);
            case "Right Arm Down-Up": return Mathf.Lerp(baseline, behind ? 0.6f : 0.85f, reach);
            case "Left Arm Front-Back": return Mathf.Lerp(baseline, behind ? -0.8f : -0.55f, reach);
            case "Right Arm Front-Back": return Mathf.Lerp(baseline, behind ? -0.55f : -0.85f, reach);
            case "Left Forearm Stretch": return Mathf.Lerp(baseline, 0.45f, reach);
            case "Right Forearm Stretch": return Mathf.Lerp(baseline, 0.35f, reach);
            case "Left Upper Leg Front-Back": return Mathf.Lerp(baseline, 0.3f, reach);
            case "Right Upper Leg Front-Back": return Mathf.Lerp(baseline, 0.45f, reach);
            default: return baseline;
        }
    }
}
