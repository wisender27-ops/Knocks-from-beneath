using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class KnockReplyTests
{
    private static KnockReply TapSeries(int taps, float interval = 0.3f)
    {
        var reply = new KnockReply();
        for (int i = 0; i < taps; i++) reply.Tap(i * interval);
        return reply;
    }

    [Test]
    public void SeriesIsNotAnsweredUntilPlayerPauses()
    {
        var reply = TapSeries(2);
        Assert.That(reply.Poll(0.3f + reply.BurstGap * 0.5f, out _), Is.EqualTo(KnockReplyKind.None));
        Assert.That(reply.IsListening, Is.True);
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(4)]
    public void ShortSeries_IsRepeatedByCount(int taps)
    {
        var reply = TapSeries(taps);
        float end = (taps - 1) * 0.3f + reply.BurstGap + 0.01f;

        Assert.That(reply.Poll(end, out int count), Is.EqualTo(KnockReplyKind.Repeat));
        Assert.That(count, Is.EqualTo(taps));
        Assert.That(reply.Poll(end + 5f, out _), Is.EqualTo(KnockReplyKind.None), "Решение выдаётся один раз");
    }

    [Test]
    public void FranticSeries_MakesKnockerGoSilent()
    {
        var reply = TapSeries(7, 0.2f);
        Assert.That(reply.Poll(10f, out int count), Is.EqualTo(KnockReplyKind.Silence));
        Assert.That(count, Is.EqualTo(7));
    }

    [Test]
    public void LongPause_SplitsIntoTwoSeries()
    {
        var reply = new KnockReply();
        reply.Tap(0f);
        reply.Tap(0.3f);
        Assert.That(reply.Poll(0.3f + reply.BurstGap + 0.01f, out int first), Is.EqualTo(KnockReplyKind.Repeat));
        reply.Tap(5f);
        Assert.That(reply.Poll(5f + reply.BurstGap + 0.01f, out int second), Is.EqualTo(KnockReplyKind.Repeat));
        Assert.That(first, Is.EqualTo(2));
        Assert.That(second, Is.EqualTo(1));
    }

    [Test]
    public void Hear_PointsTrackerAtKnockImmediately()
    {
        var tracker = new KnockTracker();
        tracker.Reset(Vector2.zero);
        tracker.Hear(new Vector2(5f, 0f));

        Assert.That(tracker.HasPlayer, Is.True);
        Assert.That(tracker.KnownTarget, Is.EqualTo(new Vector2(5f, 0f)));
        tracker.Step(new Vector2(5f, 0f), KnockNoise.Quiet, 1f);
        Assert.That(tracker.Source.x, Is.GreaterThan(0f), "Стучащий идёт к месту стука даже в тишине");
    }
}
