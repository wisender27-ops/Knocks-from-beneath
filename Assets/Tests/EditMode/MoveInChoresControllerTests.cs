using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using KnocksFromBeneath;

public class MoveInChoresControllerTests
{
    private sealed class Recorder
    {
        public readonly List<string> CreatedQuestTags = new List<string>();
        public bool ChoresFinishedCalled;
        public bool ShowThoughtsCalled;
        public Action LastOnComplete;

        public void CreateQuest(string title, int amount, UnityAction callback, string tag)
        {
            CreatedQuestTags.Add(tag);
        }

        public void ShowThoughts(string[] lines, Action onComplete)
        {
            ShowThoughtsCalled = true;
            LastOnComplete = onComplete;
        }

        public void OnChoresFinished() => ChoresFinishedCalled = true;
    }

    private GameObject _trashZone;
    private GameObject _garageZone;
    private Recorder _recorder;

    private MoveInChoresController CreateController()
    {
        _trashZone = new GameObject("TrashZone");
        _garageZone = new GameObject("GarageZone");
        _recorder = new Recorder();

        return new MoveInChoresController(
            _trashZone, _garageZone,
            _recorder.CreateQuest, _recorder.ShowThoughts,
            _recorder.OnChoresFinished);
    }

    [TearDown]
    public void TearDown()
    {
        if (_trashZone != null) UnityEngine.Object.DestroyImmediate(_trashZone);
        if (_garageZone != null) UnityEngine.Object.DestroyImmediate(_garageZone);
    }

    [Test]
    public void SetupTrashQuest_CreatesTrashCollectQuest()
    {
        var controller = CreateController();

        controller.SetupTrashQuest();

        Assert.That(_recorder.CreatedQuestTags, Does.Contain("trash-collect"));
    }

    [Test]
    public void StartTrashDeliveryQuest_ActivatesZoneAndIgnoresSecondCall()
    {
        var controller = CreateController();

        controller.StartTrashDeliveryQuest();
        Assert.That(_trashZone.activeSelf, Is.True);
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("trash-delivery"));

        int countBefore = _recorder.CreatedQuestTags.Count;
        controller.StartTrashDeliveryQuest();
        Assert.That(_recorder.CreatedQuestTags.Count, Is.EqualTo(countBefore), "second call must be ignored");
    }

    [Test]
    public void OnTrashFinished_HidesZoneAndChainsToBoxQuest()
    {
        var controller = CreateController();
        controller.StartTrashDeliveryQuest();

        controller.OnTrashFinished();

        Assert.That(_trashZone.activeSelf, Is.False);
        Assert.That(_recorder.ShowThoughtsCalled, Is.True);

        _recorder.LastOnComplete?.Invoke();
        Assert.That(_garageZone.activeSelf, Is.True);
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("box-delivery"));
    }

    [Test]
    public void OnBoxFinished_HidesZoneAndChainsToChoresFinished()
    {
        var controller = CreateController();
        controller.SetupBoxQuest();

        controller.OnBoxFinished();

        Assert.That(_garageZone.activeSelf, Is.False);
        Assert.That(_recorder.ShowThoughtsCalled, Is.True);

        _recorder.LastOnComplete?.Invoke();
        Assert.That(_recorder.ChoresFinishedCalled, Is.True);
    }
}
