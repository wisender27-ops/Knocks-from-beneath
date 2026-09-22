using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

// Регрессионный тест на баг: удар ломом по полу до того, как квест
// "Вскрыть доски на кухне" вообще создан (пока ещё идёт диалог после
// подбора лома), ломал пол вхолостую — QuestManager.AddProgress уходил
// в никуда, а повторно сломать уже нельзя (isBroken), квест зависал навсегда.
public class FloorLogicTests
{
    private GameObject _questManagerGo;
    private QuestManager _questManager;
    private GameObject _floorGo;
    private FloorLogic _floor;
    private GameObject _inventoryGo;
    private PlayerInventory _inventory;

    [SetUp]
    public void SetUp()
    {
        _questManagerGo = new GameObject("QuestManager");
        _questManager = _questManagerGo.AddComponent<QuestManager>();
        // AddComponent doesn't reliably fire Awake() in the EditMode test runner (same reason
        // QuestManagerTests never touches QuestManager.Instance) — set the singleton by hand so
        // FloorLogic.Break(), which reads QuestManager.Instance, sees the manager we just built.
        QuestManager.Instance = _questManager;

        _floorGo = new GameObject("Floor");
        _floor = _floorGo.AddComponent<FloorLogic>();
        _inventoryGo = new GameObject("Inventory");
        _inventory = _inventoryGo.AddComponent<PlayerInventory>();
        _inventory.hasCrowbar = true;
    }

    [TearDown]
    public void TearDown()
    {
        QuestManager.Instance = null;
        Object.DestroyImmediate(_inventoryGo);
        Object.DestroyImmediate(_floorGo);
        Object.DestroyImmediate(_questManagerGo);
    }

    [Test]
    public void Break_DoesNothingWhenNoQuestActive()
    {
        // Симулируем окно между "лом подобран" и "квест на пол создан":
        // никакого активного квеста ещё нет.
        _floor.TryStartBreak(_inventory);

        Assert.That(_floor.isBroken, Is.False,
            "пол не должен ломаться, пока квест 'break-floor' не активен");
    }

    [Test]
    public void Break_DoesNothingWhenDifferentQuestActive()
    {
        _questManager.CreateQuest("Найти лом", 1, questTag: "crowbar-find");

        _floor.TryStartBreak(_inventory);

        Assert.That(_floor.isBroken, Is.False);
    }

    [Test]
    public void Break_WorksWhenBreakFloorQuestActive()
    {
        _questManager.CreateQuest("Вскрыть доски на кухне", 1, questTag: "break-floor");

        Assert.That(_floor.TryStartBreak(_inventory), Is.True);
        Assert.That(_floor.State, Is.EqualTo(FloorLogic.BreakState.Playing));
        _floor.CompleteBreak();

        Assert.That(_floor.isBroken, Is.True);
        Assert.That(_questManager.questList[0].currentAmount, Is.EqualTo(1));
    }

    [Test]
    public void Break_IsIdempotentOnSecondCall()
    {
        _questManager.CreateQuest("Вскрыть доски на кухне", 1, questTag: "break-floor");

        _floor.TryStartBreak(_inventory);
        _floor.CompleteBreak();
        Assert.That(_floor.TryStartBreak(_inventory), Is.False);
        _floor.CompleteBreak();

        Assert.That(_questManager.questList[0].currentAmount, Is.EqualTo(1),
            "второй удар не должен начислять прогресс повторно");
    }
}
