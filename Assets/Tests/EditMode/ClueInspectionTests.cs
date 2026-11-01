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

    private static void Settle(InspectOrbit orbit, float seconds = 2f)
    {
        for (float t = 0f; t < seconds; t += 0.02f) orbit.Step(0.02f);
    }

    // Камера превью смотрит вдоль +Z: передняя грань предмета — та, что обращена к -Z.
    private static Vector3 Front(InspectOrbit orbit) => orbit.Rotation * Vector3.back;

    [Test]
    public void Orbit_DragRight_TurnsFrontToTheRight()
    {
        var orbit = new InspectOrbit();
        orbit.Drag(30f, 0f);
        Settle(orbit);
        Assert.That(Front(orbit).x, Is.GreaterThan(0.4f), "Тянешь вправо — передняя грань уходит вправо");
        Assert.That(Front(orbit).y, Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void Orbit_DragUp_TiltsFrontUp()
    {
        var orbit = new InspectOrbit();
        orbit.Drag(0f, 30f);
        Settle(orbit);
        Assert.That(Front(orbit).y, Is.GreaterThan(0.4f), "Тянешь вверх — передняя грань поднимается");
        Assert.That(Front(orbit).x, Is.EqualTo(0f).Within(1e-4f));
    }

    [Test]
    public void Orbit_PitchIsClamped()
    {
        var orbit = new InspectOrbit();
        orbit.Drag(0f, 500f);
        Settle(orbit);
        Assert.That(orbit.Pitch, Is.EqualTo(InspectOrbit.MaxPitch).Within(0.01f));
        orbit.Drag(0f, -1000f);
        Settle(orbit);
        Assert.That(orbit.Pitch, Is.EqualTo(-InspectOrbit.MaxPitch).Within(0.01f));
    }

    [Test]
    public void Orbit_NoRoll_AfterAnyDragMix()
    {
        var orbit = new InspectOrbit();
        float[] xs = { 40f, -15f, 90f, 7f, -130f, 60f };
        float[] ys = { 25f, -60f, 30f, 45f, -10f, -70f };
        for (int i = 0; i < xs.Length; i++)
        {
            orbit.Drag(xs[i], ys[i]);
            Settle(orbit, 0.3f);
            Vector3 up = orbit.Rotation * Vector3.up;
            Assert.That(up.x, Is.EqualTo(0f).Within(1e-4f), "Верх предмета не заваливается вбок");
            Assert.That(up.y, Is.GreaterThan(0f), "Предмет не переворачивается вверх ногами");
        }
    }

    [Test]
    public void Orbit_IsSmooth_NotInstant()
    {
        var orbit = new InspectOrbit();
        orbit.Drag(60f, 0f);
        orbit.Step(0.016f);
        Assert.That(Mathf.Abs(orbit.Yaw), Is.GreaterThan(1f).And.LessThan(59f));
        Settle(orbit);
        Assert.That(orbit.Yaw, Is.EqualTo(-60f).Within(0.05f));
    }

    [Test]
    public void Orbit_Reset_ReturnsByShortWay()
    {
        var orbit = new InspectOrbit();
        for (int i = 0; i < 10; i++) { orbit.Drag(100f, 10f); Settle(orbit, 0.5f); }
        orbit.Reset();
        Assert.That(Mathf.Abs(orbit.Yaw), Is.LessThanOrEqualTo(180f), "Сброс не крутит лишние обороты");
        Settle(orbit);
        Assert.That(Quaternion.Angle(orbit.Rotation, Quaternion.identity), Is.LessThan(0.1f));
    }

    [Test]
    public void Orbit_CanShowTopAndBottomFaces()
    {
        // Деталь по умолчанию на дне предмета; её и верх должно быть можно повернуть к камере.
        Vector3 toViewer = Vector3.back;
        foreach (Vector3 faceNormal in new[] { Vector3.down, Vector3.up })
        {
            var orbit = new InspectOrbit();
            float best = -1f;
            for (int yawStep = 0; yawStep < 36; yawStep++)
            {
                orbit.Drag(10f, faceNormal.y < 0f ? 500f : -500f);
                orbit.Snap();
                Vector3 normal = orbit.Rotation * ItemPreviewStage.PresentRotation * faceNormal;
                best = Mathf.Max(best, ClueInspection.Facing(normal, toViewer));
            }
            Assert.That(best, Is.GreaterThanOrEqualTo(ClueInspection.FacingDot), "Грань " + faceNormal + " достижима");
        }
    }
}
