using UnityEngine;
using System.Collections;

public class SmartLamp : MonoBehaviour
{
    public Light lightSource;
    public AudioSource audioSource;
    public AudioClip flickerClip;
    public string lampID;

    private float _defaultIntensity;
    private Coroutine _currentFlicker;

    void Start()
    {
        if (lightSource == null) lightSource = GetComponent<Light>();
        if (lightSource == null)
            return;

        _defaultIntensity = lightSource.intensity;
        if (LightingManager.Instance != null)
            LightingManager.Instance.RegisterLamp(this);
    }

    public void StartSimpleFlicker(float duration, float interval)
    {
        if (lightSource == null) return;
        if (_currentFlicker != null) StopCoroutine(_currentFlicker);
        _currentFlicker = StartCoroutine(FlickerRoutine(Mathf.Max(0f, duration), Mathf.Max(0.01f, interval)));
    }

    private IEnumerator FlickerRoutine(float duration, float interval)
    {
        if (lightSource == null) yield break;

        float elapsed = 0;
        if (audioSource && flickerClip) { audioSource.clip = flickerClip; audioSource.Play(); }

        while (elapsed < duration)
        {
            // Просто инвертируем состояние: если горела — гасим, если нет — включаем
            lightSource.intensity = (lightSource.intensity > 0) ? 0 : _defaultIntensity;

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        lightSource.intensity = _defaultIntensity;
        if (audioSource) audioSource.Stop();
    }
}