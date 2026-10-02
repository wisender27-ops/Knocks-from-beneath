#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KnocksFromBeneath;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReferenceSurvivorIntegration
{
    public const string PrefabPath = "Assets/Characters/Protagonist/Protagonist.prefab";
    public const string SourcePath = "Assets/Characters/ReferenceSurvivor/ReferenceSurvivor.prefab";
    public const string AnimationFolder = "Assets/Characters/ReferenceSurvivor/Animations/Mixamo/";
    public static readonly string[] ClipNames = { "Idle", "WalkForward", "WalkBackward", "StrafeLeft", "StrafeRight",
        "RunForward", "RunBackward", "RunLeft", "RunRight", "CrouchIdle", "CrouchForward", "CrouchBackward", "CrouchLeft", "CrouchRight" };
    private static readonly HumanBodyBones[] BodyBones = { HumanBodyBones.Hips, HumanBodyBones.Spine,
        HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg,
        HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg,
        HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };

    public static string BuildSourcePrefab()
    {
        const string folder = "Assets/Characters/ReferenceSurvivor/";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Models/CH_ReferenceSurvivor.fbx");
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "Materials/Survivor.mat");
        if (model == null || material == null) throw new InvalidOperationException("Survivor model or material missing.");
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "ReferenceSurvivor";
            var bones = root.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var levels = new LOD[3];
            var baseSkins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            levels[0] = new LOD(.5f, baseSkins);
            for (int level = 1; level <= 2; level++)
            {
                var lodModel = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Models/CH_ReferenceSurvivor_LOD" + level + ".fbx");
                if (lodModel == null) throw new InvalidOperationException("Missing survivor LOD " + level);
                var lodSkins = new List<Renderer>();
                foreach (var sourceSkin in lodModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var skin = UnityEngine.Object.Instantiate(sourceSkin.gameObject, root.transform, false).GetComponent<SkinnedMeshRenderer>();
                    skin.name = sourceSkin.name + "_LOD" + level;
                    skin.bones = sourceSkin.bones.Select(b => bones[b.name]).ToArray();
                    skin.rootBone = bones[sourceSkin.rootBone.name];
                    lodSkins.Add(skin);
                }
                levels[level] = new LOD(level == 1 ? .2f : .05f, lodSkins.ToArray());
            }
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMesh.subMeshCount).ToArray();
                skin.updateWhenOffscreen = true;
            }
            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();
            group.SetLODs(levels);
            group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, SourcePath);
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
        }
        return SourcePath;
    }

    public static void ImportAnimations()
    {
        foreach (string name in ClipNames)
        {
            string path = AnimationFolder + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing Mixamo source: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clips = importer.defaultClipAnimations;
            if (clips.Length != 1) throw new InvalidOperationException("Expected one source take: " + path);
            var clip = clips[0];
            clip.name = name;
            clip.loopTime = true;
            clip.loopPose = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }
    }

    public static string Install()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        if (source == null) throw new InvalidOperationException("Survivor prefab missing.");
        var sourceAnimator = source.GetComponent<Animator>();
        if (sourceAnimator == null || sourceAnimator.avatar == null || !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman)
            throw new InvalidOperationException("Survivor requires a valid Humanoid avatar.");
        var heads = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(IsHead).ToArray();
        if (heads.Length == 0) throw new InvalidOperationException("Split head skins must contain Head in their object names.");
        var clips = ClipNames.ToDictionary(n => n, n => LoadClip(n));
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Characters/Protagonist/Protagonist.controller");
        if (controller == null) throw new InvalidOperationException("Existing protagonist controller missing.");
        var leaves = new List<ChildMotion>();
        foreach (var layer in controller.layers) CollectLeaves(layer.stateMachine, leaves);
        foreach (string name in ClipNames)
            if (!leaves.Any(c => c.motion != null && c.motion.name == name))
                throw new InvalidOperationException("Existing controller leaf missing: " + name);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var body = root.GetComponent<ProtagonistBody>();
            var animator = root.GetComponent<Animator>();
            if (body == null || animator == null) throw new InvalidOperationException("Existing protagonist components missing.");
            // Keep the existing root and component fileIDs while replacing only the visual hierarchy.
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.scene);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                foreach (Transform child in visual.transform.Cast<Transform>().ToArray()) child.SetParent(root.transform, false);
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                var sourceLOD = visual.GetComponent<LODGroup>();
                var targetLOD = root.GetComponent<LODGroup>();
                if (targetLOD == null) targetLOD = root.AddComponent<LODGroup>();
                if (sourceLOD != null) EditorUtility.CopySerialized(sourceLOD, targetLOD);
                animator.avatar = sourceAnimator.avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                var bones = BodyBones.Select(b => animator.GetBoneTransform(b)).ToArray();
                if (bones.Any(b => b == null)) throw new InvalidOperationException("Required Humanoid IK bone missing.");
                heads = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(IsHead).ToArray();
                body.Configure(animator, heads[0], bones);
                body.ConfigureHeadRenderers(heads);
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                foreach (var layer in controller.layers) ReplaceMotions(layer.stateMachine, clips);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(visual); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return PrefabPath;
    }

    private static bool IsHead(SkinnedMeshRenderer skin) => skin.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0;
    private static AnimationClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(AnimationFolder + name + ".fbx").OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (clip == null || !clip.humanMotion || !clip.isLooping) throw new InvalidOperationException("Missing looping Humanoid clip: " + name);
        return clip;
    }
    private static void CollectLeaves(AnimatorStateMachine machine, List<ChildMotion> leaves)
    {
        foreach (var state in machine.states) CollectLeaves(state.state.motion, leaves);
        foreach (var child in machine.stateMachines) CollectLeaves(child.stateMachine, leaves);
    }
    private static void CollectLeaves(Motion motion, List<ChildMotion> leaves)
    {
        var tree = motion as BlendTree;
        if (tree == null) return;
        foreach (var child in tree.children)
            if (child.motion is BlendTree) CollectLeaves(child.motion, leaves); else leaves.Add(child);
    }
    private static void ReplaceMotions(AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
    {
        foreach (var state in machine.states) ReplaceMotions(state.state.motion as BlendTree, clips);
        foreach (var child in machine.stateMachines) ReplaceMotions(child.stateMachine, clips);
    }
    private static void ReplaceMotions(BlendTree tree, Dictionary<string, AnimationClip> clips)
    {
        if (tree == null) return;
        var children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            var nested = children[i].motion as BlendTree;
            if (nested != null) ReplaceMotions(nested, clips);
            else if (children[i].motion != null && clips.TryGetValue(children[i].motion.name, out var clip)) children[i].motion = clip;
        }
        tree.children = children;
        EditorUtility.SetDirty(tree);
    }
}
#endif
