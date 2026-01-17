using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    [Header("Настройки света")]
    public Light[] lightsToControl; // Список лампочек
    public bool isOn = true;        // Твоя галочка в инспекторе

    [Header("Звуки")]
    public AudioSource switchSound; 

    // Этот метод срабатывает один раз при старте игры
    void Start()
    {
        // Принудительно устанавливаем состояние ламп в соответствии с галочкой isOn
        ApplyLightState();
    }

    public void ToggleLight()
    {
        isOn = !isOn; // Меняем состояние
        ApplyLightState();

        if (switchSound != null)
        {
            switchSound.Play();
        }
    }

    // Вынесли логику включения/выключения в отдельный метод, чтобы не дублировать код
    void ApplyLightState()
    {
        foreach (Light l in lightsToControl)
        {
            if (l != null)
            {
                l.enabled = isOn;
            }
        }
    }
}