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
        public int AnsweredKnocks { get; private set; } // сколько ударов было в ответе снизу
        private int answerCount; // K-02: сколько раз игрок постучал перед ответом (0 — не стучал)

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
            answerCount = 0;
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
                PlayerFloorKnock floorKnock = PlayerFloorKnock.Instance;
                if (floorKnock != null) floorKnock.NotifyKnockFromBelow();
                int knocksBefore = floorKnock != null ? floorKnock.KnockCount : 0;
                yield return WaitPlayable(1.6f);

                // Ответ следует за первым движением или стуком игрока (K-02); неподвижный игрок
                // тоже получит сцену. На стук — повтор числа ударов, как только серия закончилась.
                var reply = new KnockReply();
                float wait = 0f;
                while (wait < 4.5f || reply.IsListening)
                {
                    if (!director.IsOwner(this)) yield break;
                    int knocks = floorKnock != null ? floorKnock.KnockCount : 0;
                    for (; knocksBefore < knocks; knocksBefore++) reply.Tap(Time.time);
                    if (reply.IsListening)
                    {
                        if (reply.Poll(Time.time, out answerCount) != KnockReplyKind.None) break;
                    }
                    else if (Vector3.ProjectOnPlane(player.transform.position - origin, Vector3.up).sqrMagnitude >= 0.36f) break;
                    if (director.GameplayAvailable) wait += Time.deltaTime;
                    UpdatePause();
                    yield return null;
                }
                int answers = AnsweredKnocks = answerCount >= 1 && answerCount <= reply.MaxRepeat ? answerCount : 1; // на беспорядочный — один удар
                for (int i = 0; i < answers; i++)
                {
                    yield return WaitPlayable(i == 0 ? 0.38f : 0.5f);
                    if (!director.IsOwner(this)) yield break;
                    PlayAt(knock, origin - Vector3.up * 0.08f + right * 0.35f, 0.85f, 0.78f);
                }
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
