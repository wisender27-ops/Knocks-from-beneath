using System;
using System.Linq;
using System.Reflection;
using KnocksFromBeneath;
using NUnit.Framework;

public sealed class DebugStoryStageTests
{
    private static Type Drawer => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("DebugStoryStageDrawer")).First(t => t != null);

    [Test]
    public void Inspector_ContainsEveryStageExactlyOnce()
    {
        var entries = (Array)Drawer.GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var stages = entries.Cast<object>().Select(e => (DebugStoryStage)e.GetType()
            .GetField("Stage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(e)).ToArray();
        CollectionAssert.AreEquivalent(Enum.GetValues(typeof(DebugStoryStage)), stages);
        Assert.That(stages.Distinct().Count(), Is.EqualTo(stages.Length));
    }

    [Test]
    public void Inspector_UnknownValueIsNotDisplayedAsNone()
    {
        var index = Drawer.GetMethod("IndexOf", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(index.Invoke(null, new object[] { int.MaxValue }), Is.EqualTo(-1));
        Assert.That((int)index.Invoke(null, new object[] { (int)DebugStoryStage.PlayNightRecording }), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void StageValues_PreserveSerializedContracts()
    {
        Assert.That((int)DebugStoryStage.StartBoxQuest, Is.EqualTo(1));
        Assert.That((int)DebugStoryStage.StartNight, Is.EqualTo(5));
        Assert.That((int)DebugStoryStage.StartBreakFloor, Is.EqualTo(9));
        Assert.That((int)DebugStoryStage.PlayNightRecording, Is.EqualTo(25));
        Assert.That((int)DebugStoryStage.StartNightTwoExits, Is.EqualTo(26));
        Assert.That((int)DebugStoryStage.StartCameraQuest, Is.EqualTo(27));
        Assert.That((int)DebugStoryStage.StartBasement, Is.EqualTo(28));
        Assert.That((int)DebugStoryStage.StartHatchChoice, Is.EqualTo(29));
    }
}
