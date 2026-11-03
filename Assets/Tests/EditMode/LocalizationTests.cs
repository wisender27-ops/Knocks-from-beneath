using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KnocksFromBeneath;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Все EditMode-тесты идут на исходном русском: прочие тесты сравнивают русские строки.
[SetUpFixture]
public class LocalizationTestSetup
{
    string previous;

    [OneTimeSetUp]
    public void ForceSourceLanguage()
    {
        previous = Loc.Current;
        Loc.Apply(Loc.SourceLanguage);
    }

    [OneTimeTearDown]
    public void RestoreLanguage() => Loc.Apply(previous);
}

// L-02: покрытие переводов. Ключ — русская строка из кода (Loc.T/F/Key) или из данных (сцены, ассеты предметов).
public class LocalizationTests
{
    static readonly Regex Cyrillic = new Regex("[А-Яа-яЁё]");
    static readonly Regex Placeholder = new Regex(@"\{\d+\}");

    // Вызовы, в которых русский текст не показывается игроку.
    static readonly Regex NonPlayerCalls = new Regex(
        @"(^|\.)(Log|LogWarning|LogError|LogFormat|Assert|Header|Tooltip|ContextMenu|AddComponentMenu|MenuItem|CreateAssetMenu|InspectorName|\w*Exception)$");

    // Русские строки, которые не текст для игрока: имя объекта в сцене, слова в именах объектов для звука, названия языков.
    static readonly string[] NonPlayerFiles =
    {
        "Scripts/Interactables/LightSwitch.cs",
        "Scripts/Systems_and_Visuals/Audio/ItemAudio.cs",
        "Scripts/Localization/Loc.cs",
    };

    const string TextMeshProUGuiScript = "f4688fdb7df04437aeb418b961361dc5";

    // Поля данных с текстом для игрока: класс компонента → имена полей.
    static readonly Dictionary<string, string[]> DataFields = new Dictionary<string, string[]>
    {
        { "InspectableClue", new[] { "displayName", "foundCaption", "thoughtLines" } },
        { "ArrivalCutscene", new[] { "storyPages" } },
        { "ToolPickup", new[] { "prompt" } },
        { "PickableItem", new[] { "destinationHint" } },
        { "QuestManager", new[] { "questTitle" } },
        { "TextMeshProUGUI", new[] { "m_text" } },
        { "InventoryItemDefinition", new[] { "displayName", "description" } },
    };

    static readonly string[] DataFiles =
    {
        "Scenes/Main Scene/Main.unity",
        "Scenes/Main Menu/MainMenu.unity",
    };

    [Test]
    public void EveryPlayerFacingRussianLiteralGoesThroughLoc()
    {
        var offenders = new List<string>();
        foreach (string file in RuntimeScripts())
        {
            string relative = Relative(file);
            if (NonPlayerFiles.Contains(relative)) continue;
            foreach (Literal literal in ScanLiterals(File.ReadAllText(file)))
                if (!literal.InLoc && Cyrillic.IsMatch(literal.Text) && !literal.CallStack.Any(c => NonPlayerCalls.IsMatch(c)))
                    offenders.Add($"{relative}:{literal.Line} \"{literal.Text}\"");
        }
        Assert.That(offenders, Is.Empty, "Русский текст мимо Loc.T/Loc.F/Loc.Key:\n" + string.Join("\n", offenders));
    }

    [Test]
    public void InterpolatedStringsAreNotKeys()
    {
        var offenders = new List<string>();
        foreach (string file in RuntimeScripts())
            foreach (Literal literal in ScanLiterals(File.ReadAllText(file)))
                if (literal.InLoc && literal.Interpolated) offenders.Add($"{Relative(file)}:{literal.Line}");
        Assert.That(offenders, Is.Empty, "В Loc.T/F/Key нельзя передавать $\"...\": ключ должен быть постоянным, используйте Loc.F.");
    }

    [Test]
    public void EveryTranslationTableCoversExactlyTheSourceKeys()
    {
        HashSet<string> keys = SourceKeys();
        foreach (Loc.Language language in Loc.Languages.Where(l => l.Code != Loc.SourceLanguage))
        {
            Dictionary<string, string> table = Loc.LoadTable(language.Code);
            string[] missing = keys.Where(k => !table.ContainsKey(k)).ToArray();
            string[] stale = table.Keys.Where(k => !keys.Contains(k)).ToArray();
            Assert.That(missing, Is.Empty, $"{language.Code}: нет перевода:\n" + string.Join("\n", missing));
            Assert.That(stale, Is.Empty, $"{language.Code}: лишние ключи (удалите или обновите):\n" + string.Join("\n", stale));
        }
    }

