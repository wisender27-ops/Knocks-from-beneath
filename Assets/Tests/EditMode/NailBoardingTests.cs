using NUnit.Framework;
using KnocksFromBeneath;

public class NailBoardingTests
{
    // Крутит качание мелкими шагами, пока гвоздь не встанет ровно (или не отклонится).
    private static void WaitUntil(NailBoarding game, bool upright)
    {
        for (int i = 0; i < 2000 && game.IsUpright != upright; i++) game.Advance(0.002f);
        Assert.That(game.IsUpright, Is.EqualTo(upright));
    }

    [Test]
    public void NewNail_StartsTilted()
    {
        var game = new NailBoarding();
        Assert.That(game.IsUpright, Is.False);
        Assert.That(game.Angle, Is.EqualTo(NailBoarding.Amplitude).Within(1e-3f));
    }

    [Test]
    public void UprightHits_DriveNails()
    {
        var game = new NailBoarding();
        WaitUntil(game, true);
        Assert.That(game.Strike(), Is.EqualTo(NailStrike.Drove));
        Assert.That(game.HitsOnNail, Is.EqualTo(1));
        WaitUntil(game, false);
        WaitUntil(game, true);
        Assert.That(game.Strike(), Is.EqualTo(NailStrike.NailDone));
        Assert.That(game.NailsDone, Is.EqualTo(1));
        Assert.That(game.HitsOnNail, Is.EqualTo(0));
        Assert.That(game.IsUpright, Is.False, "Новый гвоздь снова качается с наклона");
    }

    [Test]
    public void TiltedHit_BendsNailAndCostsTime()
    {
        var game = new NailBoarding();
        WaitUntil(game, true);
        game.Strike();
        WaitUntil(game, false);
        float pressure = game.Pressure;
        Assert.That(game.Strike(), Is.EqualTo(NailStrike.Bent));
        Assert.That(game.Bent, Is.EqualTo(1));
        Assert.That(game.HitsOnNail, Is.EqualTo(0), "Погнутый гвоздь начинается заново");
        Assert.That(game.Pressure, Is.EqualTo(pressure + NailBoarding.MissCost).Within(1e-4f));
    }

    [Test]
    public void AllNails_Succeed()
    {
        var game = new NailBoarding();
        for (int hit = 0; hit < NailBoarding.NailCount * NailBoarding.HitsPerNail; hit++)
        {
            WaitUntil(game, false);
            WaitUntil(game, true);
            game.Strike();
        }
        Assert.That(game.Succeeded, Is.True);
        Assert.That(game.Failed, Is.False);
        float pressure = game.Pressure;
        game.Advance(100f);
        Assert.That(game.Pressure, Is.EqualTo(pressure), "После успеха время стоит");
    }

    [Test]
    public void BudgetRunsOut_BoardsBroken()
    {
        var game = new NailBoarding();
        game.Advance(NailBoarding.Budget - 0.01f);
        Assert.That(game.Failed, Is.False);
        game.Advance(0.02f);
        Assert.That(game.Failed, Is.True);
        Assert.That(game.Finished, Is.True);
    }

    [Test]
    public void Misses_BreakBoardsSooner()
    {
        var game = new NailBoarding();
        int misses = 0;
        while (!game.Failed && misses < 20)
        {
            game.Strike(); // гвоздь стоит с наклоном — каждый удар мимо
            misses++;
        }
        Assert.That(game.Failed, Is.True);
        Assert.That(misses, Is.EqualTo((int)System.Math.Ceiling(NailBoarding.Budget / NailBoarding.MissCost)));
    }

    [Test]
    public void Wobble_SpeedsUpUnderPressure()
    {
        var late = new NailBoarding();
        late.Advance(NailBoarding.Budget * 0.75f);
        Assert.That(HalfSwing(late), Is.LessThan(HalfSwing(new NailBoarding()) * 0.85f));
    }

    // Время между двумя соседними моментами, когда гвоздь проходит вертикаль.
    private static float HalfSwing(NailBoarding game)
    {
        WaitUntil(game, true);
        WaitUntil(game, false);
        float time = 0f;
        for (; time < 5f && !game.IsUpright; time += 0.002f) game.Advance(0.002f);
        return time;
    }
}
