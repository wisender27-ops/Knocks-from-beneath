using NUnit.Framework;
using KnocksFromBeneath;

public sealed class DeadlineScaleTests
{
    [Test]
    public void Blows_GetMoreFrequentAndStronger()
    {
        Assert.Greater(DeadlineScale.BlowInterval(0f), DeadlineScale.BlowInterval(0.5f));
        Assert.Greater(DeadlineScale.BlowInterval(0.5f), DeadlineScale.BlowInterval(1f));
        Assert.Less(DeadlineScale.BlowStrength(0f), DeadlineScale.BlowStrength(1f));
    }

    [Test]
    public void Body_Accelerates()
    {
        Assert.Greater(DeadlineScale.HeartInterval(0f), DeadlineScale.HeartInterval(1f));
        Assert.Greater(DeadlineScale.BreathInterval(0f), DeadlineScale.BreathInterval(1f));
    }

    [TestCase(0f, OwnerKnockMood.Ordered)]
    [TestCase(0.39f, OwnerKnockMood.Ordered)]
    [TestCase(0.4f, OwnerKnockMood.Frantic)]
    [TestCase(0.71f, OwnerKnockMood.Frantic)]
    [TestCase(0.72f, OwnerKnockMood.Desperate)]
    public void OwnerKnock_MoodFollowsScale(float tension, OwnerKnockMood mood)
    {
        Assert.AreEqual(mood, DeadlineScale.Mood(tension));
    }

    [Test]
    public void OwnerKnock_OrderedRepeatsTwoOne()
    {
        Assert.AreEqual(DeadlineScale.KnockGap(OwnerKnockMood.Ordered, 0, 0f), DeadlineScale.KnockGap(OwnerKnockMood.Ordered, 3, 1f));
        Assert.Greater(DeadlineScale.KnockGap(OwnerKnockMood.Ordered, 2, 0f), DeadlineScale.KnockGap(OwnerKnockMood.Ordered, 1, 0f));
    }

    [Test]
    public void OwnerKnock_DesperateIsFasterThanFrantic()
    {
        Assert.Less(DeadlineScale.KnockGap(OwnerKnockMood.Desperate, 0, 1f), DeadlineScale.KnockGap(OwnerKnockMood.Frantic, 0, 1f));
    }

    [TestCase(3.01f, false)]
    [TestCase(3f, true)]
    [TestCase(0.5f, true)]
    [TestCase(0f, false)]
    public void Silence_OnlyInLastSeconds(float remaining, bool silent)
    {
        Assert.AreEqual(silent, DeadlineScale.IsSilent(remaining));
    }

    [Test]
    public void Flicker_ThresholdFallsTowardsEnd()
    {
        Assert.Greater(DeadlineScale.FlickerThreshold(DeadlineScale.FlickerFrom), DeadlineScale.FlickerThreshold(1f));
    }
}
