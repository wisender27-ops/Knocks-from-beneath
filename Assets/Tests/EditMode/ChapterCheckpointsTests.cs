using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class ChapterCheckpointsTests
{
    // Тесты пишут в настоящие PlayerPrefs — сохраняем и возвращаем ключи прохождения.
    static readonly string[] Keys =
    {
        "kfb.progress.last", "kfb.progress.unlocked",
        "kfb.progress.chapter.0", "kfb.progress.chapter.1", "kfb.progress.chapter.2", "kfb.progress.chapter.3",
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
            if (key.StartsWith("kfb.progress.chapter.")) backup[key] = PlayerPrefs.GetString(key);
            else backupInts[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(key);
        }
        ChapterCheckpoints.ClearLaunch();
        ChapterCheckpoints.CommitReached();
        foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
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
        StoryFlags.MarkKnockAnswered();
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
        Assert.That(StoryFlags.KnockAnswered, Is.True);
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
        ChapterCheckpoints.CommitReached();
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.NightTwo), Is.True);

        ChapterCheckpoints.RequestChapter(StoryChapter.NightOne);
        ChapterCheckpoints.TryTakeLaunch(out _);
        ChapterCheckpoints.ApplyLaunchState(null);
        Assert.That(StoryFlags.KnockTrust, Is.EqualTo(1));
    }

    [Test]
    public void FreshProfile_HasNoSave()
    {
        Assert.That(ChapterCheckpoints.HasSave, Is.False);
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.DayOne), Is.False);
        Assert.That(ChapterCheckpoints.RequestContinue(), Is.False);
    }

    [Test]
    public void ReachedChapter_OpensOnlyAfterExit()
    {
        ChapterCheckpoints.Save(StoryChapter.DayOne, null);
        ChapterCheckpoints.Save(StoryChapter.NightOne, null);
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.DayOne), Is.False);
        Assert.That(ChapterCheckpoints.HasSave, Is.True);

        ChapterCheckpoints.CommitReached();
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.NightOne), Is.True);
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.DayTwo), Is.False);
    }

    [Test]
    public void DebugSession_DoesNotSaveOrUnlock()
    {
        ChapterCheckpoints.DisableSaves();
        ChapterCheckpoints.Save(StoryChapter.NightTwo, null);
        ChapterCheckpoints.CommitReached();
        Assert.That(ChapterCheckpoints.HasSave, Is.False);
        Assert.That(ChapterCheckpoints.IsUnlocked(StoryChapter.NightTwo), Is.False);
    }
}
