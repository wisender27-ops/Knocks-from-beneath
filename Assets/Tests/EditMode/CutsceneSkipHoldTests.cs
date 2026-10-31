using NUnit.Framework;
using KnocksFromBeneath;

public class CutsceneSkipHoldTests
{
    const float Dt = 0.05f;

    static bool Run(CutsceneSkipHold hold, float seconds, bool held)
    {
        bool done = false;
        for (float t = 0f; t < seconds; t += Dt) done = hold.Advance(Dt, false, held);
        return done;
    }

    [Test]
    public void HoldingForFullTime_Completes()
    {
        var hold = new CutsceneSkipHold();
        hold.Advance(Dt, true, true);

        Assert.That(Run(hold, CutsceneSkipHold.HoldSeconds - 0.3f, true), Is.False);
        Assert.That(Run(hold, 0.5f, true), Is.True);
        Assert.That(hold.Completed, Is.True);
    }

    [Test]
    public void ShortTap_DoesNotComplete_AndDrainsBack()
    {
        var hold = new CutsceneSkipHold();
        hold.Advance(Dt, true, true);
        Run(hold, 0.2f, true);
        Assert.That(hold.Progress, Is.GreaterThan(0f));

        Run(hold, CutsceneSkipHold.DrainSeconds, false);

        Assert.That(hold.Progress, Is.Zero);
        Assert.That(hold.Completed, Is.False);
    }

    [Test]
    public void KeyHeldWithoutPress_IsIgnored()
    {
        var hold = new CutsceneSkipHold();

        Assert.That(Run(hold, CutsceneSkipHold.HoldSeconds * 2f, true), Is.False);
        Assert.That(hold.Progress, Is.Zero);
    }

    [Test]
    public void Interruption_RequiresNewPress()
    {
        var hold = new CutsceneSkipHold();
        hold.Advance(Dt, true, true);
        Run(hold, 0.5f, true);
        hold.Advance(Dt, false, false);
        float interrupted = hold.Progress;

        Run(hold, 0.2f, true);

        Assert.That(hold.Progress, Is.LessThan(interrupted), "the key still down after a pause must not resume the hold");
    }
}
