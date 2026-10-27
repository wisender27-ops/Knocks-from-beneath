using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Дверь, которая на плёнке стоит иначе, чем в доме сейчас: закрытое положение снимается при загрузке сцены,
    // пока её никто не трогал.
    public readonly struct TapeDoor
    {
        public readonly Transform Transform;
        public readonly Quaternion Closed;
        public readonly float Swing; // как Door.angleRotation этой двери
        public readonly bool Draft;  // false — входная дверь, true — жалюзийная (сквозняк)

        public TapeDoor(Transform transform, float swing, bool draft)
        {
            Transform = transform;
            Closed = transform.localRotation;
            Swing = swing;
            Draft = draft;
        }

        public float Angle(float time) => Swing * (Draft ? NightTape.DraftOpen(time) : NightTape.DoorOpen(time));
    }

    // C-02: ночная запись — живая сцена в движке. Своя камера стоит в объективе видеокамеры на штативе и рисует
    // в RenderTexture; только для неё (beginCameraRendering → endCameraRendering) в доме ночь, монстр идёт по
    // сценарию NightTape, входная дверь открыта, а герой и сама видеокамера не видны. Камера игрока в это время
    // видит обычный дом. Время плёнки задаётся снаружи (Seek/Playing), поэтому перемотка C-03 — тот же кадр.
    public sealed class NightRecording : MonoBehaviour
    {
        public const int Width = 320, Height = 240;
        private const float FieldOfView = 54f;
        private const float FrontDoorSwing = -90f, DraftDoorSwing = 90f; // Door.angleRotation этих дверей в Main
        private static readonly Color NightAmbient = new Color(0.008f, 0.01f, 0.018f); // как ночью в SkyboxSwitcher
        private static readonly Vector3 LookShares = new Vector3(0.3f, 0.3f, 0.4f);

        private Camera tapeCamera;
        private Transform view;
        private TextMeshProUGUI stamp;
        private int stampClock = -1;

        private GameObject figure;
        private Renderer[] figureRenderers;
        private Transform chest, neck, head;
        private Vector3 faceLocal = Vector3.forward;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable walkPlayable, standPlayable;
        private float walkSecondsPerMeter;
        private bool figureVisible;

        private TapeDoor[] doors;
        private Quaternion[] doorSaved;

        private Light[] sceneLights;
        private bool[] sceneLightStates;
        private Light[] tapeLights;
        private Renderer[] hiddenOnTape;
        private bool[] hiddenStates;
        private SphericalHarmonicsL2 nightProbe, savedProbe;
        private bool savedFog;
        private float savedReflection;
        private bool rendering, subscribed;

        private AudioSource hiss, room, outside, events;
        private AudioClip knockClip, glitchClip;
        private int lastStep = -1, lastCreak = -1;

        public RenderTexture Texture { get; private set; }
        public float Time { get; private set; }
        public bool Playing { get; set; }
        public bool Finished => Time >= NightTape.Duration;
        public float SignalLoss => NightTape.SignalLoss(Time);
        public float Tracking => NightTape.Tracking(Time);

        // Вызывать при загрузке сцены (IntroSequence.Awake): двери ещё в закрытом положении.
        public static TapeDoor[] CaptureDoors(Door frontDoor)
        {
            var list = new System.Collections.Generic.List<TapeDoor>(2);
            if (frontDoor != null) list.Add(new TapeDoor(frontDoor.transform, FrontDoorSwing, false));
            Door draft = null;
            float best = 0.36f; // 60 см
            foreach (Door door in FindObjectsByType<Door>(FindObjectsInactive.Exclude))
            {
                float d = (door.transform.position - NightTape.DraftDoorPoint).sqrMagnitude;
                Renderer r = door.GetComponentInChildren<Renderer>();
                if (r != null) d = Mathf.Min(d, (r.bounds.center - NightTape.DraftDoorPoint).sqrMagnitude);
                if (d < best) { best = d; draft = door; }
            }
            if (draft != null && draft != frontDoor) list.Add(new TapeDoor(draft.transform, DraftDoorSwing, true));
            return list.ToArray();
        }

        // view — точка объектива (CameraPlacementController.RecordingView).
        public static NightRecording Create(Transform view, TapeDoor[] doors)
        {
            if (view == null) return null;
            var root = new GameObject("Night Recording (C-02)");
            var recording = root.AddComponent<NightRecording>();
            recording.Build(view, doors);
            return recording;
        }

        public void Seek(float time)
        {
            Time = NightTape.Clamp(time);
            Pose();
        }

        private void Build(Transform recordingView, TapeDoor[] tapeDoors)
        {
            view = recordingView;
            doors = tapeDoors ?? new TapeDoor[0];
            doorSaved = new Quaternion[doors.Length];
            nightProbe = new SphericalHarmonicsL2();
            nightProbe.AddAmbientLight(NightAmbient);
            BuildCamera();
            BuildStamp();
            BuildFigure();
            BuildLights();
            BuildAudio();
            CollectSceneState();
            Pose();
        }

        private void BuildCamera()
        {
            Texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "NightRecordingRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var cameraObject = new GameObject("Tape Camera");
            cameraObject.transform.SetParent(transform, false);
            tapeCamera = cameraObject.AddComponent<Camera>();
            Camera player = Camera.main;
            if (player != null) tapeCamera.CopyFrom(player);
            tapeCamera.targetTexture = Texture;
            tapeCamera.clearFlags = CameraClearFlags.SolidColor;
            tapeCamera.backgroundColor = Color.black;
            tapeCamera.fieldOfView = FieldOfView;
            tapeCamera.nearClipPlane = 0.05f;
            tapeCamera.farClipPlane = 60f;
            tapeCamera.allowMSAA = false;
            tapeCamera.depth = -10f;
            tapeCamera.cullingMask |= 1 << 5; // слой UI — отпечаток времени
            FollowView();
        }

        private void BuildStamp()
        {
            var canvasObject = new GameObject("Burned-in Stamp", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.layer = 5;
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = tapeCamera;
            canvas.planeDistance = 0.2f;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            var textObject = new GameObject("Stamp", typeof(RectTransform));
            textObject.layer = 5;
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 14f);
            rect.sizeDelta = new Vector2(160f, 24f);
            stamp = textObject.AddComponent<TextMeshProUGUI>();
            stamp.font = TapeFont.Get();
            stamp.fontSize = 19f;
            stamp.alignment = TextAlignmentOptions.BottomRight;
            stamp.color = new Color(1f, 0.97f, 0.88f);
            stamp.raycastTarget = false;
        }

        private void BuildFigure()
        {
            var prefab = Resources.Load<GameObject>("Scares/CornerPeekMonster");
            if (prefab == null) return;
            figure = Instantiate(prefab, transform);
            figure.name = "Taped Figure";
            CornerStalkerRig rig = figure.GetComponent<CornerStalkerRig>();
            AnimationClip walk = rig != null ? rig.WalkClip : null;
            AnimationClip stand = rig != null ? rig.StandClip : null;
            float walkSpeed = rig != null ? rig.WalkClipSpeed : 0.75f;
            if (rig != null) DestroyImmediate(rig);
            figure.SetActive(true); // префаб хранится выключенным
            foreach (var col in figure.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var body in figure.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            figureRenderers = figure.GetComponentsInChildren<Renderer>(true);
            SetForcedOff(figureRenderers, true);

            Animator animator = figure.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman || walk == null || stand == null) return;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) faceLocal = Quaternion.Inverse(head.rotation) * figure.transform.forward;

            float natural = walk.averageSpeed.magnitude;
            if (natural < 0.1f) natural = Mathf.Max(0.1f, walkSpeed);
            walkSecondsPerMeter = 1f / natural;
            // Ручное обновление: позу задаёт время плёнки, а не игровые часы.
            graph = PlayableGraph.Create("Night Recording Figure");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Body", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            walkPlayable = AnimationClipPlayable.Create(graph, walk);
            standPlayable = AnimationClipPlayable.Create(graph, stand);
            walkPlayable.SetApplyFootIK(true);
            standPlayable.SetApplyFootIK(true);
            mixer.ConnectInput(0, walkPlayable, 0, 0f);
            mixer.ConnectInput(1, standPlayable, 0, 1f);
            graph.Play();
        }

        private void BuildLights()
        {
            var lights = new GameObject("Tape Night Lights");
            lights.transform.SetParent(transform, false);
            // Луна — копия ночного направленного света сцены (SkyboxSwitcher.lightNight).
            Light moon = NewLight(lights.transform, "Moon", LightType.Directional);
            SkyboxSwitcher sky = FindFirstObjectByType<SkyboxSwitcher>();
            Light night = sky != null && sky.lightNight != null ? sky.lightNight.GetComponentInChildren<Light>(true) : null;
            if (night != null)
            {
                moon.transform.rotation = night.transform.rotation;
                moon.color = night.color;
                moon.intensity = night.intensity;
                moon.shadows = night.shadows;
            }
            else
            {
                moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
                moon.color = new Color(0.55f, 0.65f, 0.9f);
                moon.intensity = 0.15f;
                moon.shadows = LightShadows.Soft;
            }
            // Фонарь с улицы: светит в прихожую через входную дверь — пока она закрыта, его держит створка.
            Light street = NewLight(lights.transform, "Street Lamp", LightType.Spot);
            street.transform.position = new Vector3(-39.3f, 2.9f, 120.5f);
            street.transform.rotation = Quaternion.LookRotation(new Vector3(-42.6f, 0.8f, 115.3f) - street.transform.position);
            street.color = new Color(0.62f, 0.72f, 1f);
            street.intensity = 6f;
            street.range = 12f;
            street.spotAngle = 70f;
            street.shadows = LightShadows.Soft;
            // Слабый отсвет окна гостиной — чтобы силуэт читался в «шуме» камеры, а не тонул в черноте.
            Light window = NewLight(lights.transform, "Window Spill", LightType.Point);
            window.transform.position = new Vector3(-44.2f, 2.3f, 115.8f);
            window.color = new Color(0.5f, 0.58f, 0.8f);
            window.intensity = 0.6f;
            window.range = 6f;
            window.shadows = LightShadows.None;
            // Лампа у лестницы, оставленная на ночь: тёплое пятно у телефона и шкафа — там оно и встанет.
            Light lamp = NewLight(lights.transform, "Hall Lamp", LightType.Point);
            lamp.transform.position = new Vector3(-42.9f, 3.2f, 111.9f);
            lamp.color = new Color(1f, 0.78f, 0.52f);
            lamp.intensity = 0.9f;
            lamp.range = 7f;
            lamp.shadows = LightShadows.Soft;
            tapeLights = new[] { moon, street, window, lamp };
        }

        private static Light NewLight(Transform parent, string name, LightType type)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Light light = obj.AddComponent<Light>();
            light.type = type;
            light.enabled = false; // горит только для камеры плёнки
            return light;
        }

        private void BuildAudio()
        {
            GameSoundLibrary lib = GameSounds.Library;
            hiss = Loop("Tape Hiss", Resources.Load<AudioClip>("Audio/Recording/TapeHiss"), 0.22f);
            room = Loop("Taped Room Tone", lib != null ? lib.roomTone : null, 0.45f);
            outside = Loop("Taped Night Outside", lib != null ? lib.nightOutside : null, 0f);
            events = AudioRouting.Create(transform, "Taped Sounds", AudioBus.World, 0f);
            events.playOnAwake = false;
            // Звук с микрофона камеры: глухой, без верха.
            events.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3600f;
            room.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3600f;
            outside.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3000f;
            RandomKnock knock = FindFirstObjectByType<RandomKnock>(FindObjectsInactive.Include);
            knockClip = knock != null ? knock.ShortestClip : null;
            if (knockClip == null && lib != null && lib.subHits.Length > 0) knockClip = lib.subHits[0];
            glitchClip = Resources.Load<AudioClip>("Audio/Recording/TrackingGlitch");
        }

        private AudioSource Loop(string name, AudioClip clip, float volume)
        {
            AudioSource source = AudioRouting.Create(transform, name, AudioBus.World, 0f);
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = volume;
            return source;
        }

        // Что в доме не должно попасть на плёнку ночи 1: герой (днём он может стоять в кадре) и сама видеокамера
        // со штативом. Плюс все источники света дома — ночью свет погашен, светят только свои.
        private void CollectSceneState()
        {
            Light[] all = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            int count = 0;
            foreach (Light light in all)
                if (!IsTapeLight(light)) all[count++] = light;
            sceneLights = new Light[count];
            System.Array.Copy(all, sceneLights, count);
            sceneLightStates = new bool[count];

            var hidden = new System.Collections.Generic.List<Renderer>();
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) hidden.AddRange(player.GetComponentsInChildren<Renderer>(true));
            if (view != null) hidden.AddRange(view.root.GetComponentsInChildren<Renderer>(true));
            hiddenOnTape = hidden.ToArray();
            hiddenStates = new bool[hiddenOnTape.Length];
        }

        private bool IsTapeLight(Light light)
        {
            foreach (Light own in tapeLights)
                if (own == light) return true;
            return false;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                RenderPipelineManager.beginCameraRendering -= BeforeCamera;
                RenderPipelineManager.endCameraRendering -= AfterCamera;
                subscribed = false;
            }
            Restore();
            if (hiss != null) hiss.Stop();
            if (room != null) room.Stop();
            if (outside != null) outside.Stop();
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (Texture != null)
            {
                if (tapeCamera != null) tapeCamera.targetTexture = null;
                Texture.Release();
                Destroy(Texture);
            }
        }

        private void Update()
        {
            if (!Playing) { SetLoops(false); return; }
            float previous = Time;
            Time = NightTape.Clamp(Time + UnityEngine.Time.deltaTime);
            // Звуки — только при обычном ходе плёнки; прыжок перемотки их не повторяет.
            if (Time > previous && Time - previous < 0.25f) FireEvents(previous, Time);
            SetLoops(!Finished);
        }

        private void LateUpdate()
        {
            FollowView();
            Pose();
        }

        private void FollowView()
        {
            if (view == null || tapeCamera == null) return;
            tapeCamera.transform.SetPositionAndRotation(view.position, view.rotation);
        }

        private void Pose()
        {
            int clock = NightTape.ClockSeconds(Time);
            if (stamp != null && clock != stampClock)
            {
                stampClock = clock;
                stamp.text = NightTape.Stamp(Time);
            }
            if (outside != null) outside.volume = 0.3f * NightTape.DoorOpen(Time) / NightTape.DoorOpenAmount;
            if (figure == null) return;
            TapeFigurePose pose = NightTape.Figure(Time);
            figureVisible = pose.Visible;
            if (!pose.Visible) return;
            figure.transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0f, pose.Yaw, 0f));
            if (!graph.IsValid()) return;
            walkPlayable.SetTime(pose.Distance * walkSecondsPerMeter);
            standPlayable.SetTime(Time);
            mixer.SetInputWeight(0, pose.Walk);
            mixer.SetInputWeight(1, 1f - pose.Walk);
            graph.Evaluate(0f);
            if (tapeCamera != null)
                CornerStalkerRig.AimHead(chest, neck, head, faceLocal, tapeCamera.transform.position, pose.Look, LookShares, 150f, 0f);
        }

        private void FireEvents(float from, float to)
        {
            Vector3 mic = view != null ? view.position : transform.position;
            foreach (TapeEvent e in NightTape.SoundEvents)
            {
                if (e.Time <= from) continue;
                if (e.Time > to) break;
                AudioClip clip = Clip(e.Sound);
                if (clip == null) continue;
                float volume = e.Volume;
                if (e.Sound != TapeSound.Glitch) volume *= Mathf.Clamp01(2.4f / (Vector3.Distance(mic, e.Position) + 0.8f));
                events.pitch = e.Sound == TapeSound.Knock ? 0.8f : Random.Range(0.95f, 1.05f);
                events.PlayOneShot(clip, volume);
            }
        }

        private AudioClip Clip(TapeSound sound)
        {
            GameSoundLibrary lib = GameSounds.Library;
            switch (sound)
            {
                case TapeSound.Knock: return knockClip;
                case TapeSound.Glitch: return glitchClip;
            }
            if (lib == null) return null;
            switch (sound)
            {
                case TapeSound.HouseCreak: return GameSounds.Pick(lib.houseCreaks, ref lastCreak);
                case TapeSound.DoorCreak: return GameSounds.Pick(lib.doorCreaks);
                case TapeSound.DoorClose: return GameSounds.Pick(lib.doorClose);
                case TapeSound.StepWood: return GameSounds.Pick(lib.stepWood, ref lastStep);
                case TapeSound.StepStairs: return GameSounds.Pick(lib.stepStairs, ref lastStep);
                default: return null;
            }
        }

        private void SetLoops(bool on)
        {
            Toggle(hiss, on);
            Toggle(room, on);
            Toggle(outside, on);
        }

        private static void Toggle(AudioSource source, bool on)
        {
            if (source == null || source.clip == null) return;
            if (on && !source.isPlaying) source.Play();
            else if (!on && source.isPlaying) source.Pause();
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != tapeCamera || rendering) return;
            rendering = true;
            for (int i = 0; i < sceneLights.Length; i++)
            {
                Light light = sceneLights[i];
                if (light == null) continue;
                sceneLightStates[i] = light.enabled;
                light.enabled = false;
            }
            foreach (Light light in tapeLights) light.enabled = true;
            savedProbe = RenderSettings.ambientProbe;
            savedFog = RenderSettings.fog;
            savedReflection = RenderSettings.reflectionIntensity;
            RenderSettings.ambientProbe = nightProbe;
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = 0.2f;
            for (int i = 0; i < doors.Length; i++)
            {
                Transform door = doors[i].Transform;
                if (door == null) continue;
                doorSaved[i] = door.localRotation;
                door.localRotation = Quaternion.AngleAxis(doors[i].Angle(Time), Vector3.up) * doors[i].Closed;
            }
            for (int i = 0; i < hiddenOnTape.Length; i++)
            {
                if (hiddenOnTape[i] == null) continue;
                hiddenStates[i] = hiddenOnTape[i].forceRenderingOff;
                hiddenOnTape[i].forceRenderingOff = true;
            }
            SetForcedOff(figureRenderers, !figureVisible);
        }

        private void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == tapeCamera) Restore();
        }

        private void Restore()
        {
            if (!rendering) return;
            rendering = false;
            for (int i = 0; i < sceneLights.Length; i++)
                if (sceneLights[i] != null) sceneLights[i].enabled = sceneLightStates[i];
            foreach (Light light in tapeLights)
                if (light != null) light.enabled = false;
            RenderSettings.ambientProbe = savedProbe;
            RenderSettings.fog = savedFog;
            RenderSettings.reflectionIntensity = savedReflection;
            for (int i = 0; i < doors.Length; i++)
                if (doors[i].Transform != null) doors[i].Transform.localRotation = doorSaved[i];
            for (int i = 0; i < hiddenOnTape.Length; i++)
                if (hiddenOnTape[i] != null) hiddenOnTape[i].forceRenderingOff = hiddenStates[i];
            SetForcedOff(figureRenderers, true);
        }

        private static void SetForcedOff(Renderer[] renderers, bool off)
        {
            if (renderers == null) return;
            foreach (Renderer renderer in renderers)
                if (renderer != null) renderer.forceRenderingOff = off;
        }
    }

    // Шрифт экранного меню видеомагнитофона и отпечатка времени (VT323, OFL) — TMP-ассет в Resources.
    public static class TapeFont
    {
        private static TMP_FontAsset font;
        private static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { font = null; loaded = false; }

        public static TMP_FontAsset Get()
        {
            if (!loaded) { font = Resources.Load<TMP_FontAsset>("Fonts/VT323 SDF"); loaded = true; }
            return font != null ? font : UiStyle.Font(null);
        }
    }
}
