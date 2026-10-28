using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Выходы развилки ночи 2 (B-02). До развилки задние двери на задний двор (гараж Door_009 и сдвижная дверь кухни)
    // ночью не открываются — герой не хочет выходить в темноту. После пряток (Open) они открыты, и игрок решает ногами:
    // задний двор → onBackyard (E3), дорога в RoadDistance от люка → onRoad (E4). Границы — по полу участка, без триггеров
    // в сцене: дом x −47…−33, z 104.7…120; задний двор — за порогом задних дверей; асфальт дороги — с z 128.6.
    public sealed class NightTwoExits : MonoBehaviour
    {
        private static readonly Vector3 GarageBackDoor = new Vector3(-38.04f, 1.55f, 104.71f);
        private static readonly Vector3 KitchenBackDoor = new Vector3(-43.39f, 2.10f, 104.71f);
        private const float DoorMatchDistance = 0.6f;
        private const float BackyardZ = 104f;          // задние двери на z 104.71: шаг за порог — уже двор
        private const float PlotSouthZ = 90.5f;
        private const float PlotWestX = -50.1f, PlotEastX = -29.6f;
        private const float GroundFloorMaxY = 3f;       // второй этаж (пол 4.1) — не двор
        private const float RoadZ = 128.5f;
        public const float RoadDistance = 10f;
        private const float ThoughtCooldown = 5f;

        private static readonly string[] DarkOutside = { "Я не хочу выходить из дома, пока там темно." };

        private Transform _player;
        private Vector3 _hatch;
        private Action _onBackyard, _onRoad;
        private Door[] _backDoors = Array.Empty<Door>();
        private bool _locked, _watching;
        private bool _enteredBackyard;
        private Vector3 _backyardEntry;
        private GameObject _escapeMonster;
        private BasementHatch _basementHatch;
        private GameObject _monsterPrefab;
        private bool _missingPrefabLogged;
        private bool _liftingPlayer, _playingDeathTape;
        private Vector3 _playerBeforeLift;
        private Transform _ambushCamera;
        private Vector3 _cameraBeforePull;
        private Quaternion _cameraRotationBeforePull;
        private readonly RaycastHit[] _groundHits = new RaycastHit[16];

        public void Initialize(Transform player, Vector3 hatch, Action onBackyard, Action onRoad)
        {
            _player = player;
            _hatch = hatch;
            _onBackyard = onBackyard;
            _onRoad = onRoad;
            Door garage = null, kitchen = null;
            foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (garage == null && Vector3.Distance(door.transform.position, GarageBackDoor) < DoorMatchDistance) garage = door;
                else if (kitchen == null && Vector3.Distance(door.transform.position, KitchenBackDoor) < DoorMatchDistance) kitchen = door;
            }
            if (garage == null || kitchen == null)
                Debug.LogWarning("[NightTwoExits] Не найдена задняя дверь: " + (garage == null ? "гараж " : "") + (kitchen == null ? "кухня" : ""));
            _backDoors = garage != null && kitchen != null ? new[] { garage, kitchen }
                : garage != null ? new[] { garage } : kitchen != null ? new[] { kitchen } : Array.Empty<Door>();
            foreach (Door door in _backDoors) door.LockedAttempted += OnLockedAttempt;
        }

        public void SetHatch(BasementHatch hatch) => _basementHatch = hatch;

        private void OnEnable()
        {
            GameEvents.OnNightTwoStarted += LockBackDoors;
            GameEvents.OnDayStarted += Release;
        }

        private void OnDisable()
        {
            if (_playingDeathTape) DeathTapeEffect.Cancel();
            if (_liftingPlayer && _player != null) _player.position = _playerBeforeLift;
            if (_ambushCamera != null)
            {
                _ambushCamera.localPosition = _cameraBeforePull;
                _ambushCamera.localRotation = _cameraRotationBeforePull;
                _ambushCamera = null;
            }
            _liftingPlayer = false;
            GameEvents.OnNightTwoStarted -= LockBackDoors;
            GameEvents.OnDayStarted -= Release;
            if (_escapeMonster != null) Destroy(_escapeMonster);
        }

        private void OnDestroy()
        {
            foreach (Door door in _backDoors)
                if (door != null) door.LockedAttempted -= OnLockedAttempt;
        }

        // Развилка: задние двери открываются, следим, куда ушёл игрок. false — концовка уже выбрана.
        public void SetOpen(bool open)
        {
            _watching = open;
            _enteredBackyard = false;
            if (open) SetBackDoorsLocked(false);
        }

        // B-07 «Стакан воды»: на время сцены задние двери открываются — открыть их значит умереть (следит сама сцена).
        public System.Collections.Generic.IReadOnlyList<Door> BackDoors => _backDoors;
        public void SetBackDoorsNightLock(bool locked) => SetBackDoorsLocked(locked);

        private void LockBackDoors()
        {
            _watching = false;
            SetBackDoorsLocked(true);
        }

        private void Release()
        {
            _watching = false;
            SetBackDoorsLocked(false);
        }

        private void SetBackDoorsLocked(bool locked)
        {
            if (_locked == locked) return;
            _locked = locked;
            foreach (Door door in _backDoors)
                if (door != null) door.SetInteractionLocked(locked);
        }

        private void OnLockedAttempt()
        {
            if (_locked) ThoughtManager.Instance?.TryShowInteractionThoughts("back-door-dark", DarkOutside, ThoughtCooldown);
        }

        private void Update()
        {
            if (!_watching || _player == null || PauseMenuController.InputBlocked || Time.timeScale <= 0f) return;
            Vector3 position = _player.position;
            if (IsInBackyard(position))
            {
                if (!_enteredBackyard)
                {
                    _enteredBackyard = true;
                    _backyardEntry = position;
                }
                else if (BackyardWalked(_backyardEntry, position) &&
                    (position.x < _backyardEntry.x || (position.x >= -34.3f && FrontPathClear(position))))
                {
                    _watching = false;
                    _onBackyard?.Invoke();
                }
            }
            else if (IsOnRoad(position, _hatch))
            {
                _watching = false;
                _onRoad?.Invoke();
            }
            else _enteredBackyard = false;
        }

        public static bool IsInBackyard(Vector3 position) =>
            position.y < GroundFloorMaxY && position.z < BackyardZ && position.z > PlotSouthZ &&
            position.x > PlotWestX && position.x < PlotEastX;

        public static bool BackyardWalked(Vector3 entry, Vector3 position)
        {
            return Mathf.Abs(position.x - entry.x) >= 2.5f;
        }

        private Vector3 FrontAmbushStart(Vector3 playerPosition) =>
            new Vector3(-31.8f, playerPosition.y, Mathf.Min(playerPosition.z, 100.1f));

        private bool FrontPathClear(Vector3 playerPosition)
        {
            Vector3 start = FrontAmbushStart(playerPosition);
            Vector3 end = playerPosition;
            if (!Physics.Linecast(start, end, out RaycastHit hit, PhysicsMasks.AllLayers,
                    QueryTriggerInteraction.Ignore)) return true;
            return hit.collider != null && hit.collider.transform.IsChildOf(_player);
        }

        public bool CanBeginExit()
        {
            if (_monsterPrefab == null) _monsterPrefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (_monsterPrefab != null && _enteredBackyard && _player != null && IsInBackyard(_player.position))
            {
                var animator = _monsterPrefab.GetComponentInChildren<Animator>(true);
                if (Resources.Load<AnimationClip>("Animations/Monster/AmbushBehind") == null ||
                    _monsterPrefab.GetComponent<CornerStalkerRig>() == null || animator == null ||
                    animator.avatar == null || !animator.avatar.isHuman)
                {
                    if (!_missingPrefabLogged) Debug.LogError("[NightTwoExits] Missing B-03 ambush animation or humanoid monster rig.");
                    _missingPrefabLogged = true;
                    return false;
                }
            }
            if (_monsterPrefab != null) return true;
            if (!_missingPrefabLogged) Debug.LogError("[NightTwoExits] Missing Scares/CornerPeekMonster prefab; exit sequence unavailable.");
            _missingPrefabLogged = true;
            return false;
        }

        public void BeginExit(EndingId exit, Action<EndingId> finished)
        {
            StartCoroutine(exit == EndingId.Hidden ? Ambush(finished) : Chase(finished));
        }

        private GameObject SpawnMonster(Vector3 position, Vector3 look)
        {
            var prefab = _monsterPrefab;
            if (prefab == null)
            {
                Debug.LogError("[NightTwoExits] Missing Scares/CornerPeekMonster prefab");
                return null;
            }
            look.y = 0f;
            if (look.sqrMagnitude < 0.001f) look = Vector3.forward;
            _escapeMonster = Instantiate(prefab, position, Quaternion.LookRotation(look));
            _escapeMonster.SetActive(true);
            foreach (Collider c in _escapeMonster.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (Rigidbody body in _escapeMonster.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            return _escapeMonster;
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private float GroundY(Vector3 point, float fallbackY)
        {
            // Люк и триггеры не относятся к опорной поверхности монстра.
            Vector3 origin = new Vector3(point.x, Mathf.Max(point.y + 3f, _hatch.y + 5f), point.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, 20f,
                PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity, result = fallbackY;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null || hit.normal.y < 0.5f || hit.distance >= distance) continue;
                Transform owner = hit.collider.transform;
                if ((_basementHatch != null && owner.IsChildOf(_basementHatch.transform)) ||
                    (_player != null && owner.IsChildOf(_player)) ||
                    (_escapeMonster != null && owner.IsChildOf(_escapeMonster.transform))) continue;
                distance = hit.distance;
                result = hit.point.y;
            }
            return result;
        }

        private float AmbushGroundY(Vector3 point, float playerY)
        {
            Vector3 origin = new Vector3(point.x, playerY + 0.35f, point.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, 2f,
                PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity, result = playerY - 0.9f;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.collider == null || hit.normal.y < 0.5f || hit.distance >= distance) continue;
                Transform owner = hit.collider.transform;
                if ((_basementHatch != null && owner.IsChildOf(_basementHatch.transform)) ||
                    (_player != null && owner.IsChildOf(_player)) ||
                    (_escapeMonster != null && owner.IsChildOf(_escapeMonster.transform))) continue;
                distance = hit.distance;
                result = hit.point.y;
            }
            return result;
        }

        private IEnumerator Chase(Action<EndingId> finished)
        {
            Vector3 start = _hatch;
            start.y = GroundY(start + Vector3.forward * 1.8f, _hatch.y);
            _basementHatch?.OpenForCinematic();
            var monster = SpawnMonster(start - Vector3.up * 1.5f, _player.position - start);
            if (monster == null) yield break;
            var rig = monster.GetComponent<CornerStalkerRig>();
            if (rig != null) rig.Lunge(0.25f, 0.6f);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                if (monster == null) yield break;
                monster.transform.position = Vector3.Lerp(start - Vector3.up * 1.5f, start, t);
                yield return null;
            }
            monster.transform.position = start;
            if (rig != null) rig.StopMoving();
            var playerController = _player.GetComponent<PlayerController>();
            var view = _player.GetComponentInChildren<Camera>();
            float chaseSpeed = 2f * (playerController != null ? playerController.runSpeed : 4f);
            if (rig != null) rig.Run(1.6f);
            var sound = AudioRouting.Create(monster.transform, "Escape footsteps", AudioBus.Horror, 1f, 30f);
            sound.transform.localPosition = Vector3.up;
            sound.volume = 0.9f;
            sound.pitch = 0.8f;
            var attack = AudioRouting.Create(monster.transform, "Escape attack", AudioBus.Horror, 1f, 30f);
            attack.clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (attack.clip != null) attack.Play();
            var library = GameSounds.Library;
            var steps = library != null ? library.stepConcrete : null;
            int stepIndex = 0;
            float stepIn = 0f;
            var lookStop = new ChaseLookStop();
            bool audioPaused = false;

            float elapsed = 0f;
            float groundSampleIn = 0f, groundY = start.y;
            Vector3 roadEntry = _player.position;
            while (monster != null && _player != null)
            {
                if (PauseMenuController.InputBlocked || Time.timeScale <= 0f)
                {
                    if (rig != null) rig.SetChaseFrozen(true);
                    sound.Stop();
                    if (!audioPaused) { attack.Pause(); audioPaused = true; }
                    yield return null;
                    continue;
                }
                if (audioPaused) { attack.UnPause(); audioPaused = false; }
                {
                    float dt = Time.deltaTime;
                    elapsed += dt;
                    bool wasFrozen = lookStop.Frozen;
                    bool visible = !lookStop.Used && ChaseMonsterVisible(view, monster, rig);
                    lookStop.Tick(visible, dt);
                    if (rig != null)
                    {
                        rig.SetChaseFrozen(lookStop.Frozen);
                        rig.LookTarget = view != null ? view.transform.position : _player.position + Vector3.up;
                    }
                    if (lookStop.Frozen)
                    {
                        sound.Stop();
                        attack.Stop();
                        if (ChaseEscaped(roadEntry, _player.position, elapsed)) break;
                        yield return null;
                        continue;
                    }
                    if (wasFrozen && attack.clip != null) attack.Play();
                    stepIn -= dt;
                    if (stepIn <= 0f && steps != null && steps.Length > 0)
                    {
                        var clip = steps[stepIndex++ % steps.Length];
                        if (clip != null) sound.PlayOneShot(clip);
                        stepIn = 0.22f;
                    }
                    if (_player.position.z < RoadZ - 1f || _player.position.y >= GroundFloorMaxY)
                    {
                        Vector3 attackPosition = _player.position + Flat(_player.forward).normalized * 2f;
                        attackPosition.y = GroundY(attackPosition, monster.transform.position.y);
                        monster.transform.position = attackPosition;
                        sound.Stop();
                        attack.Stop();
                        yield return Jumpscare(monster);
                        finished(EndingId.Hidden);
                        yield break;
                    }
                    if (ChaseEscaped(roadEntry, _player.position, elapsed))
                        break;
                    Vector3 target = _player.position;
                    target.y = monster.transform.position.y;
                    Vector3 direction = Flat(target - monster.transform.position);
                    if (direction.magnitude < 1.5f)
                    {
                        sound.Stop();
                        attack.Stop();
                        yield return Jumpscare(monster);
                        finished(EndingId.Hidden);
                        yield break;
                    }
                    monster.transform.rotation = Quaternion.Slerp(monster.transform.rotation,
                        Quaternion.LookRotation(direction), 1f - Mathf.Exp(-8f * dt));
                    Vector3 next = Vector3.MoveTowards(monster.transform.position, target, chaseSpeed * dt);
                    groundSampleIn -= dt;
                    if (groundSampleIn <= 0f)
                    {
                        groundY = GroundY(next, monster.transform.position.y);
                        groundSampleIn = 0.1f;
                    }
                    next.y = Mathf.MoveTowards(monster.transform.position.y, groundY, 2f * dt);
                    monster.transform.position = next;
                }
                yield return null;
            }
            if (monster != null) Destroy(monster);
            _escapeMonster = null;
            finished(EndingId.Escaped);
        }

        public static bool ChaseEscaped(Vector3 start, Vector3 position, float elapsed) =>
            position.z >= RoadZ && position.y < GroundFloorMaxY && Mathf.Abs(position.x - start.x) >= 15f;

        public struct ChaseLookStop
        {
            public bool Used { get; private set; }
            public bool Frozen => Remaining > 0f;
            public float Remaining { get; private set; }

            public void Tick(bool visible, float deltaTime)
            {
                if (deltaTime <= 0f) return;
                if (!Used && visible)
                {
                    Used = true;
                    Remaining = 5f;
                }
                else if (Frozen) Remaining = Mathf.Max(0f, Remaining - deltaTime);
            }
        }

        private bool ChaseMonsterVisible(Camera view, GameObject monster, CornerStalkerRig rig)
        {
            if (view == null) return false;
            Vector3 point = rig != null && rig.Head != null ? rig.Head.position : monster.transform.position + Vector3.up;
            Vector3 screen = view.WorldToViewportPoint(point);
            if (screen.z <= 0f || screen.x < 0f || screen.x > 1f || screen.y < 0f || screen.y > 1f) return false;
            if (!Physics.Linecast(view.transform.position, point, out RaycastHit hit,
                    PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.IsChildOf(monster.transform);
        }

        private IEnumerator Ambush(Action<EndingId> finished)
        {
            Vector3 playerPosition = _player.position;
            bool behind = playerPosition.x < _backyardEntry.x;
            Vector3 direction = behind ? Vector3.left : Vector3.right;
            var player = _player.GetComponent<PlayerController>();
            var camera = _player.GetComponentInChildren<Camera>();
            Vector3 cameraForward = camera != null ? Flat(camera.transform.forward) : Flat(_player.forward);
            if (cameraForward.sqrMagnitude < 0.001f) cameraForward = direction;
            cameraForward.Normalize();
            float behindDistance = 1.8f;
            if (behind && Physics.Raycast(playerPosition, -cameraForward, out RaycastHit obstacle, behindDistance,
                    PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore) &&
                !obstacle.collider.transform.IsChildOf(_player))
                behindDistance = Mathf.Max(0.5f, obstacle.distance - 0.2f);
            Vector3 start = behind ? playerPosition - cameraForward * behindDistance : FrontAmbushStart(playerPosition);
            start.y = AmbushGroundY(start, playerPosition.y);
            var monster = SpawnMonster(start, playerPosition - start);
            if (monster == null) yield break;
            var rig = monster.GetComponent<CornerStalkerRig>();
            if (player != null) { player.StopMotion(); player.isCameraLocked = true; }
            if (rig == null) { Debug.LogError("[B-03] Missing CornerStalkerRig."); yield break; }

            if (!behind)
            {
                // Мгновенная атака: без предварительной пробежки перед разворотом камеры.
                Vector3 attackStart = playerPosition + direction * 1.05f;
                attackStart.y = AmbushGroundY(attackStart, playerPosition.y);
                monster.transform.position = attackStart;
                monster.transform.rotation = Quaternion.LookRotation(Flat(playerPosition - attackStart));
            }

            if (!rig.PlayAmbush(true)) yield break;
            var audio = AudioRouting.Create(monster.transform, "Backyard ambush", AudioBus.Horror, 0f);
            audio.clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (audio.clip != null) audio.Play();
            Vector3 from = monster.transform.position;
            Vector3 endPosition = playerPosition - Flat(playerPosition - from).normalized * 0.7f;
            endPosition.y = from.y;
            Quaternion startRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            if (camera != null)
            {
                _ambushCamera = camera.transform;
                _cameraBeforePull = _ambushCamera.localPosition;
                _cameraRotationBeforePull = _ambushCamera.localRotation;
            }
            var animator = monster.GetComponentInChildren<Animator>();
            Transform leftHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            Transform rightHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            float initialHandsY = 0f, lift = 0f;
            bool sampledHands = false;
            _playerBeforeLift = playerPosition;
            _liftingPlayer = true;
            for (float t = 0f; t < 1.05f; t += Time.deltaTime)
            {
                if (monster == null || _player == null) yield break;
                monster.transform.position = Vector3.Lerp(from, endPosition, Mathf.SmoothStep(0f, 1f, t / 0.16f));
                Vector3 look = Flat(playerPosition - monster.transform.position);
                if (look.sqrMagnitude > 0.001f) monster.transform.rotation = Quaternion.LookRotation(look);
                if (leftHand != null && rightHand != null)
                {
                    float handsY = (leftHand.position.y + rightHand.position.y) * 0.5f;
                    if (!sampledHands) { initialHandsY = handsY; sampledHands = true; }
                    // Захват поднимает героя вслед за кистями; опускание рук не роняет камеру.
                    lift = Mathf.Max(lift, Mathf.Clamp(handsY - initialHandsY, 0f, 1.1f));
                    _player.position = playerPosition + Vector3.up * lift;
                }
                if (camera != null && rig.Head != null)
                    camera.transform.rotation = Quaternion.Slerp(startRotation,
                        Quaternion.LookRotation(rig.Head.position - camera.transform.position), Mathf.Clamp01(t / 0.06f));
                yield return null;
            }
            if (camera != null && rig.Head != null)
            {
                rig.SetAmbushSpeed(0f);
                yield return new WaitForSeconds(0.2f);
                if (monster == null || _player == null || camera == null) yield break;

                Vector3 pullFrom = camera.transform.position;
                Quaternion lookFrom = camera.transform.rotation;
                float faceDistance = Mathf.Max(0.42f, camera.nearClipPlane + 0.25f);
                rig.SetAmbushSpeed(2f);
                rig.LookWeight = 1f;
                // Короткое замирание сменяется рывком к уровню глаз, затем VHS фиксирует крупный план.
                for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    if (monster == null || camera == null || rig.Head == null) yield break;
                    float pull = Mathf.Clamp01(t / 0.09f);
                    Vector3 target = rig.Head.position + monster.transform.forward * faceDistance;
                    camera.transform.position = Vector3.Lerp(pullFrom, target, pull * pull);
                    rig.LookTarget = camera.transform.position;
                    rig.HeadRoll = Mathf.Lerp(0f, -18f, Mathf.Clamp01(t / 0.07f));
                    camera.transform.rotation = Quaternion.Slerp(lookFrom,
                        Quaternion.LookRotation(rig.Head.position - camera.transform.position), pull);
                    yield return null;
                }
            }
            yield return PlayDeathTape();
            finished(EndingId.Hidden);
        }

        private IEnumerator PlayDeathTape()
        {
            _playingDeathTape = true;
            try { yield return DeathTapeEffect.Play(GetComponent<IntroSequence>()?.fadeScreen); }
            finally { _playingDeathTape = false; }
        }

        private IEnumerator Jumpscare(GameObject monster)
        {
            var player = _player.GetComponent<PlayerController>();
            var camera = _player.GetComponentInChildren<Camera>();
            if (player != null) { player.StopMotion(); player.isCameraLocked = true; }
            var rig = monster.GetComponent<CornerStalkerRig>();
            if (rig != null) { rig.StopMoving(); rig.Lunge(0.25f, 0.6f); }
            var audio = AudioRouting.Create(monster.transform, "Escape scare", AudioBus.Horror, 0f);
            audio.clip = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (audio.clip != null) audio.Play();
            Vector3 from = monster.transform.position;
            Vector3 direction = Flat(_player.position - from).normalized;
            Vector3 target = _player.position - direction * 0.65f;
            target.y = from.y;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                if (monster == null || _player == null) yield break;
                monster.transform.position = Vector3.Lerp(from, target, Mathf.Clamp01(t / 0.16f));
                if (camera != null)
                {
                    Vector3 head = rig != null && rig.Head != null ? rig.Head.position : monster.transform.position + Vector3.up * 1.5f;
                    Vector3 look = head - camera.transform.position;
                    if (look.sqrMagnitude > 0.001f) camera.transform.rotation = Quaternion.LookRotation(look);
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.65f);
            yield return PlayDeathTape();
        }

        public static bool IsOnRoad(Vector3 position, Vector3 hatch)
        {
            if (position.z < RoadZ) return false;
            float dx = position.x - hatch.x, dz = position.z - hatch.z;
            return dx * dx + dz * dz >= RoadDistance * RoadDistance;
        }
    }
}
