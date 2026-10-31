#if UNITY_EDITOR
using System;
using KnocksFromBeneath;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BasementOwnerBuilder
{
    private const string Folder = "Assets/Resources/Basement";
    private const string PrefabPath = Folder + "/BasementOwner.prefab";
    private const string ControllerPath = Folder + "/BasementOwner.controller";
    private const string MaterialPath = Folder + "/BasementOwner.mat";
    private const string ShadowPath = Folder + "/BasementOwnerShadow.mat";

    [MenuItem("Tools/Knocks From Beneath/Basement/Create owner prefab")]
    public static void Build()
    {
        const string sourcePath = "Assets/Characters/Protagonist/Protagonist.prefab";
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters/SharedAnimations/CrouchIdle.anim");
        var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/PSXProtagonist/Character_16.mat");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null || idle == null || sourceMaterial == null)
            throw new InvalidOperationException("Missing PSX character, material or crouch idle animation.");
        if (AssetDatabase.LoadMainAssetAtPath(PrefabPath) != null || AssetDatabase.LoadMainAssetAtPath(ControllerPath) != null || AssetDatabase.LoadMainAssetAtPath(MaterialPath) != null || AssetDatabase.LoadMainAssetAtPath(ShadowPath) != null)
            throw new InvalidOperationException("Owner assets already exist; refusing to overwrite authored work.");

        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Basement");
        var material = new Material(sourceMaterial) { name = "Walter worn clothes" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.32f, 0.29f, 0.25f, 1f));
        AssetDatabase.CreateAsset(material, MaterialPath);
        var shadow = new Material(sourceMaterial) { name = "Walter obscured face" };
        if (shadow.HasProperty("_BaseColor")) shadow.SetColor("_BaseColor", new Color(0.025f, 0.02f, 0.018f, 1f));
        AssetDatabase.CreateAsset(shadow, ShadowPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var state = controller.layers[0].stateMachine.AddState("Crouched idle");
        state.motion = idle;
        controller.layers[0].stateMachine.defaultState = state;

        GameObject owner = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            owner.name = "Walter Hale";
            var playerBody = owner.GetComponent<ProtagonistBody>();
            if (playerBody != null) UnityEngine.Object.DestroyImmediate(playerBody);
            var animator = owner.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                throw new InvalidOperationException("The character has no humanoid Animator.");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            SkinnedMeshRenderer head = null;
            foreach (var renderer in owner.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.updateWhenOffscreen = true;
                if (renderer.name == "HeadMesh") head = renderer;
            }
            if (head == null) throw new InvalidOperationException("The PSX character head renderer is missing.");
            head.sharedMaterial = shadow;
            owner.AddComponent<BasementOwner>().Configure(head, material);
            PrefabUtility.SaveAsPrefabAsset(owner, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(owner); }
    }
}
#endif
