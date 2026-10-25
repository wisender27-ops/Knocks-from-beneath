using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // K-02: стучащий снизу отвечает на стук игрока. Живёт на объекте IntroSequence и не зависит от
    // Knock Controller: тот включён только в отдельные этапы ночи 2, а хозяин в подвале — всегда.
    // Если слежка K-01 (KnockDirector) активна — ответ идёт из её источника, иначе из точки под полом
    // под игроком, глушённой как через одно перекрытие. Короткая серия (1..4) — повтор, длинная — тишина.
    [DisallowMultipleComponent]
    public sealed class KnockResponder : MonoBehaviour
    {
        private const float ListenHold = 3f;       // пока игрок стучит, свой стук стучащий придерживает
        private const float ReplyDelay = 0.9f;
        private const float ReplyInterval = 0.5f;
        private const float AfterReplyQuiet = 3f;
        private const float SilenceSeconds = 10f;  // на беспорядочный стук — затихает
        private const float BusyTimeout = 6f;      // чужая пугалка дольше — ответ пропадает
        private const float DepthBelowFloor = 1.2f;
        private const float ThroughFloorCutoff = 1250f; // как у K-01 при двух препятствиях
        private const float ThroughFloorVolume = 0.72f;

        private readonly KnockReply _reply = new KnockReply();
        private PlayerFloorKnock _playerKnock;
        private KnockDirector _director;
        private AudioClip _clip;
        private Func<bool> _isAwake;
        private AudioSource _source;
        private Coroutine _replyRoutine;
        private Vector3 _lastFloor;

        public bool IsReplying => _replyRoutine != null;
        public KnockReplyKind LastReply { get; private set; }
        public int LastReplyCount { get; private set; }
        public int ReplyKnocksPlayed { get; private set; }
        public event Action<KnockReplyKind, int> Replied;

        public void Initialize(PlayerFloorKnock playerKnock, KnockDirector director, AudioClip clip, Func<bool> isAwake)
        {
            if (_playerKnock != null) _playerKnock.Knocked -= OnPlayerKnocked;
            _playerKnock = playerKnock;
            if (_playerKnock != null) _playerKnock.Knocked += OnPlayerKnocked;
            _director = director;
            _clip = clip;
            _isAwake = isAwake;
            if (_source == null)
            {
                _source = HorrorMomentDirector.CreateSource(transform, "Knock reply under floor", 25f);
                _source.minDistance = 6f;
                _source.volume = ThroughFloorVolume;
                _source.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = ThroughFloorCutoff;
            }
        }

        private void OnDestroy()
        {
            if (_playerKnock != null) _playerKnock.Knocked -= OnPlayerKnocked;
        }

        private void OnDisable()
        {
            _reply.Clear();
            _replyRoutine = null; // корутина останавливается вместе с компонентом
        }

        private bool DirectorActive => _director != null && _director.isActiveAndEnabled;

        private void OnPlayerKnocked(Vector3 floor)
        {
            if (_replyRoutine != null || (_isAwake != null && !_isAwake())) return;
            _reply.Tap(Time.time);
            _lastFloor = floor;
            if (DirectorActive) _director.Hear(floor, _reply.BurstGap + ListenHold);
        }

        private void Update()
        {
            if (_replyRoutine != null) return;
            KnockReplyKind kind = _reply.Poll(Time.time, out int count);
            if (kind == KnockReplyKind.None) return;
            LastReply = kind;
            LastReplyCount = count;
            if (kind == KnockReplyKind.Repeat && _clip != null)
                _replyRoutine = StartCoroutine(RepeatRoutine(count));
            else
            {
                if (DirectorActive) _director.HoldKnocks(SilenceSeconds);
                Replied?.Invoke(kind, count);
            }
        }

        private IEnumerator RepeatRoutine(int count)
        {
            // Не перебиваем чужую пугалку; если она затянулась — ответа не будет.
            float busy = 0f;
            var horror = HorrorMomentDirector.Instance;
            while (horror != null && horror.HasOwner && busy < BusyTimeout)
            {
                if (horror.GameplayAvailable) busy += Time.deltaTime;
                yield return null;
            }
            if (horror != null && horror.HasOwner)
            {
                _replyRoutine = null;
                yield break;
            }

            if (DirectorActive) _director.HoldKnocks(ReplyDelay + count * ReplyInterval + AfterReplyQuiet);
            yield return WaitPlayable(ReplyDelay);
            Vector3 under = _lastFloor - Vector3.up * DepthBelowFloor;
            for (int i = 0; i < count; i++)
            {
                float pitch = UnityEngine.Random.Range(0.92f, 1f);
                if (!DirectorActive || !_director.PlayReplyKnock(_clip, pitch))
                {
                    _source.transform.position = under;
                    _source.pitch = pitch;
                    _source.PlayOneShot(_clip);
                }
                ReplyKnocksPlayed++;
                yield return WaitPlayable(ReplyInterval);
            }
            if (DirectorActive)
            {
                _director.ResetPitch();
                _director.HoldKnocks(AfterReplyQuiet);
            }
            _replyRoutine = null;
            Replied?.Invoke(KnockReplyKind.Repeat, count);
        }

        private static IEnumerator WaitPlayable(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                var director = HorrorMomentDirector.Instance;
                if (director == null || director.GameplayAvailable) elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
