using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{
    // B-07 «Стакан воды» — начало ночи 2, если засов не задвинут: монстр тихо вошёл и стоит на кухне, у стеклянной
    // двери во двор (со стороны выхода), подсвечен сзади уличным светом — силуэт. Голова всё время следит за игроком.
    // Один раз, когда игрок его видит, говорит «Don't move» (субтитр «Не двигайся») — больше ни слова. Ходить и бегать
    // можно; нужно взять стакан (кухонный остров), налить воды у раковины (E), выпить (F, только звук) и лечь в кровать →
    // блэкаут, дальше ночь 2 как обычно. Стакан — обычный физический предмет (PickupController): его можно уронить и поднять.
    // Смерть (→ E3): открыть дверь наружу (главная, гараж → двор, кухня → двор) или подойти к нему ближе ApproachDistance.
    public sealed class GlassOfWaterScene : MonoBehaviour
    {
        // Перед восточной створкой сдвижной двери кухни: стекло у него за спиной (стена начинается с x −42.8).
        public static readonly Vector3 MonsterSpot = new Vector3(-42.95f, 0.78f, 105.45f);
        private static readonly Vector3 MonsterFacing = new Vector3(-44.5f, 0.78f, 108.5f);  // смотрит в кухню, на проход
        private static readonly Vector3 GlassSpot = new Vector3(-43.85f, 1.79f, 108.45f);    // кухонный остров, дальний от монстра край
        private static readonly Vector3 SinkSpot = new Vector3(-45.6f, 1.8f, 105.27f);       // чаша раковины (столешница 1.78)
        private static readonly Vector3 FaucetSpot = new Vector3(-45.6f, 1.95f, 105.05f);
        private static readonly Vector3 BacklightSpot = new Vector3(-43.2f, 2.5f, 103.3f);   // на улице, за стеклом двери
        public const float ApproachDistance = 1.8f;
        private const float GroundFloorMaxY = 3f;
        private const float VoiceDistance = 7f;
        private const float FadeSeconds = 1.5f;
        public const string TakeQuestTag = "night-two-water";
        public const string FillQuestTag = "night-two-water-fill";
        public const string DrinkQuestTag = "night-two-water-drink";
        public const KeyCode DrinkKey = KeyCode.F;
        public const string BedQuestTag = "night-two-water-bed";

        private IntroSequence _intro;
        private PlayerController _player;
        private Transform _playerTransform;
        private Camera _camera;
        private Door _frontDoor;
        private NightTwoExits _exits;
        private Action<string[], Action> _showThoughts;
        private Action<string, int, UnityAction, string> _createQuest;
        private Action<EndingId> _endGame;

        private readonly List<Door> _outerDoors = new List<Door>();
        private GameObject _monster, _glass, _water, _sinkTarget, _backlight;
        private PickupController _pickup;
        private CornerStalkerRig _rig;
        private AudioSource _voice, _sink, _gulp;
        private AudioClip _voiceClip, _faucetClip, _gulpsClip, _stinger;
        private bool _voiced, _filling, _filled, _drinking, _drunk, _inBed, _dead, _watchersWere;

        public bool IsRunning { get; private set; }
        public bool MonsterInView
        {
            get
            {
                if (!IsRunning || _dead || _monster == null || _camera == null || _playerTransform == null ||
                    _playerTransform.position.y > GroundFloorMaxY) return false;
                Vector3 eye = _camera.transform.position;
                Vector3 head = _rig != null && _rig.Head != null ? _rig.Head.position : MonsterSpot + Vector3.up * 1.7f;
                Vector3 view = _camera.WorldToViewportPoint(head);
                if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f) return false;
                Vector3 delta = head - eye;
                float distance = delta.magnitude;
                return distance <= 0.3f || !Physics.Raycast(eye, delta / distance, distance - 0.3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
        }
        public bool Survived { get; private set; }
        public bool IsGlassHeld => _glass != null && _pickup != null && _pickup.GetHeldObject() == _glass;
        public bool CanFill => IsRunning && !_dead && !_filling && !_filled && IsGlassHeld && IsQuestActive(FillQuestTag);
        public bool CanDrink => IsRunning && !_dead && _filled && !_drinking && !_drunk && IsGlassHeld && IsQuestActive(DrinkQuestTag);
        private static bool IsQuestActive(string tag) => QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(tag);

        public void Initialize(IntroSequence intro, Transform playerTransform, Door frontDoor, NightTwoExits exits,
            Action<string[], Action> showThoughts, Action<string, int, UnityAction, string> createQuest,
            Action<EndingId> endGame)
        {
            _intro = intro;
            _playerTransform = playerTransform;
            _player = playerTransform != null ? playerTransform.GetComponent<PlayerController>() : null;
            _pickup = playerTransform != null ? playerTransform.GetComponentInChildren<PickupController>(true) : null;
            _frontDoor = frontDoor;
            _exits = exits;
            _showThoughts = showThoughts;
            _createQuest = createQuest;
            _endGame = endGame;
        }

        private void OnDisable()
        {
            GameEvents.OnBedTriggerReached -= OnBedReached;
            foreach (Door door in _outerDoors) if (door != null) door.Opened -= OnOuterDoorOpened;
            _outerDoors.Clear();
        }

        // Экран в начале чёрный (NightRoutine). Возвращается, когда игрок лёг (экран снова чёрный, Survived) или погиб
        // (смерть сама доводит до карточки E3 и повтора ночи 2 — дальше NightRoutine не идёт).
        public IEnumerator Play(CanvasGroup fade)
        {
            IsRunning = true;
            Survived = _voiced = _filling = _filled = _drinking = _drunk = _inBed = _dead = false;
            _camera = Camera.main;
            Setup();

            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                if (fade != null) fade.alpha = 1f - t / FadeSeconds;
                yield return null;
            }
            if (fade != null) fade.alpha = 0f;

            _showThoughts(new[] { "Пить хочется…", "Схожу вниз, на кухню, за водой." }, null);
            _createQuest("Взять стакан на кухне", 1, OnGlassTaken, TakeQuestTag);

            while (!_inBed && !_dead) yield return null;
            if (_dead) yield break;

            // Лёг — блэкаут.
            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                if (fade != null) fade.alpha = t / FadeSeconds;
                yield return null;
            }
            if (fade != null) fade.alpha = 1f;
            Cleanup();
            Survived = true;
            IsRunning = false;
        }

        private void Setup()
        {
            // Сцена всегда ночная, даже если в неё попали не через NightRoutine.
            SkyboxSwitcher sky = FindAnyObjectByType<SkyboxSwitcher>();
            if (sky != null) sky.isDayTime = false;
            RenderSettings.fog = false;
            LoadAudio();
            SpawnMonster();
            SpawnGlass();
            _backlight = new GameObject("Glass Of Water Backlight (B-07)");
            _backlight.transform.position = BacklightSpot;
            Light light = _backlight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.65f, 0.85f);
            light.intensity = 3f;
            light.range = 5.5f;
            light.shadows = LightShadows.None;

            // Выключатели работают как обычно: авария дома — после сцены, когда герой проснётся снова.
            LightSwitch.SetNightTwoPowerTrip(false);
            MonsterWatcherManager watchers = MonsterWatcherManager.Instance;
            if (watchers != null) { _watchersWere = watchers.ambientWatchersEnabled; watchers.ambientWatchersEnabled = false; }

            _exits?.SetBackDoorsNightLock(false);
            _outerDoors.Clear();
            if (_frontDoor != null) _outerDoors.Add(_frontDoor);
            if (_exits != null) foreach (Door door in _exits.BackDoors) if (door != null) _outerDoors.Add(door);
            foreach (Door door in _outerDoors)
            {
                door.CloseDoor();
                door.SetInteractionLocked(false);
                door.Opened += OnOuterDoorOpened;
            }
            GameEvents.OnBedTriggerReached += OnBedReached;
        }

        private void Cleanup()
        {
            GameEvents.OnBedTriggerReached -= OnBedReached;
            foreach (Door door in _outerDoors) if (door != null) door.Opened -= OnOuterDoorOpened;
            _outerDoors.Clear();
            _exits?.SetBackDoorsNightLock(true);
            if (_monster != null) Destroy(_monster);
            if (_glass != null)
            {
                if (IsGlassHeld) _pickup.ForceClearHeld();
                Destroy(_glass);
            }
            if (_sinkTarget != null) Destroy(_sinkTarget);
            if (_backlight != null) Destroy(_backlight);
            // Сон до утра стёр включённый игроком свет: дом снова тёмный, выключатель выбьет пробки, как обычно.
            foreach (LightSwitch lightSwitch in FindObjectsByType<LightSwitch>()) lightSwitch.ForceTurnOff();
            LightSwitch.SetNightTwoPowerTrip(true);
            MonsterWatcherManager watchers = MonsterWatcherManager.Instance;
            if (watchers != null) watchers.ambientWatchersEnabled = _watchersWere;
            QuestManager.Instance?.RecordQuestCompletion(BedQuestTag);
        }

        private void LoadAudio()
        {
            _voiceClip = Resources.Load<AudioClip>("Audio/GlassOfWater/DontMove");
            _faucetClip = Resources.Load<AudioClip>("Audio/GlassOfWater/FaucetFill");
            _gulpsClip = Resources.Load<AudioClip>("Audio/GlassOfWater/Gulps");
            _stinger = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            if (_gulp != null) return;
            _gulp = gameObject.AddComponent<AudioSource>();
            AudioRouting.Assign(_gulp, AudioBus.Voice);
            _gulp.playOnAwake = false;
            _gulp.spatialBlend = 0f;
        }

        private void SpawnMonster()
        {
            GameObject prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) return;
            _monster = Instantiate(prefab, MonsterSpot, Quaternion.LookRotation(Flat(MonsterFacing - MonsterSpot)));
            _monster.name = "Glass Of Water Monster (B-07)";
            _monster.SetActive(true); // префаб хранится выключенным
            foreach (Collider col in _monster.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (Rigidbody rb in _monster.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            _rig = _monster.GetComponent<CornerStalkerRig>();
            if (_rig == null) _rig = _monster.AddComponent<CornerStalkerRig>();
            _rig.Stand();
            _rig.LookWeight = 1f;
            _voice = HorrorMomentDirector.CreateSource(_monster.transform, "Glass Of Water Voice", 22f);
            _voice.minDistance = 2.5f;
            _voice.transform.localPosition = Vector3.up * 1.75f;
        }

        private void SpawnGlass()
        {
            GameObject prefab = Resources.Load<GameObject>("Props/WaterGlass");
            if (prefab == null)
            {
                Debug.LogWarning("[GlassOfWater] Нет Resources/Props/WaterGlass — стакан не появится.");
                return;
            }
            // Ставим на столешницу по лучу вниз: висящий в воздухе стакан выдаёт себя сразу.
            Vector3 spot = GlassSpot;
            if (Physics.Raycast(spot + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 0.8f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)) spot.y = hit.point.y;
            _glass = Instantiate(prefab, spot, Quaternion.identity);
            _glass.name = "Water Glass (B-07)";
            WaterGlassInteractable interactable = _glass.GetComponent<WaterGlassInteractable>();
            if (interactable == null) interactable = _glass.AddComponent<WaterGlassInteractable>();
            interactable.Bind(this);
            QuestItemMarker.Ensure(_glass).ConfigureCondition(() => IsRunning && !_dead && IsQuestActive(TakeQuestTag));
            Transform water = _glass.transform.Find("Water");
            _water = water != null ? water.gameObject : null;
            SetWater(0f);

            // Раковина: тонкая плашка над чашей — цель «E — налить воды» и место для квестовой метки.
            _sinkTarget = new GameObject("Kitchen Sink (B-07)");
            _sinkTarget.transform.position = SinkSpot;
            BoxCollider sinkCollider = _sinkTarget.AddComponent<BoxCollider>();
            sinkCollider.size = new Vector3(0.7f, 0.04f, 0.4f);
            _sinkTarget.AddComponent<KitchenSinkTarget>().Bind(this);
            QuestItemMarker.Ensure(_sinkTarget).ConfigureCondition(() => IsRunning && !_dead && !_filling && IsQuestActive(FillQuestTag));
            _sink = HorrorMomentDirector.CreateSource(_sinkTarget.transform, "Glass Of Water Faucet", 14f);
            AudioRouting.Assign(_sink, AudioBus.World);
            _sink.minDistance = 1.5f;
            _sink.transform.position = FaucetSpot;
        }

        // Видимая доля воды в стакане (0 — пустой).
        private void SetWater(float level)
        {
            if (_water == null) return;
            _water.SetActive(level > 0.01f);
            _water.transform.localScale = new Vector3(1f, Mathf.Clamp(level, 0.01f, 1f), 1f);
        }

        private void Update()
        {
            if (!IsRunning || _dead || _playerTransform == null) return;
            Vector3 eye = _camera != null ? _camera.transform.position : _playerTransform.position + Vector3.up * 1.6f;
            if (_rig != null) _rig.LookTarget = eye;
            if (_monster == null) return;
            if (IsTooClose(_playerTransform.position, MonsterSpot))
            {
                Kill();
                return;
            }
            if (!_voiced && SeesMonster(eye)) Speak();
            if (IsQuestActive(TakeQuestTag) && IsGlassHeld) QuestManager.Instance.AddProgress(1);
            if (CanDrink && Input.GetKeyDown(DrinkKey) && Time.timeScale > 0f && !PauseMenuController.InputBlocked &&
                !RetroInventoryScreen.IsAnyOpen && !QuestJournalScreen.IsAnyOpen) StartCoroutine(DrinkRoutine());
        }

        // Подсказка, пока стакан в руках (CrosshairJuice): у раковины — E, с водой — F.
        public string HeldHint(bool aimingAtSink)
        {
            if (CanDrink) return "F — выпить воды";
            if (CanFill) return aimingAtSink ? "E — налить воды" : "Подойдите к раковине";
            return null;
        }

        public static bool IsTooClose(Vector3 player, Vector3 monster)
        {
            if (player.y > GroundFloorMaxY) return false;
            float dx = player.x - monster.x, dz = player.z - monster.z;
            return dx * dx + dz * dz < ApproachDistance * ApproachDistance;
        }

        // Монстр в кадре, близко и ничем не заслонён.
        private bool SeesMonster(Vector3 eye)
        {
            if (_camera == null) return false;
            Vector3 head = _rig != null && _rig.Head != null ? _rig.Head.position : MonsterSpot + Vector3.up * 1.7f;
            Vector3 toHead = head - eye;
            float distance = toHead.magnitude;
            if (distance > VoiceDistance) return false;
            Vector3 view = _camera.WorldToViewportPoint(head);
            if (view.z <= 0f || view.x < 0.15f || view.x > 0.85f || view.y < 0.1f || view.y > 0.95f) return false;
            return !Physics.Raycast(eye, toHead / distance, distance - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private void Speak()
        {
            _voiced = true;
            ThoughtManager thoughts = ThoughtManager.Instance;
            if (thoughts == null)
            {
                PlayVoice();
                return;
            }
            thoughts.ShowDialogue("???", new[] { "Не двигайся." }, () =>
            {
                if (!_dead) _showThoughts(new[] { "Оно… говорит.", "Не подходить к нему. Взять воды — и обратно в кровать." }, null);
            }, PlayVoice);
        }

        private void PlayVoice()
        {
            if (_dead || _voice == null || _voiceClip == null) return;
            _voice.PlayOneShot(_voiceClip, 1f);
        }

        private void OnGlassTaken()
        {
            if (_dead) return;
            _createQuest("Налить воды", 1, OnGlassFilled, FillQuestTag);
        }

        // E у раковины со стаканом в руках: кран, стакан наполняется.
        public bool TryFill()
        {
            if (!CanFill) return false;
            StartCoroutine(FillRoutine());
            return true;
        }

        private IEnumerator FillRoutine()
        {
            _filling = true;
            float fill = _faucetClip != null ? _faucetClip.length : 2f;
            if (_sink != null && _faucetClip != null) _sink.PlayOneShot(_faucetClip, 0.8f);
            // Вода идёт между открытием и закрытием крана (в записи ~0.5 с с каждой стороны).
            for (float t = 0f; t < fill && !_dead; t += Time.deltaTime)
            {
                SetWater(Mathf.InverseLerp(0.5f, fill - 0.6f, t));
                yield return null;
            }
            if (_dead) yield break;
            SetWater(1f);
            _filling = false;
            _filled = true;
            if (IsQuestActive(FillQuestTag)) QuestManager.Instance.AddProgress(1);
        }

        private void OnGlassFilled()
        {
            if (_dead) return;
            _createQuest("Выпить воды", 1, OnWaterDrunk, DrinkQuestTag);
        }

        // F со стаканом воды в руках: только звук глотков, вода уходит.
        private IEnumerator DrinkRoutine()
        {
            _drinking = true;
            float length = _gulpsClip != null ? _gulpsClip.length : 1.5f;
            if (_gulpsClip != null) _gulp.PlayOneShot(_gulpsClip, 0.9f);
            for (float t = 0f; t < length && !_dead; t += Time.deltaTime)
            {
                SetWater(1f - t / length);
                yield return null;
            }
            if (_dead) yield break;
            SetWater(0f);
            _drinking = false;
            _drunk = true;
            if (IsQuestActive(DrinkQuestTag)) QuestManager.Instance.AddProgress(1);
        }

        private void OnWaterDrunk()
        {
            if (_dead) return;
            _showThoughts(new[] { "Холодная.", "А теперь наверх. Не оглядываться." }, null);
            _createQuest("Вернуться в кровать", 1, () => _inBed = true, BedQuestTag);
        }

        private void OnBedReached()
        {
            if (_dead || QuestManager.Instance == null || !QuestManager.Instance.IsQuestActive(BedQuestTag)) return;
            QuestManager.Instance.AddProgress(1);
        }

        private void OnOuterDoorOpened()
        {
            if (IsRunning && !_inBed) Kill();
        }

        // Нарушение: срывается с места, рывок к лицу, смерть.
        private void Kill()
        {
            if (_dead) return;
            _dead = true;
            StartCoroutine(KillRoutine());
        }

        private IEnumerator KillRoutine()
        {
            if (_player != null)
            {
                _player.StopMotion();
                _player.isCameraLocked = true;
                foreach (Behaviour input in new Behaviour[] { _player.GetComponent<PlayerInteraction>(),
                    _player.GetComponentInChildren<PlayerDoorOpen>(), _player.GetComponentInChildren<PickupController>(),
                    _player.GetComponentInChildren<HeadBobbing>(), _player.GetComponent<PlayerFloorKnock>() })
                    if (input != null) input.enabled = false;
            }
            ThoughtManager.Instance?.HideImmediately();
            if (_voice != null) _voice.Stop();
            Transform cam = _camera != null ? _camera.transform : null;
            Vector3 eye = cam != null ? cam.position : _playerTransform.position + Vector3.up * 1.6f;

            if (_monster != null && _rig != null)
            {
                // Далеко (дверь в другом конце дома) — он уже в паре шагов за спиной, со стороны кухни.
                Vector3 feet = _playerTransform.position;
                Vector3 toward = Flat(MonsterSpot - feet);
                if (toward.sqrMagnitude < 0.01f) toward = _playerTransform.forward;
                toward.Normalize();
                Vector3 from = Flat(MonsterSpot - feet).magnitude > 4.5f ? feet + toward * RushStart(eye, toward) : MonsterSpot;
                from.y = feet.y;
                Vector3 stop = feet + toward * 0.75f;
                stop.y = feet.y;
                if (_voice != null && _stinger != null) _voice.PlayOneShot(_stinger, 1f);
                _rig.StopMoving();
                _rig.Run(1.6f);
                Quaternion look0 = cam != null ? cam.rotation : Quaternion.identity;
                for (float t = 0f; t < 0.4f; t += Time.deltaTime)
                {
                    _monster.transform.position = Vector3.Lerp(from, stop, t / 0.4f);
                    _monster.transform.rotation = Quaternion.LookRotation(Flat(eye - _monster.transform.position));
                    _rig.LookTarget = eye;
                    Aim(cam, look0, t / 0.25f, 2f);
                    yield return null;
                }
                _rig.StopMoving();
                for (float t = 0f; t < 0.9f; t += Time.deltaTime)
                {
                    _rig.Lunge(Mathf.Lerp(0.15f, 0.6f, t / 0.9f), 0f);
                    Aim(cam, look0, 1f, Mathf.Lerp(2.5f, 0f, t / 0.9f));
                    yield return null;
                }
            }
            yield return DeathTapeEffect.Play(_intro != null ? _intro.fadeScreen : null);
            _endGame?.Invoke(EndingId.Hidden);
        }

        // Сколько места за спиной: стена ближе — встаёт у неё.
        private static float RushStart(Vector3 eye, Vector3 toward)
        {
            const float Wanted = 3.5f;
            return Physics.Raycast(eye, toward, out RaycastHit hit, Wanted, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(1.2f, hit.distance - 0.4f) : Wanted;
        }

        private void Aim(Transform cam, Quaternion from, float k, float shake)
        {
            if (cam == null || _monster == null) return;
            Vector3 target = _rig != null && _rig.Head != null ? _rig.Head.position : _monster.transform.position + Vector3.up * 1.7f;
            Quaternion jitter = Quaternion.Euler(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake), 0f);
            cam.rotation = Quaternion.Slerp(from, Quaternion.LookRotation(target - cam.position), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k))) * jitter;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
