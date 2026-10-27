using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{

// Значения сохраняются в PlayerPrefs — только дописывать новые в конец.
public enum StoryChapter
{
    DayOne = 0,
    NightOne = 1,
    DayTwo = 2,
    NightTwo = 3
}

// Чекпойнты по главам (F-04): в начале каждого дня/ночи сохраняются глава, StoryFlags и
// инвентарь. Восстанавливает IntroSequence теми же прыжками, что и дебаг (DebugStoryStage).
// Меню кладёт сюда запрос запуска, сцена игры забирает его в IntroSequence.Start.
// Глава открывается в «Выборе главы» не сразу, а когда игрок, дойдя до неё, выходит из игры —
// в главное меню или совсем (CommitReached). Дебаг-прыжки прогресс не пишут.
public static class ChapterCheckpoints
{
    public sealed class ChapterInfo
    {
        public readonly StoryChapter Chapter;
        public readonly string Title;
        public readonly DebugStoryStage Stage;
        public ChapterInfo(StoryChapter chapter, string title, DebugStoryStage stage) { Chapter = chapter; Title = title; Stage = stage; }
    }

    [Serializable]
    private sealed class Snapshot
    {
        public int chapter;
        public int knockTrust;
        public bool knockAnswered;
        public bool sawRecording;
        public bool neighborNoteRead;
        public bool frontDoorLocked;
        public int cameraPlacementSlot;
        public string[] clues = Array.Empty<string>();
        public string[] items = Array.Empty<string>();
        public int[] quantities = Array.Empty<int>();
    }

    public static readonly ChapterInfo[] Chapters =
    {
        new ChapterInfo(StoryChapter.DayOne, "1. Переезд", DebugStoryStage.None),
        new ChapterInfo(StoryChapter.NightOne, "2. Первая ночь", DebugStoryStage.StartNightOne),
        new ChapterInfo(StoryChapter.DayTwo, "3. Второй день", DebugStoryStage.StartDay2Decoration),
        new ChapterInfo(StoryChapter.NightTwo, "4. Вторая ночь", DebugStoryStage.StartNight),
    };

    // Ключи kfb.save.* (до правки) забиты дебаг-прыжками — прогресс начинается заново.
    private const string LastKey = "kfb.progress.last";
    private const string UnlockedKey = "kfb.progress.unlocked";
    private const string ChapterKeyPrefix = "kfb.progress.chapter.";

    private static bool hasLaunch;
    private static StoryChapter launchChapter;
    private static Snapshot launchSnapshot;
    private static int reachedThisSession = -1;
    private static bool savesDisabled;

    public static bool HasSave => PlayerPrefs.HasKey(LastKey);
    public static StoryChapter LastChapter => (StoryChapter)PlayerPrefs.GetInt(LastKey, 0);
    public static bool IsUnlocked(StoryChapter chapter) => (int)chapter <= PlayerPrefs.GetInt(UnlockedKey, -1);

    public static ChapterInfo Info(StoryChapter chapter)
    {
        foreach (ChapterInfo info in Chapters) if (info.Chapter == chapter) return info;
        return Chapters[0];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        ResetLaunch();
        reachedThisSession = -1;
        savesDisabled = false;
        Application.quitting -= CommitReached;
        Application.quitting += CommitReached;
    }

    private static void ResetLaunch()
    {
        hasLaunch = false;
        launchSnapshot = null;
    }

    // Сессия запущена дебаг-прыжком: ни сохранений, ни открытия глав.
    public static void DisableSaves() => savesDisabled = true;
    public static bool SavesDisabled => savesDisabled;

    // Выход из игры (главное меню или закрытие): открыть главы, до которых игрок дошёл.
    public static void CommitReached()
    {
        if (reachedThisSession > PlayerPrefs.GetInt(UnlockedKey, -1))
        {
            PlayerPrefs.SetInt(UnlockedKey, reachedThisSession);
            PlayerPrefs.Save();
        }
        reachedThisSession = -1;
    }

    public static void Save(StoryChapter chapter, PlayerInventory inventory)
    {
        if (savesDisabled) return;
        var snapshot = new Snapshot
        {
            chapter = (int)chapter,
            knockTrust = StoryFlags.KnockTrust,
            knockAnswered = StoryFlags.KnockAnswered,
            sawRecording = StoryFlags.SawRecording,
            neighborNoteRead = StoryFlags.NeighborNoteRead,
            frontDoorLocked = StoryFlags.FrontDoorLocked,
            cameraPlacementSlot = StoryFlags.CameraPlacementSlot,
            clues = new List<string>(StoryFlags.Clues).ToArray(),
        };
        if (inventory != null)
        {
            var items = new List<string>();
            var quantities = new List<int>();
            foreach (InventoryEntry entry in inventory.Entries)
            {
                if (entry?.Definition == null) continue;
                items.Add(entry.Definition.Id);
                quantities.Add(entry.Quantity);
            }
            snapshot.items = items.ToArray();
            snapshot.quantities = quantities.ToArray();
        }
        PlayerPrefs.SetString(ChapterKeyPrefix + (int)chapter, JsonUtility.ToJson(snapshot));
        PlayerPrefs.SetInt(LastKey, (int)chapter);
        PlayerPrefs.Save();
        reachedThisSession = Mathf.Max(reachedThisSession, (int)chapter);
    }

    // «Продолжить» — последняя сохранённая глава с её флагами и инвентарём.
    public static bool RequestContinue() => HasSave && RequestChapter(LastChapter);

    // «Выбор главы» — состояние на момент, когда игрок дошёл до этой главы.
    public static bool RequestChapter(StoryChapter chapter)
    {
        string json = PlayerPrefs.GetString(ChapterKeyPrefix + (int)chapter, string.Empty);
        Snapshot snapshot = null;
        if (!string.IsNullOrEmpty(json))
        {
            try { snapshot = JsonUtility.FromJson<Snapshot>(json); }
            catch (ArgumentException) { snapshot = null; }
        }
        hasLaunch = true;
        launchChapter = chapter;
        launchSnapshot = snapshot;
        return true;
    }

    // Запуск из меню — настоящая игра, даже если до этого в сессии был дебаг-прыжок.
    public static void ClearLaunch()
    {
        ResetLaunch();
        savesDisabled = false;
    }

    // Забирает запрос один раз: перезагрузка сцены после этого — обычная новая игра.
    public static bool TryTakeLaunch(out StoryChapter chapter)
    {
        chapter = launchChapter;
        if (!hasLaunch) return false;
        hasLaunch = false;
        return true;
    }

    // Флаги и инвентарь главы. Вызывать после TryTakeLaunch, до прыжка на этап.
    public static void ApplyLaunchState(PlayerInventory inventory)
    {
        Snapshot snapshot = launchSnapshot;
        launchSnapshot = null;
        StoryFlags.Reset();
        if (snapshot == null) return;
        StoryFlags.Restore(snapshot.knockTrust, snapshot.sawRecording, snapshot.neighborNoteRead,
            snapshot.frontDoorLocked, snapshot.clues, snapshot.knockAnswered);
        StoryFlags.SetCameraPlacementSlot(snapshot.cameraPlacementSlot);
        if (inventory == null || snapshot.items == null) return;
        for (int i = 0; i < snapshot.items.Length; i++)
        {
            string id = snapshot.items[i];
            int quantity = snapshot.quantities != null && i < snapshot.quantities.Length ? snapshot.quantities[i] : 1;
            if (string.IsNullOrEmpty(id) || inventory.HasItem(id)) continue;
            if (inventory.AddItem(id, Mathf.Max(1, quantity))) continue;
            if (id == "Flashlight") inventory.hasFlashlight = true;
            else if (id == "Crowbar") inventory.hasCrowbar = true;
            else if (id == "Hammer") inventory.hasHammer = true;
        }
    }
}
}
