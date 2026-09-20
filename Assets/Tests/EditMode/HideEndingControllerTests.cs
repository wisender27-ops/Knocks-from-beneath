using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class HideEndingControllerTests
{
    private GameObject[] _zones;
    private Transform[] _ambushPoints;
    private GameObject _player;
    private bool _cameraLockCalled;
    private bool _cameraLockedValue;
    private bool _endGameCalled;
    private List<string[]> _shownThoughts;
    private Action _lastOnComplete;
    // Имитирует общую защёлку концовок: BranchEndingController.TryResolve() отдаёт
    // развилку только первому дошедшему пути.
    private bool _endingClaimable;

    // Manually pumps the coroutine to completion instead of running it through Unity's own
    // scheduler — WaitForSeconds is just a yielded object here, nothing enforces real time,
    // so SearchRoutine/AmbushRoutine run fully synchronously. Lets these tests exercise the
    // whole flow without Play Mode, same limitation EditMode tests always have with coroutines.
    static Coroutine FakeStartCoroutine(IEnumerator routine)
    {
        while (routine.MoveNext()) { }
        return null;
    }

    private HideEndingController CreateController()
    {
        _zones = new[] { new GameObject("Zone1"), new GameObject("Zone2") };
        foreach (var z in _zones) z.SetActive(false);

        _ambushPoints = new[] { new GameObject("Ambush1").transform, new GameObject("Ambush2").transform };
        _player = new GameObject("Player");

        _cameraLockCalled = false;
        _endGameCalled = false;
        _endingClaimable = true;
        _shownThoughts = new List<string[]>();

        return new HideEndingController(
            _zones, _ambushPoints, _player.transform,
            FakeStartCoroutine,
            (lines, onComplete) => { _shownThoughts.Add(lines); _lastOnComplete = onComplete; },
            locked => { _cameraLockCalled = true; _cameraLockedValue = locked; },
            tryClaimEnding: () => _endingClaimable,
            endGame: () => _endGameCalled = true);
    }

    [TearDown]
    public void TearDown()
    {
        if (_zones != null)
            foreach (var z in _zones) if (z != null) UnityEngine.Object.DestroyImmediate(z);
        if (_ambushPoints != null)
            foreach (var a in _ambushPoints) if (a != null) UnityEngine.Object.DestroyImmediate(a.gameObject);
        if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
    }

    [Test]
    public void ActivateZones_ActivatesAllZones()
    {
        var controller = CreateController();

        controller.ActivateZones();

        Assert.That(_zones, Has.All.Matches<GameObject>(z => z.activeSelf));
    }

    [Test]
    public void HandleZoneEntered_UnknownZone_DoesNothing()
    {
        var controller = CreateController();
        var stranger = new GameObject("NotAZone");

        controller.HandleZoneEntered(stranger);

        Assert.That(_shownThoughts, Is.Empty);
        UnityEngine.Object.DestroyImmediate(stranger);
    }

    [Test]
    public void LeavingTheHidingSpot_AlwaysTriggersAmbushAndEndsGame_RegardlessOfTiming()
    {
        // The whole point of this ending: no safe way out. Leaving right after entering
        // (before any "safe to leave" signal could plausibly have fired) still catches you.
        var controller = CreateController();
        controller.HandleZoneEntered(_zones[0]);

        controller.HandleZoneExited(_zones[0]);

        Assert.That(_cameraLockCalled, Is.True);
        Assert.That(_cameraLockedValue, Is.True);
        Assert.That(_shownThoughts, Is.Not.Empty);

        _lastOnComplete?.Invoke();
        Assert.That(_endGameCalled, Is.True);
    }

    [Test]
    public void HandleZoneExited_ForADifferentZoneThanTheOneEntered_DoesNotResolve()
    {
        var controller = CreateController();
        controller.HandleZoneEntered(_zones[0]);

        controller.HandleZoneExited(_zones[1]);

        Assert.That(_endGameCalled, Is.False);
    }

    [Test]
    public void HandleZoneEntered_WhileAlreadyHiding_IgnoresSecondZone()
    {
        var controller = CreateController();
        controller.HandleZoneEntered(_zones[0]);
        _shownThoughts.Clear(); // isolate: only care about what the second Entered call does

        controller.HandleZoneEntered(_zones[1]);

        Assert.That(_shownThoughts, Is.Empty, "entering a second zone while already hiding must be a no-op");
    }

    [Test]
    public void ZoneExit_DoesNothing_WhenAnotherEndingAlreadyClaimedTheBranch()
    {
        var controller = CreateController();
        controller.ActivateZones();
        controller.HandleZoneEntered(_zones[0]);

        _shownThoughts.Clear();
        _endingClaimable = false; // развилку уже забрал побег через дверь
        controller.HandleZoneExited(_zones[0]);

        Assert.That(_cameraLockCalled, Is.False);
        Assert.That(_endGameCalled, Is.False);
        Assert.That(_shownThoughts, Is.Empty);
    }

    [Test]
    public void SetZonesActiveFalse_DisablesZonesAndStopsTheHidingPath()
    {
        var controller = CreateController();
        controller.ActivateZones();
        controller.HandleZoneEntered(_zones[0]);

        controller.SetZonesActive(false);
        _shownThoughts.Clear();
        controller.HandleZoneExited(_zones[0]);

        Assert.That(_zones[0].activeSelf, Is.False);
        Assert.That(_endGameCalled, Is.False);
        Assert.That(_shownThoughts, Is.Empty);
    }
}
