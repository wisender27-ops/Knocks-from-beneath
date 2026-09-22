using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KnocksFromBeneath;

// Зона принимает предмет только если его тип и тег активного квеста совпадают
// (см. OnTriggerEnter) — и, как PlacementZone, может прятать/показывать связанные
// предметы декора (itemsToReveal), не завися от родительства или активности самой
// зоны при старте сцены.
public class QuestCollectorTests
{
    private GameObject _questManagerGo;
    private GameObject _collectorGo;
    private GameObject _item1;

    private QuestCollector CreateCollector(CollectableItem.ItemType acceptedType)
    {
        _questManagerGo = new GameObject("QuestManager");
        QuestManager.Instance = _questManagerGo.AddComponent<QuestManager>();
        QuestManager.Instance.questList = new List<QuestManager.QuestData>();

        _collectorGo = new GameObject("Collector");
        var collector = _collectorGo.AddComponent<QuestCollector>();
        collector.acceptedType = acceptedType;
        return collector;
    }

    private static void SetActiveQuest(string tag, int required)
    {
        QuestManager.Instance.questList.Add(new QuestManager.QuestData
        {
            questTitle = tag,
            questTag = tag,
            requiredAmount = required,
            currentAmount = 0,
            onQuestComplete = new UnityEngine.Events.UnityEvent()
        });
        QuestManager.Instance.currentQuestIndex = QuestManager.Instance.questList.Count - 1;
    }

    private void SetItemsToReveal(QuestCollector collector, params GameObject[] items)
    {
        var field = typeof(QuestCollector).GetField("itemsToReveal", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(collector, items);
    }

    private void InvokeAwake(QuestCollector collector)
    {
        var method = typeof(QuestCollector).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(collector, null);
    }

    private void InvokeOnTriggerEnter(QuestCollector collector, Collider other)
    {
        var method = typeof(QuestCollector).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(collector, new object[] { other });
    }

    [TearDown]
    public void TearDown()
    {
        QuestManager.Instance = null;
        if (_collectorGo != null) Object.DestroyImmediate(_collectorGo);
        if (_questManagerGo != null) Object.DestroyImmediate(_questManagerGo);
        if (_item1 != null) Object.DestroyImmediate(_item1);
    }

    [Test]
    public void Awake_HidesConfiguredItems()
    {
        var collector = CreateCollector(CollectableItem.ItemType.Trash);
        _item1 = new GameObject("GarbageBags");
        SetItemsToReveal(collector, _item1);

        InvokeAwake(collector);

        Assert.That(_item1.activeSelf, Is.False);
    }

    [Test]
    public void OnTriggerEnter_AcceptingItem_RevealsConfiguredItems()
    {
        var collector = CreateCollector(CollectableItem.ItemType.Trash);
        _item1 = new GameObject("GarbageBags");
        SetItemsToReveal(collector, _item1);
        InvokeAwake(collector);
        SetActiveQuest("trash-delivery", 1);

        var bagGo = new GameObject("TrashBag");
        var bagItem = bagGo.AddComponent<CollectableItem>();
        bagItem.currentItemType = CollectableItem.ItemType.Trash;
        var bagCollider = bagGo.AddComponent<BoxCollider>();

        // OnTriggerEnter calls Object.Destroy(), which is a no-op-with-error outside Play
        // Mode — expected here, this EditMode test runner never enters Play Mode. The item
        // itself isn't actually destroyed, so no manual cleanup needed for bagGo either.
        LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode.*", RegexOptions.Singleline));
        InvokeOnTriggerEnter(collector, bagCollider);

        Assert.That(_item1.activeSelf, Is.True);
        Object.DestroyImmediate(bagGo);
    }

    [Test]
    public void OnTriggerEnter_WrongQuestTag_DoesNotRevealItems()
    {
        var collector = CreateCollector(CollectableItem.ItemType.Trash);
        _item1 = new GameObject("GarbageBags");
        SetItemsToReveal(collector, _item1);
        InvokeAwake(collector);
        SetActiveQuest("box-delivery", 1); // не тот тег — сдача мусора не засчитается

        var bagGo = new GameObject("TrashBag");
        var bagItem = bagGo.AddComponent<CollectableItem>();
        bagItem.currentItemType = CollectableItem.ItemType.Trash;
        var bagCollider = bagGo.AddComponent<BoxCollider>();

        InvokeOnTriggerEnter(collector, bagCollider);

        Assert.That(_item1.activeSelf, Is.False);
        Object.DestroyImmediate(bagGo);
    }
}
