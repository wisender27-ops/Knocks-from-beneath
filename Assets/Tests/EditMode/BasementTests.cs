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
    public void Stalker_PassesDoorwaysInsteadOfWalls()
    {
        Vector2 hall = new Vector2(-30.3f, 120.2f), cell = new Vector2(-28f, 100f);
        Vector2 first = BasementStalkerBrain.NextPoint(hall, cell);
        Assert.That(first.y, Is.EqualTo(110f).Within(0.01f)); // сначала проём зала B в коридор C
        Assert.That(BasementStalkerBrain.NextPoint(new Vector2(-28f, 109f), cell).y, Is.EqualTo(103f).Within(0.01f));
        Assert.That(BasementStalkerBrain.NextPoint(hall, new Vector2(-30f, 114f)), Is.EqualTo(new Vector2(-30f, 114f)));
    }

    [Test]
    public void Stalker_IgnoresCrouchingPlayerButHearsRunningOne()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(BasementLayout.Patrol[0]);
        var player = new Vector2(-28f, 116.4f); // центр зала, подальше от обхода
        for (int i = 0; i < 100; i++) brain.Step(player, KnockNoise.Quiet, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
        for (int i = 0; i < 12; i++) brain.Step(player, KnockNoise.Run, false, 0.1f);
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
        brain.Reset(new Vector2(-28f, 116f));
        var player = new Vector2(-28f, 118f);
        for (int i = 0; i < 40 && !brain.CaughtPlayer; i++) brain.Step(player, KnockNoise.Run, false, 0.1f);
        Assert.That(brain.CaughtPlayer, Is.True);
    }

    [Test]
    public void Stalker_SeesCloseUncrouchedPlayerOnlyWhenInSight()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-28f, 116f));
        var player = new Vector2(-28f, 117.8f);
        brain.Step(player, KnockNoise.Quiet, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
        brain.Step(player, KnockNoise.Quiet, true, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Investigate));
    }

    [Test]
    public void Stalker_ReturnsToPatrolAfterSearching()
    {
        var brain = new BasementStalkerBrain();
        brain.Reset(new Vector2(-30f, 116f));
        brain.Alert(new Vector2(-30f, 114f));
        var far = new Vector2(-24.5f, 111f);
        for (int i = 0; i < 500; i++) brain.Step(far, KnockNoise.Quiet, false, 0.1f);
        Assert.That(brain.CurrentMode, Is.EqualTo(BasementStalkerBrain.Mode.Patrol));
    }
}
