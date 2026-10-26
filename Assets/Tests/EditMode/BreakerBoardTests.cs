using NUnit.Framework;
using KnocksFromBeneath;

public class BreakerBoardTests
{
    private static BreakerBoard Board(int extra = 2) => new BreakerBoard(extra, new System.Random(7));

    private static void RaiseAllExceptFaulty(BreakerBoard board)
    {
        for (int i = 0; i < BreakerBoard.GroupCount; i++)
            if (i != BreakerBoard.Faulty && !board.IsUp(i)) board.Toggle(i);
    }

    [Test]
    public void Start_KitchenAndExtrasTripped_BasementUp()
    {
        var board = Board(2);
        Assert.That(board.IsUp(BreakerBoard.Faulty), Is.False);
        Assert.That(board.GroupsDown, Is.EqualTo(3));
        Assert.That(board.IsUp(BreakerBoard.GroupCount - 1), Is.True, "Подвал не выбит");
        Assert.That(board.MainUp, Is.False);
    }

    [Test]
    public void MainWithGroupDown_TripsAndBringsKnockCloser()
    {
        var board = Board();
        Assert.That(board.ToggleMain(), Is.EqualTo(BreakerFlip.MainTripped));
        Assert.That(board.MainUp, Is.False);
        Assert.That(board.Approach, Is.EqualTo(BreakerBoard.MainTripNoise).Within(1e-4f));
        Assert.That(board.Finished, Is.False);
    }

    [Test]
    public void KitchenTripsBackOnItsOwn()
    {
        var board = Board();
        Assert.That(board.Toggle(BreakerBoard.Faulty), Is.EqualTo(BreakerFlip.Raised));
        Assert.That(board.Advance(BreakerBoard.FirstRetrip - 0.1f), Is.False);
        Assert.That(board.Advance(0.2f), Is.True);
        Assert.That(board.IsUp(BreakerBoard.Faulty), Is.False);
        Assert.That(board.Retrips, Is.EqualTo(1));
    }

    [Test]
    public void KitchenLastThenMainQuickly_Restores()
    {
        var board = Board();
        RaiseAllExceptFaulty(board);
        board.Toggle(BreakerBoard.Faulty);
        board.Advance(0.8f);
        Assert.That(board.ToggleMain(), Is.EqualTo(BreakerFlip.Restored));
        Assert.That(board.Succeeded, Is.True);
        Assert.That(board.Advance(10f), Is.False, "После успеха кухня в логике больше не выбивает");
    }

    [Test]
    public void KitchenFirst_IsDownAgainByTheTimeOthersAreUp()
    {
        var board = Board();
        board.Toggle(BreakerBoard.Faulty);
        board.Advance(1.5f);
        RaiseAllExceptFaulty(board);
        board.Advance(1.5f);
        Assert.That(board.ToggleMain(), Is.EqualTo(BreakerFlip.MainTripped));
    }

    [Test]
    public void LoweringKitchen_CancelsRetrip()
    {
        var board = Board();
        board.Toggle(BreakerBoard.Faulty);
        board.Toggle(BreakerBoard.Faulty);
        Assert.That(board.RetripPending, Is.False);
        Assert.That(board.Advance(5f), Is.False);
    }

    [Test]
    public void Idle_KnockReachesPanel()
    {
        var board = Board();
        for (int i = 0; i < 80 && !board.Finished; i++) board.Advance(1f);
        Assert.That(board.Failed, Is.True);
        Assert.That(board.Elapsed, Is.EqualTo(BreakerBoard.FailSeconds).Within(1.01f));
    }

    [Test]
    public void KnocksQuickenAsTheyApproach()
    {
        var board = Board();
        float calm = board.KnockInterval;
        board.Advance(BreakerBoard.FailSeconds * 0.8f);
        Assert.That(board.KnockInterval, Is.LessThan(calm));
    }

    [Test]
    public void FinishedBoard_IgnoresInput()
    {
        var board = Board(0);
        board.Toggle(BreakerBoard.Faulty);
        Assert.That(board.ToggleMain(), Is.EqualTo(BreakerFlip.Restored));
        Assert.That(board.Toggle(1), Is.EqualTo(BreakerFlip.None));
        Assert.That(board.ToggleMain(), Is.EqualTo(BreakerFlip.None));
    }
}
