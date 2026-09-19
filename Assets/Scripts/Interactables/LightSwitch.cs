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

    // --- ПОДПИСКА НА СОБЫТИЯ ---
    private void OnEnable()
    {
        GameEvents.OnNightStarted += ForceTurnOff;
    }

    private void OnDisable()
    {
        GameEvents.OnNightStarted -= ForceTurnOff;
    }

    // Метод, который сработает при наступлении темноты
    public void ForceTurnOff()
    {
        if (!isOn) return;
        isOn = false;
        ApplyLightState();
    }

    void Start()
    {
        ApplyLightState();
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

        // 2. Управляем визуальным свечением материала
        if (lampRenderers != null)
        foreach (Renderer rend in lampRenderers)
        {
            if (rend != null)
            {
                Material mat = rend.material;

                if (isOn)
                {
                    mat.EnableKeyword("_EMISSION");
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }
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
