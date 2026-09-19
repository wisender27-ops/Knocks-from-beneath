using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using KnocksFromBeneath;

public class PieQuestControllerTests
{
    private sealed class Recorder
    {
        public readonly List<string> CreatedQuestTags = new List<string>();
        public bool ShowThoughtsCalled;
        public Action LastOnComplete;
        public bool DinnerFinishedCalled;

        public void CreateQuest(string title, int amount, UnityAction callback, string tag)
        {
            CreatedQuestTags.Add(tag);
        }

        public void ShowThoughts(string[] lines, Action onComplete)
        {
            ShowThoughtsCalled = true;
            LastOnComplete = onComplete;
        }

        public void OnDinnerFinished() => DinnerFinishedCalled = true;
    }

    private GameObject _pieObject;
    private GameObject _microwaveZone;
    private Recorder _recorder;
    private string _activeTag;

    private PieQuestController CreateController()
    {
        _pieObject = new GameObject("Pie");
        _microwaveZone = new GameObject("MicrowaveZone");
        _recorder = new Recorder();
        _activeTag = "";

        return new PieQuestController(
            _pieObject, _microwaveZone,
            _recorder.CreateQuest, _recorder.ShowThoughts,
            tag => tag == _activeTag,
            _recorder.OnDinnerFinished);
    }

    [TearDown]
    public void TearDown()
    {
        if (_pieObject != null) UnityEngine.Object.DestroyImmediate(_pieObject);
        if (_microwaveZone != null) UnityEngine.Object.DestroyImmediate(_microwaveZone);
    }

    [Test]
    public void SetupTakePieQuest_ActivatesPieAndHidesMicrowave()
    {
        var controller = CreateController();
        _microwaveZone.SetActive(true);

        controller.SetupTakePieQuest();

        Assert.That(_pieObject.activeSelf, Is.True);
        Assert.That(_microwaveZone.activeSelf, Is.False);
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("pie-take"));
    }

    [Test]
    public void HandlePieGrabbed_IgnoredWhenPieTakeQuestNotActive()
    {
        var controller = CreateController();
        _activeTag = "trash-collect";
        _microwaveZone.SetActive(false);

        controller.HandlePieGrabbed();

        Assert.That(_recorder.CreatedQuestTags, Is.Empty);
        Assert.That(_microwaveZone.activeSelf, Is.False);
    }

    [Test]
    public void HandlePieGrabbed_StartsMicrowaveQuestWhenPieTakeActive()
    {
        var controller = CreateController();
        _activeTag = "pie-take";

        controller.HandlePieGrabbed();

        Assert.That(_microwaveZone.activeSelf, Is.True);
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("pie-microwave"));
    }

    [Test]
    public void OnPiePlacedInMicrowave_HidesZoneAndCreatesEatQuest()
    {
        var controller = CreateController();
        controller.OnPieTaken();

        controller.OnPiePlacedInMicrowave();

        Assert.That(_microwaveZone.activeSelf, Is.False);
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("pie-eat"));
    }

    [Test]
    public void OnPieEaten_ShowsThoughtsAndChainsToOnDinnerFinished()
    {
        var controller = CreateController();

        controller.OnPieEaten();

        Assert.That(_recorder.ShowThoughtsCalled, Is.True);
        _recorder.LastOnComplete?.Invoke();
        Assert.That(_recorder.DinnerFinishedCalled, Is.True);
    }

    [Test]
    public void CanPlacePieInMicrowave_TrueOnlyAfterPieTakenAndBeforeHeated()
    {
        var controller = CreateController();
        _activeTag = "pie-microwave";

        Assert.That(controller.CanPlacePieInMicrowave(), Is.False, "pie not taken yet");

        controller.OnPieTaken();
        Assert.That(controller.CanPlacePieInMicrowave(), Is.True);

        controller.OnPiePlacedInMicrowave();
        Assert.That(controller.CanPlacePieInMicrowave(), Is.False, "already heated");
    }

    [Test]
    public void CanEatPie_TrueOnlyAfterHeatedAndQuestTagActive()
    {
        var controller = CreateController();

        controller.OnPieTaken();
        controller.OnPiePlacedInMicrowave();
        Assert.That(controller.CanEatPie(), Is.False, "quest tag not active yet");

        _activeTag = "pie-eat";
        Assert.That(controller.CanEatPie(), Is.True);
    }
}
