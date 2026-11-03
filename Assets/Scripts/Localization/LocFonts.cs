using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
// L-02: китайский, японский и корейский берут глифы из общих запасных шрифтов TMP (TMP Settings → Fallback Font Assets).
// Иероглифы в китайском и японском начертании различаются, поэтому шрифт текущего языка ставится в списке первым.
// Сами шрифты и списки запасных настраивает Tools/Localization/Setup Fonts (LocalizationFontSetup).
public static class LocFonts
{
    public const string FontFolder = "Localization/Fonts/";
    public static readonly string[] CjkFontNames = { "NotoSansSC SDF", "NotoSansJP SDF", "NotoSansKR SDF" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        Loc.Changed += Apply;
        Apply();
    }

    public static string PreferredCjkFont(string language)
    {
        switch (language)
        {
            case "ja": return "NotoSansJP SDF";
            case "ko": return "NotoSansKR SDF";
            default: return "NotoSansSC SDF";
        }
    }

    static void Apply()
    {
        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
        string preferred = PreferredCjkFont(Loc.Current);
        int index = fallbacks.FindIndex(font => font != null && font.name == preferred);
        if (index < 0)
        {
            Debug.LogError($"[LocFonts] В TMP Settings нет запасного шрифта {preferred}: запустите Tools/Localization/Setup Fonts.");
            return;
        }
        if (index == 0) return;
        TMP_FontAsset font = fallbacks[index];
        fallbacks.RemoveAt(index);
        fallbacks.Insert(0, font);
    }
}
}
