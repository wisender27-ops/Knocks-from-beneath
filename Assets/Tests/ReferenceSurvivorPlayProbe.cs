#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnocksFromBeneath;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public sealed class ReferenceSurvivorPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public string scene;
        public bool complete, passed;
        public float rootDrift, rootRotationDrift, crouchHipDrop;
        public List<string> checks = new List<string>();
        public List<string> failures = new List<string>();
        public string interactionScope = "Clip retargeting and real scene camera verification; live movement, carry, kick and basement recovery require separate checks.";
    }
    private readonly Report report = new Report();
    private PlayerController player;
    private ProtagonistBody body;
    private Animator animator;
    private Camera view, overview;
    private LODGroup lod;
    private CharacterController capsule;
    private Rigidbody rigidbody;
    private bool capsuleEnabled, wasKinematic;
    private Vector3 savedVelocity, savedAngularVelocity;
    private PlayableGraph graph;
    private Vector3 rootPosition;
    private Quaternion rootRotation;
    private bool playerEnabled, bodyEnabled, animatorEnabled, restored, savedStates;
    private int forcedLOD;
    private string folder;
    private bool sawHidden;

    public static void Begin()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        if (UnityEngine.Object.FindFirstObjectByType<ReferenceSurvivorPlayProbe>() != null)
            throw new InvalidOperationException("Survivor probe already running.");
        new GameObject("Reference Survivor Verification (temporary)").AddComponent<ReferenceSurvivorPlayProbe>();
    }

    private IEnumerator Start()
    {
        yield return null;
        folder = Path.GetFullPath(".codex/verification/reference-survivor/runtime");
        Directory.CreateDirectory(folder);
        try { Prepare(); }
        catch (Exception e) { Fail(e.ToString()); }
        if (report.failures.Count > 0) { Finish(); yield break; }
        var clips = animator.runtimeAnimatorController.animationClips.Distinct().OrderBy(c => c.name).ToArray();
        Check(clips.Length == 14, "14 humanoid clips assigned");
        float standing = float.NaN, crouching = float.NaN;
        foreach (var clip in clips)
        {
            try
            {
                Sample(clip, .2f);
                Check(clip.humanMotion && clip.isLooping, clip.name + " looping Humanoid");
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (clip.name == "Idle") standing = hips.position.y;
                if (clip.name == "CrouchIdle") crouching = hips.position.y;
                for (int i = 0; i <= 12; i++)
                {
                    Sample(clip, clip.length * i / 12f);
                    Check(animator.GetComponentsInChildren<Transform>().All(t => Finite(t.position) && Finite(t.rotation)), clip.name + " finite pose " + i);
                }
                Sample(clip, .2f);
                ProtagonistPlayProbe.Capture(overview, folder + "/" + clip.name + ".png");
                Vector3 previousCameraPosition = overview.transform.position;
                Quaternion previousCameraRotation = overview.transform.rotation;
                overview.transform.position = rootPosition + player.transform.TransformDirection(new Vector3(0f, 1f, 2f));
                overview.transform.LookAt(rootPosition + Vector3.up * .85f);
                ProtagonistPlayProbe.Capture(overview, folder + "/front-" + clip.name + ".png");
                overview.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation);
            }
            catch (Exception e) { Fail(clip.name + ": " + e.Message); }
            yield return null;
        }
        report.crouchHipDrop = standing - crouching;
        Check(!float.IsNaN(report.crouchHipDrop) && report.crouchHipDrop > .15f, "Crouch lowers hips >15cm");
        var idle = clips.FirstOrDefault(c => c.name == "Idle");
        if (idle != null) Sample(idle, .2f);
        body.enabled = true;
        RenderPipelineManager.beginCameraRendering += ObserveHead;
        for (int level = 0; level < 3; level++)
        {
            try
            {
                lod.ForceLOD(level);
                sawHidden = false;
                ProtagonistPlayProbe.Capture(view, folder + "/first-person-LOD" + level + ".png");
                Check(sawHidden, "Player camera hides all head skins LOD" + level);
                ProtagonistPlayProbe.Capture(overview, folder + "/external-LOD" + level + ".png");
                Check(body.HeadRenderers.All(h => h.shadowCastingMode == ShadowCastingMode.On), "External camera restores heads LOD" + level);
            }
            catch (Exception e) { Fail("LOD" + level + ": " + e.Message); }
            yield return null;
        }
        report.rootDrift = Vector3.Distance(rootPosition, player.transform.position);
        report.rootRotationDrift = Quaternion.Angle(rootRotation, player.transform.rotation);
        Check(report.rootDrift < .001f && report.rootRotationDrift < .01f, "Player root stationary throughout sampling");
        Finish();
    }

    private void Prepare()
    {
        player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null) throw new InvalidOperationException("Actual scene player missing.");
        body = player.GetComponentInChildren<ProtagonistBody>();
        animator = body.Animator;
        view = player.GetComponentInChildren<Camera>();
        lod = body.GetComponent<LODGroup>();
        if (view == null || lod == null) throw new InvalidOperationException("Player camera or LOD group missing.");
        report.scene = player.gameObject.scene.path;
        rootPosition = player.transform.position; rootRotation = player.transform.rotation;
        playerEnabled = player.enabled; bodyEnabled = body.enabled; animatorEnabled = animator.enabled;
        savedStates = true;
        capsule = player.GetComponent<CharacterController>();
        capsuleEnabled = capsule != null && capsule.enabled;
        rigidbody = player.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            wasKinematic = rigidbody.isKinematic;
            savedVelocity = rigidbody.linearVelocity; savedAngularVelocity = rigidbody.angularVelocity;
            rigidbody.isKinematic = true;
        }
        if (capsule != null) capsule.enabled = false;
        // ForceLOD has no readable public getter; integration defaults to automatic selection.
        forcedLOD = -1;
        player.enabled = false; body.enabled = false; animator.enabled = true;
        overview = new GameObject("Survivor Verification Camera").AddComponent<Camera>();
        overview.enabled = false; overview.fieldOfView = 40f;
        overview.transform.position = rootPosition + player.transform.TransformDirection(new Vector3(2f, 1.4f, 2.8f));
        overview.transform.LookAt(rootPosition + Vector3.up * .9f);
        graph = PlayableGraph.Create("Reference Survivor runtime retarget verification");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        AnimationPlayableOutput.Create(graph, "Pose", animator);
        graph.Play();
    }

    private void Sample(AnimationClip clip, float time)
    {
        var output = (AnimationPlayableOutput)graph.GetOutput(0);
        var old = output.GetSourcePlayable();
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetTime(time); output.SetSourcePlayable(playable);
        graph.Evaluate(0f);
        if (old.IsValid()) graph.DestroyPlayable(old);
    }
    private void ObserveHead(ScriptableRenderContext context, Camera camera)
    {
        if (camera == view && body.HeadRenderers.Length == 3 && body.HeadRenderers.All(h => h.shadowCastingMode == ShadowCastingMode.ShadowsOnly)) sawHidden = true;
    }
    private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    private static bool Finite(Quaternion q) => Finite(new Vector3(q.x, q.y, q.z)) && !float.IsNaN(q.w) && !float.IsInfinity(q.w);
    private void Check(bool condition, string check) { if (condition) report.checks.Add(check); else Fail(check); }
    private void Fail(string failure) { report.failures.Add(failure); Debug.LogError("SURVIVOR_RUNTIME_FAIL: " + failure); }
    private void Finish()
    {
        Restore(); report.complete = true; report.passed = report.failures.Count == 0;
        File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(report, true));
        Debug.Log("SURVIVOR_RUNTIME_COMPLETE passed=" + report.passed);
        Destroy(gameObject);
    }
    private void Restore()
    {
        if (restored) return; restored = true;
        RenderPipelineManager.beginCameraRendering -= ObserveHead;
        if (graph.IsValid()) graph.Destroy();
        if (savedStates)
        {
            if (lod != null) lod.ForceLOD(forcedLOD);
            if (animator != null) { animator.Rebind(); animator.Update(0f); animator.enabled = animatorEnabled; }
            if (body != null) body.enabled = bodyEnabled;
            if (player != null) player.transform.SetPositionAndRotation(rootPosition, rootRotation);
            if (rigidbody != null)
            {
                rigidbody.position = rootPosition; rigidbody.rotation = rootRotation;
                rigidbody.isKinematic = wasKinematic;
                if (!wasKinematic) { rigidbody.linearVelocity = savedVelocity; rigidbody.angularVelocity = savedAngularVelocity; }
            }
            if (capsule != null) capsule.enabled = capsuleEnabled;
            if (player != null) player.enabled = playerEnabled;
        }
        if (overview != null) Destroy(overview.gameObject);
    }
    private void OnDestroy() { Restore(); }
}
#endif
