using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

// Зона засчитывает прогресс не всем квестам подряд, а только тем, чьи теги ей назначены:
// коробки дня 1 (box-delivery) и декор дня 2 (room-decoration) используют один и тот же
// компонент, но разные наборы зон и разные квесты.
//
// itemsToReveal раньше жил в отдельном компоненте RoomRevealZone — слит сюда напрямую
// (RequireComponent-пара всегда стояла 1:1 на каждой зоне в сцене, отдельный компонент
// не добавлял гибкости).
public class PlacementZoneTests
{
    private GameObject _questManagerGo;
    private GameObject _zoneGo;
    private GameObject _slotGo;
    private GameObject _box;
    private GameObject _item1;
    private GameObject _item2;

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
        // TryPlaceBox теперь для зон box-delivery/box-collect требует реальную коробку
        // (CollectableItem.ItemType.Box) — тестовый объект должен вести себя как настоящая
        // коробка, иначе тесты на прогресс/слоты ломаются на этой не связанной с ними проверке.
        var collectable = _box.AddComponent<CollectableItem>();
        collectable.currentItemType = CollectableItem.ItemType.Box;
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

    // itemsToReveal — приватный [SerializeField], задаётся в инспекторе, не из кода.
    // Ставим через рефлексию, как раньше в RoomRevealZoneTests.
    private void SetItemsToReveal(PlacementZone zone, params GameObject[] items)
    {
        var field = typeof(PlacementZone).GetField("itemsToReveal", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(zone, items);
    }

    // AddComponent не гарантирует вызов Awake() в этом раннере — вызываем явно, чтобы
    // отработала реальная логика HideItems(), как при загрузке сцены.
    private void InvokeAwake(PlacementZone zone)
    {
        var method = typeof(PlacementZone).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(zone, null);
    }

    // Симулирует [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] — прогоняет HideItems()
    // по всем зонам сцены, включая выключенные (чей Awake() ещё не вызывался).
    private void InvokeBootHide()
    {
        var method = typeof(PlacementZone).GetMethod("HideAllRevealItemsAtBoot", BindingFlags.NonPublic | BindingFlags.Static);
        method.Invoke(null, null);
    }

    [TearDown]
    public void TearDown()
    {
        QuestManager.Instance = null;
        if (_zoneGo != null) Object.DestroyImmediate(_zoneGo);
        if (_slotGo != null) Object.DestroyImmediate(_slotGo);
        if (_box != null) Object.DestroyImmediate(_box);
        if (_questManagerGo != null) Object.DestroyImmediate(_questManagerGo);
        if (_item1 != null) Object.DestroyImmediate(_item1);
        if (_item2 != null) Object.DestroyImmediate(_item2);
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

    [Test]
    public void Awake_HidesConfiguredItems_EvenIfLeftActiveInScene()
    {
        var zone = CreateZone();
        _item1 = new GameObject("HiddenItem1");
        _item2 = new GameObject("HiddenItem2");
        SetItemsToReveal(zone, _item1, _item2);

        InvokeAwake(zone);

        Assert.That(_item1.activeSelf, Is.False);
        Assert.That(_item2.activeSelf, Is.False);
    }

    [Test]
    public void RevealItems_ActivatesAllConfiguredItems()
    {
        var zone = CreateZone();
        _item1 = new GameObject("HiddenItem1");
        _item2 = new GameObject("HiddenItem2");
        SetItemsToReveal(zone, _item1, _item2);
        InvokeAwake(zone);

        zone.RevealItems();

        Assert.That(_item1.activeSelf, Is.True);
        Assert.That(_item2.activeSelf, Is.True);
    }

    [Test]
    public void RevealItems_CalledTwice_StaysIdempotent()
    {
        var zone = CreateZone();
        _item1 = new GameObject("HiddenItem1");
        SetItemsToReveal(zone, _item1);
        InvokeAwake(zone);

        zone.RevealItems();
        _item1.SetActive(false); // simulate something else turning it back off
        zone.RevealItems(); // must not re-activate — _revealed guard should block this

        Assert.That(_item1.activeSelf, Is.False);
    }

    [Test]
    public void BootHide_HidesConfiguredItems_OnZoneThatStartsInactive()
    {
        // Все зоны в реальной сцене начинают выключенными (их включает
        // MoveInChoresController/RoomDecorationController по ходу сюжета) — Awake()
        // выключенного GameObject не срабатывает, поэтому скрытие не может зависеть от
        // него. HideAllRevealItemsAtBoot() должен находить такие зоны через
        // FindObjectsInactive.Include и прятать их предметы без Awake().
        var zone = CreateZone();
        _zoneGo.SetActive(false);
        _item1 = new GameObject("HiddenItem1");
        SetItemsToReveal(zone, _item1);

        InvokeBootHide();

        Assert.That(_item1.activeSelf, Is.False);
    }

    [Test]
    public void TryPlaceBox_RevealsConfiguredItems()
    {
        var zone = CreateZone();
        _item1 = new GameObject("HiddenItem1");
        _item2 = new GameObject("HiddenItem2");
        SetItemsToReveal(zone, _item1, _item2);
        InvokeAwake(zone);

        zone.TryPlaceBox(_box);

        Assert.That(_item1.activeSelf, Is.True);
        Assert.That(_item2.activeSelf, Is.True);
    }
}
