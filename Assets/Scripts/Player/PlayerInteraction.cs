using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки луча")]
    public Camera playerCamera;
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;
    public PlayerInventory inventory;

    [Header("Физический захват (PickUp)")]
    public Transform holdPoint;
    public float followSpeed = 20f;
    private GameObject _heldObj;
    private Rigidbody _heldObjRb;
    private PickableItem _heldItemScript;
    private int _originalLayer;
    private Quaternion _heldRotationOffset = Quaternion.identity;

    [Header("Настройки броска")]
    public float throwForce = 15f;

    [Header("Эффекты сочности")]
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.15f;
    public float fovKickAmount = 3f;
    public float fovReturnSpeed = 5f;

    private float _defaultFov;
    private bool _isEatingPie = false;

    [Header("Звуки действий")]
    public AudioSource actionAudioSource;

    void Start()
    {
        _defaultFov = playerCamera.fieldOfView;
    }

    void Update()
    {
        if (_isEatingPie) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (_heldObj == null)
            {
                PerformInteraction();
            }
            else
            {
                if (TryStartEatHeldPie())
                    return;

                if (TryInteractWhileHolding())
                    return;

                DropObject();
            }
        }

        if (Input.GetMouseButtonDown(0) && _heldObj != null)
            ThrowObject();
    }

    void FixedUpdate()
    {
        if (_heldObj != null)
            MovePhysicsObject();
    }

    void PerformInteraction()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
        {
            GameObject hitObj = hit.transform.gameObject;

            // 0. КУЧКА МУСОРА
            TrashPile trashPile = hitObj.GetComponent<TrashPile>();
            if (trashPile != null)
            {
                trashPile.Collect();
                return;
            }

            // 1. СЮЖЕТНЫЕ ТРИГГЕРЫ
            HammerTrap storyItem = hitObj.GetComponent<HammerTrap>();
            if (storyItem != null)
            {
                storyItem.TriggerEvent(inventory);
                return;
            }

            // 2. ВЫКЛЮЧАТЕЛИ И ДВЕРИ
            LightSwitch lightSwitch = hitObj.GetComponent<LightSwitch>();
            if (lightSwitch != null)
            {
                lightSwitch.ToggleLight();
                return;
            }

            // 3. ПРЕДМЕТЫ В ИНВЕНТАРЬ
            SimpleItem item = hitObj.GetComponent<SimpleItem>();
            if (item != null)
            {
                PickUpToInventory(item);
                return;
            }

            // 4. ФИЗИЧЕСКИЙ ЗАХВАТ
            if (hitObj.CompareTag("Pickable"))
                GrabPhysicsObject(hitObj);

            // 5. КРОВАТЬ ДЛЯ СНА
            BedSleepInteractable bed = hitObj.GetComponent<BedSleepInteractable>();
            if (bed != null)
            {
                bed.Interact();
                return;
            }

            MicrowaveInteractable microwave = hit.transform.GetComponentInParent<MicrowaveInteractable>();
            if (microwave != null)
            {
                microwave.Interact();
                return;
            }

        }
    }

    bool TryStartEatHeldPie()
    {
        PieQuestItem pie = _heldObj != null ? _heldObj.GetComponent<PieQuestItem>() : null;
        if (pie == null || !pie.isHeated) return false;

        var intro = FindObjectOfType<IntroSequence>();
        if (intro == null || !intro.CanEatPie()) return false;

        StartCoroutine(EatPieRoutine(pie));
        return true;
    }

    bool TryInteractWhileHolding()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
            return false;

        MicrowaveInteractable microwave = hit.transform.GetComponentInParent<MicrowaveInteractable>();
        if (microwave == null)
            return false;

        microwave.Interact();
        return true;
    }

    System.Collections.IEnumerator EatPieRoutine(PieQuestItem pie)
    {
        _isEatingPie = true;

        if (actionAudioSource != null && pie.eatSfx != null)
            actionAudioSource.PlayOneShot(pie.eatSfx);

        float eatDuration = pie.eatDuration > 0f ? pie.eatDuration : 5f;
        yield return new WaitForSeconds(eatDuration);

        GameObject pieObj = _heldObj;
        if (pieObj != null)
            Destroy(pieObj);

        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;

        var intro = FindObjectOfType<IntroSequence>();
        if (intro != null)
            intro.OnPieEaten();

        _isEatingPie = false;
    }

    // --- ЛОГИКА ИНВЕНТАРЯ ---
    void PickUpToInventory(SimpleItem item)
    {
        // Check if we can pick up this item - it should be required by the active quest
        if (!CanPickUpItem(item))
        {
            Debug.Log($"[Inventory] Cannot pick up {item.itemType.ToString()} - required item is not needed for current quest!");
            return;
        }

        if (item.itemType == ItemType.Crowbar) inventory.hasCrowbar = true;
        else if (item.itemType == ItemType.Flashlight) inventory.hasFlashlight = true;
        else if (item.itemType == ItemType.Hammer) inventory.hasHammer = true;

        inventory.ActivateItem(item.itemType.ToString());

        if (InventoryUI.Instance != null)
            InventoryUI.Instance.AddItem(item.itemType.ToString());

        if (QuestManager.Instance.currentQuestIndex < QuestManager.Instance.questList.Count)
        {
            var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];

            bool isCorrectItem =
                (item.itemType == ItemType.Crowbar && activeQuest.questTitle.Contains("лом")) ||
                (item.itemType == ItemType.Hammer && activeQuest.questTitle.Contains("молоток")) ||
                (item.itemType == ItemType.Flashlight && activeQuest.questTitle.Contains("фонарик"));

            if (isCorrectItem)
                QuestManager.Instance.AddProgress(1);
        }

        Destroy(item.gameObject);
    }

    bool CanPickUpItem(SimpleItem item)
    {
        // If no active quest, cannot pick up story items
        if (QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count)
            return false;

        var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];

        // Check if the item matches active quest requirement via tag (reliable) or title (fallback)
        bool itemIsNeeded =
            (item.itemType == ItemType.Crowbar && activeQuest.questTag == "crowbar-find") ||
            (item.itemType == ItemType.Hammer && activeQuest.questTag == "hammer-find") ||
            (item.itemType == ItemType.Flashlight && activeQuest.questTag == "flashlight-find") ||
            (item.itemType == ItemType.Crowbar && activeQuest.questTitle.Contains("лом")) ||
            (item.itemType == ItemType.Hammer && activeQuest.questTitle.Contains("молоток")) ||
            (item.itemType == ItemType.Flashlight && activeQuest.questTitle.Contains("фонарик"));

        return itemIsNeeded;
    }

    // --- ФИЗИКА ---
    void GrabPhysicsObject(GameObject obj)
    {
        _heldObj = obj;
        _heldObjRb = obj.GetComponent<Rigidbody>();
        _heldRotationOffset = Quaternion.Inverse(holdPoint.rotation) * obj.transform.rotation;

        _originalLayer = _heldObj.layer;
        _heldObj.layer = LayerMask.NameToLayer("HeldItem");

        _heldObjRb.interpolation = RigidbodyInterpolation.Interpolate;
        _heldObjRb.useGravity = false;
        _heldObjRb.linearDamping = 15f;
        _heldObjRb.angularDamping = 15f;
        _heldObjRb.constraints = RigidbodyConstraints.FreezeRotation;

        // Останавливаем частицы когда берём объект
        ParticleSystem ps = obj.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Сюжет: достали пирог (через уже существующую механику поднятия предмета в руки)
        if (obj.GetComponent<PieQuestItem>() != null)
        {
            var intro = FindObjectOfType<IntroSequence>();
            if (intro != null && intro.IsPieTakeQuestActive())
                intro.OnPieTaken();
        }
    }

    void TryReleaseObject()
    {
        if (_heldItemScript != null && _heldItemScript.activeZone != null)
        {
            if (_heldItemScript.activeZone.TryPlaceBox(_heldObj))
            {
                ClearHeldObject();
                return;
            }
        }
        DropObject();
    }

    void DropObject()
    {
        if (_heldObj == null) return;

        _heldObj.layer = _originalLayer;
        _heldObj.transform.SetParent(null);

        _heldObjRb.useGravity = true;
        _heldObjRb.isKinematic = false;
        _heldObjRb.linearDamping = 0.05f;
        _heldObjRb.angularDamping = 0.05f;
        _heldObjRb.constraints = RigidbodyConstraints.None;
        _heldObjRb.AddForce(playerCamera.transform.forward * 2f, ForceMode.Impulse);

        ClearHeldObject();
    }

    void ClearHeldObject()
    {
        if (_heldObj != null)
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

        if (distance > 2.2f)
            DropObject();
    }

    void ThrowObject()
    {
        Rigidbody rbToThrow = _heldObjRb;
        GameObject objToThrow = _heldObj;

        objToThrow.layer = _originalLayer;
        rbToThrow.useGravity = true;
        rbToThrow.isKinematic = false;
        rbToThrow.linearDamping = 0.05f;
        rbToThrow.angularDamping = 0.05f;
        rbToThrow.constraints = RigidbodyConstraints.None;
        rbToThrow.interpolation = RigidbodyInterpolation.None;

        ClearHeldObject();

        rbToThrow.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
        rbToThrow.AddTorque(new Vector3(Random.value, Random.value, Random.value) * 5f, ForceMode.Impulse);

        StopAllCoroutines();
        StartCoroutine(ShakeAndKick());
    }

    private System.Collections.IEnumerator ShakeAndKick()
    {
        Vector3 originalPos = playerCamera.transform.localPosition;
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

        while (Mathf.Abs(playerCamera.fieldOfView - _defaultFov) > 0.1f)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, _defaultFov, Time.deltaTime * fovReturnSpeed);
            yield return null;
        }
        playerCamera.fieldOfView = _defaultFov;
    }

    public GameObject GetHeldObject()
    {
        return _heldObj;
    }

    public GameObject ReleaseHeldObject()
    {
        if (_heldObj == null) return null;

        GameObject released = _heldObj;
        Rigidbody releasedRb = _heldObjRb;

        released.layer = _originalLayer;
        released.transform.SetParent(null);

        if (releasedRb != null)
        {
            releasedRb.useGravity = true;
            releasedRb.isKinematic = false;
            releasedRb.linearDamping = 0.05f;
            releasedRb.angularDamping = 0.05f;
            releasedRb.constraints = RigidbodyConstraints.None;
            releasedRb.interpolation = RigidbodyInterpolation.None;
        }

        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldRotationOffset = Quaternion.identity;
        return released;
    }

    public bool TryGrabObjectFromScript(GameObject obj)
    {
        if (obj == null || _heldObj != null) return false;
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return false;

        GrabPhysicsObject(obj);
        return true;
    }

    void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        Gizmos.color = Color.green;
        Gizmos.DrawRay(ray.origin, ray.direction * interactionDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(ray.origin + ray.direction * interactionDistance, 0.05f);
    }
}