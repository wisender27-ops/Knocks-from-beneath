using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public sealed class BasementDepthsTests
{
    [Test]
    public void Route_StaysInsideTunnelsAndClearOfMachinery()
    {
        Vector2 previous = new Vector2(DepthsLayout.DropPoint.x, DepthsLayout.DropPoint.z);
        foreach (Vector2 point in DepthsLayout.Route)
        {
            for (float k = 0f; k <= 1f; k += 0.02f)
            {
                Vector2 sample = Vector2.Lerp(previous, point, k);
                Assert.That(DepthsLayout.IsInside(sample), Is.True, "outside at " + sample);
                Assert.That(DepthsLayout.BlockedByProp(sample, 0.4f), Is.False, "blocked at " + sample);
            }
            previous = point;
        }
    }

    [Test]
    public void Tunnels_ReachFarBeyondTheHouse()
    {
        Rect house = BasementLayout.HouseFootprint;
        Assert.That(DepthsLayout.PumpHall.XMax - house.xMax, Is.GreaterThan(house.width));
    }

    [Test]
    public void Tunnels_PassUnderTheBasementFloor()
    {
        Assert.That(DepthsLayout.CeilingY + 0.3f, Is.LessThan(BasementLayout.FloorY - 0.3f));
    }

    [Test]
    public void Ladder_EndsUnderTheSealedHalfOfTheBasement()
    {
        Vector2 ladder = new Vector2(DepthsLayout.LadderUp.x, DepthsLayout.LadderUp.z);
        Assert.That(DepthsLayout.Riser.Contains(ladder), Is.True);
        Assert.That(BasementLayout.CorridorA.Contains(new Vector2(DepthsLayout.WingStart.x, DepthsLayout.WingStart.z)), Is.True);
    }

    [Test]
    public void SealedDoor_ClosesTheOldEntranceCompletely()
    {
        float half = DepthsLayout.SealedDoorSize.z * 0.5f;
        Assert.That(DepthsLayout.SealedDoor.z - half, Is.LessThanOrEqualTo(BasementLayout.EntranceFrom));
        Assert.That(DepthsLayout.SealedDoor.z + half, Is.GreaterThanOrEqualTo(BasementLayout.EntranceTo));
    }

    [Test]
    public void Basement_IsMuchSmallerThanTheHouse()
    {
        float basement = (BasementLayout.ExistingWallX + 47.3f) * (BasementLayout.ExistingRoomZMax - BasementLayout.ExistingRoomZMin);
        Rect house = BasementLayout.HouseFootprint;
        Assert.That(basement, Is.LessThan(house.width * house.height * 0.5f));
    }

    [Test]
    public void Duct_FitsACrouchingHeroButNotAStandingOne()
    {
        Assert.That(DepthsLayout.DuctSize, Is.GreaterThan(1f));
        Assert.That(DepthsLayout.DuctSize, Is.LessThan(1.8f));
    }

    [Test]
    public void Duct_RunsBetweenBasementFloorAndTunnelCeiling()
    {
        Assert.That(DepthsLayout.DuctFloorY - 0.1f, Is.GreaterThan(DepthsLayout.CeilingY + 0.3f));
        Assert.That(DepthsLayout.DuctFloorY + DepthsLayout.DuctSize + 0.1f, Is.LessThan(BasementLayout.FloorY - 0.3f));
        Vector2 end = DepthsLayout.DuctPath[DepthsLayout.DuctPath.Length - 1];
        Assert.That(DepthsLayout.Shaft.Contains(end), Is.True);
    }

    [Test]
    public void Duct_IsMadeOfStraightLegs()
    {
        for (int i = 1; i < DepthsLayout.DuctPath.Length; i++)
        {
            Vector2 leg = DepthsLayout.DuctPath[i] - DepthsLayout.DuctPath[i - 1];
            Assert.That(Mathf.Min(Mathf.Abs(leg.x), Mathf.Abs(leg.y)), Is.EqualTo(0f), "leg " + i);
        }
    }

    [Test]
    public void VentTape_GrilleGoesBeforeTheMonsterCrawlsOut()
    {
        Assert.That(VentTape.GrilleOpen(VentTape.GrilleStart), Is.EqualTo(0f));
        Assert.That(VentTape.GrilleOpen(VentTape.GrilleEnd), Is.EqualTo(1f));
        Assert.That(VentTape.CrawlStart, Is.GreaterThanOrEqualTo(VentTape.GrilleEnd));
        Assert.That(VentTape.Timeline.Figure(VentTape.CrawlStart - 0.1f).Visible, Is.False);
        Assert.That(VentTape.Timeline.Figure(VentTape.CrawlStart + 0.1f).Position.y, Is.LessThan(BasementLayout.FloorY - 1f));
        Assert.That(VentTape.Timeline.Figure(VentTape.StoodUp + 0.5f).Position.y, Is.EqualTo(BasementLayout.FloorY).Within(0.001f));
    }

    [Test]
    public void VentTape_MonsterStaysOutOfTheCameraTripod()
    {
        for (float t = VentTape.CrawlStart; t < VentTape.Timeline.Duration; t += 0.2f)
        {
            TapeFigurePose pose = VentTape.Timeline.Figure(t);
            if (!pose.Visible) continue;
            float distance = Vector2.Distance(new Vector2(pose.Position.x, pose.Position.z), new Vector2(DepthsLayout.CameraSlot.x, DepthsLayout.CameraSlot.z));
            Assert.That(distance, Is.GreaterThan(0.6f), "t=" + t);
        }
    }

    [Test]
    public void BasementCamera_CountsOnlyInTheBasementSlot()
    {
        StoryFlags.Reset();
        StoryFlags.SetCameraPlacementSlot(1);
        Assert.That(StoryFlags.BasementCameraSet, Is.False);
        StoryFlags.SetCameraPlacementSlot(DepthsLayout.CameraSlotNumber);
        Assert.That(StoryFlags.BasementCameraSet, Is.True);
        StoryFlags.Reset();
    }

    [Test]
    public void DayVisit_TellsUndergroundFromTheSurface()
    {
        Assert.That(BasementDayVisit.IsUnderground(new Vector3(-44.8f, BasementLayout.FloorY + 1f, 115f)), Is.True);
        Assert.That(BasementDayVisit.IsUnderground(new Vector3(-44.8f, 1f, 122f)), Is.False);
        Assert.That(BasementDayVisit.IsAboveGround(new Vector3(-44.8f, 1f, 122f)), Is.True);
    }
}
