using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using System.Collections.Generic;

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
        [SerializeField] private Mesh firstPersonArmsMesh;
        [SerializeField, Range(1f, 2f)] private float viewForearmLength = 1.8f;

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
        private SkinnedMeshRenderer firstPersonArms;
        private Mesh viewForearmsMesh;
        private float rightCarryWeight, leftCarryWeight, pitch;
        private Vector3 lastCarry, lastRightGripTarget;
        private Quaternion lastRightGripRotation;
        private Vector3 referenceRightHandLocalPosition;
        private bool hasReferenceRightHandPose;
        private readonly RaycastHit[] groundHits = new RaycastHit[12];
        private bool subscribed;
        private bool lockKick;
        private Vector3 kickTarget, kickStart;
        private float kickTime;
        private RigBuilder carryBuilder;
        private TwoBoneIKConstraint rightCarryIK, leftCarryIK;
        private Transform rightCarryTarget, leftCarryTarget, rightElbowHint, leftElbowHint;
        private Vector3 rightPalmOffset, leftPalmOffset;
        private Quaternion rightPalmRotation, leftPalmRotation;
        private float carryWeight;
        private Transform rightViewUpper, rightViewLower, rightViewHand, leftViewUpper, leftViewLower, leftViewHand;
        private readonly Dictionary<Transform, Transform> viewBones = new Dictionary<Transform, Transform>();
        private readonly List<(Transform source, Transform target, Vector3 axis, float angle)> gripFingers =
            new List<(Transform, Transform, Vector3, float)>();

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
        public Transform RightHand => rightHand;
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
            CreateFirstPersonArms();
        }

        private void Start()
        {
            if (player == null || animator == null || !animator.isHuman) return;
            var rigObject = new GameObject("Carry Rig");
            rigObject.transform.SetParent(animator.transform, false);
            Rig rig = rigObject.AddComponent<Rig>();
            rightViewUpper = CloneViewBones(rightUpperArm, rigObject.transform);
            leftViewUpper = CloneViewBones(leftUpperArm, rigObject.transform);
            rightViewLower = viewBones[rightForearm]; rightViewHand = viewBones[rightHand];
            leftViewLower = viewBones[leftForearm]; leftViewHand = viewBones[leftHand];
            rightViewLower.localScale *= viewForearmLength;
            leftViewLower.localScale *= viewForearmLength;
            rightViewHand.localScale /= viewForearmLength;
            leftViewHand.localScale /= viewForearmLength;
            if (firstPersonArms != null)
            {
                var bones = firstPersonArms.bones;
                for (int i = 0; i < bones.Length; i++)
                    if (viewBones.TryGetValue(bones[i], out Transform replacement)) bones[i] = replacement;
                firstPersonArms.bones = bones;
            }
            rightCarryIK = CreateCarryConstraint(rigObject.transform, "Right", rightViewUpper, rightViewLower,
                rightViewHand, out rightCarryTarget, out rightElbowHint);
            leftCarryIK = CreateCarryConstraint(rigObject.transform, "Left", leftViewUpper, leftViewLower,
                leftViewHand, out leftCarryTarget, out leftElbowHint);
            CalibratePalm(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightLittleProximal, rightHand, out rightPalmOffset, out rightPalmRotation);
            CalibratePalm(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftLittleProximal, leftHand, out leftPalmOffset, out leftPalmRotation);
            ConfigureGripFingers(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightLittleDistal, rightHand, rightPalmRotation);
            ConfigureGripFingers(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftLittleDistal, leftHand, leftPalmRotation);
            carryBuilder = animator.gameObject.AddComponent<RigBuilder>();
            carryBuilder.enabled = false;
            carryBuilder.layers.Add(new RigLayer(rig));
            // Официальный Manual Evaluation: IK вычисляется после камеры и интерполяции Rigidbody.
            if (carryBuilder.Build()) carryBuilder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            else Debug.LogError("Failed to build carry Animation Rigging graph.", this);
        }

        private Transform CloneViewBones(Transform source, Transform parent)
        {
            var clone = new GameObject("View " + source.name).transform;
            clone.gameObject.AddComponent<RigTransform>();
            clone.SetParent(parent, false);
            clone.SetPositionAndRotation(source.position, source.rotation);
            Vector3 sourceScale = source.lossyScale, parentScale = parent.lossyScale;
            clone.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z);
            viewBones.Add(source, clone);
            foreach (Transform child in source) CloneViewBones(child, clone);
            return clone;
        }

        private TwoBoneIKConstraint CreateCarryConstraint(Transform rig, string side, Transform upper,
            Transform lower, Transform hand, out Transform target, out Transform hint)
        {
            var node = new GameObject(side + " Carry IK");
            node.transform.SetParent(rig, false);
            target = new GameObject(side + " Grip Target").transform;
            hint = new GameObject(side + " Elbow Hint").transform;
            target.SetParent(rig, false);
            hint.SetParent(rig, false);
            target.SetPositionAndRotation(hand.position, hand.rotation);
            hint.position = lower.position;
            var constraint = node.AddComponent<TwoBoneIKConstraint>();
            var data = constraint.data;
            data.root = upper; data.mid = lower; data.tip = hand;
            data.target = target; data.hint = hint;
            data.targetPositionWeight = data.targetRotationWeight = data.hintWeight = 1f;
            data.maintainTargetPositionOffset = data.maintainTargetRotationOffset = false;
            constraint.data = data;
            constraint.weight = 0f;
            return constraint;
        }

        private void CalibratePalm(HumanBodyBones middleBone, HumanBodyBones indexBone,
            HumanBodyBones littleBone, Transform hand, out Vector3 offset, out Quaternion rotation)
        {
            var middle = animator.GetBoneTransform(middleBone);
            var index = animator.GetBoneTransform(indexBone);
            var little = animator.GetBoneTransform(littleBone);
            offset = Vector3.zero;
            rotation = Quaternion.identity;
            if (middle == null || index == null || little == null) return;
            Vector3 fingers = (middle.position - hand.position).normalized;
            Vector3 normal = Vector3.Cross(index.position - little.position, fingers).normalized;
            if (Vector3.Dot(normal, transform.forward) < 0f) normal = -normal;
            if (normal.sqrMagnitude < 0.5f || fingers.sqrMagnitude < 0.5f) return;
            Quaternion palmFrame = Quaternion.LookRotation(normal, fingers);
            rotation = Quaternion.Inverse(palmFrame) * hand.rotation;
            Vector3 palmCenter = Vector3.Lerp(hand.position, (index.position + little.position) * 0.5f, 0.65f);
            offset = Quaternion.Inverse(hand.rotation) * (palmCenter - hand.position);
        }

        private void ConfigureGripFingers(HumanBodyBones first, HumanBodyBones last, Transform hand, Quaternion palmRotation)
        {
            Vector3 normal = hand.rotation * Quaternion.Inverse(palmRotation) * Vector3.forward;
            for (int bone = (int)first; bone <= (int)last; bone++)
            {
                Transform source = animator.GetBoneTransform((HumanBodyBones)bone);
                if (source == null || source.childCount == 0 || !viewBones.TryGetValue(source, out Transform target)) continue;
                Vector3 direction = (source.GetChild(0).position - source.position).normalized;
                Vector3 axis = source.InverseTransformDirection(Vector3.Cross(direction, normal)).normalized;
                int segment = (bone - (int)first) % 3;
                float angle = bone - (int)first < 3 ? 12f : segment == 1 ? 35f : 20f;
                gripFingers.Add((source, target, axis, angle));
            }
        }

        private bool EvaluateCarryRig()
        {
            if (carryBuilder == null || !carryBuilder.graph.IsValid()) return false;
            bool carrying = pickup != null && pickup.GetHeldObject() != null && pickup.IsHeldObjectTwoHanded &&
                player.enabled && capsule.enabled && !player.isCameraLocked && !lockKick;
            carryWeight = Mathf.MoveTowards(carryWeight, carrying ? 1f : 0f, Time.deltaTime * 10f);
            rightCarryIK.weight = leftCarryIK.weight = carryWeight;
            // Отдельные плечи для viewmodel: высота мировой модели не ограничивает FPS-хват.
            foreach (var pair in viewBones)
                if (pair.Key != rightUpperArm && pair.Key != leftUpperArm)
                {
                    float lengthScale = carrying || carryWeight > 0f ? viewForearmLength : 1f;
                    pair.Value.localPosition = pair.Key.localPosition;
                    pair.Value.localScale = pair.Key.localScale *
                        (pair.Key == rightForearm || pair.Key == leftForearm ? lengthScale :
                         pair.Key == rightHand || pair.Key == leftHand ? 1f / lengthScale : 1f);
                    pair.Value.localRotation = pair.Key.localRotation;
                }
            foreach (var finger in gripFingers)
                finger.target.localRotation = finger.source.localRotation * Quaternion.AngleAxis(finger.angle * carryWeight, finger.axis);
            if (carrying)
            {
                Quaternion rightRotation = pickup.PrimaryGripRotation * rightPalmRotation;
                Quaternion leftRotation = pickup.SecondaryGripRotation * leftPalmRotation;
                rightCarryTarget.SetPositionAndRotation(pickup.PrimaryHandGripTarget - rightRotation * rightPalmOffset, rightRotation);
                leftCarryTarget.SetPositionAndRotation(pickup.SecondaryHandGripTarget - leftRotation * leftPalmOffset, leftRotation);
                rightElbowHint.position = view.transform.TransformPoint(new Vector3(0.75f, -1f, -0.2f));
                leftElbowHint.position = view.transform.TransformPoint(new Vector3(-0.75f, -1f, -0.2f));
            }
            Vector3 rightLocal = view.transform.InverseTransformPoint(rightCarryTarget.position);
            Vector3 leftLocal = view.transform.InverseTransformPoint(leftCarryTarget.position);
            float spread = Mathf.Max(.26f, Mathf.Max(Mathf.Abs(rightLocal.x), Mathf.Abs(leftLocal.x)) - .18f);
            float depth = Mathf.Max(.1f, Mathf.Max(rightLocal.z, leftLocal.z) - .40f);
            float height = Mathf.Min(-.6f, Mathf.Min(rightLocal.y, leftLocal.y) - .36f);
            rightViewUpper.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(spread, height, depth)), rightUpperArm.rotation);
            leftViewUpper.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(-spread, height, depth)), leftUpperArm.rotation);
            carryBuilder.Evaluate(Time.deltaTime);
            return carrying || carryWeight > 0f;
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
            if (carryBuilder != null && carryBuilder.graph.IsValid()) carryBuilder.graph.Stop();
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            RestoreBodyShadows();
            if (view == null || camera != view || bodyRenderers == null) return;
            // Исходный меш целиком остаётся невидимым от камеры игрока.
            bool showHands = (pickup != null && pickup.GetHeldObject() != null) || EquippedItem() != null;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            if (firstPersonArms != null)
                firstPersonArms.shadowCastingMode = showHands ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }

        private void CreateFirstPersonArms()
        {
            if (animator == null || firstPersonArmsMesh == null) return;

            SkinnedMeshRenderer source = null;
            SkinnedMeshRenderer[] candidates = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                SkinnedMeshRenderer candidate = candidates[i];
                if (candidate == headRenderer || candidate.sharedMesh == null) continue;
                bool isHead = false;
                for (int h = 0; headRenderers != null && h < headRenderers.Length; h++)
                    if (candidate == headRenderers[h]) { isHead = true; break; }
                if (isHead) continue;
                if (source == null || candidate.sharedMesh.vertexCount > source.sharedMesh.vertexCount)
                    source = candidate;
            }
            if (source == null) return;

            GameObject armsObject = new GameObject("First Person Arms");
            armsObject.transform.SetParent(source.transform.parent, false);
            armsObject.transform.localPosition = source.transform.localPosition;
            armsObject.transform.localRotation = source.transform.localRotation;
            armsObject.transform.localScale = source.transform.localScale;
            firstPersonArms = armsObject.AddComponent<SkinnedMeshRenderer>();
            viewForearmsMesh = CreateForearmsMesh(firstPersonArmsMesh, source.bones);
            firstPersonArms.sharedMesh = viewForearmsMesh;
            firstPersonArms.bones = source.bones;
            firstPersonArms.rootBone = source.rootBone;
            firstPersonArms.sharedMaterials = source.sharedMaterials;
            firstPersonArms.localBounds = source.localBounds;
            firstPersonArms.quality = source.quality;
            firstPersonArms.updateWhenOffscreen = true;
            firstPersonArms.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            firstPersonArms.receiveShadows = source.receiveShadows;
        }

        private Mesh CreateForearmsMesh(Mesh source, Transform[] bones)
        {
            Mesh mesh = Instantiate(source);
            mesh.name = "First person forearms";
            var forearmBones = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                forearmBones[i] = bones[i] != null &&
                    (bones[i] == leftForearm || bones[i].IsChildOf(leftForearm) ||
                     bones[i] == rightForearm || bones[i].IsChildOf(rightForearm));
            BoneWeight[] weights = source.boneWeights;
            var keep = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float influence = (forearmBones[w.boneIndex0] ? w.weight0 : 0f) +
                    (forearmBones[w.boneIndex1] ? w.weight1 : 0f) +
                    (forearmBones[w.boneIndex2] ? w.weight2 : 0f) +
                    (forearmBones[w.boneIndex3] ? w.weight3 : 0f);
                keep[i] = influence >= .5f;
            }
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = source.GetTriangles(submesh);
                var visible = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (!keep[triangles[i]] || !keep[triangles[i + 1]] || !keep[triangles[i + 2]]) continue;
                    visible.Add(triangles[i]); visible.Add(triangles[i + 1]); visible.Add(triangles[i + 2]);
                }
                mesh.SetTriangles(visible, submesh, false);
            }
            return mesh;
        }

        private void OnDestroy()
        {
            if (carryBuilder != null) carryBuilder.Clear();
            if (firstPersonArms != null) Destroy(firstPersonArms.gameObject);
            if (viewForearmsMesh != null) Destroy(viewForearmsMesh);
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
            if (firstPersonArms != null)
                firstPersonArms.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
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
            if (carryBuilder != null && carryBuilder.graph.IsValid() && !carryBuilder.graph.IsPlaying())
                carryBuilder.graph.Play();
            bool rigCarrying = EvaluateCarryRig();
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
            if (rigCarrying)
            {
                rightCarryWeight = leftCarryWeight = 0f;
                if (capsule.isGrounded)
                {
                    PlantFoot(leftThigh, leftShin, leftFoot, leftSoleNormal);
                    PlantFoot(rightThigh, rightShin, rightFoot, rightSoleNormal);
                }
                return;
            }
            float desiredPitch = Mathf.Asin(Mathf.Clamp(-view.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Lerp(pitch, desiredPitch, 1f - Mathf.Exp(-12f * Time.deltaTime));
            chest.localRotation *= Quaternion.Euler(Mathf.Clamp(pitch * 0.15f, -10f, 12f), 0f, 0f);

            GameObject held = pickup != null ? pickup.GetHeldObject() : null;
            Transform equipped = held == null ? EquippedItem() : null;
            float step = Time.deltaTime * 7f;
            bool gripping = held != null || equipped != null;
            bool twoHanded = pickup != null && pickup.IsHeldObjectTwoHanded;
            bool lockHeldWrist = twoHanded && pickup.IsHeldObjectAttachedToHand;
            rightCarryWeight = Mathf.MoveTowards(rightCarryWeight, gripping ? 1f : 0f, step);
            leftCarryWeight = Mathf.MoveTowards(leftCarryWeight, twoHanded ? 1f : 0f, step);
            if (held != null) lastCarry = held.transform.position;

            if (rightHand != null)
            {
                Vector3 animatedHandLocal = player.transform.InverseTransformPoint(rightHand.position);
                if (!hasReferenceRightHandPose)
                {
                    referenceRightHandLocalPosition = animatedHandLocal;
                    hasReferenceRightHandPose = true;
                }
                Vector3 animationOffset = player.transform.TransformVector(animatedHandLocal - referenceRightHandLocalPosition);
                if (gripping)
                {
                    Vector3 gripBase = held != null
                        ? twoHanded ? pickup.PrimaryHandGripTarget
                            : pickup != null && pickup.holdPoint != null
                                ? pickup.holdPoint.position + view.transform.right * 0.18f
                                : lastCarry + view.transform.right * 0.18f
                        : inventory != null && inventory.HasEquippedItemAttachedToHand
                            ? inventory.EquippedHandTargetPosition
                            : equipped.position;
                    lastRightGripTarget = gripBase + animationOffset;
                    lastRightGripRotation = view.transform.rotation * Quaternion.Euler(-85f, 0f, 8f) * rightWristBoneOffset;
                }
            }
            if (rightCarryWeight > 0f)
            {
                SolveLimb(rightUpperArm, rightForearm, rightHand, lastRightGripTarget,
                    player.transform.TransformPoint(new Vector3(0.65f, 0.95f, 0f)), rightCarryWeight);
                if (equipped != null || lockHeldWrist)
                {
                    Quaternion wristTarget = lockHeldWrist ? pickup.HeldHandRotationTarget : lastRightGripRotation;
                    rightHand.rotation = Quaternion.Slerp(rightHand.rotation, wristTarget, rightCarryWeight);
                }
            }
            if (leftCarryWeight > 0f)
            {
                Vector3 leftGripTarget = twoHanded ? pickup.SecondaryHandGripTarget : lastCarry - view.transform.right * 0.18f;
                SolveLimb(leftUpperArm, leftForearm, leftHand, leftGripTarget,
                    player.transform.TransformPoint(new Vector3(-0.65f, 0.95f, 0f)), leftCarryWeight);
            }
            if (capsule.isGrounded)
            {
                PlantFoot(leftThigh, leftShin, leftFoot, leftSoleNormal);
                PlantFoot(rightThigh, rightShin, rightFoot, rightSoleNormal);
            }
            if (!rigCarrying)
                foreach (var pair in viewBones)
                    pair.Value.SetPositionAndRotation(pair.Key.position, pair.Key.rotation);
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

