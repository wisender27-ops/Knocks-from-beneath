using NUnit.Framework;
using KnocksFromBeneath;

// K-05: мысли героя на стук снизу и сохранение факта ответа.
public class KnockReactionsTests
{
    [TearDown]
    public void TearDown() => StoryFlags.Reset();

    [Test]
    public void TrustLine_OnlyWhenThresholdIsCrossed()
    {
        Assert.That(KnockCallGame.TrustLine(2, 3), Is.Not.Null);
        Assert.That(KnockCallGame.TrustLine(3, 4), Is.Null, "Выше порога — без повторов");
        Assert.That(KnockCallGame.TrustLine(-1, -2), Is.Not.Null);
        Assert.That(KnockCallGame.TrustLine(-2, -3), Is.Null);
        Assert.That(KnockCallGame.TrustLine(0, 1), Is.Null);
        Assert.That(KnockCallGame.TrustLine(0, -1), Is.Null);
    }

    [Test]
    public void HeroReaction_FirstRepeatAndRageOnly()
    {
        Assert.That(KnockResponder.HeroReaction(KnockReplyKind.Repeat, true, false), Is.Not.Empty);
        Assert.That(KnockResponder.HeroReaction(KnockReplyKind.Repeat, false, false), Is.Null);
        Assert.That(KnockResponder.HeroReaction(KnockReplyKind.Silence, false, true), Is.Not.Empty);
        Assert.That(KnockResponder.HeroReaction(KnockReplyKind.Silence, true, false), Is.Null);
    }

    [Test]
    public void KnockAnswered_ResetsAndRestores()
    {
        StoryFlags.MarkKnockAnswered();
        Assert.That(StoryFlags.KnockAnswered, Is.True);
        StoryFlags.Reset();
        Assert.That(StoryFlags.KnockAnswered, Is.False);

        StoryFlags.Restore(1, false, false, false, null, knockAnswered: true);
        Assert.That(StoryFlags.KnockAnswered, Is.True);
        Assert.That(StoryFlags.KnockTrust, Is.EqualTo(1));
    }
}
