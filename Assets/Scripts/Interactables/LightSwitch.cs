using UnityEngine;

namespace KnocksFromBeneath
{

public class LightSwitch : MonoBehaviour
{
    [Header("Настройки света")]
    public Light[] lightsToControl;
    public Renderer[] lampRenderers;
    public bool isOn = true;
    public bool turnOnAfterNightTwoWake;

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
        GameEvents.OnNightStarted += ResetPowerGrid;
        GameEvents.OnDayStarted += ForceTurnOn;
        GameEvents.OnNightTwoStarted += EnableNightTwoPowerTrip;
        GameEvents.OnNightTwoAwake += TurnOnAfterNightTwoWake;
        GameEvents.OnDayStarted += ResetPowerGrid;
    }

    private void OnDisable()
    {
        GameEvents.OnNightStarted -= ForceTurnOff;
        GameEvents.OnNightStarted -= ResetPowerGrid;
        GameEvents.OnDayStarted -= ForceTurnOn;
        GameEvents.OnNightTwoStarted -= EnableNightTwoPowerTrip;
        GameEvents.OnNightTwoAwake -= TurnOnAfterNightTwoWake;
        GameEvents.OnDayStarted -= ResetPowerGrid;
    }

    private static bool _nightTwoPowerTripEnabled;
    private static bool _powerOutage;

    private void EnableNightTwoPowerTrip()
    {
        _nightTwoPowerTripEnabled = true;
    }

    private void TurnOnAfterNightTwoWake()
    {
        if (!turnOnAfterNightTwoWake) return;
        isOn = true;
        ApplyLightState();
    }

    private void ResetPowerGrid()
    {
        _nightTwoPowerTripEnabled = false;
        _powerOutage = false;
        ApplyLightState();
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
        if (turnOnAfterNightTwoWake) return;
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

        Debug.Log($"[LightSwitch] '{name}' toggled -> {(isOn ? "ON" : "OFF")} fanLinked={(fanRotator != null)}");

        if (switchSound != null)
        {
            // A-01: свои щелчки на включение и выключение; без библиотеки — прежний клип.
            AudioClip click = isOn ? GameSounds.Library?.switchOn : GameSounds.Library?.switchOff;
            if (click != null) { switchSound.pitch = Random.Range(.97f, 1.03f); switchSound.PlayOneShot(click, .9f); }
            else switchSound.Play();
        }

        if (_nightTwoPowerTripEnabled && !_powerOutage)
            TripHousePower();

        ApplyLightState();
    }

    private static void TripHousePower()
    {
        _powerOutage = true;

        Transform houseRoot = GameObject.Find("Разный мусор")?.transform;
        if (houseRoot != null)
        {
            Transform outdoorLights = houseRoot.Find("Lights outside");
            foreach (Light lightSource in houseRoot.GetComponentsInChildren<Light>(true))
            {
                if (lightSource == null || (outdoorLights != null && lightSource.transform.IsChildOf(outdoorLights))) continue;
                lightSource.enabled = false;
            }
        }

        LightSwitch[] switches = FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
        foreach (LightSwitch lightSwitch in switches)
            if (lightSwitch != null) lightSwitch.ApplyLightState();

        GameEvents.OnPowerOutageSwitchAttempt?.Invoke();
    }

    void ApplyLightState()
    {
        bool effectiveIsOn = isOn && !_powerOutage;

        // 1. Управляем источниками света
        if (lightsToControl != null)
        foreach (Light l in lightsToControl)
        {
            if (l != null) l.enabled = effectiveIsOn;
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
                _propBlock.SetColor(EmissionColor, effectiveIsOn ? original : Color.black);
                rend.SetPropertyBlock(_propBlock);
            }
        }

        // 3. Управляем вентилятором, если он привязан
        if (fanRotator != null)
        {
            fanRotator.ToggleRotation(effectiveIsOn);
        }
    }
}
}