    [Test]
    public void TranslationsKeepPlaceholdersKeysAndLineBreaks()
    {
        var problems = new List<string>();
        foreach (Loc.Language language in Loc.Languages.Where(l => l.Code != Loc.SourceLanguage))
            foreach (KeyValuePair<string, string> pair in Loc.LoadTable(language.Code))
            {
                string source = pair.Key, text = pair.Value;
                if (string.IsNullOrWhiteSpace(text)) problems.Add($"{language.Code}: пусто: {source}");
                if (!SamePlaceholders(source, text)) problems.Add($"{language.Code}: плейсхолдеры: {source} → {text}");
                foreach (string prefix in new[] { "E ", "F " })
                    if (source.StartsWith(prefix) && !text.StartsWith(prefix)) problems.Add($"{language.Code}: нет «{prefix}» в начале: {text}");
                if (source.Count(c => c == '\n') != text.Count(c => c == '\n')) problems.Add($"{language.Code}: переносы строк: {text}");
                if (Placeholder.IsMatch(source)) Assert.DoesNotThrow(() => string.Format(text, 1, 2, 3), $"{language.Code}: формат {text}");
            }
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    // LocSceneTexts переводит подпись сцены, если её текст — ключ. Перевод не должен совпадать с другим ключом.
    [Test]
    public void TranslationIsNeverAnotherSourceKey()
    {
        var problems = new List<string>();
        foreach (Loc.Language language in Loc.Languages.Where(l => l.Code != Loc.SourceLanguage))
        {
            Dictionary<string, string> table = Loc.LoadTable(language.Code);
            foreach (KeyValuePair<string, string> pair in table)
                if (pair.Value != pair.Key && table.TryGetValue(pair.Value, out string again) && again != pair.Value)
                    problems.Add($"{language.Code}: «{pair.Key}» → «{pair.Value}» → «{again}»");
        }
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void UiFontRendersEveryTranslatedCharacter()
    {
        TMP_FontAsset ui = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/" + UiStyle.FontName + ".asset");
        Assert.That(ui, Is.Not.Null);
        var chain = new List<TMP_FontAsset>();
        Flatten(ui, chain);
        foreach (TMP_FontAsset global in TMP_Settings.fallbackFontAssets) Flatten(global, chain);
        var missing = new List<string>();
        foreach (Loc.Language language in Loc.Languages)
        {
            IEnumerable<string> texts = language.Code == Loc.SourceLanguage ? SourceKeys() : Loc.LoadTable(language.Code).Values;
            var chars = new HashSet<char>(texts.SelectMany(t => t).Concat(language.NativeName)
                .Where(c => !char.IsWhiteSpace(c) && !char.IsControl(c)));
            foreach (TMP_FontAsset font in chain) chars.RemoveWhere(c => Renders(font, c));
            if (chars.Count > 0) missing.Add($"{language.Code}: {new string(chars.ToArray())}");
        }
        Assert.That(missing, Is.Empty, "Нет глифов (Tools/Localization/Setup Fonts):\n" + string.Join("\n", missing));
    }

    [Test]
    public void SystemLanguageMapsToKnownLanguage()
    {
        foreach (SystemLanguage system in Enum.GetValues(typeof(SystemLanguage)))
            Assert.DoesNotThrow(() => Loc.Find(Loc.FromSystemLanguage(system)));
    }

    [Test]
    public void SourceLanguageReturnsKeyAndFormats()
    {
        Assert.That(Loc.T("Лечь спать"), Is.EqualTo("Лечь спать"));
        Assert.That(Loc.F("Выполнено: {0}", 3), Is.EqualTo("Выполнено: 3"));
        Assert.That(Loc.T(new[] { "А", "Б" }), Is.EqualTo(new[] { "А", "Б" }));
    }

    [Test]
    public void TurkishUppercaseKeepsDottedI()
    {
        Loc.Apply("tr");
        try { Assert.That(Loc.Upper("bitir"), Is.EqualTo("BİTİR")); }
        finally { Loc.Apply(Loc.SourceLanguage); }
    }

    // ---------- источники ключей ----------

    public static HashSet<string> SourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in RuntimeScripts())
            foreach (Literal literal in ScanLiterals(File.ReadAllText(file)))
                if (literal.InLoc && !literal.Interpolated) keys.Add(literal.Text);
        foreach (string text in DataStrings()) keys.Add(text);
        return keys;
    }

