using System.Linq;
using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public sealed class BasementTests
{
    [Test]
    public void AnswerEnding_NeedsAnyTwoOfThreeConditions()
    {
        Assert.That(AnswerEnding.IsAvailable(0, false, 0), Is.False);
        Assert.That(AnswerEnding.IsAvailable(AnswerEnding.TrustNeeded, false, 0), Is.False);
        Assert.That(AnswerEnding.IsAvailable(AnswerEnding.TrustNeeded, true, 0), Is.True);
        Assert.That(AnswerEnding.IsAvailable(0, true, AnswerEnding.CluesNeeded), Is.True);
        Assert.That(AnswerEnding.IsAvailable(AnswerEnding.TrustNeeded, false, AnswerEnding.CluesNeeded), Is.True);
        Assert.That(AnswerEnding.IsAvailable(AnswerEnding.TrustNeeded - 1, false, AnswerEnding.CluesNeeded - 1), Is.False);
        Assert.That(AnswerEnding.ConditionsMet(AnswerEnding.TrustNeeded, true, AnswerEnding.CluesNeeded), Is.EqualTo(3));
    }

    [Test]
    public void Layout_PatrolPointsAreInsideHallAndFreeOfProps()
    {
        foreach (Vector2 point in BasementLayout.Patrol)
        {
            Assert.That(BasementLayout.HallB.Contains(point, 0.5f), Is.True, point.ToString());
            Assert.That(BasementLayout.BlockedByProp(point, 0.4f), Is.False, point.ToString());
        }
    }

    [Test]
    public void Layout_DoorwaysAreOpenInWallsAndCapturedByLintels()
    {
        // Зал B: в западной стене нет стены на участке проёма, но есть перемычка над ним.
        var walls = BasementLayout.Walls(BasementLayout.HallB);
        float door = 0.5f * (BasementLayout.EntranceFrom + BasementLayout.EntranceTo);
        float westX = BasementLayout.HallB.XMin - BasementLayout.WallThickness * 0.5f;
        bool solidAtDoorway = walls.Any(w => Mathf.Abs(w.Center.x - westX) < 0.01f &&
            Mathf.Abs(w.Center.z - door) < w.Size.z * 0.5f && w.Center.y - w.Size.y * 0.5f < BasementLayout.FloorY + 1f);
        bool lintelAbove = walls.Any(w => Mathf.Abs(w.Center.x - westX) < 0.01f &&
            Mathf.Abs(w.Center.z - door) < w.Size.z * 0.5f && w.Center.y - w.Size.y * 0.5f >= BasementLayout.FloorY + BasementLayout.DoorHeight - 0.01f);
        Assert.That(solidAtDoorway, Is.False);
        Assert.That(lintelAbove, Is.True);
    }

    [Test]
    public void Layout_CorridorEndsHaveNoCapWalls()
    {
        // Коридор A открыт с обоих концов: только две длинные стены.
        Assert.That(BasementLayout.Walls(BasementLayout.CorridorA).Count, Is.EqualTo(2));
        // Коридор C открыт на севере и юге: две боковые стены.
        Assert.That(BasementLayout.Walls(BasementLayout.CorridorC).Count, Is.EqualTo(2));
    }

    [Test]
    public void Layout_JunkKeepsKeyPointsClear()
    {
        foreach (Vector2 point in BasementLayout.KeepClear())
        {
            Assert.That(BasementLayout.BlockedByJunk(point, 0.35f), Is.False, "junk blocks " + point);
            Assert.That(BasementLayout.BlockedByProp(point, 0.35f), Is.False, "prop blocks " + point);
        }
        Assert.That(BasementLayout.Junk.Length, Is.GreaterThanOrEqualTo(15));
    }

    [Test]
    public void Layout_NooksAreHidingPlacesInsideTheCellBlock()
    {
        Assert.That(BasementLayout.IsInNook(new Vector2(BasementLayout.HideSpot.x, BasementLayout.HideSpot.z)), Is.True);
        Assert.That(BasementLayout.IsInNook(new Vector2(-39.6f, 106.9f)), Is.True);
        Assert.That(BasementLayout.IsInNook(new Vector2(-40.7f, 106.9f)), Is.False);
        Assert.That(BasementLayout.IsInNook(new Vector2(BasementLayout.HideExit.x, BasementLayout.HideExit.z)), Is.False);
        Assert.That(BasementLayout.AllRooms.Length, Is.EqualTo(BasementLayout.Rooms.Length + 3));
    }

    [Test]
    public void Layout_StaysUnderTheHouse()
    {
        Rect house = BasementLayout.HouseFootprint;
        float limit = BasementLayout.FootprintMargin;
        foreach (BasementRoom room in BasementLayout.AllRooms)
        {
            Assert.That(room.XMin, Is.GreaterThanOrEqualTo(house.xMin - limit), room.Name);
            Assert.That(room.XMax, Is.LessThanOrEqualTo(house.xMax + limit), room.Name);
            Assert.That(room.ZMin, Is.GreaterThanOrEqualTo(house.yMin - limit), room.Name);
            Assert.That(room.ZMax, Is.LessThanOrEqualTo(house.yMax + limit), room.Name);
        }
    }

    [Test]
    public void Layout_RoomsAvoidTheSoilFacesOfTheExcavation()
    {
        // Грани выемки (меш grass_soil, MeshCollider нельзя отключить: по нему ходят на улице): x −46.3 и −38.3 при z ≤ 109.9.
        foreach (BasementRoom room in BasementLayout.AllRooms)
        {
            if (room.ZMin > 109.95f) continue;
            Assert.That(room.XMin, Is.GreaterThan(-46.1f), room.Name);
            Assert.That(room.ZMin, Is.GreaterThan(105.6f), room.Name);
            if (room != BasementLayout.HallB) Assert.That(room.XMax, Is.LessThan(-38.5f), room.Name);
        }
        Assert.That(BasementLayout.PassageFrom, Is.GreaterThan(110f));
    }

    [Test]
    public void Layout_PipeRunsFromTheCellToTheHouseEnd()
    {
        Vector2 start = BasementLayout.PipeRoute[0];
        Assert.That(BasementLayout.CellArea.Contains(start), Is.True);
        Vector2 end = BasementLayout.PipeRoute[BasementLayout.PipeRoute.Length - 1];
        Assert.That(end.x, Is.LessThan(-37f)); // к существующей комнате и дому
    }

    [Test]
    public void Stalker_SearchesTheRoomAfterNoiseAndLeaves()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-37.85f, 111.2f));
        var hidden = new Vector2(BasementLayout.HideSpot.x, BasementLayout.HideSpot.z);
        brain.AlertAndSearch(BasementLayout.SearchRoute[0], BasementLayout.SearchRoute, BasementLayout.SearchFacing);
        bool visitedRoom = false;
        for (int i = 0; i < 1200 && !brain.SearchCompleted; i++)
        {
            brain.Step(hidden, KnockNoise.Quiet, false, 0.05f);
            if (brain.CurrentMode == BasementStalkerBrain.Mode.RoomSearch) visitedRoom = true;
            Assert.That(brain.CaughtPlayer, Is.False, "step " + i);
        }
        Assert.That(visitedRoom, Is.True);
        Assert.That(brain.SearchCompleted, Is.True);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
    }

    [Test]
    public void Stalker_LureWalksIntoTheCellPastTheHiddenPlayer()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.TrapStart);
        brain.Lure(BasementLayout.TrapRoute);
        var hidden = new Vector2(BasementLayout.HideSpot.x, BasementLayout.HideSpot.z);
        for (int i = 0; i < 1200 && !brain.LureFinished; i++)
        {
            brain.Step(hidden, KnockNoise.Quiet, false, 0.05f, true);
            Assert.That(brain.CaughtPlayer, Is.False, "step " + i);
        }
        Assert.That(brain.LureFinished, Is.True);
        Assert.That(brain.Position.x, Is.InRange(BasementLayout.CellArea.xMin, BasementLayout.CellArea.xMax));
        Assert.That(brain.Position.y, Is.LessThan(BasementLayout.CellArea.yMax - 0.3f)); // решётка закроется перед ним
    }

    [Test]
    public void Stalker_LureTakesLongEnoughToHide()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.TrapStart);
        brain.Lure(BasementLayout.TrapRoute);
        var hidden = new Vector2(BasementLayout.HideSpot.x, BasementLayout.HideSpot.z);
        int steps = 0;
        while (steps < 1200 && !brain.LureFinished) { brain.Step(hidden, KnockNoise.Quiet, false, 0.05f, true); steps++; }
        Assert.That(steps * 0.05f, Is.InRange(8f, 15f));
    }

    [Test]
    public void Stalker_LureResumesAfterHearingThePlayer()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.TrapStart);
        brain.Lure(BasementLayout.TrapRoute);
        Vector2 far = BasementLayout.TrapStart + new Vector2(-2f, 0f);
        brain.Alert(far);
        Vector2 hidden = new Vector2(BasementLayout.HideSpot.x, BasementLayout.HideSpot.z);
        for (int i = 0; i < 1500 && !brain.LureFinished; i++) brain.Step(hidden, KnockNoise.Quiet, false, 0.05f, true);
        Assert.That(brain.LureFinished, Is.True);
    }

    [Test]
    public void Stalker_LureCatchesPlayerStandingOnTheWay()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.TrapStart);
        brain.Lure(BasementLayout.TrapRoute);
        Vector2 inTheWay = BasementLayout.TrapMouth;
        for (int i = 0; i < 1200 && !brain.CaughtPlayer; i++) brain.Step(inTheWay, KnockNoise.Quiet, false, 0.05f);
        Assert.That(brain.CaughtPlayer, Is.True);
    }

    [Test]
    public void Layout_LockHangsAtTheFreeEdgeOfTheDoor()
    {
        Assert.That(BasementLayout.CellLock.x, Is.LessThan(BasementLayout.CellArea.xMin + 0.3f)); // петли на xMax
    }

    [Test]
    public void Stalker_DoesNotCatchHiddenPlayerAtCloseRangeButCatchesWhenExposed()
    {
        var brain = new BasementStalkerBrain();
        Vector2 player = new Vector2(-41.8f, 109f);
        brain.Reset(player + Vector2.up);
        brain.Alert(player);
        brain.Step(player, KnockNoise.Quiet, false, 0.1f, true);
        Assert.That(brain.CaughtPlayer, Is.False);
        brain.Step(player, KnockNoise.Quiet, false, 0.1f, false);
        Assert.That(brain.CaughtPlayer, Is.True);
    }

    [Test]
    public void Stalker_CatchesPlayerWhoMakesNoiseDuringSearch()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-37.85f, 111.2f));
        var player = new Vector2(BasementLayout.HideSpot.x, BasementLayout.HideSpot.z);
        brain.AlertAndSearch(BasementLayout.SearchRoute[0], BasementLayout.SearchRoute, BasementLayout.SearchFacing);
        for (int i = 0; i < 600 && !brain.CaughtPlayer; i++)
            brain.Step(player, KnockNoise.Run, false, 0.05f);
        Assert.That(brain.CaughtPlayer, Is.True);
    }

    [Test]
    public void Stalker_PassesDoorwaysInsteadOfWalls()
    {
        Vector2 hall = BasementLayout.Patrol[2], cell = new Vector2(-42f, 109f);
        float door = 0.5f * (BasementLayout.PassageFrom + BasementLayout.PassageTo);
        Assert.That(BasementStalkerBrain.NextPoint(hall, cell), Is.EqualTo(new Vector2(BasementLayout.HallB.XMin, door))); // сначала проём зала B в переход C
        Assert.That(BasementStalkerBrain.NextPoint(new Vector2(-37.8f, door), cell), Is.EqualTo(new Vector2(BasementLayout.CellBlockD.XMax, door)));
        Vector2 inHall = new Vector2(-34.9f, 112f);
        Assert.That(BasementStalkerBrain.NextPoint(hall, inHall), Is.EqualTo(inHall));
    }

    [Test]
    public void Stalker_ReachesTheExistingRoomThroughTheEntrance()
    {
        // Точка у южной стены существующей комнаты ближе к блоку D, но путь к ней — через проход A.
        Vector2 stairsRoom = new Vector2(-44f, 113f);
        Vector2 first = BasementStalkerBrain.NextPoint(BasementLayout.Patrol[2], stairsRoom);
        Assert.That(first, Is.EqualTo(new Vector2(BasementLayout.HallB.XMin, 0.5f * (BasementLayout.EntranceFrom + BasementLayout.EntranceTo))));
    }

    [Test]
    public void Stalker_IgnoresCrouchingPlayerButHearsRunningOne()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.Patrol[0]);
        var player = new Vector2(-36.4f, 112.4f); // полоса у западной стены, подальше от обхода
        for (int i = 0; i < 100; i++) brain.Step(player, KnockNoise.Quiet, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
        for (int i = 0; i < 12 && brain.CurrentMode == BasementStalkerBrain.Mode.Patrol; i++) brain.Step(player, KnockNoise.Run, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Investigate));
    }

    [Test]
    public void Stalker_FarWalkingIsNotHeard()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.Patrol[0]);
        Vector2 far = BasementLayout.Patrol[0] + new Vector2(BasementStalkerBrain.WalkHearRange + 6f, 0f);
        for (int i = 0; i < 100; i++) brain.Step(far, KnockNoise.Walk, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
    }

    [Test]
    public void Stalker_CatchesPlayerStandingNearAfterNoticing()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-34.9f, 112f));
        var player = new Vector2(-34.9f, 114f);
        for (int i = 0; i < 40 && !brain.CaughtPlayer; i++) brain.Step(player, KnockNoise.Run, false, 0.1f);
        Assert.That(brain.CaughtPlayer, Is.True);
    }

    [Test]
    public void Stalker_SeesCloseUncrouchedPlayerOnlyWhenInSight()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-34.9f, 112f));
        var player = new Vector2(-34.9f, 113.8f);
        brain.Step(player, KnockNoise.Quiet, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
        brain.Step(player, KnockNoise.Quiet, true, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Investigate));
    }

    [Test]
    public void Stalker_ReturnsToPatrolAfterSearching()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-35.5f, 116f));
        brain.Alert(new Vector2(-35.5f, 114f));
        var far = new Vector2(-36.4f, 106.9f);
        for (int i = 0; i < 500; i++) brain.Step(far, KnockNoise.Quiet, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
    }
}
