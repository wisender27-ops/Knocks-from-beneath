#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using KnocksFromBeneath;
using UnityEditor;
using UnityEngine;

[DefaultExecutionOrder(10015)]
public sealed class CarryPlayProbe : MonoBehaviour
{
    private PlayerController player;
    private PickupController pickup;
    private ProtagonistBody body;
    private Camera view;
    private float pitch;
    private bool crouch, moving;
    private string output;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Begin() => new GameObject("Carry verification").AddComponent<CarryPlayProbe>();

    private void Update()
    {
        if (player == null) return;
        player.isCameraLocked = false;
        typeof(PlayerController).GetField("yRotation", Flags).SetValue(player, pitch);
        typeof(PlayerController).GetField("isCrouching", Flags).SetValue(player, crouch);
        var capsule = player.GetComponent<CharacterController>();
        capsule.height = crouch ? player.crouchingHeight : player.standingHeight;
        capsule.center = Vector3.up * capsule.height * .5f;
        if (moving) capsule.Move(player.transform.right * Time.deltaTime);
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/carry");
        Directory.CreateDirectory(output);
        File.WriteAllText(output + "/results.txt", "Carry Play Mode verification\n");
        yield return new WaitForSeconds(1f);
        var intro = Object.FindAnyObjectByType<IntroSequence>();
        if (intro != null) { intro.StopAllCoroutines(); intro.enabled = false; }
        Time.timeScale = 1f;
        player = Object.FindAnyObjectByType<PlayerController>();
        pickup = player.GetComponent<PickupController>();
        body = player.GetComponentInChildren<ProtagonistBody>();
        view = pickup.playerCamera;
        player.enabled = pickup.enabled = true;
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(300f, 0.1f, 300f), Quaternion.identity);
        capsule.enabled = true;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(300f, -0.1f, 300f);
        floor.transform.localScale = new Vector3(40f, 0.2f, 40f);
        var light = new GameObject("Carry verification light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        yield return new WaitForSeconds(1f);
        var rightTarget = (Transform)typeof(ProtagonistBody).GetField("rightCarryTarget", Flags).GetValue(body);
        var leftTarget = (Transform)typeof(ProtagonistBody).GetField("leftCarryTarget", Flags).GetValue(body);
        var leftHand = (Transform)typeof(ProtagonistBody).GetField("leftViewHand", Flags).GetValue(body);
        var rightHand = (Transform)typeof(ProtagonistBody).GetField("rightViewHand", Flags).GetValue(body);
        var types = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder };
        var scales = new[] { new Vector3(.5f, .45f, .4f), Vector3.one * .12f, new Vector3(.25f, .35f, .25f) };
        var candidates = new List<GameObject>();
        foreach (var source in Object.FindObjectsByType<PickableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var clone = Object.Instantiate(source.gameObject);
            clone.name = source.name + " verification";
            clone.SetActive(false);
            candidates.Add(clone);
        }
        for (int i = 0; i < types.Length; i++)
        {
            var item = GameObject.CreatePrimitive(types[i]);
            item.name = "Carry verification " + types[i];
            item.transform.localScale = scales[i];
            item.AddComponent<Rigidbody>();
            if (i == 2) item.AddComponent<PickableItem>().placementQuestTag = "room-decoration";
            item.SetActive(false);
            candidates.Add(item);
        }
        var large = GameObject.CreatePrimitive(PrimitiveType.Cube);
        large.name = "Large cube"; large.transform.localScale = new Vector3(1.2f, .6f, .5f);
        large.AddComponent<Rigidbody>(); large.SetActive(false); candidates.Add(large);
        var pie = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pie.name = "Heated pie"; pie.transform.localScale = new Vector3(.3f, .08f, .3f);
        pie.AddComponent<Rigidbody>(); pie.AddComponent<PieQuestItem>().isHeated = true;
        pie.SetActive(false); candidates.Add(pie);
        foreach (var item in candidates)
        {
            item.SetActive(true);
            item.transform.position = view.transform.TransformPoint(new Vector3(0f, -.28f, .55f));
            item.transform.rotation = Quaternion.Euler(10f, 25f, 5f);
            var rb = item.GetComponent<Rigidbody>();
            Physics.SyncTransforms();
            bool grabbed = pickup.TryGrab(item);
            foreach (float angle in new[] { 0f, 40f, -40f })
            {
                pitch = angle;
                crouch = angle < 0f; moving = angle > 0f;
                yield return new WaitForSeconds(.7f);
                float rightError = 0f, leftError = 0f;
                for (int sample = 0; sample < 12; sample++)
                {
                    yield return new WaitForEndOfFrame();
                    rightError = Mathf.Max(rightError, Vector3.Distance(rightHand.position, rightTarget.position));
                    leftError = Mathf.Max(leftError, Vector3.Distance(leftHand.position, leftTarget.position));
                }
                File.AppendAllText(output + "/results.txt", item.name + " pitch=" + angle + " grabbed=" + grabbed +
                    " held=" + (pickup.GetHeldObject() == item) + " parent=" + (item.transform.parent != null) +
                    " rightError=" + rightError + " leftError=" + leftError + "\n");
                if (angle == 0f)
                {
                    ProtagonistPlayProbe.Capture(view, output + "/" + item.name + ".png");
                }
            }
            pickup.Release();
            File.AppendAllText(output + "/results.txt", "release=" + (pickup.GetHeldObject() == null && !rb.isKinematic && item.GetComponent<Collider>().enabled) + "\n");
            Object.Destroy(item);
            pitch = 0f;
            crouch = moving = false;
        }
        File.AppendAllText(output + "/results.txt", "Finished\n");
        EditorApplication.isPlaying = false;
    }
}
#endif
