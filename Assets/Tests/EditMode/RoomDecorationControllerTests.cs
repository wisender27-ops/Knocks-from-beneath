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
    public void SetupDecorationQuest_ActivatesZonesWithAmountMatchingZoneCount()
    {
        var controller = CreateController();

        controller.SetupDecorationQuest();

        Assert.That(_zones, Has.All.Matches<GameObject>(z => z.activeSelf));
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("room-decoration"));
        Assert.That(_recorder.CreatedQuestAmounts, Does.Contain(_zones.Length));
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
