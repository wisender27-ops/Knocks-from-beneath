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
        // Hint — намёк на условие без спойлера (закрытая кассета); Summary — итог (открытая).
        public readonly string Hint;
        public readonly string Summary;
        public EndingInfo(EndingId id, string title, string hint = "", string summary = "")
        {
            Id = id; Title = title; Hint = hint; Summary = summary;
        }
    }

    public static readonly EndingInfo[] Endings =
    {
        new EndingInfo(EndingId.Boarded, "Тихо",
            "Иногда люк лучше оставить закрытым. Навсегда.",
            "Дэниел заколотил люк. Снизу кто-то кричал, но стук прекратился."),
        new EndingInfo(EndingId.Pushed, "Внизу",
            "Не всё решается у люка. Промедление стоит дорого.",
            "Гвозди не удержали. Монстр забрал того, кто стоял над люком."),
        new EndingInfo(EndingId.Hidden, "Ты остался здесь",
            "Спрятаться — не значит скрыться. Тихий чёрный ход не безопаснее парадного.",
            "Монстр нашёл Дэниела раньше, чем тот успел уйти."),
        new EndingInfo(EndingId.Escaped, "Побег",
            "Дорога ведёт прочь от дома. Не оглядывайся.",
            "Дэниел уехал. В новой комнате снова послышался стук под полом."),
        new EndingInfo(EndingId.Answer, "Ответ",
            "Тому, кто стучит, можно ответить. Не всё надо заколачивать.",
            "Дэниел спустился и ответил на стук. Теперь стучать приходится ему."),
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
