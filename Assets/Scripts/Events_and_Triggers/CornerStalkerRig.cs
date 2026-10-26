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

        private Animator animator;
        private Transform chest, neck, head;
        private Vector3 faceLocal = Vector3.forward; // куда смотрит морда в осях кости головы
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
            // В позе префаба морда смотрит вперёд по корню — запоминаем это направление в осях головы.
            if (head != null) faceLocal = Quaternion.Inverse(head.rotation) * transform.forward;
        }

        // Стоит, сгорбившись: idle с произвольной фазы, чтобы поза не была «нулевым кадром».
        public void Idle(float phase)
        {
            if (animator == null) return;
            animator.Play("Observe", 0, Mathf.Repeat(phase, 1f));
            animator.Update(0f);
        }

        // Бросок: «хватающий» замах из Attack (Superhuman Choke Lift) — рука тянется к лицу игрока.
        public void Lunge(float phase, float speed)
        {
            if (animator == null) return;
            animator.speed = speed;
            animator.Play("Attack", 0, Mathf.Clamp01(phase));
            animator.Update(0f);
        }

        public void Run(float speed)
        {
            if (animator == null || runClip == null || runGraph.IsValid()) return;
            animator.speed = 1f;
            runGraph = PlayableGraph.Create("Corner Stalker Run");
            runGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(runGraph, "Run", animator);
            var clip = AnimationClipPlayable.Create(runGraph, runClip);
            clip.SetSpeed(speed);
            output.SetSourcePlayable(clip);
            runGraph.Play();
        }

        // Поверх анимации доворачивает морду на цель: грудь и шея берут часть угла, голова — остаток.
        // Угол считается от того, куда морда смотрит сейчас (анимация может уводить голову в сторону).
        private void LateUpdate()
        {
            if (LookWeight <= 0.001f || head == null) return;
            Vector3 desired = LookTarget - head.position;
            if (desired.sqrMagnitude < 0.0001f) return;
            Turn(chest, desired, lookShares.x);
            Turn(neck, desired, lookShares.y / Mathf.Max(0.01f, 1f - lookShares.x));
            Turn(head, desired, 1f);
        }

        private void Turn(Transform bone, Vector3 desired, float share)
        {
            if (bone == null) return;
            Vector3 face = head.rotation * faceLocal;
            Quaternion full = Quaternion.FromToRotation(face, desired);
            full.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle, -maxYaw, maxYaw) * share * LookWeight;
            bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }

        private void OnDestroy()
        {
            if (runGraph.IsValid()) runGraph.Destroy();
        }
    }
}
