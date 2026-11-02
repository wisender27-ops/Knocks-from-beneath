using KnocksFromBeneath;
using NUnit.Framework;

public sealed class VentBoltsTests
{
    [Test]
    public void Hold_TakesTheFullTimeToRemoveABolt()
    {
        var bolts = new VentBolts();
        Assert.That(bolts.Hold(0, VentBolts.HoldSeconds * 0.5f), Is.False);
        Assert.That(bolts.IsRemoved(0), Is.False);
        Assert.That(bolts.Hold(0, VentBolts.HoldSeconds * 0.5f), Is.True);
        Assert.That(bolts.Removed, Is.EqualTo(1));
    }

    [Test]
    public void Release_KeepsProgressAndARemovedBoltCountsOnce()
    {
        var bolts = new VentBolts();
        bolts.Hold(2, 0.5f);
        Assert.That(bolts.Progress(2), Is.GreaterThan(0f));
        bolts.Hold(2, VentBolts.HoldSeconds);
        Assert.That(bolts.Hold(2, VentBolts.HoldSeconds), Is.False);
        Assert.That(bolts.Removed, Is.EqualTo(1));
    }

    [Test]
    public void Finished_OnlyWhenAllFourAreOut()
    {
        var bolts = new VentBolts();
        for (int i = 0; i < VentBolts.Count - 1; i++) bolts.Hold(i, VentBolts.HoldSeconds);
        Assert.That(bolts.Finished, Is.False);
        bolts.Hold(VentBolts.Count - 1, VentBolts.HoldSeconds);
        Assert.That(bolts.Finished, Is.True);
    }

    [Test]
    public void Hold_IgnoresABoltThatDoesNotExist()
    {
        var bolts = new VentBolts();
        Assert.That(bolts.Hold(-1, 1f), Is.False);
        Assert.That(bolts.Hold(VentBolts.Count, 1f), Is.False);
    }
}
