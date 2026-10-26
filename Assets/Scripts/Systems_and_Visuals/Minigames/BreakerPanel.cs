using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Мини-игра M4 «Щиток» в мире (модель Poly Haven «Power Box 01», автоматы — кости рига). Герой подходит
    // вплотную, открывает дверцу; луч фонарика сужается — видно только то, что в центре. Мышь — светить,
    // ЛКМ — щёлкнуть автомат под лучом. Логика — BreakerBoard, здесь подача: снизу стук ползёт к щитку,
    // автомат кухни сам щёлкает вниз, вводной при опущенной группе выбивает с треском.
    public sealed class BreakerPanel : MonoBehaviour
    {
        private const float StandDistance = 0.6f;     // от дверцы до глаз
        private const float ZoomFactor = 0.62f;
        private const float AimYaw = 22f, AimPitch = 17f;
        private const float BeamAngle = 20f, BeamInner = 11f;
        // Фонарик рассчитан на метры, а до щитка 0.6 м: в полную силу металл засвечивается добела.
        private const float BeamIntensity = 0.8f;
        private const float GroupOffAngle = 65f, MainOffAngle = 60f;
        private const float FlipSpeed = 900f;          // градусов в секунду: щелчок, а не плавный ход
        private const float PickRadius = 0.016f;       // автоматы стоят через 2.2 см
        private const float DoorClosedAngle = -90f;    // в модели дверца сохранена открытой
        private const float DepthBelowFloor = 1.2f;

        private static readonly string[] GroupBones = {
            "def_line1_a", "def_line1_b", "def_line1_c", "def_line1_d", "def_line1_e", "def_line1_f", "def_line1_g", "def_line1_h",
            "def_line2_a", "def_line2_b", "def_line2_c", "def_line2_d", "def_line2_e", "def_line2_f", "def_line2_g", "def_line2_h" };
        private const string MainBone = "def_line3_b";
        private const string DoorBone = "def_door";

        public static BreakerPanel Active { get; private set; }
        public static bool IsPlaying => Active != null;

        public BreakerBoard Board { get; private set; }
        public bool Played { get; private set; }
        public int Aimed { get; private set; } = -2;   // -2 — ничего, -1 — вводной, иначе группа
        public int KnocksPlayed { get; private set; }

        [Tooltip("Над какой точкой пола (мир) стук начинает путь к щитку — дальний угол подвала.")]
        [SerializeField] private Vector3 knockStart = new Vector3(-44.5f, 0.8f, 103.5f);
        [Tooltip("Высота пола первого этажа: стук идёт под ним.")]
        [SerializeField] private float floorY = 0.78f;

        private Func<bool> canInteract;
        private Action<bool> finished;
        private Transform door, main;
        private readonly Transform[] groups = new Transform[GroupBones.Length];
        private readonly Quaternion[] groupRest = new Quaternion[GroupBones.Length];
        private readonly float[] groupAngle = new float[GroupBones.Length];
        private Quaternion mainRest, doorRest;
        private float mainAngle, doorAngle = DoorClosedAngle, doorTarget = DoorClosedAngle;
        private bool[] wired;                          // кость нашлась — автомат участвует в игре
        private int boardGroups;

        private PlayerController player;
        private PlayerInventory inventory;
        private Camera camComponent;
        private Transform cam;
        private Behaviour[] inputs;
        private bool[] inputStates;
        private Quaternion camLocal, baseAim;
        private float baseFov, aimYaw, aimPitch, sensitivity, shake, shakeTime;
        private bool invertY, released, kitchenDead; // kitchenDead — после успеха кухня выбита насовсем
        private Light beam;
        private float beamAngle, beamInner, beamIntensity;
        private string equippedBefore;

        private AudioSource panelSource, humSource, knockSource;
        private AudioLowPassFilter knockLowPass;
        private AudioClip[] onClips;
        private AudioClip tripClip, mainOnClip, mainOffClip, mainTripClip, doorOpenClip, doorCloseClip, humClip;
        private AudioClip knockClip, heavyClip, thudClip, scrapeClip;
        private int lastOn = -1;
        private RandomKnock[] otherKnocks;
        private float nextKnock;

        private GameObject ui;
        private TextMeshProUGUI caption;
        private UiKeyHints keys;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Active = null;

        public bool CanInteract => !Played && Active == null && canInteract != null && canInteract();

        // IntroSequence: когда щиток доступен и что делать с результатом (true — свет вернулся).
        public void Configure(Func<bool> available, Action<bool> onFinished)
        {
            canInteract = available;
            finished = onFinished;
        }

        private void Awake()
        {
            for (int i = 0; i < GroupBones.Length; i++)
            {
                groups[i] = FindDeep(transform, GroupBones[i]);
                if (groups[i] != null) groupRest[i] = groups[i].localRotation;
            }
            main = FindDeep(transform, MainBone);
            if (main != null) mainRest = main.localRotation;
            door = FindDeep(transform, DoorBone);
            if (door != null) doorRest = door.localRotation;
            // Риг считается и вне поля зрения: дверца и рычажки не должны «залипать» у края кадра.
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            ApplyPose();
        }

        public bool Interact(PlayerInteraction actor)
        {
            if (!CanInteract || actor == null) return false;
            var pc = actor.GetComponent<PlayerController>();
            if (pc == null || pc.isCameraLocked) return false;
            StartCoroutine(Play(pc));
            return true;
        }

        // Для проб: щёлкнуть автомат (-1 — вводной) тем же путём, что ЛКМ.
        public void RequestFlip(int index) => requestedFlip = index;
        private int? requestedFlip;

        private IEnumerator Play(PlayerController pc)
        {
            Played = true;
            Active = this;
            Board = new BreakerBoard(3, new System.Random(Environment.TickCount));
            boardGroups = Mathf.Min(BreakerBoard.GroupCount, groups.Length);
            for (int i = 0; i < boardGroups; i++) groupAngle[i] = Board.IsUp(i) ? 0f : GroupOffAngle;
            mainAngle = MainOffAngle;
            ApplyPose();
            HorrorMomentDirector.Instance?.Interrupt(5f);
            CaptureControl(pc);
            LoadAudio();
            CreateAudio();

            yield return Approach();
            doorTarget = 0f;
            Sfx(panelSource, doorOpenClip, 1f, 0.9f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                UpdateView(Time.deltaTime);
                yield return null;
            }
            BuildUi();
            nextKnock = 1.2f;

            while (!Board.Finished)
            {
                if (Time.timeScale == 0f || PauseMenuController.InputBlocked)
                {
                    requestedFlip = null;
                    yield return null;
                    continue;
                }
                float dt = Time.deltaTime;
                HoldOtherKnocks();
                HandleInput();
                if (Board.Finished) break;
                if (Board.Advance(dt)) KitchenTrips();
                Knocks(dt);
                UpdateView(dt);
                yield return null;
            }

            bool success = Board.Succeeded;
            if (ui != null) ui.SetActive(false);
            yield return success ? PowerReturns() : KnockReachesPanel();
            Finish();
            finished?.Invoke(success);
        }

        // ---------------------------------------------------------------- ввод

        private void HandleInput()
        {
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * (invertY ? -1f : 1f);
            aimYaw = Mathf.Clamp(aimYaw + Input.GetAxis("Mouse X") * sensitivity, -AimYaw, AimYaw);
            aimPitch = Mathf.Clamp(aimPitch - mouseY, -AimPitch, AimPitch);
            Aimed = FindAimed();
            UpdateCaption();
            int? flip = requestedFlip;
            requestedFlip = null;
            if (flip == null && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)) && Aimed != -2) flip = Aimed;
            if (flip == null) return;
            if (flip.Value == -1) FlipMain();
            else FlipGroup(flip.Value);
        }

        private void FlipGroup(int group)
        {
            if (group < 0 || group >= boardGroups || !wired[group]) return;
            BreakerFlip result = Board.Toggle(group);
            if (result == BreakerFlip.None) return;
            Sfx(panelSource, GameSounds.Pick(onClips, ref lastOn), result == BreakerFlip.Raised ? 1f : 0.9f, 0.85f);
            Shake(0.08f);
            if (keys != null) keys.gameObject.SetActive(false);
        }

        private void FlipMain()
        {
            BreakerFlip result = Board.ToggleMain();
            if (result == BreakerFlip.None) return;
            if (keys != null) keys.gameObject.SetActive(false);
            if (result == BreakerFlip.Restored)
            {
                mainAngle = 0f;
                Sfx(panelSource, mainOnClip, 1f, 1f);
                Shake(0.25f);
                return;
            }
            // Вводной встаёт и тут же с треском уходит вниз; фонарик вздрагивает.
            StartCoroutine(MainTripsBack());
        }

        private IEnumerator MainTripsBack()
        {
            mainAngle = 0f;
            Sfx(panelSource, mainOnClip, 1f, 0.9f);
            yield return new WaitForSeconds(0.12f);
            mainAngle = MainOffAngle;
            Sfx(panelSource, mainTripClip, 1f, 1f);
            Sfx(panelSource, mainOffClip, 0.95f, 0.8f);
            Shake(0.45f);
            if (beam != null) StartCoroutine(Flicker(0.35f));
            if (Board.MainTrips == 1)
                ThoughtManager.Instance?.TryShowInteractionThoughts("breaker-main-trip",
                    new[] { "Выбило. Сначала все группы, потом вводной." }, 0f);
        }

        private void KitchenTrips()
        {
            Sfx(panelSource, tripClip, 1.05f, 1f);
            Shake(0.12f);
            if (Board.Retrips == 1)
                ThoughtManager.Instance?.TryShowInteractionThoughts("breaker-kitchen-trip",
                    new[] { "Кухня сама выбила. Не держит." }, 0f);
        }

        private int FindAimed()
        {
            if (cam == null) return -2;
            Vector3 origin = cam.position, direction = cam.forward;
            int best = -2;
            float bestDistance = PickRadius;
            for (int i = 0; i < boardGroups; i++)
                if (wired[i]) Consider(i, LeverPoint(groups[i]), origin, direction, ref best, ref bestDistance);
            if (main != null) Consider(-1, LeverPoint(main), origin, direction, ref best, ref bestDistance);
            return best;
        }

        private static void Consider(int index, Vector3 point, Vector3 origin, Vector3 direction, ref int best, ref float bestDistance)
        {
            Vector3 to = point - origin;
            if (Vector3.Dot(to, direction) <= 0f) return;
            float distance = Vector3.Cross(direction, to).magnitude;
            if (distance >= bestDistance) return;
            bestDistance = distance;
            best = index;
        }

        // Рычажок торчит из корпуса автомата вверх-наружу от кости.
        private Vector3 LeverPoint(Transform bone) => bone.position + transform.forward * 0.012f;

        private void UpdateCaption()
        {
            if (caption == null) return;
            if (Aimed == -2) caption.text = string.Empty;
            else if (Aimed == -1) caption.text = "ВВОДНОЙ";
            else caption.text = (Aimed + 1) + " · " + BreakerBoard.Labels[Aimed];
        }

        // ---------------------------------------------------------------- стук снизу

        // Свой стук игры — остальной на время щитка придержан.
        private void HoldOtherKnocks()
        {
            if (otherKnocks == null) return;
            foreach (var knock in otherKnocks) if (knock != null) knock.PauseFor(1f);
        }

        private void Knocks(float dt)
        {
            if (knockSource == null) return;
            float approach = Board.Approach;
            knockSource.transform.position = KnockPoint(approach);
            if (knockLowPass != null) knockLowPass.cutoffFrequency = Mathf.Lerp(650f, 2600f, approach);
            nextKnock -= dt;
            if (nextKnock > 0f) return;
            nextKnock = Board.KnockInterval * UnityEngine.Random.Range(0.85f, 1.15f);
            StartCoroutine(KnockBurst(approach));
        }

        private IEnumerator KnockBurst(float approach)
        {
            int count = approach < 0.35f ? 1 : approach < 0.7f ? 2 : 3;
            for (int i = 0; i < count && Active == this; i++)
            {
                Sfx(knockSource, approach > 0.7f && heavyClip != null ? heavyClip : knockClip,
                    UnityEngine.Random.Range(0.9f, 1.02f), Mathf.Lerp(0.45f, 1f, approach));
                KnocksPlayed++;
                yield return new WaitForSeconds(0.24f);
            }
            if (approach > 0.85f && scrapeClip != null && UnityEngine.Random.value < 0.5f)
                Sfx(knockSource, scrapeClip, 0.6f, 0.5f);
        }

        private Vector3 KnockPoint(float approach)
        {
            Vector3 under = transform.position + transform.forward * StandDistance;
            under.y = floorY - DepthBelowFloor;
            Vector3 start = knockStart;
            start.y = floorY - DepthBelowFloor;
            return Vector3.Lerp(start, under, Mathf.SmoothStep(0f, 1f, approach));
        }

        // ---------------------------------------------------------------- исходы

        private IEnumerator PowerReturns()
        {
            released = true;
            // Гул сети, потом свет; через секунду кухня снова щёлкает вниз — и больше не встаёт.
            if (humSource != null && humClip != null)
            {
                humSource.clip = humClip;
                humSource.loop = true;
                humSource.Play();
            }
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                UpdateView(Time.deltaTime);
                yield return null;
            }
            kitchenDead = true;
            Sfx(panelSource, tripClip, 1f, 1f);
            Shake(0.12f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                UpdateView(Time.deltaTime);
                yield return null;
            }
        }

        // Стук дошёл: удар прямо под ногами, фонарик гаснет, в темноте дыхание и скрежет уходит под полом.
        private IEnumerator KnockReachesPanel()
        {
            Vector3 under = KnockPoint(1f);
            under.y = floorY - 0.15f;
            if (knockSource != null)
            {
                knockSource.transform.position = under;
                if (knockLowPass != null) knockLowPass.cutoffFrequency = 4000f;
                Sfx(knockSource, heavyClip, 0.8f, 1f);
                Sfx(knockSource, thudClip, 0.7f, 1f);
            }
            Shake(1.4f);
            doorTarget = -35f;
            Sfx(panelSource, doorCloseClip, 0.85f, 1f);
            bool lit = beam != null && beam.enabled;
            if (beam != null) beam.enabled = false;
            AudioClip breath = GameSounds.Pick(GameSounds.Library?.breathScared);
            if (breath != null) Sfx(panelSource, breath, 1f, 0.9f);
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                if (knockSource != null)
                    knockSource.transform.position = Vector3.Lerp(under, KnockPoint(0.4f), t / 2.2f);
                if (t > 0.6f && t - Time.deltaTime <= 0.6f && scrapeClip != null) Sfx(knockSource, scrapeClip, 0.55f, 0.9f);
                UpdateView(Time.deltaTime);
                yield return null;
            }
            if (beam != null && lit)
            {
                yield return Flicker(0.4f);
                beam.enabled = true;
            }
            released = true;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                UpdateView(Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator Flicker(float seconds)
        {
            if (beam == null) yield break;
            bool on = beam.enabled;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                beam.enabled = Mathf.PerlinNoise(Time.time * 40f, 0.3f) > 0.5f;
                yield return null;
            }
            if (beam != null) beam.enabled = on;
        }

        // ---------------------------------------------------------------- камера и игрок

        private void CaptureControl(PlayerController pc)
        {
            player = pc;
            inventory = pc.GetComponent<PlayerInventory>();
            camComponent = pc.GetComponentInChildren<Camera>();
            cam = camComponent != null ? camComponent.transform : null;
            camLocal = cam != null ? cam.localRotation : Quaternion.identity;
            baseFov = camComponent != null ? camComponent.fieldOfView : 60f;
            baseAim = cam != null ? cam.rotation : Quaternion.identity;
            sensitivity = SettingsStore.GetMouseSensitivity();
            invertY = SettingsStore.GetInvertY();
            pc.StopMotion();
            inputs = new Behaviour[] { inventory, pc.GetComponent<PlayerInteraction>(), pc.GetComponentInChildren<PlayerDoorOpen>(),
                pc.GetComponentInChildren<PickupController>(), pc.GetComponentInChildren<HeadBobbing>(), pc.GetComponent<PlayerFloorKnock>() };
            inputStates = new bool[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputStates[i] = inputs[i] != null && inputs[i].enabled;
                if (inputs[i] != null) inputs[i].enabled = false;
            }
            pc.isCameraLocked = true;

            // Фонарик в руку и узкий луч: видно только то, что в центре.
            if (inventory != null && inventory.flashlightLightSource != null)
            {
                beam = inventory.flashlightLightSource;
                equippedBefore = inventory.EquippedItemId;
                inventory.Equip("Flashlight");
                if (inventory.EquippedItemId != "Flashlight") inventory.ActivateItem("Flashlight");
                beam.enabled = true;
                beamAngle = beam.spotAngle;
                beamInner = beam.innerSpotAngle;
                beamIntensity = beam.intensity;
                beam.spotAngle = BeamAngle;
                beam.intensity = Mathf.Min(beamIntensity, BeamIntensity);
                beam.innerSpotAngle = BeamInner;
            }
        }

        // Герой подходит вплотную и поворачивается к щитку.
        private IEnumerator Approach()
        {
            if (player == null) yield break;
            Transform body = player.transform;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Vector3 from = body.position;
            Vector3 eye = cam != null ? cam.position - body.position : Vector3.up * 1.6f;
            Vector3 stand = transform.position + transform.forward * StandDistance - new Vector3(eye.x, 0f, eye.z);
            stand.y = from.y;
            Quaternion fromYaw = body.rotation;
            Quaternion toYaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(-transform.forward, Vector3.up));
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                body.position = Vector3.Lerp(from, stand, k);
                body.rotation = Quaternion.Slerp(fromYaw, toYaw, k);
                UpdateView(Time.deltaTime);
                yield return null;
            }
            body.position = stand;
            body.rotation = toYaw;
            if (cc != null) cc.enabled = true;
        }

        private Vector3 Focus => transform.position;

        private void UpdateView(float dt)
        {
            doorAngle = Mathf.MoveTowards(doorAngle, doorTarget, 260f * dt);
            for (int i = 0; i < boardGroups; i++)
            {
                bool up = Board != null && Board.IsUp(i) && !(kitchenDead && i == BreakerBoard.Faulty);
                groupAngle[i] = Mathf.MoveTowards(groupAngle[i], up ? 0f : GroupOffAngle, FlipSpeed * dt);
            }
            ApplyPose();
            if (cam == null) return;
            baseAim = Quaternion.Slerp(baseAim, Quaternion.LookRotation(Focus - cam.position), 1f - Mathf.Exp(-6f * dt));
            float time = Time.time;
            float sway = 0.12f + (Board != null ? Board.Approach * 0.5f : 0f);
            Quaternion offset = Quaternion.Euler(
                (Mathf.PerlinNoise(time * 0.8f, 0.1f) - 0.5f) * 2f * sway,
                (Mathf.PerlinNoise(0.4f, time * 0.8f) - 0.5f) * 2f * sway, 0f);
            if (shake > 0f)
            {
                shakeTime += dt;
                float a = shake * Mathf.Exp(-shakeTime * 9f);
                if (a < 0.02f) shake = 0f;
                else
                {
                    float n = shakeTime * 25f;
                    offset *= Quaternion.Euler((Mathf.PerlinNoise(n, 0.3f) - 0.5f) * 2f * a,
                        (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2f * a, 0f);
                }
            }
            if (released)
            {
                float ease = 1f - Mathf.Exp(-2.5f * dt);
                aimPitch = Mathf.Lerp(aimPitch, 0f, ease);
                aimYaw = Mathf.Lerp(aimYaw, 0f, ease);
            }
            cam.rotation = baseAim * Quaternion.Euler(aimPitch, aimYaw, 0f) * offset;
            if (camComponent != null)
                camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView, released ? baseFov : baseFov * ZoomFactor,
                    1f - Mathf.Exp(-5f * dt));
        }

        private void Shake(float degrees)
        {
            shake = Mathf.Max(shake, degrees);
            shakeTime = 0f;
        }

        private void ApplyPose()
        {
            if (wired == null)
            {
                wired = new bool[groups.Length];
                for (int i = 0; i < groups.Length; i++) wired[i] = groups[i] != null;
            }
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] != null) groups[i].localRotation = groupRest[i] * Quaternion.Euler(groupAngle[i], 0f, 0f);
            if (main != null) main.localRotation = mainRest * Quaternion.Euler(mainAngle, 0f, 0f);
            if (door != null) door.localRotation = doorRest * Quaternion.AngleAxis(doorAngle, Vector3.right);
        }

        private void Finish()
        {
            if (ui != null) Destroy(ui);
            if (camComponent != null) camComponent.fieldOfView = baseFov;
            if (cam != null)
            {
                // Взгляд остаётся на щитке: тело уже развернуто к нему, камере — прежний наклон.
                cam.localRotation = camLocal;
            }
            if (beam != null)
            {
                beam.spotAngle = beamAngle;
                beam.innerSpotAngle = beamInner;
                beam.intensity = beamIntensity;
                // Ночь: фонарик остаётся в руке и светит. Если до щитка в руке был лом/молоток — он возвращается.
                bool otherTool = !string.IsNullOrEmpty(equippedBefore) && equippedBefore != "Flashlight";
                if (otherTool && inventory != null) inventory.Equip(equippedBefore);
                else beam.enabled = true;
            }
            if (inputs != null)
                for (int i = 0; i < inputs.Length; i++) if (inputs[i] != null) inputs[i].enabled = inputStates[i];
            if (player != null) player.isCameraLocked = false;
            if (knockSource != null) Destroy(knockSource.gameObject, 3f);
            if (Active == this) Active = null;
        }

        private void OnDisable()
        {
            if (Active != this) return;
            StopAllCoroutines();
            Finish();
        }

        // ---------------------------------------------------------------- звук и UI

        private void LoadAudio()
        {
            onClips = new[] {
                Resources.Load<AudioClip>("Audio/Breaker/BreakerOn1"), Resources.Load<AudioClip>("Audio/Breaker/BreakerOn2"),
                Resources.Load<AudioClip>("Audio/Breaker/BreakerOn3"), Resources.Load<AudioClip>("Audio/Breaker/BreakerOn4"),
                Resources.Load<AudioClip>("Audio/Breaker/BreakerOn5") };
            tripClip = Resources.Load<AudioClip>("Audio/Breaker/BreakerTrip");
            mainOnClip = Resources.Load<AudioClip>("Audio/Breaker/MainOn");
            mainOffClip = Resources.Load<AudioClip>("Audio/Breaker/MainOff");
            mainTripClip = Resources.Load<AudioClip>("Audio/Breaker/MainTrip");
            doorOpenClip = Resources.Load<AudioClip>("Audio/Breaker/PanelDoorOpen");
            doorCloseClip = Resources.Load<AudioClip>("Audio/Breaker/PanelDoorClose");
            humClip = Resources.Load<AudioClip>("Audio/Breaker/MainsHum");
            heavyClip = Resources.Load<AudioClip>("Audio/Horror/impactWood_heavy_000");
            thudClip = Resources.Load<AudioClip>("Audio/Horror/impactPunch_heavy_000");
            scrapeClip = Resources.Load<AudioClip>("Audio/Horror/PlateScrape");
            var intro = FindIntro();
            otherKnocks = intro != null && intro.knockController != null
                ? intro.knockController.GetComponentsInChildren<RandomKnock>(true) : null;
            var knock = intro != null && intro.knockController != null ? intro.knockController.GetComponent<RandomKnock>() : null;
            knockClip = knock != null ? knock.ShortestClip : null;
            if (knockClip == null) knockClip = Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");
        }

        private void CreateAudio()
        {
            if (panelSource == null)
            {
                panelSource = Source("Panel Audio", transform, AudioBus.World, 0.5f, 6f);
                humSource = Source("Mains Hum", transform, AudioBus.World, 0.4f, 4f);
                humSource.volume = 0.22f;
            }
            var knockObj = new GameObject("Breaker Knock");
            knockObj.transform.position = KnockPoint(0f);
            knockSource = Source(null, knockObj.transform, AudioBus.Horror, 6f, 25f);
            knockLowPass = knockObj.AddComponent<AudioLowPassFilter>();
            knockLowPass.cutoffFrequency = 650f;
        }

        private static AudioSource Source(string name, Transform parent, AudioBus bus, float min, float max)
        {
            GameObject obj = parent.gameObject;
            if (name != null)
            {
                obj = new GameObject(name);
                obj.transform.SetParent(parent, false);
            }
            var source = obj.AddComponent<AudioSource>();
            AudioRouting.Assign(source, bus);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = min;
            source.maxDistance = max;
            return source;
        }

        private static void Sfx(AudioSource audio, AudioClip clip, float pitch, float volume)
        {
            if (audio == null || clip == null) return;
            audio.pitch = pitch * UnityEngine.Random.Range(0.97f, 1.03f);
            audio.PlayOneShot(clip, volume);
        }

        private void BuildUi()
        {
            TMP_FontAsset font = QuestManager.Instance != null && QuestManager.Instance.questUiText != null
                ? QuestManager.Instance.questUiText.font : null;
            var canvas = UiStyle.OverlayCanvas(null, "BreakerPanel", 150, new Vector2(1280f, 720f), false);
            ui = canvas.gameObject;
            var dot = UiStyle.Rect("Aim", ui.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(5f, 5f));
            dot.gameObject.AddComponent<Image>().color = new Color(UiStyle.Ivory.r, UiStyle.Ivory.g, UiStyle.Ivory.b, 0.75f);
            caption = UiStyle.Text("Label", ui.transform, font, 22f, UiStyle.Thought, TextAlignmentOptions.Center);
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = caption.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            caption.rectTransform.sizeDelta = new Vector2(400f, 34f);
            keys = UiKeyHints.Create(ui.transform, "Keys", font);
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, 70f);
            keys.Set("МЫШЬ", "СВЕТИТЬ", "ЛКМ", "ЩЁЛКНУТЬ АВТОМАТ");
        }

        private IntroSequence intro;
        private IntroSequence FindIntro() => intro != null ? intro : intro = FindFirstObjectByType<IntroSequence>();

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
