using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    // C-01: camera and tripod are imported art; only their interaction colliders are created here.
    // The tripod is a carried item: while it is in the hands every slot shows a ghost, E puts it
    // on the aimed slot. A mounted camera becomes part of the tripod and is carried with it.
    public sealed class CameraPlacementController : MonoBehaviour
    {
        private readonly struct SlotPose
        {
            public readonly Vector3 Position;
            public readonly float Yaw;
            public SlotPose(Vector3 position, float yaw) { Position = position; Yaw = yaw; }
        }

        private static readonly SlotPose[] DefaultSlots =
        {
            new SlotPose(new Vector3(-45.0f, 0.78f, 113.0f), 70f),
            new SlotPose(new Vector3(-40.5f, 0.78f, 115.5f), -130f)
        };
        // The tripod mesh is a triangle of legs, so its bounds centre is off the camera mount axis.
        // Measured in the prefab pose: the mount axis relative to the bounds centre.
        private static readonly Vector3 HeadOffset = new Vector3(0.13f, 0f, 0.01f);
        private const float SlotReach = 3.5f;
        private const float SlotAimDot = 0.6f;
        private const float SlotAimHeight = 0.7f;
        private static readonly Color GhostColor = new Color(0.45f, 0.9f, 1f, 0.3f);
        private static readonly Color GhostTargetColor = new Color(0.45f, 0.9f, 1f, 0.7f);

        private readonly RaycastHit[] placementHits = new RaycastHit[16];
        private SlotPose[] slots = DefaultSlots;
        private GameObject[] ghosts;
        private Material ghostMaterial, ghostTargetMaterial;
        private int ghostTarget = -2;
        private PlacementZone[] zones;
        private GameObject cameraPrefab, tripodPrefab, cameraObject, tripod;
        private Rigidbody tripodBody;
        private Vector3 headLocal, centerLocal;
        private int tripodSlot = -1;
        private bool cameraMounted;
        private PickupController heldBy;
        private Transform recordingView;
        private Action onPlaced, onRemoved;

        public static CameraPlacementController Instance { get; private set; }
        public bool Available => cameraPrefab != null && tripodPrefab != null;
        public bool IsPlaced => cameraObject != null && cameraMounted && tripodSlot >= 0 && StoryFlags.CameraPlacementSlot > 0;
        public int SelectedSlot => StoryFlags.CameraPlacementSlot;
        public Transform RecordingView => IsPlaced ? recordingView : null;
        public bool IsCamera(GameObject item) => item != null && item == cameraObject;
        public bool IsTripod(GameObject item) => item != null && item == tripod;
        public bool IsGear(GameObject item) => IsCamera(item) || IsTripod(item);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => Instance = null;

        public void Initialize(GameObject[] roomZones, Action placed, Action removed)
        {
            Instance = this;
            onPlaced = placed;
            onRemoved = removed;
            cameraPrefab = Resources.Load<GameObject>("Phase4/Camcorder");
            tripodPrefab = Resources.Load<GameObject>("Phase4/Tripod");
            if (!Available)
            {
                Debug.LogError("C-01: camera or tripod prefab is missing from Resources/Phase4.");
                return;
            }
            CollectSlots();
            MeasureTripod();
            zones = new PlacementZone[roomZones != null ? roomZones.Length : 0];
            for (int i = 0; i < zones.Length; i++)
            {
                zones[i] = roomZones[i] != null ? roomZones[i].GetComponent<PlacementZone>() : null;
                if (zones[i] != null) zones[i].BoxPlaced += OnBoxPlaced;
            }
        }

        private void OnDestroy()
        {
            if (zones != null)
                foreach (PlacementZone zone in zones)
                    if (zone != null) zone.BoxPlaced -= OnBoxPlaced;
            if (ghosts != null)
                foreach (GameObject ghost in ghosts)
                    if (ghost != null) Destroy(ghost);
            if (ghostMaterial != null) Destroy(ghostMaterial);
            if (ghostTargetMaterial != null) Destroy(ghostTargetMaterial);
            if (Instance == this) Instance = null;
        }

        private void CollectSlots()
        {
            CameraSlot[] marked = FindObjectsByType<CameraSlot>(FindObjectsInactive.Exclude);
            if (marked.Length == 0) { slots = DefaultSlots; return; }
            Array.Sort(marked, (a, b) => string.CompareOrdinal(a.name, b.name));
            slots = new SlotPose[marked.Length];
            for (int i = 0; i < marked.Length; i++)
                slots[i] = new SlotPose(marked[i].transform.position, marked[i].transform.eulerAngles.y);
        }

        // Mount axis and bounds centre in the tripod's own space, so they stay exact for any pose.
        private void MeasureTripod()
        {
            GameObject probe = Instantiate(tripodPrefab);
            probe.SetActive(true);
            Vector3 center = ObjectBounds(probe).center;
            centerLocal = probe.transform.InverseTransformPoint(center);
            headLocal = probe.transform.InverseTransformPoint(center + HeadOffset);
            Destroy(probe);
        }

        private void Update()
        {
            if (tripod != null && tripodBody != null && !tripodBody.isKinematic &&
                tripodBody.constraints == RigidbodyConstraints.None)
                tripodBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            if (heldBy == null) return;
            if (tripod == null || heldBy.GetHeldObject() != tripod)
            {
                heldBy = null;
                HideGhosts();
                return;
            }
            ShowGhosts(FindSlot(heldBy));
        }

        private void OnBoxPlaced(GameObject box)
        {
            if (cameraObject != null || !Available || box == null) return;
            CollectableItem item = box.GetComponent<CollectableItem>();
            if (item == null || item.currentItemType != CollectableItem.ItemType.Box) return;
            Bounds b = ObjectBounds(box);
            CreateCamera(new Vector3(b.center.x, b.max.y + 0.02f, b.center.z));
            CreateTripod(0);
        }

        // Used by debug jumps after the box delivery, and by old saves without a camera slot.
        public void RevealForDebug()
        {
            if (cameraObject != null || !Available) return;
            CreateCamera(slots[0].Position + new Vector3(0.7f, 0f, 0f));
            CreateTripod(0);
        }

        public void RestoreFromCheckpoint()
        {
            int slot = StoryFlags.CameraPlacementSlot;
            if (slot <= 0 || !Available) return;
            // A slot saved when the scene had more places may be gone now.
            int index = slot - 1 < slots.Length ? slot - 1 : 0;
            if (tripod == null) CreateTripod(index);
            else PlaceTripodAt(index);
            if (cameraObject == null) CreateCamera(slots[index].Position);
            MountCamera();
            StoryFlags.SetCameraPlacementSlot(index + 1);
        }

        private void CreateCamera(Vector3 bottom)
        {
            cameraObject = Instantiate(cameraPrefab);
            cameraObject.name = "Video Camera (C-01)";
            cameraObject.SetActive(true);
            recordingView = new GameObject("Recording View").transform;
            recordingView.SetParent(cameraObject.transform, false);
            recordingView.localPosition = new Vector3(0.007f, 0.066f, -0.077f);
            recordingView.localRotation = Quaternion.Euler(0f, 180f, 0f);
            cameraObject.tag = "Pickable";
            if (cameraObject.GetComponent<PickableItem>() == null)
                cameraObject.AddComponent<PickableItem>().displayName = "видеокамеру";
            Rigidbody body = cameraObject.GetComponent<Rigidbody>();
            if (body == null) body = cameraObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            if (cameraObject.GetComponentInChildren<Collider>() == null) AddBoundsCollider(cameraObject);
            SnapBottom(cameraObject, bottom);
            QuestItemMarker.Ensure(cameraObject).ConfigureCondition(() =>
                !cameraMounted && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("camera-place"));
        }

        private void CreateTripod(int slot)
        {
            tripod = Instantiate(tripodPrefab);
            tripod.name = "Camera Tripod (C-01)";
            tripod.SetActive(true);
            tripod.tag = "Pickable";
            if (tripod.GetComponent<PickableItem>() == null)
                tripod.AddComponent<PickableItem>().displayName = "штатив";
            tripodBody = tripod.GetComponent<Rigidbody>();
            if (tripodBody == null) tripodBody = tripod.AddComponent<Rigidbody>();
            tripodBody.mass = 3f;
            tripodBody.constraints = RigidbodyConstraints.FreezeRotation;
            if (tripod.GetComponentInChildren<Collider>() == null) AddBoundsCollider(tripod);
            PlaceTripodAt(slot);
            QuestItemMarker.Ensure(tripod).ConfigureCondition(() =>
                cameraMounted && tripodSlot < 0 && QuestManager.Instance != null &&
                QuestManager.Instance.IsQuestActive("camera-place"));
        }

        // Stands the tripod (or a ghost of it) so that its mount axis is on the slot point.
        private void PoseAtSlot(GameObject stand, int slot)
        {
            stand.transform.rotation = Quaternion.Euler(0f, slots[slot].Yaw, 0f) * tripodPrefab.transform.rotation;
            Vector3 head = stand.transform.TransformPoint(headLocal);
            Vector3 target = slots[slot].Position;
            stand.transform.position += new Vector3(target.x - head.x, target.y - ObjectBounds(stand).min.y, target.z - head.z);
        }

        private void PlaceTripodAt(int slot)
        {
            if (tripod == null || slot < 0 || slot >= slots.Length) return;
            if (!tripodBody.isKinematic)
            {
                tripodBody.linearVelocity = Vector3.zero;
                tripodBody.angularVelocity = Vector3.zero;
            }
            tripodBody.isKinematic = true;
            tripodBody.useGravity = false;
            tripodBody.interpolation = RigidbodyInterpolation.None;
            PoseAtSlot(tripod, slot);
            tripodBody.position = tripod.transform.position;
            tripodBody.rotation = tripod.transform.rotation;
            tripodSlot = slot;
            Physics.SyncTransforms();
        }

        public void BeforeGrab(GameObject item, PickupController pickup)
        {
            if (item == tripod)
            {
                tripodSlot = -1;
                tripodBody.isKinematic = false;
                tripodBody.useGravity = true;
                heldBy = pickup;
                NotifyRemoved();
            }
        }

        private void NotifyRemoved()
        {
            if (StoryFlags.CameraPlacementSlot == 0) return;
            StoryFlags.SetCameraPlacementSlot(0);
            onRemoved?.Invoke();
        }

        public bool TryGetHeldHint(PickupController pickup, out string hint)
        {
            hint = null;
            if (pickup == null) return false;
            GameObject held = pickup.GetHeldObject();
            if (tripod != null && held == tripod)
            {
                hint = FindSlot(pickup) >= 0 ? "E — поставить штатив здесь" : "Поднесите штатив к отмеченному месту";
                return true;
            }
            if (cameraObject == null || held != cameraObject) return false;
            hint = FindTarget(pickup) ? "E — установить камеру на штатив" : "Поднесите камеру к штативу";
            return true;
        }

        public bool TryPlaceHeld(PickupController pickup)
        {
            if (pickup == null) return false;
            GameObject held = pickup.GetHeldObject();
            if (tripod != null && held == tripod) return TryPlaceTripod(pickup);
            if (cameraObject == null || held != cameraObject || !FindTarget(pickup) ||
                !pickup.ReleaseForCameraPlacement(cameraObject)) return false;
            MountCamera();
            if (tripodSlot >= 0)
            {
                StoryFlags.SetCameraPlacementSlot(tripodSlot + 1);
                onPlaced?.Invoke();
            }
            return true;
        }

        private bool TryPlaceTripod(PickupController pickup)
        {
            int slot = FindSlot(pickup);
            if (slot < 0 || !pickup.ReleaseForCameraPlacement(tripod)) return false;
            PlaceTripodAt(slot);
            HideGhosts();
            heldBy = null;
            if (cameraMounted)
            {
                StoryFlags.SetCameraPlacementSlot(slot + 1);
                onPlaced?.Invoke();
            }
            return true;
        }

        private Vector3 SlotAimPoint(int slot) => slots[slot].Position + Vector3.up * SlotAimHeight;

        private int FindSlot(PickupController pickup)
        {
            Camera view = pickup.playerCamera;
            if (view == null) return -1;
            int best = -1;
            float bestDot = SlotAimDot;
            for (int i = 0; i < slots.Length; i++)
            {
                Vector3 delta = SlotAimPoint(i) - view.transform.position;
                if (delta.sqrMagnitude > SlotReach * SlotReach) continue;
                float dot = Vector3.Dot(view.transform.forward, delta.normalized);
                if (dot < bestDot || !HasClearView(pickup, view.transform.position, delta)) continue;
                best = i;
                bestDot = dot;
            }
            return best;
        }

        private bool FindTarget(PickupController pickup)
        {
            Camera view = pickup.playerCamera;
            if (view == null || tripod == null) return false;
            Vector3 delta = tripod.transform.TransformPoint(centerLocal) - view.transform.position;
            return delta.sqrMagnitude <= 5.29f &&
                Vector3.Dot(view.transform.forward, delta.normalized) >= 0.72f &&
                HasClearView(pickup, view.transform.position, delta);
        }

        private bool HasClearView(PickupController pickup, Vector3 origin, Vector3 delta)
        {
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, placementHits, delta.magnitude,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = placementHits[i].transform;
                if (hit == null || hit.IsChildOf(pickup.transform) ||
                    (tripod != null && hit.IsChildOf(tripod.transform)) ||
                    (cameraObject != null && hit.IsChildOf(cameraObject.transform))) continue;
                return false;
            }
            // If the bounded buffer filled up, do not place through an unchecked obstacle.
            return count < placementHits.Length;
        }

        private void ShowGhosts(int target)
        {
            if (ghosts == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                ghostMaterial = new Material(shader) { color = GhostColor };
                ghostTargetMaterial = new Material(shader) { color = GhostTargetColor };
                ghosts = new GameObject[slots.Length];
                for (int i = 0; i < ghosts.Length; i++) ghosts[i] = CreateGhost(i);
                ghostTarget = -2;
            }
            if (ghostTarget == target) return;
            ghostTarget = target;
            for (int i = 0; i < ghosts.Length; i++)
            {
                ghosts[i].SetActive(true);
                Material material = i == target ? ghostTargetMaterial : ghostMaterial;
                foreach (MeshRenderer renderer in ghosts[i].GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int m = 0; m < materials.Length; m++) materials[m] = material;
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void HideGhosts()
        {
            ghostTarget = -2;
            if (ghosts == null) return;
            foreach (GameObject ghost in ghosts)
                if (ghost != null) ghost.SetActive(false);
        }

        private GameObject CreateGhost(int slot)
        {
            GameObject ghost = Instantiate(tripodPrefab);
            ghost.name = "Tripod slot " + (slot + 1);
            ghost.SetActive(true);
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            foreach (MonoBehaviour behaviour in ghost.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(behaviour);
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            PoseAtSlot(ghost, slot);
            return ghost;
        }

        // Once mounted the camera is part of the tripod for good: without its own rigidbody its
        // colliders join the tripod body, so grabbing either one carries both together.
        private void MountCamera()
        {
            if (cameraObject == null || tripod == null || cameraMounted) return;
            Rigidbody body = cameraObject.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                Destroy(body);
            }
            // The licensed camera's lens points along its local -Z; the view transform above points +Z.
            Vector3 forward = Vector3.ProjectOnPlane(
                tripod.transform.rotation * Quaternion.Inverse(tripodPrefab.transform.rotation) * Vector3.forward, Vector3.up);
            float yaw = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y : 0f;
            cameraObject.transform.rotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            cameraObject.transform.SetParent(null, true);
            Vector3 head = tripod.transform.TransformPoint(headLocal);
            SnapBottom(cameraObject, new Vector3(head.x, ObjectBounds(tripod).max.y, head.z));
            cameraObject.transform.SetParent(tripod.transform, true);
            Physics.SyncTransforms();
            cameraMounted = true;
        }

        // Renderers of a nested rigidbody (the camera standing on the tripod) are not part of the object.
        private static Bounds ObjectBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            Rigidbody own = obj.GetComponent<Rigidbody>();
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Rigidbody nested = renderer.GetComponentInParent<Rigidbody>();
                if (nested != null && nested != own && nested.transform.IsChildOf(obj.transform)) continue;
                if (!initialized) { bounds = renderer.bounds; initialized = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (initialized) return bounds;
            Collider collider = obj.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds : bounds;
        }

        private static void SnapBottom(GameObject obj, Vector3 bottom)
        {
            Bounds b = ObjectBounds(obj);
            obj.transform.position += bottom - new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static void AddBoundsCollider(GameObject obj)
        {
            Bounds world = ObjectBounds(obj);
            var bounds = new Bounds(obj.transform.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(obj.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            BoxCollider box = obj.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
        }
    }
}
