using UnityEngine;

public class ImpactSounds : MonoBehaviour
{
    public AudioSource impactSource;
    public AudioClip[] clips;

    [Header("Настройки физики")]
    public float minVelocity = 1.5f;
    [SerializeField] private float volumeMultiplier = 0.1f; // speed / 10

    [Header("Тайминги")]
    [SerializeField] private float cooldown = 0.1f; // Защита от спама
    private float _lastPlayTime;

    private void OnCollisionEnter(Collision collision)
    {
        // 1. Проверка кулдауна
        if (Time.time < _lastPlayTime + cooldown) return;

        // 2. Проверка массива (чтобы не было ошибок в консоли)
        if (clips == null || clips.Length == 0) return;

        float speed = collision.relativeVelocity.magnitude;

        if (speed > minVelocity)
        {
            _lastPlayTime = Time.time;

            // Выбираем звук
            AudioClip clip = clips[Random.Range(0, clips.Length)];

            // 3. Рандомим питч (от 0.9 до 1.1) — это даст ОГРОМНУЮ разницу в сочности
            impactSource.pitch = Random.Range(0.9f, 1.1f);

            // Громкость
            float volume = Mathf.Clamp01(speed * volumeMultiplier);

            impactSource.PlayOneShot(clip, volume);
        }
    }
}