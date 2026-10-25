using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class KnockTrackerTests
{
    private static KnockTracker Create()
    {
        var tracker = new KnockTracker();
        tracker.Reset(Vector2.zero);
        return tracker;
    }

    private static void Run(KnockTracker tracker, Vector2 player, KnockNoise noise, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.05f) tracker.Step(player, noise, 0.05f);
    }

    [Test]
    public void Quiet_PlayerIsNeverHeard()
    {
        var tracker = Create();
        Run(tracker, new Vector2(8f, 0f), KnockNoise.Quiet, 10f);

        Assert.That(tracker.HasPlayer, Is.False);
        Assert.That(tracker.Source, Is.EqualTo(Vector2.zero));
    }

    [Test]
    public void Walking_IsHeardWithDelayAndSourceLagsBehind()
    {
        var tracker = Create();
        var player = new Vector2(8f, 0f);
        Run(tracker, player, KnockNoise.Walk, 1f);
        Assert.That(tracker.HasPlayer, Is.False, "шаг не слышен сразу");

        Run(tracker, player, KnockNoise.Walk, 2f);
        Assert.That(tracker.HasPlayer, Is.True);
        Assert.That(tracker.Source.x, Is.GreaterThan(0f).And.LessThan(8f), "источник ползёт, а не прыгает");
    }

    [Test]
    public void Running_FindsPlayerFasterThanWalking()
    {
        var walker = Create();
        var runner = Create();
        var player = new Vector2(10f, 0f);
        Run(walker, player, KnockNoise.Walk, 4f);
        Run(runner, player, KnockNoise.Run, 4f);

        Assert.That(runner.HasPlayer, Is.True);
        Assert.That(Vector2.Distance(runner.Source, player), Is.LessThan(Vector2.Distance(walker.Source, player)));
    }

    [Test]
    public void Crouching_LosesPlayerAndStopsAtLastKnownSpot()
    {
        var tracker = Create();
        Run(tracker, new Vector2(3f, 0f), KnockNoise.Run, 4f);
        Assert.That(tracker.HasPlayer, Is.True);

        Run(tracker, new Vector2(-6f, 0f), KnockNoise.Quiet, 6f);
        Assert.That(tracker.HasPlayer, Is.False);
        Assert.That(tracker.KnownTarget, Is.EqualTo(new Vector2(3f, 0f)), "тихий игрок не выдаёт новую позицию");
        Assert.That(tracker.Source.x, Is.GreaterThan(2f));
    }

    [Test]
    public void Source_StaysInsideBounds()
    {
        var tracker = new KnockTracker { BoundsMin = new Vector2(-2f, -2f), BoundsMax = new Vector2(2f, 2f) };
        tracker.Reset(Vector2.zero);
        Run(tracker, new Vector2(30f, -30f), KnockNoise.Run, 30f);

        Assert.That(tracker.Source.x, Is.LessThanOrEqualTo(2f));
        Assert.That(tracker.Source.y, Is.GreaterThanOrEqualTo(-2f));
    }
}
