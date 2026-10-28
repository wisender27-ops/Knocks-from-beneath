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
        private float _floorHeight;
        public int SeriesPlayed { get; private set; }
        public Vector3 LastPosition { get; private set; }

        public void Initialize(Transform player, KnockResponder responder, AudioClip clip, Func<Transform> target, float floorHeight)
        {
            _floorHeight = floorHeight;
            _player = player;
            _responder = responder;
            _clip = clip;
            _target = target;
            _source = HorrorMomentDirector.CreateSource(transform, "Knock hint under target", 18f);
            _source.minDistance = 2f;
            _source.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1250f;
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
                _tapAt = Time.time + 0.55f;
                if (--_remaining == 0)
                {
                    SeriesPlayed++;
                    Release();
                }
                return;
            }
            if (target == null || Time.time < _next || MinigameWindow.IsAnyOpen ||
                (_responder != null && (_responder.IsBusy || _responder.Suspended)) ||
                !CanGuide(StoryFlags.KnockTrust, (_player.position - target.position).sqrMagnitude) ||
                !horror.TryBegin(this)) return;
            _current = target;
            LastPosition = new Vector3(target.position.x, _floorHeight - 1.2f, target.position.z);
            _source.transform.position = LastPosition;
            _responder?.HoldKnocks(7f);
            if (_responder != null) _responder.Suspended = true;
            _remaining = 3;
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
