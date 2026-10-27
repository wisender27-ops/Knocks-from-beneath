using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class NightTapeTests
{
    [Test]
    public void Clock_JumpsAtCutsAndRunsInsideShots()
    {
        Assert.That(NightTape.Stamp(0f), Is.EqualTo("PM 11:41:07"));
        Assert.That(NightTape.Stamp(8.9f), Is.EqualTo("PM 11:41:15"));
        Assert.That(NightTape.Stamp(9f), Is.EqualTo("AM  2:46:31"));
        Assert.That(NightTape.ClockSeconds(20f) - NightTape.ClockSeconds(10f), Is.EqualTo(10));
        Assert.That(NightTape.ShotIndex(49.9f), Is.EqualTo(1));
        Assert.That(NightTape.ShotIndex(50f), Is.EqualTo(2));
    }

    [Test]
    public void Figure_IsHiddenBeforeEntranceAndLooksIntoLensOnlyWhileStanding()
    {
        Assert.That(NightTape.Figure(NightTape.FirstSighting - 0.1f).Visible, Is.False);
        TapeFigurePose face = NightTape.Figure(NightTape.FaceToLens + 0.5f);
        Assert.That(face.Visible, Is.True);
        Assert.That(face.Look, Is.EqualTo(1f));
        Assert.That(face.Walk, Is.LessThan(0.05f));
        Assert.That(NightTape.Figure(26f).Look, Is.EqualTo(0f));
        Assert.That(NightTape.Figure(26f).Walk, Is.GreaterThan(0.9f));
        Assert.That(NightTape.Figure(NightTape.Duration).Visible, Is.False);
    }

    [Test]
    public void Figure_IsSeekableAndContinuous()
    {
        // Перемотка (C-03): одна и та же поза при любом порядке запросов, без скачков между соседними кадрами.
        TapeFigurePose a = NightTape.Figure(33f);
        NightTape.Figure(60f);
        TapeFigurePose b = NightTape.Figure(33f);
        Assert.That(b.Position, Is.EqualTo(a.Position));
        Assert.That(b.Distance, Is.EqualTo(a.Distance));

        TapeFigurePose previous = NightTape.Figure(NightTape.FirstSighting);
        for (float t = NightTape.FirstSighting + 1f / 30f; t < 43.5f; t += 1f / 30f)
        {
            TapeFigurePose pose = NightTape.Figure(t);
            Assert.That(Vector3.Distance(pose.Position, previous.Position), Is.LessThan(0.15f), "t=" + t);
            Assert.That(pose.Distance, Is.GreaterThanOrEqualTo(previous.Distance), "t=" + t);
            previous = pose;
        }
    }

    [Test]
    public void Doors_FrontOpensFirstThenDraftAndFrontClosesAtDawn()
    {
        Assert.That(NightTape.DoorOpen(5f), Is.EqualTo(0f));
        Assert.That(NightTape.DraftOpen(NightTape.Duration - 1f), Is.EqualTo(NightTape.DraftOpenAmount).Within(1e-4f));
        float frontOpened = 0f, draftOpened = 0f;
        for (float t = 0f; t < 30f; t += 0.05f)
        {
            if (frontOpened == 0f && NightTape.DoorOpen(t) > 0.01f) frontOpened = t;
            if (draftOpened == 0f && NightTape.DraftOpen(t) > 0.01f) draftOpened = t;
        }
        Assert.That(frontOpened, Is.GreaterThan(0f));
        Assert.That(draftOpened, Is.GreaterThan(frontOpened));
        Assert.That(NightTape.DoorOpen(40f), Is.EqualTo(NightTape.DoorOpenAmount).Within(1e-4f));
        Assert.That(NightTape.DoorOpen(NightTape.Duration), Is.EqualTo(0f));
    }

    [Test]
    public void Signal_SnowsAtStartCutsAndEnd()
    {
        Assert.That(NightTape.SignalLoss(0f), Is.EqualTo(1f));
        Assert.That(NightTape.SignalLoss(9f), Is.EqualTo(1f));
        Assert.That(NightTape.SignalLoss(30f), Is.EqualTo(0f));
        Assert.That(NightTape.SignalLoss(NightTape.Duration), Is.EqualTo(1f));
        Assert.That(NightTape.Tracking(NightTape.FaceToLens + 0.5f), Is.GreaterThan(NightTape.Tracking(25f)));
    }

    [Test]
    public void Sounds_AreSortedAndStepsFollowTheWalk()
    {
        var events = NightTape.SoundEvents;
        int steps = 0;
        for (int i = 0; i < events.Count; i++)
        {
            if (i > 0) Assert.That(events[i].Time, Is.GreaterThanOrEqualTo(events[i - 1].Time));
            Assert.That(events[i].Time, Is.InRange(0f, NightTape.Duration));
            if (events[i].Sound != TapeSound.StepWood) continue;
            steps++;
            Assert.That(NightTape.Figure(events[i].Time).Visible, Is.True, "шаг без фигуры в t=" + events[i].Time);
        }
        Assert.That(steps, Is.GreaterThan(10));
    }
}
