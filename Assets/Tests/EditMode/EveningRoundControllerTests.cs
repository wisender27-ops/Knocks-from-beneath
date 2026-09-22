using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using KnocksFromBeneath;

public class EveningRoundControllerTests
{
    private sealed class Recorder
    {
        public readonly List<string> CreatedQuestTags = new List<string>();

        public void CreateQuest(string title, int amount, UnityAction callback, string tag)
        {
            CreatedQuestTags.Add(tag);
        }

        public void ShowThoughts(string[] lines, Action onComplete) { }
    }

    private GameObject _questManagerGo;
    private QuestManager _questManager;
    private GameObject _doorGo;
    private Door _door;
    private Recorder _recorder;

    private EveningRoundController CreateController()
    {
        _questManagerGo = new GameObject("QuestManager");
        _questManager = _questManagerGo.AddComponent<QuestManager>();
        // AddComponent doesn't reliably fire Awake() in the EditMode test runner — set the
        // singleton by hand, same workaround as FloorLogicTests.
        QuestManager.Instance = _questManager;

        _doorGo = new GameObject("FrontDoor");
        _door = _doorGo.AddComponent<Door>();

        _recorder = new Recorder();
        return new EveningRoundController(_door, _recorder.CreateQuest, _recorder.ShowThoughts, () => { });
    }

    [TearDown]
    public void TearDown()
    {
        QuestManager.Instance = null;
        GameState.frontDoorLocked = false;
        if (_doorGo != null) UnityEngine.Object.DestroyImmediate(_doorGo);
        if (_questManagerGo != null) UnityEngine.Object.DestroyImmediate(_questManagerGo);
    }

    [Test]
    public void SetupEveningWalkQuest_CreatesEveningWalkQuest()
    {
        var controller = CreateController();

        controller.SetupEveningWalkQuest();

        Assert.That(_recorder.CreatedQuestTags, Does.Contain("evening-walk"));
    }

    [Test]
    public void LockFrontDoor_IgnoredWhenEveningWalkNotActive()
    {
        var controller = CreateController();
        _questManager.CreateQuest("Другое", 1, questTag: "some-other-quest");

        Assert.That(controller.LockFrontDoor(), Is.False);

        Assert.That(GameState.frontDoorLocked, Is.False);
        Assert.That(_door.IsInteractionLocked, Is.False);
    }

    [Test]
    public void LockFrontDoor_LocksDoorAndSetsFlagWhenEveningWalkActive()
    {
        var controller = CreateController();
        _questManager.CreateQuest("Обход", 1, questTag: "evening-walk");

        Assert.That(controller.LockFrontDoor(), Is.True);

        Assert.That(GameState.frontDoorLocked, Is.True);
        Assert.That(_door.IsInteractionLocked, Is.True);
        Assert.That(_questManager.questList[0].currentAmount, Is.EqualTo(1));
    }

    [Test]
    public void HandleBedReached_CompletesQuestWithoutLockingDoor()
    {
        var controller = CreateController();
        _questManager.CreateQuest("Обход", 1, questTag: "evening-walk");

        controller.HandleBedReached();

        Assert.That(GameState.frontDoorLocked, Is.False);
        Assert.That(_door.IsInteractionLocked, Is.False);
        Assert.That(_questManager.questList[0].currentAmount, Is.EqualTo(1));
    }

    [Test]
    public void LockFrontDoor_SecondCall_ReturnsFalseAndDoesNotAddProgressTwice()
    {
        var controller = CreateController();
        _questManager.CreateQuest("Обход", 1, questTag: "evening-walk");

        Assert.That(controller.LockFrontDoor(), Is.True);
        Assert.That(controller.LockFrontDoor(), Is.False, "повторное нажатие на щеколду ничего не делает");
        Assert.That(_questManager.questList[0].currentAmount, Is.EqualTo(1));
    }
}
