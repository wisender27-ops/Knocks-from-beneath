using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class KnockHints : MonoBehaviour
    {
        private Transform _player;
        private Func<Transform> _target;
        private KnockResponder _responder;
        private AudioSource _source;
        private AudioClip _clip;
        private Transform _current;
        private float _next, _tapAt;
        private int _remaining;
        public int SeriesPlayed { get; private set; }
        public Vector3 LastPosition { get; private set; }

        // S-02: подсказка — голос Хейла: подпись 2·1 из точки трубы, ближайшей к цели.
        public void Initialize(Transform player, KnockResponder responder, AudioClip clip, Func<Transform> target)
        {
            _player = player;
            _responder = responder;
            _clip = clip;
            _target = target;
            _source = HorrorMomentDirector.CreateSource(transform, "Knock hint under target", 18f);
            _source.minDistance = 2f;
            _source.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 2600f;
            _next = Time.time + 12f;
        }

        public static bool CanGuide(int trust, float distanceSquared) =>
            trust >= KnockCallGame.TrustedLevel && distanceSquared >= 2.25f && distanceSquared <= 144f;

        private void Update()
        {
            var horror = HorrorMomentDirector.Instance;
            Transform target = _target?.Invoke();
            if (_player == null || _clip == null || _source == null || horror == null) return;
            if (_remaining > 0)
            {
                if (!horror.IsOwner(this) || target != _current || !horror.GameplayAvailable ||
                    MinigameWindow.IsAnyOpen || StoryFlags.KnockTrust < KnockCallGame.TrustedLevel)
                { Cancel(); return; }
                if (Time.time < _tapAt) return;
                _source.PlayOneShot(_clip, 0.72f);
                if (--_remaining == 0)
                {
                    SeriesPlayed++;
                    Release();
                    return;
                }
                KnockPattern signature = KnockPattern.HaleSignature;
                _tapAt = Time.time + signature.GapBefore(signature.TapCount - _remaining);
                return;
            }
            if (target == null || Time.time < _next || MinigameWindow.IsAnyOpen ||
                (_responder != null && (_responder.IsBusy || _responder.Suspended)) ||
                !CanGuide(StoryFlags.KnockTrust, (_player.position - target.position).sqrMagnitude) ||
                !horror.TryBegin(this)) return;
            _current = target;
            LastPosition = HalePipe.NearestPoint(target.position);
            _source.transform.position = LastPosition;
            _responder?.HoldKnocks(7f);
            if (_responder != null) _responder.Suspended = true;
            _remaining = KnockPattern.HaleSignature.TapCount;
            _tapAt = Time.time + 0.4f;
        }

        private void Release()
        {
            _remaining = 0;
            _current = null;
            _next = Time.time + 35f;
            if (_responder != null) _responder.Suspended = false;
            HorrorMomentDirector.Instance?.End(this, 5f);
        }

        private void Cancel()
        {
            if (_remaining == 0) return;
            if (_source != null) _source.Stop();
            Release();
        }

        private void OnEnable() => GameEvents.OnDayStarted += Cancel;
        private void OnDisable()
        {
            GameEvents.OnDayStarted -= Cancel;
            Cancel();
        }
    }
}
