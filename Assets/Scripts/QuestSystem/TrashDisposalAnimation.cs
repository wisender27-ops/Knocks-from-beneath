using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnocksFromBeneath
{
    // Отдельный хозяин: зачёт квеста выключает QuestCollector ещё до оседания мешков.
    public sealed class TrashDisposalAnimation : MonoBehaviour
    {
        private const float LiftTime = .2f, MouthTime = .5f, ImpactTime = .65f, Duration = 1.05f;
        private QuestCollector collector;
        private GameObject bag;
        private Rigidbody body;
        private Collider[] bagColliders;
        private bool[] colliderStates;
        private ParticleSystem[] particles;
        private bool[] particleStates;
        private RevealPose[] reveal;
        private Transform bin;
        private Vector3 binPosition, bagPosition, bagScale, centerLocal, startCenter, liftedCenter, landingCenter;
        private Vector3 mouthCenter, compressedScale;
        private Quaternion binRotation, bagRotation, landingRotation;
        private bool bodyKinematic, bodyGravity, bodyCollisions;
        private QuestItemMarker marker;
        private bool markerEnabled;
        private Action onImpact, onCancelled;
        private float elapsed;
        private bool impacted, revealed, finished;

        private struct RevealPose
        {
            public Transform Transform;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
            public bool Active;
            public float Height;
        }

        public static bool TryBegin(QuestCollector collector, PlayerInteraction actor, GameObject[] revealItems,
            Action onImpact, Action onCancelled)
        {
            GameObject held = actor != null ? actor.GetHeldObject() : null;
            if (!Application.isPlaying || collector == null || collector.DisposalCollider == null ||
                held == null || !held.TryGetComponent<Rigidbody>(out var heldBody)) return false;

            var runner = new GameObject("TrashDisposalAnimation");
            SceneManager.MoveGameObjectToScene(runner, collector.gameObject.scene);
            var animation = runner.AddComponent<TrashDisposalAnimation>();
            animation.collector = collector;
            animation.onImpact = onImpact;
            animation.onCancelled = onCancelled;
            animation.bag = actor.ReleaseHeldObject();
            if (animation.bag == null)
            {
                animation.finished = true;
                Destroy(runner);
                return false;
            }
            animation.Initialize(heldBody, revealItems);
            return true;
        }

        private void Initialize(Rigidbody heldBody, GameObject[] revealItems)
        {
            marker = bag.GetComponent<QuestItemMarker>();
            if (marker != null)
            {
                markerEnabled = marker.enabled;
                marker.enabled = false;
            }
            body = heldBody;
            bodyKinematic = body.isKinematic;
            bodyGravity = body.useGravity;
            bodyCollisions = body.detectCollisions;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            bagColliders = bag.GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[bagColliders.Length];
            for (int i = 0; i < bagColliders.Length; i++)
            {
                colliderStates[i] = bagColliders[i].enabled;
                bagColliders[i].enabled = false;
            }
            particles = bag.GetComponentsInChildren<ParticleSystem>(true);
            particleStates = new bool[particles.Length];
            for (int i = 0; i < particles.Length; i++)
            {
                particleStates[i] = particles[i].isPlaying;
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            bagPosition = bag.transform.position;
            bagScale = bag.transform.localScale;
            bagRotation = bag.transform.rotation;
            Bounds localBounds = LocalVisualBounds(bag);
            centerLocal = localBounds.center;
            startCenter = bag.transform.TransformPoint(centerLocal);
            liftedCenter = startCenter + Vector3.up * .2f;
            landingCenter = collector.DisposalCollider.bounds.center;
            landingRotation = Quaternion.AngleAxis(17f, Vector3.forward) *
                Quaternion.AngleAxis(-12f, Vector3.up) * bagRotation;

            reveal = new RevealPose[revealItems != null ? revealItems.Length : 0];
            for (int i = 0; i < reveal.Length; i++)
            {
                GameObject item = revealItems[i];
                if (item == null) continue;
                Transform t = item.transform;
                reveal[i] = new RevealPose { Transform = t, Position = t.localPosition, Scale = t.localScale,
                    Rotation = t.localRotation, Active = item.activeSelf,
                    Height = Mathf.Max(.1f, VisualBounds(item).size.y) };
                if (bin == null && t.parent != null && !collector.transform.IsChildOf(t.parent) &&
                    t.parent.GetComponent<Renderer>() != null) bin = t.parent;
            }
            if (bin != null)
            {
                binPosition = bin.localPosition;
                binRotation = bin.localRotation;
            }
            Bounds mouth = bin != null ? bin.GetComponent<Renderer>().bounds : collector.DisposalCollider.bounds;
            float rimHeight = mouth.max.y;
            landingCenter.x = mouth.center.x;
            landingCenter.z = mouth.center.z;
            Vector3 fullExtents = RotatedExtents(localBounds.extents, bagScale, landingRotation);
            float fit = Mathf.Min(1f, mouth.extents.x * .85f / Mathf.Max(.001f, fullExtents.x),
                mouth.extents.z * .85f / Mathf.Max(.001f, fullExtents.z));
            compressedScale = Vector3.Scale(bagScale, new Vector3(fit, .9f, fit));
            Vector3 packedExtents = RotatedExtents(localBounds.extents, compressedScale, landingRotation);
            float clearance = Mathf.Min(1f, mouth.extents.x * .85f / Mathf.Max(.001f, packedExtents.x),
                mouth.extents.z * .85f / Mathf.Max(.001f, packedExtents.z));
            compressedScale *= clearance;
            packedExtents = RotatedExtents(localBounds.extents, compressedScale, landingRotation);
            mouthCenter = new Vector3(landingCenter.x, rimHeight + fullExtents.y + .08f, landingCenter.z);
            // До удаления весь мешок должен уйти под кромку, а не исчезнуть над баком.
            landingCenter.y = rimHeight - packedExtents.y - .025f;
            var library = GameSounds.Library;
            if (library != null) Play(library.cloth, startCenter, .32f, .95f);
        }

        private void Update()
        {
            if (finished || Time.deltaTime <= 0f) return;
            if (!impacted && (collector == null || !collector.isActiveAndEnabled || bag == null))
            {
                Finish(false);
                return;
            }
            elapsed = Mathf.Min(Duration, elapsed + Time.deltaTime);
            if (!impacted)
            {
                MoveBag();
                if (elapsed >= .53f) AnimateReveal(Mathf.InverseLerp(.53f, ImpactTime, elapsed));
                if (elapsed >= ImpactTime) Impact();
            }
            if (impacted)
            {
                float t = Mathf.Clamp01((elapsed - ImpactTime) / (Duration - ImpactTime));
                Settle(t);
                if (t >= 1f) Finish(true);
            }
        }

        private void MoveBag()
        {
            Vector3 center;
            Quaternion rotation;
            if (elapsed < LiftTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / LiftTime);
                center = Vector3.Lerp(startCenter, liftedCenter, t);
                rotation = Quaternion.Slerp(bagRotation, landingRotation, t * .15f);
            }
            else if (elapsed < MouthTime)
            {
                float t = Mathf.Clamp01((elapsed - LiftTime) / (MouthTime - LiftTime));
                Vector3 control = mouthCenter + Vector3.up * .28f;
                center = (1f - t) * (1f - t) * liftedCenter + 2f * (1f - t) * t * control + t * t * mouthCenter;
                rotation = Quaternion.Slerp(Quaternion.Slerp(bagRotation, landingRotation, .15f),
                    landingRotation, Mathf.SmoothStep(0f, 1f, t));
            }
            else
            {
                float t = Mathf.Clamp01((elapsed - MouthTime) / (ImpactTime - MouthTime));
                // Над горловиной гасим горизонтальное движение: дальше мешок входит строго сверху.
                center = Vector3.Lerp(mouthCenter, landingCenter, t * t);
                rotation = landingRotation;
                bag.transform.localScale = Vector3.Lerp(bagScale, compressedScale, Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 2f, 1f)));
            }
            bag.transform.rotation = rotation;
            bag.transform.position = center - bag.transform.TransformVector(centerLocal);
        }

        private void AnimateReveal(float t)
        {
            revealed = true;
            for (int i = 0; i < reveal.Length; i++)
            {
                RevealPose pose = reveal[i];
                if (pose.Transform == null || pose.Active) continue;
                float rise = Mathf.SmoothStep(0f, 1f, t);
                SetRevealPose(pose, Mathf.Lerp(.12f, .84f, rise), Mathf.Lerp(-pose.Height * .38f, -.025f, rise));
                pose.Transform.gameObject.SetActive(true);
            }
        }

        private static void SetRevealPose(RevealPose pose, float scaleY, float offsetY)
        {
            pose.Transform.localScale = Vector3.Scale(pose.Scale, new Vector3(1f, scaleY, 1f));
            Vector3 worldOffset = Vector3.up * offsetY;
            pose.Transform.localPosition = pose.Position + (pose.Transform.parent != null
                ? pose.Transform.parent.InverseTransformVector(worldOffset) : worldOffset);
        }

        private void Impact()
        {
            impacted = true;
            bag.SetActive(false);
            Destroy(bag);
            var library = GameSounds.Library;
            if (library != null)
            {
                Play(library.softDrop, landingCenter, .58f, .86f);
                Play(library.metalDrop, landingCenter, .18f, .72f);
                Play(library.cloth, landingCenter, .25f, .9f);
            }
            onImpact?.Invoke();
            onImpact = null;
        }

        private void Settle(float t)
        {
            float damp = Mathf.Exp(-6f * t) * Mathf.Sin(t * Mathf.PI * 3f);
            for (int i = 0; i < reveal.Length; i++)
            {
                RevealPose pose = reveal[i];
                if (pose.Transform == null || pose.Active) continue;
                SetRevealPose(pose, Mathf.Lerp(.84f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(t * 3f, 1f))) + damp * .055f,
                    -.025f * (1f - t) + damp * .025f);
            }
            if (bin != null)
            {
                bin.localPosition = binPosition;
                bin.localRotation = binRotation * Quaternion.Euler(damp * .7f, 0f, damp * -.45f);
            }
        }

        private void Finish(bool completed)
        {
            if (finished) return;
            finished = true;
            if (bin != null) { bin.localPosition = binPosition; bin.localRotation = binRotation; }
            if (reveal != null)
                for (int i = 0; i < reveal.Length; i++)
                {
                    RevealPose pose = reveal[i];
                    if (pose.Transform == null) continue;
                    pose.Transform.localPosition = pose.Position;
                    pose.Transform.localRotation = pose.Rotation;
                    pose.Transform.localScale = pose.Scale;
                    if (!impacted && revealed) pose.Transform.gameObject.SetActive(pose.Active);
                }
            if (!impacted)
            {
                if (bag != null)
                {
                    bag.transform.SetPositionAndRotation(bagPosition, bagRotation);
                    bag.transform.localScale = bagScale;
                    if (marker != null) marker.enabled = markerEnabled;
                    for (int i = 0; bagColliders != null && i < bagColliders.Length; i++)
                        if (bagColliders[i] != null) bagColliders[i].enabled = colliderStates[i];
                    if (body != null)
                    {
                        body.isKinematic = bodyKinematic;
                        body.useGravity = bodyGravity;
                        body.detectCollisions = bodyCollisions;
                    }
                    for (int i = 0; particles != null && i < particles.Length; i++)
                        if (particles[i] != null && particleStates[i]) particles[i].Play(true);
                }
                onCancelled?.Invoke();
            }
            onImpact = null;
            onCancelled = null;
            Destroy(gameObject);
        }

        private void OnDisable() => Finish(false);

        private static Bounds VisualBounds(GameObject item)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(item.transform.position, Vector3.zero);
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || !renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static Bounds LocalVisualBounds(GameObject item)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || !renderer.enabled) continue;
                Bounds local = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = item.transform.InverseTransformPoint(renderer.transform.TransformPoint(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        private static Vector3 RotatedExtents(Vector3 extents, Vector3 scale, Quaternion rotation)
        {
            Vector3 x = rotation * Vector3.right * Mathf.Abs(extents.x * scale.x);
            Vector3 y = rotation * Vector3.up * Mathf.Abs(extents.y * scale.y);
            Vector3 z = rotation * Vector3.forward * Mathf.Abs(extents.z * scale.z);
            return new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        }

        private static void Play(AudioClip[] clips, Vector3 position, float volume, float pitch)
        {
            GameAudio.PlayAt(GameSounds.Pick(clips), position, AudioBus.Foley, volume, pitch, 10f);
        }
    }
}
