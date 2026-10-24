using UnityEngine;

namespace KnocksFromBeneath
{

// Физический захват/перенос/бросок предметов игроком. Выделен из PlayerInteraction
// (T-07): PlayerInteraction остаётся тонким диспетчером рейкаста, вся физика
// удержания предмета в руках — здесь.
public class PickupController : MonoBehaviour
{
    [Header("Ссылки")]
    public Camera playerCamera;
    public Transform holdPoint;

    [Header("Следование за точкой удержания")]
    public float followSpeed = 20f;
    [SerializeField] private float maxHoldDistance = 2.2f;

    [Header("Установка в зоны")]
    [Tooltip("Радиус поиска зоны размещения вокруг предмета в руках, если триггер зоны почему-то не сработал.")]
    [SerializeField] private float placementSearchRadius = 2.5f;

    [Header("Настройки броска")]
    public float throwForce = 15f;
    [SerializeField] private float throwTorqueRandomness = 5f;
    [SerializeField] private float dropForwardImpulse = 2f;

    [Header("Физика удержания/отпускания")]
    [SerializeField] private float heldRigidbodyDamping = 15f;
    [SerializeField] private float releasedRigidbodyDamping = 0.05f;

    [Header("Эффекты сочности при броске")]
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.15f;
    public float fovKickAmount = 3f;
    public float fovReturnSpeed = 5f;
    private const float FovSnapThreshold = 0.1f;

    [Header("Управление переносимым предметом")]
    [SerializeField, Range(.5f, 6f)] private float rotationSensitivity = 3f;
    [SerializeField, Range(.03f, .3f)] private float zoomStep = .12f;
    [SerializeField, Range(.2f, 1.2f)] private float zoomOutRange = .65f;
    private float _zoomOffset, _targetZoomOffset, _carryRadius;
    private Vector3 _carryHalfExtents;
    private bool _carryPoseBlocked;
    private Vector3 _carryBlockNormal;
    private readonly Collider[] _carryOverlaps = new Collider[24];
    private Quaternion _pickupRotationOffset;
    private PlayerInteraction _interaction;
    private PlayerController _playerController;
    private readonly RaycastHit[] _carryObstacles = new RaycastHit[16];

    private GameObject _heldObj;
    private Rigidbody _heldObjRb;
    private PickableItem _heldItemScript;
    private Transform handSocket;
    private bool _heldObjectAttachedToHand;
    private bool _heldObjectUsesTwoHandGrip;
    private Vector3 _primaryGripLocalPosition;
    private Vector3 _secondaryGripLocalPosition;
    private Quaternion _primaryGripLocalRotation, _secondaryGripLocalRotation;
    private Vector3 _carryCenterLocal;
    private float _carryDepth;
    private int _originalLayer;
    private Quaternion _heldRotationOffset = Quaternion.identity;
    private float _defaultFov;
    private Vector3 _shakeCameraLocalPos;
    private bool _isShaking;
    private bool _cameraEffectsInitialized;
    private PlacementZone[] _questPlacementZones;
    private float _nextPlacementRefresh;
    private PieQuestItem _heldPieView;
    private bool _pieOriginalIsKinematic;
    private bool _pieOriginalUseGravity;
    private RigidbodyConstraints _pieOriginalConstraints;
    private RigidbodyInterpolation _pieOriginalInterpolation;
    private float _pieOriginalLinearDamping;
    private float _pieOriginalAngularDamping;
    private Vector3 _pieOriginalWorldScale;
    private Collider[] _pieColliders;
    private bool[] _pieColliderStates;
    private bool _heldKitchenItem;
    private Collider[] _kitchenColliders;
    private bool[] _kitchenColliderStates;
    private Collider[] _carryColliders;
    private bool[] _carryColliderStates;

    void Start()
    {
        _interaction = GetComponent<PlayerInteraction>();
        _playerController = GetComponent<PlayerController>();
        ProtagonistBody body = GetComponentInChildren<ProtagonistBody>();
        handSocket = body != null ? body.RightHand : null;
        if (playerCamera != null)
        {
            _defaultFov = playerCamera.fieldOfView;
            _cameraEffectsInitialized = true;
        }
    }

    public bool CanManipulateHeldObject => _heldObj != null && !_heldObjectAttachedToHand &&
        _heldPieView == null && isActiveAndEnabled && Time.timeScale > 0f && !PauseMenuController.InputBlocked &&
        (_interaction == null || (_interaction.isActiveAndEnabled && !_interaction.IsEatingPie && !_interaction.IsBreakingLock)) &&
        (_playerController == null || !_playerController.isCameraLocked) &&
        !RetroInventoryScreen.IsAnyOpen && !QuestJournalScreen.IsAnyOpen &&
        !NarrativeReadingPanel.IsModal && !FadeTransitionPresentation.IsActive;

    public bool IsRotatingHeldObject => CanManipulateHeldObject &&
        (Input.GetKey(KeyCode.R) || Input.GetMouseButton(1));
    public float HoldDistanceOffset => _targetZoomOffset;
    public bool IsCarryObstructed => _carryPoseBlocked;

    void Update()
    {
        if (!CanManipulateHeldObject) return;
        if (IsRotatingHeldObject)
            RotateHeldObject(new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > .001f) AdjustHoldDistance(scroll);
        if (Input.GetMouseButtonDown(2)) ResetHeldRotation();
        _zoomOffset = Mathf.Lerp(_zoomOffset, _targetZoomOffset, 1f - Mathf.Exp(-14f * Time.deltaTime));
    }

    public void RotateHeldObject(Vector2 mouseDelta)
    {
        if (!CanManipulateHeldObject) return;
        Quaternion delta = Quaternion.AngleAxis(-mouseDelta.x * rotationSensitivity, Vector3.up) *
            Quaternion.AngleAxis(mouseDelta.y * rotationSensitivity, Vector3.right);
        _heldRotationOffset = delta * _heldRotationOffset;
    }

