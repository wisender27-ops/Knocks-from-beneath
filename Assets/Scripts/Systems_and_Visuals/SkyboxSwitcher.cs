using UnityEngine;

public class SkyboxSwitcher : MonoBehaviour
{
    [Header("Настройки материалов")]
    public Material skyboxDay;
    public Material skyboxNight;

    [Header("Источники света (Directional Lights)")]
    public GameObject lightDay;   // Ваш объект A
    public GameObject lightNight; // Ваш объект B

    [Header("Состояние")]
    [Tooltip("Галочка включена — день, выключена — ночь")]
    public bool isDayTime = true;

    private bool lastState;

    void Start()
    {
        lastState = isDayTime;
        UpdateEnvironment();
    }

    void Update()
    {
        if (isDayTime != lastState)
        {
            UpdateEnvironment();
            lastState = isDayTime;
        }
    }

    void UpdateEnvironment()
    {
        // 1. Меняем материал неба
        RenderSettings.skybox = isDayTime ? skyboxDay : skyboxNight;

        // 2. Переключаем объекты (свет)
        if (lightDay != null) lightDay.SetActive(isDayTime);
        if (lightNight != null) lightNight.SetActive(!isDayTime);

        RenderSettings.reflectionIntensity = isDayTime ? 0.5f : 0.2f;

        // 3. Обновляем освещение сцены
        DynamicGI.UpdateEnvironment();
    }
}