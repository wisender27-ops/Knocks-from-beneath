using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

// Зона засчитывает прогресс не всем квестам подряд, а только тем, чьи теги ей назначены:
// коробки дня 1 (box-delivery) и декор дня 2 (room-decoration) используют один и тот же
// компонент, но разные наборы зон и разные квесты.
public class PlacementZoneTests
{
    private GameObject _questManagerGo;
    private GameObject _zoneGo;
    private GameObject _slotGo;
    private GameObject _box;

    private PlacementZone CreateZone(params string[] progressTags)
    {
        _questManagerGo = new GameObject("QuestManager");
        // AddComponent не гарантирует Awake() в EditMode-раннере — синглтон ставим руками,
        // как в EveningRoundControllerTests/FloorLogicTests.
        QuestManager.Instance = _questManagerGo.AddComponent<QuestManager>();
        QuestManager.Instance.questList = new List<QuestManager.QuestData>();

        _zoneGo = new GameObject("Zone");
        var zone = _zoneGo.AddComponent<PlacementZone>();
        _slotGo = new GameObject("Slot1");
        zone.slots = new List<Transform> { _slotGo.transform };
        zone.progressQuestTags = progressTags;

        _box = new GameObject("Box");
        return zone;
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

    [TearDown]
    public void TearDown()
    {
        QuestManager.Instance = null;
        if (_zoneGo != null) Object.DestroyImmediate(_zoneGo);
        if (_slotGo != null) Object.DestroyImmediate(_slotGo);
        if (_box != null) Object.DestroyImmediate(_box);
        if (_questManagerGo != null) Object.DestroyImmediate(_questManagerGo);
    }

    [Test]
    public void TryPlaceBox_AddsProgress_ForConfiguredQuestTag()
    {
        var zone = CreateZone("room-decoration");
        SetActiveQuest("room-decoration", 3);

        Assert.That(zone.TryPlaceBox(_box), Is.True);
        Assert.That(QuestManager.Instance.questList[0].currentAmount, Is.EqualTo(1));
    }

    [Test]
    public void TryPlaceBox_DoesNotAddProgress_ForOtherQuestTag()
    {
        var zone = CreateZone("box-delivery", "box-collect");
        SetActiveQuest("room-decoration", 3);

        Assert.That(zone.TryPlaceBox(_box), Is.True, "предмет всё равно встаёт в слот");
        Assert.That(QuestManager.Instance.questList[0].currentAmount, Is.EqualTo(0));
    }
}
