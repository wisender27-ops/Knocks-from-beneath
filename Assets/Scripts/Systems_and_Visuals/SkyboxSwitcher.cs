using UnityEngine;
using UnityEngine.Rendering;

namespace KnocksFromBeneath
{

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
    private AmbientMode dayAmbientMode;
    private Color dayAmbientLight;
    private Color dayFogColor;
    private float dayAmbientIntensity;
    private LightmapData[] dayLightmaps;
    private LightProbes sceneLightProbes;
    private SphericalHarmonicsL2[] dayProbes;

    void Start()
    {
        dayAmbientMode = RenderSettings.ambientMode;
        dayAmbientLight = RenderSettings.ambientLight;
        dayAmbientIntensity = RenderSettings.ambientIntensity;
        dayFogColor = RenderSettings.fogColor;
        dayLightmaps = LightmapSettings.lightmaps;
        sceneLightProbes = LightmapSettings.lightProbes;
        if (sceneLightProbes != null)
            dayProbes = sceneLightProbes.bakedProbes;
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
        RenderSettings.ambientMode = isDayTime ? dayAmbientMode : AmbientMode.Flat;
        RenderSettings.ambientLight = isDayTime ? dayAmbientLight : new Color(0.008f, 0.01f, 0.018f);
        RenderSettings.ambientIntensity = isDayTime ? dayAmbientIntensity : 1f;
        RenderSettings.fogColor = isDayTime ? dayFogColor : new Color(0.008f, 0.01f, 0.018f);

        // Дневные карты света и проба иначе продолжают освещать сцену ночью.
        LightmapSettings.lightmaps = isDayTime ? dayLightmaps : System.Array.Empty<LightmapData>();
        if (sceneLightProbes != null && dayProbes != null)
            sceneLightProbes.bakedProbes = isDayTime
                ? dayProbes
                : new SphericalHarmonicsL2[dayProbes.Length];

        // 3. Обновляем освещение сцены
        DynamicGI.UpdateEnvironment();
    }
}
}
