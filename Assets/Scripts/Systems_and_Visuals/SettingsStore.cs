using UnityEngine;

public static class SettingsStore
{
    public const string Key_MasterVolume = "settings_masterVolume";
    public const string Key_MouseSensitivity = "settings_mouseSensitivity";

    public static float GetMasterVolume(float defaultValue = 1f)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(Key_MasterVolume, defaultValue));
    }

    public static void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(Key_MasterVolume, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        ApplyMasterVolume();
    }

    public static void ApplyMasterVolume()
    {
        AudioListener.volume = GetMasterVolume();
    }

    public static float GetMouseSensitivity(float defaultValue = 2f)
    {
        return Mathf.Clamp(PlayerPrefs.GetFloat(Key_MouseSensitivity, defaultValue), 0.1f, 10f);
    }

    public static void SetMouseSensitivity(float value)
    {
        PlayerPrefs.SetFloat(Key_MouseSensitivity, Mathf.Clamp(value, 0.1f, 10f));
        PlayerPrefs.Save();
    }
}

