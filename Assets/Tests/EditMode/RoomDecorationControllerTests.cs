using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using KnocksFromBeneath;

public class RoomDecorationControllerTests
{
    private sealed class Recorder
    {
        public readonly List<string> CreatedQuestTags = new List<string>();
        public readonly List<int> CreatedQuestAmounts = new List<int>();
        public bool ShowThoughtsCalled;
        public Action LastOnComplete;
        public bool DecorationFinishedCalled;
        public UnityAction LastQuestCompleteCallback;

        public void CreateQuest(string title, int amount, UnityAction callback, string tag)
        {
            CreatedQuestTags.Add(tag);
            CreatedQuestAmounts.Add(amount);
            LastQuestCompleteCallback = callback;
        }

        public void ShowThoughts(string[] lines, Action onComplete)
        {
            ShowThoughtsCalled = true;
            LastOnComplete = onComplete;
        }

        public void OnDecorationFinished() => DecorationFinishedCalled = true;
    }

    private GameObject[] _zones;
    private Recorder _recorder;

    private RoomDecorationController CreateController()
    {
        _zones = new[] { new GameObject("DecoZone1"), new GameObject("DecoZone2") };
        _recorder = new Recorder();

        return new RoomDecorationController(
            _zones, _recorder.CreateQuest, _recorder.ShowThoughts,
            _recorder.OnDecorationFinished);
    }

    [TearDown]
    public void TearDown()
    {
        if (_zones == null) return;
        foreach (var z in _zones)
            if (z != null) UnityEngine.Object.DestroyImmediate(z);
    }

    [Test]
    public void IsAtKitchen_OnlyNearKitchenOnFirstFloor()
    {
        var kitchen = new Vector3(-46.3f, 1.8f, 106.6f);
        Assert.That(RoomDecorationController.IsAtKitchen(new Vector3(-44f, 1.6f, 107f), kitchen), Is.True);
        Assert.That(RoomDecorationController.IsAtKitchen(new Vector3(-44f, 4.9f, 107f), kitchen), Is.False, "спальня над кухней");
        Assert.That(RoomDecorationController.IsAtKitchen(new Vector3(-44f, 1.6f, 116f), kitchen), Is.False, "гостиная");
    }

    [Test]
    public void IsIndoors_SeparatesHouseFromPorchAndYard()
    {
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-39.5f, 1.6f, 115f)), Is.True, "прихожая");
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-44f, 1.6f, 118f)), Is.True, "лестничное крыло");
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-39.25f, 1.6f, 118f)), Is.False, "крыльцо");
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-43f, 1.6f, 103f)), Is.False, "задний двор");
    }

    [Test]
    public void SetupDecorationQuest_ActivatesZonesWithAmountMatchingZoneCount()
    {
        var controller = CreateController();

        controller.SetupDecorationQuest();

        Assert.That(_zones, Has.All.Matches<GameObject>(z => z.activeSelf));
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("room-decoration"));
        Assert.That(_recorder.CreatedQuestAmounts, Does.Contain(_zones.Length));
    }

    [Test]
    public void SetupDecorationQuest_ConfiguresPlacementPreviews()
    {
        var controller = CreateController();
        var zone = _zones[0].AddComponent<PlacementZone>();
        zone.progressQuestTags = new[] { "room-decoration" };

        controller.SetupDecorationQuest();
        Assert.That(zone.AcceptsQuest("room-decoration"), Is.True);

        _recorder.LastQuestCompleteCallback.Invoke();
        Assert.That(zone.AcceptsQuest("room-decoration"), Is.False);
    }

    [Test]
    public void QuestCompletion_DeactivatesZonesAndChainsToOnDecorationFinished()
    {
        var controller = CreateController();
        controller.SetupDecorationQuest();

        // Simulate QuestManager.AddProgress reaching amount and firing the completion
        // callback that SetupDecorationQuest registered (private OnDecorationFinished).
        _recorder.LastQuestCompleteCallback.Invoke();

        Assert.That(_zones, Has.None.Matches<GameObject>(z => z.activeSelf));
        Assert.That(_recorder.ShowThoughtsCalled, Is.True);

        _recorder.LastOnComplete?.Invoke();
        Assert.That(_recorder.DecorationFinishedCalled, Is.True);
    }
}
