using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class ChapterCheckpointsTests
{
    // Тесты пишут в настоящие PlayerPrefs — сохраняем и возвращаем ключи прохождения.
    static readonly string[] Keys =
    {
        "kfb.save.last", "kfb.save.unlocked",
        "kfb.save.chapter.0", "kfb.save.chapter.1", "kfb.save.chapter.2", "kfb.save.chapter.3",
    };
    readonly Dictionary<string, string> backup = new Dictionary<string, string>();
    readonly Dictionary<string, int> backupInts = new Dictionary<string, int>();

    [SetUp]
    public void SetUp()
    {
        backup.Clear();
        backupInts.Clear();
        foreach (string key in Keys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            if (key.StartsWith("kfb.save.chapter.")) backup[key] = PlayerPrefs.GetString(key);
            else backupInts[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(key);
        }
        ChapterCheckpoints.ClearLaunch();
        StoryFlags.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
        foreach (var pair in backup) PlayerPrefs.SetString(pair.Key, pair.Value);
        foreach (var pair in backupInts) PlayerPrefs.SetInt(pair.Key, pair.Value);
        PlayerPrefs.Save();
        ChapterCheckpoints.ClearLaunch();
        StoryFlags.Reset();
    }

    [Test]
    public void Continue_RestoresFlagsOfLastChapter()
    {
        StoryFlags.AddKnockTrust(2);
        StoryFlags.MarkRecordingSeen();
        StoryFlags.RegisterClue("photo");
        StoryFlags.FrontDoorLocked = true;
        ChapterCheckpoints.Save(StoryChapter.NightTwo, null);
        StoryFlags.Reset();

        Assert.That(ChapterCheckpoints.RequestContinue(), Is.True);
        Assert.That(ChapterCheckpoints.TryTakeLaunch(out StoryChapter chapter), Is.True);
        ChapterCheckpoints.ApplyLaunchState(null);

        Assert.That(chapter, Is.EqualTo(StoryChapter.NightTwo));
        Assert.That(StoryFlags.KnockTrust, Is.EqualTo(2));
        Assert.That(StoryFlags.SawRecording, Is.True);
        Assert.That(StoryFlags.HasClue("photo"), Is.True);
        Assert.That(StoryFlags.FrontDoorLocked, Is.True);
    }

    [Test]
    public void Launch_IsTakenOnce_AndNewGameClearsIt()
    {
        ChapterCheckpoints.Save(StoryChapter.DayTwo, null);
        ChapterCheckpoints.RequestChapter(StoryChapter.DayTwo);
        Assert.That(ChapterCheckpoints.TryTakeLaunch(out _), Is.True);
        Assert.That(ChapterCheckpoints.TryTakeLaunch(out _), Is.False);

        ChapterCheckpoints.RequestChapter(StoryChapter.DayTwo);
        ChapterCheckpoints.ClearLaunch();
        Assert.That(ChapterCheckpoints.TryTakeLaunch(out _), Is.False);
    }

    [Test]
    public void Chapter_KeepsItsOwnSnapshot_AndUnlockOnlyGrows()
    {
        StoryFlags.AddKnockTrust(1);
        ChapterCheckpoints.Save(StoryChapter.NightOne, null);
        StoryFlags.AddKnockTrust(4);
        ChapterCheckpoints.Save(StoryChapter.NightTwo, null);
        ChapterCheckpoints.Save(StoryChapter.DayOne, null); // новая игра
        StoryFlags.Reset();

        Assert.That(ChapterCheckpoints.LastChapter, Is.EqualTo(StoryChapter.DayOne));
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.NightTwo), Is.True);
        Assert.That(ChapterCheckpoints.AnyChapterBeyondFirst, Is.True);

        ChapterCheckpoints.RequestChapter(StoryChapter.NightOne);
        ChapterCheckpoints.TryTakeLaunch(out _);
        ChapterCheckpoints.ApplyLaunchState(null);
        Assert.That(StoryFlags.KnockTrust, Is.EqualTo(1));
    }

    [Test]
    public void FreshProfile_HasNoSave()
    {
        Assert.That(ChapterCheckpoints.HasSave, Is.False);
        Assert.That(ChapterCheckpoints.AnyChapterBeyondFirst, Is.False);
        Assert.That(ChapterCheckpoints.RequestContinue(), Is.False);
    }
}
