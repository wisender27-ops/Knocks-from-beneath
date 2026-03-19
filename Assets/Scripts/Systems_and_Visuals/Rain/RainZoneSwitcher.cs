using UnityEngine;
using UnityEngine.Audio;
using System.Collections; // Нужно для работы корутин

public class RainZoneSwitcher : MonoBehaviour
{
    public AudioMixerSnapshot targetSnapshot;
    public float transitionTime = 3.0f; // Теперь стоит 3 секунды

    [Header("Настройки тумана")]
    public float targetFogDensity; // Плотность, к которой стремимся

    // Статическая переменная, чтобы все триггеры знали о запущенном переходе
    private static Coroutine fogCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 1. Плавный переход звука (уже встроен в Unity)
            targetSnapshot.TransitionTo(transitionTime);

            // 2. Плавный переход тумана
            if (fogCoroutine != null)
            {
                StopCoroutine(fogCoroutine); // Останавливаем старый переход, если он шел
            }
            fogCoroutine = StartCoroutine(LerpFog(targetFogDensity, transitionTime));

            Debug.Log("Переход в зону: " + targetSnapshot.name);
        }
    }

    // Логика плавного изменения плотности
    IEnumerator LerpFog(float endValue, float duration)
    {
        float startValue = RenderSettings.fogDensity;
        float time = 0;

        while (time < duration)
        {
            // Плавно вычисляем значение между стартом и концом
            RenderSettings.fogDensity = Mathf.Lerp(startValue, endValue, time / duration);
            time += Time.deltaTime;
            yield return null; // Ждем следующего кадра
        }

        RenderSettings.fogDensity = endValue; // Финально ставим точное значение
    }
}