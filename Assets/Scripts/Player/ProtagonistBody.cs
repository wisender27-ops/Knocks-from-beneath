using UnityEngine;
using UnityEngine.Rendering;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent, DefaultExecutionOrder(10020)]
    public sealed class ProtagonistBody : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private SkinnedMeshRenderer headRenderer;
        [SerializeField] private SkinnedMeshRenderer[] headRenderers = new SkinnedMeshRenderer[0];
        [SerializeField] private Transform chest;
        [SerializeField] private Transform rightUpperArm, rightForearm, rightHand;
        [SerializeField] private Transform leftUpperArm, leftForearm, leftHand;
        [SerializeField] private Transform leftThigh, leftShin, leftFoot;
        [SerializeField] private Transform rightThigh, rightShin, rightFoot;
        [SerializeField] private float parameterDamping = 0.12f;
        [SerializeField] private float footAdjustment = 0.12f;
        [SerializeField] private Vector3 leftSoleNormal = Vector3.up;
        [SerializeField] private Vector3 rightSoleNormal = Vector3.up;
        [SerializeField] private Quaternion rightWristBoneOffset = Quaternion.identity;

        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int Crouch = Animator.StringToHash("Crouch");
        private PlayerController player;
        private CharacterController capsule;
        private PlayerInventory inventory;
        private PickupController pickup;
        private Camera view;
        private Renderer[] bodyRenderers;
        private ShadowCastingMode[] bodyShadowModes;
        private float gripWeight, carryWeight, pitch;
        private Vector3 lastGrip, lastCarry;
        private Quaternion lastWrist;
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private bool subscribed;
        private bool lockKick;
        private Vector3 kickTarget, kickStart;
        private float kickTime;

        public bool CanKickLock(Vector3 target) => rightThigh != null && rightShin != null && rightFoot != null &&
            Vector3.Distance(rightThigh.position, target) <
            (Vector3.Distance(rightThigh.position, rightShin.position) + Vector3.Distance(rightShin.position, rightFoot.position)) * 0.96f;

        public void BeginLockKick(Vector3 target)
        {
            kickTarget = target;
            kickStart = rightFoot.position;
            kickTime = 0f;
            lockKick = true;
        }

        public void EndLockKick() => lockKick = false;

        public Animator Animator => animator;
        public SkinnedMeshRenderer HeadRenderer => headRenderer;
        public SkinnedMeshRenderer[] HeadRenderers => headRenderers;

        private void Awake()
        {
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            bodyShadowModes = new ShadowCastingMode[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
                bodyShadowModes[i] = bodyRenderers[i].shadowCastingMode;
            player = GetComponentInParent<PlayerController>();
            if (player == null) return;
            capsule = player.GetComponent<CharacterController>();
            inventory = player.GetComponent<PlayerInventory>();
            pickup = player.GetComponent<PickupController>();
            view = player.GetComponentInChildren<Camera>();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                RenderPipelineManager.beginCameraRendering -= BeforeCamera;
                RenderPipelineManager.endCameraRendering -= AfterCamera;
                subscribed = false;
            }
            RestoreBodyShadows();
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            RestoreBodyShadows();
            if (view == null || camera != view || bodyRenderers == null) return;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        private void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            RestoreBodyShadows();
        }

        private void RestoreBodyShadows()
        {
            if (bodyRenderers == null) return;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].shadowCastingMode = bodyShadowModes[i];
        }

        private void Update()
        {
            if (player == null || animator == null || capsule == null || Time.deltaTime <= 0f) return;
            Vector3 velocity = player.transform.InverseTransformDirection(player.PlanarVelocity);
            float speedScale = 2f / Mathf.Max(0.01f, player.walkSpeed);
            animator.SetFloat(MoveX, velocity.x * speedScale, parameterDamping, Time.deltaTime);
            animator.SetFloat(MoveZ, velocity.z * speedScale, parameterDamping, Time.deltaTime);
            animator.SetFloat(Crouch, player.IsCrouching ? 1f : 0f, parameterDamping, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (player == null || capsule == null || Time.deltaTime <= 0f) return;
            // Визуал следует стопам капсулы; камера и физика остаются под управлением PlayerController.
            transform.localPosition = new Vector3(0f, capsule.center.y - capsule.height * 0.5f, -0.10f);
            if (lockKick)
            {
                kickTime += Time.deltaTime;
                Vector3 windup = kickStart - player.transform.forward * 0.12f + Vector3.up * 0.28f;
                Vector3 target = kickTime < 0.35f
                    ? Vector3.Lerp(kickStart, windup, Mathf.SmoothStep(0f, 1f, kickTime / 0.35f))
                    : kickTime < 0.53f
                        ? Vector3.Lerp(windup, kickTarget, Mathf.Clamp01((kickTime - 0.35f) / 0.18f))
                        : Vector3.Lerp(kickTarget, kickStart, Mathf.SmoothStep(0f, 1f, (kickTime - 0.53f) / 0.42f));
                SolveLimb(rightThigh, rightShin, rightFoot, target,
                    rightThigh.position + player.transform.forward + player.transform.right * 0.25f, 1f);
                return;
            }
            if (view == null || !capsule.enabled || !player.enabled || player.isCameraLocked) return;
            float desiredPitch = Mathf.Asin(Mathf.Clamp(-view.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Lerp(pitch, desiredPitch, 1f - Mathf.Exp(-12f * Time.deltaTime));
            chest.localRotation *= Quaternion.Euler(Mathf.Clamp(pitch * 0.15f, -10f, 12f), 0f, 0f);

            GameObject held = pickup != null ? pickup.GetHeldObject() : null;
            Transform item = held == null ? EquippedItem() : null;
            float step = Time.deltaTime * 7f;
            gripWeight = Mathf.MoveTowards(gripWeight, item != null ? 1f : 0f, step);
            carryWeight = Mathf.MoveTowards(carryWeight, held != null ? 1f : 0f, step);
            if (item != null)
            {
                lastGrip = item.position;
                if (inventory != null && item == inventory.flashlightInHand?.transform)
                    lastGrip += item.TransformDirection(new Vector3(0f, -0.015f, 0.07f));
                lastWrist = view.transform.rotation * Quaternion.Euler(-85f, 0f, 8f) * rightWristBoneOffset;
            }
            if (gripWeight > 0f)
            {
                SolveLimb(rightUpperArm, rightForearm, rightHand, lastGrip,
                    player.transform.TransformPoint(new Vector3(0.65f, 1.05f, -0.18f)), gripWeight);
                rightHand.rotation = Quaternion.Slerp(rightHand.rotation, lastWrist, gripWeight);
            }
            if (held != null) lastCarry = held.transform.position;
            if (carryWeight > 0f)
            {
                SolveLimb(leftUpperArm, leftForearm, leftHand, lastCarry - view.transform.right * 0.18f,
                    player.transform.TransformPoint(new Vector3(-0.65f, 0.95f, 0f)), carryWeight);
                SolveLimb(rightUpperArm, rightForearm, rightHand, lastCarry + view.transform.right * 0.18f,
                    player.transform.TransformPoint(new Vector3(0.65f, 0.95f, 0f)), carryWeight);
            }
            if (capsule.isGrounded)
            {
                PlantFoot(leftThigh, leftShin, leftFoot, leftSoleNormal);
                PlantFoot(rightThigh, rightShin, rightFoot, rightSoleNormal);
            }
        }

        private Transform EquippedItem()
        {
            if (inventory == null) return null;
            if (inventory.flashlightInHand != null && inventory.flashlightInHand.activeInHierarchy)
                return inventory.flashlightInHand.transform;
            if (inventory.crowbarInHand != null && inventory.crowbarInHand.activeInHierarchy)
                return inventory.crowbarInHand.transform;
            if (inventory.hammerInHand != null && inventory.hammerInHand.activeInHierarchy)
                return inventory.hammerInHand.transform;
            return null;
        }

        private void PlantFoot(Transform thigh, Transform shin, Transform foot, Vector3 soleNormal)
        {
            Vector3 origin = foot.position + Vector3.up * 0.22f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 0.44f,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit ground = default;
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].collider.transform.IsChildOf(player.transform) || groundHits[i].distance >= nearest) continue;
                nearest = groundHits[i].distance;
                ground = groundHits[i];
            }
            if (float.IsInfinity(nearest)) return;
            float delta = ground.point.y + 0.105f * transform.lossyScale.y - foot.position.y;
            if (Mathf.Abs(delta) > footAdjustment) return;
            SolveLimb(thigh, shin, foot, foot.position + Vector3.up * delta,
                thigh.position + player.transform.forward, 0.8f);
            foot.rotation = Quaternion.Slerp(foot.rotation,
                Quaternion.FromToRotation(foot.TransformDirection(soleNormal), ground.normal) * foot.rotation, 0.65f);
        }

        public static void SolveLimb(Transform upper, Transform lower, Transform tip,
            Vector3 target, Vector3 pole, float weight)
        {
            if (upper == null || lower == null || tip == null || weight <= 0f) return;
            Vector3 origin = upper.position;
            float a = Vector3.Distance(origin, lower.position);
            float b = Vector3.Distance(lower.position, tip.position);
            if (a < 0.001f || b < 0.001f) return;
            Vector3 direction = target - origin;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            if (direction.sqrMagnitude < 0.000001f) return;
            direction.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(pole - origin, direction).normalized;
            if (bend.sqrMagnitude < 0.001f) bend = Vector3.ProjectOnPlane(upper.forward, direction).normalized;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            Vector3 elbow = origin + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Quaternion upperPose = Quaternion.FromToRotation(lower.position - origin, elbow - origin) * upper.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, upperPose, weight);
            Quaternion lowerPose = Quaternion.FromToRotation(tip.position - lower.position,
                origin + direction * distance - lower.position) * lower.rotation;
            lower.rotation = Quaternion.Slerp(lower.rotation, lowerPose, weight);
        }

