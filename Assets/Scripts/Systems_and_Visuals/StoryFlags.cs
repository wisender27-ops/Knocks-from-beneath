using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{

// Сюжетные флаги, копящиеся за оба дня и решающие концовку (DESIGN_ROADMAP, раздел 5).
// Сбрасываются при старте Play Mode и при новой игре из меню (MainMenuController.StartGame);
// часть пишется сама из GameEvents, остальное — через методы ниже из механик (K-03, M-05, C-03).
public static class StoryFlags
{
    private static readonly HashSet<string> clues = new HashSet<string>();

    // Доверие стучащего снизу (перестук M1): успех повышает, провал/игнор понижает.
    public static int KnockTrust { get; private set; }
    // Игрок увидел аномалию на записи видеокамеры (M2).
    public static bool SawRecording { get; private set; }
    // Прочитана записка соседа (GameEvents.OnNeighborNoteRead) — открывает звонок M6.
    public static bool NeighborNoteRead { get; private set; }
    public static int CluesFound => clues.Count;
    public static IReadOnlyCollection<string> Clues => clues;

    // Единственный источник правды остаётся в GameState — его читают дверь и концовки.
    public static bool FrontDoorLocked
    {
        get => GameState.frontDoorLocked;
        set => GameState.frontDoorLocked = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        Reset();
        Install();
    }

    // Идемпотентно: повторный вызов не дублирует подписки.
    public static void Install()
    {
        GameEvents.OnNeighborNoteRead -= HandleNeighborNoteRead;
        GameEvents.OnNeighborNoteRead += HandleNeighborNoteRead;
    }

    public static void Reset()
    {
        KnockTrust = 0;
        SawRecording = false;
        NeighborNoteRead = false;
        clues.Clear();
        GameState.frontDoorLocked = false;
    }

    public static void AddKnockTrust(int delta) => KnockTrust += delta;
    public static void MarkRecordingSeen() => SawRecording = true;

    // Возвращает true только для новой улики — повторный осмотр не накручивает счётчик.
    public static bool RegisterClue(string id) => !string.IsNullOrEmpty(id) && clues.Add(id);
    public static bool HasClue(string id) => !string.IsNullOrEmpty(id) && clues.Contains(id);

    private static void HandleNeighborNoteRead() => NeighborNoteRead = true;
}
}
