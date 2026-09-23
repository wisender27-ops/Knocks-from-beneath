using UnityEngine;

namespace KnocksFromBeneath
{

public class ImpactSounds : MonoBehaviour
{
    public AudioSource impactSource;
    public AudioClip[] clips;

    [Header("Настройки физики")]
    public float minVelocity = 1.5f;
    [SerializeField] private float volumeMultiplier = 0.1f; // speed / 10

    [Header("Тайминги")]
    [SerializeField] private float cooldown = 0.1f; // Защита от спама
    private float _nextPlayTime;

    private void OnCollisionEnter(Collision collision)
    {
        if (gameObject.layer == LayerMask.NameToLayer("HeldItem")) return;
        if (Time.time < _nextPlayTime) return;

        if (impactSource == null || clips == null || clips.Length == 0) return;

        float speed = 0f;
        for (int i = 0; i < collision.contactCount; i++)
        {
            float normalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(i).normal));
            speed = Mathf.Max(speed, normalSpeed);
        }

        if (speed <= minVelocity) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        float pitch = Random.Range(0.9f, 1.1f);
        impactSource.pitch = pitch;
        float volume = Mathf.Clamp01((speed - minVelocity) * volumeMultiplier);
        impactSource.PlayOneShot(clip, volume);
        _nextPlayTime = Time.time + Mathf.Max(cooldown, clip.length / pitch);
    }
}
}
