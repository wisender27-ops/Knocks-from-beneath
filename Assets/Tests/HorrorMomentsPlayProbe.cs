#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using UnityEngine;

[DefaultExecutionOrder(10025)]
public sealed class HorrorMomentsPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public string startedUtc, completedUtc, stage;
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }
    private readonly Report report = new Report();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private IntroSequence intro;
    private PlayerController player;
    private CharacterController capsule;
    private Camera view;
    private HorrorMomentDirector director;
    private string output;
    private bool walk;
    private Vector3 direction;

    public static void Begin()
    {
        if (!UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
        new GameObject("Horror moments verification").AddComponent<HorrorMomentsPlayProbe>();
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/horror-moments");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        capsule = player.GetComponent<CharacterController>();
        view = player.GetComponentInChildren<Camera>();
        director = intro.GetComponent<HorrorMomentDirector>();
        intro.CancelInvoke();
        intro.StopAllCoroutines();
        ClearNarrative();
        yield return null;
        yield return null;

        Stage("first-night");
        var night = (NightOneController)typeof(IntroSequence).GetField("_nightOne", Private).GetValue(intro);
        night.Run();
        night.Run();
        var scare = intro.GetComponent<NightOneScare>();
        yield return Until(() => scare.IsPlaying, 12f, "First night visible episode starts");
        Check(intro.fadeScreen == null || intro.fadeScreen.alpha == 0f, "First-night sounds are after black screen");
        AudioSource nightAudio = GameObject.Find("First night floor").GetComponent<AudioSource>();
        yield return Until(() => nightAudio.isPlaying, 8f, "First-night actual knock plays");
        float timeBeforePause = nightAudio.time;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(.35f);
        Check(!nightAudio.isPlaying && Mathf.Abs(nightAudio.time - timeBeforePause) < .08f, "Night audio pauses with game");
        Time.timeScale = 1f;
        yield return new WaitForSeconds(.8f);
        Vector3 beforeMovement = player.transform.position;
        for (int attempt = 0; attempt < 8 && Vector3.Distance(beforeMovement, player.transform.position) < .5f; attempt++)
        {
            Vector3 candidate = Quaternion.Euler(0f, attempt * 45f, 0f) * Vector3.forward;
            capsule.Move(candidate * .8f);
        }
        Check(Vector3.Distance(beforeMovement, player.transform.position) > .5f, "Night player can move freely");
        yield return Until(() => !scare.IsPlaying, 16f, "First-night episode completes");
        Check(!player.isCameraLocked && player.enabled && intro.playerInventory.enabled, "Night keeps controls and inventory available");
        yield return Until(() => QuestManager.Instance.IsQuestActive("room-decoration"), 65f, "Night advances naturally into day-two cleanup");

        Stage("kitchen");
        ClearNarrative();
        director.Interrupt(0f);
        SetPrivate(director, "quietUntil", 0f);
        foreach (GameObject zoneObject in intro.roomZones)
            if (zoneObject != null) zoneObject.GetComponent<PlacementZone>()?.RevealItemsForDebug();
        Teleport(new Vector3(-44.15f, .84f, 106.1f), new Vector3(-45.4f, 1.8f, 105.45f));
        yield return new WaitForSeconds(.5f);
        int placed = 0;
        foreach (GameObject zoneObject in intro.decorationZones)
        {
            var zone = zoneObject.GetComponent<PlacementZone>();
            foreach (Transform slot in zone.slots)
            {
                var marker = slot.GetComponent<FurnitureSlot>();
                if (marker == null || marker.ItemForSlot == null) continue;
                Teleport(new Vector3(slot.position.x + 1.25f, .84f, slot.position.z), slot.position);
                yield return new WaitForSeconds(.2f);
                var interaction = player.GetComponent<PlayerInteraction>();
                if (interaction.TryGrabObjectFromScript(marker.ItemForSlot.gameObject))
                {
                    yield return new WaitForSeconds(.4f);
                    interaction.pickup.HandleInteractPressed();
                    if (interaction.GetHeldObject() == null && !marker.ItemForSlot.GetComponent<PickableItem>().enabled) placed++;
                    else interaction.ReleaseHeldObject();
                }
                break;
            }
        }
        Teleport(new Vector3(-44.15f, .84f, 106.1f), new Vector3(-45.4f, 1.8f, 105.45f));
        Check(placed == 3, "All three real cleanup placements are accepted");
        ClearNarrative();
        yield return null;
        yield return null;
        var kitchen = intro.GetComponent<KitchenDisturbance>();
        yield return Until(() => GetPrivate<bool>(kitchen, "running"), 15f, "Kitchen event survives rapid third placement");
        var plate = GameObject.Find("Kitchen disturbed plate");
        Check(plate != null, "Existing plate mesh is staged on real countertop");
        if (plate != null)
        {
            var positionBeforePause = plate.transform.position;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.4f);
            Check(Vector3.Distance(positionBeforePause, plate.transform.position) < .001f, "Kitchen plate freezes in pause");
            Time.timeScale = 1f;
            yield return new WaitForSeconds(.45f);
            Capture("kitchen-slide");
            yield return Until(() => GetPrivate<bool>(kitchen, "settled"), 8f, "Kitchen plate falls and settles");
            Check(Vector3.Distance(positionBeforePause, plate.transform.position) > .5f, "Kitchen plate actually moves onto the floor");
            Check(plate.transform.position.y > .75f && plate.transform.position.y < .9f, "Plate rests above kitchen floor");
            Capture("kitchen-settled");
        }

        Stage("locked-hatch");
        ClearNarrative();
        SetPrivate(director, "quietUntil", 0f);
        var hatch = intro.holeEventController.basementHatch;
        QuestManager.Instance.CreateQuest("Проверить люк", 1, () => QuestManager.Instance.CreateQuest("Лом", 1, questTag:"crowbar-find"), "open-basement");
        yield return new WaitForSeconds(.1f);
        ClearNarrative();
        yield return null;
        var hatchLock = hatch.GetComponentInChildren<BasementHatchLock>(true);
        Teleport(hatchLock.StrikePoint + Vector3.right * 1.2f, hatchLock.StrikePoint);
        var leaf = GetPrivate<Transform>(hatch, "leftLeaf");
        var leafStart = leaf.localRotation;
        int callbacks = 0;
        Action complete = () => { callbacks++; if (QuestManager.Instance.IsQuestActive("open-basement")) QuestManager.Instance.AddProgress(1); };
        Check(!hatch.TryOpen(), "Locked hatch rejects hand opening");
        Check(hatch.TryPlayLockedResponse(complete), "Locked hatch response starts");
        hatch.TryPlayLockedResponse(complete);
        hatch.TryPlayLockedResponse(complete);
        yield return new WaitForSeconds(1.05f);
        Check(Quaternion.Angle(leafStart, leaf.localRotation) > .1f, "Hatch leaf flexes on interior impact");
        var frozen = leaf.localRotation;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(.35f);
        Check(Quaternion.Angle(frozen, leaf.localRotation) < .01f, "Hatch response pauses without moving leaf");
        Time.timeScale = 1f;
        yield return Until(() => !hatch.IsLockedResponsePlaying, 10f, "Hatch response completes");
        Check(callbacks == 1 && QuestManager.Instance.IsQuestActive("crowbar-find"), "Repeated E cannot duplicate quest progress");
        Check(hatch.IsLocked && !hatch.IsOpen && Quaternion.Angle(leafStart, leaf.localRotation) < .01f, "Hatch stays locked and restores exact leaf pose");
        Check(!player.isCameraLocked && intro.playerInventory.enabled, "Hatch response leaves controls available");

        Stage("following-steps");
        ClearNarrative();
        SetPrivate(director, "quietUntil", 0f);
        QuestManager.Instance.CreateQuest("Молоток", 1, questTag:"hammer-find");
        ClearNarrative();
        Teleport(new Vector3(-43.4f, .84f, 108.5f), new Vector3(-43.4f, 2.4f, 113f));
        direction = Vector3.forward;
        yield return null;
        yield return null;
        walk = true;
        var steps = intro.GetComponent<FollowingFootsteps>();
        yield return Until(() => GetPrivate<int>(steps, "beat") > 0, 8f, "Following steps play on actual travelled path");
        walk = false;
        yield return new WaitForSeconds(.3f);
        var stepAudio = GameObject.Find("Steps following player").GetComponent<AudioSource>();
        Check(!stepAudio.isPlaying && !GetPrivate<bool>(steps, "paired"), "Following steps cease when player stops");
        Check(QuestManager.Instance.IsQuestActive("hammer-find") && !player.isCameraLocked, "Following steps do not consume quest or player control");
        director.Interrupt();
        Check(!director.IsOwner(steps), "Existing story scare interrupts optional sounds");

        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Write();
    }

    private IEnumerator Until(Func<bool> condition, float timeout, string message)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(condition(), message);
    }
    private void LateUpdate()
    {
        if (walk && Time.timeScale > 0f && capsule != null && capsule.enabled)
            capsule.Move((direction * 2f + Vector3.down * .25f) * Time.deltaTime);
    }
    private static IEnumerator Empty() { yield break; }
    private void ClearNarrative() => NarrativeDisplayQueue.InterruptAndEnqueue(this, Empty);
    private void Teleport(Vector3 position, Vector3 target)
    {
        capsule.enabled = false;
        player.transform.position = position;
        Vector3 directionToTarget = target - position;
        directionToTarget.y = 0f;
        player.transform.rotation = Quaternion.LookRotation(directionToTarget);
        capsule.enabled = true;
        player.StopMotion();
        float pitch = -Mathf.Atan2(target.y - view.transform.position.y, Vector3.ProjectOnPlane(target - view.transform.position, Vector3.up).magnitude) * Mathf.Rad2Deg;
        SetPrivate(player, "yRotation", pitch);
    }
    private static FieldInfo FindField(object obj, string field)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo info = type.GetField(field, Private);
            if (info != null) return info;
        }
        throw new MissingFieldException(obj.GetType().Name, field);
    }
    private static T GetPrivate<T>(object obj, string field) => (T)FindField(obj, field).GetValue(obj);
    private static void SetPrivate(object obj, string field, object value) => FindField(obj, field).SetValue(obj, value);
    private void Check(bool passed, string message)
    {
        (passed ? report.passed : report.failed).Add(message);
        Write();
    }
    private void Stage(string stage) { report.stage = stage; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
    private void Capture(string name) => ProtagonistPlayProbe.Capture(view, Path.Combine(output, name + ".png"));
    private void OnDisable() { walk = false; Time.timeScale = 1f; }
}
#endif
