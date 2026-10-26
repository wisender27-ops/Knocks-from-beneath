using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // K-03, мини-игра M1 «Перестук». Хозяин в подвале сам простукивает ритм под игроком, когда тот
    // стоит над подвалом; игрок повторяет его клавишей Q (сидя, как в K-02). Без окна — в мире.
    // Успех: тихий ответный удар, доверие +1. Ошибка или молчание: тишина, доверие −1; на уровнях 2–3
    // после первой ошибки хозяин повторяет ритм ещё раз. Уровень по главе даёт IntroSequence
    // (0 — сейчас нельзя), раундов на уровень — RoundsPerLevel. Удары игрока в раунде не уходят в KnockResponder.
    [DisallowMultipleComponent]
    public sealed class KnockCallGame : MonoBehaviour
    {
        private const float DwellBeforeCall = 2f;  // столько игрок стоит над подвалом — и снизу стучат
        private const float Cooldown = 40f;        // между раундами
        private const float QuietAfterPlayer = 12f; // игрок сам стучал (K-02) — хозяин не перебивает
        private const float LeadIn = 0.6f;
        private const float RetryPause = 1.5f;
        private const float AckDelay = 0.8f;
        private const float RecoverySeconds = 5f;
        private static readonly int[] RoundsPerLevel = { 1, 2, 2 };

        private PlayerFloorKnock _playerKnock;
        private KnockResponder _responder;
        private Func<int> _level;
        private Action<string[]> _showThoughts;
        private readonly KnockAnswer _answer = new KnockAnswer();
        private readonly int[] _roundsPlayed = new int[3];
        private Coroutine _round;
        private bool _listening;
        private float _clock;      // игровое время раунда: стоит на паузе, в инвентаре, в катсцене
        private float _dwell;
        private float _nextRoundTime;
        private int _variant;
        private int _successes;
        private int _roundLevel;
        private float _lastPlayerKnock = float.NegativeInfinity;

        public bool IsActive => _round != null;
        public KnockPattern CurrentPattern { get; private set; }
        public KnockCallOutcome LastOutcome { get; private set; }
        public int TotalRounds { get; private set; }
        public int PatternKnocksPlayed { get; private set; }
        public event Action<KnockCallOutcome> Finished;

        public void Initialize(PlayerFloorKnock playerKnock, KnockResponder responder, Func<int> level, Action<string[]> showThoughts)
        {
            if (_playerKnock != null) _playerKnock.Knocked -= OnPlayerKnocked;
            _playerKnock = playerKnock;
            if (_playerKnock != null) _playerKnock.Knocked += OnPlayerKnocked;
            _responder = responder;
            _level = level;
            _showThoughts = showThoughts;
            _variant = UnityEngine.Random.Range(0, 6);
        }

        private void OnEnable() => GameEvents.OnDayStarted += Abort;

        private void OnDisable()
        {
            GameEvents.OnDayStarted -= Abort;
            Abort();
        }

        private void OnDestroy()
        {
            if (_playerKnock != null) _playerKnock.Knocked -= OnPlayerKnocked;
        }

        private static HorrorMomentDirector Director => HorrorMomentDirector.Instance;
        private static bool Playable => Director == null || Director.GameplayAvailable;

        private int CurrentLevel => _level != null ? Mathf.Clamp(_level(), 0, RoundsPerLevel.Length) : 0;

        private void OnPlayerKnocked(Vector3 floor)
        {
            _lastPlayerKnock = Time.time;
            if (_listening) _answer.Tap(_clock);
        }

        private void Update()
        {
            if (_round != null)
            {
                if (!Owns) Abort(); // раунд отобрала чужая пугалка — корутина уже вышла сама
                else if (Playable) _clock += Time.deltaTime;
                return;
            }
            int level = CurrentLevel;
            if (!CanCall(level))
            {
                _dwell = 0f;
                return;
            }
            _dwell += Time.deltaTime;
            if (_dwell >= DwellBeforeCall) TryStartRound();
        }

        private bool CanCall(int level) =>
            level > 0 && _roundsPlayed[level - 1] < RoundsPerLevel[level - 1] && Time.time >= _nextRoundTime &&
            Time.time - _lastPlayerKnock >= QuietAfterPlayer &&
            _playerKnock != null && Director != null && Director.CanPresent && !Director.HasOwner &&
            !MinigameWindow.IsAnyOpen && (_responder == null || !_responder.IsBusy) && _playerKnock.CheckOverBasement();

        // Без ожидания над подвалом и кулдауна — для Play-проб; гейты сюжета и пугалок те же.
        public bool TryStartRound()
        {
            int level = CurrentLevel;
            if (_round != null || level == 0 || _playerKnock == null || Director == null ||
                !_playerKnock.CheckOverBasement() || (_responder != null && _responder.IsBusy) ||
                MinigameWindow.IsAnyOpen || !Director.TryBegin(this)) return false;
            _dwell = 0f;
            _roundLevel = level;
            _roundsPlayed[level - 1]++;
            TotalRounds++;
            CurrentPattern = KnockPattern.ForLevel(level, _variant++);
            _round = StartCoroutine(Round(CurrentPattern, _playerKnock.FloorPoint));
            return true;
        }

        private IEnumerator Round(KnockPattern pattern, Vector3 floor)
        {
            if (_responder != null) _responder.Suspended = true;
            LastOutcome = KnockCallOutcome.None;
            KnockCallOutcome outcome = KnockCallOutcome.None;
            int attempts = pattern.Level >= 2 ? 2 : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (_responder != null)
                    _responder.HoldKnocks(LeadIn + pattern.Duration + _answer.ResponseWindow + _answer.EndGap + 4f);
                yield return Wait(attempt == 0 ? LeadIn : RetryPause);
                if (!Owns) yield break;
                for (int i = 0; i < pattern.TapCount; i++)
                {
                    if (i > 0) yield return Wait(pattern.GapBefore(i));
                    if (!Owns) yield break;
                    if (_responder != null) _responder.PlayUnderFloor(floor, UnityEngine.Random.Range(0.92f, 1f));
                    PatternKnocksPlayed++;
                }
                if (_responder != null) _responder.ResetDirectorPitch();
                if (attempt == 0) _playerKnock.PromptAnswer();

                _answer.Begin(pattern, _clock);
                _listening = true;
                while ((outcome = _answer.Poll(_clock)) == KnockCallOutcome.None)
                {
                    if (!Owns) yield break;
                    yield return null;
                }
                _listening = false;
                if (outcome != KnockCallOutcome.Wrong) break;
            }

            int trustBefore = StoryFlags.KnockTrust;
            if (outcome == KnockCallOutcome.Correct)
            {
                yield return Wait(AckDelay);
                if (!Owns) yield break;
                if (_responder != null)
                {
                    _responder.PlayUnderFloor(floor, 0.85f, 0.6f);
                    _responder.ResetDirectorPitch();
                }
                StoryFlags.AddKnockTrust(1);
                StoryFlags.MarkKnockAnswered();
                _successes++;
            }
            else StoryFlags.AddKnockTrust(-1);
            Finish(outcome);
            _showThoughts?.Invoke(WithTrustLine(Reaction(outcome), TrustLine(trustBefore, StoryFlags.KnockTrust)));
        }

        public const int TrustedLevel = 3;
        public const int DistrustLevel = -2;

        // K-05: доверие перешло порог — герой это замечает (одна мысль на переход, не каждый раунд).
        public static string TrustLine(int before, int after)
        {
            if (before < TrustedLevel && after >= TrustedLevel)
                return "Он отвечает сразу. Будто ждёт меня. Будто доверяет.";
            if (before > DistrustLevel && after <= DistrustLevel)
                return "Он больше не зовёт. Обиделся? На что вообще обижается подвал...";
            return null;
        }

        private static string[] WithTrustLine(string[] lines, string trustLine)
        {
            if (trustLine == null) return lines;
            var all = new string[lines.Length + 1];
            lines.CopyTo(all, 0);
            all[lines.Length] = trustLine;
            return all;
        }

        private string[] Reaction(KnockCallOutcome outcome)
        {
            switch (outcome)
            {
                case KnockCallOutcome.Correct:
                    return _successes == 1
                        ? new[] { "Тот же ритм... и тихий стук в ответ.", "Это не трубы. Там внизу кто-то есть. И он меня понял." }
                        : new[] { "Он снова ответил. Будто кивнул." };
                case KnockCallOutcome.Wrong:
                    return new[] { "Не тот ритм. Снизу — ни звука." };
                default:
                    return new[] { "Я не ответил. Внизу стало тихо." };
            }
        }

        private bool Owns => Director != null && Director.IsOwner(this);

        private void Finish(KnockCallOutcome outcome)
        {
            LastOutcome = outcome;
            Release();
            Finished?.Invoke(outcome);
        }

        // Глава сменилась или раунд отобрали — без итога и без изменения доверия; раунд не засчитан.
        private void Abort()
        {
            if (_round == null) return;
            StopCoroutine(_round);
            _roundsPlayed[_roundLevel - 1]--;
            TotalRounds--;
            Release();
        }

        private void Release()
        {
            _listening = false;
            _round = null;
            _nextRoundTime = Time.time + Cooldown;
            if (_responder != null) _responder.Suspended = false;
            HorrorMomentDirector director = Director;
            if (director != null) director.End(this, RecoverySeconds);
        }

        private IEnumerator Wait(float duration)
        {
            float end = _clock + duration;
            while (_clock < end && Owns) yield return null;
        }
    }
}