    public void AdjustHoldDistance(float scroll)
    {
        if (!CanManipulateHeldObject) return;
        float baseDepth = .30f + _carryDepth;
        float safeMinimum = Mathf.Min(0f, Mathf.Max(-.18f, _carryRadius + .12f - baseDepth));
        _targetZoomOffset = Mathf.Clamp(_targetZoomOffset - scroll * zoomStep, safeMinimum, zoomOutRange);
    }

    public void ResetHeldRotation()
    {
        if (CanManipulateHeldObject) _heldRotationOffset = _pickupRotationOffset;
    }

    // Квест на расстановку коробок мог запуститься ПОЗЖЕ, чем игрок взял коробку
    // в руки (например, взял её до сбора мусора). Старый код искал зоны и показывал
    // превью ОДИН раз — в момент захвата, и только если квест к этому моменту уже
    // был активен. Не повезло — и больше зоны не появлялись: игрок держал коробку,
    // E не работал, подсказки не было, а квест Later всё ждал коробок.
    //
    // Теперь пока держим квестовый предмет, зоны и превью пересобираются каждый
    // кадр: как только квест стартует, места появятся сами.
    void FixedUpdate()
    {
        if (_heldObj != null && _heldObjRb != null && holdPoint != null)
            MovePhysicsObject();

        RefreshQuestPlacementIfNeeded();
    }

    /// <summary>
    /// Построить зоны размещения для квестового предмета в руках.
    /// Работает и когда квест ещё не начат: превью просто не покажется, но как
    /// только квест станет активным, места появятся (см. RefreshQuestPlacementIfNeeded).
    /// </summary>
    private void TrySetupQuestPlacementZones(CollectableItem collectable)
    {
        string questTag = collectable != null && collectable.currentItemType == CollectableItem.ItemType.Box
            ? "box-delivery"
            : _heldItemScript != null ? _heldItemScript.placementQuestTag : null;
        if (string.IsNullOrEmpty(questTag)) return;

        IntroSequence intro = FindAnyObjectByType<IntroSequence>();
        GameObject[] zoneObjects = intro == null ? null :
            questTag == "box-delivery" ? intro.roomZones : intro.decorationZones;
        if (zoneObjects == null) return;

        GameObject item = _heldObj;
        PlacementZone[] zones = new PlacementZone[zoneObjects.Length];
        bool any = false;
        for (int i = 0; i < zoneObjects.Length; i++)
        {
            if (zoneObjects[i] == null) continue;
            PlacementZone zone = zoneObjects[i].GetComponent<PlacementZone>();
            if (zone == null || !zone.AcceptsQuest(questTag)) continue;
            // Зона подходит только под СВОЙ предмет: у декора дня 2 у каждого
            // предмета своя зона. Без этой проверки превью рисовалось сразу во
            // всех трёх слотах — игрок видел три копии взятой вещи и не понимал,
            // куда её ставить.
            if (item == null || !zone.AcceptsItem(item)) continue;
            zones[i] = zone;
            any = true;
        }

        if (!any) return;

        _questPlacementZones = zones;
        ShowQuestPreviews();
    }

    /// <summary>
    /// Если квест стартовал, пока предмет уже в руках, — достроить зоны сейчас.
    /// Дёшево: работает только когда предмет в руках, а пересборка идёт не чаще
    /// раза в 0.25 с и только когда реально что-то изменилось.
    /// </summary>
    private void RefreshQuestPlacementIfNeeded()
    {
        if (_heldObj == null) return;

        if (_questPlacementZones != null && AreZonesUsable(_questPlacementZones)) return;

        if (Time.unscaledTime < _nextPlacementRefresh) return;
        _nextPlacementRefresh = Time.unscaledTime + 0.25f;

        // Квест не начат — ждём. Показывать превью раньше времени нельзя: зоны
        // ещё выключены, ShowBoxPreview всё равно ничего не нарисует.
        QuestManager quests = QuestManager.Instance;
        if (quests == null) return;

        CollectableItem collectable = _heldObj.GetComponent<CollectableItem>();
        string questTag = collectable != null && collectable.currentItemType == CollectableItem.ItemType.Box
            ? "box-delivery"
            : _heldItemScript != null ? _heldItemScript.placementQuestTag : null;
        if (string.IsNullOrEmpty(questTag) || !quests.IsQuestActive(questTag)) return;

        TrySetupQuestPlacementZones(collectable);
    }

