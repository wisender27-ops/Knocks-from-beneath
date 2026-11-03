using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KnocksFromBeneath
{
// L-02: локализация. Исходный язык — русский: русская строка в коде и есть ключ.
// Каждая русская строка, которую видит игрок, проходит через Loc.T (или Loc.F для строк с {0}).
// Таблицы переводов — Resources/Localization/<code>.json, их покрытие проверяет LocalizationTests.
public static class Loc
{
    public const string SourceLanguage = "ru";
    public const string PrefKey = "kfb.settings.language";
    const string TablePath = "Localization/";

    public readonly struct Language
    {
        public readonly string Code, NativeName, CultureName;
        public Language(string code, string nativeName, string cultureName)
        { Code = code; NativeName = nativeName; CultureName = cultureName; }
    }

    // Порядок — порядок в настройках. Коды не менять: они сохраняются в PlayerPrefs.
    public static readonly Language[] Languages =
    {
        new Language("ru", "Русский", "ru-RU"),
        new Language("en", "English", "en-US"),
        new Language("zh-Hans", "简体中文", "zh-CN"),
        new Language("es", "Español", "es-ES"),
        new Language("pt-BR", "Português (Brasil)", "pt-BR"),
        new Language("de", "Deutsch", "de-DE"),
        new Language("fr", "Français", "fr-FR"),
        new Language("ja", "日本語", "ja-JP"),
        new Language("ko", "한국어", "ko-KR"),
        new Language("pl", "Polski", "pl-PL"),
        new Language("tr", "Türkçe", "tr-TR"),
        new Language("it", "Italiano", "it-IT"),
        new Language("uk", "Українська", "uk-UA"),
    };

    [Serializable] public class Entry { public string source; public string text; }
    [Serializable] public class Table { public string language; public List<Entry> entries = new List<Entry>(); }

    static string current;
    static Dictionary<string, string> table;
    static readonly HashSet<string> reportedMissing = new HashSet<string>();

    // Срабатывает после смены языка: шрифты (LocFonts) и подписи сцены (LocSceneTexts).
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { current = null; table = null; Changed = null; reportedMissing.Clear(); }

    public static string Current { get { EnsureLoaded(); return current; } }
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Find(Current).CultureName);

    public static string T(string source)
    {
        EnsureLoaded();
        if (table == null || string.IsNullOrEmpty(source)) return source;
        if (table.TryGetValue(source, out string text)) return text;
        if (reportedMissing.Add(source)) Debug.LogError($"[Loc] Нет перевода ({current}): \"{source}\"");
        return source;
    }

    // Перевод набора ключей (реплики, объявленные через Loc.Key в статических полях).
    public static string[] T(string[] sources) => Array.ConvertAll(sources, T);

    // Помечает русскую строку как ключ там, где переводить ещё рано: инициализаторы полей и статические таблицы
    // (PlayerPrefs и Resources нельзя трогать из конструктора MonoBehaviour). Переводит её позже Loc.T в месте показа.
    public static string Key(string source) => source;

    public static string F(string sourceFormat, params object[] args) => string.Format(Culture, T(sourceFormat), args);

    // Заглавные буквы по правилам языка (турецкое i → İ).
    public static string Upper(string text) => text?.ToUpper(Culture);

    public static bool HasTranslation(string source)
    {
        EnsureLoaded();
        return table == null || table.ContainsKey(source);
    }

    // Выбор игрока: сохраняется и применяется сразу.
    public static void Select(string code)
    {
        Apply(code);
        PlayerPrefs.SetString(PrefKey, code);
        PlayerPrefs.Save();
    }

    // Применить без сохранения (тесты, первый запуск).
    public static void Apply(string code)
    {
        Find(code);
        table = code == SourceLanguage ? null : LoadTable(code);
        current = code;
        reportedMissing.Clear();
        Changed?.Invoke();
    }

    public static Language Find(string code)
    {
        foreach (Language language in Languages)
            if (language.Code == code) return language;
        throw new ArgumentException($"[Loc] Неизвестный язык: {code}");
    }

    public static Dictionary<string, string> LoadTable(string code)
    {
        TextAsset asset = Resources.Load<TextAsset>(TablePath + code);
        if (asset == null) throw new InvalidOperationException($"[Loc] Нет таблицы Resources/{TablePath}{code}.json");
        Table parsed = JsonUtility.FromJson<Table>(asset.text);
        var result = new Dictionary<string, string>(parsed.entries.Count, StringComparer.Ordinal);
        foreach (Entry entry in parsed.entries) result.Add(entry.source, entry.text);
        return result;
    }

    static void EnsureLoaded()
    {
        if (current != null) return;
        string saved = PlayerPrefs.GetString(PrefKey, "");
        Apply(IsKnown(saved) ? saved : FromSystemLanguage(Application.systemLanguage));
    }

    static bool IsKnown(string code)
    {
        foreach (Language language in Languages)
            if (language.Code == code) return true;
        return false;
    }

    public static string FromSystemLanguage(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.Russian: case SystemLanguage.Belarusian: return "ru";
            case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: case SystemLanguage.ChineseTraditional: return "zh-Hans";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.Portuguese: return "pt-BR";
            case SystemLanguage.German: return "de";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Polish: return "pl";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Ukrainian: return "uk";
            default: return "en";
        }
    }
}
}
