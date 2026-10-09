using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] Slider masterVolumeSlider;
    [SerializeField] TextMeshProUGUI masterVolumeValueText;
    [SerializeField] Slider mouseSensitivitySlider;
    [SerializeField] TextMeshProUGUI mouseSensitivityValueText;
    Slider fovSlider;
    TextMeshProUGUI fovText;
    Toggle invertToggle, fullscreenToggle, vsyncToggle;
    TextMeshProUGUI qualityValue, resolutionValue;
    readonly List<Vector2Int> resolutions = new List<Vector2Int>();

    void OnEnable()
    {
        if (masterVolumeSlider) masterVolumeSlider.SetValueWithoutNotify(SettingsStore.GetMasterVolume());
        if (mouseSensitivitySlider) mouseSensitivitySlider.SetValueWithoutNotify(SettingsStore.GetMouseSensitivity());
        RefreshMasterVolumeText(SettingsStore.GetMasterVolume());
        RefreshMouseSensitivityText(SettingsStore.GetMouseSensitivity());
        if (fovSlider) { fovSlider.SetValueWithoutNotify(SettingsStore.GetFieldOfView()); fovText.text = Mathf.RoundToInt(fovSlider.value) + "°"; }
        if (invertToggle) invertToggle.SetIsOnWithoutNotify(SettingsStore.GetInvertY());
        if (fullscreenToggle) fullscreenToggle.SetIsOnWithoutNotify(SettingsStore.GetFullscreen());
        if (vsyncToggle) vsyncToggle.SetIsOnWithoutNotify(SettingsStore.GetVSync());
    }

    void Start()
    {
        SetupSlider(masterVolumeSlider, 0, 1, SettingsStore.GetMasterVolume(), OnMasterVolumeChanged);
        SetupSlider(mouseSensitivitySlider, .1f, 10, SettingsStore.GetMouseSensitivity(), OnMouseSensitivityChanged);
        RefreshMasterVolumeText(SettingsStore.GetMasterVolume());
        RefreshMouseSensitivityText(SettingsStore.GetMouseSensitivity());
    }
    static void SetupSlider(Slider slider, float min, float max, float value, UnityEngine.Events.UnityAction<float> action)
    {
        if (!slider) return;
        slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(value); slider.onValueChanged.AddListener(action);
    }
    void OnDestroy()
    {
        if (masterVolumeSlider) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (mouseSensitivitySlider) mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
    }
    void OnMasterVolumeChanged(float value) { SettingsStore.SetMasterVolume(value); RefreshMasterVolumeText(value); }
    void OnMouseSensitivityChanged(float value) { SettingsStore.SetMouseSensitivity(value); RefreshMouseSensitivityText(value); }
    void RefreshMasterVolumeText(float value) { if (masterVolumeValueText) masterVolumeValueText.text = $"{Mathf.RoundToInt(value * 100)}%"; }
    void RefreshMouseSensitivityText(float value) { if (mouseSensitivityValueText) mouseSensitivityValueText.text = value.ToString("0.0"); }

    public static SettingsMenuUI BuildRuntime(Transform parent, TMP_FontAsset font, Action onBack)
    {
        RectTransform panel = Rect("Settings", parent, new Vector2(860, 640));
        panel.gameObject.AddComponent<Image>().color = new Color(.055f, .055f, .065f, .98f);
        SettingsMenuUI ui = panel.gameObject.AddComponent<SettingsMenuUI>();
        Label(panel, font, "НАСТРОЙКИ", new Vector2(0, 278), new Vector2(760, 55), 30);
        ui.masterVolumeSlider = SliderRow(panel, font, "Общая громкость", 210, out ui.masterVolumeValueText);
        ui.mouseSensitivitySlider = SliderRow(panel, font, "Чувствительность мыши", 150, out ui.mouseSensitivityValueText);
        ui.fovSlider = SliderRow(panel, font, "Поле зрения", 90, out ui.fovText);
        SetupSlider(ui.fovSlider, 50, 100, SettingsStore.GetFieldOfView(), value => { SettingsStore.SetFieldOfView(value); ui.fovText.text = Mathf.RoundToInt(value) + "°"; });
        ui.fovText.text = Mathf.RoundToInt(SettingsStore.GetFieldOfView()) + "°";
        ui.invertToggle = ToggleRow(panel, font, "Инверсия мыши по вертикали", 30, SettingsStore.GetInvertY(), SettingsStore.SetInvertY);
        ui.fullscreenToggle = ToggleRow(panel, font, "Полноэкранный режим", -30, SettingsStore.GetFullscreen(), SettingsStore.SetFullscreen);
        ui.vsyncToggle = ToggleRow(panel, font, "Вертикальная синхронизация", -90, SettingsStore.GetVSync(), SettingsStore.SetVSync);
        string[] qualityNames = QualitySettings.names;
        int quality = SettingsStore.GetQuality();
        ui.qualityValue = ChoiceRow(panel, font, "Качество графики", -150, qualityNames[quality], direction => {
            quality = (quality + direction + qualityNames.Length) % qualityNames.Length;
            SettingsStore.SetQuality(quality); ui.qualityValue.text = qualityNames[quality];
        });
        List<string> options = new List<string>();
        Vector2Int current = new Vector2Int(SettingsStore.GetResolutionWidth(), SettingsStore.GetResolutionHeight());
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (ui.resolutions.Contains(size)) continue;
            ui.resolutions.Add(size); options.Add(size.x + " × " + size.y);
        }
        if (!ui.resolutions.Contains(current)) { ui.resolutions.Add(current); options.Add(current.x + " × " + current.y); }
        int resolutionIndex = ui.resolutions.IndexOf(current);
        ui.resolutionValue = ChoiceRow(panel, font, "Разрешение", -210, options[resolutionIndex], direction => {
            resolutionIndex = (resolutionIndex + direction + options.Count) % options.Count;
            Vector2Int size = ui.resolutions[resolutionIndex];
            SettingsStore.SetResolution(size.x, size.y); ui.resolutionValue.text = options[resolutionIndex];
        });
        RectTransform back = Rect("Back", panel, new Vector2(220, 44), new Vector2(0, -275));
        back.gameObject.AddComponent<Image>().color = new Color(.2f, .2f, .23f);
        back.gameObject.AddComponent<Button>().onClick.AddListener(() => onBack?.Invoke());
        Label(back, font, "НАЗАД", Vector2.zero, new Vector2(220, 44), 22);
        return ui;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position = default)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size, float fontSize = 22)
    {
        TextMeshProUGUI label = Rect("Label", parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.color = new Color(.9f, .88f, .83f); label.raycastTarget = false; return label;
    }
    static Slider SliderRow(Transform parent, TMP_FontAsset font, string caption, float y, out TextMeshProUGUI valueText)
    {
        Label(parent, font, caption, new Vector2(-210, y), new Vector2(360, 44));
        RectTransform track = Rect(caption, parent, new Vector2(290, 24), new Vector2(155, y));
        track.gameObject.AddComponent<Image>().color = new Color(.22f, .22f, .24f);
        Slider slider = track.gameObject.AddComponent<Slider>();
        RectTransform handle = Rect("Handle", track, new Vector2(18, 30));
        Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.85f, .8f, .65f);
        slider.handleRect = handle; slider.targetGraphic = handleImage; slider.direction = Slider.Direction.LeftToRight;
        valueText = Label(parent, font, "", new Vector2(350, y), new Vector2(90, 44), 20); return slider;
    }
    static Toggle ToggleRow(Transform parent, TMP_FontAsset font, string caption, float y, bool value, UnityEngine.Events.UnityAction<bool> action)
    {
        Label(parent, font, caption, new Vector2(-160, y), new Vector2(460, 44));
        RectTransform rect = Rect(caption, parent, new Vector2(34, 34), new Vector2(155, y));
        Image background = rect.gameObject.AddComponent<Image>(); background.color = new Color(.25f, .25f, .27f);
        Toggle toggle = rect.gameObject.AddComponent<Toggle>();
        Image mark = Rect("Check", rect, new Vector2(22, 22)).gameObject.AddComponent<Image>(); mark.color = new Color(.85f, .8f, .65f);
        toggle.targetGraphic = background; toggle.graphic = mark; toggle.SetIsOnWithoutNotify(value); toggle.onValueChanged.AddListener(action); return toggle;
    }
    static TextMeshProUGUI ChoiceRow(Transform parent, TMP_FontAsset font, string caption, float y, string value, Action<int> changed)
    {
        Label(parent, font, caption, new Vector2(-210, y), new Vector2(360, 44));
        RectTransform rect = Rect(caption, parent, new Vector2(360, 44), new Vector2(180, y));
        rect.gameObject.AddComponent<Image>().color = new Color(.2f, .2f, .23f);
        var label = Label(rect, font, value, Vector2.zero, new Vector2(260, 44), 20);
        foreach (int direction in new[] { -1, 1 })
        {
            int step = direction;
            RectTransform arrow = Rect(step < 0 ? "Previous" : "Next", rect, new Vector2(46, 44), new Vector2(step * 156, 0));
            Image image = arrow.gameObject.AddComponent<Image>(); image.color = new Color(.3f, .3f, .33f);
            Button button = arrow.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => changed(step));
            Label(arrow, font, step < 0 ? "<" : ">", Vector2.zero, new Vector2(46, 44), 24);
        }
        return label;
    }
}
}
