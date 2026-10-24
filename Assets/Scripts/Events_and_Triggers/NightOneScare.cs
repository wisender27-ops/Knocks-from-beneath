using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class NightOneScare : MonoBehaviour
    {
        private PlayerController player;
        private AudioSource source;
        private AudioClip knock, creak;
        private bool played, paused;
        public bool IsPlaying { get; private set; }

        public void Initialize(PlayerInventory inventory)
        {
            player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            knock = Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");
            creak = Resources.Load<AudioClip>("Audio/Horror/FloorCreak");
            source = HorrorMomentDirector.CreateSource(transform, "First night floor", 18f);
        }

        public IEnumerator Play()
        {
            if (played || player == null || knock == null) yield break;
            played = true;
            var director = HorrorMomentDirector.Instance;
            if (director == null) yield break;
            float acquireWait = 0f;
            while (!director.TryBegin(this, true))
            {
                if (!isActiveAndEnabled || director == null) yield break;
                if (Time.timeScale > 0f && !RetroInventoryScreen.IsAnyOpen && !QuestJournalScreen.IsAnyOpen)
                    acquireWait += Time.deltaTime;
                if (acquireWait >= 12f) yield break;
                yield return null;
            }
            IsPlaying = true;
            Vector3 origin = player.transform.position;
            Vector3 right = player.transform.right;
            try
            {
                yield return WaitPlayable(3.2f);
                if (!director.IsOwner(this)) yield break;
                PlayAt(knock, origin - Vector3.up * 0.1f - right * 1.6f, 0.58f, 0.95f);
                yield return WaitPlayable(0.64f);
                if (!director.IsOwner(this)) yield break;
                PlayAt(knock, origin - Vector3.up * 0.1f - right * 1.6f, 0.65f, 0.88f);
                yield return WaitPlayable(1.6f);

                // Ответ следует за первым движением; неподвижный игрок тоже получит сцену.
                float wait = 0f;
                while (wait < 4.5f && Vector3.ProjectOnPlane(player.transform.position - origin, Vector3.up).sqrMagnitude < 0.36f)
                {
                    if (!director.IsOwner(this)) yield break;
                    if (director.GameplayAvailable) wait += Time.deltaTime;
                    UpdatePause();
                    yield return null;
                }
                yield return WaitPlayable(0.38f);
                if (!director.IsOwner(this)) yield break;
                PlayAt(knock, origin - Vector3.up * 0.08f + right * 0.35f, 0.85f, 0.78f);
                yield return WaitPlayable(1.1f);
                if (!director.IsOwner(this)) yield break;
                PlayAt(creak, origin + right * 0.65f, 0.38f, 0.92f);
                yield return WaitPlayable(2.1f);
            }
            finally
            {
                IsPlaying = false;
                if (source != null) source.Stop();
                director.End(this, 20f);
            }
        }

        private IEnumerator WaitPlayable(float duration)
        {
            float elapsed = 0f;
            var director = HorrorMomentDirector.Instance;
            while (elapsed < duration && director != null && director.IsOwner(this))
            {
                UpdatePause();
                if (director.GameplayAvailable) elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void UpdatePause()
        {
            var director = HorrorMomentDirector.Instance;
            HorrorMomentDirector.SetAudioPaused(source, director == null || !director.GameplayAvailable, ref paused);
        }

        private void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null || source == null) return;
            source.transform.position = position;
            source.pitch = pitch;
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
            HorrorMomentDirector.Instance?.End(this, 0f);
            IsPlaying = false;
        }
    }
}
