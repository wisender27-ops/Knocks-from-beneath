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
        [SerializeField] private GameObject firstPersonArmsPrefab;
        private Transform modelRightUpper, modelLeftUpper, modelRightHand, modelLeftHand;
        private Transform[] modelBones;
        private float rightViewReach, leftViewReach;
        private GameObject firstPersonModel;
        private readonly Dictionary<HumanBodyBones, Transform> armBoneCache = new Dictionary<HumanBodyBones, Transform>();
        [SerializeField] private Vector3 readyHandPosition = new Vector3(0.19f, -0.27f, 0.39f);
        [SerializeField, Range(0f, 1f)] private float handMotion = 1f;
        private float motionPhase;
        private Quaternion previousViewRotation;
        private Vector2 viewSway;
        private float rightGripWeight, leftGripWeight;
        private Vector3 lastLeftGripTarget;
        private Quaternion lastLeftGripRotation = Quaternion.identity;

        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveZ = Animator.StringToHash("MoveZ");
        private static readonly int Crouch = Animator.StringToHash("Crouch");
        private PlayerController player;
        private CharacterController capsule;
        private PlayerInventory inventory;
        private PickupController pickup;
        private PlayerDoorOpen doorInput;
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
        private Quaternion rightPalmRotation = Quaternion.identity, leftPalmRotation = Quaternion.identity;
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
        public Transform ViewRightHand => rightViewHand;
        public Transform ViewLeftHand => leftViewHand;
        public Vector3 ReadyRightHandPosition => view != null ? view.transform.TransformPoint(readyHandPosition) : rightHand.position;
        public Vector3 ReadyRightPalmPosition => ReadyRightHandPosition + ReadyRightHandRotation * rightPalmOffset;
        public Quaternion ReadyRightPalmRotation => ReadyRightHandRotation * Quaternion.Inverse(rightPalmRotation);
        public Quaternion ReadyRightHandRotation => view != null ? view.transform.rotation * Quaternion.LookRotation(Vector3.left, new Vector3(0f, .25f, 1f)) * rightPalmRotation : rightHand.rotation;
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
            doorInput = player.GetComponentInChildren<PlayerDoorOpen>();
            view = player.GetComponentInChildren<Camera>();
            CreateFirstPersonArms();
        }

        private void Start()
        {
            if (player == null || animator == null || !animator.isHuman) return;
            var rigObject = new GameObject("Carry Rig");
            rigObject.transform.SetParent(animator.transform, false);
            Rig rig = rigObject.AddComponent<Rig>();
            rightViewUpper = CloneViewBones(modelRightUpper != null ? modelRightUpper : rightUpperArm, rigObject.transform);
            leftViewUpper = CloneViewBones(modelLeftUpper != null ? modelLeftUpper : leftUpperArm, rigObject.transform);
            rightViewLower = viewBones[GetArmBone(HumanBodyBones.RightLowerArm)]; rightViewHand = viewBones[GetArmBone(HumanBodyBones.RightHand)];
            leftViewLower = viewBones[GetArmBone(HumanBodyBones.LeftLowerArm)]; leftViewHand = viewBones[GetArmBone(HumanBodyBones.LeftHand)];
            rightViewReach = Vector3.Distance(rightViewUpper.position, rightViewLower.position) + Vector3.Distance(rightViewLower.position, rightViewHand.position);
            leftViewReach = Vector3.Distance(leftViewUpper.position, leftViewLower.position) + Vector3.Distance(leftViewLower.position, leftViewHand.position);
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
                HumanBodyBones.RightLittleProximal, GetArmBone(HumanBodyBones.RightHand), out rightPalmOffset, out rightPalmRotation);
            CalibratePalm(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftLittleProximal, GetArmBone(HumanBodyBones.LeftHand), out leftPalmOffset, out leftPalmRotation);
            ConfigureGripFingers(HumanBodyBones.RightThumbProximal, HumanBodyBones.RightLittleDistal, GetArmBone(HumanBodyBones.RightHand), rightPalmRotation);
            ConfigureGripFingers(HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftLittleDistal, GetArmBone(HumanBodyBones.LeftHand), leftPalmRotation);
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
            constraint.weight = 1f;
            return constraint;
        }

        private void CalibratePalm(HumanBodyBones middleBone, HumanBodyBones indexBone,
            HumanBodyBones littleBone, Transform hand, out Vector3 offset, out Quaternion rotation)
        {
            var middle = GetArmBone(middleBone);
            var index = GetArmBone(indexBone);
            var little = GetArmBone(littleBone);
            offset = Vector3.zero;
            rotation = Quaternion.identity;
            Transform finger = middle != null ? middle : index;
            if (finger == null || hand == null) return;
            Vector3 fingers = (finger.position - hand.position).normalized;
            Vector3 normal = index != null && little != null
                ? Vector3.Cross(index.position - little.position, fingers).normalized
                : Vector3.ProjectOnPlane(transform.forward, fingers).normalized;
            if (Vector3.Dot(normal, transform.forward) < 0f) normal = -normal;
            if (normal.sqrMagnitude < 0.5f || fingers.sqrMagnitude < 0.5f) return;
            Quaternion palmFrame = Quaternion.LookRotation(normal, fingers);
            rotation = Quaternion.Inverse(palmFrame) * hand.rotation;
            Vector3 knuckles = index != null && little != null ? (index.position + little.position) * .5f : finger.position;
            Vector3 palmCenter = Vector3.Lerp(hand.position, knuckles, 0.65f);
            offset = Quaternion.Inverse(hand.rotation) * (palmCenter - hand.position);
        }

        private void ConfigureGripFingers(HumanBodyBones first, HumanBodyBones last, Transform hand, Quaternion palmRotation)
        {
            Vector3 normal = hand.rotation * Quaternion.Inverse(palmRotation) * Vector3.forward;
            for (int bone = (int)first; bone <= (int)last; bone++)
            {
                Transform source = GetArmBone((HumanBodyBones)bone);
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
            if (carryBuilder == null || !carryBuilder.graph.IsValid() || view == null) return false;
            float dt = Time.deltaTime;
            bool held = pickup != null && pickup.GetHeldObject() != null;
            bool equipped = !held && EquippedItem() != null;
            bool twoHanded = held && pickup.IsHeldObjectTwoHanded;
            bool touchingDoor = !held && !equipped && doorInput != null && doorInput.HeldDoor != null;
            rightGripWeight = Mathf.MoveTowards(rightGripWeight, held || equipped || touchingDoor ? 1f : 0f, dt * 6f);
            leftGripWeight = Mathf.MoveTowards(leftGripWeight, twoHanded ? 1f : 0f, dt * 6f);
            rightCarryIK.weight = leftCarryIK.weight = 1f;
            foreach (var pair in viewBones)
            {
                if (pair.Value != rightViewUpper && pair.Value != leftViewUpper)
                {
                    pair.Value.localPosition = pair.Key.localPosition;
                    pair.Value.localScale = pair.Key.localScale;
                }
                pair.Value.localRotation = pair.Key.localRotation;
            }
            foreach (var finger in gripFingers)
            {
                bool right = finger.source.IsChildOf(GetArmBone(HumanBodyBones.RightHand));
                float curl = Mathf.Lerp(.18f, 1f, right ? rightGripWeight : leftGripWeight);
                finger.target.localRotation = finger.source.localRotation * Quaternion.AngleAxis(finger.angle * curl, finger.axis);
            }
            float speed = player.PlanarVelocity.magnitude;
            float movement = Mathf.Clamp(speed / Mathf.Max(.01f, player.walkSpeed), 0f, 1.8f);
            motionPhase += dt * Mathf.Lerp(1.6f, 9f, Mathf.Clamp01(movement));
            Quaternion delta = Quaternion.Inverse(previousViewRotation) * view.transform.rotation;
            Vector3 angles = delta.eulerAngles;
            Vector2 angularMotion = new Vector2(Mathf.DeltaAngle(0f, angles.y), Mathf.DeltaAngle(0f, angles.x));
            if (previousViewRotation == default) angularMotion = Vector2.zero;
            previousViewRotation = view.transform.rotation;
            Vector2 desiredSway = Vector2.ClampMagnitude(angularMotion * -.0018f, .025f);
            viewSway = Vector2.Lerp(viewSway, desiredSway, 1f - Mathf.Exp(-12f * dt));
            Vector3 sway = new Vector3(viewSway.x + Mathf.Sin(motionPhase) * .007f * movement,
                -viewSway.y + Mathf.Cos(motionPhase * 2f) * .005f * movement + Mathf.Sin(Time.time * 1.6f) * .003f, 0f) * handMotion;
            Quaternion cameraRotation = view.transform.rotation;
            Vector3 rightIdle = view.transform.TransformPoint(readyHandPosition + sway);
            Vector3 leftIdle = view.transform.TransformPoint(new Vector3(-readyHandPosition.x, readyHandPosition.y, readyHandPosition.z) + sway);
            Quaternion rightIdleRotation = cameraRotation * Quaternion.LookRotation(Vector3.left, new Vector3(0f, .25f, 1f)) * rightPalmRotation;
            Quaternion leftIdleRotation = cameraRotation * Quaternion.LookRotation(Vector3.right, new Vector3(0f, .25f, 1f)) * leftPalmRotation;
            if (held)
            {
                lastRightGripRotation = pickup.PrimaryGripRotation * rightPalmRotation;
                lastRightGripTarget = pickup.PrimaryHandGripTarget - lastRightGripRotation * rightPalmOffset;
                if (twoHanded)
                {
                    lastLeftGripRotation = pickup.SecondaryGripRotation * leftPalmRotation;
                    lastLeftGripTarget = pickup.SecondaryHandGripTarget - lastLeftGripRotation * leftPalmOffset;
                }
            }
            else if (equipped)
            {
                lastRightGripRotation = inventory.EquippedHandTargetRotation * rightPalmRotation;
                lastRightGripTarget = inventory.EquippedHandTargetPosition - lastRightGripRotation * rightPalmOffset;
            }
            if (touchingDoor)
            {
                Vector3 normal = doorInput.HeldDoor.transform.forward;
                if (Vector3.Dot(normal, doorInput.HeldContactPoint - view.transform.position) < 0f) normal = -normal;
                lastRightGripRotation = Quaternion.LookRotation(normal, Vector3.up) * rightPalmRotation;
                lastRightGripTarget = doorInput.HeldContactPoint - lastRightGripRotation * rightPalmOffset;
            }
            rightCarryTarget.SetPositionAndRotation(Vector3.Lerp(rightIdle, lastRightGripTarget, rightGripWeight),
                Quaternion.Slerp(rightIdleRotation, lastRightGripRotation, rightGripWeight));
            leftCarryTarget.SetPositionAndRotation(Vector3.Lerp(leftIdle, lastLeftGripTarget, leftGripWeight),
                Quaternion.Slerp(leftIdleRotation, lastLeftGripRotation, leftGripWeight));
            rightViewUpper.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(.21f, -.34f, .05f)), rightUpperArm.rotation);
            leftViewUpper.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(-.21f, -.34f, .05f)), leftUpperArm.rotation);
            if (modelRightUpper != null)
            {
                // Keep bone lengths intact when an item exceeds the resting reach.
                rightViewUpper.position = ReachableShoulder(rightViewUpper.position, rightCarryTarget.position, rightViewReach);
                leftViewUpper.position = ReachableShoulder(leftViewUpper.position, leftCarryTarget.position, leftViewReach);
            }
            rightElbowHint.position = view.transform.TransformPoint(new Vector3(.52f, -.58f, .02f));
            leftElbowHint.position = view.transform.TransformPoint(new Vector3(-.52f, -.58f, .02f));
            carryBuilder.Evaluate(dt);
            return true;
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
            bool showHands = enabled &&
                ((pickup != null && pickup.GetHeldObject() != null) ||
                 EquippedItem() != null ||
                 (doorInput != null && doorInput.HeldDoor != null));
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            if (firstPersonArms != null)
                firstPersonArms.forceRenderingOff = !showHands;
        }

        private Vector3 ReachableShoulder(Vector3 shoulder, Vector3 target, float reach)
        {
            Vector3 offset = shoulder - target;
            float distance = offset.magnitude;
            if (distance <= reach * .96f) return shoulder;
            shoulder = target + offset * (reach * .96f / distance);
            Vector3 lowerEdgeNormal = (view.transform.up + view.transform.forward *
                Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f)).normalized;
            if (Vector3.Dot(shoulder - view.transform.position, lowerEdgeNormal) > -.025f)
                shoulder = target - lowerEdgeNormal * (reach * .96f);
            return shoulder;
        }

        private Transform GetArmBone(HumanBodyBones bone)
        {
            if (modelRightUpper == null) return animator.GetBoneTransform(bone);
            if (armBoneCache.TryGetValue(bone, out Transform cached)) return cached;
            string name = bone.ToString();
            bool right = name.StartsWith("Right");
            string part = name.Substring(right ? 5 : 4);
            string suffix = right ? "_R" : "_L";
            string target;
            switch (part)
            {
                case "UpperArm": target = "arm" + suffix; break;
                case "LowerArm": target = "forearm" + suffix; break;
                case "Hand": return right ? modelRightHand : modelLeftHand;
                default:
                    string segment = part.EndsWith("Proximal") ? "1" : part.EndsWith("Intermediate") ? "2" : "3";
                    string finger = part.Replace("Proximal", "").Replace("Intermediate", "").Replace("Distal", "").ToLowerInvariant();
                    target = (finger == "little" ? "pinky" : finger) + segment + suffix;
                    break;
            }
            foreach (Transform transform in modelBones)
                if (transform.name == target) { armBoneCache.Add(bone, transform); return transform; }
            return null;
        }

        private void CreateFirstPersonArms()
        {
            if (firstPersonArmsPrefab != null)
            {
                var model = Instantiate(firstPersonArmsPrefab, transform);
                firstPersonModel = model;
                model.name = "First Person Arms";
                firstPersonArms = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (firstPersonArms == null) { Destroy(model); return; }
                modelBones = firstPersonArms.bones;
                foreach (Transform bone in modelBones)
                {
                    switch (bone.name)
                    {
                        case "arm_R": modelRightUpper = bone; break;
                        case "arm_L": modelLeftUpper = bone; break;
                        case "hand_R": modelRightHand = bone; break;
                        case "hand_L": modelLeftHand = bone; break;
                    }
                }
                firstPersonArms.shadowCastingMode = ShadowCastingMode.Off;
                firstPersonArms.receiveShadows = false;
                return;
            }
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
            firstPersonArms.shadowCastingMode = ShadowCastingMode.Off;
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
            if (firstPersonModel != null) Destroy(firstPersonModel);
            else if (firstPersonArms != null) Destroy(firstPersonArms.gameObject);
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
                firstPersonArms.forceRenderingOff = true;
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
                if (middle == null) middle = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
                var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var pinky = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                if (middle != null && index != null)
                {
                    Vector3 fingers = (middle.position - rightHand.position).normalized;
                    Vector3 palmNormal = pinky != null
                        ? Vector3.Cross(index.position - pinky.position, fingers).normalized
                        : Vector3.ProjectOnPlane(transform.forward, fingers).normalized;
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

