#if UNITY_EDITOR
using System.Linq;
using KnocksFromBeneath;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Rendering;
using System.Reflection;

public sealed class ProtagonistTests
{
    private const string Path = "Assets/Characters/Protagonist/Protagonist.prefab";

    [Test]
    public void Prefab_HasValidSkinAndBoundRig()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        Assert.That(prefab, Is.Not.Null);
        var body = prefab.GetComponent<ProtagonistBody>();
        Assert.That(body.Animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(body.Animator.applyRootMotion, Is.False);
        Assert.That(body.HeadRenderer, Is.Not.Null);
        var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>();
        Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(body.Animator.avatar.isValid && body.Animator.avatar.isHuman, Is.True);
        Assert.That(body.Animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
        Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        var heads = renderers.Where(r => r.name.Contains("Head")).ToArray();
        Assert.That(heads, Is.Not.Empty);
        Assert.That(body.HeadRenderers, Is.EquivalentTo(heads));
        foreach (var renderer in renderers)
        {
            Assert.That(renderer.bones.Length, Is.GreaterThanOrEqualTo(17));
            Assert.That(renderer.bones.All(b => b != null), Is.True);
            Assert.That(renderer.sharedMesh.bindposes.Length, Is.EqualTo(renderer.bones.Length));
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            foreach (BoneWeight weight in renderer.sharedMesh.boneWeights)
            {
                Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3,
                    Is.EqualTo(1f).Within(.0001f));
                Assert.That(weight.boneIndex0, Is.InRange(0, renderer.bones.Length - 1));
            }
        }
        Assert.That(renderers.Sum(r => r.sharedMesh.triangles.Length / 3), Is.LessThan(20000));
    }

    [Test]
    public void AnimationClips_LoopWithoutMovingTheControllerRoot()
    {
        var animator = AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<Animator>();
        var clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
        Assert.That(clips.Length, Is.EqualTo(14));
        foreach (var clip in clips)
        {
            Assert.That(clip.isLooping, Is.True, clip.name);
            Assert.That(clip.length, Is.GreaterThan(.5f));
            Assert.That(clip.humanMotion, Is.True, clip.name);
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as ModelImporter;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            Assert.That(settings.loopBlendOrientation && settings.loopBlendPositionY && settings.loopBlendPositionXZ, Is.True, clip.name);
        }
    }

