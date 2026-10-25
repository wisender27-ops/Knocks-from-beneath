using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class CornerPeekSequence : MonoBehaviour
    {
        private PlayerInventory _inventory;
        private IntroSequence _intro;
        private GameObject _knocks;
        private Door _exitDoor;
        private Transform _corner;
        private Camera _camera;
        private bool _nightTwo, _played;
        private GameObject _monster, _runEmitter;
        private Vector3 _hidden, _shown, _headTarget;
        private Coroutine _sequence;
        private Transform _head;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaying() => IsPlaying = false;

        public void Initialize(IntroSequence intro, PlayerInventory inventory, GameObject knocks, Door exitDoor)
        {
            _intro = intro;
            _inventory = inventory;
            _knocks = knocks;
            _exitDoor = exitDoor;
            _corner = GameObject.Find("closet_door_02")?.transform;
            _camera = Camera.main;
            _player = inventory != null ? inventory.GetComponentInParent<PlayerController>() : null;
            if (_corner == null) { enabled = false; return; }
            // Keep the body behind the closet edge; only the leaning head/shoulder clear it.
            Vector3 edge = _corner.position;
            _hidden = new Vector3(edge.x + 0.5f, 0.78f, edge.z - 0.3f);
            _shown = new Vector3(edge.x + 0.15f, 0.78f, edge.z + 0.27f);
            _headTarget = new Vector3(edge.x - 0.16f, 2.25f, edge.z + 0.08f);
        }

        private void OnEnable()
        {
            GameEvents.OnNightTwoStarted += Arm;
            GameEvents.OnDayStarted += Disarm;
        }

        private void Arm() => _nightTwo = true;

        // Сюжет прошёл фазу кухни — пугалка больше не срабатывает.
        public void Retire() => _played = true;
        private void Disarm() => _nightTwo = false;

        private void Update()
        {
            if (!_nightTwo || _played || _camera == null || _inventory == null || !_inventory.hasFlashlight) return;
            if (Time.timeScale == 0f || RetroInventoryScreen.IsAnyOpen) return;
            if ((_player != null && (!_player.enabled || _player.isCameraLocked)) || PlayerInteraction.IsLockSequencePlaying) return;
            Vector3 view = _camera.WorldToViewportPoint(_headTarget);
            if (_camera.transform.position.y > 3.8f || view.z <= 0f || view.z > 3f ||
                view.x < 0.12f || view.x > 0.88f || view.y < 0.12f || view.y > 0.88f) return;
            Vector3 direction = _headTarget - _camera.transform.position;
            Vector3 approach = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            if (_player != null && Vector3.Dot(_player.PlanarVelocity, approach) <= 0.1f) return;
            if (Physics.Raycast(_camera.transform.position, direction.normalized, direction.magnitude - 0.08f,
                PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore)) return;
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
            try
            {
            _monster = Instantiate(prefab, _hidden, Quaternion.Euler(0f, 180f, 0f));
            _monster.SetActive(true);
            Animator animator = _monster.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Play("Observe", 0, 0.25f);
                animator.Update(0f);
                if (animator.isHuman) _head = animator.GetBoneTransform(HumanBodyBones.Head);
                animator.enabled = false;
            }
            CornerPeekPose pose = _monster.AddComponent<CornerPeekPose>();
            pose.Initialize(animator);
            float time = 0f;
            const float emerge = 0.22f;
            bool startled = false;
            while (time < emerge)
            {
                time += Time.deltaTime;
                float reveal = Mathf.SmoothStep(0f, 1f, time / emerge);
                _monster.transform.position = Vector3.Lerp(_hidden, _shown, reveal);
                ApplyPose(pose, reveal, time);
                if (!startled && reveal >= 0.25f)
                {
                    startled = true;
                    PlayJumpscare();
                }
                if (startled) FocusCamera(Mathf.Clamp01((time - 0.04f) / 0.14f));
                yield return null;
            }
            time = 0f;
            while (time < 0.25f)
            {
                time += Time.deltaTime;
                ApplyPose(pose, 1f, emerge + time);
                FocusCamera(1f);
                yield return null;
            }
            time = 0f;
            while (time < 0.14f)
            {
                time += Time.deltaTime;
                float retreat = Mathf.Clamp01(time / 0.14f);
                float reveal = 1f - retreat * retreat;
                _monster.transform.position = Vector3.Lerp(_hidden, _shown, reveal);
                ApplyPose(pose, reveal, emerge + 0.25f + time);
                yield return null;
            }
            Destroy(_monster);
            _monster = null;
            if (_knocks != null)
                foreach (RandomKnock knock in _knocks.GetComponentsInChildren<RandomKnock>(true))
                    knock.SilencePermanently();
            yield return RunToExit();
            yield return new WaitForSeconds(1.1f);
            float release = 0f;
            Quaternion focusRotation = _camera.transform.localRotation;
            float focusFov = _camera.fieldOfView;
            while (release < 0.25f)
            {
                release += Time.deltaTime;
                float weight = Mathf.SmoothStep(0f, 1f, release / 0.25f);
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
            _sequence = null;
            }
            finally
            {
                RestoreControl();
                if (_monster != null) Destroy(_monster);
                if (_runEmitter != null) Destroy(_runEmitter);
            }
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

        private void FocusCamera(float weight)
        {
            Vector3 target = _head != null ? _head.position : _headTarget;
            Vector3 direction = target - _camera.transform.position;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion localAim = _camera.transform.parent != null
                ? Quaternion.Inverse(_camera.transform.parent.rotation) * Quaternion.LookRotation(direction)
                : Quaternion.LookRotation(direction);
            _camera.transform.localRotation = Quaternion.Slerp(_cameraRotation, localAim, Mathf.SmoothStep(0f, 1f, weight));
            _camera.fieldOfView = Mathf.Lerp(_cameraFov, Mathf.Max(35f, _cameraFov - 18f), weight);
        }

        private void PlayJumpscare()
        {
            _stinger = gameObject.AddComponent<AudioSource>();
            _stinger.playOnAwake = false;
            _stinger.spatialBlend = 0f;
            _stinger.volume = 0.85f;
            _stinger.clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (_stinger.clip != null) _stinger.Play();
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

        private void ApplyPose(CornerPeekPose pose, float reveal, float time)
        {
            pose.Apply(reveal, time);
            if (_head == null || _camera == null) return;
            Vector3 direction = _camera.transform.position - _head.position;
            if (direction.sqrMagnitude < 0.001f) return;
            Quaternion aim = Quaternion.FromToRotation(_head.forward, direction.normalized) * _head.rotation;
            _head.rotation = Quaternion.Slerp(_head.rotation, aim, 0.8f * reveal);
        }

        private IEnumerator RunToExit()
        {
            AudioClip run = Resources.Load<AudioClip>("Audio/CornerEscapeRun");
            _runEmitter = new GameObject("CornerEscapeAudio");
            _runEmitter.transform.position = _headTarget - Vector3.up;
            AudioSource source = _runEmitter.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 18f;
            source.dopplerLevel = 0f;
            source.volume = 0.9f;
            Vector3 start = _runEmitter.transform.position;
            Vector3 end = _exitDoor != null ? _exitDoor.transform.position : start + Vector3.forward * 3f;
            if (run != null)
            {
                source.clip = run;
                source.Play();
                while (isActiveAndEnabled && source != null && source.isPlaying)
                {
                    _runEmitter.transform.position = Vector3.Lerp(start, end, source.time / run.length);
                    yield return null;
                }
            }
            if (!isActiveAndEnabled || _runEmitter == null) yield break;
            _runEmitter.transform.position = end;
            if (_exitDoor != null)
            {
                _exitDoor.BreakOffHinges(Vector3.forward, Resources.Load<AudioClip>("Audio/CornerDoorCrack"),
                    Resources.Load<AudioClip>("Audio/CornerDoorLanding"));
                GameState.frontDoorLocked = false;
            }
            Destroy(_runEmitter);
            _runEmitter = null;
        }

        private void OnDisable()
        {
            RestoreControl();
            GameEvents.OnNightTwoStarted -= Arm;
            GameEvents.OnDayStarted -= Disarm;
            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = null;
            if (_monster != null) Destroy(_monster);
            if (_runEmitter != null) Destroy(_runEmitter);
        }
    }
}
