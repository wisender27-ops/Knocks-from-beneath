using NUnit.Framework;
using KnocksFromBeneath;

public class StoryFlagsTests
{
    [SetUp]
    public void SetUp()
    {
        StoryFlags.Reset();
        StoryFlags.Install();
    }

    [TearDown]
    public void TearDown() => StoryFlags.Reset();

    [Test]
    public void Flags_AccumulateAcrossCalls()
    {
        StoryFlags.AddKnockTrust(2);
        StoryFlags.AddKnockTrust(-1);
        StoryFlags.MarkRecordingSeen();

        Assert.That(StoryFlags.KnockTrust, Is.EqualTo(1));
        Assert.That(StoryFlags.SawRecording, Is.True);
    }

    [Test]
    public void RegisterClue_CountsEachClueOnce()
    {
        Assert.That(StoryFlags.RegisterClue("photo"), Is.True);
        Assert.That(StoryFlags.RegisterClue("photo"), Is.False);
        Assert.That(StoryFlags.RegisterClue("scratches"), Is.True);
        Assert.That(StoryFlags.RegisterClue(""), Is.False);

        Assert.That(StoryFlags.CluesFound, Is.EqualTo(2));
        Assert.That(StoryFlags.HasClue("photo"), Is.True);
        Assert.That(StoryFlags.HasClue("letter"), Is.False);
    }

    [Test]
    public void NeighborNoteEvent_SetsFlag_AndInstallIsIdempotent()
    {
        StoryFlags.Install();

        GameEvents.OnNeighborNoteRead?.Invoke();

        Assert.That(StoryFlags.NeighborNoteRead, Is.True);
        int subscriptions = 0;
        foreach (var handler in GameEvents.OnNeighborNoteRead.GetInvocationList())
            if (handler.Method.DeclaringType == typeof(StoryFlags)) subscriptions++;
        Assert.That(subscriptions, Is.EqualTo(1));
    }

    [Test]
    public void FrontDoorLocked_SharesStateWithGameState()
    {
        GameState.frontDoorLocked = true;
        Assert.That(StoryFlags.FrontDoorLocked, Is.True);

        StoryFlags.FrontDoorLocked = false;
        Assert.That(GameState.frontDoorLocked, Is.False);
    }

    [Test]
    public void Reset_ClearsEverything()
    {
        StoryFlags.AddKnockTrust(3);
        StoryFlags.MarkRecordingSeen();
        StoryFlags.RegisterClue("photo");
        StoryFlags.FrontDoorLocked = true;
        GameEvents.OnNeighborNoteRead?.Invoke();

        StoryFlags.Reset();

        Assert.That(StoryFlags.KnockTrust, Is.Zero);
        Assert.That(StoryFlags.SawRecording, Is.False);
        Assert.That(StoryFlags.NeighborNoteRead, Is.False);
        Assert.That(StoryFlags.CluesFound, Is.Zero);
        Assert.That(GameState.frontDoorLocked, Is.False);
    }
}
