using UnityEngine;

public class ImpactSounds : MonoBehaviour
{
    public AudioSource impactSource;
    public AudioClip[] clips;
    public float minVelocity = 1.5f; // Минимальная скорость для звука

    private void OnCollisionEnter(Collision collision)
    {
        // Проверяем силу удара
        float speed = collision.relativeVelocity.magnitude;

        if (speed > minVelocity)
        {
            // Выбираем случайный звук из массива
            AudioClip clip = clips[Random.Range(0, clips.Length)];

            // Громкость зависит от силы удара (но не выше 1)
            float volume = Mathf.Clamp01(speed / 10f);

            impactSource.PlayOneShot(clip, volume);
        }
    }
}