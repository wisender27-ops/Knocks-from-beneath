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
        _defaultIntensity = lightSource.intensity;
        LightingManager.Instance.RegisterLamp(this);
    }

    public void StartSimpleFlicker(float duration, float interval)
    {
        if (_currentFlicker != null) StopCoroutine(_currentFlicker);
        _currentFlicker = StartCoroutine(FlickerRoutine(duration, interval));
    }

    private IEnumerator FlickerRoutine(float duration, float interval)
    {
        float elapsed = 0;
        if (audioSource && flickerClip) { audioSource.clip = flickerClip; audioSource.Play(); }

        while (elapsed < duration)
        {
            // ѕросто инвертируем состо€ние: если горела Ч гасим, если нет Ч включаем
            lightSource.intensity = (lightSource.intensity > 0) ? 0 : _defaultIntensity;

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        lightSource.intensity = _defaultIntensity;
        if (audioSource) audioSource.Stop();
    }
}