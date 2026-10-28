using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace KnocksFromBeneath
{
    // Монстр пугалки в коридоре: стоит сгорбившись (Injured Idle из контроллера), оборачивается через плечо
    // на игрока (поворот грудь → шея → голова поверх анимации), срывается бегом (клип бега через Playables).
    // M-07 «под кроватью»: шаг и стойка (Quaternius UAL, CC0) с плавным переходом и Foot IK — ступни на полу;
    // Peek — своя анимация MonsterPeekUnderBed (Editor/MonsterPeekClipBuilder): резкий провал, голова рывком в щель
    // под кроватью; в ней ступни прижимаются к полу (таз сдвигается), так что глубину задают только колени.
    [DisallowMultipleComponent]
    public sealed class CornerStalkerRig : MonoBehaviour
    {
        [SerializeField] private AnimationClip runClip;
        [Tooltip("Шаг по дому (M-07: обход спальни, пока игрок под кроватью).")]
        [SerializeField] private AnimationClip walkClip;
        [Tooltip("С какой скоростью (м/с) идёт клип шага, если в нём нет движения корня — под неё подгоняется темп.")]
        [SerializeField] private float walkClipSpeed = 0.75f;
        [Tooltip("Спокойная стойка (M-07) — вместо дрожащего Injured Idle.")]
        [SerializeField] private AnimationClip standClip;
        [Tooltip("Заглядывает под кровать (M-07, своя анимация MonsterPeekUnderBed).")]
        [SerializeField] private AnimationClip peekClip;
        [Tooltip("Насколько сильно каждая кость участвует в повороте (грудь, шея, голова) — в сумме 1.")]
        [SerializeField] private Vector3 lookShares = new Vector3(0.4f, 0.25f, 0.35f);
        [SerializeField] private float maxYaw = 145f; // неестественный выворот через плечо

        private Animator animator;
        private Transform chest, neck, head;
        private Vector3 faceLocal = Vector3.forward; // куда смотрит морда в осях кости головы
        private PlayableGraph runGraph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable current, previous;
        private AnimationClip looping;
        private float fade = 1f, fadeTime = 0.25f;
        private bool grounded;
        private bool chaseFrozen;
        private Quaternion frozenHeadLocal;
        private float speedBeforeFreeze;
        private const float AnkleHeight = 0.09f, KneeHeight = 0.06f;

        public Vector3 LookTarget { get; set; }
        public float LookWeight { get; set; }
        // Завал головы набок вокруг направления взгляда, градусы (M-07: морда боком в щели под кроватью).
        public float HeadRoll { get; set; }
        public Transform Head => head;
        // C-02: ночная запись сама семплирует эти клипы по времени плёнки (перемотка, стоп-кадр).
        public AnimationClip WalkClip => walkClip;
        public AnimationClip StandClip => standClip;
        public float WalkClipSpeed => walkClipSpeed;
        // Под кроватью корпус уже сложен клипом — доворачивают только шея и голова.
        public static readonly Vector3 PeekLookShares = new Vector3(0f, 0.45f, 0.55f);

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

        public void Run(float speed) => Loop(runClip, speed, 0.12f);

        public void SetChaseFrozen(bool frozen)
        {
            if (chaseFrozen == frozen || animator == null) return;
            chaseFrozen = frozen;
            if (frozen)
            {
                speedBeforeFreeze = animator.speed;
                animator.speed = 0f;
                if (head != null) frozenHeadLocal = head.localRotation;
                if (runGraph.IsValid()) runGraph.Stop();
            }
            else
            {
                animator.speed = speedBeforeFreeze;
                if (head != null) head.localRotation = frozenHeadLocal;
                if (runGraph.IsValid()) runGraph.Play();
            }
        }

        public bool PlayAmbush(bool fromBehind)
        {
            var clip = Resources.Load<AnimationClip>(fromBehind
                ? "Animations/Monster/AmbushBehind" : "Animations/Monster/AmbushFront");
            if (clip == null || animator == null)
            {
                Debug.LogError("[B-03] Missing ambush animation or humanoid animator.");
                return false;
            }
            Loop(clip, 1f, 0f);
            return true;
        }

        public void SetAmbushSpeed(float speed)
        {
            if (runGraph.IsValid() && current.IsValid()) current.SetSpeed(speed);
        }

        // Шаг со скоростью metersPerSecond: клип ускоряется/замедляется под реальный ход, ступни не скользят.
        public void Walk(float metersPerSecond)
        {
            float natural = walkClip != null ? walkClip.averageSpeed.magnitude : 0f;
            if (natural < 0.1f) natural = Mathf.Max(0.1f, walkClipSpeed);
            Loop(walkClip, Mathf.Clamp(metersPerSecond / natural, 0.4f, 2f), 0.3f);
        }

        public void Stand() => Loop(standClip, 1f, 0.35f);
        public bool CanWalk => walkClip != null;

        // Резко: почти без перехода — в клипе провал занимает 0.14 с.
        public void Peek()
        {
            Loop(peekClip, 1f, 0.05f);
            grounded = true;
        }

        public bool Peeking => grounded;

        // Снять шаг/бег: дальше снова работает контроллер (Idle/Lunge).
        public void StopMoving()
        {
            SetChaseFrozen(false);
            if (runGraph.IsValid()) runGraph.Destroy();
            looping = null;
            grounded = false;
            HeadRoll = 0f;
        }

        private void Loop(AnimationClip clip, float speed, float blend)
        {
            if (animator == null || clip == null) return;
            if (runGraph.IsValid() && looping == clip)
            {
                current.SetSpeed(speed);
                return;
            }
            animator.speed = 1f;
            if (!runGraph.IsValid())
            {
                runGraph = PlayableGraph.Create("Corner Stalker");
                runGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var output = AnimationPlayableOutput.Create(runGraph, "Body", animator);
                mixer = AnimationMixerPlayable.Create(runGraph, 2);
                output.SetSourcePlayable(mixer);
                runGraph.Play();
                blend = 0f; // первый клип — сразу, без перехода из позы контроллера
            }
            // Старый «предыдущий» уходит совсем, текущий становится предыдущим и гаснет, новый нарастает.
            if (previous.IsValid())
            {
                runGraph.Disconnect(mixer, 1);
                previous.Destroy();
            }
            if (current.IsValid())
            {
                runGraph.Disconnect(mixer, 0);
                previous = current;
                mixer.ConnectInput(1, previous, 0, 1f);
            }
            current = AnimationClipPlayable.Create(runGraph, clip);
            current.SetApplyFootIK(true);
            current.SetSpeed(speed);
            mixer.ConnectInput(0, current, 0, 0f);
            looping = clip;
            fadeTime = Mathf.Max(0.01f, blend);
            fade = blend <= 0f ? 1f : 0f;
            ApplyWeights();
        }

        private void ApplyWeights()
        {
            if (!mixer.IsValid()) return;
            mixer.SetInputWeight(0, fade);
            mixer.SetInputWeight(1, previous.IsValid() ? 1f - fade : 0f);
        }

        private void Update()
        {
            if (chaseFrozen) return;
            if (fade < 1f && runGraph.IsValid())
            {
                fade = Mathf.Min(1f, fade + Time.deltaTime / fadeTime);
                ApplyWeights();
            }
        }

        // Поверх анимации: ступни на пол (Peek), потом доворот морды на цель — грудь и шея берут часть угла,
        // голова — остаток. Угол считается от того, куда морда смотрит сейчас (анимация может уводить голову в сторону).
        private void LateUpdate()
        {
            if (chaseFrozen)
            {
                if (Time.timeScale <= 0f || PauseMenuController.InputBlocked || head == null) return;
                head.localRotation = frozenHeadLocal;
                AimHead(null, null, head, faceLocal, LookTarget, 1f, new Vector3(0f, 0f, 1f), maxYaw, 0f);
                return;
            }
            if (grounded && animator != null) GroundFeet(animator, transform.position.y);
            AimHead(chest, neck, head, faceLocal, LookTarget, LookWeight, grounded ? PeekLookShares : lookShares, maxYaw, HeadRoll);
        }

        // Доворот морды на цель (грудь и шея берут часть угла, голова — остаток) и завал головы набок.
        // Угол считается от того, куда морда смотрит сейчас (анимация может уводить голову в сторону).
        // Статический — Editor-превью M-07 показывает ровно то же, что игра.
        public static void AimHead(Transform chest, Transform neck, Transform head, Vector3 faceLocal, Vector3 target,
            float weight, Vector3 shares, float maxYaw, float roll)
        {
            if (weight <= 0.001f || head == null) return;
            Vector3 desired = target - head.position;
            if (desired.sqrMagnitude < 0.0001f) return;
            Turn(chest, head, faceLocal, desired, shares.x * weight, maxYaw);
            Turn(neck, head, faceLocal, desired, shares.y / Mathf.Max(0.01f, 1f - shares.x) * weight, maxYaw);
            Turn(head, head, faceLocal, desired, weight, maxYaw);
            if (Mathf.Abs(roll) > 0.01f) head.rotation = Quaternion.AngleAxis(roll * weight, head.rotation * faceLocal) * head.rotation;
        }

        // Опора — на пол: самая низкая из ступней и коленей (на коленях опираются колени), таз (а с ним всё тело)
        // сдвигается по вертикали. Для своих клипов, где высота таза задана приблизительно; Editor-превью зовёт то же.
        public static void GroundFeet(Animator animator, float floorY)
        {
            Transform hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hipsBone == null) return;
            float lowest = float.MaxValue;
            Lowest(animator, HumanBodyBones.LeftFoot, AnkleHeight, ref lowest);
            Lowest(animator, HumanBodyBones.RightFoot, AnkleHeight, ref lowest);
            Lowest(animator, HumanBodyBones.LeftLowerLeg, KneeHeight, ref lowest);
            Lowest(animator, HumanBodyBones.RightLowerLeg, KneeHeight, ref lowest);
            if (lowest < float.MaxValue) hipsBone.position += Vector3.up * (floorY - lowest);
        }

        private static void Lowest(Animator animator, HumanBodyBones bone, float radius, ref float lowest)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null) lowest = Mathf.Min(lowest, t.position.y - radius);
        }

        private static void Turn(Transform bone, Transform head, Vector3 faceLocal, Vector3 desired, float share, float maxYaw)
        {
            if (bone == null || share <= 0f) return;
            Vector3 face = head.rotation * faceLocal;
            Quaternion full = Quaternion.FromToRotation(face, desired);
            full.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle, -maxYaw, maxYaw) * share;
            bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
        }

        private void OnDestroy()
        {
            if (runGraph.IsValid()) runGraph.Destroy();
        }
    }
}