#if UNITY_EDITOR
        public void ConfigureHeadRenderers(SkinnedMeshRenderer[] heads)
        {
            headRenderers = heads ?? new SkinnedMeshRenderer[0];
            headRenderer = headRenderers.Length > 0 ? headRenderers[0] : null;
        }

        public void Configure(Animator rigAnimator, SkinnedMeshRenderer head, Transform[] bones)
        {
            animator = rigAnimator;
            headRenderer = head;
            headRenderers = head != null ? new[] { head } : new SkinnedMeshRenderer[0];
            chest = bones[2];
            leftUpperArm = bones[5]; leftForearm = bones[6]; leftHand = bones[7];
            rightUpperArm = bones[8]; rightForearm = bones[9]; rightHand = bones[10];
            leftThigh = bones[11]; leftShin = bones[12]; leftFoot = bones[13];
            rightThigh = bones[14]; rightShin = bones[15]; rightFoot = bones[16];
            leftSoleNormal = Quaternion.Inverse(leftFoot.rotation) * transform.up;
            rightSoleNormal = Quaternion.Inverse(rightFoot.rotation) * transform.up;
            rightWristBoneOffset = Quaternion.identity;
            if (animator.avatar != null && animator.avatar.isHuman)
            {
                var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
                var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var pinky = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                if (middle != null && index != null && pinky != null)
                {
                    Vector3 fingers = (middle.position - rightHand.position).normalized;
                    Vector3 palmNormal = Vector3.Cross(index.position - pinky.position, fingers).normalized;
                    if (Vector3.Dot(palmNormal, transform.forward) < 0f) palmNormal = -palmNormal;
                    if (palmNormal.sqrMagnitude > .5f && fingers.sqrMagnitude > .5f)
                    {
                        // Старый хват задан в анатомической системе: пальцы вниз, ладонь вперёд.
                        Quaternion palmFrame = Quaternion.LookRotation(palmNormal, -fingers);
                        rightWristBoneOffset = Quaternion.Inverse(palmFrame) * rightHand.rotation;
                    }
                }
            }
        }
#endif
    }
}

