using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class BranchEndingControllerTests
{
    private GameObject _escape;
    private bool _hammer;
    private bool _hide;
    private int _coroutines;
    private int _thoughts;
    private int _cameraLocks;
    private int _endings;

    private BranchEndingController CreateController()
    {
        _escape = new GameObject("EscapeDoorTrigger");
        _hide = true;
        return new BranchEndingController(
            () => _hammer = true, _escape, active => _hide = active, null,
            routine => { _coroutines++; return null; },
            (lines, complete) => { _thoughts++; complete?.Invoke(); },
            locked => _cameraLocks++, title => _endings++);
    }

    [TearDown]
    public void TearDown()
    {
        if (_escape != null) UnityEngine.Object.DestroyImmediate(_escape);
        GameState.frontDoorLocked = false;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Activate_EnablesHammerAndDisablesOtherRoutes(bool doorLocked)
    {
        var controller = CreateController();
        GameState.frontDoorLocked = doorLocked;

        controller.Activate();

        Assert.That(_hammer, Is.True);
        Assert.That(_hide, Is.False);
        Assert.That(_escape.activeSelf, Is.False);
        Assert.That(controller.DeadlineExpired, Is.False);
    }

    [Test]
    public void HandleDoorEscape_DoesNothingAndCannotClaimFinale()
    {
        var controller = CreateController();
        controller.Activate();

        controller.HandleDoorEscape();
        controller.HandleDoorEscape();

        AssertNoEndingSideEffects();
        Assert.That(controller.TryResolve(), Is.True);
    }

    [Test]
    public void HandleTimerExpired_OnlyMarksDeadlineAndLeavesFinaleAvailable()
    {
        var controller = CreateController();
        controller.Activate();

        controller.HandleTimerExpired();
        controller.HandleTimerExpired();

        Assert.That(controller.DeadlineExpired, Is.True);
        AssertNoEndingSideEffects();
        Assert.That(controller.TryResolve(), Is.True);
    }

    [Test]
    public void HandleTimerExpired_AfterResolveDoesNotChangeDeadline()
    {
        var controller = CreateController();
        controller.Activate();
        Assert.That(controller.TryResolve(), Is.True);

        controller.HandleTimerExpired();

        Assert.That(controller.DeadlineExpired, Is.False);
        AssertNoEndingSideEffects();
    }

    [Test]
    public void TryResolve_IsIdempotentUntilReactivated()
    {
        var controller = CreateController();
        controller.Activate();
        controller.HandleTimerExpired();

        Assert.That(controller.TryResolve(), Is.True);
        Assert.That(controller.TryResolve(), Is.False);
        controller.Activate();

        Assert.That(controller.DeadlineExpired, Is.False);
        Assert.That(controller.TryResolve(), Is.True);
        Assert.That(controller.TryResolve(), Is.False);
    }

    private void AssertNoEndingSideEffects()
    {
        Assert.That(_coroutines, Is.Zero);
        Assert.That(_thoughts, Is.Zero);
        Assert.That(_cameraLocks, Is.Zero);
        Assert.That(_endings, Is.Zero);
    }
}
