using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public sealed class BasementDepthsTests
{
    [Test]
    public void WingStart_IsBehindTheSealedDoor()
    {
        Assert.That(BasementLayout.HallB.Contains(new Vector2(DepthsLayout.WingStart.x, DepthsLayout.WingStart.z)), Is.True);
        Assert.That(DepthsLayout.WingStart.z, Is.InRange(BasementLayout.EntranceFrom, BasementLayout.EntranceTo));
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
    public void Duct_LeadsFromTheVentIntoHallB()
    {
        Vector2 outlet = new Vector2(DepthsLayout.DuctOutlet.x, DepthsLayout.DuctOutlet.z);
        Assert.That(DepthsLayout.DuctFloorY + DepthsLayout.DuctSize, Is.LessThan(-1.86f), "Короб под балками");
        Assert.That(BasementLayout.HallB.Contains(outlet), Is.True);
        Assert.That(BasementLayout.BlockedByProp(outlet, 0.4f), Is.False);
        Assert.That(BasementLayout.BlockedByJunk(outlet, 0.4f), Is.False);
    }

    [Test]
    public void InDuct_CoversTheLegsButNotTheRoomOrTheHall()
    {
        Assert.That(DepthsLayout.InDuct(new Vector2(-39f, 117.7f)), Is.True);
        Assert.That(DepthsLayout.InDuct(new Vector2(-37.2f, 117.7f)), Is.True);
        Assert.That(DepthsLayout.InDuct(new Vector2(-39f, 116.9f)), Is.False);
        Assert.That(DepthsLayout.InDuct(new Vector2(DepthsLayout.DuctOutlet.x, DepthsLayout.DuctOutlet.z)), Is.False);
    }

    [Test]
    public void Duct_TheRoomFloorBelowItIsNotInsideIt()
    {
        Assert.That(DepthsLayout.AtDuctHeight(BasementLayout.FloorY), Is.False);
        Assert.That(DepthsLayout.AtDuctHeight(DepthsLayout.DuctFloorY), Is.True);
    }

    [Test]
    public void Duct_ClimbSpotIsInsideAndCrouchedHeroFits()
    {
        Assert.That(DepthsLayout.InDuct(new Vector2(DepthsLayout.DuctClimbSpot.x, DepthsLayout.DuctClimbSpot.z)), Is.True);
        Assert.That(DepthsLayout.DuctWidth, Is.GreaterThan(0.8f));
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
    public void VentTape_GrilleOpensUnderStaticBeforeTheFigureAppears()
    {
        Assert.That(VentTape.GrilleOpen(VentTape.GrilleStart), Is.EqualTo(0f));
        Assert.That(VentTape.GrilleOpen(VentTape.GrilleEnd), Is.EqualTo(1f));
        Assert.That(VentTape.Timeline.SignalLoss(VentTape.GrilleEnd), Is.EqualTo(1f));
        Assert.That(VentTape.StandStart, Is.GreaterThan(VentTape.GrilleEnd + 2f), "пустой кадр с открытой решёткой");
        Assert.That(VentTape.Timeline.Figure(VentTape.StandStart - 0.1f).Visible, Is.False);
        Assert.That(VentTape.Timeline.Figure(VentTape.StandStart + 0.1f).Position.y, Is.EqualTo(BasementLayout.FloorY).Within(0.001f));
        Assert.That(VentTape.Timeline.Figure(VentTape.StandStart + 0.1f).Crawl, Is.EqualTo(0f));
    }

    [Test]
    public void VentTape_MonsterIsBarelyOnScreen()
    {
        float seen = 0f;
        for (float t = 0f; t < VentTape.Duration; t += 0.01f)
            if (VentTape.Timeline.Figure(t).Visible) seen += 0.01f;
        Assert.That(seen, Is.LessThan(2f));
        // Между появлением у трубы и ударом кадр пуст.
        Assert.That(VentTape.StrikeStart - VentTape.StandEnd, Is.GreaterThan(2f));
        Assert.That(VentTape.Timeline.Figure(VentTape.StandEnd + 0.1f).Visible, Is.False);
        Assert.That(VentTape.Timeline.Figure(VentTape.StrikeStart - 0.05f).Visible, Is.False);
    }

    [Test]
    public void VentTape_MonsterHitsTheCameraAndTheTapeEndsOnTheFall()
    {
        Assert.That(VentTape.Timeline.Figure(VentTape.StrikeStart - 0.1f).Strike, Is.EqualTo(0f));
        Assert.That(VentTape.Timeline.Figure(VentTape.Hit).Strike, Is.EqualTo(1f));
        Assert.That(VentTape.Hit, Is.EqualTo(VentTape.StrikeStart + VentTape.StrikeImpact).Within(0.0001f));
        // Рывок в упор, удар идёт, пока лента не оборвалась.
        for (float t = VentTape.StrikeStart; t < VentTape.Gone; t += 0.1f)
            Assert.That(VentTape.Timeline.Figure(t).Visible, Is.True, "t=" + t);
        Assert.That(VentTape.Duration - VentTape.Hit, Is.InRange(1.2f, 2.5f));
        Assert.That(VentTape.ViewShift(VentTape.Hit - 0.1f).Position, Is.EqualTo(Vector3.zero));
        Assert.That(VentTape.ViewShift(VentTape.Duration).Position.y, Is.EqualTo(-VentTape.CameraFall).Within(0.1f));
        Assert.That(VentTape.ViewShift(VentTape.Duration).Euler.z, Is.GreaterThan(60f));
    }

    [Test]
    public void Screwdriver_IsNotInTheHeroPocketsUntilTaken()
    {
        var holder = new GameObject("inventory");
        var inventory = holder.AddComponent<PlayerInventory>();
        Assert.That(inventory.HasItem("Screwdriver"), Is.False);
        inventory.hasScrewdriver = true;
        Assert.That(inventory.HasItem("Screwdriver"), Is.True);
        Object.DestroyImmediate(holder);
    }

    [Test]
    public void Duct_IsShortAndTheHeroCannotStandInIt()
    {
        float length = 0f;
        for (int i = 1; i < DepthsLayout.DuctPath.Length; i++) length += Vector2.Distance(DepthsLayout.DuctPath[i - 1], DepthsLayout.DuctPath[i]);
        Assert.That(length, Is.LessThan(5f));
        Assert.That(DepthsLayout.DuctSize, Is.LessThan(1.8f * 0.7f));
    }

    [Test]
    public void VentTape_MonsterStaysOutOfTheCameraTripod()
    {
        for (float t = VentTape.StandStart; t < VentTape.Timeline.Duration; t += 0.05f)
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
