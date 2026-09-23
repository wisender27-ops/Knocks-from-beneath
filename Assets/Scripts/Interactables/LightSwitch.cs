using UnityEngine;

namespace KnocksFromBeneath
{

public class LightSwitch : MonoBehaviour
{
    [Header("Настройки света")]
    public Light[] lightsToControl;
    public Renderer[] lampRenderers;
    public bool isOn = true;

    [Header("Настройки вентилятора (необязательно)")]
    public Rotator fanRotator;

    [Header("Звуки")]
    public AudioSource switchSound;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _propBlock;
    private Color[] _originalEmissionColors;

    // --- ПОДПИСКА НА СОБЫТИЯ ---
    private void OnEnable()
    {
        GameEvents.OnNightStarted += ForceTurnOff;
        GameEvents.OnDayStarted += ForceTurnOn;
    }

    private void OnDisable()
    {
        GameEvents.OnNightStarted -= ForceTurnOff;
        GameEvents.OnDayStarted -= ForceTurnOn;
    }

    // Метод, который сработает при наступлении темноты. Форсит ВСЕ выключатели сразу
    // (сюжетное затемнение дома на ночь, не имитация "игрок сам выключил свет") —
    // поэтому у него есть парный ForceTurnOn на OnDayStarted, который возвращает свет
    // утром, иначе весь день 2 дом стоит тёмным, даже если игрок ничего не трогал.
    public void ForceTurnOff()
    {
        if (!isOn) return;
        isOn = false;
        ApplyLightState();
    }

    public void ForceTurnOn()
    {
        if (isOn) return;
        isOn = true;
        ApplyLightState();
    }

    void Start()
    {
        CaptureOriginalEmissionColors();
        ApplyLightState();
    }

    void CaptureOriginalEmissionColors()
    {
        if (lampRenderers == null) return;
        _originalEmissionColors = new Color[lampRenderers.Length];
        for (int i = 0; i < lampRenderers.Length; i++)
        {
            if (lampRenderers[i] != null && lampRenderers[i].sharedMaterial != null && lampRenderers[i].sharedMaterial.HasProperty(EmissionColor))
                _originalEmissionColors[i] = lampRenderers[i].sharedMaterial.GetColor(EmissionColor);
        }
    }

    public void ToggleLight()
    {
        isOn = !isOn;
        ApplyLightState();
        Debug.Log($"[LightSwitch] '{name}' toggled -> {(isOn ? "ON" : "OFF")} fanLinked={(fanRotator != null)}");

        if (switchSound != null)
        {
            switchSound.Play();
        }
    }

    void ApplyLightState()
    {
        // 1. Управляем источниками света
        if (lightsToControl != null)
        foreach (Light l in lightsToControl)
        {
            if (l != null) l.enabled = isOn;
        }

        // 2. Управляем визуальным свечением материала через MaterialPropertyBlock —
        // rend.material создавал персональную копию материала на каждый рендерер
        // (утечка, ломает батчинг). Ключевое слово _EMISSION остаётся включённым на самом
        // ассете материала, переключаем только цвет эмиссии между исходным и чёрным.
        if (lampRenderers != null)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < lampRenderers.Length; i++)
            {
                Renderer rend = lampRenderers[i];
                if (rend == null) continue;

                Color original = (_originalEmissionColors != null && i < _originalEmissionColors.Length)
                    ? _originalEmissionColors[i] : Color.white;

                rend.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColor, isOn ? original : Color.black);
                rend.SetPropertyBlock(_propBlock);
            }
        }

        // 3. Управляем вентилятором, если он привязан
        if (fanRotator != null)
        {
            fanRotator.ToggleRotation(isOn);
        }
    }
}
}
