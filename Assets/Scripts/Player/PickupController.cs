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

    private GameObject _heldObj;
    private Rigidbody _heldObjRb;
    private PickableItem _heldItemScript;
    private int _originalLayer;
    private Quaternion _heldRotationOffset = Quaternion.identity;
    private float _defaultFov;
    private Vector3 _defaultCamLocalPos;
    private PlacementZone[] _questPlacementZones;
    private float _nextPlacementRefresh;

    void Start()
    {
        if (playerCamera != null)
        {
            _defaultFov = playerCamera.fieldOfView;
            _defaultCamLocalPos = playerCamera.transform.localPosition;
        }
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

        PlacementZone[] zones = new PlacementZone[zoneObjects.Length];
        bool any = false;
        for (int i = 0; i < zoneObjects.Length; i++)
        {
            if (zoneObjects[i] == null) continue;
            PlacementZone zone = zoneObjects[i].GetComponent<PlacementZone>();
            if (zone == null || !zone.AcceptsQuest(questTag)) continue;
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

        // Если StopAllCoroutines() оборвал ShakeAndKick() посреди тряски/кика FOV — тряска
        // сама по себе не успевала откатить позицию камеры и FOV. Возвращаем к исходным
        // значениям явно, а не полагаемся на то, что корутина доиграет до конца.
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = _defaultCamLocalPos;
            playerCamera.fieldOfView = _defaultFov;
        }
    }

    public GameObject GetHeldObject()
    {
        return _heldObj;
    }

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
        _heldObjRb = obj.GetComponent<Rigidbody>();
        if (_heldObjRb == null)
        {
            _heldObj = null;
            return;
        }

        _heldItemScript = obj.GetComponent<PickableItem>();
        CollectableItem collectable = obj.GetComponent<CollectableItem>();

        TrySetupQuestPlacementZones(collectable);

        _heldRotationOffset = Quaternion.Inverse(holdPoint.rotation) * obj.transform.rotation;

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

        // Сюжет: достали пирог. Публикуем событие безусловно — IntroSequence
        // сама решит, важно ли ей это сейчас (см. GameEvents.OnPieGrabbed).
        if (obj.GetComponent<PieQuestItem>() != null)
            GameEvents.OnPieGrabbed?.Invoke();
    }

    // Нажатие E, пока что-то держим в руках, и это не еда/микроволновка —
    // пробуем поставить в зону размещения, иначе просто роняем.
    public void HandleInteractPressed()
    {
        if (_questPlacementZones != null)
        {
            PlacementZone closest = null;
            float bestDistance = float.MaxValue;
            foreach (PlacementZone candidate in _questPlacementZones)
            {
                if (candidate == null ||
                    (!candidate.IsPlayerNearFreeSlot(transform.position, 1.8f) &&
                     !candidate.IsPlayerNearFreeSlot(_heldObj.transform.position, 1.8f))) continue;
                if (!HasLineOfSight(_heldObj.transform.position, candidate.transform.position)) continue;
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

    void DropObject()
    {
        if (_heldObj == null) return;

        _heldObj.layer = _originalLayer;
        _heldObj.transform.SetParent(null);

        RestoreHeldRigidbody(_heldObjRb, true);
        if (_heldObjRb != null && playerCamera != null)
            _heldObjRb.AddForce(playerCamera.transform.forward * dropForwardImpulse, ForceMode.Impulse);

        ClearHeldObject();
    }

    void ClearHeldObject(bool restartParticles = true)
    {
        ClearQuestPlacementPreviews();
        if (_heldObj != null && restartParticles)
        {
            // Включаем частицы обратно когда отпускаем объект
            ParticleSystem ps = _heldObj.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldRotationOffset = Quaternion.identity;
    }

    // Вызывается, когда держимый объект уничтожили извне (например, пирог
    // съеден в PlayerInteraction) — просто забываем о нём, без физики и частиц.
    public void ForceClearHeld()
    {
        ClearQuestPlacementPreviews();
        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
    }

    void MovePhysicsObject()
    {
        Vector3 targetPos = holdPoint.position;
        Vector3 currentPos = _heldObj.transform.position;

        Vector3 directionToTarget = targetPos - currentPos;
        float distance = directionToTarget.magnitude;

        _heldObjRb.linearVelocity = directionToTarget * followSpeed;
        Quaternion targetRotation = holdPoint.rotation * _heldRotationOffset;
        _heldObjRb.MoveRotation(
            Quaternion.Slerp(_heldObj.transform.rotation, targetRotation, Time.fixedDeltaTime * followSpeed)
        );

        if (distance > maxHoldDistance)
            DropObject();
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

        objToThrow.layer = _originalLayer;
        RestoreHeldRigidbody(rbToThrow, true);

        ClearHeldObject();

        rbToThrow.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
        rbToThrow.AddTorque(new Vector3(Random.value, Random.value, Random.value) * throwTorqueRandomness, ForceMode.Impulse);

        StopAllCoroutines();
        StartCoroutine(ShakeAndKick());
    }

    private System.Collections.IEnumerator ShakeAndKick()
    {
        // Берём кэшированную "стояночную" позицию, а не текущую transform.localPosition:
        // если предыдущий ShakeAndKick был оборван на середине (повторный быстрый бросок),
        // текущая позиция уже смещена тряской, и от неё накапливался бы дрейф.
        Vector3 originalPos = _defaultCamLocalPos;
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

        playerCamera.transform.localPosition = originalPos;

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
        released.transform.SetParent(null);

        RestoreHeldRigidbody(releasedRb, true);

        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldRotationOffset = Quaternion.identity;

        ParticleSystem ps = released.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();
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
