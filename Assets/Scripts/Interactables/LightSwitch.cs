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
    private static readonly System.Collections.Generic.List<Light> _trippedLights = new System.Collections.Generic.List<Light>();
    private bool _lineDead; // M-04: после щитка линия этого выключателя осталась без тока

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _nightTwoPowerTripEnabled = false;
        _powerOutage = false;
        _trippedLights.Clear();
    }


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
        _lineDead = false;
        _trippedLights.Clear();
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

    private static void TripHousePower() => CutHousePower(true);

    // B-07: «Стакан воды» идёт до аварии — пока сцена не кончилась, выключатели дом не выбивают.
    public static void SetNightTwoPowerTrip(bool enabled) => _nightTwoPowerTripEnabled = enabled;

    // notify=false — дебаг-прыжок к щитку: свет погашен, а реакция героя на выключатель не нужна.
    public static void CutHousePower(bool notify)
    {
        _powerOutage = true;
        _trippedLights.Clear();

        Transform houseRoot = GameObject.Find("Разный мусор")?.transform;
        if (houseRoot != null)
        {
            Transform outdoorLights = houseRoot.Find("Lights outside");
            foreach (Light lightSource in houseRoot.GetComponentsInChildren<Light>(true))
            {
                if (lightSource == null || (outdoorLights != null && lightSource.transform.IsChildOf(outdoorLights))) continue;
                if (lightSource.enabled) _trippedLights.Add(lightSource);
                lightSource.enabled = false;
            }
        }

        LightSwitch[] switches = FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
        foreach (LightSwitch lightSwitch in switches)
            if (lightSwitch != null) lightSwitch.ApplyLightState();

        if (notify) GameEvents.OnPowerOutageSwitchAttempt?.Invoke();
    }

    // M-04 «Щиток»: ток вернулся, кроме линий, для которых dead == true. Выключатели снова работают
    // (и больше не выбивают дом); свет без выключателя, погашенный аварией, загорается, если deadLight не против.
    public static void RestoreHousePower(System.Predicate<LightSwitch> dead, System.Predicate<Light> deadLight)
    {
        _powerOutage = false;
        _nightTwoPowerTripEnabled = false;
        LightSwitch[] switches = FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
        var deadLights = new System.Collections.Generic.HashSet<Light>();
        var switched = new System.Collections.Generic.HashSet<Light>();
        foreach (LightSwitch lightSwitch in switches)
        {
            if (lightSwitch == null) continue;
            lightSwitch._lineDead = dead != null && dead(lightSwitch);
            if (lightSwitch.lightsToControl == null) continue;
            foreach (Light l in lightSwitch.lightsToControl)
                if (l != null) (lightSwitch._lineDead ? deadLights : switched).Add(l);
        }
        foreach (Light l in _trippedLights)
            if (l != null && !switched.Contains(l) && !deadLights.Contains(l) && (deadLight == null || !deadLight(l))) l.enabled = true;
        _trippedLights.Clear();
        foreach (LightSwitch lightSwitch in switches)
            if (lightSwitch != null) lightSwitch.ApplyLightState();
    }

    void ApplyLightState()
    {
        bool effectiveIsOn = isOn && !_powerOutage && !_lineDead;

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
