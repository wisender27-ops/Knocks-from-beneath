using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class BranchEndingControllerTests
{
    private GameObject _escapeDoorTrigger;
    private bool _hammerPathActivated;
    private bool _endGameCalled;
    private List<string[]> _shownThoughts;
    private Action _lastOnComplete;

    private BranchEndingController CreateController()
    {
        _escapeDoorTrigger = new GameObject("EscapeDoorTrigger");
        _escapeDoorTrigger.SetActive(false);
        _hammerPathActivated = false;
        _endGameCalled = false;
        _shownThoughts = new List<string[]>();

        return new BranchEndingController(
            activateHammerPath: () => _hammerPathActivated = true,
            escapeDoorTrigger: _escapeDoorTrigger,
            showThoughts: (lines, onComplete) => { _shownThoughts.Add(lines); _lastOnComplete = onComplete; },
            endGame: () => _endGameCalled = true);
    }

    [TearDown]
    public void TearDown()
    {
        GameState.frontDoorLocked = false;
        if (_escapeDoorTrigger != null) UnityEngine.Object.DestroyImmediate(_escapeDoorTrigger);
    }

    [Test]
    public void Activate_AlwaysActivatesHammerPath()
    {
        var controller = CreateController();

        controller.Activate();

        Assert.That(_hammerPathActivated, Is.True);
    }

    [Test]
    public void Activate_EnablesEscapeDoorWhenNotLocked()
    {
        GameState.frontDoorLocked = false;
        var controller = CreateController();

        controller.Activate();

        Assert.That(_escapeDoorTrigger.activeSelf, Is.True);
    }

    [Test]
    public void Activate_KeepsEscapeDoorDisabledWhenLocked()
    {
        GameState.frontDoorLocked = true;
        var controller = CreateController();

        controller.Activate();

        Assert.That(_escapeDoorTrigger.activeSelf, Is.False);
    }

    [Test]
    public void HandleDoorEscape_DeactivatesTriggerAndEndsGameOnce()
    {
        GameState.frontDoorLocked = false;
        var controller = CreateController();
        controller.Activate();

        controller.HandleDoorEscape();
        Assert.That(_escapeDoorTrigger.activeSelf, Is.False);
        Assert.That(_shownThoughts.Count, Is.EqualTo(1));

        _lastOnComplete?.Invoke();
        Assert.That(_endGameCalled, Is.True);
    }

    [Test]
    public void HandleDoorEscape_IgnoredOnSecondCall()
    {
        var controller = CreateController();
        controller.Activate();
        controller.HandleDoorEscape();
        int countAfterFirst = _shownThoughts.Count;

        controller.HandleDoorEscape();

        Assert.That(_shownThoughts.Count, Is.EqualTo(countAfterFirst), "second call must be ignored");
    }
}
