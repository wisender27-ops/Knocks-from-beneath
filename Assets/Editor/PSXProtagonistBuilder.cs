#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using KnocksFromBeneath;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PSXProtagonistBuilder
{
    const string Folder = "Assets/Characters/PSXProtagonist";
    [MenuItem("Tools/Knocks From Beneath/Protagonist/Install PSX Character 16")]
    public static void Build()
    {
        string modelPath = Folder + "/Character_16.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();
        var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + "/Character_16.png");
        textureImporter.filterMode = FilterMode.Point;
        textureImporter.mipmapEnabled = true;
        textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
        textureImporter.SaveAndReimport();
        var hero = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
        hero.name = "Protagonist";
        try
        {
            var animator = hero.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Character_16 humanoid avatar is invalid.");
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/Protagonist/Protagonist.controller");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var source = hero.GetComponentInChildren<SkinnedMeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Character_16" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Character_16.png"));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Cull", 0f);
            Save(material, Folder + "/Character_16.mat");
            source.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Character_16.mat");
            Mesh original = source.sharedMesh;
            var headSet = new HashSet<int>(); var armsSet = new HashSet<int>();
            for (int i=0;i<source.bones.Length;i++)
            {
                string n=source.bones[i].name;
                if(n.EndsWith("Head"))headSet.Add(i);
                if(n.Contains("Arm") || n.Contains("Hand"))armsSet.Add(i);
            }
            Mesh head = Extract(original, headSet, true, "PSX Head");
            Mesh body = Extract(original, headSet, false, "PSX Body");
            Mesh arms = Extract(original, armsSet, true, "PSX First Person Arms");
            Save(head,Folder+"/Head.asset"); Save(body,Folder+"/Body.asset"); Save(arms,Folder+"/FirstPersonArms.asset");
            source.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Body.asset");
            source.name="Body"; source.updateWhenOffscreen=true;
            var headObject = new GameObject("HeadMesh"); headObject.transform.SetParent(source.transform.parent,false);
            headObject.transform.localPosition=source.transform.localPosition; headObject.transform.localRotation=source.transform.localRotation; headObject.transform.localScale=source.transform.localScale;
            var headRenderer=headObject.AddComponent<SkinnedMeshRenderer>();
            headRenderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Head.asset"); headRenderer.bones=source.bones; headRenderer.rootBone=source.rootBone;
            headRenderer.sharedMaterials=source.sharedMaterials; headRenderer.localBounds=source.localBounds; headRenderer.updateWhenOffscreen=true;
            HumanBodyBones[] mapping={HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Neck,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
            var bodyComponent=hero.AddComponent<ProtagonistBody>(); bodyComponent.Configure(animator,headRenderer,mapping.Select(animator.GetBoneTransform).ToArray());
            var serialized=new SerializedObject(bodyComponent); serialized.FindProperty("firstPersonArmsMesh").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/FirstPersonArms.asset"); serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(hero,"Assets/Characters/Protagonist/Protagonist.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("PSX Character_16 installed; humanoid valid; clips="+animator.runtimeAnimatorController.animationClips.Distinct().Count()+"; bones="+source.bones.Length+"; bodyTriangles="+source.sharedMesh.triangles.Length/3+"; headTriangles="+headRenderer.sharedMesh.triangles.Length/3+"; armsTriangles="+AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/FirstPersonArms.asset").triangles.Length/3);
        }
        finally { UnityEngine.Object.DestroyImmediate(hero); }
    }
    static void Save(UnityEngine.Object asset,string path)
    {
        var previous=AssetDatabase.LoadMainAssetAtPath(path);
        if(previous==null)AssetDatabase.CreateAsset(asset,path);
        else { EditorUtility.CopySerialized(asset,previous); UnityEngine.Object.DestroyImmediate(asset); EditorUtility.SetDirty(previous); }
    }
    static Mesh Extract(Mesh source,HashSet<int> selected,bool include,string name)
    {
        Mesh mesh=UnityEngine.Object.Instantiate(source);mesh.name=name;
        var weights=source.boneWeights;var triangles=source.triangles;var result=new List<int>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            float sum=0f;
            for(int j=0;j<3;j++)
            {
                var w=weights[triangles[i+j]];
                if(selected.Contains(w.boneIndex0))sum+=w.weight0;
                if(selected.Contains(w.boneIndex1))sum+=w.weight1;
                if(selected.Contains(w.boneIndex2))sum+=w.weight2;
                if(selected.Contains(w.boneIndex3))sum+=w.weight3;
            }
            if((sum>=1.5f)==include){result.Add(triangles[i]);result.Add(triangles[i+1]);result.Add(triangles[i+2]);}
        }
        mesh.subMeshCount=1;mesh.SetTriangles(result,0);mesh.RecalculateBounds();return mesh;
    }
}
#endif

