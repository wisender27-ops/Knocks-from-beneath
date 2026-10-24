using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonKnockdown : MonoBehaviour
    {
        public enum KnockdownPhase { Idle, Falling, Grounded, GettingUp, Recovered }

        [SerializeField, Min(0.1f)] private float fallDuration = 0.58f;
        [SerializeField, Min(0.1f)] private float groundDuration = 0.65f;
        [SerializeField, Min(0.1f)] private float riseDuration = 1.45f;
        [SerializeField, Min(0.15f)] private float groundEyeHeight = 0.28f;
        [SerializeField] private AudioSource foley;
        [SerializeField] private AudioClip fallClip;
        [SerializeField] private AudioClip riseClip;
        [SerializeField] private AnimationCurve fallWeight = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.18f, 0.05f, 0.8f, 0.8f),
            new Keyframe(0.8f, 0.94f, 1f, 1f), new Keyframe(1f, 1f, 0f, 0f));

        public KnockdownPhase Phase { get; private set; }
        public bool IsPlaying { get; private set; }

        private readonly RaycastHit[] hits = new RaycastHit[24];
        private Camera view;
        private Transform playerRoot;
        private Vector3 standingPosition;
        private Quaternion standingRotation;
        private float standingFov;
        private Vector3 cameraAnchor;
        private float cameraRadius;
        private Light beam;
        private bool beamEnabled;
        private float beamIntensity;

        public IEnumerator Play(Camera camera, Transform root, Vector3 returnPosition,
            Quaternion returnRotation, float returnFov, Light flashlight)
        {
            if (IsPlaying || camera == null || root == null) yield break;
            view = camera;
            playerRoot = root;
            standingPosition = returnPosition;
            standingRotation = returnRotation;
            standingFov = returnFov;
            cameraAnchor = view.transform.position;
            float near = view.nearClipPlane;
            float tangent = Mathf.Tan(Mathf.Max(view.fieldOfView, returnFov + 5f) * 0.5f * Mathf.Deg2Rad);
            cameraRadius = Mathf.Max(0.075f, near * Mathf.Sqrt(1f + tangent * tangent *
                (1f + view.aspect * view.aspect)) + 0.015f);
            beam = flashlight;
            if (beam != null)
            {
                beamEnabled = beam.enabled;
                beamIntensity = beam.intensity;
                beam.enabled = false;
            }
            IsPlaying = true;
            Phase = KnockdownPhase.Falling;

            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = playerRoot.forward;
            Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 start = view.transform.position;
            Quaternion startRotation = view.transform.rotation;
            float floor = FindGroundHeight(start);
            CharacterController capsule = root.GetComponent<CharacterController>();
            float retreat = capsule != null ? Mathf.Max(0f,
                capsule.radius - capsule.skinWidth - cameraRadius - 0.02f) : 0.05f;
            Vector3 grounded = start - forward * Mathf.Min(retreat, 0.08f);
            grounded.y = Mathf.Min(start.y - 0.25f, floor + groundEyeHeight);
            Quaternion onBack = yaw * Quaternion.Euler(-76f, 0f, 9f);

            for (float elapsed = 0f; elapsed < fallDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / fallDuration);
                float weight = Mathf.Clamp01(fallWeight.Evaluate(t));
                if (beam != null) beam.enabled = t >= 0.08f;
                ApplyPose(Vector3.Lerp(start, grounded, weight),
                    Quaternion.Slerp(startRotation, onBack, weight), standingFov + 5f * weight);
                yield return null;
            }
            ApplyPose(grounded, onBack, standingFov + 5f);
            if (foley != null && fallClip != null) foley.PlayOneShot(fallClip, 0.8f);
            if (beam != null) beam.enabled = true;
            Phase = KnockdownPhase.Grounded;

            for (float elapsed = 0f; elapsed < groundDuration; elapsed += Time.deltaTime)
            {
                float impact = Mathf.Exp(-elapsed * 15f);
                float breath = Mathf.Sin(elapsed * 4.5f) * 0.6f;
                ApplyPose(grounded + Vector3.up * (0.022f * impact),
                    onBack * Quaternion.Euler(Mathf.Sin(elapsed * 45f) * 2f * impact + breath,
                        0f, -1.2f * impact), standingFov + 5f * (1f - elapsed / groundDuration));
                yield return null;
            }

            Phase = KnockdownPhase.GettingUp;
            if (foley != null && riseClip != null) foley.PlayOneShot(riseClip, 0.6f);
            Vector3 upright = view.transform.parent != null
                ? view.transform.parent.TransformPoint(standingPosition) : standingPosition;
            Quaternion uprightRotation = view.transform.parent != null
                ? view.transform.parent.rotation * standingRotation : standingRotation;
            Vector3 seated = Vector3.Lerp(grounded, upright, 0.42f);
            Quaternion seatedRotation = yaw * Quaternion.Euler(14f, 0f, -7f);
            yield return MoveCamera(grounded, seated, onBack, seatedRotation,
                riseDuration * 0.38f, standingFov, standingFov - 3f);
            yield return new WaitForSeconds(0.12f);
            if (foley != null && riseClip != null) foley.PlayOneShot(riseClip, 0.35f);
            yield return MoveCamera(seated, upright, seatedRotation, uprightRotation,
                riseDuration * 0.62f, standingFov - 3f, standingFov);
            Phase = KnockdownPhase.Recovered;
            Restore();
        }

        private IEnumerator MoveCamera(Vector3 from, Vector3 to, Quaternion fromRotation,
            Quaternion toRotation, float duration, float fromFov, float toFov)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float weight = t * t * t * (t * (6f * t - 15f) + 10f);
                ApplyPose(Vector3.Lerp(from, to, weight),
                    Quaternion.Slerp(fromRotation, toRotation, weight), Mathf.Lerp(fromFov, toFov, weight));
                yield return null;
            }
            ApplyPose(to, toRotation, toFov);
        }

        private float FindGroundHeight(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position, Vector3.down, hits, 4f,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            float height = position.y - 1.5f;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(playerRoot) || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                height = hits[i].point.y;
            }
            return height;
        }

        private void ApplyPose(Vector3 position, Quaternion rotation, float fov)
        {
            // Проверяем весь путь от исходной точки: камера не пересекает ступени даже при скачке кадра.
            Vector3 delta = position - cameraAnchor;
            float length = delta.magnitude;
            if (length > 0.001f)
            {
                int count = Physics.SphereCastNonAlloc(cameraAnchor, cameraRadius, delta / length,
                    hits, length, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
                float permitted = length;
                for (int i = 0; i < count; i++)
                    if (!hits[i].collider.transform.IsChildOf(playerRoot))
                        permitted = Mathf.Min(permitted, Mathf.Max(0f, hits[i].distance - 0.015f));
                position = cameraAnchor + delta / length * permitted;
            }
            view.transform.SetPositionAndRotation(position, rotation);
            view.fieldOfView = fov;
        }

        public void Restore()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            if (view != null)
            {
                view.transform.localPosition = standingPosition;
                view.transform.localRotation = standingRotation;
                view.fieldOfView = standingFov;
            }
            if (beam != null)
            {
                beam.enabled = beamEnabled;
                beam.intensity = beamIntensity;
            }
            if (Phase != KnockdownPhase.Recovered && foley != null) foley.Stop();
        }

        private void OnDisable() => Restore();
    }
}
