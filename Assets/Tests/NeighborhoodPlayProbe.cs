#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KnocksFromBeneath;
using UnityEngine;

public sealed class NeighborhoodPlayProbe : MonoBehaviour
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
    private Camera view;
    private NarrativeReadingPanel panel;
    private bool previousAuto;
    private string output;
    public static void Begin() => new GameObject("Neighborhood verification").AddComponent<NeighborhoodPlayProbe>();
    private IEnumerator Start()
    {
        output = Path.GetFullPath(".codex/verification/neighborhood");
        Directory.CreateDirectory(output);
        report.startedUtc = DateTime.UtcNow.ToString("O");
        intro = FindFirstObjectByType<IntroSequence>();
        player = intro.playerTransform.GetComponent<PlayerController>();
        view = player.GetComponentInChildren<Camera>();
        panel = ThoughtManager.Instance.GetComponent<NarrativeReadingPanel>();
        previousAuto = Get<bool>(panel, "autoAdvance");
        Set(panel, "autoAdvance", true);
        intro.CancelInvoke();
        intro.StopAllCoroutines();
        // Проба стартует сразу после входа в Play Mode — посреди катсцены приезда. Её OnDisable
        // возвращает камеру игроку, иначе телепорты пробы снимают улицу, а не дом.
        ResumeFocusPause();
        if (intro.openingCutscene != null) intro.openingCutscene.gameObject.SetActive(false);
        foreach (var screen in FindObjectsByType<StoryIntroScreen>(FindObjectsSortMode.None)) Destroy(screen.gameObject);
        // Катсцена возвращает блокировку камеры, снять которую должен был остановленный IntroSequence.
        player.isCameraLocked = false;
        NarrativeDisplayQueue.InterruptAndEnqueue(this, Empty);
        yield return null;
        Stage("first-night");
        Check(intro.neighborVisit != null && intro.neighborVisit.IsConfigured, "Neighbor is fully wired in saved scene");
        var night = Get<NightOneController>(intro, "_nightOne");
        night.Run();
        night.Run();
        yield return Until(() => intro.GetComponent<NightOneScare>().IsPlaying, 12f, "Existing floor knock episode begins");
        yield return Until(() => intro.neighborVisit.IsPlaying, 25f, "Neighbor follows the existing floor knocks");
        yield return Until(() => QuestManager.Instance.IsQuestActive("neighbor-visit"), 8f, "Door visitor objective is created");
        var neighbor = Get<GameObject>(intro.neighborVisit, "neighbor");
        Check(neighbor != null && neighbor.GetComponentInChildren<Animator>().avatar.isValid, "Downloaded neighbor has a valid humanoid rig");
        Check(!player.isCameraLocked && player.enabled, "Player controls remain available while finding visitor");
        intro.StartCoroutine(intro.neighborVisit.Play());
        yield return null;
        Check(FindObjectsByType<Animator>(FindObjectsSortMode.None).Length > 0 && Get<GameObject>(intro.neighborVisit, "neighbor") == neighbor, "Repeated visit does not replace or duplicate neighbor");
        Stage("closed-door");
        Teleport(new Vector3(-39.25f, .84f, 115.65f), new Vector3(-39.25f, 2.1f, 118.35f));
        yield return new WaitForSeconds(1f);
        Check(QuestManager.Instance.IsQuestActive("neighbor-visit") && !NarrativeReadingPanel.IsModal, "Closed door prevents conversation");
        Time.timeScale = 0f;
        var before = neighbor.transform.position;
        yield return new WaitForSecondsRealtime(.4f);
        Check(neighbor.transform.position == before && QuestManager.Instance.IsQuestActive("neighbor-visit"), "Pause keeps visitor and objective stable");
        Time.timeScale = 1f;
        Stage("silhouette");
        var visit = intro.neighborVisit;
        var porch = visit.porchLightSwitch;
        var doorInput = player.GetComponentInChildren<PlayerDoorOpen>();
        var interaction = player.GetComponent<PlayerInteraction>();
        RaycastHit doorHit;
        Check(porch != null && !porch.isOn && visit.IsSilhouetteVisible, "Porch light stays off and the visitor is a silhouette");
        Check(!intro.frontDoor.IsInteractionLocked && QuestManager.Instance.IsQuestActive("neighbor-visit"), "Silhouette adds no lock or objective");
        Teleport(new Vector3(-42.4f, .84f, 117.4f), neighbor.transform.position + Vector3.up * 1.8f);
        yield return new WaitForSeconds(.5f);
        Capture("window-silhouette");
        Stage("porch-light");
        porch.ToggleLight();
        yield return null;
        Check(porch.isOn && !visit.IsSilhouetteVisible && !visit.IsAtDoor, "Porch light shows a man at the window in the same frame");
        yield return new WaitForSeconds(.4f);
        Capture("neighbor-revealed");
        yield return Until(() => visit.IsAtDoor && (neighbor.transform.position - visit.visitPoint.position).sqrMagnitude < .0025f, 12f, "Revealed neighbor walks from the window to the door");
        Teleport(new Vector3(-39.25f, .84f, 115.65f), new Vector3(-39.25f, 2.1f, 118.35f));
        yield return new WaitForSeconds(.3f);
        Stage("door-interaction");
        bool focused = interaction.TryGetInteractionHit(out doorHit) && doorHit.collider.GetComponentInParent<Door>() == intro.frontDoor;
        Check(focused, "Actual player ray focuses the entrance door");
        if (focused)
        {
            Check(doorInput.BeginDoorHold(intro.frontDoor, doorHit.point), "Existing player door interaction accepts entrance");
            doorInput.EndDoorHold(true);
        }
        yield return Until(() => NarrativeReadingPanel.IsModal, 8f, "Opening entrance starts neighbor dialogue");
        Capture("neighbor-at-door");
        Check(!QuestManager.Instance.IsQuestActive("neighbor-visit") && !QuestManager.Instance.IsQuestActive("neighbor-open-door"), "Visitor objective completes once conversation starts");
        Stage("conversation");
        yield return Until(() => neighbor != null && neighbor.GetComponentInChildren<Animator>().GetBool("Walking"), 65f, "Dialogue completes and walking animation begins");
        Check(!player.isCameraLocked && Time.timeScale > 0f && interaction.enabled, "Dialogue restores movement and interaction");
        Capture("neighbor-leaving");
        if (neighbor != null)
        {
            before = neighbor.transform.position;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.4f);
            Check(neighbor.transform.position == before, "Departure pauses with the game");
            Time.timeScale = 1f;
            yield return new WaitForSeconds(.5f);
            Check(neighbor.transform.position != before, "Walking neighbor travels along configured route");
        }
        yield return Until(() => QuestManager.Instance.IsQuestActive("neighbor-return-bed"), 40f, "Departure creates return-to-bed objective");
        Check(Get<GameObject>(intro.neighborVisit, "neighbor") == null && intro.neighborVisit.HasVisited, "Neighbor departs and records the meeting");
        var beds = FindObjectsByType<BedSleepInteractable>(FindObjectsSortMode.None);
        BedSleepInteractable nearest = null;
        float closest = float.MaxValue;
        foreach (var bed in beds)
        {
            float distance = (bed.transform.position - intro.nightTwoBedSpawnPosition).sqrMagnitude;
            if (distance < closest) { closest = distance; nearest = bed; }
        }
        Check(nearest != null && nearest.CanInteract, "Existing bed is interactive for return quest");
        Teleport(intro.nightTwoBedSpawnPosition, nearest != null ? nearest.transform.position : intro.nightTwoBedSpawnPosition + Vector3.forward);
        if (nearest != null) nearest.Interact();
        Stage("morning");
        yield return Until(() => QuestManager.Instance.IsQuestActive("room-decoration"), 35f, "Real bed interaction continues into day-two cleanup");
        Check(intro.skySwitcher.isDayTime && !intro.neighborVisit.IsPlaying, "Day returns and visitor sequence releases state");
        Check(!player.isCameraLocked && interaction.enabled && player.enabled && Time.timeScale > 0f, "Morning controls and time are restored");
        Check(GameObject.Find("Neighbor door knocks") == null, "Visit audio emitter is cleaned up");
        Stage("interruption");
        var temporary = new GameObject("Interrupted visitor").AddComponent<NeighborVisitSequence>();
        temporary.neighborPrefab = intro.neighborVisit.neighborPrefab;
        temporary.visitPoint = intro.neighborVisit.visitPoint;
        temporary.frontDoor = intro.frontDoor;
        temporary.player = intro.playerTransform;
        temporary.porchLightSwitch = intro.neighborVisit.porchLightSwitch;
        temporary.knockClip = intro.neighborVisit.knockClip;
        temporary.windowPeekPoint = intro.neighborVisit.windowPeekPoint;
        temporary.windowReturnPath = intro.neighborVisit.windowReturnPath;
        bool lockBefore = intro.frontDoor.IsInteractionLocked;
        intro.frontDoor.SetInteractionLocked(true);
        bool lightBefore = temporary.porchLightSwitch != null && temporary.porchLightSwitch.isOn;
        // Визит начинается при закрытой двери (ночь закрывает её); ждём, пока она докроется.
        yield return new WaitForSeconds(2f);
        StartCoroutine(temporary.Play());
        yield return new WaitForSeconds(.1f);
        Check(temporary.IsPlaying && temporary.IsSilhouetteVisible && temporary.porchLightSwitch != null && !temporary.porchLightSwitch.isOn, "Interrupted visitor starts as a silhouette with the porch dark");
        // Дверь, открытая в темноте, показывает человека у двери, а не силуэт у окна.
        intro.frontDoor.OpenDoor();
        yield return null;
        var dark = Get<GameObject>(temporary, "neighbor");
        Check(!temporary.IsSilhouetteVisible && temporary.IsAtDoor && dark != null && (dark.transform.position - temporary.visitPoint.position).sqrMagnitude < .0001f, "Opening the door in the dark always finds a man at the door");
        temporary.enabled = false;
        yield return new WaitForSeconds(1.5f);
        Check(!temporary.IsPlaying && intro.frontDoor.IsInteractionLocked, "Interrupted visit restores captured door lock");
        Check(temporary.porchLightSwitch == null || temporary.porchLightSwitch.isOn == lightBefore, "Interrupted visit restores the existing porch light");
        Check(Get<GameObject>(temporary, "neighbor") == null && GameObject.Find("Neighbor door knocks") == null && GameObject.Find("Neighbor silhouette backlight") == null, "Interrupted visit removes actor, audio and backlight");
        Check(QuestManager.Instance.IsQuestActive("room-decoration") && !NarrativeReadingPanel.IsModal, "Interrupted visit does not enqueue dialogue or replace current quest");
        intro.frontDoor.SetInteractionLocked(lockBefore);
        Destroy(temporary.gameObject);
        Stage("complete");
        report.completedUtc = DateTime.UtcNow.ToString("O");
        Set(panel, "autoAdvance", previousAuto);
        Write();
    }
    // Редактор, которым управляют извне, теряет фокус, и PauseMenuController ставит паузу.
    // Собственные проверки паузы проба делает через Time.timeScale, а не через меню.
    // Снимать паузу нужно до разблокировки камеры в Start: Resume возвращает блокировку,
    // запомненную в момент паузы.
    private void Update() => ResumeFocusPause();
    private static void ResumeFocusPause()
    {
        if (!PauseMenuController.IsPaused) return;
        var menu = FindFirstObjectByType<PauseMenuController>();
        if (menu != null) menu.Resume();
    }
    private IEnumerator Until(Func<bool> condition, float timeout, string label)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Check(condition(), label);
    }
    private void Teleport(Vector3 position, Vector3 target)
    {
        var capsule = player.GetComponent<CharacterController>();
        capsule.enabled = false;
        player.transform.position = position;
        Vector3 delta = target - position;
        delta.y = 0f;
        if (delta.sqrMagnitude > .001f) player.transform.rotation = Quaternion.LookRotation(delta);
        capsule.enabled = true;
        player.StopMotion();
        Set(player, "yRotation", -Mathf.Atan2(target.y - view.transform.position.y, Vector3.ProjectOnPlane(target - view.transform.position, Vector3.up).magnitude) * Mathf.Rad2Deg);
    }
    private static T Get<T>(object obj, string field) => (T)Field(obj, field).GetValue(obj);
    private static void Set(object obj, string field, object value) => Field(obj, field).SetValue(obj, value);
    private static FieldInfo Field(object obj, string name) { for (Type t = obj.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, Private); if (f != null) return f; } throw new MissingFieldException(name); }
    private static IEnumerator Empty() { yield break; }
    private void Check(bool value, string label) { (value ? report.passed : report.failed).Add(label); Write(); }
    private void Stage(string value) { report.stage = value; Write(); }
    private void Write() => File.WriteAllText(Path.Combine(output, "neighbor-report.json"), JsonUtility.ToJson(report, true));
    private void Capture(string name) => ProtagonistPlayProbe.Capture(view, Path.Combine(output, name + ".png"));
    private void OnDisable() { if (panel != null) Set(panel, "autoAdvance", previousAuto); Time.timeScale = 1f; }
}
#endif