    static IEnumerable<string> DataStrings()
    {
        foreach (string file in DataFiles)
            foreach (string text in YamlStrings(Path.Combine(Application.dataPath, file))) yield return text;
        foreach (string guid in AssetDatabase.FindAssets("t:InventoryItemDefinition"))
            foreach (string text in YamlStrings(Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid)))) yield return text;
    }

    // Строки из полей DataFields в YAML сцены или ассета (Unity пишет не-ASCII в двойных кавычках с \uXXXX).
    static IEnumerable<string> YamlStrings(string path)
    {
        foreach (string document in File.ReadAllText(path).Split(new[] { "\n--- !u!" }, StringSplitOptions.None))
        {
            Match script = Regex.Match(document, @"m_Script: \{fileID: -?\d+, guid: (\w+)");
            if (!script.Success) continue;
            string guid = script.Groups[1].Value;
            string type = guid == TextMeshProUGuiScript ? "TextMeshProUGUI" : Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
            if (!DataFields.TryGetValue(type, out string[] fields)) continue;
            string[] lines = document.Split('\n');
            string field = null;
            for (int i = 0; i < lines.Length; i++)
            {
                Match key = Regex.Match(lines[i], @"^\s*(?:- )?(\w+):(.*)$");
                Match item = Regex.Match(lines[i], @"^\s*- (.*)$");
                string value;
                if (key.Success && !lines[i].TrimStart().StartsWith("- \""))
                {
                    field = key.Groups[1].Value;
                    value = key.Groups[2].Value.Trim();
                }
                else if (item.Success) value = item.Groups[1].Value.Trim();
                else continue;
                if (field == null || !fields.Contains(field) || !value.StartsWith("\"")) continue;
                string text = ReadQuoted(lines, ref i, value);
                if (field == "m_text") text = text.Trim();
                if (Cyrillic.IsMatch(text)) yield return text;
            }
        }
    }

    static string ReadQuoted(string[] lines, ref int index, string first)
    {
        var raw = new List<string> { first.Substring(1) };
        while (!EndsQuoted(raw[raw.Count - 1]) && index + 1 < lines.Length) raw.Add(lines[++index].Trim());
        string last = raw[raw.Count - 1];
        raw[raw.Count - 1] = last.Substring(0, last.LastIndexOf('"'));
        var folded = new StringBuilder();
        for (int i = 0; i < raw.Count; i++)
        {
            string line = raw[i];
            if (i > 0 && line.Length == 0) { folded.Append("\\n"); continue; }
            if (line.EndsWith("\\") && !line.EndsWith("\\\\") && i < raw.Count - 1) { folded.Append(line, 0, line.Length - 1); continue; }
            folded.Append(line);
            if (i < raw.Count - 1 && raw[i + 1].Length > 0) folded.Append(' ');
        }
        return Unescape(folded.ToString());
    }

    static bool EndsQuoted(string line)
    {
        int quotes = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (line[i] == '"') quotes++;
        }
        return quotes > 0;
    }

    static string Unescape(string text)
    {
        var result = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 >= text.Length) { result.Append(text[i]); continue; }
            char next = text[++i];
            switch (next)
            {
                case 'n': result.Append('\n'); break;
                case 't': result.Append('\t'); break;
                case '0': result.Append('\0'); break;
                case 'u': result.Append((char)Convert.ToInt32(text.Substring(i + 1, 4), 16)); i += 4; break;
                case 'x': result.Append((char)Convert.ToInt32(text.Substring(i + 1, 2), 16)); i += 2; break;
                default: result.Append(next); break;
            }
        }
        return result.ToString();
    }

    // ---------- разбор C#: строковые литералы и вызов, в котором они стоят ----------

    struct Literal
    {
        public string Text;
        public int Line;
        public bool InLoc, Interpolated;
        public string[] CallStack;
    }

    static IEnumerable<string> RuntimeScripts() =>
        Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(Application.dataPath, "Prefabs"), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Replace('\\', '/').Contains("/Editor/"));

    static string Relative(string file) => file.Replace('\\', '/').Substring(Application.dataPath.Length + 1);

    static List<Literal> ScanLiterals(string source)
    {
        var literals = new List<Literal>();
        var calls = new List<string>();
        var pending = new StringBuilder(); // код между последней скобкой и литералом
        int line = 1;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '\n') line++;
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                line++;
                continue;
            }
            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) end = source.Length - 2;
                line += source.Substring(i, end - i).Count(ch => ch == '\n');
                i = end + 1;
                continue;
            }
            if (c == '\'')
            {
                Match ch = Regex.Match(source.Substring(i, Math.Min(8, source.Length - i)), @"^'(\\.[^']*|[^\\'])'");
                if (ch.Success) { i += ch.Length - 1; continue; }
            }
            bool interpolated = c == '$' && i + 1 < source.Length && (source[i + 1] == '"' || source[i + 1] == '@');
            bool verbatim = (c == '@' && i + 1 < source.Length && (source[i + 1] == '"' || source[i + 1] == '$'));
            if (c == '"' || interpolated || verbatim)
            {
                int start = i;
                while (source[i] != '"') { if (source[i] == '@') verbatim = true; if (source[i] == '$') interpolated = true; i++; }
                var text = new StringBuilder();
                int startLine = line;
                for (i++; i < source.Length; i++)
                {
                    if (verbatim && source[i] == '"' && i + 1 < source.Length && source[i + 1] == '"') { text.Append('"'); i++; continue; }
                    if (!verbatim && source[i] == '\\') { text.Append(source, i, 2); i++; continue; }
                    if (source[i] == '"') break;
                    if (source[i] == '\n') line++;
                    text.Append(source[i]);
                }
                string top = calls.Count > 0 ? calls[calls.Count - 1] : "";
                bool direct = pending.ToString().Trim().Length == 0;
                literals.Add(new Literal
                {
                    Text = verbatim ? text.ToString() : Unescape(text.ToString()),
                    Line = startLine,
                    Interpolated = interpolated,
                    InLoc = direct && (top == "Loc.T" || top == "Loc.F" || top == "Loc.Key"),
                    CallStack = calls.ToArray(),
                });
                pending.Append(' ');
                continue;
            }
            if (c == '(' || c == '[')
            {
                Match name = Regex.Match(pending.ToString(), @"([\w.]+)\s*$");
                calls.Add(name.Success ? name.Groups[1].Value : "");
                pending.Clear();
            }
            else if (c == ')' || c == ']')
            {
                if (calls.Count > 0) calls.RemoveAt(calls.Count - 1);
                pending.Clear();
            }
            else if (c == ';' || c == '{' || c == '}') pending.Clear();
            else pending.Append(c);
        }
        return literals;
    }

    // ---------- шрифты ----------

    // Порядок поиска глифа TMP: шрифт, его запасные (вглубь), затем общие запасные TMP Settings.
    static void Flatten(TMP_FontAsset font, List<TMP_FontAsset> chain)
    {
        if (font == null || chain.Contains(font)) return;
        chain.Add(font);
        if (font.fallbackFontAssetTable != null)
            foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable) Flatten(fallback, chain);
    }

    static TMP_FontAsset loadedFace;

    // Статический атлас — по таблице символов; динамический — по самому TTF (атлас ещё не заполнен).
    static bool Renders(TMP_FontAsset font, char c)
    {
        if (font.HasCharacter(c, false, false)) return true;
        if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic || font.sourceFontFile == null) return false;
        if (loadedFace != font)
        {
            Assert.That(FontEngine.LoadFontFace(font.sourceFontFile), Is.EqualTo(FontEngineError.Success), font.name);
            loadedFace = font;
        }
        return FontEngine.TryGetGlyphIndex(c, out uint glyph) && glyph != 0;
    }

    static bool SamePlaceholders(string a, string b)
    {
        var left = Placeholder.Matches(a).Cast<Match>().Select(m => m.Value).OrderBy(s => s);
        var right = Placeholder.Matches(b).Cast<Match>().Select(m => m.Value).OrderBy(s => s);
        return left.SequenceEqual(right);
    }
}
