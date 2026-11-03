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
        // Тексты хранятся русскими ключами Loc; свойства отдают перевод на текущий язык.
        private readonly string title, hint, summary;
        public string Title => Loc.T(title);
        // Hint — намёк на условие без спойлера (закрытая кассета); Summary — итог (открытая).
        public string Hint => Loc.T(hint);
        public string Summary => Loc.T(summary);
        public EndingInfo(EndingId id, string title, string hint = "", string summary = "")
        {
            Id = id; this.title = title; this.hint = hint; this.summary = summary;
        }
    }

    public static readonly EndingInfo[] Endings =
    {
        new EndingInfo(EndingId.Boarded, Loc.Key("Тихо"),
            Loc.Key("Иногда люк лучше оставить закрытым. Навсегда."),
            Loc.Key("Дэниел снова заколотил люк. Снизу звал человек — потом затих. Как в прошлый раз.")),
        new EndingInfo(EndingId.Pushed, Loc.Key("Внизу"),
            Loc.Key("У люка нельзя медлить."),
            Loc.Key("Доски не выдержали. Оно забрало того, за кем пришло.")),
        new EndingInfo(EndingId.Hidden, Loc.Key("Ты остался здесь"),
            Loc.Key("Спрятаться — не значит скрыться. Тихий чёрный ход не безопаснее парадного."),
            Loc.Key("Оно нашло Дэниела раньше, чем он успел уйти.")),
        new EndingInfo(EndingId.Escaped, Loc.Key("Побег"),
            Loc.Key("Дорога ведёт прочь от дома. Не оглядывайся."),
            Loc.Key("Дэниел уехал. В новой комнате под полом снова постучали.")),
        new EndingInfo(EndingId.Answer, Loc.Key("Ответ"),
            Loc.Key("Тому, кто стучит, можно ответить. Не всё надо заколачивать."),
            Loc.Key("Дэниел спустился и впервые не сбежал. Теперь каждую ночь стучать будет он.")),
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
        Loc.F("КОНЕЦ: {0} ({1}/{2})", Loc.Upper(Info(id).Title), Number(id), Count);

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
