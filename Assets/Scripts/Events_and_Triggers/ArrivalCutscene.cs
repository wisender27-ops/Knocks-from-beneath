using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Начало игры: чёрный экран с предысторией, затем катсцена — такси привозит героя
    // к новому дому, камера переходит в первое лицо, и дальше обычная игра.
    //
    // Камера катсцены — это сама камера игрока (с её PSX-эффектами): на время сцены
    // управление отключается, а камера ставится в точки съёмки. В конце она плавно
    // переезжает в глаза героя, стоящего у такси, и управление возвращается.
    [DisallowMultipleComponent]
    public sealed class ArrivalCutscene : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }

        [Header("Предыстория")]
        public TMP_FontAsset font;
        [TextArea(2, 4)]
        public string[] storyPages =
        {
            "Из старого дома мне пришлось уехать.\nБыстро. Почти без вещей.",
            "Что там случилось — не важно.\nВажно, что возвращаться туда я не собираюсь.",
            "Новый дом нашёлся почти сразу.\nСемья продавала его срочно — и почти даром.",
            "Цена смутила.\nСлишком дёшево для двух этажей, гаража и участка.",
            "Но кто в моём положении отказался бы\nот такого дома за такие деньги?",
            "Я подписал бумаги, не задавая лишних вопросов.",
        };

        [Header("Такси")]
        public Transform taxi;
        public Transform[] arrivalPath;
        public Transform[] departurePath;
        [Min(.5f)] public float driveSpeed = 9f;

        [Header("Съёмка")]
        [Tooltip("Общий план: камера стоит здесь и следит за подъезжающим такси.")]
        public Transform establishingShot;
        [Tooltip("Куда смотрит общий план, кроме самого такси (обычно дом).")]
        public Transform houseFocus;
        [Tooltip("Второй план у такси: отсюда камера переезжает в глаза героя.")]
        public Transform curbShot;
        [Tooltip("Где герой стоит после того, как вышел из такси (смотрит по оси Z точки).")]
        public Transform playerStart;

        [Header("Звук")]
        public AudioClip engineLoop;
        public AudioClip doorOpenClip;
        public AudioClip doorCloseClip;
        public AudioClip trunkOpenClip;
        public AudioClip trunkCloseClip;

        private PlayerController player;
        private Camera view;
        private Transform cameraParent;
        private Behaviour[] inputs;
        private bool[] inputStates;
        private bool captured, cameraWasLocked, cameraDetached;
        private Vector3 cameraLocalPosition;
        private Quaternion cameraLocalRotation;
        private float cameraFov;
        private AudioSource engine, effects;
        private StoryIntroScreen screen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaying() => IsPlaying = false;

        private void Awake()
        {
            if (taxi != null)
            {
                engine = CreateSource("Taxi engine", engineLoop, true);
                effects = CreateSource("Taxi effects", null, false);
                taxi.gameObject.SetActive(false);
            }
        }

        public void Play(Action onFinished)
        {
            player = FindAnyObjectByType<PlayerController>();
            view = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
            if (player == null || view == null || taxi == null || playerStart == null ||
                establishingShot == null || curbShot == null)
            {
                onFinished?.Invoke();
                return;
            }
            StartCoroutine(Run(onFinished));
        }

        private IEnumerator Run(Action onFinished)
        {
            CaptureControl();
            screen = StoryIntroScreen.Create(font);
            try
            {
                yield return screen.Show(storyPages);

                // Общий план: такси въезжает на улицу и останавливается у дома.
                taxi.gameObject.SetActive(true);
                engine.volume = 0f;
                engine.Play();
                List<Vector3> arrival = Points(arrivalPath, null);
                taxi.position = arrival[0];
                if (arrival.Count > 1) taxi.rotation = Quaternion.LookRotation(Flat(arrival[1] - arrival[0]), Vector3.up);
                // Камеру ставим в точку съёмки ещё под чёрным экраном, мгновенно (без
                // плавного довода), иначе в начале проявления виден случайный ракурс.
                Vector3 shotStart = establishingShot.position;
                Vector3 shotEnd = shotStart + establishingShot.forward * 2.5f;
                float shotTime = 0f;
                DetachCamera();
                AimEstablishing(shotStart, true);
                StartCoroutine(screen.FadeOut(2.2f));
                StartCoroutine(FadeEngine(.7f, 2.5f));
                IEnumerator drive = Drive(arrival, driveSpeed);
                while (drive.MoveNext())
                {
                    shotTime += Time.deltaTime;
                    AimEstablishing(Vector3.Lerp(shotStart, shotEnd, Mathf.SmoothStep(0f, 1f, shotTime / 9f)));
                    yield return drive.Current;
                }
                for (float t = 0f; t < 1.2f; t += Time.deltaTime)
                {
                    AimEstablishing(Vector3.Lerp(shotStart, shotEnd, Mathf.SmoothStep(0f, 1f, (shotTime + t) / 9f)));
                    yield return null;
                }

                // Склейка через короткое затемнение — план у бордюра.
                yield return screen.FadeIn(.45f);
                view.transform.SetPositionAndRotation(curbShot.position, curbShot.rotation);
                StartCoroutine(FadeEngine(.45f, .4f));
                yield return screen.FadeOut(.6f);

                PlayEffect(doorOpenClip, .9f);
                yield return new WaitForSeconds(.9f);
                PlaceHeroOnSidewalk();
                ProtagonistBody.ShowToViewCamera = true;
                PlayEffect(doorCloseClip, 1f);
                yield return new WaitForSeconds(.8f);
                PlayEffect(trunkOpenClip, .8f);
                yield return new WaitForSeconds(1.4f);
                PlayEffect(trunkCloseClip, .9f);
                yield return new WaitForSeconds(.6f);

                // Камера переезжает в глаза героя — дальше первое лицо.
                yield return GlideToFirstPerson(2.6f);
                RestoreControl();
                StartCoroutine(DriveAway());
            }
            finally
            {
                RestoreControl();
                if (screen != null) Destroy(screen.gameObject);
            }
            onFinished?.Invoke();
        }

        // snap — выставить ракурс сразу, без плавного довода.
        private void AimEstablishing(Vector3 position, bool snap = false)
        {
            Vector3 focus = houseFocus != null ? houseFocus.position : taxi.position;
            // Камера в основном держит дом, но ведёт такси, пока то едет по улице.
            Vector3 target = Vector3.Lerp(focus, taxi.position + Vector3.up * .8f, .45f);
            Quaternion aim = Quaternion.LookRotation(target - position, Vector3.up);
            view.transform.position = position;
            view.transform.rotation = snap ? aim
                : Quaternion.Slerp(view.transform.rotation, aim, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
        }

        // Камера по умолчанию — ребёнок игрока (через CameraOffset). Когда герой переносится
        // на тротуар посреди сцены, камера уезжала бы вместе с ним. На время катсцены
        // отцепляем её от игрока; RestoreControl вернёт на место.
        private void DetachCamera()
        {
            if (cameraDetached) return;
            view.transform.SetParent(null, true);
            cameraDetached = true;
        }

        private void PlaceHeroOnSidewalk()
        {
            var capsule = player.GetComponent<CharacterController>();
            // Корень игрока стоит у ног (капсула: центр = высота/2, см. PlayerController).
            float footOffset = capsule != null ? -(capsule.center.y - capsule.height * .5f) : 0f;
            Vector3 ground = playerStart.position;
            if (Physics.Raycast(ground + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            Vector3 forward = Flat(playerStart.forward);
            if (capsule != null) capsule.enabled = false;
            player.transform.SetPositionAndRotation(ground + Vector3.up * (footOffset + .02f),
                Quaternion.LookRotation(forward, Vector3.up));
            if (capsule != null) capsule.enabled = true;
            Physics.SyncTransforms();
        }

        private IEnumerator GlideToFirstPerson(float duration)
        {
            Vector3 fromPosition = view.transform.position;
            Quaternion fromRotation = view.transform.rotation;
            float fromFov = view.fieldOfView;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                // Глаза героя пересчитываются каждый кадр — игрок уже стоит на месте.
                Vector3 eye = cameraParent != null ? cameraParent.TransformPoint(cameraLocalPosition) : cameraLocalPosition;
                Quaternion look = cameraParent != null ? cameraParent.rotation * cameraLocalRotation : cameraLocalRotation;
                view.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, eye, k), Quaternion.Slerp(fromRotation, look, k));
                view.fieldOfView = Mathf.Lerp(fromFov, cameraFov, k);
                yield return null;
            }
        }

        private IEnumerator DriveAway()
        {
            yield return new WaitForSeconds(.4f);
            StartCoroutine(FadeEngine(.8f, 1.2f));
            List<Vector3> path = Points(departurePath, taxi.position);
            yield return Drive(path, driveSpeed);
            yield return FadeEngine(0f, 1f);
            engine.Stop();
            taxi.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------------
        // Управление игроком
        // ---------------------------------------------------------------------

        private void CaptureControl()
        {
            if (captured) return;
            // Значения капсулы и камеры в сцене — редакторские (центр 1.6, высота 2). В игре
            // PlayerController сразу пересчитывает их: центр = высота/2, камера на
            // standingHeight - 0.2. Пока игрок выключен, этот пересчёт не идёт, и героя
            // ставили бы по старой капсуле — на 0.6 м ниже земли, а при возврате управления
            // капсула «проваливалась». Приводим всё к игровым значениям заранее.
            var capsule = player.GetComponent<CharacterController>();
            if (capsule != null)
            {
                capsule.height = player.standingHeight;
                capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
            }
            cameraParent = view.transform.parent;
            if (cameraParent != null)
            {
                Vector3 offset = cameraParent.localPosition;
                offset.y = player.standingHeight - .2f;
                cameraParent.localPosition = offset;
                Vector3 local = view.transform.localPosition;
                local.y = 0f;
                view.transform.localPosition = local;
            }
            cameraLocalPosition = view.transform.localPosition;
            cameraLocalRotation = view.transform.localRotation;
            cameraFov = view.fieldOfView;
            cameraWasLocked = player.isCameraLocked;
            inputs = new Behaviour[]
            {
                player, player.GetComponent<PlayerInteraction>(), player.GetComponent<PlayerInventory>(),
                player.GetComponent<PickupController>(), player.GetComponentInChildren<PlayerDoorOpen>(),
                view.GetComponentInParent<HeadBobbing>()
            };
            inputStates = new bool[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputStates[i] = inputs[i] != null && inputs[i].enabled;
                if (inputs[i] != null) inputs[i].enabled = false;
            }
            player.StopMotion();
            player.isCameraLocked = true;
            captured = true;
            IsPlaying = true;
        }

        private void RestoreControl()
        {
            if (!captured) return;
            captured = false;
            ProtagonistBody.ShowToViewCamera = false;
            if (cameraDetached)
            {
                view.transform.SetParent(cameraParent, false);
                cameraDetached = false;
            }
            view.transform.localPosition = cameraLocalPosition;
            view.transform.localRotation = cameraLocalRotation;
            view.fieldOfView = cameraFov;
            for (int i = 0; i < inputs.Length; i++) if (inputs[i] != null) inputs[i].enabled = inputStates[i];
            player.isCameraLocked = cameraWasLocked;
            IsPlaying = false;
        }

        private void OnDisable() => RestoreControl();

        // ---------------------------------------------------------------------
        // Езда и звук
        // ---------------------------------------------------------------------

        private IEnumerator Drive(List<Vector3> points, float speed)
        {
            if (points.Count < 2) yield break;
            List<Vector3> samples = SamplePath(points);
            var distances = new float[samples.Count];
            for (int i = 1; i < samples.Count; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
            float length = distances[samples.Count - 1];
            float travelled = 0f, current = speed * .6f;
            const float accel = 3f;
            int segment = 0;
            while (travelled < length)
            {
                float remaining = length - travelled;
                float target = Mathf.Min(speed, Mathf.Sqrt(2f * accel * remaining) + .2f);
                current = Mathf.MoveTowards(current, target, accel * Time.deltaTime);
                travelled += Mathf.Min(current * Time.deltaTime, remaining);
                while (segment < samples.Count - 2 && distances[segment + 1] < travelled) segment++;
                float span = Mathf.Max(.0001f, distances[segment + 1] - distances[segment]);
                taxi.position = Vector3.Lerp(samples[segment], samples[segment + 1], (travelled - distances[segment]) / span);
                Vector3 tangent = Flat(samples[segment + 1] - samples[segment]);
                if (tangent.sqrMagnitude > .0001f)
                    taxi.rotation = Quaternion.Slerp(taxi.rotation, Quaternion.LookRotation(tangent, Vector3.up), 1f - Mathf.Exp(-8f * Time.deltaTime));
                if (engine != null) engine.pitch = Mathf.Lerp(.8f, 1.25f, current / speed);
                yield return null;
            }
            if (engine != null) engine.pitch = .8f;
        }

        private IEnumerator FadeEngine(float to, float duration)
        {
            if (engine == null) yield break;
            float from = engine.volume;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                engine.volume = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            engine.volume = to;
        }

        private AudioSource CreateSource(string name, AudioClip clip, bool loop)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(taxi, false);
            holder.transform.localPosition = Vector3.up * .8f;
            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 4f;
            source.maxDistance = 70f;
            source.dopplerLevel = .3f;
            return source;
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            if (effects != null && clip != null) effects.PlayOneShot(clip, volume);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static List<Vector3> Points(Transform[] path, Vector3? start)
        {
            var points = new List<Vector3>();
            if (start.HasValue) points.Add(start.Value);
            if (path == null) return points;
            foreach (Transform point in path)
                if (point != null) points.Add(point.position);
            return points;
        }

        private static List<Vector3> SamplePath(List<Vector3> points)
        {
            var samples = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = points[Mathf.Max(0, i - 1)];
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = points[Mathf.Min(points.Count - 1, i + 2)];
                for (int s = 0; s < 16; s++)
                {
                    float t = s / 16f;
                    samples.Add(.5f * (2f * p1 + (p2 - p0) * t +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                        (3f * p1 - p0 - 3f * p2 + p3) * t * t * t));
                }
            }
            samples.Add(points[points.Count - 1]);
            return samples;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .85f, .2f);
            List<Vector3> arrival = Points(arrivalPath, null);
            for (int i = 1; i < arrival.Count; i++) Gizmos.DrawLine(arrival[i - 1], arrival[i]);
            List<Vector3> departure = Points(departurePath, arrival.Count > 0 ? arrival[arrival.Count - 1] : (Vector3?)null);
            for (int i = 1; i < departure.Count; i++) Gizmos.DrawLine(departure[i - 1], departure[i]);
            Gizmos.color = new Color(.4f, .9f, 1f);
            foreach (Transform shot in new[] { establishingShot, curbShot, playerStart })
            {
                if (shot == null) continue;
                Gizmos.DrawWireSphere(shot.position, .25f);
                Gizmos.DrawLine(shot.position, shot.position + shot.forward * 1.5f);
            }
        }
    }
}
