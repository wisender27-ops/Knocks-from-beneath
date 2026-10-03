using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public class RandomKnock : MonoBehaviour
{
    [Header("Компоненты")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] knockClips; // Массив звуков для разнообразия

    [Header("Настройки времени")]
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 5f;

    private void OnEnable()
    {
        if (_silencedPermanently) return;
        if (_knockRoutine != null) return;
        _knockRoutine = StartCoroutine(KnockRoutine());
    }

    private Coroutine _knockRoutine;
    private float _pausedUntil;
    private bool _silencedPermanently;

    public void SilencePermanently()
    {
        _silencedPermanently = true;
        if (_knockRoutine != null) StopCoroutine(_knockRoutine);
        _knockRoutine = null;
        if (audioSource != null) audioSource.Stop();
    }
    public int PlayedKnockCount { get; private set; }
    public float LastKnockTime { get; private set; } = float.NegativeInfinity;

    public void PauseFor(float seconds)
    {
        _pausedUntil = Mathf.Max(_pausedUntil, Time.time + Mathf.Max(0f, seconds));
        if (audioSource != null) audioSource.Stop();
    }

    private void OnDisable()
    {
        if (_knockRoutine != null) StopCoroutine(_knockRoutine);
        _knockRoutine = null;
        if (audioSource != null) audioSource.Stop();
    }

    private IEnumerator KnockRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(Mathf.Max(0f, minDelay), Mathf.Max(minDelay, maxDelay));
            yield return new WaitForSeconds(waitTime);
            while (Time.time < _pausedUntil) yield return null;

            if (audioSource == null || knockClips == null || knockClips.Length == 0) continue;

            int randomIndex = Random.Range(0, knockClips.Length);
            AudioClip clip = knockClips[randomIndex];
            if (clip == null) continue;

            audioSource.PlayOneShot(clip);
            LastKnockTime = Time.time;
            PlayedKnockCount++;
            yield return new WaitForSeconds(clip.length);
        }
    }
}
}
