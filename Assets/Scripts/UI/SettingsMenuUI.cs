using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TextMeshProUGUI masterVolumeValueText;

    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TextMeshProUGUI mouseSensitivityValueText;

    void Start()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = SettingsStore.GetMasterVolume();
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            RefreshMasterVolumeText(masterVolumeSlider.value);
        }

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = 0.1f;
            mouseSensitivitySlider.maxValue = 10f;
            mouseSensitivitySlider.value = SettingsStore.GetMouseSensitivity();
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            RefreshMouseSensitivityText(mouseSensitivitySlider.value);
        }
    }

    void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (mouseSensitivitySlider != null)
            mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
    }

    void OnMasterVolumeChanged(float value)
    {
        SettingsStore.SetMasterVolume(value);
        RefreshMasterVolumeText(value);
    }

    void OnMouseSensitivityChanged(float value)
    {
        SettingsStore.SetMouseSensitivity(value);
        RefreshMouseSensitivityText(value);
    }

    void RefreshMasterVolumeText(float value)
    {
        if (masterVolumeValueText != null)
            masterVolumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    void RefreshMouseSensitivityText(float value)
    {
        if (mouseSensitivityValueText != null)
            mouseSensitivityValueText.text = value.ToString("0.0");
    }
}

