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

    void Start()
    {
        dayAmbientMode = RenderSettings.ambientMode;
        dayAmbientLight = RenderSettings.ambientLight;
        dayAmbientIntensity = RenderSettings.ambientIntensity;
        dayFogColor = RenderSettings.fogColor;
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

        // ВНИМАНИЕ: раньше тут ночью подменяли LightmapSettings.lightmaps на пустой массив
        // и зануляли lightProbes.bakedProbes, чтобы дневная запечённая подсветка не "просвечивала"
        // ночью. Но у статичных объектов (Contribute GI, например газон grass_soil_*) остаётся
        // ссылка на m_LightmapIndex в уже несуществующий (пустой) массив лайтмап — рендерер
        // не получает освещения и объект визуально пропадает, пока не наступит день. Дневная
        // запечённая карта света физически не меняется от переключения времени суток само по
        // себе — реальную смену настроения ночью и так обеспечивают ambientLight/ambientIntensity/
        // fogColor/reflectionIntensity и сама смена directional light (lightDay/lightNight) ниже,
        // поэтому обнулять лайтмапы и пробы не нужно.

        // 3. Обновляем освещение сцены
        DynamicGI.UpdateEnvironment();
    }
}
}
