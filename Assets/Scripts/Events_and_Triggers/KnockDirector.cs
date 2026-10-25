using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    // K-01: стук «под ногами». Живёт на объекте Knock Controller рядом с RandomKnock и двигает
    // этот объект (и его AudioSource) под полом к игроку. Время и звук ударов по-прежнему
    // решает RandomKnock — его API (PauseFor/SilencePermanently) и вызовы не меняются.
    // Активность следует за knockController: IntroSequence включает его только в нужные этапы.
    [DisallowMultipleComponent]
    public sealed class KnockDirector : MonoBehaviour
    {
        private const float DepthBelowFloor = 1.2f; // между полом первого этажа и дном подвала
        private const float StillSpeed = 0.3f;
        private const float SlowWalkMargin = 0.15f;
        private const float RunSpeedFactor = 1.25f;
        private const float TeleportDistance = 1.5f;

        private readonly KnockTracker _tracker = new KnockTracker();
        private PlayerController _player;
        private Func<bool> _isNight;
        private Vector3 _home;
        private Vector3 _lastPlayerPosition;
        private bool _initialized;

        public Vector3 SourcePosition => transform.position;
        public bool HasPlayer => _tracker.HasPlayer;
        public float Attention => _tracker.Attention;

        public void Initialize(PlayerController player, Func<bool> isNight, Vector2 houseMin, Vector2 houseMax)
        {
            _player = player;
            _isNight = isNight;
            _home = transform.position;
            _tracker.BoundsMin = houseMin;
            _tracker.BoundsMax = houseMax;
            _initialized = true;
            if (isActiveAndEnabled) ResetToHome();
        }

        private void OnEnable()
        {
            if (_initialized) ResetToHome();
        }

        private void OnDisable()
        {
            if (_initialized) transform.position = _home;
        }

        private void ResetToHome()
        {
            _tracker.Reset(new Vector2(_home.x, _home.z));
            if (_player != null) _lastPlayerPosition = _player.transform.position;
            Apply();
        }

        private void Update()
        {
            if (!_initialized || _player == null) return;
            Vector3 position = _player.transform.position;
            float deltaTime = Time.deltaTime;
            var director = HorrorMomentDirector.Instance;
            if (deltaTime <= 0f || (_isNight != null && !_isNight()) ||
                (director != null && !director.GameplayAvailable))
            {
                // Катсцены и пауза двигают игрока без шагов — это не шум.
                _lastPlayerPosition = position;
                return;
            }

            _tracker.Step(new Vector2(position.x, position.z), ClassifyNoise(position, deltaTime), deltaTime);
            Apply();
        }

        // Скорость — по фактическому смещению: упор в стену или телепорт не считаются шагами.
        private KnockNoise ClassifyNoise(Vector3 position, float deltaTime)
        {
            Vector3 moved = Vector3.ProjectOnPlane(position - _lastPlayerPosition, Vector3.up);
            _lastPlayerPosition = position;
            if (moved.sqrMagnitude > TeleportDistance * TeleportDistance || _player.IsCrouching) return KnockNoise.Quiet;
            float speed = moved.magnitude / deltaTime;
            if (speed < StillSpeed || speed <= _player.crouchSpeed + SlowWalkMargin) return KnockNoise.Quiet;
            return _player.IsRunning || speed > _player.walkSpeed * RunSpeedFactor ? KnockNoise.Run : KnockNoise.Walk;
        }

        private void Apply()
        {
            Vector2 source = _tracker.Source;
            transform.position = new Vector3(source.x, _home.y - DepthBelowFloor, source.y);
        }
    }
}
