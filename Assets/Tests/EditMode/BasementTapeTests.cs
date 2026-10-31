using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public sealed class BasementTapeTests
{
    [Test]
    public void Stamp_ShowsTheNightAndThenTheMorning()
    {
        Assert.That(BasementTape.Timeline.Stamp(0f), Is.EqualTo("AM  3:07:12"));
        Assert.That(BasementTape.Timeline.Stamp(29f), Is.EqualTo("AM  3:07:41"));
        Assert.That(BasementTape.Timeline.Stamp(30f), Is.EqualTo("AM  6:58:40"));
    }

    [Test]
    public void Door_StaysOpenUntilTheMonsterIsInsideThenStaysShut()
    {
        Assert.That(BasementTape.DoorOpen(20f), Is.EqualTo(1f));
        Assert.That(BasementTape.Timeline.Figure(BasementTape.Slammed - 0.4f).Position.z, Is.LessThan(BasementLayout.CellArea.yMax));
        Assert.That(BasementLayout.CellArea.Contains(ToPlan(BasementTape.Timeline.Figure(BasementTape.Slammed).Position)), Is.True);
        Assert.That(BasementTape.DoorOpen(BasementTape.Slammed), Is.EqualTo(0f));
        Assert.That(BasementTape.DoorOpen(BasementTape.Duration), Is.EqualTo(0f));
    }

    [Test]
    public void Figure_EntersThroughTheFreeSideOfTheOpenDoorAndNeverLeavesTheCellAgain()
    {
        // Открытая решётка стоит к северу от петли: фигура проходит севернее её конца и у западной стены.
        for (float t = 12f; t < 19f; t += 0.1f)
            Assert.That(BasementTape.Timeline.Figure(t).Position.z, Is.GreaterThan(110.5f), "t=" + t);
        for (float t = BasementTape.Slammed; t <= BasementTape.Duration; t += 0.1f)
        {
            TapeFigurePose pose = BasementTape.Timeline.Figure(t);
            Assert.That(pose.Visible, Is.True, "t=" + t);
            Assert.That(pose.Position.z, Is.LessThan(BasementLayout.CellArea.yMax), "t=" + t);
        }
    }

    [Test]
    public void Figure_FacesTheLensOnlyAfterTheDoorIsShut()
    {
        Assert.That(BasementTape.Timeline.Figure(BasementTape.Slammed).Look, Is.EqualTo(0f));
        Assert.That(BasementTape.Timeline.Figure(BasementTape.LookFull + 0.1f).Look, Is.EqualTo(1f));
    }

    [Test]
    public void Knocks_CallTheMonsterAndAnswerFromTheHouse()
    {
        int beforeEntry = 0, morning = 0;
        foreach (TapeEvent e in BasementTape.Timeline.SoundEvents)
        {
            if (e.Sound != TapeSound.Knock) continue;
            if (e.Time < BasementTape.Timeline.FirstSighting) beforeEntry++;
            if (e.Time > 30f) { morning++; Assert.That(e.Position.y, Is.GreaterThan(BasementLayout.CeilingY)); }
        }
        Assert.That(beforeEntry, Is.EqualTo(6));
        Assert.That(morning, Is.EqualTo(3));
    }

    [Test]
    public void Camera_StandsInsideTheCellBlockAndLooksAtTheBars()
    {
        Assert.That(BasementLayout.CellBlockD.Contains(ToPlan(BasementLayout.TapeCameraPosition)), Is.True);
        Assert.That(BasementLayout.CellArea.Contains(ToPlan(BasementLayout.TapeCameraLook)), Is.True);
    }

    private static Vector2 ToPlan(Vector3 world) => new Vector2(world.x, world.z);
}
