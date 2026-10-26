using System;
using UnityEngine;

namespace KnocksFromBeneath
{

// Значения сохраняются в PlayerPrefs (биты открытых концовок) — только дописывать новые в конец.
public enum EndingId
{
    Boarded = 0,  // E1 «Тихо» — заколотил люк
    Pushed = 1,   // E2 «Внизу» — монстр столкнул в подвал
    Hidden = 2,   // E3 «Ты остался здесь» — прятки
    Escaped = 3,  // E4 «Побег» — через входную дверь
    Answer = 4    // E5 «Ответ» — истинная
}

// Реестр концовок (F-05): название, номер «(k/N)» и факт открытия. Номер и общее число
// берутся отсюда — новая концовка добавляется одной строкой в Endings. Галерея (B-06) читает IsSeen.
public static class EndingRegistry
{
    public sealed class EndingInfo
    {
        public readonly EndingId Id;
        public readonly string Title;
        public EndingInfo(EndingId id, string title) { Id = id; Title = title; }
    }

    public static readonly EndingInfo[] Endings =
    {
        new EndingInfo(EndingId.Boarded, "Тихо"),
        new EndingInfo(EndingId.Pushed, "Внизу"),
        new EndingInfo(EndingId.Hidden, "Ты остался здесь"),
        new EndingInfo(EndingId.Escaped, "Побег"),
        new EndingInfo(EndingId.Answer, "Ответ"),
    };

    private const string SeenKey = "kfb.endings.seen";

    public static int Count => Endings.Length;

    public static EndingInfo Info(EndingId id)
    {
        foreach (EndingInfo info in Endings) if (info.Id == id) return info;
        throw new ArgumentOutOfRangeException(nameof(id), id, "Концовки нет в реестре");
    }

    // Порядковый номер в реестре, с единицы.
    public static int Number(EndingId id) => Array.IndexOf(Endings, Info(id)) + 1;

    // Текст карточки концовки: «КОНЕЦ: ТИХО (1/5)».
    public static string ScreenTitle(EndingId id) =>
        "КОНЕЦ: " + Info(id).Title.ToUpperInvariant() + " (" + Number(id) + "/" + Count + ")";

    public static bool IsSeen(EndingId id) => (PlayerPrefs.GetInt(SeenKey, 0) & (1 << (int)id)) != 0;

    public static int SeenCount
    {
        get
        {
            int count = 0;
            foreach (EndingInfo info in Endings) if (IsSeen(info.Id)) count++;
            return count;
        }
    }

    public static void MarkSeen(EndingId id)
    {
        int mask = PlayerPrefs.GetInt(SeenKey, 0) | (1 << (int)id);
        PlayerPrefs.SetInt(SeenKey, mask);
        PlayerPrefs.Save();
    }
}
}
