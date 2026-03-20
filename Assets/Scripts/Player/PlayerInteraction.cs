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

    [Header("Настройки броска")]
    public float throwForce = 15f; // Сила броска

    [Header("Эффекты сочности")]
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.15f;
    public float fovKickAmount = 3f;
    public float fovReturnSpeed = 5f;

    private float _defaultFov;

    void Start()
    {
        _defaultFov = playerCamera.fieldOfView;
    }

    void Update()
    {
        // Нажатие E — взять или просто отпустить (Drop)
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (_heldObj == null) PerformInteraction();
            else DropObject();
        }

        // Нажатие ЛКМ (0) — если в руках что-то есть, кидаем
        if (Input.GetMouseButtonDown(0) && _heldObj != null)
        {
            ThrowObject();
        }
    }

    void FixedUpdate()
    {
        if (_heldObj != null)
        {
            MovePhysicsObject();
        }
    }

    void PerformInteraction()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer))
        {
            GameObject hitObj = hit.transform.gameObject;

            // 0. КУЧКА МУСОРА — ПРИОРИТЕТ №1
            TrashPile trashPile = hitObj.GetComponent<TrashPile>();
            if (trashPile != null)
            {
                trashPile.Collect();
                return;
            }

            // 1. СЮЖЕТНЫЕ ТРИГГЕРЫ (Молоток и т.д.) - ПРИОРИТЕТ №1
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

            // 3. ПРЕДМЕТЫ В ИНВЕНТАРЬ (Лом, Фонарик)
            SimpleItem item = hitObj.GetComponent<SimpleItem>();
            if (item != null)
            {
                PickUpToInventory(item);
                return;
            }

            // 4. ФИЗИЧЕСКИЙ ЗАХВАТ (Ящики, бочки) - ПРИОРИТЕТ ПОСЛЕДНИЙ
            if (hitObj.CompareTag("Pickable"))
            {
                GrabPhysicsObject(hitObj);
            }
        }
    }

    // --- ЛОГИКА ИНВЕНТАРЯ ---
    void PickUpToInventory(SimpleItem item)
    {
        if (item.itemType == ItemType.Crowbar) inventory.hasCrowbar = true;
        else if (item.itemType == ItemType.Flashlight) inventory.hasFlashlight = true;
        else if (item.itemType == ItemType.Hammer) inventory.hasHammer = true;

        inventory.ActivateItem(item.itemType.ToString());

        // ДОБАВЛЯЕМ В UI
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

    // --- ФИЗИКА (БЫВШИЙ PickUpSystem) ---
    void GrabPhysicsObject(GameObject obj)
    {
        _heldObj = obj;
        _heldObjRb = obj.GetComponent<Rigidbody>();

        _originalLayer = _heldObj.layer;
        _heldObj.layer = LayerMask.NameToLayer("HeldItem");

        _heldObjRb.interpolation = RigidbodyInterpolation.Interpolate;
        _heldObjRb.useGravity = false;
        _heldObjRb.linearDamping = 15f;   // Higher damping kills oscillation
        _heldObjRb.angularDamping = 15f;
        _heldObjRb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void TryReleaseObject()
    {
        // Проверка на зону установки (как у тебя с ящиками)
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
        _heldObj.transform.SetParent(null); // На всякий случай, если использовал SetParent

        // --- ВОЗВРАЩАЕМ ФИЗИКУ В НОРМУ ---
        _heldObjRb.useGravity = true;
        _heldObjRb.isKinematic = false;

        // Сбрасываем сопротивление (Damping) к стандартным значениям Unity
        // Обычно это 0 или очень маленькое число (0.05)
        _heldObjRb.linearDamping = 0.05f;
        _heldObjRb.angularDamping = 0.05f;

        // Снимаем заморозку вращения, чтобы объект мог катиться/падать естественно
        _heldObjRb.constraints = RigidbodyConstraints.None;

        // При желании: даем легкий импульс вперед, чтобы объект не падал под ноги
        _heldObjRb.AddForce(playerCamera.transform.forward * 2f, ForceMode.Impulse);

        ClearHeldObject();
    }

    void ClearHeldObject()
    {
        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
    }

    void MovePhysicsObject()
    {
        Vector3 targetPos = holdPoint.position;
        Vector3 currentPos = _heldObj.transform.position;

        // --- POSITION: spring-based velocity ---
        // Instead of moving directly, we push the rigidbody toward the target.
        // This works WITH the physics engine, not against it.
        Vector3 directionToTarget = targetPos - currentPos;
        float distance = directionToTarget.magnitude;

        // Apply velocity proportional to the error (PD controller approach)
        _heldObjRb.linearVelocity = directionToTarget * followSpeed;

        // --- ROTATION: smooth slerp toward holdPoint ---
        Quaternion targetRot = holdPoint.rotation;
        _heldObjRb.MoveRotation(
            Quaternion.Slerp(_heldObj.transform.rotation, targetRot, Time.fixedDeltaTime * followSpeed)
        );

        // Drop if too far (still stuck somewhere)
        if (distance > 2.2f)
            DropObject();
    }

    void ThrowObject()
    {
        // Сохраняем ссылку, так как ClearHeldObject её занулит
        Rigidbody rbToThrow = _heldObjRb;
        GameObject objToThrow = _heldObj;

        // Сначала сбрасываем все настройки (как при обычном Drop)
        objToThrow.layer = _originalLayer;
        rbToThrow.useGravity = true;
        rbToThrow.isKinematic = false;
        rbToThrow.linearDamping = 0.05f;
        rbToThrow.angularDamping = 0.05f;
        rbToThrow.constraints = RigidbodyConstraints.None;
        rbToThrow.interpolation = RigidbodyInterpolation.None;

        // Очищаем переменные в скрипте (руки пусты)
        ClearHeldObject();

        // ПРИКЛАДЫВАЕМ СИЛУ
        // Кидаем вперед по направлению камеры
        rbToThrow.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);

        // Добавим немного случайного вращения для сочности
        rbToThrow.AddTorque(new Vector3(Random.value, Random.value, Random.value) * 5f, ForceMode.Impulse);

        // Запускаем сочные эффекты
        StopAllCoroutines(); // Чтобы эффекты не накладывались друг на друга
        StartCoroutine(ShakeAndKick());
    }

    private System.Collections.IEnumerator ShakeAndKick()
    {
        Vector3 originalPos = playerCamera.transform.localPosition;
        float elapsed = 0.0f;

        // Устанавливаем целевой FOV для рывка
        playerCamera.fieldOfView = _defaultFov + fovKickAmount;

        while (elapsed < shakeDuration)
        {
            // Тряска позиции
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;

            playerCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Возвращаем камеру на место
        playerCamera.transform.localPosition = originalPos;

        // Плавный возврат FOV к стандартному
        while (Mathf.Abs(playerCamera.fieldOfView - _defaultFov) > 0.1f)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, _defaultFov, Time.deltaTime * fovReturnSpeed);
            yield return null;
        }
        playerCamera.fieldOfView = _defaultFov;
    }

    // Позволяет другим скриптам узнать, несем ли мы что-то
    public GameObject GetHeldObject()
    {
        return _heldObj;
    }
}