#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(10010)]
public sealed class ProtagonistPlayProbe : MonoBehaviour
{
    private PlayerController player;
    private CharacterController capsule;
    private PlayerInventory inventory;
    private ProtagonistBody body;
    private Camera view, overview;
    private float started;
    private int stage = -1;
    private readonly string[] names = { "idle", "walk", "run", "crouch", "flashlight" };
    private readonly FieldInfo crouchField = typeof(PlayerController).GetField("isCrouching", BindingFlags.NonPublic | BindingFlags.Instance);
    private readonly FieldInfo lookField = typeof(PlayerController).GetField("yRotation", BindingFlags.NonPublic | BindingFlags.Instance);
    private string output;
    private bool headHidden;

    public static void Begin()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        new GameObject("Protagonist Verification (temporary)").AddComponent<ProtagonistPlayProbe>();
    }

    private void Start()
    {
        output = Path.GetFullPath(".codex/verification/hero");
        Directory.CreateDirectory(output);
        player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        capsule = player.GetComponent<CharacterController>();
        inventory = player.GetComponent<PlayerInventory>();
        body = player.GetComponentInChildren<ProtagonistBody>();
        view = player.GetComponentInChildren<Camera>();
        capsule.enabled = false; player.transform.SetPositionAndRotation(new Vector3(300f, .06f, 300f), Quaternion.identity); capsule.enabled = true;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Verification Floor"; floor.transform.position = new Vector3(300f, -.1f, 300f);
        floor.transform.localScale = new Vector3(40f, .2f, 40f);
        var key = new GameObject("Verification Key").AddComponent<Light>(); key.type = LightType.Directional;
        key.intensity = 2f; key.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
        overview = new GameObject("Verification Camera").AddComponent<Camera>();
        overview.enabled = false; overview.clearFlags = CameraClearFlags.SolidColor;
        overview.backgroundColor = new Color(.08f, .08f, .08f); overview.fieldOfView = 35f;
        started = Time.time;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += ObserveHead;
        File.WriteAllText(output + "/playmode.txt", "Runtime verification started\n");
    }

    private void ObserveHead(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if (camera == view && body != null && body.HeadRenderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
            headHidden = true;
    }

    private void Update()
    {
        if (player == null || Time.timeScale <= 0f) return;
        float elapsed = Time.time - started;
        int current = Mathf.Min(4, Mathf.FloorToInt(elapsed / 2f));
        player.isCameraLocked = false;
        float speed = current == 1 ? player.walkSpeed : current == 2 ? player.runSpeed : current == 3 ? player.crouchSpeed : 0f;
        crouchField.SetValue(player, current == 3);
        if (current == 3) { capsule.height = player.crouchingHeight; capsule.center = Vector3.up * capsule.height * .5f; }
        lookField.SetValue(player, current == 4 ? 25f : 65f);
        capsule.Move((Vector3.forward * speed + Vector3.down * .2f) * Time.deltaTime);
        if (current == 4)
        {
            inventory.hasFlashlight = true;
            inventory.ActivateItem("Flashlight");
        }
        if (current > stage && elapsed % 2f > 1.3f)
        {
            stage = current;
            EditorApplication.delayCall += CaptureStage;
        }
        if (elapsed > 10f)
        {
            File.AppendAllText(output + "/playmode.txt", "First person head hidden=" + headHidden + "\nFinished\n");
            UnityEngine.Object.Destroy(gameObject);
        }
    }

    private void CaptureStage()
    {
        if (this == null || body == null) return;
        overview.transform.position = player.transform.position + new Vector3(2.5f, 1.6f, 3.2f);
        overview.transform.LookAt(player.transform.position + Vector3.up * .9f);
        Capture(overview, output + "/runtime-" + names[stage] + ".png");
        if (stage == 0 || stage == 4) Capture(view, output + "/fps-" + names[stage] + ".png");
        File.AppendAllText(output + "/playmode.txt", names[stage] + " MoveZ=" + body.Animator.GetFloat("MoveZ")
            + " Crouch=" + body.Animator.GetFloat("Crouch") + " grounded=" + capsule.isGrounded + "\n");
        if (stage == 4)
        {
            var hand = Array.Find(body.GetComponentsInChildren<Transform>(), t => t.name == "RightHand");
            File.AppendAllText(output + "/playmode.txt", "Flashlight hand distance=" + Vector3.Distance(hand.position, inventory.flashlightInHand.transform.position) + "\n");
        }
    }

    public static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(960, 720, 24);
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        var texture = new Texture2D(960, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private void OnDestroy()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= ObserveHead;
        EditorApplication.delayCall -= CaptureStage;
    }
}
#endif
