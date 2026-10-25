using UnityEngine;

namespace KnocksFromBeneath
{
    // Стук соседа в дверь поверх его idle: правая рука по IK поднимается к полотну двери
    // и отстукивает. Готовой свободной анимации стука нет, поэтому движение собрано здесь;
    // слой контроллера соседа должен иметь IK Pass. Звук удара даёт NeighborVisitSequence
    // в момент касания (TapContactDelay после Tap).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class NeighborKnockIK : MonoBehaviour
    {
        public const float RaiseTime = 0.3f;
        public const float TapDuration = 0.16f;
        public const float TapContactDelay = TapDuration * 0.5f;
        private const float RestDistance = 0.1f;

        private Animator animator;
        private Vector3 contact, outward;
        private float weight, targetWeight, tapStarted = -1f;

        public void Begin(Vector3 contactPoint, Vector3 outwardNormal)
        {
            contact = contactPoint;
            outward = outwardNormal.normalized;
            targetWeight = 1f;
        }

        public void Tap() => tapStarted = Time.time;
        public void End() => targetWeight = 0f;
        public void StopNow() => weight = targetWeight = 0f;

        private void Awake() => animator = GetComponent<Animator>();

        private void Update()
        {
            weight = Mathf.MoveTowards(weight, targetWeight, Time.deltaTime / RaiseTime);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            float w = Mathf.SmoothStep(0f, 1f, weight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w * 0.8f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w);
            if (w <= 0f) return;

            // Кулак держится у двери и на каждом ударе касается полотна.
            float tap = tapStarted < 0f ? 1f : Mathf.Clamp01((Time.time - tapStarted) / TapDuration);
            float distance = RestDistance * (1f - Mathf.Sin(tap * Mathf.PI));
            animator.SetIKPosition(AvatarIKGoal.RightHand, contact + outward * distance);

            // Кисть костяшками к двери, ладонью вниз-внутрь.
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
            animator.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0f, 0f, 90f));
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (shoulder != null)
                animator.SetIKHintPosition(AvatarIKHint.RightElbow, shoulder.position + Vector3.down * 0.35f + outward * 0.15f - side * 0.2f);
        }
    }
}