    /// <summary>Зоны на месте и готовы принимать предмет (квест активен, зоны включены).</summary>
    private static bool AreZonesUsable(PlacementZone[] zones)
    {
        if (zones == null) return false;
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null) continue;
            if (zones[i].isActiveAndEnabled) return true;
        }
        return false;
    }

    private void ShowQuestPreviews()
    {
        if (_questPlacementZones == null || _heldObj == null) return;
        for (int i = 0; i < _questPlacementZones.Length; i++)
        {
            if (_questPlacementZones[i] == null) continue;
            _questPlacementZones[i].ShowBoxPreview(_heldObj);
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (_heldObj != null)
            Release();

        ResetCameraShake();
        if (playerCamera != null && _cameraEffectsInitialized)
            playerCamera.fieldOfView = _defaultFov;
    }

    public GameObject GetHeldObject()
    {
        return _heldObj;
    }

    public void SetBaseFieldOfView(float value)
    {
        _defaultFov = value;
        if (playerCamera != null) playerCamera.fieldOfView = value;
    }

    public bool IsHeldObjectAttachedToHand => _heldObjectAttachedToHand && _heldObj != null;
    public bool IsHeldObjectTwoHanded => _heldObjectUsesTwoHandGrip && _heldObj != null;
    public Vector3 PrimaryHandGripTarget => _heldObj != null
        ? _heldObj.transform.TransformPoint(_primaryGripLocalPosition) : Vector3.zero;
    public Vector3 SecondaryHandGripTarget => _heldObj != null
        ? _heldObj.transform.TransformPoint(_secondaryGripLocalPosition)
        : Vector3.zero;
    public Quaternion HeldHandRotationTarget => PrimaryGripRotation;
    public Quaternion PrimaryGripRotation => _heldObj != null
        ? _heldObj.transform.rotation * _primaryGripLocalRotation : Quaternion.identity;
    public Quaternion SecondaryGripRotation => _heldObj != null
        ? _heldObj.transform.rotation * _secondaryGripLocalRotation : Quaternion.identity;

    public bool TryGrab(GameObject obj)
    {
        if (obj == null || _heldObj != null || holdPoint == null) return false;
        if (obj.GetComponent<Rigidbody>() == null) return false;

        GrabPhysicsObject(obj);
        return true;
    }

    void GrabPhysicsObject(GameObject obj)
    {
        _heldObj = obj;
        _carryPoseBlocked = false;
        _zoomOffset = _targetZoomOffset = 0f;
        _heldObjectAttachedToHand = false;
        _heldObjRb = obj.GetComponent<Rigidbody>();
        if (_heldObjRb == null)
        {
            _heldObj = null;
            return;
        }

        _heldItemScript = obj.GetComponent<PickableItem>();
        CollectableItem collectable = obj.GetComponent<CollectableItem>();
        PieQuestItem pie = obj.GetComponent<PieQuestItem>();
        _heldPieView = pie != null && pie.isHeated ? pie : null;
        CacheHandGripPoints(obj, _heldItemScript);

        Vector3 horizontalForward = Vector3.ProjectOnPlane(holdPoint.forward, Vector3.up);
        Quaternion pickupFrame = horizontalForward.sqrMagnitude > .0001f
            ? Quaternion.LookRotation(horizontalForward, Vector3.up) : holdPoint.rotation;
        _heldRotationOffset = Quaternion.Inverse(pickupFrame) * obj.transform.rotation;
        // До выключения коллайдеров: по ним считаются габариты для хвата.
        if (_heldObjectUsesTwoHandGrip && _heldPieView == null)
            AlignTwoHandedItem(obj, _heldItemScript);
        _pickupRotationOffset = _heldRotationOffset;

        _heldKitchenItem = _heldItemScript != null && _heldItemScript.placementQuestTag == "room-decoration";
        if (_heldKitchenItem)
        {
            _heldObjRb.linearVelocity = Vector3.zero;
            _heldObjRb.angularVelocity = Vector3.zero;
            _heldObjRb.isKinematic = true;
            _kitchenColliders = obj.GetComponentsInChildren<Collider>(true);
            _kitchenColliderStates = new bool[_kitchenColliders.Length];
            for (int i = 0; i < _kitchenColliders.Length; i++)
            {
                _kitchenColliderStates[i] = _kitchenColliders[i].enabled;
                _kitchenColliders[i].enabled = false;
            }
        }

        if (_heldPieView != null)
        {
            Transform pieViewAnchor = playerCamera != null ? playerCamera.transform : holdPoint;
            CapturePiePhysicsState();
            _heldObjRb.linearVelocity = Vector3.zero;
            _heldObjRb.angularVelocity = Vector3.zero;
            _heldObjRb.isKinematic = true;
            _heldObjRb.useGravity = false;
            _pieOriginalWorldScale = obj.transform.lossyScale;
            obj.transform.SetParent(pieViewAnchor, false);
            obj.transform.localScale = DivideScale(_pieOriginalWorldScale * _heldPieView.heldScale, pieViewAnchor.lossyScale);
            _heldPieView.AttachToView(pieViewAnchor);
            for (int i = 0; i < _pieColliders.Length; i++)
                if (_pieColliders[i] != null) _pieColliders[i].enabled = _pieColliderStates[i];
            Physics.SyncTransforms();
            CacheHandGripPoints(obj, _heldItemScript);
            for (int i = 0; i < _pieColliders.Length; i++)
                if (_pieColliders[i] != null) _pieColliders[i].enabled = false;
        }
        if (_heldPieView == null)
        {
            _carryColliders = obj.GetComponentsInChildren<Collider>(true);
            _carryColliderStates = new bool[_carryColliders.Length];
            for (int i = 0; i < _carryColliders.Length; i++)
            {
                _carryColliderStates[i] = _carryColliders[i].enabled;
                _carryColliders[i].enabled = false;
            }
        }
        TrySetupQuestPlacementZones(collectable);


        _originalLayer = _heldObj.layer;
        int heldLayer = LayerMask.NameToLayer("HeldItem");
        if (heldLayer >= 0)
            _heldObj.layer = heldLayer;

        _heldObjRb.interpolation = RigidbodyInterpolation.Interpolate;
        _heldObjRb.useGravity = false;
        _heldObjRb.linearDamping = heldRigidbodyDamping;
        _heldObjRb.angularDamping = heldRigidbodyDamping;
        _heldObjRb.constraints = RigidbodyConstraints.FreezeRotation;

        // Останавливаем частицы когда берём объект
        ParticleSystem ps = obj.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        obj.GetComponent<ItemFoley>()?.PlayGrab();

        // Сюжет: достали пирог. Публикуем событие безусловно — IntroSequence
        // сама решит, важно ли ей это сейчас (см. GameEvents.OnPieGrabbed).
        if (obj.GetComponent<PieQuestItem>() != null)
            GameEvents.OnPieGrabbed?.Invoke();
    }

    // Нажатие E, пока что-то держим в руках, и это не еда/микроволновка —
    // пробуем поставить в зону размещения, иначе просто роняем.
    public void HandleInteractPressed()
    {
        if (_heldObj == null) return;
        if (_questPlacementZones != null)
        {
            PlacementZone closest = null;
            float bestDistance = float.MaxValue;
            foreach (PlacementZone candidate in _questPlacementZones)
            {
                if (!CanPlaceInZone(candidate)) continue;
                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance) { closest = candidate; bestDistance = distance; }
            }
            if (closest != null && closest.TryPlaceBox(_heldObj, transform.position)) ClearHeldObject(false);
            else DropObject();
            return;
        }

        PlacementZone zone = _heldItemScript != null ? _heldItemScript.activeZone : null;

        // PickableItem.activeZone ставится по OnTriggerEnter и обнуляется любым OnTriggerExit —
        // достаточно войти в две зоны подряд или пронести предмет мимо края, чтобы ссылка
        // потерялась, и игрок жмёт E внутри зоны впустую. Поэтому в момент нажатия ещё и
        // ищем зону вокруг предмета напрямую: это не зависит от истории триггеров.
        if (zone == null)
            zone = FindPlacementZoneNearHeldObject();

        if (zone != null && zone.TryPlaceBox(_heldObj))
        {
            ClearHeldObject();
            return;
        }
        DropObject();
    }

    public bool CanPlaceInZone(PlacementZone zone)
    {
        if (_heldObj == null || zone == null || !zone.AcceptsItem(_heldObj) ||
            !zone.TryGetFreeSlot(out Transform slot)) return false;
        if (!zone.IsPlayerNearFreeSlot(transform.position, 1.8f) &&
            !zone.IsPlayerNearFreeSlot(_heldObj.transform.position, 1.8f)) return false;

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : _heldObj.transform.position;
        Vector3 aimPoint = zone.PlacementAimPoint(slot);
        if (playerCamera != null)
        {
            Vector3 viewport = playerCamera.WorldToViewportPoint(aimPoint);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return false;
        }
        return HasLineOfSight(origin, aimPoint);
    }

    PlacementZone FindPlacementZoneNearHeldObject()
    {
        if (_heldObj == null) return null;

        Collider[] around = Physics.OverlapSphere(
            _heldObj.transform.position, placementSearchRadius, PhysicsMasks.AllLayers, QueryTriggerInteraction.Collide);

        PlacementZone closest = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < around.Length; i++)
        {
            PlacementZone zone = around[i].GetComponentInParent<PlacementZone>();
            if (zone == null || !zone.isActiveAndEnabled) continue;

            // Раньше бралась просто ближайшая зона в радиусе, без проверки стен между —
            // предмет у стены телепортировался в зону соседней комнаты. Требуем видимость.
            if (!HasLineOfSight(_heldObj.transform.position, zone.transform.position)) continue;

            float distance = (zone.transform.position - _heldObj.transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = zone;
            }
        }
        return closest;
    }

    // Луч между предметом и зоной размещения, игнорируя сам предмет в руках (слой HeldItem)
    // и триггеры (сами зоны — триггеры, иначе они бы всегда "перекрывали" сами себя).
    bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist <= 0.05f) return true;

        int heldItemLayer = LayerMask.NameToLayer("HeldItem");
        int mask = heldItemLayer >= 0 ? ~(1 << heldItemLayer) : ~0;
        // Слой-блокер (DoorNoRaycast) не должен считаться препятствием для предмета:
        // предмет должен перелетать/проноситься через калитку, а не упираться в её луч.
        return !Physics.Raycast(from, delta / dist, dist - 0.05f, PhysicsMasks.WithoutNoRaycast(mask), QueryTriggerInteraction.Ignore);
    }

    private void RestoreHeldRigidbody(Rigidbody rb, bool resetInterpolation)
    {
        if (rb == null) return;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = releasedRigidbodyDamping;
        rb.angularDamping = releasedRigidbodyDamping;
        rb.constraints = RigidbodyConstraints.None;
        if (resetInterpolation)
            rb.interpolation = RigidbodyInterpolation.None;
    }

    private void CapturePiePhysicsState()
    {
        _pieOriginalIsKinematic = _heldObjRb.isKinematic;
        _pieOriginalUseGravity = _heldObjRb.useGravity;
        _pieOriginalConstraints = _heldObjRb.constraints;
        _pieOriginalInterpolation = _heldObjRb.interpolation;
        _pieOriginalLinearDamping = _heldObjRb.linearDamping;
        _pieOriginalAngularDamping = _heldObjRb.angularDamping;
        _pieColliders = _heldObj.GetComponentsInChildren<Collider>(true);
        _pieColliderStates = new bool[_pieColliders.Length];
        for (int i = 0; i < _pieColliders.Length; i++)
        {
            _pieColliderStates[i] = _pieColliders[i].enabled;
            _pieColliders[i].enabled = false;
        }
    }

    private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            divisor.x == 0f ? value.x : value.x / divisor.x,
            divisor.y == 0f ? value.y : value.y / divisor.y,
            divisor.z == 0f ? value.z : value.z / divisor.z);
    }

    private void RestorePieForWorld(GameObject pieObject, Rigidbody rb)
    {
        if (pieObject == null) return;
        if (_heldPieView != null) _heldPieView.DetachFromView();
        pieObject.transform.SetParent(null, true);
        pieObject.transform.localScale = _pieOriginalWorldScale;
        for (int i = 0; _pieColliders != null && i < _pieColliders.Length; i++)
            if (_pieColliders[i] != null) _pieColliders[i].enabled = _pieColliderStates[i];

        if (rb != null)
        {
            rb.isKinematic = _pieOriginalIsKinematic;
            rb.useGravity = _pieOriginalUseGravity;
            rb.constraints = _pieOriginalConstraints;
            rb.interpolation = _pieOriginalInterpolation;
            rb.linearDamping = _pieOriginalLinearDamping;
            rb.angularDamping = _pieOriginalAngularDamping;
            if (!rb.isKinematic && rb.useGravity) rb.WakeUp();
        }

        _heldPieView = null;
        _pieColliders = null;
        _pieColliderStates = null;
    }

    void DropObject()
    {
        if (_heldObj == null) return;

        _heldObj.layer = _originalLayer;
        if (_heldPieView != null)
            RestorePieForWorld(_heldObj, _heldObjRb);
        else
        {
            _heldObj.transform.SetParent(null);
            RestoreHeldRigidbody(_heldObjRb, true);
        }
        if (_heldObjRb != null && playerCamera != null)
            _heldObjRb.AddForce(playerCamera.transform.forward * dropForwardImpulse, ForceMode.Impulse);

        ClearHeldObject();
    }

    void ClearHeldObject(bool restartParticles = true)
    {
        ClearQuestPlacementPreviews();
        for (int i = 0; _kitchenColliders != null && i < _kitchenColliders.Length; i++)
            if (_kitchenColliders[i] != null) _kitchenColliders[i].enabled = _kitchenColliderStates[i];
        for (int i = 0; _carryColliders != null && i < _carryColliders.Length; i++)
            if (_carryColliders[i] != null) _carryColliders[i].enabled = _carryColliderStates[i];
        _carryColliders = null;
        _carryColliderStates = null;
        _kitchenColliders = null;
        _kitchenColliderStates = null;
        _heldKitchenItem = false;
        if (_heldPieView != null)
            RestorePieForWorld(_heldObj, _heldObjRb);
        if (_heldObj != null && restartParticles)
        {
            // Включаем частицы обратно когда отпускаем объект
            ParticleSystem ps = _heldObj.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        _zoomOffset = _targetZoomOffset = 0f;
        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldObjectAttachedToHand = false;
        _heldPieView = null;
        _heldRotationOffset = Quaternion.identity;
    }

    // Вызывается, когда держимый объект уничтожили извне (например, пирог
    // съеден в PlayerInteraction) — просто забываем о нём, без физики и частиц.
    public void ForceClearHeld()
    {
        ClearQuestPlacementPreviews();
        _zoomOffset = _targetZoomOffset = 0f;
        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldObjectAttachedToHand = false;
        _heldPieView = null;
        _pieColliders = null;
        _pieColliderStates = null;
    }

    void MovePhysicsObject()
    {
        if (_heldPieView != null || _heldObjectAttachedToHand) return;
        Quaternion desiredRotation = holdPoint.rotation * _heldRotationOffset;
        Quaternion rotationStep = Quaternion.Slerp(_heldObjRb.rotation, desiredRotation, Time.fixedDeltaTime * followSpeed);
        // Раньше при препятствии на пути предмет замораживался в «последней безопасной»
        // мировой позиции: коллайдеры у него выключены, игрок уходил, а коробка висела
        // в воздухе/в стене, и руки тянулись к ней по IK. Теперь цель всегда считается
        // от камеры (GetHeldRootTargetPosition сам подтягивает её ближе к игроку).
        Vector3 targetPos = GetHeldRootTargetPosition(desiredRotation);
        Vector3 scaledCenter = Vector3.Scale(_carryCenterLocal, _heldObj.transform.lossyScale);
        Vector3 currentCenter = _heldObjRb.position + rotationStep * scaledCenter;
        float clearDepth = playerCamera != null ? Vector3.Dot(currentCenter - playerCamera.transform.position, playerCamera.transform.forward) : float.PositiveInfinity;
        // Move the item away before turning a long side toward the camera.
        if (clearDepth < CarryDepth(rotationStep) + .12f) rotationStep = _heldObjRb.rotation;
        Vector3 desiredCenter = targetPos + desiredRotation * scaledCenter;
        targetPos = desiredCenter - rotationStep * scaledCenter;
        if (!CarryPoseClear(desiredCenter, rotationStep)) rotationStep = _heldObjRb.rotation;
        if (_heldKitchenItem)
        {
            float t = Mathf.Clamp01(Time.fixedDeltaTime * followSpeed);
            _heldObjRb.MovePosition(Vector3.Lerp(_heldObjRb.position, targetPos, t));
            _heldObjRb.MoveRotation(rotationStep);
            return;
        }
        Vector3 direction = targetPos - _heldObjRb.position;
        _heldObjRb.linearVelocity = direction * followSpeed;
        _heldObjRb.MoveRotation(rotationStep);
        if (direction.magnitude > maxHoldDistance) DropObject();
    }

    private float CarryDepth(Quaternion rotation)
    {
        if (playerCamera == null) return _carryDepth;
        Quaternion viewRotation = Quaternion.Inverse(playerCamera.transform.rotation) * rotation;
        return Mathf.Abs((viewRotation * Vector3.right).z) * _carryHalfExtents.x +
            Mathf.Abs((viewRotation * Vector3.up).z) * _carryHalfExtents.y +
            Mathf.Abs((viewRotation * Vector3.forward).z) * _carryHalfExtents.z;
    }

    private bool CarryPoseClear(Vector3 center, Quaternion rotation)
    {
        Vector3 extents = new Vector3(Mathf.Max(.01f, _carryHalfExtents.x - .015f),
            Mathf.Max(.01f, _carryHalfExtents.y - .015f), Mathf.Max(.01f, _carryHalfExtents.z - .015f));
        int count = Physics.OverlapBoxNonAlloc(center, extents, _carryOverlaps, rotation,
            PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
        if (count == _carryOverlaps.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Transform hit = _carryOverlaps[i].transform;
            if (hit.IsChildOf(_heldObj.transform) || hit.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }

    private Vector3 GetHeldRootTargetPosition(Quaternion targetRotation)
    {
        _carryPoseBlocked = false;
        _carryBlockNormal = Vector3.zero;
        if (playerCamera == null)
            return holdPoint.position + holdPoint.forward * _zoomOffset;
        if (!_heldObjectUsesTwoHandGrip)
        {
            Vector3 smallCenter = playerCamera.transform.TransformPoint(new Vector3(.18f, -.19f, .32f + _carryDepth + _zoomOffset));
            return smallCenter - targetRotation * Vector3.Scale(_carryCenterLocal, _heldObj.transform.lossyScale);
        }
        Vector3 center = TwoHandedCarryCenter(targetRotation);
        if (!CarryPoseClear(center, targetRotation))
        {
            // Место перед камерой занято (стена, стол) — подтягиваем предмет к игроку
            // по линии взгляда до первого препятствия, а не оставляем его в мире.
            _carryPoseBlocked = true;
            Vector3 origin = playerCamera.transform.position;
            Vector3 path = center - origin;
            float length = path.magnitude;
            if (length > .0001f)
            {
                Vector3 extents = Vector3.Max(_carryHalfExtents - Vector3.one * .015f, Vector3.one * .01f);
                int count = Physics.BoxCastNonAlloc(origin, extents, path / length, _carryObstacles,
                    targetRotation, length, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
                float nearest = length;
                for (int i = 0; i < count; i++)
                {
                    // distance == 0 — коробка уже пересекается с этим коллайдером у самой камеры
                    // (игрок вплотную к стене); ближе подтянуть всё равно некуда.
                    Transform hit = _carryObstacles[i].collider.transform;
                    if (hit.IsChildOf(_heldObj.transform) || hit.IsChildOf(transform) || _carryObstacles[i].distance <= 0f) continue;
                    if (_carryObstacles[i].distance >= nearest) continue;
                    nearest = _carryObstacles[i].distance;
                    _carryBlockNormal = _carryObstacles[i].normal;
                }
                center = origin + path / length * nearest;
            }
        }
        return center - targetRotation * Vector3.Scale(_carryCenterLocal, _heldObj.transform.lossyScale);
    }

    private Vector3 TwoHandedCarryCenter(Quaternion rotation)
    {
        float depth = Mathf.Max(.30f + _carryDepth + _zoomOffset, CarryDepth(rotation) + .18f);
        return playerCamera.transform.TransformPoint(new Vector3(0f, -.16f, depth));
    }

    // Крупный предмет в руках упирается в препятствия и останавливает игрока.
    // Раньше коробку подтягивало к камере, а игрок продолжал идти и оказывался
    // внутри неё. Пол и столешницы (почти горизонтальные поверхности) не мешают,
    // иначе нельзя было бы ходить, глядя вниз. Вызывается из PlayerController.
    public Vector3 ClampCarryMovement(Vector3 step)
    {
        if (_heldObj == null || _heldObjRb == null || playerCamera == null || holdPoint == null ||
            _heldPieView != null || !_heldObjectUsesTwoHandGrip) return step;
        Vector3 flat = new Vector3(step.x, 0f, step.z);
        float distance = flat.magnitude;
        if (distance < .00001f) return step;

        if (_carryPoseBlocked)
        {
            // Коробку уже подтянуло к игроку: не даём шагать в сторону препятствия.
            flat = SlideAlong(flat, _carryBlockNormal);
        }
        else
        {
            Quaternion rotation = holdPoint.rotation * _heldRotationOffset;
            Vector3 direction = flat / distance;
            Vector3 extents = Vector3.Max(_carryHalfExtents - Vector3.one * .015f, Vector3.one * .01f);
            int count = Physics.BoxCastNonAlloc(TwoHandedCarryCenter(rotation), extents, direction, _carryObstacles,
                rotation, distance + CarryMoveSkin, PhysicsMasks.RaycastDefault, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _carryObstacles[i];
                if (hit.collider.transform.IsChildOf(_heldObj.transform) || hit.collider.transform.IsChildOf(transform) ||
                    hit.distance <= 0f || Mathf.Abs(hit.normal.y) > CarryFloorNormalY || hit.distance >= nearest) continue;
                nearest = hit.distance;
                normal = hit.normal;
            }
            if (!float.IsPositiveInfinity(nearest))
            {
                float allowed = Mathf.Max(0f, nearest - CarryMoveSkin);
                flat = direction * allowed + SlideAlong(direction * (distance - allowed), normal);
            }
        }
        return new Vector3(flat.x, step.y, flat.z);
    }

    private const float CarryMoveSkin = .02f;
    private const float CarryFloorNormalY = .7f;

    // Убирает из движения составляющую, направленную в препятствие с нормалью normal.
    private static Vector3 SlideAlong(Vector3 move, Vector3 normal)
    {
        if (Mathf.Abs(normal.y) > CarryFloorNormalY) return move;
        Vector3 wall = new Vector3(normal.x, 0f, normal.z);
        if (wall.sqrMagnitude < .0001f) return move;
        wall.Normalize();
        float into = Vector3.Dot(move, wall);
        return into < 0f ? move - wall * into : move;
    }

    private void CacheHandGripPoints(GameObject item, PickableItem pickable)
    {
        _heldObjectUsesTwoHandGrip = false;
        _primaryGripLocalPosition = Vector3.zero;
        _secondaryGripLocalPosition = Vector3.zero;
        _carryCenterLocal = Vector3.zero;
        Transform frame = playerCamera != null ? playerCamera.transform : holdPoint;
        bool viewPose = _heldPieView != null && item.transform.parent == frame;
        Vector3 up = viewPose ? frame.up : Vector3.up;
        Vector3 forward = viewPose ? frame.forward : Vector3.ProjectOnPlane(frame.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < .0001f) forward = transform.forward;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Transform root = item.transform;
        Collider[] colliders = item.GetComponentsInChildren<Collider>();
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        bool found = false;
        foreach (Collider c in colliders)
        {
            if (!c.enabled || c.isTrigger || c.attachedRigidbody != _heldObjRb) continue;
            if (!found) { bounds = c.bounds; found = true; }
            else bounds.Encapsulate(c.bounds);
        }
        if (!found && TryGetItemLocalBounds(item, out Bounds localBounds))
            bounds = new Bounds(root.TransformPoint(localBounds.center), Vector3.Scale(localBounds.size, root.lossyScale));
        _carryCenterLocal = root.InverseTransformPoint(bounds.center);
        _carryDepth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
        float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
        _carryRadius = radius;
        if (TryGetItemLocalBounds(item, out Bounds gripBounds))
        {
            Vector3 scale = root.lossyScale;
            _carryHalfExtents = Vector3.Scale(gripBounds.extents,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
        else _carryHalfExtents = bounds.extents;
        Vector3 rightQuery = bounds.center + right * radius * 2f - forward * radius * .35f;
        Vector3 leftQuery = bounds.center - right * radius * 2f - forward * radius * .35f;
        float width = Vector3.Dot(bounds.size, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)));
        if (width > .65f)
        {
            // Широкий предмет берём за ближнюю поверхность, не растягивая руки до дальних краёв.
            rightQuery = bounds.center + right * .26f - forward * (_carryDepth + radius) + up * .08f;
            leftQuery = bounds.center - right * .26f - forward * (_carryDepth + radius) + up * .08f;
        }
        CacheGrip(root, colliders, pickable != null ? pickable.primaryHandGrip : null,
            rightQuery, -right, up, out _primaryGripLocalPosition, out _primaryGripLocalRotation);
        CacheGrip(root, colliders, pickable != null ? pickable.secondaryHandGrip : null,
            leftQuery, right, up, out _secondaryGripLocalPosition, out _secondaryGripLocalRotation);
        Vector3 size = bounds.size;
        _heldObjectUsesTwoHandGrip = _heldPieView != null ||
            Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > .38f ||
            (_heldObjRb != null && _heldObjRb.mass > 3f);
    }

    // Крупный предмет (коробку) всегда берём одинаково. Раньше он оставался в руках
    // под тем углом, под каким лежал, а точки хвата искались ближайшей точкой
    // коллайдера от камеры — при каждом подборе это были разные рёбра и углы, и руки
    // хватались как попало. Теперь предмет разворачивается к игроку ближайшей гранью
    // (верх остаётся верхом), а ладони ставятся по формуле на боковые грани.
    private void AlignTwoHandedItem(GameObject item, PickableItem pickable)
    {
        if (!TryGetItemLocalBounds(item, out Bounds bounds)) return;
        Vector3 scale = item.transform.lossyScale;
        Quaternion toLocal = Quaternion.Inverse(_heldRotationOffset);
        Vector3 localUp = NearestLocalAxis(toLocal * Vector3.up, Vector3.zero);
        Vector3 localForward = NearestLocalAxis(toLocal * Vector3.forward, localUp);
        Vector3 localRight = Vector3.Cross(localUp, localForward);
        // Широкая сторона всегда поперёк взгляда — с какой стороны ни подойди,
        // коробка в руках выглядит одинаково.
        if (Mathf.Abs(Vector3.Dot(bounds.extents, localForward)) * AxisScale(scale, localForward) >
            Mathf.Abs(Vector3.Dot(bounds.extents, localRight)) * AxisScale(scale, localRight) + .001f)
            localForward = Vector3.Dot(toLocal * Vector3.forward, localRight) >= 0f ? localRight : -localRight;
        localRight = Vector3.Cross(localUp, localForward);
        _heldRotationOffset = Quaternion.Inverse(Quaternion.LookRotation(localForward, localUp));

        float scaleRight = AxisScale(scale, localRight);
        float scaleUp = AxisScale(scale, localUp);
        float scaleForward = AxisScale(scale, localForward);
        float halfWidth = Mathf.Abs(Vector3.Dot(bounds.extents, localRight));
        float halfHeight = Mathf.Abs(Vector3.Dot(bounds.extents, localUp));
        float halfDepth = Mathf.Abs(Vector3.Dot(bounds.extents, localForward));
        _carryDepth = halfDepth * scaleForward;

        // Ладонь в 1.5 см от поверхности, чтобы меш руки не утопал в коробке.
        float gap = .015f / scaleRight;
        // У самого ближнего ребра (центр ладони в 6 см за ним) и ниже середины: дальше
        // по боку кисть целиком закрывает передняя грань коробки.
        float edgeInset = Mathf.Min(halfDepth * .5f, .06f / scaleForward);
        Vector3 along = bounds.center - localUp * halfHeight * .3f - localForward * (halfDepth - edgeInset);
        Vector3 rightPosition = along + localRight * (halfWidth + gap);
        Vector3 leftPosition = along - localRight * (halfWidth + gap);
        Vector3 fingers = (localForward - localUp * .35f).normalized;
        Quaternion rightRotation = Quaternion.LookRotation(-localRight, fingers);
        Quaternion leftRotation = Quaternion.LookRotation(localRight, fingers);
        if (halfWidth * scaleRight * 2f > .75f)
        {
            // Слишком широкий для хвата с боков — подхватываем снизу у ближнего края.
            float spread = Mathf.Min(halfWidth * .6f, .26f / scaleRight);
            Vector3 under = bounds.center - localUp * (halfHeight + .015f / scaleUp) - localForward * halfDepth * .6f;
            rightPosition = under + localRight * spread;
            leftPosition = under - localRight * spread;
            rightRotation = leftRotation = Quaternion.LookRotation((localUp + localForward * .6f).normalized, localForward);
        }
        // Вручную размеченные точки хвата на предмете важнее расчётных.
        if (pickable == null || pickable.primaryHandGrip == null)
        {
            _primaryGripLocalPosition = rightPosition;
            _primaryGripLocalRotation = rightRotation;
        }
        if (pickable == null || pickable.secondaryHandGrip == null)
        {
            _secondaryGripLocalPosition = leftPosition;
            _secondaryGripLocalRotation = leftRotation;
        }
    }

    // Ось ±X/±Y/±Z, ближайшая к direction; ось exclude не рассматривается.
    private static Vector3 NearestLocalAxis(Vector3 direction, Vector3 exclude)
    {
        Vector3 best = Vector3.forward;
        float bestDot = float.NegativeInfinity;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        foreach (Vector3 axis in axes)
        {
            if (Mathf.Abs(Vector3.Dot(axis, exclude)) > .5f) continue;
            float dot = Vector3.Dot(direction, axis);
            if (Mathf.Abs(dot) <= bestDot) continue;
            bestDot = Mathf.Abs(dot);
            best = dot >= 0f ? axis : -axis;
        }
        return best;
    }

    private static float AxisScale(Vector3 scale, Vector3 axis) =>
        Mathf.Max(.0001f, Mathf.Abs(Vector3.Dot(scale, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)))));

    private void CacheGrip(Transform root, Collider[] colliders, Transform authored, Vector3 query,
        Vector3 inward, Vector3 up, out Vector3 position, out Quaternion rotation)
    {
        if (authored != null)
        {
            position = root.InverseTransformPoint(authored.position);
            rotation = Quaternion.Inverse(root.rotation) * authored.rotation;
            return;
        }
        Vector3 contact = root.TransformPoint(_carryCenterLocal);
        float nearest = float.PositiveInfinity;
        foreach (Collider c in colliders)
        {
            if (!c.enabled || c.isTrigger || c.attachedRigidbody != _heldObjRb) continue;
            Vector3 candidate;
            // ClosestPoint поддерживает примитивы и convex MeshCollider; для concave используем Raycast.
            if (c is MeshCollider mesh && !mesh.convex)
            {
                Vector3 direction = c.bounds.center - query;
                if (!c.Raycast(new Ray(query, direction.normalized), out RaycastHit hit, direction.magnitude * 2f)) continue;
                candidate = hit.point;
            }
            else candidate = c.ClosestPoint(query);
            float distance = (candidate - query).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            contact = candidate;
        }
        Vector3 normal = query - contact;
        if (normal.sqrMagnitude > 0.000001f) inward = -normal.normalized;
        position = root.InverseTransformPoint(contact);
        rotation = Quaternion.Inverse(root.rotation) * Quaternion.LookRotation(inward, up);
    }

    private static bool TryGetItemLocalBounds(GameObject item, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;
        Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled) continue;
            // Мировой AABB повёрнутого предмета раздут; где можно, берём собственную форму.
            if (c is BoxCollider box)
                EncapsulateBoundsInLocalSpace(item.transform, c.transform, new Bounds(box.center, box.size), ref bounds, ref hasBounds);
            else if (c is MeshCollider mesh && mesh.sharedMesh != null)
                EncapsulateBoundsInLocalSpace(item.transform, c.transform, mesh.sharedMesh.bounds, ref bounds, ref hasBounds);
            else
                EncapsulateBoundsInLocalSpace(item.transform, null, c.bounds, ref bounds, ref hasBounds);
        }
        if (!hasBounds)
        {
            Renderer[] renderers = item.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    EncapsulateBoundsInLocalSpace(item.transform, renderers[i].transform, renderers[i].localBounds, ref bounds, ref hasBounds);
        }
        return hasBounds;
    }

    // space == null — boxBounds уже в мировых координатах.
    private static void EncapsulateBoundsInLocalSpace(Transform root, Transform space, Bounds boxBounds, ref Bounds localBounds, ref bool hasBounds)
    {
        Vector3 center = boxBounds.center;
        Vector3 extents = boxBounds.extents;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 point = center + Vector3.Scale(extents, new Vector3(x, y, z));
            Vector3 corner = root.InverseTransformPoint(space != null ? space.TransformPoint(point) : point);
            if (!hasBounds) { localBounds = new Bounds(corner, Vector3.zero); hasBounds = true; }
            else localBounds.Encapsulate(corner);
        }
    }

    public void HandleThrowPressed()
    {
        ThrowObject();
    }

    void ThrowObject()
    {
        if (_heldObj == null || _heldObjRb == null) return;

        Rigidbody rbToThrow = _heldObjRb;
        GameObject objToThrow = _heldObj;
        objToThrow.GetComponent<ItemFoley>()?.PlayThrow();

        objToThrow.layer = _originalLayer;
        if (_heldPieView != null)
            RestorePieForWorld(objToThrow, rbToThrow);
        else
        {
            objToThrow.transform.SetParent(null);
            RestoreHeldRigidbody(rbToThrow, true);
        }

        ClearHeldObject();

        rbToThrow.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
        rbToThrow.AddTorque(new Vector3(Random.value, Random.value, Random.value) * throwTorqueRandomness, ForceMode.Impulse);

        StopAllCoroutines();
        ResetCameraShake();
        StartCoroutine(ShakeAndKick());
    }

    private void ResetCameraShake()
    {
        if (!_isShaking) return;
        if (playerCamera != null)
            playerCamera.transform.localPosition = _shakeCameraLocalPos;
        _isShaking = false;
    }

    private System.Collections.IEnumerator ShakeAndKick()
    {
        if (playerCamera == null) yield break;
        // Start выполняется до нормализации высоты в PlayerController.LateUpdate.
        // Снимаем актуальную позу после сброса предыдущего эффекта, а не стартовую.
        _shakeCameraLocalPos = playerCamera.transform.localPosition;
        _isShaking = true;
        Vector3 originalPos = _shakeCameraLocalPos;
        float elapsed = 0.0f;

        playerCamera.fieldOfView = _defaultFov + fovKickAmount;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            playerCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetCameraShake();

        while (Mathf.Abs(playerCamera.fieldOfView - _defaultFov) > FovSnapThreshold)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, _defaultFov, Time.deltaTime * fovReturnSpeed);
            yield return null;
        }
        playerCamera.fieldOfView = _defaultFov;
    }

    public GameObject Release()
    {
        if (_heldObj == null) return null;

        ClearQuestPlacementPreviews();

        GameObject released = _heldObj;
        Rigidbody releasedRb = _heldObjRb;

        released.layer = _originalLayer;
        if (_heldPieView != null)
            RestorePieForWorld(released, releasedRb);
        else
        {
            released.transform.SetParent(null);
            RestoreHeldRigidbody(releasedRb, true);
        }

        ClearHeldObject();
        return released;
    }

    void ClearQuestPlacementPreviews()
    {
        if (_questPlacementZones == null) return;
        foreach (PlacementZone zone in _questPlacementZones)
            if (zone != null) zone.HideBoxPreview();
        _questPlacementZones = null;
    }
}
}
