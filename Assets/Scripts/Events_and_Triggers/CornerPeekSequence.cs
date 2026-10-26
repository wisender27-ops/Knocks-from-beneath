using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Пугалка ночи 2 в прихожей (переделана по отзыву автора: раньше голова «выпирала» из стены у шкафа).
    // Монстр заранее, вне поля зрения, встаёт в тёмном конце коридора у входной двери — лицом в угол,
    // сгорбившись. Игрок поворачивает в коридор, луч фонарика его находит: пауза, вдох героя, монстр медленно
    // оборачивается через плечо и смотрит на игрока; удар звука, фонарик мигает — монстр срывается к двери,
    // вышибает её с петель и исчезает на улице. Имя и API класса прежние (IntroSequence, тесты, пробы).
    public sealed class CornerPeekSequence : MonoBehaviour
    {
        // Конец коридора у входной двери: между шкафом (z 114.7) и дверью (z 116.75), в полуметре от торца.
        private static readonly Vector3 StandPoint = new Vector3(-39.25f, 0.78f, 115.7f);
        private static readonly Vector3 StandFacing = new Vector3(1f, 0f, 0.7f).normalized; // лицом в угол у двери
        private const float RevealDistance = 4.6f;
        private const float RevealConeDegrees = 16f;                   // луч фонарика — в центре экрана
        private const float CloseDistance = 2.4f;                      // вплотную видно и без прямого луча
        private const float RunSpeed = 5.5f;

        private PlayerInventory _inventory;
        private IntroSequence _intro;
        private GameObject _knocks;
        private Door _exitDoor;
        private Camera _camera;
        private bool _nightTwo, _played, _beamWasOn;
        private GameObject _monster, _runEmitter;
        private CornerStalkerRig _rig;
        public static bool IsPlaying { get; private set; }
        private PlayerController _player;
        private Behaviour[] _inputs;
        private bool[] _inputStates;
        private bool _controlCaptured, _cameraWasLocked;
        private Vector3 _cameraPosition;
        private Quaternion _cameraRotation;
        private float _cameraFov;
        private Rigidbody _playerBody;
        private bool _bodyWasKinematic;
        private AudioSource _stinger;
        private Light _beam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaying() => IsPlaying = false;

        private static Vector3 Chest => StandPoint + Vector3.up * 1.45f;

        public void Initialize(IntroSequence intro, PlayerInventory inventory, GameObject knocks, Door exitDoor)
        {
            _intro = intro;
            _inventory = inventory;
            _knocks = knocks;
            _exitDoor = exitDoor;
            _camera = Camera.main;
            _player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            _beam = inventory != null ? inventory.flashlightLightSource : null;
        }

        private void OnEnable()
        {
            GameEvents.OnNightTwoStarted += Arm;
            GameEvents.OnDayStarted += Disarm;
        }

        private void Arm() => _nightTwo = true;

        // Сюжет ушёл от дома — пугалка больше не срабатывает; если монстр уже стоит в коридоре, он исчезает.
        public void Retire()
        {
            if (!_played && _monster != null) Destroy(_monster);
            _played = true;
        }

        private void Disarm() => _nightTwo = false;

        private void Update()
        {
            if (!_nightTwo || _played || _camera == null || _inventory == null || !_inventory.hasFlashlight) return;
            if (Time.timeScale == 0f || RetroInventoryScreen.IsAnyOpen) return;
            if ((_player != null && (!_player.enabled || _player.isCameraLocked)) || PlayerInteraction.IsLockSequencePlaying) return;
            if (_intro != null && _intro.HoldsCornerScare) return; // M-04: сначала щиток
            if (_camera.transform.position.y > 3.8f) return;      // игрок на втором этаже

            bool visible = CanSee(Chest, out float distance, out float angle);
            // Ставим монстра только туда, куда игрок сейчас не смотрит, — никакого «появления из воздуха».
            if (_monster == null)
            {
                if (!visible && Flat(_camera.transform.position - StandPoint).magnitude > 2.5f) SpawnMonster();
                return;
            }
            if (!visible || distance > RevealDistance) return;
            if (angle > RevealConeDegrees && distance > CloseDistance) return;
            if (_beam != null && !_beam.enabled && distance > CloseDistance) return; // в темноте его не видно

            _played = true;
            HorrorMomentDirector.Instance?.Interrupt();
            _intro?.PrepareCornerScare();
            NarrativeDisplayQueue.InterruptAndEnqueue(this, Play);
        }

        private bool CanSee(Vector3 point, out float distance, out float angle)
        {
            Vector3 eye = _camera.transform.position;
            Vector3 to = point - eye;
            distance = to.magnitude;
            angle = Vector3.Angle(_camera.transform.forward, to);
            Vector3 view = _camera.WorldToViewportPoint(point);
            if (view.z <= 0f || view.x < 0.05f || view.x > 0.95f || view.y < 0.05f || view.y > 0.95f) return false;
            return !Physics.Raycast(eye, to / distance, distance - 0.15f, PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore);
        }

        private void SpawnMonster()
        {
            GameObject prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) { _played = true; return; }
            _monster = Instantiate(prefab, StandPoint, Quaternion.LookRotation(StandFacing));
            _monster.name = "Hallway Stalker";
            _rig = _monster.GetComponent<CornerStalkerRig>();
            if (_rig == null) _rig = _monster.AddComponent<CornerStalkerRig>();
            _rig.Idle(Random.value);
        }

        private IEnumerator Play()
        {
            if (_monster == null) yield break;
            CaptureControl();
            _beamWasOn = _beam != null && _beam.enabled;
            try
            {
                SilenceKnocks();
                Transform head = _rig != null && _rig.Head != null ? _rig.Head : _monster.transform;

                // 1. Заметил: взгляд сам тянется к нему, герой резко вдыхает. Монстр стоит.
                AudioClip gasp = GameSounds.Pick(GameSounds.Library?.breathScared);
                PlayOneShot(gasp, 0.75f);
                PlayOneShot(GameSounds.Library?.riser, 0.35f);
                for (float t = 0f; t < 1.1f; t += Time.deltaTime)
                {
                    FocusCamera(Chest + Vector3.up * 0.2f, Mathf.SmoothStep(0f, 1f, t / 0.6f), 6f);
                    yield return null;
                }

                // 2. Оборачивается через плечо и смотрит прямо в него. Фонарик дрожит.
                if (_rig != null) _rig.LookTarget = _camera.transform.position;
                for (float t = 0f; t < 1.1f; t += Time.deltaTime)
                {
                    float turn = Mathf.SmoothStep(0f, 1f, t / 0.75f);
                    if (_rig != null)
                    {
                        _rig.LookTarget = _camera.transform.position;
                        _rig.LookWeight = turn;
                    }
                    FocusCamera(Vector3.Lerp(Chest + Vector3.up * 0.2f, head.position, turn), 1f, 6f + 4f * turn);
                    if (_beam != null && t > 0.55f) _beam.enabled = Mathf.PerlinNoise(Time.time * 18f, 0.4f) > 0.3f;
                    yield return null;
                }
                if (_beam != null) _beam.enabled = _beamWasOn;

                // 3. Срывается: удар звука, свет моргает, монстр бросается к двери и вышибает её.
                PlayJumpscare();
                if (_rig != null) _rig.LookWeight = 0f;
                yield return RunToExit();

                // 4. Тишина, взгляд возвращается.
                yield return new WaitForSeconds(0.9f);
                float release = 0f;
                Quaternion focusRotation = _camera.transform.localRotation;
                float focusFov = _camera.fieldOfView;
                while (release < 0.35f)
                {
                    release += Time.deltaTime;
                    float weight = Mathf.SmoothStep(0f, 1f, release / 0.35f);
                    _camera.transform.localRotation = Quaternion.Slerp(focusRotation, _cameraRotation, weight);
                    _camera.fieldOfView = Mathf.Lerp(focusFov, _cameraFov, weight);
                    yield return null;
                }
                RestoreControl();
                ThoughtManager.Instance?.ShowImportantThoughts(new[] {
                    "Блядь! Это что за хрень?!",
                    "Оно стояло прямо тут. Смотрело на меня.",
                    "Стук был из подвала. Оно оттуда вылезло?.."
                }, () => _intro?.ContinueAfterCornerScare());
                if (ThoughtManager.Instance == null) _intro?.ContinueAfterCornerScare();
            }
            finally
            {
                RestoreControl();
                if (_beam != null && _beamWasOn) _beam.enabled = true;
                if (_monster != null) Destroy(_monster);
                if (_runEmitter != null) Destroy(_runEmitter);
            }
        }

        private void SilenceKnocks()
        {
            if (_knocks == null) return;
            foreach (RandomKnock knock in _knocks.GetComponentsInChildren<RandomKnock>(true))
                knock.SilencePermanently();
        }

        private void CaptureControl()
        {
            _cameraPosition = _camera.transform.localPosition;
            _cameraRotation = _camera.transform.localRotation;
            _cameraFov = _camera.fieldOfView;
            _player = _inventory.GetComponentInParent<PlayerController>();
            _cameraWasLocked = _player != null && _player.isCameraLocked;
            _inputs = new Behaviour[] { _player, _inventory, _inventory.GetComponent<PlayerInteraction>(),
                _inventory.GetComponent<PlayerDoorOpen>(), _camera.GetComponentInParent<HeadBobbing>() };
            _inputStates = new bool[_inputs.Length];
            for (int i = 0; i < _inputs.Length; i++)
            {
                _inputStates[i] = _inputs[i] != null && _inputs[i].enabled;
                if (_inputs[i] != null) _inputs[i].enabled = false;
            }
            if (_player != null) { _player.StopMotion(); _player.isCameraLocked = true; }
            _playerBody = _inventory.GetComponentInParent<Rigidbody>();
            if (_playerBody != null)
            {
                _bodyWasKinematic = _playerBody.isKinematic;
                if (!_bodyWasKinematic) { _playerBody.linearVelocity = Vector3.zero; _playerBody.angularVelocity = Vector3.zero; }
                _playerBody.isKinematic = true;
            }
            _controlCaptured = true;
            IsPlaying = true;
        }

        // Мягко подводит взгляд к точке; zoom — на сколько градусов сужается FOV («всматривается»).
        private void FocusCamera(Vector3 target, float weight, float zoom)
        {
            Vector3 direction = target - _camera.transform.position;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion localAim = _camera.transform.parent != null
                ? Quaternion.Inverse(_camera.transform.parent.rotation) * Quaternion.LookRotation(direction)
                : Quaternion.LookRotation(direction);
            _camera.transform.localRotation = Quaternion.Slerp(_cameraRotation, localAim, Mathf.Clamp01(weight));
            _camera.fieldOfView = Mathf.Lerp(_cameraFov, Mathf.Max(35f, _cameraFov - zoom), Mathf.Clamp01(weight));
        }

        private void PlayJumpscare()
        {
            EnsureStinger();
            _stinger.volume = 0.85f;
            _stinger.clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (_stinger.clip != null) _stinger.Play();
            // A-01: низкий удар под скример — вес в теле, а не только громкость.
            AudioClip sub = GameSounds.Pick(GameSounds.Library?.subHits);
            if (sub != null) _stinger.PlayOneShot(sub, .7f);
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip == null) return;
            EnsureStinger();
            _stinger.PlayOneShot(clip, volume);
        }

        private void EnsureStinger()
        {
            if (_stinger != null) return;
            _stinger = gameObject.AddComponent<AudioSource>();
            _stinger.playOnAwake = false;
            _stinger.spatialBlend = 0f;
            AudioRouting.Assign(_stinger, AudioBus.Horror);
        }

        private void RestoreControl()
        {
            if (!_controlCaptured) return;
            _controlCaptured = false;
            if (_camera != null)
            {
                _camera.transform.localPosition = _cameraPosition;
                _camera.transform.localRotation = _cameraRotation;
                _camera.fieldOfView = _cameraFov;
            }
            if (_playerBody != null) _playerBody.isKinematic = _bodyWasKinematic;
            for (int i = 0; i < _inputs.Length; i++) if (_inputs[i] != null) _inputs[i].enabled = _inputStates[i];
            if (_player != null) _player.isCameraLocked = _cameraWasLocked;
            if (_stinger != null) { _stinger.Stop(); Destroy(_stinger); }
            IsPlaying = false;
        }

        // Разворот к двери, рывок, дверь слетает с петель, монстр уходит в темноту двора. Взгляд провожает его.
        private IEnumerator RunToExit()
        {
            Transform body = _monster.transform;
            Vector3 doorway = _exitDoor != null
                ? new Vector3(_exitDoor.transform.position.x + 0.45f, StandPoint.y, _exitDoor.transform.position.z)
                : StandPoint + Vector3.forward * 1.1f;
            Vector3 outside = doorway + Vector3.forward * 3.5f;
            AudioClip run = Resources.Load<AudioClip>("Audio/CornerEscapeRun");
            _runEmitter = new GameObject("CornerEscapeAudio");
            _runEmitter.transform.SetParent(body, false);
            _runEmitter.transform.localPosition = Vector3.up;
            AudioSource source = _runEmitter.AddComponent<AudioSource>();
            AudioRouting.Assign(source, AudioBus.Horror);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 18f;
            source.dopplerLevel = 0f;
            source.volume = 0.9f;
            if (run != null) { source.clip = run; source.Play(); }

            _rig?.Run(1.25f);
            Quaternion from = body.rotation, to = Quaternion.LookRotation(Flat(doorway - body.position));
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                body.rotation = Quaternion.Slerp(from, to, t / 0.14f);
                Follow(body);
                yield return null;
            }
            body.rotation = to;
            bool broken = false;
            Vector3[] path = { doorway, outside };
            foreach (Vector3 point in path)
            {
                while (_monster != null && Flat(point - body.position).magnitude > 0.05f)
                {
                    body.position = Vector3.MoveTowards(body.position, point, RunSpeed * Time.deltaTime);
                    Vector3 heading = Flat(point - body.position);
                    if (heading.sqrMagnitude > 0.0001f)
                        body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(heading), 1f - Mathf.Exp(-14f * Time.deltaTime));
                    if (!broken && _exitDoor != null && Flat(_exitDoor.transform.position - body.position).magnitude < 0.9f)
                    {
                        broken = true;
                        BreakDoor();
                    }
                    Follow(body);
                    yield return null;
                }
            }
            if (!broken) BreakDoor();
            if (_monster != null) Destroy(_monster);
            _monster = null;
            if (_runEmitter != null)
            {
                _runEmitter.transform.SetParent(null, true);
                Destroy(_runEmitter, 2f);
                _runEmitter = null;
            }
        }

        private void BreakDoor()
        {
            if (_exitDoor == null) return;
            _exitDoor.BreakOffHinges(Vector3.forward, Resources.Load<AudioClip>("Audio/CornerDoorCrack"),
                GameSounds.Library?.doorLanding != null ? GameSounds.Library.doorLanding
                    : Resources.Load<AudioClip>("Audio/CornerDoorLanding"));
            GameState.frontDoorLocked = false;
        }

        private void Follow(Transform body) =>
            FocusCamera(body.position + Vector3.up * 1.5f, 1f, 10f);

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void OnDisable()
        {
            RestoreControl();
            GameEvents.OnNightTwoStarted -= Arm;
            GameEvents.OnDayStarted -= Disarm;
            if (_monster != null) Destroy(_monster);
            if (_runEmitter != null) Destroy(_runEmitter);
        }
    }
}
