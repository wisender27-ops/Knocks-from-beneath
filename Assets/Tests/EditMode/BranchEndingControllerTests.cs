using System.Collections.Generic;
using NUnit.Framework;
using KnocksFromBeneath;

public class BranchEndingControllerTests
{
    private bool _hammer;
    private bool _hide;
    private bool _exitsOpen;
    private int _cameraLocks;
    private List<EndingId> _endings;

    private BranchEndingController CreateController()
    {
        _hammer = false;
        _hide = true;
        _exitsOpen = false;
        _cameraLocks = 0;
        _endings = new List<EndingId>();
        return new BranchEndingController(
            () => _hammer = true, open => _exitsOpen = open, active => _hide = active,
            locked => _cameraLocks++, ending => _endings.Add(ending));
    }

    [TearDown]
    public void TearDown()
    {
        GameState.frontDoorLocked = false;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Activate_EnablesHammerAndKeepsExitsClosed(bool doorLocked)
    {
        var controller = CreateController();
        GameState.frontDoorLocked = doorLocked;

        controller.Activate();

        Assert.That(_hammer, Is.True);
        Assert.That(_hide, Is.False);
        Assert.That(_exitsOpen, Is.False);
        Assert.That(controller.ExitsOpen, Is.False);
        Assert.That(controller.DeadlineExpired, Is.False);
    }

    [Test]
    public void Exits_BeforeOpenExits_DoNothing()
    {
        var controller = CreateController();
        controller.Activate();

        controller.HandleBackyardReached();
        controller.HandleRoadReached();

        Assert.That(_endings, Is.Empty);
        Assert.That(_cameraLocks, Is.Zero);
        Assert.That(controller.TryResolve(), Is.True);
    }

    [Test]
    public void Backyard_AfterOpenExits_EndsWithE3AndClaimsBranch()
    {
        var controller = CreateController();
        controller.Activate();
        controller.OpenExits();
        Assert.That(_exitsOpen, Is.True);

        controller.HandleBackyardReached();
        controller.HandleRoadReached();

        Assert.That(_endings, Is.EqualTo(new[] { EndingId.Hidden }));
        Assert.That(_cameraLocks, Is.EqualTo(1));
        Assert.That(_exitsOpen, Is.False);
        Assert.That(controller.TryResolve(), Is.False);
    }

    [Test]
    public void Road_AfterOpenExits_EndsWithE4EvenAfterDeadline()
    {
        var controller = CreateController();
        controller.Activate();
        controller.HandleTimerExpired();
        controller.OpenExits();

        controller.HandleRoadReached();

        Assert.That(_endings, Is.EqualTo(new[] { EndingId.Escaped }));
        Assert.That(controller.TryResolve(), Is.False);
    }

    [Test]
    public void HatchClaim_ClosesExits()
    {
        var controller = CreateController();
        controller.Activate();
        controller.OpenExits();

        Assert.That(controller.TryResolve(), Is.True);
        controller.HandleRoadReached();

        Assert.That(_exitsOpen, Is.False);
        Assert.That(_endings, Is.Empty);
    }

    [Test]
    public void OpenExits_AfterResolveStaysClosed()
    {
        var controller = CreateController();
        controller.Activate();
        Assert.That(controller.TryResolve(), Is.True);

        controller.OpenExits();

        Assert.That(_exitsOpen, Is.False);
        Assert.That(controller.ExitsOpen, Is.False);
    }

    [Test]
    public void HandleTimerExpired_OnlyMarksDeadlineAndLeavesFinaleAvailable()
    {
        var controller = CreateController();
        controller.Activate();

        controller.HandleTimerExpired();
        controller.HandleTimerExpired();

        Assert.That(controller.DeadlineExpired, Is.True);
        Assert.That(_endings, Is.Empty);
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
        Assert.That(_endings, Is.Empty);
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
}
