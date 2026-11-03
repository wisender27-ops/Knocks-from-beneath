using System.Collections.Generic;
using KnocksFromBeneath;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// L-02: шрифты для переводов. Игровые шрифты (VCR, MADE Dillan, VT323) — статические атласы только с латиницей ASCII
// и кириллицей. Скрипт создаёт динамические «двойники» из тех же TTF (буквы с диакритикой того же начертания),
// добавляет их в запасные шрифты исходных, а китайский, японский и корейский ставит в общие запасные TMP Settings
// (порядок по языку меняет LocFonts). Повторный запуск ничего не дублирует.
public static class LocalizationFontSetup
{
    const string CjkFolder = "Assets/ThirdParty/Fonts/NotoSansCJK/";
    const string CjkAssetFolder = "Assets/Resources/" + LocFonts.FontFolder;
    const string TwinFolder = "Assets/Fonts/Localization/";

    // Исходный статический шрифт → TTF, из которого строится его динамический двойник.
    static readonly (string primary, string source, string twin)[] Twins =
    {
        ("Assets/Fonts/VCR OSD Mono Cyr SDF.asset", "Assets/Fonts/VCR OSD Mono Cyr.ttf", "VCR OSD Mono Cyr Dynamic SDF"),
        ("Assets/Fonts/MADE Dillan RUS SDF.asset", "Assets/Fonts/MADE Dillan RUS.otf", "MADE Dillan RUS Dynamic SDF"),
        ("Assets/Resources/Fonts/VT323 SDF.asset", "Assets/ThirdParty/Fonts/VT323/VT323-Regular.ttf", "VT323 Dynamic SDF"),
    };

    // У VCR нет польских и турецких букв (ł, ş, ğ…) — их добирает VT323, тоже пиксельный.
    const string LatinExtendedTwin = "VT323 Dynamic SDF";

    [MenuItem("Tools/Localization/Setup Fonts")]
    public static void Setup()
    {
        EnsureFolder(TwinFolder);
        EnsureFolder(CjkAssetFolder);

        var twins = new Dictionary<string, TMP_FontAsset>();
        foreach (var (_, source, twin) in Twins)
            twins[twin] = CreateDynamic(source, TwinFolder + twin + ".asset", 90, 9);

        foreach (var (primary, _, twin) in Twins)
        {
            TMP_FontAsset asset = Load<TMP_FontAsset>(primary);
            AddFallback(asset, twins[twin]);
            AddFallback(asset, twins[LatinExtendedTwin]);
        }

        List<TMP_FontAsset> global = TMP_Settings.fallbackFontAssets;
        foreach (string name in LocFonts.CjkFontNames)
        {
            string file = name.Replace(" SDF", "-Regular.ttf");
            TMP_FontAsset cjk = CreateDynamic(CjkFolder + file, CjkAssetFolder + name + ".asset", 64, 6);
            if (!global.Contains(cjk)) global.Add(cjk);
        }

        // Корейский пишется с пробелами: переносить по словам, а не посреди слова.
        var settings = new SerializedObject(TMP_Settings.instance);
        SerializedProperty hangul = settings.FindProperty("m_UseModernHangulLineBreakingRules");
        if (hangul == null) Debug.LogError("[L-02] В TMP Settings нет m_UseModernHangulLineBreakingRules — включите «Modern Hangul line breaking» вручную.");
        else hangul.boolValue = true;
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();
        Debug.Log("[L-02] Шрифты локализации настроены.");
    }

    static TMP_FontAsset CreateDynamic(string sourcePath, string assetPath, int samplingSize, int padding)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        Font font = Load<Font>(sourcePath);
        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, samplingSize, padding, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        asset.name = name;
        AssetDatabase.CreateAsset(asset, assetPath);
        asset.atlasTextures[0].name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    static void AddFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
    {
        if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (asset.fallbackFontAssetTable.Contains(fallback)) return;
        asset.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(asset);
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new System.InvalidOperationException($"[L-02] Нет ассета {path}");
        return asset;
    }

    static void EnsureFolder(string path)
    {
        string trimmed = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(trimmed)) return;
        string parent = System.IO.Path.GetDirectoryName(trimmed).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(trimmed));
    }
}
