#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KnocksFromBeneath;
using UnityEngine;

public sealed class TrashDisposalPlayProbe : MonoBehaviour
{
    [Serializable] private sealed class Report
    {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
        public bool complete;
    }
    private readonly Report report = new Report();
    private const string Folder = "Library/TrashDisposalVerification";
    private QuestCollector collector;
    private PlayerInteraction actor;
    private QuestManager quests;

    public static void Begin()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required");
        new GameObject("Trash disposal verification").AddComponent<TrashDisposalPlayProbe>();
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(Folder);
        var intro = FindFirstObjectByType<IntroSequence>();
        intro.StopAllCoroutines();
        intro.enabled = false;
        NarrativeDisplayQueue.InterruptAndEnqueue(this, Empty);
        actor = FindFirstObjectByType<PlayerInteraction>();
        actor.enabled = false;
        quests = QuestManager.Instance;
        collector = FindObjectsByType<QuestCollector>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(c => c.acceptedType == CollectableItem.ItemType.Trash);
        collector.gameObject.SetActive(true);
        var itemsField = typeof(QuestCollector).GetField("itemsToReveal", BindingFlags.Instance | BindingFlags.NonPublic);
        var items = (GameObject[])itemsField.GetValue(collector);
        var fill = items.First().transform;
        Vector3 fillPosition = fill.localPosition, fillScale = fill.localScale;
        Transform bin = fill.parent;
        Quaternion binRotation = bin.localRotation;
        var player = actor.GetComponent<PlayerController>();
        player.enabled = false;
        actor.GetComponent<CharacterController>().enabled = false;
        actor.transform.position = new Vector3(-36.43f, .86f, 126.4f);
        actor.playerCamera.transform.LookAt(collector.DisposalCollider.bounds.center);
        player.isCameraLocked = false;
        yield return null;

        quests.CreateQuest("Проверка мусора", 1, () => collector.gameObject.SetActive(false), "trash-delivery");
        var quest = quests.GetActiveQuest();
        GameObject bag = SpawnBag();
        Check(actor.TryGrabObjectFromScript(bag), "Held actual Garbage prefab");
        Check(actor.TryGetTrashDisposalTarget(out var target) && target == collector, "Target is actual trash bin");
        Capture("00-before");
        Check(collector.TryDisposeTrash(actor), "Disposal starts");
        Check(actor.GetHeldObject() == null && quest.currentAmount == 0, "Hands free; no instant quest credit");
        Check(!collector.TryDisposeTrash(actor), "Repeated disposal rejected");
        yield return new WaitForSeconds(.1f);
        Capture("01-lift");
        Vector3 frozen = bag.transform.position;
        Time.timeScale = 0;
        AudioListener.pause = true;
        yield return new WaitForSecondsRealtime(.25f);
        Check(bag != null && bag.transform.position == frozen && quest.currentAmount == 0, "Pause freezes flight and quest");
        Time.timeScale = 1;
        AudioListener.pause = false;
        Check(!player.isCameraLocked, "Animation does not lock camera");
        yield return new WaitForSeconds(.25f);
        Capture("02-flight");
        Check(bag != null && quest.currentAmount == 0, "Bag remains visible midflight without credit");
        yield return new WaitForSeconds(.23f);
        Capture("03-entry");
        yield return new WaitForSeconds(.15f);
        Capture("04-impact");
        Check(quest.currentAmount == 1 && !collector.gameObject.activeSelf && bag == null, "Impact credits once and disables real collector");
        Check(FindFirstObjectByType<TrashDisposalAnimation>() != null, "Settling survives collector deactivation");
        yield return new WaitForSeconds(.5f);
        Capture("05-settled");
        Check(FindFirstObjectByType<TrashDisposalAnimation>() == null, "Animation runner cleaned up");
        Check(fill.gameObject.activeSelf && fill.localPosition == fillPosition && fill.localScale == fillScale &&
            bin.localRotation == binRotation, "Final fill and bin transforms restored exactly");
        yield return null;

        collector.gameObject.SetActive(true);
        quests.CreateQuest("Проверка отмены", 1, questTag: "trash-delivery");
        quest = quests.GetActiveQuest();
        bag = SpawnBag();
        Check(actor.TryGrabObjectFromScript(bag) && collector.TryDisposeTrash(actor), "Second disposal starts");
        yield return new WaitForSeconds(.15f);
        FindFirstObjectByType<TrashDisposalAnimation>().gameObject.SetActive(false);
        yield return null;
        Check(quest.currentAmount == 0 && bag != null && !bag.GetComponent<Rigidbody>().isKinematic &&
            bag.GetComponentsInChildren<Collider>().Any(c => c.enabled) && collector.CanAcceptTrash(bag),
            "Cancellation restores usable physical bag and reservation");
        Destroy(bag);

        itemsField.SetValue(collector, null);
        quests.CreateQuest("Без наполнения", 1, questTag: "trash-delivery");
        quest = quests.GetActiveQuest();
        bag = SpawnBag();
        Check(actor.TryGrabObjectFromScript(bag) && collector.TryDisposeTrash(actor), "Missing fill has safe fallback");
        yield return new WaitForSeconds(1.2f);
        Check(quest.currentAmount == 1 && actor.GetHeldObject() == null, "Missing fill still completes once");
        itemsField.SetValue(collector, items);
        report.complete = true;
        File.WriteAllText(Folder + "/report.json", JsonUtility.ToJson(report, true));
    }

    private GameObject SpawnBag()
    {
        var bag = Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Garbage.prefab"));
        bag.transform.position = actor.playerCamera.transform.position + actor.playerCamera.transform.forward * .7f;
        bag.transform.rotation = Quaternion.identity;
        ColliderRepair.FitBoxColliderToMesh(bag);
        Physics.SyncTransforms();
        var center = bag.GetComponent<Renderer>().bounds.center;
        bag.transform.position += actor.playerCamera.transform.position + actor.playerCamera.transform.forward * .8f - center;
        Physics.SyncTransforms();
        return bag;
    }

    private void Check(bool value, string label)
    {
        (value ? report.passed : report.failed).Add(label);
        File.WriteAllText(Folder + "/report.json", JsonUtility.ToJson(report, true));
    }
    private static IEnumerator Empty() { yield break; }
    private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Folder + "/" + name + ".png");
}
#endif
