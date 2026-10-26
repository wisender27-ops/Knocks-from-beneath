using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KnocksFromBeneath
{
    // Монстр пугалки в коридоре: стоит сгорбившись (Injured Idle из контроллера), оборачивается через плечо
    // на игрока (поворот грудь → шея → голова поверх анимации), срывается бегом (клип бега через Playables).
    [DisallowMultipleComponent]
    public sealed class CornerStalkerRig : MonoBehaviour
    {
        [SerializeField] private AnimationClip runClip;
        [Tooltip("Насколько сильно каждая кость участвует в повороте (грудь, шея, голова) — в сумме 1.")]
        [SerializeField] private Vector3 lookShares = new Vector3(0.4f, 0.25f, 0.35f);
        [SerializeField] private float maxYaw = 145f; // неестественный выворот через плечо
        [SerializeField] private float maxPitch = 30f;

        private Animator animator;
        private Transform chest, neck, head;
        private PlayableGraph runGraph;

        public Vector3 LookTarget { get; set; }
        public float LookWeight { get; set; }
        public Transform Head => head;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        // Стоит, сгорбившись: idle с произвольной фазы, чтобы поза не была «нулевым кадром».
        public void Idle(float phase)
        {
            if (animator == null) return;
            animator.Play("Observe", 0, Mathf.Repeat(phase, 1f));
            animator.Update(0f);
        }

        public void Run(float speed)
        {
            if (animator == null || runClip == null || runGraph.IsValid()) return;
            runGraph = PlayableGraph.Create("Corner Stalker Run");
            runGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(runGraph, "Run", animator);
            var clip = AnimationClipPlayable.Create(runGraph, runClip);
            clip.SetSpeed(speed);
            output.SetSourcePlayable(clip);
            runGraph.Play();
        }

        private void LateUpdate()
        {
            if (LookWeight <= 0.001f || head == null) return;
            Transform body = transform;
            Vector3 from = head.position;
            Vector3 local = body.InverseTransformDirection(LookTarget - from);
            if (local.sqrMagnitude < 0.0001f) return;
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw) * LookWeight;
            float flat = new Vector2(local.x, local.z).magnitude;
            float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, flat) * Mathf.Rad2Deg, -maxPitch, maxPitch) * LookWeight;
            Turn(chest, yaw * lookShares.x, pitch * lookShares.x);
            Turn(neck, yaw * lookShares.y, pitch * lookShares.y);
            Turn(head, yaw * lookShares.z, pitch * lookShares.z);
        }

        // Оси берутся у корня: поворот родителя уже унёс детей, доли складываются в полный угол.
        private void Turn(Transform bone, float yaw, float pitch)
        {
            if (bone == null) return;
            Vector3 right = Quaternion.AngleAxis(yaw, transform.up) * transform.right;
            bone.rotation = Quaternion.AngleAxis(pitch, right) * Quaternion.AngleAxis(yaw, transform.up) * bone.rotation;
        }

        private void OnDestroy()
        {
            if (runGraph.IsValid()) runGraph.Destroy();
        }
    }
}
