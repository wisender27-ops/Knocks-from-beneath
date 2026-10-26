using NUnit.Framework;
using KnocksFromBeneath;

public class BreathHoldTests
{
    const float Dt = 0.05f;
    const float Far = 20f;

    static void Run(BreathHold breath, float seconds, bool hold, float distance, bool moving = false)
    {
        for (float t = 0f; t < seconds; t += Dt) breath.Advance(Dt, hold, distance, moving);
    }

    [Test]
    public void Holding_DrainsAirInSevenSeconds_ThenGasps()
    {
        var breath = new BreathHold();
        Run(breath, BreathHold.HoldSeconds - 0.5f, true, Far);
        Assert.That(breath.Holding, Is.True);
        Assert.That(breath.Gasps, Is.Zero);

        Run(breath, 1f, true, Far);

        Assert.That(breath.Gasps, Is.EqualTo(1));
        Assert.That(breath.Holding, Is.False, "after a gasp the hero can't hold for a moment");
    }

    [Test]
    public void Releasing_RecoversAir()
    {
        var breath = new BreathHold();
        Run(breath, 4f, true, Far);
        float low = breath.Air;

        Run(breath, BreathHold.RecoverSeconds, false, Far);

        Assert.That(low, Is.LessThan(0.5f));
        Assert.That(breath.Air, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void BreathingNextToMonster_GetsFound()
    {
        var breath = new BreathHold();
        Run(breath, 6f, false, 1.1f);
        Assert.That(breath.Found, Is.True);
    }

    [Test]
    public void HoldingNextToMonster_StaysHidden()
    {
        var breath = new BreathHold();
        Run(breath, 4f, true, 1.1f);
        Assert.That(breath.Found, Is.False);
        Assert.That(breath.Suspicion, Is.LessThan(0.1f));
    }

    [Test]
    public void BreathingWhileMonsterIsFar_IsNotHeard()
    {
        var breath = new BreathHold();
        Run(breath, 30f, false, BreathHold.HearingRadius + 1f);
        Assert.That(breath.Suspicion, Is.Zero);
        Assert.That(breath.Found, Is.False);
    }

    [Test]
    public void GaspNearMonster_RaisesSuspicionSharply()
    {
        var breath = new BreathHold();
        Run(breath, BreathHold.HoldSeconds + 0.2f, true, 1.6f);
        Assert.That(breath.Gasps, Is.EqualTo(1));
        Assert.That(breath.Suspicion, Is.GreaterThan(0.4f));
    }

    [Test]
    public void MonsterNearby_RaisesPulse()
    {
        var calm = new BreathHold();
        var scared = new BreathHold();
        Run(calm, 2f, true, Far);
        Run(scared, 2f, true, 1f);
        Assert.That(scared.Pulse, Is.GreaterThan(calm.Pulse + 0.3f));
    }

    [Test]
    public void MovingInHiding_IsNoisyEvenWhileHolding()
    {
        var breath = new BreathHold();
        breath.Advance(Dt, true, Far, moving: true);
        Assert.That(breath.Noise, Is.GreaterThanOrEqualTo(0.6f));
    }
}