    [Test]
    public void BodySkins_AreHiddenOnlyForPlayerCameraAcrossAllLODs()
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
        var cameraObject = new GameObject("View test");
        var externalObject = new GameObject("External test");
        try
        {
            var body = actor.GetComponent<ProtagonistBody>();
            var view = cameraObject.AddComponent<Camera>();
            var external = externalObject.AddComponent<Camera>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ProtagonistBody).GetMethod("Awake", flags).Invoke(body, null);
            typeof(ProtagonistBody).GetField("view", flags).SetValue(body, view);
            var before = typeof(ProtagonistBody).GetMethod("BeforeCamera", flags);
            var after = typeof(ProtagonistBody).GetMethod("AfterCamera", flags);
            before.Invoke(body, new object[] { default(ScriptableRenderContext), view });
            Assert.That(body.HeadRenderers.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), Is.True);
            Assert.That(actor.GetComponentsInChildren<SkinnedMeshRenderer>()
                .All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), Is.True);
            before.Invoke(body, new object[] { default(ScriptableRenderContext), external });
            Assert.That(body.HeadRenderers.All(r => r.shadowCastingMode == ShadowCastingMode.On), Is.True);
            before.Invoke(body, new object[] { default(ScriptableRenderContext), view });
            after.Invoke(body, new object[] { default(ScriptableRenderContext), view });
            Assert.That(body.HeadRenderers.All(r => r.shadowCastingMode == ShadowCastingMode.On), Is.True);
            before.Invoke(body, new object[] { default(ScriptableRenderContext), view });
            // Обычный MonoBehaviour не получает lifecycle callbacks при переключении enabled в Edit Mode.
            typeof(ProtagonistBody).GetMethod("OnDisable", flags).Invoke(body, null);
            Assert.That(body.HeadRenderers.All(r => r.shadowCastingMode == ShadowCastingMode.On), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(externalObject);
        }
    }

    [Test]
    public void ImportedRig_IKUsesAnatomicalSoleAndPalmAxes()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var body = prefab.GetComponent<ProtagonistBody>();
        var animator = body.Animator;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var side in new[] { "left", "right" })
        {
            var foot = animator.GetBoneTransform(side == "left" ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            var normal = (Vector3)typeof(ProtagonistBody).GetField(side + "SoleNormal", flags).GetValue(body);
            Assert.That(Vector3.Dot(foot.TransformDirection(normal).normalized, prefab.transform.up), Is.GreaterThan(.999f));
            var corrected = Quaternion.FromToRotation(foot.TransformDirection(normal), prefab.transform.up) * foot.rotation;
            Assert.That(Quaternion.Angle(corrected, foot.rotation), Is.LessThan(.1f));
        }
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal) ?? animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
        var offset = (Quaternion)typeof(ProtagonistBody).GetField("rightWristBoneOffset", flags).GetValue(body);
        Vector3 localFingers = Quaternion.Inverse(hand.rotation) * (middle.position - hand.position).normalized;
        Assert.That(Vector3.Dot(offset * localFingers, Vector3.down), Is.GreaterThan(.999f));
    }

    [Test]
    public void TwoBoneIK_ReachesTargetAndPreservesSegmentLengths()
    {
        var upper = new GameObject("Upper").transform;
        try
        {
            var lower = new GameObject("Lower").transform; lower.SetParent(upper, false); lower.localPosition = Vector3.down;
            var tip = new GameObject("Tip").transform; tip.SetParent(lower, false); tip.localPosition = Vector3.down;
            Vector3 target = new Vector3(1f, -1f, .2f);
            ProtagonistBody.SolveLimb(upper, lower, tip, target, Vector3.forward, 1f);
            Assert.That(Vector3.Distance(tip.position, target), Is.LessThan(.002f));
            Assert.That(Vector3.Distance(upper.position, lower.position), Is.EqualTo(1f).Within(.0001f));
            Assert.That(Vector3.Distance(lower.position, tip.position), Is.EqualTo(1f).Within(.0001f));
            ProtagonistBody.SolveLimb(upper, lower, tip, Vector3.up * 20f, Vector3.forward, 1f);
            Assert.That(float.IsNaN(tip.position.x), Is.False);
            Assert.That(Vector3.Distance(upper.position, tip.position), Is.LessThanOrEqualTo(2.001f));
        }
        finally { Object.DestroyImmediate(upper.gameObject); }
    }

    [Test]
    public void WalkAndCrouch_SampleDifferentHumanoidPoses()
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
        var graph = PlayableGraph.Create("Protagonist retarget test");
        try
        {
            var animator = actor.GetComponent<Animator>();
            var clips = animator.runtimeAnimatorController.animationClips;
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            graph.Play();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Sample(graph, output, clips.First(c => c.name == "Idle"), .2f);
            float standingHeight = hips.position.y;
            Quaternion standingThigh = thigh.localRotation;
            Sample(graph, output, clips.First(c => c.name == "WalkForward"), .23f);
            Assert.That(Quaternion.Angle(thigh.localRotation, standingThigh), Is.GreaterThan(5f));
            Sample(graph, output, clips.First(c => c.name == "CrouchIdle"), .2f);
            Assert.That(standingHeight - hips.position.y, Is.GreaterThan(.15f));
        }
        finally { graph.Destroy(); Object.DestroyImmediate(actor); }
    }

    private static void Sample(PlayableGraph graph, AnimationPlayableOutput output, AnimationClip clip, float time)
    {
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetTime(time);
        output.SetSourcePlayable(playable);
        graph.Evaluate(0f);
        playable.Destroy();
    }
}
#endif

