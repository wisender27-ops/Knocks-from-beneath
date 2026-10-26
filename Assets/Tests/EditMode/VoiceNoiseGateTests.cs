using NUnit.Framework;
using KnocksFromBeneath;

public class VoiceNoiseGateTests
{
    const float Dt = 0.05f;
    const float Near = 1.2f;
    const float Quiet = -60f;   // тихая комната
    const float Whisper = -44f; // тихий шёпот
    const float Speech = -18f;
    const float QuietVoice = -30f; // негромкий голос — уже выше лимита

    static VoiceNoiseGate Calibrated(float roomDb)
    {
        var gate = new VoiceNoiseGate();
        for (float t = 0f; t < VoiceNoiseGate.CalibrationSeconds + Dt; t += Dt) gate.Observe(Dt, roomDb);
        return gate;
    }

    static void Run(VoiceNoiseGate gate, float seconds, float levelDb, float distance)
    {
        for (float t = 0f; t < seconds; t += Dt) gate.Advance(Dt, levelDb, distance);
    }

    [Test]
    public void Calibration_MeasuresRoomNoise()
    {
        var gate = Calibrated(Quiet);
        Assert.That(gate.Calibrated, Is.True);
        Assert.That(gate.NoiseFloor, Is.EqualTo(Quiet).Within(0.5f));
        Assert.That(gate.Limit, Is.EqualTo(VoiceNoiseGate.LimitFloorDb), "quiet room: limit stays at the absolute floor");
    }

    [Test]
    public void Silence_NextToMonster_IsSafe()
    {
        var gate = Calibrated(Quiet);
        Run(gate, 10f, Quiet, Near);
        Assert.That(gate.Found, Is.False);
        Assert.That(gate.Suspicion, Is.Zero);
    }

    [Test]
    public void Whisper_IsUnderTheLimit()
    {
        var gate = Calibrated(Quiet);
        Run(gate, 5f, Whisper, Near);
        Assert.That(gate.Loud, Is.False);
        Assert.That(gate.Found, Is.False);
    }

    [Test]
    public void NormalSpeech_NextToMonster_GetsFoundFast()
    {
        var gate = Calibrated(Quiet);
        Run(gate, 0.6f, Speech, Near);
        Assert.That(gate.Found, Is.True);
    }

    [Test]
    public void Speech_DoesNotRaiseTheNoiseFloor()
    {
        var gate = Calibrated(Quiet);
        Run(gate, 3f, Speech, 50f);
        Assert.That(gate.NoiseFloor, Is.EqualTo(Quiet).Within(1f), "voice must not be learned as background");
    }

    [Test]
    public void NoisyRoom_RaisesTheLimit_SoBackgroundIsNotHeard()
    {
        var gate = Calibrated(-35f); // вентилятор, улица
        Assert.That(gate.Limit, Is.EqualTo(-35f + VoiceNoiseGate.Margin).Within(0.5f));
        Run(gate, 10f, -35f, Near);
        Assert.That(gate.Found, Is.False);
        Run(gate, 1f, -5f, Near);
        Assert.That(gate.Found, Is.True, "shouting over the noise still gives you away");
    }

    [Test]
    public void FarAway_SpeechIsStillHeard_ButSlower()
    {
        var near = Calibrated(Quiet);
        var far = Calibrated(Quiet);
        Run(near, 0.2f, QuietVoice, Near);
        Run(far, 0.2f, QuietVoice, 10f);
        Assert.That(far.Suspicion, Is.LessThan(near.Suspicion));
        Run(far, 1.5f, QuietVoice, 10f);
        Assert.That(far.Found, Is.True, "talking out loud is heard from downstairs too");
    }

    [Test]
    public void Loudness_IsOneAtTheLimit()
    {
        var gate = Calibrated(Quiet);
        gate.Advance(Dt, gate.Limit, 50f);
        Assert.That(gate.Loudness, Is.EqualTo(1f).Within(0.05f));
    }

    [Test]
    public void ToDb_ConvertsRms()
    {
        var full = new float[] { 1f, -1f, 1f, -1f };
        Assert.That(VoiceNoiseGate.ToDb(full, 4), Is.EqualTo(0f).Within(0.01f));
        Assert.That(VoiceNoiseGate.ToDb(new float[4], 4), Is.EqualTo(VoiceNoiseGate.SilenceDb));
    }
}
