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
    private EndingId? _lastEnding;
    private List<string[]> _shownThoughts;
    private Action _lastOnComplete;
    // Имитирует общую защёлку концовок: BranchEndingController.TryResolve() отдаёт
    // развилку только первому дошедшему пути.
    private bool _endingClaimable;
    private int _hideStarted, _survived;

    // Manually pumps the coroutine to completion instead of running it through Unity's own
    // scheduler — WaitForSeconds is just a yielded object here, nothing enforces real time,
    // so SearchRoutine/AmbushRoutine run fully synchronously. Lets these tests exercise the
    // whole flow without Play Mode, same limitation EditMode tests always have with coroutines.
    static Coroutine FakeStartCoroutine(IEnumerator routine)
    {
        while (routine.MoveNext()) { }
        return null;
    }

    // M-07: поиск, который тест завершает сам (finished) и который считает вызовы Stop().
    private sealed class FakeSearch : IHidingSearch
    {
        public Action<bool> Finished;
        public int Runs, Stops, Prompt = -1;
        public void SetPrompt(int zoneIndex) => Prompt = zoneIndex;
        public IEnumerator Run(int zoneIndex, Action<bool> finished)
        {
            Runs++;
            Finished = finished;
            yield break;
        }
        public void Stop() => Stops++;
    }

    private HideEndingController CreateController(IHidingSearch search = null)
    {
        _zones = new[] { new GameObject("Zone1"), new GameObject("Zone2") };
        foreach (var z in _zones) z.SetActive(false);

        _ambushPoints = new[] { new GameObject("Ambush1").transform, new GameObject("Ambush2").transform };
        _player = new GameObject("Player");

        _cameraLockCalled = false;
        _endGameCalled = false;
        _lastEnding = null;
        _endingClaimable = true;
        _hideStarted = _survived = 0;
        _shownThoughts = new List<string[]>();

        return new HideEndingController(
            _zones, _ambushPoints, _player.transform,
            FakeStartCoroutine,
            (lines, onComplete) => { _shownThoughts.Add(lines); _lastOnComplete = onComplete; },
            locked => { _cameraLockCalled = true; _cameraLockedValue = locked; },
            tryClaimEnding: () => _endingClaimable,
            endGame: ending => { _endGameCalled = true; _lastEnding = ending; },
            search: search,
            onHideStarted: () => _hideStarted++,
            onSurvived: () => _survived++);
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
        Assert.That(_lastEnding, Is.EqualTo(EndingId.Hidden));
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

    private HideEndingController HideInZone(FakeSearch search, int zone)
    {
        var controller = CreateController(search);
        controller.ActivateZones();
        controller.HandleZoneEntered(_zones[zone]);
        controller.HandleHideRequested();
        return controller;
    }

    [Test]
    public void Search_EnteringZoneOnlyShowsPrompt_UntilHideRequested()
    {
        var search = new FakeSearch();
        var controller = CreateController(search);
        controller.ActivateZones();

        controller.HandleZoneEntered(_zones[1]);
        Assert.That(search.Prompt, Is.EqualTo(1));
        Assert.That(search.Runs, Is.Zero);

        controller.HandleZoneExited(_zones[1]);
        Assert.That(search.Prompt, Is.EqualTo(-1));
        Assert.That(_endGameCalled, Is.False, "walking past a hiding spot must not trigger the ambush");

        controller.HandleHideRequested();
        Assert.That(search.Runs, Is.Zero, "E away from a hiding spot does nothing");
    }

    [Test]
    public void Search_HideRequested_StartsSearchAndHidesPrompt()
    {
        var search = new FakeSearch();
        var controller = HideInZone(search, 1);

        Assert.That(search.Runs, Is.EqualTo(1));
        Assert.That(search.Prompt, Is.EqualTo(-1));
        Assert.That(controller.IsSearching, Is.True);
        controller.HandleHideRequested();
        Assert.That(search.Runs, Is.EqualTo(1), "already hiding");
        Assert.That(_hideStarted, Is.EqualTo(1), "hide quest completes");
    }

    [Test]
    public void Search_FoundByMonster_AmbushesAndEndsWithHidden()
    {
        var search = new FakeSearch();
        HideInZone(search, 1);

        search.Finished(true);

        Assert.That(_cameraLockedValue, Is.True);
        _lastOnComplete?.Invoke();
        Assert.That(_lastEnding, Is.EqualTo(EndingId.Hidden));
    }

    [Test]
    public void Search_Survived_ClosesZonesWithoutEnding()
    {
        var search = new FakeSearch();
        var controller = HideInZone(search, 0);

        search.Finished(false);
        controller.HandleZoneExited(_zones[0]);

        Assert.That(_endGameCalled, Is.False);
        Assert.That(_cameraLockCalled, Is.False);
        Assert.That(_zones[0].activeSelf, Is.False);
        Assert.That(controller.ZonesOpen, Is.False);
        Assert.That(_survived, Is.EqualTo(1), "story goes on to the hatch");
        Assert.That(_shownThoughts, Is.Empty, "the story callback owns the hero's lines");
    }

    [Test]
    public void Search_ZoneExitWhileHiding_IsIgnored()
    {
        var search = new FakeSearch();
        var controller = HideInZone(search, 0);

        controller.HandleZoneExited(_zones[0]); // мини-игра сама двигает игрока в укрытие

        Assert.That(controller.IsSearching, Is.True);
        Assert.That(_endGameCalled, Is.False);
        Assert.That(search.Stops, Is.Zero);
    }

    [Test]
    public void Search_BranchTakenElsewhere_StopsSearch()
    {
        var search = new FakeSearch();
        var controller = HideInZone(search, 0);

        controller.SetZonesActive(false);
        search.Finished(true);

        Assert.That(search.Stops, Is.EqualTo(1));
        Assert.That(_endGameCalled, Is.False);
        Assert.That(_cameraLockCalled, Is.False);
    }

    [Test]
    public void Search_CaughtInOpen_EndsWithHidden()
    {
        var search = new FakeSearch();
        var controller = CreateController(search);
        controller.ActivateZones();

        controller.HandleCaughtInOpen();

        Assert.That(_cameraLockedValue, Is.True);
        _lastOnComplete?.Invoke();
        Assert.That(_lastEnding, Is.EqualTo(EndingId.Hidden));
        Assert.That(controller.ZonesOpen, Is.False);
    }

    [Test]
    public void Search_CaughtInOpen_IgnoredWhileHiding()
    {
        var search = new FakeSearch();
        var controller = HideInZone(search, 0);

        controller.HandleCaughtInOpen();

        Assert.That(_endGameCalled || _cameraLockCalled, Is.False, "already under the bed");
        Assert.That(controller.IsSearching, Is.True);
    }
}
