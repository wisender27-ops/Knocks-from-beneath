using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Мини-игра M3 «Гвозди» в мире: доски по одной ложатся на люк, на каждой два гвоздя. Гвоздь
    // качается — Space/ЛКМ, когда он стоит ровно. Снизу бьют в створки всё чаще; промах гнёт
    // гвоздь и вызывает удар сильнее. Логика — NailBoarding, здесь только подача.
    public sealed class HatchNailingGame : MonoBehaviour
    {
        private const float NailLength = 0.2f;   // крупный «костыль»: в PSX-разрешении тонкий гвоздь не виден
        private const float SwingSeconds = 0.22f;
        private const float NailInset = 0.8f;    // от центра доски к краю, м
        private const float ZoomFactor = 0.55f;

        public static HatchNailingGame Active { get; private set; }
        public NailBoarding Logic { get; private set; }
        public bool IsPlaying { get; private set; }

        private bool strikeRequested;
        private AudioSource source;
        private AudioClip nailClip, bendClip, slamClip, thudClip, plankClip;
        private BasementHatch hatch;
        private Transform cam, hammer;
        private Quaternion hammerRest;
        private Camera camComponent;
        private float baseFov, swingTime = -1f, shake, shakeTime;
        private GameObject ui;
        private RectTransform marker;
        private Material nailMaterial;
        private Light lamp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Active = null;

        // Удар тем же путём, что Space/ЛКМ (для проб и будущих привязок).
        public void RequestStrike() => strikeRequested = true;

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
            CreateAudio();
            camComponent = pc.GetComponentInChildren<Camera>();
            cam = camComponent != null ? camComponent.transform : null;
            if (camComponent != null) baseFov = camComponent.fieldOfView;
            hammer = pc.GetComponent<PlayerInventory>()?.hammerInHand?.transform;
            if (hammer != null) hammerRest = hammer.localRotation;
            nailMaterial = FindNailMaterial();
            PlaceLamp(pc);

            hatch.CloseForNailing();
            for (float t = 0f; t < 1.5f && hatch.IsMoving; t += Time.deltaTime) yield return null;

            var planks = new Transform[boards != null ? boards.transform.childCount : 0];
            for (int i = 0; i < planks.Length; i++)
            {
                planks[i] = boards.transform.GetChild(i);
                planks[i].gameObject.SetActive(false);
            }
            if (boards != null) boards.SetActive(true);
            BuildUi();

            float blowTimer = 1.4f;
            int nailsPerPlank = Mathf.Max(1, NailBoarding.NailCount / Mathf.Max(1, planks.Length));
            var spawned = new System.Collections.Generic.List<GameObject>();
            for (int p = 0; p < planks.Length && !Logic.Finished; p++)
            {
                yield return DropPlank(planks[p]);
                for (int n = 0; n < nailsPerPlank && !Logic.Finished; n++)
                {
                    int target = Logic.NailsDone + 1;
                    float side = n % 2 == 0 ? -1f : 1f;
                    while (Logic.NailsDone < target && !Logic.Finished)
                    {
                        Vector3 up = planks[p].up;
                        Vector3 top = planks[p].position + up * (planks[p].lossyScale.y * 0.5f) +
                            planks[p].right * side * (NailInset - Mathf.Min(Logic.Bent, 6) * 0.06f);
                        GameObject nail = CreateNail(planks[p], top);
                        spawned.Add(nail);
                        Vector3 view = cam != null ? cam.position - top : -planks[p].forward;
                        Vector3 axis = Vector3.Cross(up, view);
                        if (axis.sqrMagnitude < 1e-4f) axis = planks[p].forward;
                        axis.Normalize();
                        Quaternion upright = Quaternion.FromToRotation(Vector3.up, up);
                        strikeRequested = false;
                        bool bent = false;
                        while (!bent && Logic.NailsDone < target && !Logic.Finished)
                        {
                            if (Time.timeScale == 0f || PauseMenuController.InputBlocked)
                            {
                                strikeRequested = false;
                                yield return null;
                                continue;
                            }
                            float dt = Time.deltaTime;
                            bool pressed = strikeRequested || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
                            strikeRequested = false;
                            if (pressed && swingTime < 0f)
                            {
                                swingTime = 0f;
                                NailStrike result = Logic.Strike();
                                if (result == NailStrike.Bent)
                                {
                                    bent = true;
                                    nail.transform.rotation = Quaternion.AngleAxis(Mathf.Sign(Logic.Angle + 0.01f) * 68f, axis) * upright;
                                    nail.transform.position = top - up * (NailLength * 0.35f);
                                    Play(bendClip, 0.8f, 0.9f);
                                    Blow(1f);
                                    blowTimer = NextBlow();
                                }
                                else
                                {
                                    Play(nailClip, result == NailStrike.NailDone ? 0.9f : 1.08f, 1f);
                                    shake = Mathf.Max(shake, 0.6f);
                                    shakeTime = 0f;
                                }
                            }
                            if (bent) break;
                            Logic.Advance(dt);
                            blowTimer -= dt;
                            if (blowTimer <= 0f && !Logic.Finished)
                            {
                                Blow(Mathf.Lerp(0.25f, 0.75f, Logic.Pressure01));
                                blowTimer = NextBlow();
                            }
                            float sticking = Logic.NailsDone >= target ? 0.012f
                                : Mathf.Lerp(NailLength, NailLength * 0.45f, Logic.HitsOnNail / (float)Mathf.Max(1, NailBoarding.HitsPerNail - 1));
                            nail.transform.position = top - up * (NailLength - sticking);
                            nail.transform.rotation = Logic.NailsDone >= target
                                ? upright
                                : Quaternion.AngleAxis(Logic.Angle, axis) * upright;
                            UpdateView(top, dt);
                            yield return null;
                        }
                        if (bent)
                            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                            {
                                UpdateView(top, Time.deltaTime);
                                yield return null;
                            }
                    }
                }
            }

            bool success = Logic.Succeeded;
            if (!success)
            {
                // Доски выбиты: последний удар срывает их, створки уже не держат.
                Blow(1.6f);
                // Доски прячем целиком, а сами планки включаем: финал E2 показывает их заколоченными.
                foreach (var plank in planks) plank.gameObject.SetActive(true);
                if (boards != null) boards.SetActive(false);
                foreach (var nail in spawned) if (nail != null) Destroy(nail);
            }
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                UpdateView(cam != null ? cam.position + cam.forward : Vector3.zero, Time.deltaTime, false);
                yield return null;
            }
            Finish();
            done?.Invoke(success);
        }

        private float NextBlow() =>
            Mathf.Lerp(2.6f, 0.8f, Logic.Pressure01) * UnityEngine.Random.Range(0.8f, 1.2f);

        private IEnumerator DropPlank(Transform plank)
        {
            Vector3 rest = plank.position;
            plank.position = rest + Vector3.up * 0.3f;
            plank.gameObject.SetActive(true);
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                plank.position = Vector3.Lerp(rest + Vector3.up * 0.3f, rest, t * t / 0.04f);
                UpdateView(rest, Time.deltaTime);
                yield return null;
            }
            plank.position = rest;
            Play(plankClip, 1f, 0.7f);
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                UpdateView(rest, Time.deltaTime);
                yield return null;
            }
        }

        // Камера смотрит на текущий гвоздь, приближение, тряска от ударов, мах молотка.
        private void UpdateView(Vector3 lookAt, float dt, bool aim = true)
        {
            if (cam != null)
            {
                if (aim)
                {
                    Quaternion target = Quaternion.LookRotation(lookAt - cam.position);
                    cam.rotation = Quaternion.Slerp(cam.rotation, target, 1f - Mathf.Exp(-7f * dt));
                }
                if (camComponent != null)
                    camComponent.fieldOfView = Mathf.Lerp(camComponent.fieldOfView,
                        aim ? baseFov * ZoomFactor : baseFov, 1f - Mathf.Exp(-5f * dt));
                if (shake > 0f)
                {
                    shakeTime += dt;
                    float a = shake * Mathf.Exp(-shakeTime * 7f);
                    if (a < 0.02f) shake = 0f;
                    else
                    {
                        float n = shakeTime * 40f;
                        cam.rotation *= Quaternion.Euler((Mathf.PerlinNoise(n, 0.3f) - 0.5f) * 2f * a,
                            (Mathf.PerlinNoise(0.7f, n) - 0.5f) * 2f * a, 0f);
                    }
                }
            }
            if (swingTime >= 0f)
            {
                swingTime += dt;
                float k = Mathf.Clamp01(swingTime / SwingSeconds);
                if (hammer != null)
                    hammer.localRotation = hammerRest * Quaternion.Euler(-40f * Mathf.Sin(k * Mathf.PI), 0f, 0f);
                if (k >= 1f) swingTime = -1f;
            }
            if (marker != null && Logic != null)
                marker.anchoredPosition = new Vector2(Logic.Angle / NailBoarding.Amplitude * 130f, 0f);
        }

        // Удар снизу в створки: звук, створки подпрыгивают, камеру трясёт.
        private void Blow(float strength)
        {
            if (source != null)
            {
                source.pitch = UnityEngine.Random.Range(0.82f, 0.95f);
                if (slamClip != null) source.PlayOneShot(slamClip, Mathf.Clamp01(0.45f + strength * 0.45f));
                if (thudClip != null) source.PlayOneShot(thudClip, Mathf.Clamp01(0.3f + strength * 0.5f));
            }
            hatch?.Jolt(1.5f + strength * 4f);
            shake = Mathf.Max(shake, 0.6f + strength * 2.4f);
            shakeTime = 0f;
        }

        private void Play(AudioClip clip, float pitch, float volume)
        {
            if (source == null || clip == null) return;
            source.pitch = pitch * UnityEngine.Random.Range(0.96f, 1.04f);
            source.PlayOneShot(clip, volume);
        }

        private void CreateAudio()
        {
            var obj = new GameObject("Nailing Audio");
            obj.transform.SetParent(hatch.transform, false);
            source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.6f;
            source.minDistance = 1.5f;
            source.maxDistance = 18f;
            if (HorrorMomentDirector.Instance != null)
                source.outputAudioMixerGroup = HorrorMomentDirector.Instance.OutputMixerGroup;
        }

        private Material FindNailMaterial()
        {
            // Тёмный металл петли замка: на светлом дереве под фонариком читается лучше всего.
            foreach (var renderer in hatch.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "HASP") return renderer.sharedMaterial;
            return null;
        }

        private GameObject CreateNail(Transform plank, Vector3 top)
        {
            var root = new GameObject("Nail");
            root.transform.SetParent(plank.parent, true);
            root.transform.position = top;
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
            lamp.intensity = template != null ? Mathf.Max(1.5f, template.intensity) : 3f;
            lamp.color = template != null ? template.color : new Color(1f, 0.93f, 0.8f);
            lamp.shadows = LightShadows.None;
        }

        private void BuildUi()
        {
            TMP_FontAsset font = QuestManager.Instance != null && QuestManager.Instance.questUiText != null
                ? QuestManager.Instance.questUiText.font : null;
            var canvas = UiStyle.OverlayCanvas(null, "HatchNailing", 150, new Vector2(1280f, 720f), false);
            ui = canvas.gameObject;
            var gauge = UiStyle.Rect("Gauge", ui.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(268f, 22f));
            UiStyle.Fill(gauge, "Back", UiStyle.Ink);
            var zone = UiStyle.Rect("Upright", gauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(260f * NailBoarding.UprightAngle / NailBoarding.Amplitude, 14f));
            zone.gameObject.AddComponent<Image>().color = UiStyle.Amber;
            marker = UiStyle.Rect("Marker", gauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(4f, 26f));
            marker.gameObject.AddComponent<Image>().color = UiStyle.Ivory;
            var keys = UiKeyHints.Create(ui.transform, "Keys", font);
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, 70f);
            keys.Set("ПРОБЕЛ", "УДАРИТЬ, КОГДА ГВОЗДЬ РОВНО");
        }

        private void Finish()
        {
            IsPlaying = false;
            if (Active == this) Active = null;
            // Фонарик остаётся лежать: финальные реплики идут при его свете.
            if (ui != null) Destroy(ui);
            if (hammer != null) hammer.localRotation = hammerRest;
            if (camComponent != null) camComponent.fieldOfView = baseFov;
            if (source != null) Destroy(source.gameObject, 2.5f);
        }

        private void OnDestroy()
        {
            if (IsPlaying) Finish();
        }
    }
}
