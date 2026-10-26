using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class ClueInspectionTests
{
    [SetUp]
    public void ResetFlags() => StoryFlags.Reset();

    [TearDown]
    public void CleanFlags() => StoryFlags.Reset();

    private static void Hold(ClueInspection inspection, float facing, float zoom, float seconds)
    {
        for (float t = 0f; t < seconds; t += 0.05f) inspection.Step(facing, zoom, 0.05f);
    }

    [Test]
    public void Facing_IsDotBetweenNormalAndViewer()
    {
        Assert.That(ClueInspection.Facing(Vector3.forward, Vector3.forward * 3f), Is.EqualTo(1f).Within(1e-4f));
        Assert.That(ClueInspection.Facing(Vector3.down, Vector3.forward), Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void DetailOnTheOtherSide_IsHidden()
    {
        var inspection = new ClueInspection();
        Assert.That(inspection.Classify(-1f, 1.9f), Is.EqualTo(ClueView.Hidden));
        Hold(inspection, -1f, 1.9f, 5f);
        Assert.That(inspection.Found, Is.False);
    }

    [Test]
    public void FacingButFar_IsOnlyGlimpse()
    {
        var inspection = new ClueInspection();
        Assert.That(inspection.Classify(0.95f, 1f), Is.EqualTo(ClueView.Glimpse));
        Hold(inspection, 0.95f, 1f, 5f);
        Assert.That(inspection.Found, Is.False, "Издалека надпись не читается");
    }

    [Test]
    public void FacingAndZoomed_FoundAfterHold()
    {
        var inspection = new ClueInspection();
        bool reported = false;
        int reports = 0;
        for (float t = 0f; t < 2f; t += 0.05f)
            if (inspection.Step(0.9f, ClueInspection.ReadZoom, 0.05f)) { reported = true; reports++; }
        Assert.That(reported, Is.True);
        Assert.That(reports, Is.EqualTo(1), "Находка сообщается один раз");
        Assert.That(inspection.Classify(-1f, 1f), Is.EqualTo(ClueView.Found));
    }

    [Test]
    public void ShortGlance_DoesNotFind_AndDecays()
    {
        var inspection = new ClueInspection();
        Hold(inspection, 0.9f, 1.5f, ClueInspection.HoldSeconds * 0.5f);
        float partial = inspection.Progress;
        Assert.That(partial, Is.GreaterThan(0.3f));
        Hold(inspection, -1f, 1.5f, 0.5f);
        Assert.That(inspection.Progress, Is.LessThan(partial));
        Assert.That(inspection.Found, Is.False);
    }

    [Test]
    public void AlreadyFoundClue_StartsFound()
    {
        StoryFlags.RegisterClue("clue-test");
        var inspection = new ClueInspection(StoryFlags.HasClue("clue-test"));
        Assert.That(inspection.Found, Is.True);
        Assert.That(inspection.Step(1f, 2f, 1f), Is.False, "Повторный осмотр не находит улику заново");
        Assert.That(StoryFlags.CluesFound, Is.EqualTo(1));
    }
}
