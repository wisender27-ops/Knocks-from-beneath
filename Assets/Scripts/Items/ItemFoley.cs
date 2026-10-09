using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class ItemFoley : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip grabClip;
        [SerializeField] private AudioClip placeClip;
        [SerializeField] private AudioClip throwClip;
        private float nextSound;

        public void PlayGrab() => Play(grabClip, .45f);
        public void PlayPlace() => Play(placeClip, .4f);
        public void PlayThrow() => Play(throwClip, .3f);

        private void Play(AudioClip clip, float volume)
        {
            if (!Application.isPlaying || AudioListener.pause || Time.timeScale <= 0f ||
                source == null || clip == null || Time.unscaledTime < nextSound) return;
            source.pitch = Random.Range(.96f, 1.04f);
            source.PlayOneShot(clip, volume);
            nextSound = Time.unscaledTime + .08f;
        }
    }
}
