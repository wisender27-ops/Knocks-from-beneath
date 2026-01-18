using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    [Header("Настройки света")]
    public Light[] lightsToControl;   // Источники света (Light)
    public Renderer[] lampRenderers;  // Объекты ламп (на которых висит материал)
    public bool isOn = true;          

    [Header("Звуки")]
    public AudioSource switchSound; 

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        ApplyLightState();
    }

    public void ToggleLight()
    {
        isOn = !isOn; 
        ApplyLightState();

        if (switchSound != null)
        {
            switchSound.Play();
        }
    }

    void ApplyLightState()
    {
        // 1. Управляем источниками света
        foreach (Light l in lightsToControl)
        {
            if (l != null) l.enabled = isOn;
        }

        // 2. Управляем визуальным свечением материала
        foreach (Renderer rend in lampRenderers)
        {
            if (rend != null)
            {
                Material mat = rend.material; // Получаем экземпляр материала

                if (isOn)
                {
                    mat.EnableKeyword("_EMISSION");
                    // Если лампа тусклая, можно раскомментировать строку ниже, чтобы задать яркость принудительно:
                    // mat.SetColor(EmissionColor, Color.white * 2f); 
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}