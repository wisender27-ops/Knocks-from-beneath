using UnityEngine;

public class SmartLamp : MonoBehaviour
{
    public Light lightSource;
    public AudioSource audioSource; // Колонка на лампе
    public AudioClip flickerClip;   // Звук треска

    private float _defaultIntensity;
    private bool _isOverridden = false; // Блокировка для сюжетных моментов

    public string lampID; // Например, "Kitchen_Main" или "Hallway_1"

    void Start()
    {
        if (lightSource == null) lightSource = GetComponent<Light>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        _defaultIntensity = lightSource.intensity;
        LightingManager.Instance.RegisterLamp(this);
    }

    // Метод для случайного мерцания
    public void RandomFlicker(float duration, float minInt)
    {
        if (_isOverridden) return;
        StartCoroutine(FlickerRoutine(duration, minInt));
    }

    // Метод для контроллера (выключить/включить по сюжету)
    public void SetOverride(bool state, float targetIntensity = 0)
    {
        _isOverridden = state;
        lightSource.intensity = state ? targetIntensity : _defaultIntensity;

        // Если лампу выключили навсегда (Override), звук тоже гасим
        if (state && audioSource != null) audioSource.Stop();
    }

    private System.Collections.IEnumerator FlickerRoutine(float duration, float minInt)
    {
        float elapsed = 0;

        // Включаем звук треска, если он есть
        if (audioSource != null && flickerClip != null)
        {
            audioSource.clip = flickerClip;
            audioSource.loop = true;
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.Play();
        }

        while (elapsed < duration)
        {
            float rand = Random.Range(minInt, _defaultIntensity);
            lightSource.intensity = rand;

            // Синхронизируем громкость звука с яркостью (необязательно, но круто)
            if (audioSource != null)
                audioSource.volume = (1f - (rand / _defaultIntensity)) * 0.5f;

            elapsed += Time.deltaTime;
            yield return null;
        }

        lightSource.intensity = _defaultIntensity;
        if (audioSource != null) audioSource.Stop();
    }
}