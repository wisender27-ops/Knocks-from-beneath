using UnityEngine;

namespace KnocksFromBeneath
{
// Значения не менять — только дописывать в конец.
public enum UiSound
{
    Hover = 0, Click = 1, Back = 2, Toggle = 3,
    PauseOpen = 4, PauseClose = 5,
    InventoryOpen = 6, InventoryClose = 7, InventoryMove = 8,
    JournalOpen = 9, JournalClose = 10, JournalPage = 11,
    QuestNew = 12, QuestDone = 13, MenuStart = 14
}

// A-01: все новые клипы игры в одном ассете (Resources/Audio/GameSounds), заполняется
// Editor-инструментом A01AudioInstaller. Код берёт клипы отсюда, сцены не трогаются.
public sealed class GameSoundLibrary : ScriptableObject
{
    [Header("UI (индекс = UiSound)")]
    public AudioClip[] ui = new AudioClip[0];
    public AudioClip[] thoughtTicks = new AudioClip[0];

    [Header("Фон")]
    public AudioClip dayOutside, nightOutside, rain, basement, roomTone, fridgeHum, lampBuzz;
    [Header("Дроны")]
    public AudioClip droneMenu, droneNight1, droneNight2, droneBasement;
    [Header("Дом ночью — редкие одиночные звуки")]
    public AudioClip[] houseCreaks = new AudioClip[0];
    public AudioClip[] thunder = new AudioClip[0];
    public AudioClip[] distantDogs = new AudioClip[0];

    [Header("Хоррор")]
    public AudioClip heartbeat, tinnitus, riser;
    public AudioClip[] subHits = new AudioClip[0];
    public AudioClip[] darkHits = new AudioClip[0];
    public AudioClip[] breathScared = new AudioClip[0];

    [Header("Шаги")]
    public AudioClip[] stepWood = new AudioClip[0], stepConcrete = new AudioClip[0], stepGrass = new AudioClip[0],
        stepCarpet = new AudioClip[0], stepStairs = new AudioClip[0], stepDirt = new AudioClip[0], stepMetal = new AudioClip[0];
    public AudioClip[] cloth = new AudioClip[0];

    [Header("Двери и свет")]
    public AudioClip[] doorCreaks = new AudioClip[0], doorOpen = new AudioClip[0], doorClose = new AudioClip[0];
    public AudioClip doorLatch, doorLock, switchOn, switchOff, flashlightOn, flashlightOff;

    [Header("События")]
    public AudioClip[] hammerNail = new AudioClip[0];
    public AudioClip nailingAbove, doorKnock, doorLanding, lampFlicker, pryBreak, stoveIgnite, breakerOff, plateSetDown;

    [Header("Предметы")]
    public AudioClip[] grab = new AudioClip[0], equip = new AudioClip[0], paperGrab = new AudioClip[0], metalGrab = new AudioClip[0];
    public AudioClip[] cardboardDrop = new AudioClip[0], paperDrop = new AudioClip[0], plateDrop = new AudioClip[0],
        metalDrop = new AudioClip[0], glassDrop = new AudioClip[0], woodDrop = new AudioClip[0], softDrop = new AudioClip[0];
}

public static class GameSounds
{
    const string LibraryPath = "Audio/GameSounds";
    static GameSoundLibrary library;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { library = null; loaded = false; }

    public static GameSoundLibrary Library
    {
        get
        {
            if (!loaded) { library = Resources.Load<GameSoundLibrary>(LibraryPath); loaded = true; }
            return library;
        }
    }

    public static AudioClip Ui(UiSound sound)
    {
        var lib = Library;
        int i = (int)sound;
        return lib != null && i < lib.ui.Length ? lib.ui[i] : null;
    }

    public static float UiVolume(UiSound sound) => sound == UiSound.Hover || sound == UiSound.InventoryMove ? .55f : .8f;
    public static float UiPitch(UiSound sound) => sound == UiSound.Hover ? Random.Range(.96f, 1.04f) : 1f;

    // Случайный клип без повтора предыдущего (для шагов, скрипов и т.п.).
    public static AudioClip Pick(AudioClip[] clips, ref int last)
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];
        int i = Random.Range(0, clips.Length - 1);
        if (i >= last) i++;
        last = i;
        return clips[i];
    }

    public static AudioClip Pick(AudioClip[] clips)
    {
        int none = -1;
        return Pick(clips, ref none);
    }
}
}
