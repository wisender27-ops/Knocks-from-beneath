using System;
using UnityEngine;

namespace KnocksFromBeneath
{
public static class SettingsStore
{
    public const string Key_MasterVolume = "settings_masterVolume";
    public const string Key_MouseSensitivity = "settings_mouseSensitivity";
    const string InvertKey = "settings_invertY", FovKey = "settings_fov";
    const string WidthKey = "settings_width", HeightKey = "settings_height";
    const string FullscreenKey = "settings_fullscreen", VsyncKey = "settings_vsync", QualityKey = "settings_quality";
    public static event Action Changed;
    public static int Revision { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Changed = null; Revision = 0; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        ApplyMasterVolume();
        bool vsync = GetVSync();
        if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(GetQuality(), true);
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        if (PlayerPrefs.HasKey(WidthKey) || PlayerPrefs.HasKey(FullscreenKey)) ApplyDisplay();
    }

    public static float GetMasterVolume(float defaultValue = 1f) => Mathf.Clamp01(PlayerPrefs.GetFloat(Key_MasterVolume, defaultValue));
    public static void SetMasterVolume(float value) { PlayerPrefs.SetFloat(Key_MasterVolume, Mathf.Clamp01(value)); ApplyMasterVolume(); Save(); }
    public static void ApplyMasterVolume() => AudioListener.volume = GetMasterVolume();
    public static float GetMouseSensitivity(float defaultValue = 2f) => Mathf.Clamp(PlayerPrefs.GetFloat(Key_MouseSensitivity, defaultValue), .1f, 10f);
    public static void SetMouseSensitivity(float value) { PlayerPrefs.SetFloat(Key_MouseSensitivity, Mathf.Clamp(value, .1f, 10f)); Save(); NotifyChanged(); }
    public static bool GetInvertY() => PlayerPrefs.GetInt(InvertKey, 0) != 0;
    public static void SetInvertY(bool value) { PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); Save(); NotifyChanged(); }
    public static float GetFieldOfView(float defaultValue = 60f) => Mathf.Clamp(PlayerPrefs.GetFloat(FovKey, defaultValue), 50f, 100f);
    public static void SetFieldOfView(float value) { PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, 50f, 100f)); Save(); NotifyChanged(); }
    public static bool GetFullscreen() => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
    public static bool GetVSync() => PlayerPrefs.GetInt(VsyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
    public static int GetQuality() => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
    public static int GetResolutionWidth() => PlayerPrefs.GetInt(WidthKey, Screen.width);
    public static int GetResolutionHeight() => PlayerPrefs.GetInt(HeightKey, Screen.height);
    public static void SetFullscreen(bool value) { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); ApplyDisplay(); Save(); }
    public static void SetVSync(bool value) { PlayerPrefs.SetInt(VsyncKey, value ? 1 : 0); QualitySettings.vSyncCount = value ? 1 : 0; Save(); }
    public static void SetQuality(int value)
    {
        value = Mathf.Clamp(value, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        bool vsync = GetVSync();
        PlayerPrefs.SetInt(QualityKey, value);
        QualitySettings.SetQualityLevel(value, true);
        QualitySettings.vSyncCount = vsync ? 1 : 0;
        Save();
    }
    public static void SetResolution(int width, int height)
    {
        PlayerPrefs.SetInt(WidthKey, Mathf.Max(640, width));
        PlayerPrefs.SetInt(HeightKey, Mathf.Max(480, height));
        ApplyDisplay(); Save();
    }
    static void ApplyDisplay() => Screen.SetResolution(GetResolutionWidth(), GetResolutionHeight(), GetFullscreen() ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    static void Save() { PlayerPrefs.Save(); }
    static void NotifyChanged() { Revision++; Changed?.Invoke(); }
}
}
