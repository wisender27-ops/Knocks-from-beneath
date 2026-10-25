using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // K-02: стучащий снизу отвечает на стук игрока. Живёт на объекте IntroSequence и не зависит от
    // Knock Controller: тот включён только в отдельные этапы ночи 2, а хозяин в подвале — всегда.
    // Если слежка K-01 (KnockDirector) активна — ответ идёт из её источника, иначе из точки под полом
    // под игроком, глушённой как через одно перекрытие. Короткая серия (1..4) — повтор, длинная — тишина.
    // Первый спам за игру — «срыв»: через 3–4 с снизу бьют со всей силы (встряска камеры), потом тишина.
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
        private const float RageDelayMin = 3f;
        private const float RageDelayMax = 4f;
        private const float RageJolt = 5f;
        private const float RagePitch = 0.92f;   // почти родной тон: удар резкий, не тягучий
        private const float RageGain = 1.8f;     // PlayOneShot усиливает и выше 1

        private readonly KnockReply _reply = new KnockReply();
        private PlayerFloorKnock _playerKnock;
        private KnockDirector _director;
        private AudioClip _clip;
        private Func<bool> _isAwake;
        private AudioSource _source;
        private Coroutine _replyRoutine;
        private Vector3 _lastFloor;
        private AudioSource _rageSource;
        private AudioClip[] _rageLayers;

        public bool IsReplying => _replyRoutine != null;
        // K-03: пока идёт «Перестук», удары игрока — ответ на ритм, а не новая серия.
        public bool Suspended { get; set; }
        public bool IsBusy => _replyRoutine != null || _reply.IsListening;
        public KnockReplyKind LastReply { get; private set; }
        public int LastReplyCount { get; private set; }
        public int ReplyKnocksPlayed { get; private set; }
        public bool HasRaged { get; private set; } // «срыв» случается один раз за игру
        public float LastRageTime { get; private set; } = float.NegativeInfinity;
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
            if (_rageSource == null)
            {
                // Без глушения: удар такой силы пробивает пол.
                _rageSource = HorrorMomentDirector.CreateSource(transform, "Knock rage under floor", 30f);
                _rageSource.minDistance = 10f;
                _rageSource.spatialBlend = 0.6f; // наполовину «в голове» — бьёт по ушам, но слышно, что снизу
                _rageSource.priority = 10;
            }
            _rageLayers = new[]
            {
                Resources.Load<AudioClip>("Audio/Horror/impactPunch_heavy_000"),
                Resources.Load<AudioClip>("Audio/Horror/impactWood_heavy_000"),
                Resources.Load<AudioClip>("Audio/Horror/impactPlank_medium_000")
            };
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
            if (Suspended || _replyRoutine != null || (_isAwake != null && !_isAwake())) return;
            _reply.Tap(Time.time);
            _lastFloor = floor;
            if (DirectorActive) _director.Hear(floor, _reply.BurstGap + ListenHold);
        }

        private void Update()
        {
            if (_replyRoutine != null || Suspended) return;
            KnockReplyKind kind = _reply.Poll(Time.time, out int count);
            if (kind == KnockReplyKind.None) return;
            LastReply = kind;
            LastReplyCount = count;
            if (kind == KnockReplyKind.Repeat && _clip != null)
                _replyRoutine = StartCoroutine(RepeatRoutine(count));
            else if (kind == KnockReplyKind.Silence && !HasRaged && _rageLayers[0] != null)
            {
                HasRaged = true;
                _replyRoutine = StartCoroutine(RageRoutine(count));
            }
            else
            {
                if (DirectorActive) _director.HoldKnocks(SilenceSeconds);
                Replied?.Invoke(kind, count);
            }
        }

        // Не перебиваем чужую пугалку; если она затянулась — ответа не будет.
        private IEnumerator WaitUntilFree()
        {
            float busy = 0f;
            var horror = HorrorMomentDirector.Instance;
            while (horror != null && horror.HasOwner && busy < BusyTimeout)
            {
                if (horror.GameplayAvailable) busy += Time.deltaTime;
                yield return null;
            }
        }

        private static bool Busy => HorrorMomentDirector.Instance != null && HorrorMomentDirector.Instance.HasOwner;

        // Тишина, а потом — со всей силы, прямо под тем местом, где игрок стучал.
        private IEnumerator RageRoutine(int count)
        {
            yield return WaitUntilFree();
            float delay = UnityEngine.Random.Range(RageDelayMin, RageDelayMax);
            if (DirectorActive) _director.HoldKnocks(delay + SilenceSeconds);
            yield return WaitPlayable(delay);
            if (Busy)
            {
                HasRaged = false; // не сработало — пусть сорвётся в другой раз
                _replyRoutine = null;
                yield break;
            }
            _rageSource.transform.position = _lastFloor - Vector3.up * 0.6f;
            // Pitch источника общий для всех one-shot. Каждый слой дважды — ещё +6 дБ сверху.
            float[] volumes = { 1f, 1f, 0.8f };
            _rageSource.pitch = RagePitch;
            for (int i = 0; i < _rageLayers.Length; i++)
            {
                if (_rageLayers[i] == null) continue;
                _rageSource.PlayOneShot(_rageLayers[i], volumes[i] * RageGain);
                _rageSource.PlayOneShot(_rageLayers[i], volumes[i] * RageGain);
            }
            if (_playerKnock != null) _playerKnock.Jolt(RageJolt);
            LastRageTime = Time.time;
            if (DirectorActive) _director.HoldKnocks(SilenceSeconds);
            _replyRoutine = null;
            Replied?.Invoke(KnockReplyKind.Silence, count);
        }

        private IEnumerator RepeatRoutine(int count)
        {
            yield return WaitUntilFree();
            if (Busy)
            {
                _replyRoutine = null;
                yield break;
            }

            if (DirectorActive) _director.HoldKnocks(ReplyDelay + count * ReplyInterval + AfterReplyQuiet);
            yield return WaitPlayable(ReplyDelay);
            for (int i = 0; i < count; i++)
            {
                PlayUnderFloor(_lastFloor, UnityEngine.Random.Range(0.92f, 1f));
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

        // Один удар стучащего: из источника слежки K-01, если она активна, иначе из-под пола под точкой.
        public void PlayUnderFloor(Vector3 floor, float pitch, float volume = 1f)
        {
            if (_clip == null) return;
            if (DirectorActive && _director.PlayReplyKnock(_clip, pitch, volume)) return;
            _source.transform.position = floor - Vector3.up * DepthBelowFloor;
            _source.pitch = pitch;
            _source.PlayOneShot(_clip, volume);
        }

        public void HoldKnocks(float seconds)
        {
            if (DirectorActive) _director.HoldKnocks(seconds);
        }

        public void ResetDirectorPitch()
        {
            if (DirectorActive) _director.ResetPitch();
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
