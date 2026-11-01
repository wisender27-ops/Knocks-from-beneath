using KnocksFromBeneath;
using NUnit.Framework;

public sealed class TapeTransportTests
{
    [SetUp] public void SetUp() => StoryFlags.Reset();
    [TearDown] public void TearDown() => StoryFlags.Reset();

    [Test]
    public void Shuttle_AcceleratesAndCoastsAfterRelease()
    {
        var tape = new TapeTransport();
        tape.Synchronize(20f);
        tape.Advance(0.1f, 1);
        Assert.That(tape.Speed, Is.InRange(0.1f, 5.9f));
        tape.Advance(0.1f, 1);
        float before = tape.Time;
        tape.Advance(0.05f, 0);
        Assert.That(tape.Time, Is.GreaterThan(before));
        Assert.That(tape.Speed, Is.GreaterThan(0f));
        tape.Advance(1f, 0);
        Assert.That(tape.IsStopped, Is.True);
    }

    [Test]
    public void Space_StopsShuttleImmediatelyThenResumesPlayback()
    {
        var tape = new TapeTransport();
        tape.Synchronize(20f);
        tape.Advance(0.2f, -1);
        tape.TogglePlay();
        Assert.That(tape.IsStopped, Is.True);
        Assert.That(tape.Speed, Is.Zero);
        tape.TogglePlay();
        Assert.That(tape.Playing, Is.True);
    }

    [Test]
    public void EndOfTape_StopsButCanRewind()
    {
        var tape = new TapeTransport();
        tape.Synchronize(NightTape.Duration - 0.01f);
        tape.Advance(0.1f, 0);
        Assert.That(tape.Time, Is.EqualTo(NightTape.Duration));
        Assert.That(tape.IsStopped, Is.True);
        tape.Advance(0.1f, -1);
        Assert.That(tape.Time, Is.LessThan(NightTape.Duration));
    }

    [Test]
    public void Reverse_CannotLeaveStartOfTape()
    {
        var tape = new TapeTransport();
        tape.Advance(1f, -1);
        Assert.That(tape.Time, Is.Zero);
        Assert.That(tape.IsStopped, Is.True);
    }

    [Test]
    public void Capture_RequiresStoppedAnomalyAndIsIdempotent()
    {
        var tape = new TapeTransport();
        tape.Synchronize(NightTape.FaceToLens + 0.5f);
        Assert.That(tape.CaptureEvidence(true), Is.False);
        Assert.That(StoryFlags.SawRecording, Is.False);
        tape.TogglePlay();
        Assert.That(tape.CaptureEvidence(true), Is.True);
        Assert.That(tape.CaptureEvidence(true), Is.True);
        Assert.That(StoryFlags.SawRecording, Is.True);
        Assert.That(StoryFlags.CluesFound, Is.EqualTo(1));
        Assert.That(StoryFlags.HasClue(TapeTransport.EvidenceId), Is.True);
    }

    [Test]
    public void EmptyFrame_GrantsNothing()
    {
        var tape = new TapeTransport();
        tape.TogglePlay();
        tape.Synchronize(5f);
        Assert.That(tape.CaptureEvidence(true), Is.False);
        Assert.That(StoryFlags.CluesFound, Is.Zero);
        Assert.That(StoryFlags.SawRecording, Is.False);
    }

    // U-06: кадр засчитывается всё время, пока фигура в кадре, а не только когда она смотрит в объектив.
    [Test]
    public void Capture_CountsAnyStoppedFrameWithFigureInView()
    {
        var tape = new TapeTransport();
        tape.TogglePlay();
        float[] times = { 26f, 29f, NightTape.FaceToLens - 1f, 37f, 60f };
        foreach (float t in times)
        {
            tape.Synchronize(t);
            Assert.That(NightTape.Figure(t).Look, Is.LessThan(0.9f), "t=" + t);
            Assert.That(tape.CanCapture(true), Is.True, "t=" + t);
        }
        Assert.That(tape.CaptureEvidence(true), Is.True);
        Assert.That(StoryFlags.HasClue(TapeTransport.EvidenceId), Is.True);
    }

    [Test]
    public void Capture_RefusesFigureOutOfView()
    {
        var tape = new TapeTransport();
        tape.TogglePlay();
        tape.Synchronize(NightTape.FaceToLens + 0.5f);
        Assert.That(tape.CaptureEvidence(false), Is.False);
        Assert.That(StoryFlags.CluesFound, Is.Zero);
    }
}
