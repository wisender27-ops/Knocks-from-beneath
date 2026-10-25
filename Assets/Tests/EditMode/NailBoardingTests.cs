using NUnit.Framework;
using KnocksFromBeneath;

public class NailBoardingTests
{
    private static NailBoarding Open(int nails = NailBoarding.NailCount)
    {
        var game = new NailBoarding();
        game.OpenNails(nails);
        return game;
    }

    [Test]
    public void FullSwings_DriveNailInTwoHits()
    {
        var game = Open(2);
        Assert.That(game.Strike(0, 0f, 1f), Is.EqualTo(NailStrike.Drove));
        Assert.That(game.Depth(0), Is.EqualTo(NailBoarding.TapDepth + NailBoarding.PowerDepth).Within(1e-4f));
        Assert.That(game.Strike(0, 0.03f, 1f), Is.EqualTo(NailStrike.NailDone));
        Assert.That(game.IsDone(0), Is.True);
        Assert.That(game.Strike(0, 0f, 1f), Is.EqualTo(NailStrike.Miss), "Забитый гвоздь больше не бьётся");
    }

    [Test]
    public void Taps_TakeMoreHits()
    {
        var game = Open(1);
        int hits = 0;
        while (!game.IsDone(0) && hits < 20) { game.Strike(0, 0f, 0f); hits++; }
        Assert.That(hits, Is.EqualTo(5));
    }

    [Test]
    public void Windup_GrowsPower()
    {
        Assert.That(NailBoarding.Power(0f), Is.EqualTo(0f));
        Assert.That(NailBoarding.Power(NailBoarding.FullWindup * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(NailBoarding.Power(5f), Is.EqualTo(1f));
    }

    [Test]
    public void Aim_DecidesHitGlanceOrMiss()
    {
        var game = Open(1);
        Assert.That(game.Strike(0, NailBoarding.GlanceRadius * 0.9f, 1f), Is.EqualTo(NailStrike.Glance));
        Assert.That(game.Strike(0, NailBoarding.GlanceRadius * 1.5f, 1f), Is.EqualTo(NailStrike.Miss));
        Assert.That(game.Depth(0), Is.EqualTo(0f), "Мимо шляпки гвоздь не идёт");
        Assert.That(game.Strike(0, NailBoarding.HitRadius * 0.9f, 1f), Is.EqualTo(NailStrike.Drove));
    }

    [Test]
    public void ClosedBoard_NailsNotAvailable()
    {
        var game = Open(2);
        Assert.That(game.Strike(2, 0f, 1f), Is.EqualTo(NailStrike.Miss));
        game.OpenNails(4);
        Assert.That(game.Strike(2, 0f, 1f), Is.EqualTo(NailStrike.Drove));
    }

    [Test]
    public void Heave_PullsHalfDrivenNailsAndHurtsBoards()
    {
        var game = Open(2);
        game.Strike(0, 0f, 1f);
        game.Strike(1, 0f, 1f);
        game.Strike(1, 0f, 1f);
        float half = game.Depth(0);
        Assert.That(game.Advance(NailBoarding.FirstHeave - 0.01f), Is.False);
        Assert.That(game.HeaveWarning, Is.True, "Перед толчком — предупреждение");
        Assert.That(game.Advance(0.02f), Is.True);
        Assert.That(game.Depth(0), Is.LessThan(half), "Недобитый гвоздь выдернуло");
        Assert.That(game.Depth(1), Is.EqualTo(1f), "Забитый держит");
        Assert.That(game.Integrity, Is.LessThan(1f));
        Assert.That(game.LastLift, Is.LessThan(game.HeaveStrength), "Часть гвоздей держит — створки подлетают ниже");
    }

    [Test]
    public void Noise_BringsHeaveSooner()
    {
        var game = Open(2);
        float before = game.UntilHeave;
        game.Provoke(1.5f);
        Assert.That(game.UntilHeave, Is.EqualTo(before - 1.5f).Within(1e-4f));
        game.Provoke(100f);
        Assert.That(game.UntilHeave, Is.EqualTo(NailBoarding.WarnSeconds).Within(1e-4f), "Скрежет успевает предупредить");
    }

    [Test]
    public void DoingNothing_BoardsBreakInAboutHalfAMinute()
    {
        var game = Open(2);
        float time = 0f;
        while (!game.Finished && time < 120f) { game.Advance(0.02f); time += 0.02f; }
        Assert.That(game.Failed, Is.True);
        Assert.That(time, Is.InRange(25f, 50f));
    }

    [Test]
    public void SloppyPlayer_StillBoardsUp()
    {
        // Удар раз в 1.2 с, каждый третий — мимо, сила замаха половинная.
        var game = Open(2);
        float time = 0f, nextSwing = 1.2f;
        int swing = 0;
        while (!game.Finished && time < 120f)
        {
            game.Advance(0.02f);
            time += 0.02f;
            if (time < nextSwing) continue;
            nextSwing += 1.2f;
            int nail = 0;
            while (nail < NailBoarding.NailCount - 1 && game.IsDone(nail)) nail++;
            if (nail >= 2) game.OpenNails(4);
            game.Strike(nail, ++swing % 3 == 0 ? 0.2f : 0.02f, 0.5f);
        }
        Assert.That(game.Succeeded, Is.True, $"Время {time:0.0}, прочность {game.Integrity:0.00}");
        Assert.That(game.Integrity, Is.LessThan(1f), "Но доски успели пострадать");
    }

    [Test]
    public void Heaves_SpeedUpOverTime()
    {
        var early = Open(1);
        early.Advance(NailBoarding.FirstHeave + 0.01f);
        float earlyGap = early.UntilHeave;
        var late = Open(1);
        late.Advance(NailBoarding.RampSeconds);
        late.Advance(late.UntilHeave + 0.01f);
        Assert.That(late.UntilHeave, Is.LessThan(earlyGap));
    }
}
