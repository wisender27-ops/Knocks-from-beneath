using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    // C-01: camera and tripod are imported art; only their interaction colliders are created here.
    public sealed class CameraPlacementController : MonoBehaviour
    {
        private static readonly Vector3[] Positions = { new Vector3(-45.0f, 0.78f, 113.0f) };
        private static readonly float[] Yaw = { 70f };
        // The tripod mesh is a triangle of legs, so its bounds centre is off the camera mount axis.
        // Measured in the un-yawed frame: the mount axis relative to the bounds centre.
        private static readonly Vector3 HeadOffset = new Vector3(0.13f, 0f, 0.01f);
        private readonly GameObject[] tripods = new GameObject[1];
        private readonly Vector3[] tripodTargets = new Vector3[1];
        private readonly RaycastHit[] placementHits = new RaycastHit[16];
        private PlacementZone[] zones;
        private GameObject cameraPrefab, tripodPrefab, cameraObject;
        private Transform recordingView;
        private Action onPlaced, onRemoved;

        public static CameraPlacementController Instance { get; private set; }
        public bool Available => cameraPrefab != null && tripodPrefab != null;
        public bool IsPlaced => cameraObject != null && StoryFlags.CameraPlacementSlot > 0;
        public int SelectedSlot => StoryFlags.CameraPlacementSlot;
        public Transform RecordingView => IsPlaced ? recordingView : null;
        public bool IsCamera(GameObject item) => item != null && item == cameraObject;

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
            if (Instance == this) Instance = null;
        }

        private void OnBoxPlaced(GameObject box)
        {
            if (cameraObject != null || !Available || box == null) return;
            CollectableItem item = box.GetComponent<CollectableItem>();
            if (item == null || item.currentItemType != CollectableItem.ItemType.Box) return;
            Bounds b = ObjectBounds(box);
            CreateCamera(new Vector3(b.center.x, b.max.y + 0.02f, b.center.z));
            CreateTripods();
        }

        // Used by debug jumps after the box delivery, and by old saves without a camera slot.
        public void RevealForDebug()
        {
            if (cameraObject != null || !Available) return;
            Vector3 pos = Positions[0] + new Vector3(0.7f, 0f, 0f);
            CreateCamera(pos);
            CreateTripods();
        }

        public void RestoreFromCheckpoint()
        {
            int slot = StoryFlags.CameraPlacementSlot;
            if (slot <= 0 || !Available) return;
            CreateTripods();
            if (cameraObject == null) CreateCamera(Positions[0]);
            SetCameraOnTripod(0); // old saves may hold slot 2; there is a single tripod now
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
                !IsPlaced && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("camera-place"));
        }

        private void CreateTripods()
        {
            for (int i = 0; i < tripods.Length; i++)
            {
                if (tripods[i] != null) continue;
                GameObject stand = tripods[i] = Instantiate(tripodPrefab);
                stand.name = "Camera Tripod " + (i + 1);
                stand.SetActive(true);
                stand.transform.rotation = Quaternion.Euler(0f, Yaw[i], 0f) * tripodPrefab.transform.rotation;
                SnapBottom(stand, Positions[i]);
                tripodTargets[i] = ObjectBounds(stand).center;
            }
        }

        public void BeforeGrab(GameObject item)
        {
            if (item != cameraObject || StoryFlags.CameraPlacementSlot == 0) return;
            StoryFlags.SetCameraPlacementSlot(0);
            Rigidbody body = item.GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = false; body.useGravity = true; }
            onRemoved?.Invoke();
        }

        public bool TryGetHeldHint(PickupController pickup, out string hint)
        {
            hint = null;
            if (pickup == null || cameraObject == null || pickup.GetHeldObject() != cameraObject) return false;
            hint = FindTarget(pickup) >= 0 ? "E — установить камеру на штатив" : "Поднесите камеру к штативу";
            return true;
        }

        public bool TryPlaceHeld(PickupController pickup)
        {
            if (pickup == null || cameraObject == null || pickup.GetHeldObject() != cameraObject) return false;
            int slot = FindTarget(pickup);
            if (slot < 0 || !pickup.ReleaseForCameraPlacement(cameraObject)) return false;
            SetCameraOnTripod(slot);
            StoryFlags.SetCameraPlacementSlot(slot + 1);
            onPlaced?.Invoke();
            return true;
        }

        private int FindTarget(PickupController pickup)
        {
            Camera view = pickup.playerCamera;
            if (view == null) return -1;
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < tripods.Length; i++)
            {
                if (tripods[i] == null) continue;
                Vector3 target = tripodTargets[i];
                Vector3 delta = target - view.transform.position;
                float distance = delta.sqrMagnitude;
                if (distance > 5.29f || distance >= bestDistance ||
                    Vector3.Dot(view.transform.forward, delta.normalized) < 0.72f) continue;
                if (!HasClearView(pickup, tripods[i].transform, view.transform.position, delta)) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        private bool HasClearView(PickupController pickup, Transform stand, Vector3 origin, Vector3 delta)
        {
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, placementHits, delta.magnitude,
                PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = placementHits[i].transform;
                if (hit == null || hit.IsChildOf(pickup.transform) || hit.IsChildOf(stand) ||
                    (cameraObject != null && hit.IsChildOf(cameraObject.transform))) continue;
                return false;
            }
            // If the bounded buffer filled up, do not place through an unchecked obstacle.
            return count < placementHits.Length;
        }

        private void SetCameraOnTripod(int slot)
        {
            if (cameraObject == null || slot < 0 || slot >= tripods.Length || tripods[slot] == null) return;
            Rigidbody body = cameraObject.GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
            // The licensed camera's lens points along its local -Z; the view transform above points +Z.
            cameraObject.transform.rotation = Quaternion.Euler(0f, Yaw[slot] + 180f, 0f);
            Bounds stand = ObjectBounds(tripods[slot]);
            Vector3 head = stand.center + Quaternion.Euler(0f, Yaw[slot], 0f) * HeadOffset;
            SnapBottom(cameraObject, new Vector3(head.x, stand.max.y, head.z));
            if (body != null) { body.position = cameraObject.transform.position; body.rotation = cameraObject.transform.rotation; }
        }

        private static Bounds ObjectBounds(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
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
