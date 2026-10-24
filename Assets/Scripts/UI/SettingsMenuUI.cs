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

    static readonly Color PanelColor = new Color(0f, 0f, 0f, .88f);
    static readonly Color TextColor = UiStyle.Ivory;
    static readonly Color Accent = UiStyle.Amber;
    static readonly Color TrackColor = new Color(UiStyle.Ash.r, UiStyle.Ash.g, UiStyle.Ash.b, .3f);
    static readonly Color ControlColor = new Color(.1f, .1f, .1f, 1f);
    static readonly Color ControlHover = new Color(.2f, .19f, .17f, 1f);
    // Шкала текста меню: база 28, шаг x1.5 (28 / 42 / 64 / 95 / 142). Заголовок названия и пункты главного меню - на старших ступенях.
    const float TextBase = 28f, TextHeading = 42f;
    const float RowHeight = 64f, SliderWidth = 400f, ValueWidth = 100f, RowSpacing = 16f;
    const float ControlZoneWidth = SliderWidth + RowSpacing + ValueWidth;

    public static SettingsMenuUI BuildRuntime(Transform parent, TMP_FontAsset font, Action onBack)
    {
        RectTransform panel = Rect("Settings", parent, new Vector2(1160, 0));
        panel.gameObject.AddComponent<Image>().color = PanelColor;
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(64, 64, 48, 48); layout.spacing = 8; layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        SettingsMenuUI ui = panel.gameObject.AddComponent<SettingsMenuUI>();

        Label(panel, font, "► НАСТРОЙКИ", TextHeading, TextAnchor.MiddleLeft, 72).color = Accent;
        Image rule = Rect("Rule", panel, Vector2.zero).gameObject.AddComponent<Image>();
        rule.color = new Color(Accent.r, Accent.g, Accent.b, .6f); rule.raycastTarget = false;
        rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
        Spacer(panel, 14);

        ui.masterVolumeSlider = SliderRow(panel, font, "Общая громкость", out ui.masterVolumeValueText);
        ui.mouseSensitivitySlider = SliderRow(panel, font, "Чувствительность мыши", out ui.mouseSensitivityValueText);
        ui.fovSlider = SliderRow(panel, font, "Поле зрения", out ui.fovText);
        SetupSlider(ui.fovSlider, 50, 100, SettingsStore.GetFieldOfView(), value => { SettingsStore.SetFieldOfView(value); ui.fovText.text = Mathf.RoundToInt(value) + "°"; });
        ui.fovText.text = Mathf.RoundToInt(SettingsStore.GetFieldOfView()) + "°";
        ui.invertToggle = ToggleRow(panel, font, "Инверсия мыши по вертикали", SettingsStore.GetInvertY(), SettingsStore.SetInvertY);
        ui.fullscreenToggle = ToggleRow(panel, font, "Полноэкранный режим", SettingsStore.GetFullscreen(), SettingsStore.SetFullscreen);
        ui.vsyncToggle = ToggleRow(panel, font, "Вертикальная синхронизация", SettingsStore.GetVSync(), SettingsStore.SetVSync);
        string[] qualityNames = QualitySettings.names;
        int quality = SettingsStore.GetQuality();
        ui.qualityValue = ChoiceRow(panel, font, "Качество графики", qualityNames[quality], direction => {
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
        ui.resolutionValue = ChoiceRow(panel, font, "Разрешение", options[resolutionIndex], direction => {
            resolutionIndex = (resolutionIndex + direction + options.Count) % options.Count;
            Vector2Int size = ui.resolutions[resolutionIndex];
            SettingsStore.SetResolution(size.x, size.y); ui.resolutionValue.text = options[resolutionIndex];
        });

        Spacer(panel, 28);
        HorizontalLayoutGroup footer = HorizontalRow("Footer", panel, 64, TextAnchor.MiddleLeft);
        Button back = ButtonBox("Back", footer.transform, font, "НАЗАД", TextBase, 280, 64);
        back.onClick.AddListener(() => onBack?.Invoke());
        return ui;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position = default)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, 0); rect.offsetMax = new Vector2(-inset, 0);
    }
    static void Spacer(Transform parent, float height) =>
        Rect("Spacer", parent, Vector2.zero).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    static LayoutElement Size(GameObject target, float width, float height, float flexibleWidth = -1)
    {
        LayoutElement element = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
        if (width >= 0) element.preferredWidth = width;
        if (height >= 0) element.preferredHeight = height;
        if (flexibleWidth >= 0) element.flexibleWidth = flexibleWidth;
        return element;
    }
    static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string text, float fontSize, TextAnchor anchor, float height)
    {
        TextMeshProUGUI label = Rect("Label", parent, Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = fontSize; label.color = TextColor; label.raycastTarget = false;
        label.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignmentOptions.MidlineLeft
            : anchor == TextAnchor.MiddleRight ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Midline;
        Size(label.gameObject, -1, height);
        return label;
    }
    static HorizontalLayoutGroup HorizontalRow(string name, Transform parent, float height, TextAnchor alignment)
    {
        HorizontalLayoutGroup row = Rect(name, parent, Vector2.zero).gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = RowSpacing; row.childAlignment = alignment;
        row.childControlWidth = row.childControlHeight = true; row.childForceExpandWidth = row.childForceExpandHeight = false;
        Size(row.gameObject, -1, height);
        return row;
    }
    static HorizontalLayoutGroup CaptionedRow(Transform parent, TMP_FontAsset font, string caption)
    {
        HorizontalLayoutGroup row = HorizontalRow(caption, parent, RowHeight, TextAnchor.MiddleLeft);
        Size(Label(row.transform, font, caption, TextBase, TextAnchor.MiddleLeft, RowHeight).gameObject, -1, -1, 1);
        return row;
    }
    static void ApplyColors(Selectable selectable, Color normal, Color highlighted)
    {
        ColorBlock colors = selectable.colors;
        colors.normalColor = normal; colors.highlightedColor = highlighted; colors.selectedColor = highlighted;
        colors.pressedColor = Color.Lerp(highlighted, Accent, .5f); colors.disabledColor = normal * .5f;
        colors.colorMultiplier = 1f; colors.fadeDuration = .08f;
        selectable.colors = colors;
    }
    static Button ButtonBox(string name, Transform parent, TMP_FontAsset font, string text, float fontSize, float width, float height)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero);
        Image image = rect.gameObject.AddComponent<Image>(); image.color = Color.white;
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        ApplyColors(button, ControlColor, ControlHover);
        Size(rect.gameObject, width, height);
        Stretch(Label(rect, font, text, fontSize, TextAnchor.MiddleCenter, -1).rectTransform);
        return button;
    }
    static Slider SliderRow(Transform parent, TMP_FontAsset font, string caption, out TextMeshProUGUI valueText)
    {
        HorizontalLayoutGroup row = CaptionedRow(parent, font, caption);
        RectTransform root = Rect(caption, row.transform, Vector2.zero);
        Size(root.gameObject, SliderWidth, RowHeight);
        Image hit = root.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform track = Rect("Background", root, new Vector2(0, 6));
        track.anchorMin = new Vector2(0, .5f); track.anchorMax = new Vector2(1, .5f);
        track.gameObject.AddComponent<Image>().color = TrackColor;
        RectTransform fillArea = Rect("Fill Area", root, new Vector2(-16, 6));
        fillArea.anchorMin = new Vector2(0, .5f); fillArea.anchorMax = new Vector2(1, .5f);
        RectTransform fill = Rect("Fill", fillArea, Vector2.zero);
        fill.gameObject.AddComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, .75f);
        // Slider сам растягивает ручку по высоте области: высоту ручки задаёт область (32), а не sizeDelta.
        RectTransform slideArea = Rect("Handle Slide Area", root, new Vector2(-16, 32));
        slideArea.anchorMin = new Vector2(0, .5f); slideArea.anchorMax = new Vector2(1, .5f);
        RectTransform handle = Rect("Handle", slideArea, new Vector2(16, 0));
        Image handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = Color.white;

        slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage; slider.direction = Slider.Direction.LeftToRight;
        ApplyColors(slider, TextColor, Accent);
        valueText = Label(row.transform, font, "", TextBase, TextAnchor.MiddleRight, RowHeight);
        Size(valueText.gameObject, ValueWidth, -1);
        return slider;
    }
    static Toggle ToggleRow(Transform parent, TMP_FontAsset font, string caption, bool value, UnityEngine.Events.UnityAction<bool> action)
    {
        HorizontalLayoutGroup row = CaptionedRow(parent, font, caption);
        RectTransform zone = Rect("Zone", row.transform, Vector2.zero);
        Size(zone.gameObject, ControlZoneWidth, RowHeight);
        RectTransform box = Rect(caption, zone, new Vector2(40, 40));
        box.anchorMin = box.anchorMax = new Vector2(0, .5f); box.pivot = new Vector2(0, .5f); box.anchoredPosition = Vector2.zero;
        Image background = box.gameObject.AddComponent<Image>(); background.color = Color.white;
        Toggle toggle = box.gameObject.AddComponent<Toggle>();
        RectTransform markRect = Rect("Check", box, Vector2.zero); Stretch(markRect); markRect.offsetMin = new Vector2(8, 8); markRect.offsetMax = new Vector2(-8, -8);
        Image mark = markRect.gameObject.AddComponent<Image>(); mark.color = Accent; mark.raycastTarget = false;
        toggle.targetGraphic = background; toggle.graphic = mark;
        ApplyColors(toggle, ControlColor, ControlHover);
        toggle.SetIsOnWithoutNotify(value); toggle.onValueChanged.AddListener(action); return toggle;
    }
    static TextMeshProUGUI ChoiceRow(Transform parent, TMP_FontAsset font, string caption, string value, Action<int> changed)
    {
        HorizontalLayoutGroup row = CaptionedRow(parent, font, caption);
        HorizontalLayoutGroup choice = HorizontalRow("Choice", row.transform, 56, TextAnchor.MiddleCenter);
        Size(choice.gameObject, ControlZoneWidth, 56, 0);
        choice.spacing = 0; choice.gameObject.AddComponent<Image>().color = new Color(.06f, .06f, .06f);
        TextMeshProUGUI label = Label(choice.transform, font, value, TextBase, TextAnchor.MiddleCenter, 56);
        Size(label.gameObject, -1, -1, 1);
        foreach (int direction in new[] { -1, 1 })
        {
            int step = direction;
            Button arrow = ButtonBox(step < 0 ? "Previous" : "Next", choice.transform, font, step < 0 ? "<" : ">", 32, 64, 56);
            arrow.onClick.AddListener(() => changed(step));
            if (step < 0) arrow.transform.SetSiblingIndex(0);
        }
        return label;
    }
}
}
