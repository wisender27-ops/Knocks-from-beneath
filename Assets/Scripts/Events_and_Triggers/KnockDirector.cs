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
        private const float AudibleDistance = 6f;
        private const float FadeOutDistance = 25f;
        // Глушение перекрытиями: каждое препятствие на луче «уши → источник» срезает верха
        // вдвое и чуть убавляет громкость. 1-й этаж ≈ 2 препятствия, 2-й ≈ 3–5.
        private const float OcclusionInterval = 0.2f;
        private const float OpenCutoff = 5000f;
        private const float MinCutoff = 220f;
        private const float CutoffPerObstacle = 0.5f;
        private const float VolumePerObstacle = 0.85f;
        private const float OcclusionSmooth = 6f;

        private readonly KnockTracker _tracker = new KnockTracker();
        private PlayerController _player;
        private Func<bool> _isNight;
        private Vector3 _home;
        private Vector3 _lastPlayerPosition;
        private bool _initialized;

        private readonly RaycastHit[] _occluders = new RaycastHit[8];
        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private Transform _listener;
        private float _baseVolume = 1f;
        private float _targetCutoff = OpenCutoff;
        private float _targetVolume = 1f;
        private float _nextOcclusion;
        private bool _snapOcclusion = true;

        private RandomKnock _randomKnock;
        private PlayerFloorKnock _playerKnock;
        private int _seenKnocks;

        public Vector3 SourcePosition => transform.position;
        public bool HasPlayer => _tracker.HasPlayer;
        public float Attention => _tracker.Attention;
        public int Occluders { get; private set; }
        public float CutoffFrequency => _lowPass != null ? _lowPass.cutoffFrequency : OpenCutoff;

        public void Initialize(PlayerController player, Func<bool> isNight, Vector2 houseMin, Vector2 houseMax)
        {
            _player = player;
            _isNight = isNight;
            _home = transform.position;
            _tracker.BoundsMin = houseMin;
            _tracker.BoundsMax = houseMax;
            ConfigureAudio(GetComponent<AudioSource>());
            _randomKnock = GetComponent<RandomKnock>();
            AudioListener listener = player != null ? player.GetComponentInChildren<AudioListener>(true) : null;
            _listener = listener != null ? listener.transform : Camera.main != null ? Camera.main.transform : null;
            _initialized = true;
            if (isActiveAndEnabled) ResetToHome();
        }

        // K-02: стук снизу включает подсказку «Q» у игрока.
        public void SetPlayerKnock(PlayerFloorKnock playerKnock) => _playerKnock = playerKnock;

        // K-02: игрок постучал в пол — стучащий знает, где он, и придерживает свой стук.
        public void Hear(Vector3 floor, float holdSeconds)
        {
            if (!_initialized || !isActiveAndEnabled) return;
            _tracker.Hear(new Vector2(floor.x, floor.z));
            HoldKnocks(holdSeconds);
        }

        // Пауза RandomKnock (останавливает и текущий клип).
        public void HoldKnocks(float seconds)
        {
            if (_randomKnock != null) _randomKnock.PauseFor(seconds);
        }

        // Ответный удар из источника под полом — с тем же глушением перекрытиями.
        public bool PlayReplyKnock(AudioClip clip, float pitch)
        {
            if (!isActiveAndEnabled || _source == null || clip == null) return false;
            _source.pitch = pitch;
            _source.PlayOneShot(clip);
            return true;
        }

        public void ResetPitch()
        {
            if (_source != null) _source.pitch = 1f;
        }

        private void OnEnable()
        {
            if (_initialized) ResetToHome();
            _seenKnocks = _randomKnock != null ? _randomKnock.PlayedKnockCount : 0;
        }

        private void OnDisable()
        {
            if (_initialized) transform.position = _home;
        }

        private void NotifyKnockHeard()
        {
            if (_randomKnock == null || _randomKnock.PlayedKnockCount == _seenKnocks) return;
            _seenKnocks = _randomKnock.PlayedKnockCount;
            if (_playerKnock != null) _playerKnock.NotifyKnockFromBelow();
        }

        // Направление «под ногами» слышно только у полностью 3D-источника; сцена задаёт 0.7 и
        // затухание от 2 м — со второго этажа (≈10 м) стук почти не слышен. Сквозь перекрытия
        // он должен читаться: громкость держится до 6 м и спадает к 25 м.
        private void ConfigureAudio(AudioSource source)
        {
            _source = source;
            if (source == null) return;
            source.spatialBlend = 1f;
            source.minDistance = AudibleDistance;
            source.maxDistance = Mathf.Max(source.maxDistance, FadeOutDistance);
            _baseVolume = source.volume;
            _lowPass = GetComponent<AudioLowPassFilter>();
            if (_lowPass == null) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = OpenCutoff;
        }

        // Луч раз в 0.2 с (буфер на 8 попаданий) — без поиска по сцене и аллокаций.
        private void UpdateOcclusion()
        {
            if (_source == null || _lowPass == null || _listener == null) return;
            if (_snapOcclusion || Time.unscaledTime >= _nextOcclusion)
            {
                _nextOcclusion = Time.unscaledTime + OcclusionInterval;
                Vector3 from = _listener.position;
                Vector3 toSource = transform.position - from;
                float distance = toSource.magnitude;
                Occluders = distance > 0.01f
                    ? Physics.RaycastNonAlloc(from, toSource / distance, _occluders, distance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    : 0;
                _targetCutoff = Mathf.Max(MinCutoff, OpenCutoff * Mathf.Pow(CutoffPerObstacle, Occluders));
                _targetVolume = Mathf.Pow(VolumePerObstacle, Occluders);
            }
            float k = _snapOcclusion ? 1f : 1f - Mathf.Exp(-OcclusionSmooth * Time.unscaledDeltaTime);
            _snapOcclusion = false;
            _lowPass.cutoffFrequency = Mathf.Lerp(_lowPass.cutoffFrequency, _targetCutoff, k);
            _source.volume = Mathf.Lerp(_source.volume, _baseVolume * _targetVolume, k);
        }

        private void ResetToHome()
        {
            _tracker.Reset(new Vector2(_home.x, _home.z));
            if (_player != null) _lastPlayerPosition = _player.transform.position;
            Apply();
            _snapOcclusion = true;
        }

        private void Update()
        {
            if (!_initialized || _player == null) return;
            UpdateOcclusion();
            NotifyKnockHeard();
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
