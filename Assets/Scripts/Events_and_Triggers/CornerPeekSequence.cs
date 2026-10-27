using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Пугалка ночи 2 «из-за угла» — прямой скример (по отзыву автора). Монстр прячется за углом коридора у шкафа
    // (в самом коридоре, а не в стене). Игрок с фонариком подходит к углу со стороны кухни — монстр за 0.14 с
    // выпрыгивает прямо перед лицом с тянущейся рукой: удар звука, рывок камеры на морду, тряска, фонарик мигает.
    // Полсекунды морда в морду — и он срывается в коридор и через входную дверь исчезает на улице. Дверь к этому
    // моменту уже открыта (B-02): засов не задвинут — вошёл тихо, задвинут — выбил её в начале ночи; если игрок
    // успел закрыть — монстр распахивает её на бегу.
    public sealed class CornerPeekSequence : MonoBehaviour
    {
        private static readonly Vector3 Corner = new Vector3(-42.0f, 0.78f, 114.7f);   // западный край шкафа
        private static readonly Vector3 Hidden = new Vector3(-41.5f, 0.78f, 115.3f);   // в коридоре, за углом
        private static readonly Vector3 Mouth = new Vector3(-42.3f, 0.78f, 115.0f);    // выход из-за угла, мимо края шкафа
        private static readonly Vector3 CorridorWaypoint = new Vector3(-40.1f, 0.78f, 115.75f);
        private const float TriggerDistance = 1.9f;    // до угла по полу
        private const float FaceDistance = 1.1f;       // где он встаёт перед лицом: морда и тянущаяся рука в кадре
        private const float LungeSeconds = 0.14f;
        private const float HoldSeconds = 0.55f;
        private const float RunSpeed = 5.5f;

        private PlayerInventory _inventory;
        private IntroSequence _intro;
        private GameObject _knocks;
        private Door _exitDoor;
        private Vector3 _doorway; // проём снимается до того, как дверь могут выбить и унести с петель
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
        private float _shake, _shakeTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaying() => IsPlaying = false;

        public void Initialize(IntroSequence intro, PlayerInventory inventory, GameObject knocks, Door exitDoor)
        {
            _intro = intro;
            _inventory = inventory;
            _knocks = knocks;
            _exitDoor = exitDoor;
            _doorway = exitDoor != null
                ? new Vector3(exitDoor.transform.position.x + 0.45f, Corner.y, exitDoor.transform.position.z)
                : CorridorWaypoint + Vector3.forward * 1.1f;
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

        // Сюжет ушёл от дома — пугалка больше не срабатывает.
        public void Retire() => _played = true;
        private void Disarm() => _nightTwo = false;

        private void Update()
        {
            if (!_nightTwo || _played || _camera == null || _inventory == null || !_inventory.hasFlashlight) return;
            if (Time.timeScale == 0f || RetroInventoryScreen.IsAnyOpen) return;
            if ((_player != null && (!_player.enabled || _player.isCameraLocked)) || PlayerInteraction.IsLockSequencePlaying) return;
            if (_intro != null && _intro.HoldsCornerScare) return; // M-04: сначала щиток
            Vector3 eye = _camera.transform.position;
            if (eye.y > 3.8f) return;                                        // игрок на втором этаже
            if (eye.z > Corner.z + 0.2f) return;                             // уже в коридоре — угол позади
            if (Flat(eye - Corner).magnitude > TriggerDistance) return;
            // Идёт к углу (с кухни на север, к входной двери), а не стоит и не пятится.
            if (_player != null && Vector3.Dot(_player.PlanarVelocity, Flat(Corner - eye).normalized + Vector3.forward) <= 0.3f) return;

            _played = true;
            HorrorMomentDirector.Instance?.Interrupt();
            _intro?.PrepareCornerScare();
            NarrativeDisplayQueue.InterruptAndEnqueue(this, Play);
        }

        private IEnumerator Play()
        {
            GameObject prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) yield break;
            CaptureControl();
            _beamWasOn = _beam != null && _beam.enabled;
            try
            {
                SilenceKnocks();
                Vector3 eye = _camera.transform.position;
                Vector3 face = FacePoint(eye);
                Quaternion toPlayer = Quaternion.LookRotation(Flat(eye - face));
                _monster = Instantiate(prefab, Hidden, toPlayer);
                _monster.name = "Corner Jumpscare";
                _monster.SetActive(true); // префаб хранится выключенным
                _rig = _monster.GetComponent<CornerStalkerRig>();
                if (_rig == null) _rig = _monster.AddComponent<CornerStalkerRig>();
                _rig.Lunge(0.25f, 0.6f); // кадр 1.6–1.8 с: рука тянется к лицу, голова прямо
                _rig.LookTarget = eye;
                _rig.LookWeight = 1f;
                Transform head = _rig.Head != null ? _rig.Head : _monster.transform;

                // 1. Бросок из-за угла прямо в лицо + удар звука.
                PlayJumpscare();
                Shake(2.2f);
                for (float t = 0f; t < LungeSeconds; t += Time.deltaTime)
                {
                    float k = 1f - (1f - t / LungeSeconds) * (1f - t / LungeSeconds); // резкий старт, торможение у лица
                    _monster.transform.position = k < 0.45f
                        ? Vector3.Lerp(Hidden, Mouth, k / 0.45f)
                        : Vector3.Lerp(Mouth, face, (k - 0.45f) / 0.55f);
                    _rig.LookTarget = _camera.transform.position;
                    FocusCamera(head.position, Mathf.Clamp01(t / 0.07f), 10f);
                    yield return null;
                }
                _monster.transform.position = face;

                // 2. Морда в морду: тряска, фонарик захлёбывается.
                for (float t = 0f; t < HoldSeconds; t += Time.deltaTime)
                {
                    _rig.LookTarget = _camera.transform.position;
                    FocusCamera(head.position, 1f, 10f);
                    if (_beam != null && _beamWasOn) _beam.enabled = t > 0.38f || Mathf.PerlinNoise(Time.time * 30f, 0.2f) > 0.35f;
                    yield return null;
                }
                if (_beam != null) _beam.enabled = _beamWasOn;

                // 3. Срывается в коридор и убегает через входную дверь.
                _rig.LookWeight = 0f;
                _rig.Run(1.3f);
                yield return RunToExit();

                // 4. Тишина, взгляд возвращается.
                yield return new WaitForSeconds(0.8f);
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
                    "Какого чёрта... Он был прямо передо мной!",
                    "Стук был из подвала. Он оттуда вылез?.."
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

        // Перед лицом игрока, но не в стене: луч от глаз упирается в стену — встаёт ближе.
        private static Vector3 FacePoint(Vector3 eye)
        {
            Vector3 forward = Flat(Camera.main != null ? Camera.main.transform.forward : Vector3.forward);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();
            float distance = FaceDistance;
            if (Physics.Raycast(new Vector3(eye.x, Corner.y + 1f, eye.z), forward, out RaycastHit hit, FaceDistance + 0.35f,
                    PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0.45f, hit.distance - 0.35f);
            Vector3 face = new Vector3(eye.x, Corner.y, eye.z) + forward * distance;
            return face;
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

        // Рывок взгляда к точке + тряска; zoom — на сколько градусов сужается FOV.
        private void FocusCamera(Vector3 target, float weight, float zoom)
        {
            Vector3 direction = target - _camera.transform.position;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion localAim = _camera.transform.parent != null
                ? Quaternion.Inverse(_camera.transform.parent.rotation) * Quaternion.LookRotation(direction)
                : Quaternion.LookRotation(direction);
            Quaternion offset = Quaternion.identity;
            if (_shake > 0f)
            {
                _shakeTime += Time.deltaTime;
                float a = _shake * Mathf.Exp(-_shakeTime * 5f);
                float n = _shakeTime * 32f;
                offset = Quaternion.Euler((Mathf.PerlinNoise(n, 0.3f) - 0.5f) * 2f * a, (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2f * a,
                    (Mathf.PerlinNoise(n, 0.9f) - 0.5f) * a);
                if (a < 0.02f) _shake = 0f;
            }
            _camera.transform.localRotation = Quaternion.Slerp(_cameraRotation, localAim, Mathf.Clamp01(weight)) * offset;
            _camera.fieldOfView = Mathf.Lerp(_cameraFov, Mathf.Max(35f, _cameraFov - zoom), Mathf.Clamp01(weight));
        }

        private void Shake(float degrees)
        {
            _shake = degrees;
            _shakeTime = 0f;
        }

        private void PlayJumpscare()
        {
            EnsureStinger();
            _stinger.volume = 0.95f;
            _stinger.clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (_stinger.clip != null) _stinger.Play();
            // A-01: низкий удар под скример — вес в теле, а не только громкость.
            AudioClip sub = GameSounds.Pick(GameSounds.Library?.subHits);
            if (sub != null) _stinger.PlayOneShot(sub, .8f);
            AudioClip gasp = GameSounds.Pick(GameSounds.Library?.breathScared);
            if (gasp != null) _stinger.PlayOneShot(gasp, .6f);
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

        // Бегом по коридору к двери (закрытую распахивает), монстр уходит в темноту двора. Взгляд провожает его.
        private IEnumerator RunToExit()
        {
            Transform body = _monster.transform;
            Vector3 doorway = _doorway;
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

            bool opened = false;
            Vector3[] path = { Mouth, CorridorWaypoint, doorway, outside }; // мимо края шкафа, а не сквозь
            foreach (Vector3 point in path)
            {
                while (_monster != null && Flat(point - body.position).magnitude > 0.05f)
                {
                    body.position = Vector3.MoveTowards(body.position, point, RunSpeed * Time.deltaTime);
                    Vector3 heading = Flat(point - body.position);
                    if (heading.sqrMagnitude > 0.0001f)
                        body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(heading), 1f - Mathf.Exp(-16f * Time.deltaTime));
                    if (!opened && Flat(doorway - body.position).magnitude < 0.9f)
                    {
                        opened = true;
                        PushDoorOpen();
                    }
                    FocusCamera(body.position + Vector3.up * 1.5f, 1f, 8f);
                    yield return null;
                }
            }
            if (!opened) PushDoorOpen();
            if (_monster != null) Destroy(_monster);
            _monster = null;
            if (_runEmitter != null)
            {
                _runEmitter.transform.SetParent(null, true);
                Destroy(_runEmitter, 2f);
                _runEmitter = null;
            }
        }

        private void PushDoorOpen()
        {
            if (_exitDoor == null || _exitDoor.IsDetached || _exitDoor.isOpen) return;
            _exitDoor.BurstOpen();
            GameState.frontDoorLocked = false;
        }

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
