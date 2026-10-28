using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Мини-игра M7 «Дыхание» — сцена в спальне героя наверху сразу после того, как он взял молоток. Снизу скрипит
    // люк, свет гаснет, тяжёлые шаги идут к лестнице. Игрок прячется под свою кровать (E у кровати): камера у пола,
    // видна только щель между покрывалом и полом. Монстр поднимается, обходит комнату и останавливается у кровати —
    // в щели его ступни прямо перед лицом (единственный раз, когда его видно). Игрок должен молчать по-настоящему:
    // игра слушает микрофон (VoiceNoiseGate, шумоподавление по фону комнаты; шёпот можно, голос нет), на экране —
    // «не говори ни слова» и шкала громкости. Без микрофона — запасной вариант: Пробел/ПКМ — задержать дыхание
    // (BreathHold). Нашёл — резко падает на колено и заглядывает под кровать (E3). Не успел спрятаться — бросок (E3).
    // Переждал — шаги уходят вниз, хлопает люк, герой вылезает.
    public sealed class HidingBreathGame : MonoBehaviour, IHidingSearch
    {
        public enum Outcome { None, Found, Left, CaughtInOpen }

        // Спальня героя (правая наверху): кровать bed_03, стол с молотком, лестница с первого этажа.
        private const float Floor = 4.1f;
        private static readonly Vector3 BedMin = new Vector3(-43.08f, Floor, 116.16f);
        private static readonly Vector3 BedMax = new Vector3(-40.58f, 5.3f, 117.83f);
        private const float UnderBedZ = 116.85f;         // где лежит герой
        private const float BedBottom = 0.33f;           // край покрывала над полом
        private const float EyeUnderBed = 0.18f;
        private static readonly Vector3 DeskLamp = new Vector3(-40.9f, 5.35f, 114.4f);

        // Путь от люка до спальни (невидим, только шаги) и по комнате (виден только в щели под кроватью).
        private static readonly Vector3[] Climb = {
            new Vector3(-44.8f, 0.78f, 120.5f),   // люк
            new Vector3(-43.2f, 0.78f, 116.2f),
            new Vector3(-42.2f, 0.8f, 111.8f),    // низ лестницы
            new Vector3(-39.7f, 2.35f, 111.8f),
            new Vector3(-38.8f, 2.4f, 112.5f),    // площадка
            new Vector3(-39.5f, 2.5f, 113.2f),
            new Vector3(-42.0f, Floor, 113.2f) }; // верх лестницы
        private static readonly Vector3 Doorway = new Vector3(-42.4f, Floor, 114.0f);
        private static readonly Vector3 Table = new Vector3(-42.5f, Floor, 114.7f);
        private static readonly Vector3 Face = new Vector3(-41.85f, Floor, 115.55f);
        private static readonly Vector3 Aside = new Vector3(-42.6f, Floor, 115.2f);
        private const float FacePause = 4.5f;
        private const float PeekStandOff = 0.32f;        // от края кровати до монстра, когда он заглядывает под неё
        private const float PeekRoll = 75f;              // голова набок, когда морда в щели
        private const float VisibleAbove = 2.45f;       // на втором марше и выше — модель рисуется

        private const float LookYaw = 40f, LookUp = 3f, LookDown = 8f;
        private const float StepLength = 0.62f;

        public static HidingBreathGame Active { get; private set; }
        public BreathHold Breath { get; private set; }     // запасной вариант без микрофона
        public VoiceNoiseGate Voice { get; private set; }
        public bool UsesMicrophone => micClip != null;
        public float MonsterDistance { get; private set; } = float.MaxValue;
        public GameObject Monster => monster;
        public GameObject Zone { get; private set; }
        public Outcome Result => outcome;
        public bool Hidden => hidden;
        public bool SceneRunning => sceneRunning;
        public bool PromptVisible => promptUi != null && promptUi.activeSelf;
        public bool HoldRequested { get; set; }        // для проб: держать дыхание без клавиш

        private Action hideRequested, caughtInOpen;
        private PlayerController player;
        private PlayerInventory inventory;
        private int promptZone = -1;
        private bool sceneRunning, hidden, stopped, sniffing;
        private Outcome outcome;
        private Action<bool> finished;

        // Игрок под кроватью
        private Camera camComponent;
        private Transform cam, body;
        private Behaviour[] inputs;
        private bool[] inputStates;
        private Quaternion camLocal;
        private Vector3 camParentLocal;
        private float yaw, pitch, sensitivity, shake;
        private bool invertY, beamWasOn;
        private string heldBefore;

        // Монстр и свет
        private GameObject monster;
        private Renderer[] monsterRenderers = new Renderer[0];
        private CornerStalkerRig rig;
        private Vector3 monsterPos;
        private float stepCarry;
        private Light deskLamp;
        private Door bedroomDoor;
        private Light[] roomLights = new Light[0];
        private bool[] roomLightStates = new bool[0];

        // Звук
        private AudioSource monsterSource, stepSource, worldSource, heroSource, heartSource;
        private AudioClip[] stepsWood, stepsStairs, breaths, creaks, doorCreaks;
        private AudioClip heartbeat, stinger, slam;
        private int lastStep = -1, lastBreath = -1;
        private string micDevice;
        private AudioClip micClip;
        private readonly float[] micWindow = new float[1024];
        private float micLevel = VoiceNoiseGate.SilenceDb;
        private int micPosition;
        private float micStalled, micLastSound;
        private const float MicDeadSeconds = 2f;        // ровно ноль столько времени — запись запрещена или пустая
        private const float MicStallSeconds = 0.5f;     // позиция записи не двигается — микрофон отключили
        private RandomKnock[] otherKnocks;
        private float nextBreath, nextBeat, beatFlash;

        // UI
        private GameObject ui, promptUi;
        private RawImage vignette;
        private RectTransform bedEdge, airFill, meterFill, meterLimit;
        private Image airBack, airBar, meterBar;
        private TMPro.TextMeshProUGUI meterLabel;
        private static Texture2D vignetteTexture, fringeTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Active = null;

        public void Initialize(PlayerInventory playerInventory, Action onHideRequested, Action onCaughtInOpen)
        {
            inventory = playerInventory;
            player = playerInventory != null ? playerInventory.GetComponentInParent<PlayerController>() : null;
            hideRequested = onHideRequested;
            caughtInOpen = onCaughtInOpen;
            bedroomDoor = GameObject.Find("Door_001")?.GetComponent<Door>();
            BuildZone();
        }

        // Место у кровати, откуда можно под неё залезть (HidingSpotTrigger → IntroSequence → HideEndingController).
        private void BuildZone()
        {
            if (Zone != null) return;
            Zone = new GameObject("Hide Under Bed (M-07)");
            Zone.transform.position = new Vector3((BedMin.x + BedMax.x) * 0.5f, Floor + 0.9f, BedMin.z - 0.45f);
            var box = Zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(BedMax.x - BedMin.x + 0.4f, 1.8f, 1.1f);
            Zone.AddComponent<HidingSpotTrigger>();
            Zone.SetActive(false);
        }

        public void SetPrompt(int zoneIndex) => promptZone = zoneIndex;

        // Для проб: то же, что E у кровати.
        public void RequestHide() => hideRequested?.Invoke();

        public void Stop()
        {
            stopped = true;
        }

        public void CancelForEscape()
        {
            if (!sceneRunning && Active != this) return;
            stopped = true;
            StopAllCoroutines();
            Finish(Active == this);
            RestoreLights();
            if (monster != null) Destroy(monster);
            monster = null;
            sceneRunning = false;
        }

        // ---------------------------------------------------------------- сцена

        // IntroSequence: молоток взят — монстр выходит из люка и идёт наверх.
        public void BeginScene()
        {
            if (sceneRunning) return;
            StartCoroutine(SceneRoutine());
        }

        private IEnumerator SceneRoutine()
        {
            sceneRunning = true;
            stopped = hidden = sniffing = false;
            outcome = Outcome.None;
            Breath = new BreathHold();
            Voice = new VoiceNoiseGate();
            StartMicrophone(); // пока оно поднимается, меряем фон комнаты
            LoadAudio();
            CreateAudio();
            HorrorMomentDirector.Instance?.Interrupt(60f);

            // Люк внизу открывается, свет в комнате мигает и гаснет; остаётся настольная лампа.
            monsterPos = Climb[0];
            PlaceMonster(Vector3.zero);
            Sfx(stepSource, GameSounds.Pick(doorCreaks), 0.7f, 1f);
            yield return KillLights();

            SpawnMonster();
            for (int i = 1; i < Climb.Length && !Done; i++)
                yield return Walk(Climb[i], i <= 2 ? 1.25f : 0.95f);

            // Поднялся. Игрок не под кроватью и не заползает туда — увидел его на лестнице.
            if (!Done && !hidden && Active == null)
            {
                yield return CatchInOpen();
                yield break;
            }
            yield return Linger(1.2f, false);
            yield return OpenBedroomDoor();
            yield return Walk(Doorway, 0.75f);
            yield return Walk(Table, 0.75f);
            yield return Linger(2f, false);       // на столе нет молотка
            yield return Walk(Face, 0.6f);
            yield return Linger(FacePause, true); // ступни у самого лица
            yield return Walk(Aside, 0.75f);
            yield return Walk(Doorway, 0.8f);
            for (int i = Climb.Length - 1; i >= 0 && !Done; i--)
                yield return Walk(Climb[i], i >= Climb.Length - 2 ? 0.9f : 1.25f);

            if (!Done)
            {
                if (monster != null) Destroy(monster);
                monster = null;
                Sfx(worldSource, slam, 0.8f, 1f);     // люк захлопнулся
                yield return Wait(2.2f);
            }
            if (!Done && outcome == Outcome.None) outcome = Outcome.Left;
            sceneRunning = false;
            StopMicrophone();
        }

        private bool Done => stopped || outcome != Outcome.None;

        // IHidingSearch: E у кровати — герой лезет под неё и лежит, пока сцена не решится.
        public IEnumerator Run(int zoneIndex, Action<bool> onFinished)
        {
            // Спрятаться уже нельзя (сцена кончилась или решилась): исход сообщила сама сцена, здесь — ничего.
            // false значил бы «переждал» и выдал бы «Заколотить люк» без пряток.
            if (player == null || !sceneRunning || Done) yield break;
            Active = this;
            finished = onFinished;
            CaptureControl();
            yield return CrawlUnder();
            hidden = true;
            BuildUi();
            if (!UsesMicrophone) // с микрофоном всё сказано на экране
                ThoughtManager.Instance?.TryShowInteractionThoughts("hide-breath-start", new[] { "Не дышать." }, 0f);

            while (!Done)
            {
                Tick();
                yield return null;
            }

            if (stopped)
            {
                Finish(true);
                yield break;
            }
            if (outcome == Outcome.Found)
            {
                yield return CatchUnderBed();
                Finish(false);
                finished?.Invoke(true);
                yield break;
            }
            yield return CrawlOut();
            Finish(true);
            RestoreLights();
            finished?.Invoke(false);
        }

        // Кадр под кроватью: обзор, дыхание, звук, экран.
        private void Tick()
        {
            if (Paused)
            {
                if (ui != null) ui.SetActive(false);
                return;
            }
            if (ui != null && !ui.activeSelf) ui.SetActive(true);
            float dt = Time.deltaTime;
            Look();
            // Полное расстояние: пока оно внизу, под полом, — слышит хуже.
            MonsterDistance = monster != null ? Vector3.Distance(body.position, monsterPos) : float.MaxValue;
            float suspicionBefore = Suspicion;
            bool gasped = false;
            if (UsesMicrophone) Voice.Advance(dt, micLevel, MonsterDistance);
            else gasped = Breath.Advance(dt, HoldRequested || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(1),
                MonsterDistance, false);
            if (!sniffing && suspicionBefore < 0.45f && Suspicion >= 0.45f) sniffing = true;
            if (Detected && outcome == Outcome.None) outcome = Outcome.Found;
            HeroSounds(dt, gasped);
            UpdateUi(dt);
        }

        private static bool Paused => Time.timeScale == 0f || PauseMenuController.InputBlocked;
        public float Suspicion => UsesMicrophone ? Voice.Suspicion : Breath != null ? Breath.Suspicion : 0f;
        private float Pulse => UsesMicrophone ? Voice.Pulse : Breath != null ? Breath.Pulse : 0f;
        private bool Detected => UsesMicrophone ? Voice.Found : Breath != null && Breath.Found;

        // ---------------------------------------------------------------- микрофон

        private void StartMicrophone()
        {
            StopMicrophone();
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0) return;
            micDevice = devices[0];
            micClip = Microphone.Start(micDevice, true, 1, 16000);
            micLevel = VoiceNoiseGate.SilenceDb;
            micPosition = 0;
            micStalled = 0f;
            micLastSound = Time.time;
        }

        private void StopMicrophone()
        {
            if (micClip != null && Microphone.IsRecording(micDevice)) Microphone.End(micDevice);
            micClip = null;
        }

        // Последние ~64 мс с микрофона (буфер не пересоздаётся каждый кадр). Микрофон отключили (запись стоит)
        // или Windows запретила доступ (запись идёт, но ровно ноль) — переходим на задержку дыхания.
        private void SampleMicrophone()
        {
            if (micClip == null) return;
            if (!Microphone.IsRecording(micDevice)) { MicrophoneLost(); return; }
            int position = Microphone.GetPosition(micDevice);
            if (position == micPosition)
            {
                micStalled += Time.unscaledDeltaTime;
                if (micStalled > MicStallSeconds) MicrophoneLost();
                return;
            }
            micPosition = position;
            micStalled = 0f;
            int offset = position - micWindow.Length;
            if (offset < 0) offset += micClip.samples;
            if (!micClip.GetData(micWindow, offset)) return;
            micLevel = VoiceNoiseGate.ToDb(micWindow, micWindow.Length);
            if (micLevel > VoiceNoiseGate.SilenceDb + 0.5f) micLastSound = Time.time;
            else if (Time.time - micLastSound > MicDeadSeconds) MicrophoneLost();
        }

        private void MicrophoneLost()
        {
            StopMicrophone();
            micLevel = VoiceNoiseGate.SilenceDb;
            if (!hidden) return;
            BuildUi(); // шкала микрофона → полоска воздуха и «Пробел»
            ThoughtManager.Instance?.TryShowInteractionThoughts("hide-mic-lost",
                new[] { "Микрофон не слышно. Задерживай дыхание — Пробел." }, 0f);
        }

        // ---------------------------------------------------------------- подсказка у кровати

        private void Update()
        {
            if (sceneRunning && micClip != null && !Paused)
            {
                SampleMicrophone();
                if (!hidden) Voice.Observe(Time.deltaTime, micLevel);
            }
            bool show = promptZone >= 0 && Active == null && sceneRunning && !hidden && !Done && player != null &&
                !player.isCameraLocked && !Paused;
            if (show && promptUi == null) BuildPrompt();
            if (promptUi != null && promptUi.activeSelf != show) promptUi.SetActive(show);
            if (show && Input.GetKeyDown(KeyCode.E)) hideRequested?.Invoke();
        }

        // ---------------------------------------------------------------- монстр

        private void SpawnMonster()
        {
            var prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) return;
            monster = Instantiate(prefab, monsterPos, Quaternion.identity);
            monster.name = "Hiding Monster (M-07)";
            monster.SetActive(true); // префаб хранится выключенным (как у CornerPeekSequence)
            foreach (var col in monster.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var rb in monster.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            monsterRenderers = monster.GetComponentsInChildren<Renderer>(true);
            rig = monster.GetComponent<CornerStalkerRig>();
            if (rig == null) rig = monster.AddComponent<CornerStalkerRig>();
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in monsterRenderers) if (r != null) r.enabled = visible;
        }

        private IEnumerator OpenBedroomDoor()
        {
            if (Done || bedroomDoor == null || !bedroomDoor.isActiveAndEnabled) yield break;
            StandStill(0.3f);
            bedroomDoor.StopHolding();
            bedroomDoor.OpenDoor();
            // isOpen меняется сразу; движение начинается только после фактического поворота створки.
            while (!Done && bedroomDoor != null && bedroomDoor.isActiveAndEnabled && !bedroomDoor.IsFullyOpen)
                yield return null;
        }

        private IEnumerator Walk(Vector3 target, float speed)
        {
            if (Done) yield break;
            rig?.Walk(speed);
            if (rig != null) rig.LookWeight = 0f;
            while (!Done)
            {
                if (Paused) { yield return null; continue; }
                if (sniffing)
                {
                    yield return Sniff();
                    rig?.Walk(speed);
                    continue;
                }
                if (!hidden && Active == null && player != null && monster != null && monsterPos.y > VisibleAbove &&
                    Vector3.Distance(monsterPos, player.transform.position) < 2.2f)
                {
                    yield return CatchInOpen(); // столкнулся с игроком на лестнице
                    yield break;
                }
                Vector3 to = target - monsterPos;
                float move = speed * Time.deltaTime;
                if (to.magnitude <= move)
                {
                    monsterPos = target;
                    PlaceMonster(Flat(to));
                    yield break;
                }
                monsterPos += to.normalized * move;
                PlaceMonster(Flat(to));
                stepCarry += move;
                if (stepCarry >= StepLength)
                {
                    stepCarry = 0f;
                    bool stairs = monsterPos.y > 0.9f && monsterPos.y < Floor - 0.1f;
                    Sfx(stepSource, GameSounds.Pick(stairs && stepsStairs.Length > 0 ? stepsStairs : stepsWood, ref lastStep),
                        UnityEngine.Random.Range(0.62f, 0.7f), 1f);
                }
                yield return null;
            }
        }

        private IEnumerator Linger(float seconds, bool atBed)
        {
            if (Done) yield break;
            StandStill(atBed ? 0.6f : 0.3f);
            if (atBed) PlaceMonster(Vector3.forward, true); // носками к кровати
            Sfx(monsterSource, GameSounds.Pick(breaths), 0.5f, 0.8f);
            float creakAt = seconds * 0.5f;
            for (float t = 0f; t < seconds && !Done; )
            {
                if (Paused) { yield return null; continue; }
                if (sniffing) { yield return Sniff(); StandStill(0.6f); }
                float before = t;
                t += Time.deltaTime;
                // Наваливается на кровать — скрипит прямо над головой.
                if (atBed && before < creakAt && t >= creakAt) Sfx(worldSource, GameSounds.Pick(creaks), 0.75f, 1f);
                if (atBed && before < seconds * 0.8f && t >= seconds * 0.8f) Sfx(monsterSource, GameSounds.Pick(breaths), 0.45f, 0.9f);
                yield return null;
            }
        }

        // Услышал: замирает и принюхивается в сторону кровати, пока не затихнет (или не найдёт).
        private IEnumerator Sniff()
        {
            StandStill(1f);
            Sfx(monsterSource, GameSounds.Pick(breaths), 0.45f, 1f);
            for (float t = 0f; t < 4f && !Done; )
            {
                if (Paused) { yield return null; continue; }
                t += Time.deltaTime;
                if (t > 1.5f && Suspicion < 0.25f) break;
                yield return null;
            }
            sniffing = false;
        }

        private void StandStill(float lookAtBed)
        {
            if (rig == null) return;
            rig.Stand();
            rig.LookTarget = new Vector3(Face.x, Floor + 0.4f, UnderBedZ);
            rig.LookWeight = lookAtBed;
        }

        private void PlaceMonster(Vector3 heading, bool snap = false)
        {
            if (monster != null)
            {
                monster.transform.position = monsterPos;
                if (heading.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(heading.normalized);
                    monster.transform.rotation = snap ? look
                        : Quaternion.Slerp(monster.transform.rotation, look, 1f - Mathf.Exp(-6f * Time.deltaTime));
                }
                // Видно только наверху: на лестнице снизу его закрывает пол, а под кроватью — покрывало.
                SetVisible(monsterPos.y > VisibleAbove);
            }
            if (monsterSource != null) monsterSource.transform.position = monsterPos + Vector3.up * 1.6f;
            if (stepSource != null) stepSource.transform.position = monsterPos + Vector3.up * 0.1f;
            if (deskLamp != null)
            {
                // Рядом с ним лампа захлёбывается.
                float near = Mathf.InverseLerp(2.5f, 0.8f, Vector3.Distance(monsterPos, DeskLamp));
                deskLamp.intensity = Mathf.Lerp(1.3f, 0.35f + 0.9f * Mathf.PerlinNoise(Time.time * 18f, 0.3f), near);
            }
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !Done; )
            {
                if (!Paused) t += Time.deltaTime;
                yield return null;
            }
        }

        // Не спрятался: оно поднялось и бросается к игроку.
        private IEnumerator CatchInOpen()
        {
            outcome = Outcome.CaughtInOpen;
            CaptureControl();
            Vector3 eye = cam != null ? cam.position : player.transform.position + Vector3.up * 1.6f;
            Vector3 toward = Flat(monsterPos - player.transform.position);
            if (toward.sqrMagnitude < 0.01f) toward = player.transform.forward;
            Vector3 stop = player.transform.position + toward.normalized * 0.75f;
            stop.y = Mathf.Max(monsterPos.y, Floor);
            Vector3 from = monsterPos;
            Sfx(heroSource, stinger, 1f, 1f);
            if (rig != null) { rig.StopMoving(); rig.Run(1.6f); rig.LookTarget = eye; rig.LookWeight = 1f; }
            Quaternion look0 = cam != null ? cam.rotation : Quaternion.identity;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                monsterPos = Vector3.Lerp(from, stop, t / 0.4f);
                PlaceMonster(Flat(eye - monsterPos), true);
                SetVisible(true);
                AimAtMonster(look0, t / 0.25f);
                yield return null;
            }
            if (rig != null) rig.StopMoving();
            shake = 2.5f;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                rig?.Lunge(Mathf.Lerp(0.15f, 0.6f, t / 0.9f), 0f);
                AimAtMonster(look0, 1f);
                yield return null;
            }
            yield return DeathTapeEffect.Play(GetComponent<IntroSequence>()?.fadeScreen);
            if (monster != null) Destroy(monster, 6f);
            monster = null;
            Finish(false);
            sceneRunning = false;
            caughtInOpen?.Invoke();
        }

        // Нашёл под кроватью: если он не у кровати — подбегает (внизу — взлетает по лестнице), замирает вплотную
        // и резко падает на колено, корпус ломается вниз, морда боком — в щель у самого пола.
        private IEnumerator CatchUnderBed()
        {
            if (monster == null || rig == null || cam == null)
            {
                Sfx(heroSource, stinger, 1f, 1f);
                yield break;
            }
            Vector3 front = new Vector3(cam.position.x, Floor, BedMin.z - PeekStandOff);
            if (monsterPos.y < Floor - 0.2f)
            {
                // Услышал снизу: по лестнице бегом, не видно.
                SetVisible(false);
                for (int i = 0; i < 6; i++)
                {
                    Sfx(stepSource, GameSounds.Pick(stepsStairs.Length > 0 ? stepsStairs : stepsWood, ref lastStep), 0.75f, 1f);
                    stepSource.transform.position = Vector3.Lerp(monsterPos, Climb[Climb.Length - 1], i / 5f);
                    yield return new WaitForSeconds(0.17f);
                }
                monsterPos = Doorway;
            }
            if (Vector3.Distance(Flat(monsterPos), Flat(front)) > 0.25f)
            {
                Vector3 from = monsterPos;
                rig.Run(1.3f);
                rig.LookWeight = 0f;
                for (float t = 0f; t < 0.45f; t += Time.deltaTime)
                {
                    monsterPos = Vector3.Lerp(from, front, t / 0.45f);
                    PlaceMonster(Flat(front - from), true);
                    yield return null;
                }
                Sfx(stepSource, GameSounds.Pick(stepsWood, ref lastStep), 0.6f, 1f);
            }
            monsterPos = front;
            rig.Stand();
            PlaceMonster(Vector3.forward, true);
            // Вплотную — тишина на полвдоха.
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) yield return null;

            // Своя анимация MonsterPeekUnderBed: 0–0.14 с — рывком на колени, ладони в пол, к 0.26 с корпус внизу.
            // На ~0.2 с голова рывком разворачивается на игрока и заваливается набок; герой в панике щёлкает
            // фонариком — морда в луче. Дальше мелкие резкие подёргивания головы, к ~1.1 с рука тянется к лицу.
            Sfx(monsterSource, GameSounds.Pick(breaths), 0.4f, 1f);
            rig.LookWeight = 0f;
            rig.HeadRoll = 0f;
            rig.Peek();
            Quaternion baseLook = cam.localRotation;
            bool snapped = false;
            float roll = PeekRoll, nextTwitch = 0f;
            for (float t = 0f; t < 1.7f; t += Time.deltaTime)
            {
                PlaceMonster(Vector3.forward, true);
                SetVisible(true);
                rig.LookTarget = cam.position;
                if (t >= 0.2f)
                {
                    if (!snapped)
                    {
                        snapped = true;
                        Sfx(heroSource, stinger, 1f, 1f);
                        FlashlightInFace();
                        shake = 1.6f;
                    }
                    rig.LookWeight = Mathf.MoveTowards(rig.LookWeight, 1f, Time.deltaTime / 0.06f);
                    // Подёргивания: голова дёргается рывками, а не плавает.
                    if (t >= nextTwitch)
                    {
                        nextTwitch = t + UnityEngine.Random.Range(0.12f, 0.35f);
                        roll = PeekRoll + UnityEngine.Random.Range(-12f, 12f);
                    }
                    rig.HeadRoll = roll;
                }
                shake = Mathf.MoveTowards(shake, 0.15f, Time.deltaTime * 1.5f);
                cam.localRotation = baseLook * Quaternion.Euler(UnityEngine.Random.Range(-shake, shake),
                    UnityEngine.Random.Range(-shake, shake), 0f);
                yield return null;
            }
            yield return DeathTapeEffect.Play(GetComponent<IntroSequence>()?.fadeScreen);
            if (monster != null) Destroy(monster, 6f);
            monster = null;
        }

        // Фонарик героя из-под кровати прямо в морду: щелчок и луч (рука героя пустая — свет от камеры).
        private void FlashlightInFace()
        {
            if (cam == null) return;
            var lib = GameSounds.Library;
            Sfx(heroSource, lib != null ? lib.flashlightOn : null, 1f, 0.9f);
            var beamObj = new GameObject("Under Bed Flashlight (M-07)");
            beamObj.transform.SetParent(cam, false);
            beamObj.transform.localPosition = new Vector3(0.05f, -0.06f, 0f);
            var beam = beamObj.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.spotAngle = 50f;
            beam.innerSpotAngle = 25f;
            beam.range = 4f;
            beam.intensity = 2.5f;
            beam.color = new Color(1f, 0.95f, 0.85f);
            beam.shadows = LightShadows.None;
            Destroy(beamObj, 8f);
        }

        private void AimAtMonster(Quaternion from, float k)
        {
            if (cam == null || monster == null) return;
            Vector3 target = rig != null && rig.Head != null ? rig.Head.position : monsterPos + Vector3.up * 1.7f;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);
            Quaternion jitter = Quaternion.Euler(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake), 0f);
            cam.rotation = Quaternion.Slerp(from, Quaternion.LookRotation(target - cam.position), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k))) * jitter;
        }

        // ---------------------------------------------------------------- свет

        private IEnumerator KillLights()
        {
            roomLights = FindRoomLights();
            roomLightStates = new bool[roomLights.Length];
            for (int i = 0; i < roomLights.Length; i++) roomLightStates[i] = roomLights[i].enabled;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                bool on = Mathf.PerlinNoise(Time.time * 25f, 0.4f) > 0.5f;
                for (int i = 0; i < roomLights.Length; i++) if (roomLightStates[i]) roomLights[i].enabled = on;
                yield return null;
            }
            foreach (var l in roomLights) l.enabled = false;
            Sfx(worldSource, GameSounds.Library != null ? GameSounds.Library.breakerOff : null, 1f, 0.7f);
            // Настольная лампа у молотка остаётся — тусклая, тёплая, у самого пола даёт контровой свет.
            var lampObj = new GameObject("Desk Lamp Light (M-07)");
            lampObj.transform.position = DeskLamp;
            deskLamp = lampObj.AddComponent<Light>();
            deskLamp.type = LightType.Point;
            deskLamp.color = new Color(1f, 0.74f, 0.46f);
            deskLamp.range = 3.4f;
            deskLamp.intensity = 1.3f;
            deskLamp.shadows = LightShadows.None;
        }

        private static Light[] FindRoomLights()
        {
            var found = new System.Collections.Generic.List<Light>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.name == "lightRightBedroom" || l.name == "lightStairs") found.Add(l);
            return found.ToArray();
        }

        private void RestoreLights()
        {
            if (deskLamp != null) Destroy(deskLamp.gameObject);
            deskLamp = null;
            for (int i = 0; i < roomLights.Length; i++) if (roomLights[i] != null) roomLights[i].enabled = roomLightStates[i];
        }

        // ---------------------------------------------------------------- игрок

        private void CaptureControl()
        {
            if (inputs != null) return;
            body = player.transform;
            camComponent = player.GetComponentInChildren<Camera>();
            cam = camComponent != null ? camComponent.transform : null;
            camLocal = cam != null ? cam.localRotation : Quaternion.identity;
            camParentLocal = cam != null && cam.parent != null ? cam.parent.localPosition : Vector3.zero;
            sensitivity = SettingsStore.GetMouseSensitivity();
            invertY = SettingsStore.GetInvertY();
            player.StopMotion();
            inputs = new Behaviour[] { inventory, player.GetComponent<PlayerInteraction>(), player.GetComponentInChildren<PlayerDoorOpen>(),
                player.GetComponentInChildren<PickupController>(), player.GetComponentInChildren<HeadBobbing>(), player.GetComponent<PlayerFloorKnock>() };
            inputStates = new bool[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputStates[i] = inputs[i] != null && inputs[i].enabled;
                if (inputs[i] != null) inputs[i].enabled = false;
            }
            player.isCameraLocked = true;
        }

        // Лечь и заползти под кровать лицом к комнате; фонарик гаснет — свет выдал бы.
        private IEnumerator CrawlUnder()
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            beamWasOn = inventory != null && inventory.flashlightLightSource != null && inventory.flashlightLightSource.enabled;
            if (beamWasOn) inventory.flashlightLightSource.enabled = false;
            // Под кроватью руки пустые — молоток и фонарик не торчат в щели.
            heldBefore = inventory != null ? inventory.EquippedItemId : null;
            inventory?.ActivateItem(string.Empty);
            Vector3 from = body.position;
            float x = Mathf.Clamp(from.x, BedMin.x + 0.6f, BedMax.x - 0.6f);
            Vector3 edge = new Vector3(x, from.y, BedMin.z - 0.4f);
            Vector3 under = new Vector3(x, from.y, UnderBedZ);
            Quaternion fromYaw = body.rotation, toYaw = Quaternion.LookRotation(Vector3.back);
            float fromEye = cam != null && cam.parent != null ? cam.parent.localPosition.y : 0f;
            Quaternion camFrom = cam != null ? cam.localRotation : Quaternion.identity;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                body.position = k < 0.5f ? Vector3.Lerp(from, edge, k * 2f) : Vector3.Lerp(edge, under, k * 2f - 1f);
                body.rotation = Quaternion.Slerp(fromYaw, toYaw, Mathf.Min(1f, k * 2f));
                SetEye(Mathf.Lerp(fromEye, EyeUnderBed, Mathf.Min(1f, k * 1.6f)));
                if (cam != null) cam.localRotation = Quaternion.Slerp(camFrom, Quaternion.identity, k);
                yield return null;
            }
            body.SetPositionAndRotation(under, toYaw);
            SetEye(EyeUnderBed);
            yaw = pitch = 0f;
        }

        private IEnumerator CrawlOut()
        {
            if (ui != null) ui.SetActive(false);
            Vector3 from = body.position;
            Vector3 to = new Vector3(from.x, from.y, BedMin.z - 0.7f);
            float fromEye = cam != null && cam.parent != null ? cam.parent.localPosition.y : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                body.position = Vector3.Lerp(from, to, k);
                SetEye(Mathf.Lerp(fromEye, camParentLocal.y, Mathf.Max(0f, k * 1.4f - 0.4f)));
                yield return null;
            }
            body.position = to;
            if (inventory != null && !string.IsNullOrEmpty(heldBefore)) inventory.Equip(heldBefore);
            if (beamWasOn && inventory != null && inventory.flashlightLightSource != null) inventory.flashlightLightSource.enabled = true;
        }

        private void SetEye(float y)
        {
            if (cam == null || cam.parent == null) return;
            Vector3 local = cam.parent.localPosition;
            local.y = y;
            cam.parent.localPosition = local;
        }

        private void Look()
        {
            if (cam == null) return;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * (invertY ? -1f : 1f);
            yaw = Mathf.Clamp(yaw + Input.GetAxis("Mouse X") * sensitivity, -LookYaw, LookYaw);
            pitch = Mathf.Clamp(pitch - mouseY, -LookUp, LookDown);
            body.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(0f, yaw, 0f);
            float tremor = Pulse * 0.3f;
            float time = Time.time * 7f;
            cam.localRotation = Quaternion.Euler(pitch + (Mathf.PerlinNoise(time, 0.2f) - 0.5f) * tremor,
                (Mathf.PerlinNoise(0.6f, time) - 0.5f) * tremor, 0f);
        }

        // restore — вернуть управление и рост; после броска камера остаётся у концовки.
        private void Finish(bool restore)
        {
            if (ui != null) Destroy(ui);
            ui = null;
            if (monsterSource != null) monsterSource.Stop();
            if (inputs != null)
                for (int i = 0; i < inputs.Length; i++) if (inputs[i] != null) inputs[i].enabled = inputStates[i];
            inputs = null;
            if (restore && player != null)
            {
                SetEye(camParentLocal.y);
                if (cam != null) cam.localRotation = camLocal;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
                player.isCameraLocked = false;
            }
            if (Active == this) Active = null;
            hidden = false;
            HoldRequested = false;
            StopMicrophone();
        }

        private void OnDisable()
        {
            DeathTapeEffect.Cancel();
            if (promptUi != null) promptUi.SetActive(false);
            if (!sceneRunning && Active != this) return;
            stopped = true;
            StopAllCoroutines();
            Finish(true);
            RestoreLights();
            if (monster != null) Destroy(monster);
            sceneRunning = false;
        }

        // ---------------------------------------------------------------- герой: дыхание и сердце

        // С микрофоном дышит сам игрок — слышно только сердце героя. Без микрофона — дыхание по BreathHold.
        private void HeroSounds(float dt, bool gasped)
        {
            if (!UsesMicrophone)
            {
                if (gasped)
                {
                    Sfx(heroSource, GameSounds.Pick(breaths, ref lastBreath), 1.1f, 1f);
                    nextBreath = 1.2f;
                    ThoughtManager.Instance?.TryShowInteractionThoughts("hide-breath-gasp", new[] { "Не могу… воздуха…" }, 0f);
                }
                if (!Breath.Holding)
                {
                    nextBreath -= dt;
                    if (nextBreath <= 0f)
                    {
                        nextBreath = Mathf.Lerp(2.4f, 1.1f, Breath.Pulse);
                        Sfx(heroSource, GameSounds.Pick(breaths, ref lastBreath), UnityEngine.Random.Range(0.95f, 1.05f),
                            Mathf.Clamp01(Breath.Noise) * 0.8f);
                    }
                }
                else nextBreath = Mathf.Max(nextBreath, 0.35f); // отпустил — первый выдох почти сразу
            }

            nextBeat -= dt;
            if (nextBeat <= 0f && heartbeat != null)
            {
                nextBeat = 60f / Mathf.Lerp(70f, 150f, Pulse);
                heartSource.pitch = 1f;
                heartSource.PlayOneShot(heartbeat, Mathf.Lerp(0.25f, 0.9f, Pulse));
                beatFlash = 1f;
            }
        }

        // ---------------------------------------------------------------- звук и UI

        private void LoadAudio()
        {
            var lib = GameSounds.Library;
            stepsWood = lib != null && lib.stepWood.Length > 0 ? lib.stepWood : new[] {
                Resources.Load<AudioClip>("Audio/Horror/FollowingStep1"), Resources.Load<AudioClip>("Audio/Horror/FollowingStep2") };
            stepsStairs = lib != null ? lib.stepStairs : new AudioClip[0];
            breaths = lib != null ? lib.breathScared : new AudioClip[0];
            creaks = lib != null ? lib.houseCreaks : new AudioClip[0];
            doorCreaks = lib != null ? lib.doorCreaks : new AudioClip[0];
            heartbeat = lib != null ? lib.heartbeat : null;
            stinger = Resources.Load<AudioClip>("Audio/CornerJumpscare");
            slam = Resources.Load<AudioClip>("Audio/Horror/impactWood_heavy_000");
            var intro = FindFirstObjectByType<IntroSequence>();
            otherKnocks = intro != null && intro.knockController != null
                ? intro.knockController.GetComponentsInChildren<RandomKnock>(true) : null;
        }

        private void LateUpdate()
        {
            // Пока идёт сцена, свой стук снизу молчит.
            if (!sceneRunning || otherKnocks == null) return;
            foreach (var knock in otherKnocks) if (knock != null) knock.PauseFor(1f);
        }

        private void CreateAudio()
        {
            if (monsterSource != null) return;
            monsterSource = HorrorMomentDirector.CreateSource(transform, "Hiding Monster", 16f);
            monsterSource.minDistance = 1.2f;
            stepSource = HorrorMomentDirector.CreateSource(transform, "Hiding Monster Steps", 22f);
            stepSource.minDistance = 2.5f;
            worldSource = HorrorMomentDirector.CreateSource(transform, "Hiding Bed Creak", 12f);
            worldSource.transform.position = new Vector3(Face.x, Floor + 0.6f, (BedMin.z + BedMax.z) * 0.5f);
            heroSource = Flat2D("Hiding Hero Breath", AudioBus.Voice);
            heartSource = Flat2D("Hiding Heartbeat", AudioBus.Horror);
        }

        private AudioSource Flat2D(string name, AudioBus bus)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var source = obj.AddComponent<AudioSource>();
            AudioRouting.Assign(source, bus);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static void Sfx(AudioSource audio, AudioClip clip, float pitch, float volume)
        {
            if (audio == null || clip == null) return;
            audio.pitch = pitch * UnityEngine.Random.Range(0.97f, 1.03f);
            audio.PlayOneShot(clip, volume);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static TMPro.TMP_FontAsset Font() =>
            QuestManager.Instance != null && QuestManager.Instance.questUiText != null ? QuestManager.Instance.questUiText.font : null;

        private void BuildPrompt()
        {
            promptUi = UiStyle.OverlayCanvas(transform, "HidingPrompt", 139, new Vector2(1280f, 720f), false).gameObject;
            var keys = UiKeyHints.Create(promptUi.transform, "Keys", Font());
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, 110f);
            keys.Set("E", "ЗАЛЕЗТИ ПОД КРОВАТЬ");
            promptUi.SetActive(false);
        }

        private void BuildUi()
        {
            if (ui != null) Destroy(ui);
            var font = Font();
            ui = UiStyle.OverlayCanvas(null, "HidingBreath", 140, new Vector2(1280f, 720f), false).gameObject;

            // Изнанка кровати: всё выше края покрывала — темно, внизу бахрома. Край считается по наклону взгляда.
            bedEdge = UiStyle.Stretch("Bed Underside", ui.transform);
            var top = UiStyle.Fill(bedEdge, "Dark", new Color(0.01f, 0.008f, 0.006f, 1f));
            top.rectTransform.offsetMin = new Vector2(0f, 26f);
            var fringe = UiStyle.Rect("Fringe", bedEdge, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 40f));
            fringe.anchorMin = new Vector2(0f, 0f);
            fringe.anchorMax = new Vector2(1f, 0f);
            var fringeImage = fringe.gameObject.AddComponent<RawImage>();
            fringeImage.texture = FringeTexture();
            fringeImage.uvRect = new Rect(0f, 0f, 9f, 1f);
            fringeImage.raycastTarget = false;
            fringeImage.color = new Color(0.01f, 0.008f, 0.006f, 1f);

            vignette = UiStyle.Stretch("Vignette", ui.transform).gameObject.AddComponent<RawImage>();
            vignette.texture = VignetteTexture();
            vignette.raycastTarget = false;
            vignette.color = new Color(0f, 0f, 0f, 0f);

            if (UsesMicrophone) BuildMicrophoneUi(font);
            else BuildBreathUi(font);
            UpdateUi(0f);
        }

        // «Молчи»: крупно — что делать, ниже — почему, внизу — шкала громкости микрофона с отметкой лимита.
        private void BuildMicrophoneUi(TMPro.TMP_FontAsset font)
        {
            var title = UiStyle.Text("Silence", ui.transform, font, 34f, UiStyle.Ivory, TMPro.TextAlignmentOptions.Center);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -70f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 46f);
            title.text = "НЕ ГОВОРИ НИ СЛОВА";
            const float width = 320f;
            var meter = UiStyle.Rect("Meter", ui.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 96f), new Vector2(width, 8f));
            meter.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            meterFill = UiStyle.Rect("Level", meter, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
            meterBar = meterFill.gameObject.AddComponent<Image>();
            meterBar.raycastTarget = false;
            meterLimit = UiStyle.Rect("Limit", meter, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width / VoiceNoiseGate.Headroom, 0f), new Vector2(2f, 18f));
            meterLimit.gameObject.AddComponent<Image>().color = UiStyle.Rust;
            meterLabel = UiStyle.Text("Mic", ui.transform, font, 16f, UiStyle.Ash, TMPro.TextAlignmentOptions.Center);
            meterLabel.rectTransform.anchorMin = meterLabel.rectTransform.anchorMax = meterLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            meterLabel.rectTransform.anchoredPosition = new Vector2(0f, 108f);
            meterLabel.rectTransform.sizeDelta = new Vector2(600f, 24f);
        }

        // Без микрофона: задержка дыхания на Пробел и тонкая полоска воздуха.
        private void BuildBreathUi(TMPro.TMP_FontAsset font)
        {
            airBack = UiStyle.Rect("Air", ui.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 118f), new Vector2(180f, 3f)).gameObject.AddComponent<Image>();
            airBack.raycastTarget = false;
            airFill = UiStyle.Rect("Fill", airBack.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(180f, 3f));
            airBar = airFill.gameObject.AddComponent<Image>();
            airBar.raycastTarget = false;

            var keys = UiKeyHints.Create(ui.transform, "Keys", font);
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, 70f);
            keys.Set("ПРОБЕЛ", "ЗАДЕРЖАТЬ ДЫХАНИЕ");
        }

        private void UpdateUi(float dt)
        {
            if (ui == null) return;
            if (bedEdge != null && camComponent != null && cam != null)
            {
                // Край покрывала: угол над горизонтом от глаз до края кровати, сдвинутый наклоном взгляда.
                float edgeDistance = Mathf.Max(0.2f, cam.position.z - BedMin.z);
                float angle = Mathf.Atan2(Floor + BedBottom - cam.position.y, edgeDistance) * Mathf.Rad2Deg + pitch;
                float half = camComponent.fieldOfView * 0.5f;
                float y = 0.5f + 0.5f * Mathf.Tan(Mathf.Clamp(angle, -half + 1f, half - 1f) * Mathf.Deg2Rad) / Mathf.Tan(half * Mathf.Deg2Rad);
                float height = ((RectTransform)ui.transform).rect.height;
                bedEdge.offsetMin = new Vector2(0f, y * height);
                bedEdge.offsetMax = Vector2.zero;
            }
            beatFlash = Mathf.MoveTowards(beatFlash, 0f, dt * 5f);

            if (UsesMicrophone)
            {
                vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(0.3f + 0.15f * Pulse + 0.1f * Pulse * beatFlash));
                if (meterFill == null) return;
                float level = Voice.Loudness;
                meterFill.sizeDelta = new Vector2(320f * level / VoiceNoiseGate.Headroom, 8f);
                Color ivory = UiStyle.Ivory;
                meterBar.color = level >= 1f ? UiStyle.Rust
                    : level > 0.7f ? UiStyle.Amber : new Color(ivory.r, ivory.g, ivory.b, 0.8f);
                meterLabel.text = !Voice.Calibrated ? "МИКРОФОН · СЛУШАЮ ТИШИНУ…"
                    : level >= 1f ? "СЛИШКОМ ГРОМКО" : "МИКРОФОН";
                meterLabel.color = level >= 1f ? UiStyle.Rust : UiStyle.Ash;
                return;
            }

            float strain = Breath.Holding ? 1f - Breath.Air : 0f;
            float alpha = 0.3f + 0.45f * strain + 0.1f * Breath.Pulse * beatFlash;
            float red = Mathf.Clamp01((0.35f - Breath.Air) / 0.35f) * (Breath.Holding ? 0.35f : 0f);
            vignette.color = new Color(red, 0f, 0f, Mathf.Clamp01(alpha));
            if (airFill == null) return;
            // Полоска видна, только пока воздуха не полный запас.
            float show = Breath.Air < 0.995f ? 1f : 0f;
            Color ivoryAir = UiStyle.Ivory;
            airBack.color = new Color(0f, 0f, 0f, 0.35f * show);
            airBar.color = Breath.Air < 0.25f
                ? new Color(UiStyle.Rust.r, UiStyle.Rust.g, UiStyle.Rust.b, 0.8f * show)
                : new Color(ivoryAir.r, ivoryAir.g, ivoryAir.b, 0.55f * show);
            airFill.sizeDelta = new Vector2(180f * Breath.Air, 3f);
        }

        // Бахрома покрывала: неровные висящие нити.
        private static Texture2D FringeTexture()
        {
            if (fringeTexture != null) return fringeTexture;
            const int w = 64, h = 32;
            fringeTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "BedFringe", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float length = 0.35f + 0.65f * Mathf.PerlinNoise(x * 0.37f, 0.5f) * (x % 3 == 0 ? 1f : 0.7f);
                for (int y = 0; y < h; y++)
                {
                    float fromTop = 1f - (y + 0.5f) / h;
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(fromTop < length ? 255 : 0));
                }
            }
            fringeTexture.SetPixels32(pixels);
            fringeTexture.Apply(false, true);
            return fringeTexture;
        }

        // Мягкое затемнение к краям экрана.
        private static Texture2D VignetteTexture()
        {
            if (vignetteTexture != null) return vignetteTexture;
            const int size = 128;
            vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HidingVignette", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.35f, Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            vignetteTexture.SetPixels32(pixels);
            vignetteTexture.Apply(false, true);
            return vignetteTexture;
        }
    }
}
