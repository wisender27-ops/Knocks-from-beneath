using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Мини-игра M3 «Гвозди» в мире: доски по одной ложатся на люк, на каждой два гвоздя. Мышь — прицел,
    // ЛКМ держать — замах, отпустить — удар. Снизу оно толкает створки (перед толчком — скрежет): чем меньше
    // забито, тем выше они подлетают и тем сильнее трещат доски; звон по металлу толкает его раньше.
    // После последнего гвоздя снизу слабо отвечает перестук хозяина. Логика — NailBoarding, здесь подача.
    public sealed class HatchNailingGame : MonoBehaviour
    {
        private const float NailLength = 0.2f;   // крупный «костыль»: в PSX-разрешении тонкий гвоздь не виден
        private const float NailInset = 0.8f;    // от центра доски к краю, м
        private const float ZoomFactor = 0.6f;
        private const float AimYaw = 24f, AimPitch = 16f;   // конус прицела вокруг доски, градусы
        private const float StrikeSeconds = 0.16f;

        public static HatchNailingGame Active { get; private set; }
        public NailBoarding Logic { get; private set; }
        public bool IsPlaying { get; private set; }
        public int OwnerKnocksPlayed { get; private set; }
        public NailStrike LastStrike { get; private set; } = NailStrike.Miss;
        public bool BaseAimSettled => cam != null && Quaternion.Angle(baseAim, FocusRotation()) < 0.5f;

        private float? requestedPower;
        private AudioSource source, ownerSource;
        private AudioClip nailClip, bendClip, slamClip, thudClip, plankClip, scrapeClip, knockClip;
        private BasementHatch hatch;
        private Transform cam, hammer, planksParent;
        private Transform[] planks = new Transform[0];
        private Quaternion hammerRest, baseAim;
        private Camera camComponent;
        private float baseFov, aimYaw, aimPitch, sensitivity, shake, shakeTime, windup = -1f, strikeTime = -1f;
        private float flicker, lampIntensity;
        private bool invertY, warned, released; // released — всё забито: прицел убран, взгляд отпущен
        private Vector3 focus;
        private readonly GameObject[] nails = new GameObject[NailBoarding.NailCount];
        private readonly Vector3[] nailTop = new Vector3[NailBoarding.NailCount];
        private readonly Vector3[] nailUp = new Vector3[NailBoarding.NailCount];
        private readonly Quaternion[] nailTilt = new Quaternion[NailBoarding.NailCount];
        private GameObject ui;
        private UiKeyHints keys;
        private Material nailMaterial;
        private Light lamp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Active = null;

        // Для проб: удар с заданной силой тем же путём, что отпущенная ЛКМ, и прицел в точку.
        public void RequestStrike(float power) => requestedPower = Mathf.Clamp01(power);

        public void AimAt(Vector3 world)
        {
            if (cam == null) return;
            Vector3 local = (Quaternion.Inverse(baseAim) * Quaternion.LookRotation(world - cam.position)).eulerAngles;
            aimPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, local.x), -AimPitch, AimPitch);
            aimYaw = Mathf.Clamp(Mathf.DeltaAngle(0f, local.y), -AimYaw, AimYaw);
        }

        public Vector3 NailHead(int nail)
        {
            if (nail < 0 || nail >= nails.Length || nails[nail] == null) return Vector3.zero;
            return nailTop[nail] + nailUp[nail] * Sticking(nail);
        }

        public IEnumerator Play(PlayerController pc, BasementHatch basementHatch, GameObject boards,
            AudioClip woodClip, Action<bool> done)
        {
            Logic = new NailBoarding();
            IsPlaying = true;
            Active = this;
            hatch = basementHatch;
            nailClip = woodClip;
            bendClip = Resources.Load<AudioClip>("Audio/Horror/impactMetal_light_002");
            slamClip = Resources.Load<AudioClip>("Audio/Horror/impactMetal_heavy_001");
            thudClip = Resources.Load<AudioClip>("Audio/Horror/impactPunch_heavy_000");
            plankClip = Resources.Load<AudioClip>("Audio/Horror/impactPlank_medium_000");
            scrapeClip = Resources.Load<AudioClip>("Audio/Horror/PlateScrape");
            knockClip = FindOwnerKnock();
            CreateAudio();
            camComponent = pc.GetComponentInChildren<Camera>();
            cam = camComponent != null ? camComponent.transform : null;
            if (cam != null) baseAim = cam.rotation;
            if (camComponent != null) baseFov = camComponent.fieldOfView;
            sensitivity = SettingsStore.GetMouseSensitivity();
            invertY = SettingsStore.GetInvertY();
            hammer = pc.GetComponent<PlayerInventory>()?.hammerInHand?.transform;
            if (hammer != null) hammerRest = hammer.localRotation;
            nailMaterial = FindNailMaterial();
            PlaceLamp(pc);

            hatch.CloseForNailing();
            for (float t = 0f; t < 1.5f && hatch.IsMoving; t += Time.deltaTime) yield return null;

            if (boards != null)
            {
                planksParent = boards.transform;
                planks = new Transform[boards.transform.childCount];
                for (int i = 0; i < planks.Length; i++)
                {
                    planks[i] = boards.transform.GetChild(i);
                    planks[i].gameObject.SetActive(false);
                }
                boards.SetActive(true);
            }
            BuildUi();

            if (planks.Length > 0) yield return DropPlank(0);
            bool secondDropped = planks.Length < 2;
            while (!Logic.Finished)
            {
                if (Time.timeScale == 0f || PauseMenuController.InputBlocked)
                {
                    windup = -1f;
                    requestedPower = null;
                    yield return null;
                    continue;
                }
                float dt = Time.deltaTime;
                HandleInput(dt);
                if (Logic.Finished) break;
                if (!warned && Logic.HeaveWarning)
                {
                    warned = true;
                    Scrape();
                }
                if (Logic.Advance(dt))
                {
                    warned = false;
                    Heave();
                }
                if (!secondDropped && Logic.IsDone(0) && Logic.IsDone(1))
                {
                    secondDropped = true;
                    yield return DropPlank(1);
                    continue;
                }
                UpdateNails();
                UpdateView(dt);
                yield return null;
            }

            bool success = Logic.Succeeded;
            UpdateNails();
            if (success) yield return OwnerAnswers();
            else
            {
                // Доски сорваны: последний толчок, створки уже не держат.
                Blow(1.6f);
                foreach (var plank in planks) plank.gameObject.SetActive(true);
                if (planksParent != null) planksParent.gameObject.SetActive(false);
                foreach (var nail in nails) if (nail != null) Destroy(nail);
                for (float t = 0f; t < 0.4f; t += Time.deltaTime)
                {
                    UpdateView(Time.deltaTime);
                    yield return null;
                }
            }
            Finish();
            done?.Invoke(success);
        }

        private void HandleInput(float dt)
        {
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity * (invertY ? -1f : 1f);
            aimYaw = Mathf.Clamp(aimYaw + Input.GetAxis("Mouse X") * sensitivity, -AimYaw, AimYaw);
            aimPitch = Mathf.Clamp(aimPitch - mouseY, -AimPitch, AimPitch);

            bool held = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
            if (strikeTime < 0f && windup < 0f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
                windup = 0f;
            if (windup >= 0f)
            {
                windup += dt;
                if (!held) Strike(NailBoarding.Power(windup));
            }
            if (requestedPower.HasValue)
            {
                Strike(requestedPower.Value);
                requestedPower = null;
            }
        }

        private void Strike(float power)
        {
            windup = -1f;
            strikeTime = 0f;
            Vector3 origin = cam.position, direction = cam.forward;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Logic.Available; i++)
            {
                if (nails[i] == null || Logic.IsDone(i)) continue;
                Vector3 toHead = NailHead(i) - origin;
                if (Vector3.Dot(toHead, direction) <= 0f) continue;
                float distance = Vector3.Cross(direction, toHead).magnitude;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            NailStrike result = best >= 0 ? Logic.Strike(best, bestDistance, power) : NailStrike.Miss;
            LastStrike = result;
            switch (result)
            {
                case NailStrike.Drove:
                    // Гвоздь входит в дерево — тон удара растёт с глубиной.
                    Play(source, nailClip, 0.9f + 0.4f * Logic.Depth(best), 0.8f + 0.2f * power);
                    Shake(0.08f + 0.12f * power);
                    break;
                case NailStrike.NailDone:
                    bool last = Logic.Succeeded;
                    Play(source, nailClip, last ? 1.2f : 1.4f, 1f);
                    // Последний гвоздь — тяжёлый глухой удар по всей доске: слышно, что всё.
                    Play(source, plankClip, last ? 0.6f : 0.8f, last ? 1f : 0.35f);
                    if (last) Play(source, thudClip, 0.75f, 0.6f);
                    Shake(last ? 0.45f : 0.2f);
                    if (Logic.NailsDone == 1 && keys != null) keys.gameObject.SetActive(false);
                    break;
                case NailStrike.Glance:
                    Play(source, bendClip, 1.15f, 0.7f);
                    nailTilt[best] = Quaternion.AngleAxis(UnityEngine.Random.Range(-9f, 9f), cam.right) *
                        Quaternion.AngleAxis(UnityEngine.Random.Range(-9f, 9f), cam.up);
                    Logic.Provoke(0.6f);
                    Shake(0.12f);
                    break;
                default:
                    bool wood = Physics.Raycast(origin, direction, out RaycastHit hit, 6f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                        planksParent != null && hit.transform.IsChildOf(planksParent);
                    if (wood) Play(source, plankClip, 0.7f, 0.55f);
                    else
                    {
                        // Звон по металлу створок — снизу слышат.
                        Play(source, bendClip, 0.55f, 1f);
                        Logic.Provoke(1.5f);
                    }
                    Shake(0.15f);
                    break;
            }
        }

        // Удар изнутри: чем меньше держат гвозди, тем выше подлетают створки.
        private void Heave()
        {
            Blow(Logic.LastLift);
            flicker = 0.25f + 0.35f * Logic.LastLift;
            if (Logic.Integrity < 0.5f) Play(source, plankClip, 0.5f, 0.9f);
        }

        private void Scrape()
        {
            Play(source, scrapeClip, 0.45f, 0.55f);
            hatch?.Jolt(0.6f);
        }

        private void Blow(float lift)
        {
            if (source != null)
            {
                float pitch = UnityEngine.Random.Range(0.82f, 0.95f);
                Play(source, slamClip, pitch, Mathf.Clamp01(0.35f + lift * 0.6f));
                Play(source, thudClip, pitch, Mathf.Clamp01(0.3f + lift * 0.5f));
            }
            hatch?.Jolt(1.5f + lift * 14f);
            Shake(0.15f + lift * 0.75f);
        }

        private void Shake(float degrees)
        {
            shake = Mathf.Max(shake, degrees);
            shakeTime = 0f;
        }

        private IEnumerator OwnerAnswers()
        {
            // Сразу видно, что всё: прицел исчезает, молоток опускается, зум уходит, взгляд поднимается
            // от досок. Потом снизу слабый знакомый перестук «2·1»: под досками остался человек.
            released = true;
            windup = -1f;
            if (ui != null) ui.SetActive(false);
            float[] taps = { 0.7f, 1.05f, 2.05f };
            int next = 0;
            for (float t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                if (next < taps.Length && t >= taps[next])
                {
                    Play(ownerSource, knockClip, 0.95f, 0.6f);
                    OwnerKnocksPlayed++;
                    next++;
                }
                UpdateView(Time.deltaTime);
                yield return null;
            }
        }

        private float Sticking(int nail) => Mathf.Lerp(NailLength, 0.012f, Logic != null ? Logic.Depth(nail) : 0f);

        private void UpdateNails()
        {
            for (int i = 0; i < nails.Length; i++)
            {
                if (nails[i] == null) continue;
                Quaternion upright = Quaternion.FromToRotation(Vector3.up, nailUp[i]);
                nails[i].transform.position = nailTop[i] - nailUp[i] * (NailLength - Sticking(i));
                nails[i].transform.rotation = Logic.IsDone(i) ? upright : nailTilt[i] * upright;
            }
        }

        private IEnumerator DropPlank(int index)
        {
            Transform plank = planks[index];
            Vector3 rest = plank.position;
            focus = rest;
            plank.position = rest + Vector3.up * 0.3f;
            plank.gameObject.SetActive(true);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                plank.position = Vector3.Lerp(rest + Vector3.up * 0.3f, rest, t * t / 0.04f);
                UpdateView(Time.deltaTime);
                yield return null;
            }
            plank.position = rest;
            Play(source, plankClip, 1f, 0.7f);
            Vector3 up = plank.up;
            Vector3 surface = rest + up * (plank.lossyScale.y * 0.5f);
            for (int side = 0; side < 2; side++)
            {
                int nail = index * 2 + side;
                if (nail >= nails.Length) break;
                nailTop[nail] = surface + plank.right * (side == 0 ? -NailInset : NailInset);
                nailUp[nail] = up;
                nailTilt[nail] = Quaternion.identity;
                nails[nail] = CreateNail(plank);
            }
            Logic.OpenNails(Mathf.Min(NailBoarding.NailCount, index * 2 + 2));
            UpdateNails();
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                UpdateView(Time.deltaTime);
                yield return null;
            }
        }

        private Quaternion FocusRotation() => Quaternion.LookRotation(focus - cam.position);

        // Взгляд = доска + прицел мышью; дрожь рук при долгом замахе и тряска — смещение поверх.
        private void UpdateView(float dt)
        {
            if (cam != null)
            {
                baseAim = Quaternion.Slerp(baseAim, FocusRotation(), 1f - Mathf.Exp(-6f * dt));
                float time = Time.time;
                float sway = 0.15f + (Logic != null && Logic.HeaveWarning ? 0.35f : 0f);
                if (windup >= 0f) sway += 0.2f + Mathf.Min(2.5f, Mathf.Max(0f, windup - 1f) * 1.6f);
                Quaternion offset = Quaternion.Euler(
                    (Mathf.PerlinNoise(time * 0.9f, 0.1f) - 0.5f) * 2f * sway,
                    (Mathf.PerlinNoise(0.4f, time * 0.9f) - 0.5f) * 2f * sway, 0f);
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
                    aimPitch = Mathf.Lerp(aimPitch, -9f, ease);
                    aimYaw = Mathf.Lerp(aimYaw, 0f, ease);
                }
                cam.rotation = baseAim * Quaternion.Euler(aimPitch, aimYaw, 0f) * offset;
                if (camComponent != null)
                    camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView, released ? baseFov : baseFov * ZoomFactor,
                        1f - Mathf.Exp(-5f * dt));
            }
            if (hammer != null)
            {
                // Замах поднимает молоток тем выше, чем дольше держишь; удар — короткий рывок вниз.
                float raise = windup >= 0f ? NailBoarding.Power(windup) : 0f;
                float down = 0f;
                if (strikeTime >= 0f)
                {
                    strikeTime += dt;
                    float k = Mathf.Clamp01(strikeTime / StrikeSeconds);
                    down = Mathf.Sin(k * Mathf.PI);
                    if (k >= 1f) strikeTime = -1f;
                }
                hammer.localRotation = hammerRest * Quaternion.Euler(-50f * raise + 25f * down, 0f, 0f);
            }
            if (lamp != null)
            {
                flicker = Mathf.Max(0f, flicker - dt);
                lamp.intensity = flicker > 0f && Mathf.PerlinNoise(Time.time * 30f, 0.5f) > 0.45f
                    ? lampIntensity * 0.15f : lampIntensity;
            }
        }

        private static void Play(AudioSource audio, AudioClip clip, float pitch, float volume)
        {
            if (audio == null || clip == null) return;
            audio.pitch = pitch * UnityEngine.Random.Range(0.97f, 1.03f);
            audio.PlayOneShot(clip, volume);
        }

        private void CreateAudio()
        {
            source = CreateSource("Nailing Audio", 0.6f);
            ownerSource = CreateSource("Owner Knock", 0.8f);
            ownerSource.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 900f;
        }

        private AudioSource CreateSource(string name, float spatial)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(hatch.transform, false);
            var audio = obj.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = spatial;
            audio.minDistance = 1.5f;
            audio.maxDistance = 18f;
            if (HorrorMomentDirector.Instance != null)
                audio.outputAudioMixerGroup = HorrorMomentDirector.Instance.OutputMixerGroup;
            return audio;
        }

        private static AudioClip FindOwnerKnock()
        {
            var intro = FindFirstObjectByType<IntroSequence>();
            var knock = intro != null && intro.knockController != null ? intro.knockController.GetComponent<RandomKnock>() : null;
            AudioClip clip = knock != null ? knock.ShortestClip : null;
            return clip != null ? clip : Resources.Load<AudioClip>("Audio/Horror/AnswerKnock");
        }

        private Material FindNailMaterial()
        {
            // Тёмный металл петли замка: на светлом дереве под фонариком читается лучше всего.
            foreach (var renderer in hatch.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "HASP") return renderer.sharedMaterial;
            return null;
        }

        private GameObject CreateNail(Transform plank)
        {
            var root = new GameObject("Nail");
            root.transform.SetParent(plank.parent, true);
            Part(root.transform, new Vector3(0f, NailLength * 0.5f, 0f), new Vector3(0.04f, NailLength * 0.5f, 0.04f));
            Part(root.transform, new Vector3(0f, NailLength, 0f), new Vector3(0.08f, 0.006f, 0.08f));
            return root;
        }

        private void Part(Transform parent, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (nailMaterial != null) part.GetComponent<Renderer>().sharedMaterial = nailMaterial;
        }

        // В руке молоток — фонарик герой кладёт рядом, лучом на люк (свет как у фонарика в руке).
        private void PlaceLamp(PlayerController pc)
        {
            if (cam == null) return;
            Light template = pc.GetComponent<PlayerInventory>()?.flashlightInHand?.GetComponentInChildren<Light>(true);
            var obj = new GameObject("Nailing Lamp");
            obj.transform.position = cam.position - Vector3.up * 0.9f - cam.right * 0.35f;
            obj.transform.rotation = Quaternion.LookRotation(hatch.transform.position - obj.transform.position);
            lamp = obj.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = 75f;
            lamp.range = 7f;
            lamp.intensity = lampIntensity = template != null ? Mathf.Max(1.5f, template.intensity) : 3f;
            lamp.color = template != null ? template.color : new Color(1f, 0.93f, 0.8f);
            lamp.shadows = LightShadows.None;
        }

        private void BuildUi()
        {
            TMP_FontAsset font = QuestManager.Instance != null && QuestManager.Instance.questUiText != null
                ? QuestManager.Instance.questUiText.font : null;
            var canvas = UiStyle.OverlayCanvas(null, "HatchNailing", 150, new Vector2(1280f, 720f), false);
            ui = canvas.gameObject;
            var dot = UiStyle.Rect("Aim", ui.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(5f, 5f));
            dot.gameObject.AddComponent<Image>().color = new Color(UiStyle.Ivory.r, UiStyle.Ivory.g, UiStyle.Ivory.b, 0.75f);
            keys = UiKeyHints.Create(ui.transform, "Keys", font);
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, 70f);
            keys.Set("МЫШЬ", "ЦЕЛИТЬСЯ", "ЛКМ", "ДЕРЖАТЬ — ЗАМАХ, ОТПУСТИТЬ — УДАР");
        }

        private void Finish()
        {
            // Фонарик остаётся лежать: финальные реплики идут при его свете.
            IsPlaying = false;
            if (Active == this) Active = null;
            if (lamp != null) lamp.intensity = lampIntensity;
            if (ui != null) Destroy(ui);
            if (hammer != null) hammer.localRotation = hammerRest;
            if (camComponent != null) camComponent.fieldOfView = baseFov;
            if (source != null) Destroy(source.gameObject, 2.5f);
            if (ownerSource != null) Destroy(ownerSource.gameObject, 2.5f);
        }

        private void OnDestroy()
        {
            if (IsPlaying) Finish();
        }
    }
}
