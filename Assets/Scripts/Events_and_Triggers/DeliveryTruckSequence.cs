using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Доставка коробок дня 1. После того как игрок вынес мусор, к дому подъезжает
    // фургон с посылками, задом сдаёт к тротуару и сигналит. Он ждёт, пока игрок
    // подойдёт к кузову, затем поднимает рольставню, и коробки плавно выезжают из
    // кузова на землю рядом с машиной. После этого фургон закрывается и уезжает.
    //
    // Маршрут задаётся точками-детьми в редакторе: arrivalPath — едем вперёд и
    // разворачиваемся, затем задом в parkPoint, оттуда вперёд по departurePath.
    [DisallowMultipleComponent]
    public sealed class DeliveryTruckSequence : MonoBehaviour
    {
        [Header("Фургон")]
        public Transform truck;
        [Tooltip("Пивот у верхнего края проёма: рольставня «сворачивается» вверх, сжимаясь по Y.")]
        public Transform rollUpDoor;
        [Tooltip("Точка внутри кузова у проёма, откуда выезжают коробки.")]
        public Transform cargoLaunchPoint;
        public Light[] headlights;

        [Header("Маршрут")]
        public Transform[] arrivalPath;
        public Transform parkPoint;
        public Transform[] departurePath;
        [Min(.5f)] public float driveSpeed = 8f;
        [Min(.5f)] public float reverseSpeed = 2.5f;

        [Header("Ожидание игрока")]
        public Transform player;
        [Tooltip("Насколько близко игрок должен подойти к задней части фургона, чтобы тот открылся.")]
        [Min(1f)] public float approachDistance = 5.5f;
        [Min(3f)] public float hornRepeatInterval = 14f;

        [Header("Коробки")]
        public Rigidbody[] boxes;
        [Tooltip("Куда ставятся коробки — в осях фургона (Z вперёд), относительно его точки на земле.")]
        public Vector3[] boxDropOffsets =
        {
            new Vector3(-.78f, 0f, -3.35f),
            new Vector3(0f, 0f, -3.35f),
            new Vector3(.78f, 0f, -3.35f),
        };
        [Min(.2f)] public float boxSlideTime = 1.5f;
        [Min(0f)] public float boxInterval = .8f;
        public AudioClip boxSetDownClip;

        [Header("Звук")]
        public AudioClip engineLoop;
        public AudioClip hornClip;
        public AudioClip airBrakeClip;
        public AudioClip parkingBrakeClip;
        public AudioClip rollUpOpenClip;
        public AudioClip rollUpCloseClip;
        [Range(0f, 1f)] public float engineVolume = .75f;
        [Range(0f, 1f)] public float hornVolume = .9f;
        [Range(0f, 1f)] public float beeperVolume = .35f;

        private Vector3[] boxPositions;
        private Quaternion[] boxRotations;
        private float[] boxBottomOffsets;
        private Coroutine running;
        private AudioSource engineSource, beeperSource, effectSource;
        private float engineLoad;

        public bool Delivered { get; private set; }

        private void Awake()
        {
            boxPositions = new Vector3[boxes.Length];
            boxRotations = new Quaternion[boxes.Length];
            boxBottomOffsets = new float[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null) continue;
                boxPositions[i] = boxes[i].position;
                boxRotations[i] = boxes[i].rotation;
                boxBottomOffsets[i] = boxes[i].position.y - ItemBounds(boxes[i].gameObject).min.y;
                boxes[i].gameObject.SetActive(false);
            }
            if (truck != null)
            {
                CreateAudio();
                truck.gameObject.SetActive(false);
            }
            if (player == null)
            {
                PlayerController controller = FindAnyObjectByType<PlayerController>();
                if (controller != null) player = controller.transform;
            }
        }

        // Запускает доставку; onBoxesDelivered вызывается, когда последняя коробка
        // встала на землю (фургон в этот момент ещё закрывается и уезжает).
        public void Play(Action onBoxesDelivered)
        {
            if (Delivered || running != null || !isActiveAndEnabled || truck == null || parkPoint == null)
            {
                EnsureDelivered();
                onBoxesDelivered?.Invoke();
                return;
            }
            running = StartCoroutine(Run(onBoxesDelivered));
        }

        // Коробки сразу на своих местах во дворе — для запуска квеста коробок в обход
        // доставки (отладочный старт с этапа коробок) или если сцена не настроена.
        public void EnsureDelivered()
        {
            if (Delivered) return;
            if (running != null) StopCoroutine(running);
            running = null;
            Delivered = true;
            for (int i = 0; i < boxes.Length; i++) PlaceBox(i, boxPositions[i], boxRotations[i]);
            StopAudio();
            if (truck != null) truck.gameObject.SetActive(false);
        }

        private IEnumerator Run(Action onBoxesDelivered)
        {
            truck.gameObject.SetActive(true);
            SetHeadlights(true);
            SetRollUp(0f);
            engineSource.volume = 0f;
            engineSource.Play();

            yield return Drive(Points(arrivalPath, null), driveSpeed, false);
            yield return new WaitForSeconds(.5f);
            beeperSource.Play();
            yield return Drive(new List<Vector3> { truck.position, parkPoint.position }, reverseSpeed, true);
            beeperSource.Stop();
            PlayEffect(airBrakeClip, .8f);
            yield return new WaitForSeconds(.35f);
            PlayEffect(parkingBrakeClip, .7f);

            // Сигналим и ждём, пока игрок подойдёт к кузову.
            yield return new WaitForSeconds(.8f);
            QuestManager.Instance?.CreateQuest("Забрать посылки у курьера", 1, questTag: "parcel-pickup");
            float nextHorn = 0f;
            while (!PlayerNearCargo())
            {
                if (Time.time >= nextHorn)
                {
                    PlayEffect(hornClip, hornVolume);
                    nextHorn = Time.time + hornRepeatInterval;
                }
                yield return null;
            }
            QuestManager.Instance?.RecordQuestCompletion("parcel-pickup");

            yield return new WaitForSeconds(.4f);
            PlayEffect(rollUpOpenClip, 1f);
            yield return RollUp(0f, 1f, 1.8f);

            Vector3 ground = truck.position;
            Quaternion yaw = Quaternion.Euler(0f, truck.eulerAngles.y, 0f);
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null) continue;
                Vector3 offset = i < boxDropOffsets.Length ? boxDropOffsets[i] : new Vector3(0f, 0f, -3.35f - i);
                StartCoroutine(SlideOut(i, ground + yaw * offset, yaw));
                yield return new WaitForSeconds(boxInterval);
            }
            yield return new WaitForSeconds(Mathf.Max(0f, boxSlideTime - boxInterval) + .3f);
            Delivered = true;
            onBoxesDelivered?.Invoke();

            yield return new WaitForSeconds(.8f);
            PlayEffect(rollUpCloseClip, 1f);
            yield return RollUp(1f, 0f, 1.6f);
            yield return new WaitForSeconds(1.2f);
            yield return Drive(Points(departurePath, truck.position), driveSpeed, false);
            yield return FadeEngine(.8f);
            SetHeadlights(false);
            truck.gameObject.SetActive(false);
            running = null;
        }

        private bool PlayerNearCargo()
        {
            if (player == null) return true;
            Vector3 cargo = cargoLaunchPoint != null ? cargoLaunchPoint.position : truck.position;
            Vector3 delta = player.position - cargo;
            delta.y = 0f;
            return delta.magnitude <= approachDistance;
        }

        // Коробка без кувырков выезжает из кузова назад, затем плавно опускается
        // на землю рядом с машиной и получает обратно физику.
        private IEnumerator SlideOut(int index, Vector3 groundPoint, Quaternion rotation)
        {
            Rigidbody box = boxes[index];
            Vector3 start = cargoLaunchPoint != null ? cargoLaunchPoint.position : truck.position + Vector3.up;
            Vector3 end = GroundedPosition(groundPoint, index);
            Vector3 lip = new Vector3(end.x, start.y, end.z);
            box.isKinematic = true;
            box.transform.SetPositionAndRotation(start, rotation);
            box.gameObject.SetActive(true);
            for (float t = 0f; t < boxSlideTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / boxSlideTime);
                // Сначала в основном назад из кузова, потом вниз: две кривые со сдвигом по времени.
                float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f));
                float drop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - .35f) / .65f));
                Vector3 point = Vector3.Lerp(start, lip, slide);
                point.y = Mathf.Lerp(start.y, end.y, drop);
                box.transform.SetPositionAndRotation(point, rotation);
                yield return null;
            }
            PlaceBox(index, end, rotation);
            if (boxSetDownClip != null)
            {
                // A-01: через пул GameAudio (шина Foley) вместо PlayClipAtPoint без маршрута.
                if (GameAudio.Instance != null) GameAudio.PlayAt(boxSetDownClip, end, AudioBus.Foley, .45f);
                else AudioSource.PlayClipAtPoint(boxSetDownClip, end, .45f);
            }
        }

        private Vector3 GroundedPosition(Vector3 point, int index)
        {
            float groundY = point.y;
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 8f,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(truck) || hit.collider.attachedRigidbody != null) continue;
                best = Mathf.Max(best, hit.point.y);
            }
            if (!float.IsNegativeInfinity(best)) groundY = best;
            return new Vector3(point.x, groundY + boxBottomOffsets[index] + .01f, point.z);
        }

        private void PlaceBox(int index, Vector3 position, Quaternion rotation)
        {
            Rigidbody box = boxes[index];
            if (box == null) return;
            box.transform.SetPositionAndRotation(position, rotation);
            box.gameObject.SetActive(true);
            box.isKinematic = false;
            box.linearVelocity = Vector3.zero;
            box.angularVelocity = Vector3.zero;
        }

        private static Bounds ItemBounds(GameObject item)
        {
            Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
            bool found = false;
            foreach (Collider c in item.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!found) { bounds = c.bounds; found = true; }
                else bounds.Encapsulate(c.bounds);
            }
            return bounds;
        }

        private IEnumerator RollUp(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetRollUp(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetRollUp(to);
        }

        private void SetRollUp(float open)
        {
            if (rollUpDoor == null) return;
            Vector3 scale = rollUpDoor.localScale;
            scale.y = Mathf.Lerp(1f, .04f, open);
            rollUpDoor.localScale = scale;
        }

        private void SetHeadlights(bool on)
        {
            if (headlights == null) return;
            foreach (Light light in headlights)
                if (light != null) light.enabled = on;
        }

        private static List<Vector3> Points(Transform[] path, Vector3? start)
        {
            var points = new List<Vector3>();
            if (start.HasValue) points.Add(start.Value);
            if (path == null) return points;
            foreach (Transform point in path)
                if (point != null) points.Add(point.position);
            return points;
        }

        // Едет по сглаженной (Catmull-Rom) линии через точки с разгоном и торможением.
        // reverse — задом: нос смотрит против направления движения.
        private IEnumerator Drive(List<Vector3> points, float speed, bool reverse)
        {
            if (points.Count < 2) yield break;
            List<Vector3> samples = SamplePath(points);
            var distances = new float[samples.Count];
            for (int i = 1; i < samples.Count; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
            float length = distances[samples.Count - 1];
            if (length < .01f) yield break;

            truck.position = samples[0];
            float travelled = 0f, current = 0f;
            const float accel = 2.8f;
            int segment = 0;
            while (travelled < length)
            {
                float remaining = length - travelled;
                float target = Mathf.Min(speed, Mathf.Sqrt(2f * accel * remaining) + .25f);
                float previous = current;
                current = Mathf.MoveTowards(current, target, accel * Time.deltaTime);
                float step = Mathf.Min(current * Time.deltaTime, remaining);
                travelled += step;
                while (segment < samples.Count - 2 && distances[segment + 1] < travelled) segment++;
                float span = Mathf.Max(.0001f, distances[segment + 1] - distances[segment]);
                Vector3 point = Vector3.Lerp(samples[segment], samples[segment + 1], (travelled - distances[segment]) / span);
                Vector3 tangent = samples[segment + 1] - samples[segment];
                tangent.y = 0f;
                if (tangent.sqrMagnitude > .0001f)
                {
                    Quaternion facing = Quaternion.LookRotation(reverse ? -tangent : tangent, Vector3.up);
                    truck.rotation = Quaternion.Slerp(truck.rotation, facing, 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
                truck.position = point;
                UpdateEngine(current / driveSpeed, current > previous + .0001f);
                yield return null;
            }
            UpdateEngine(0f, false);
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

        // ---------------------------------------------------------------------
        // Звук. Мотор — одна петля холостого хода дизеля, у которой высота тона и
        // громкость идут за скоростью и нагрузкой (разгон звучит натужнее, чем
        // накат). Сигнал заднего хода синтезируется: чистый тон 0.5 с / пауза 0.5 с.
        // ---------------------------------------------------------------------

        private void CreateAudio()
        {
            engineSource = CreateSource("Engine", engineLoop, true, 4f, 70f);
            beeperSource = CreateSource("Reverse beeper", CreateBeeperClip(), true, 2f, 35f);
            beeperSource.volume = beeperVolume;
            effectSource = CreateSource("Truck effects", null, false, 3f, 80f);
        }

        private AudioSource CreateSource(string name, AudioClip clip, bool loop, float minDistance, float maxDistance)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(truck, false);
            holder.transform.localPosition = new Vector3(0f, 1f, name == "Engine" ? 1.6f : -1.5f);
            var source = holder.AddComponent<AudioSource>();
            AudioRouting.Assign(source, AudioBus.World);
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = .3f;
            return source;
        }

        private void UpdateEngine(float speed01, bool accelerating)
        {
            if (engineSource == null) return;
            engineLoad = Mathf.MoveTowards(engineLoad, accelerating ? 1f : 0f, Time.deltaTime * 2.5f);
            float rpm = Mathf.Clamp01(speed01);
            engineSource.pitch = Mathf.Lerp(.82f, 1.32f, rpm) + engineLoad * .1f;
            float target = engineVolume * Mathf.Lerp(.6f, 1f, Mathf.Max(rpm, engineLoad * .8f));
            engineSource.volume = Mathf.MoveTowards(engineSource.volume, target, Time.deltaTime * 1.5f);
        }

        private IEnumerator FadeEngine(float duration)
        {
            if (engineSource == null) yield break;
            float start = engineSource.volume;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                engineSource.volume = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            StopAudio();
        }

        private void StopAudio()
        {
            if (engineSource != null) engineSource.Stop();
            if (beeperSource != null) beeperSource.Stop();
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            if (effectSource != null && clip != null) effectSource.PlayOneShot(clip, volume);
        }

        private static AudioClip CreateBeeperClip()
        {
            const int rate = 44100;
            var data = new float[rate];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                if (t >= .5f) continue;
                float envelope = Mathf.Clamp01(t / .01f) * Mathf.Clamp01((.5f - t) / .015f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1150f * t) + .22f * Mathf.Sin(2f * Mathf.PI * 3450f * t);
                data[i] = tone * envelope * .6f;
            }
            AudioClip clip = AudioClip.Create("Reverse beeper", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .7f, .2f);
            DrawPath(Points(arrivalPath, null));
            Gizmos.color = new Color(.3f, .8f, 1f);
            if (parkPoint != null)
            {
                Gizmos.matrix = Matrix4x4.TRS(parkPoint.position, parkPoint.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.up * 1.2f, new Vector3(2.4f, 2.4f, 5.2f));
                Gizmos.color = new Color(.9f, .6f, .3f);
                foreach (Vector3 offset in boxDropOffsets) Gizmos.DrawWireCube(offset + Vector3.up * .3f, Vector3.one * .6f);
                Gizmos.matrix = Matrix4x4.identity;
            }
            Gizmos.color = new Color(.3f, .8f, 1f);
            DrawPath(Points(departurePath, parkPoint != null ? parkPoint.position : (Vector3?)null));
        }

        private static void DrawPath(List<Vector3> points)
        {
            for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1], points[i]);
        }
    }
}
