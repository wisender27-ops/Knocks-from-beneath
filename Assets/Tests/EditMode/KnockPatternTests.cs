using NUnit.Framework;
using KnocksFromBeneath;

public class KnockPatternTests
{
    // Удары по паузам между ними: Times(0.3f, 1f) → 0, 0.3, 1.3.
    private static float[] Times(params float[] gaps)
    {
        var taps = new float[gaps.Length + 1];
        for (int i = 0; i < gaps.Length; i++) taps[i + 1] = taps[i] + gaps[i];
        return taps;
    }

    [Test]
    public void Pattern_GapsFollowGroups()
    {
        var pattern = new KnockPattern(3, 2, 1, 2);
        Assert.That(pattern.TapCount, Is.EqualTo(5));
        Assert.That(pattern.GapBefore(1), Is.EqualTo(KnockPattern.ShortGap));
        Assert.That(pattern.GapBefore(2), Is.EqualTo(KnockPattern.LongGap));
        Assert.That(pattern.GapBefore(3), Is.EqualTo(KnockPattern.LongGap));
        Assert.That(pattern.GapBefore(4), Is.EqualTo(KnockPattern.ShortGap));
        Assert.That(pattern.Duration, Is.EqualTo(2f * KnockPattern.ShortGap + 2f * KnockPattern.LongGap).Within(1e-4f));
    }

    [Test]
    public void LevelOne_ChecksOnlyCount()
    {
        var pattern = new KnockPattern(1, 3);
        Assert.That(pattern.Matches(Times(0.3f, 1.2f)), Is.True, "Ритм на первом уровне не важен");
        Assert.That(pattern.Matches(Times(0.3f)), Is.False);
        Assert.That(pattern.Matches(Times(0.3f, 0.3f, 0.3f)), Is.False);
    }

    [TestCase(0.25f, 0.8f)]  // быстро
    [TestCase(0.4f, 1f)]     // как сам хозяин
    [TestCase(0.6f, 1.3f)]   // медленный игрок — порог считается от его темпа
    public void LevelTwo_AcceptsRhythmWithinTolerance(float shortGap, float longGap)
    {
        var pattern = new KnockPattern(2, 2, 1);
        Assert.That(pattern.Matches(Times(shortGap, longGap)), Is.True);
    }

    [Test]
    public void LevelTwo_RejectsWrongPlaceOfPause()
    {
        var twoThenOne = new KnockPattern(2, 2, 1);
        Assert.That(twoThenOne.Matches(Times(1f, 0.3f)), Is.False, "1·2 вместо 2·1");
        Assert.That(twoThenOne.Matches(Times(0.4f, 0.45f)), Is.False, "Ровный стук без длинной паузы");
    }

    [Test]
    public void LevelThree_RecognisesThreeGroups()
    {
        var pattern = new KnockPattern(3, 1, 2, 1);
        Assert.That(pattern.Matches(Times(0.9f, 0.3f, 1.1f)), Is.True);
        Assert.That(pattern.Matches(Times(0.9f, 0.3f, 0.3f)), Is.False);
    }

    [Test]
    public void EveryLevelHasVariants()
    {
        for (int level = 1; level <= 3; level++)
        {
            Assert.That(KnockPattern.VariantCount(level), Is.GreaterThanOrEqualTo(2));
            Assert.That(KnockPattern.ForLevel(level, -1).Level, Is.EqualTo(level));
            Assert.That(KnockPattern.ForLevel(level, 0).GroupCount, Is.EqualTo(level == 3 ? 3 : level));
        }
    }

    [Test]
    public void Answer_WaitsForPauseThenJudges()
    {
        var answer = new KnockAnswer();
        answer.Begin(new KnockPattern(2, 1, 2), 10f);
        answer.Tap(11f);
        answer.Tap(12f);
        answer.Tap(12.3f);
        Assert.That(answer.Poll(12.3f + answer.EndGap * 0.5f), Is.EqualTo(KnockCallOutcome.None));
        Assert.That(answer.Poll(12.3f + answer.EndGap + 0.01f), Is.EqualTo(KnockCallOutcome.Correct));
    }

    [Test]
    public void Answer_SilenceIsIgnore()
    {
        var answer = new KnockAnswer();
        answer.Begin(new KnockPattern(1, 2), 0f);
        Assert.That(answer.Poll(answer.ResponseWindow - 0.1f), Is.EqualTo(KnockCallOutcome.None));
        Assert.That(answer.Poll(answer.ResponseWindow), Is.EqualTo(KnockCallOutcome.Ignored));
    }

    [Test]
    public void Answer_SpamIsWrongAtOnce()
    {
        var answer = new KnockAnswer();
        answer.Begin(new KnockPattern(1, 2), 0f);
        for (int i = 0; i < 5; i++) answer.Tap(1f + i * 0.2f);
        Assert.That(answer.Poll(1.8f), Is.EqualTo(KnockCallOutcome.Wrong));
    }

    [Test]
    public void Answer_WrongCountIsWrong()
    {
        var answer = new KnockAnswer();
        answer.Begin(new KnockPattern(1, 3), 0f);
        answer.Tap(1f);
        answer.Tap(1.3f);
        Assert.That(answer.Poll(1.3f + answer.EndGap + 0.01f), Is.EqualTo(KnockCallOutcome.Wrong));
    }
}
