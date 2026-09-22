using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class BranchEndingControllerTests
{
    private GameObject _escapeDoorTrigger;
    private GameObject _player;
    private bool _hammerPathActivated;
    private bool _hidePathsActivated;
    private bool _endGameCalled;
    private string _lastEndingTitle;
    private bool _cameraLockCalled;
    private bool _cameraLockedValue;
    private List<string[]> _shownThoughts;
    private Action _lastOnComplete;

    // Как и в HideEndingControllerTests — прогоняет корутину синхронно, без Play Mode.
    static Coroutine FakeStartCoroutine(IEnumerator routine)
    {
        while (routine.MoveNext()) { }
        return null;
    }

    private BranchEndingController CreateController()
    {
        _escapeDoorTrigger = new GameObject("EscapeDoorTrigger");
        _escapeDoorTrigger.SetActive(false);
        _player = new GameObject("Player");
        _hammerPathActivated = false;
        _hidePathsActivated = false;
        _endGameCalled = false;
        _lastEndingTitle = null;
        _cameraLockCalled = false;
        _shownThoughts = new List<string[]>();

        return new BranchEndingController(
            activateHammerPath: () => _hammerPathActivated = true,
            escapeDoorTrigger: _escapeDoorTrigger,
            setHidePathsActive: active => _hidePathsActivated = active,
            playerTransform: _player.transform,
            startCoroutine: FakeStartCoroutine,
            showThoughts: (lines, onComplete) => { _shownThoughts.Add(lines); _lastOnComplete = onComplete; },
            lockCamera: locked => { _cameraLockCalled = true; _cameraLockedValue = locked; },
            endGame: title => { _endGameCalled = true; _lastEndingTitle = title; });
    }

    [TearDown]
    public void TearDown()
    {
        GameState.frontDoorLocked = false;
        if (_escapeDoorTrigger != null) UnityEngine.Object.DestroyImmediate(_escapeDoorTrigger);
        if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
    }

    [Test]
    public void Activate_AlwaysActivatesHammerPath()
    {
        var controller = CreateController();

        controller.Activate();

        Assert.That(_hammerPathActivated, Is.True);
    }

    [Test]
    public void Activate_AlwaysActivatesHidePaths()
    {
        var controller = CreateController();

        controller.Activate();

        Assert.That(_hidePathsActivated, Is.True);
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
    public void HandleDoorEscape_DeactivatesTriggerLocksCameraAndEndsGameOnce()
    {
        GameState.frontDoorLocked = false;
        var controller = CreateController();
        controller.Activate();

        controller.HandleDoorEscape();
        Assert.That(_escapeDoorTrigger.activeSelf, Is.False);
        Assert.That(_shownThoughts.Count, Is.EqualTo(1));
        // T-21: раньше камера не блокировалась во время финальной реплики — за это время
        // можно было успеть дойти до дыры и запустить конкурирующую концовку.
        Assert.That(_cameraLockCalled, Is.True);
        Assert.That(_cameraLockedValue, Is.True);

        _lastOnComplete?.Invoke();
        Assert.That(_endGameCalled, Is.True);
        Assert.That(_lastEndingTitle, Is.Not.Null.And.Not.Empty);
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

    // T-21: четвёртая концовка — таймер у дыры истёк, игрок не успел заколотить.
    [Test]
    public void HandleTimerExpired_LocksCameraAndEndsGame()
    {
        var controller = CreateController();
        controller.Activate();

        controller.HandleTimerExpired();

        Assert.That(_cameraLockCalled, Is.True);
        Assert.That(_cameraLockedValue, Is.True);
        Assert.That(_shownThoughts, Is.Not.Empty);

        _lastOnComplete?.Invoke();
        Assert.That(_endGameCalled, Is.True);
        Assert.That(_lastEndingTitle, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void HandleTimerExpired_IgnoredWhenAnotherEndingAlreadyClaimedTheBranch()
    {
        var controller = CreateController();
        controller.Activate();
        controller.HandleDoorEscape(); // claims the branch first
        _shownThoughts.Clear();
        _cameraLockCalled = false;

        controller.HandleTimerExpired();

        Assert.That(_shownThoughts, Is.Empty);
    }

    // T-21: единая точка, через которую проходят все концовки ветки (кроме молотка, у него
    // свой путь через IntroSequence.TryClaimEnding) — TryResolve не должна падать, даже если
    // MonsterTimer.Instance ещё не существует (в EditMode-тестах его нет вообще).
    [Test]
    public void TryResolve_DoesNotThrow_WhenMonsterTimerInstanceMissing()
    {
        var controller = CreateController();
        controller.Activate();

        Assert.DoesNotThrow(() => controller.HandleDoorEscape());
    }
}
