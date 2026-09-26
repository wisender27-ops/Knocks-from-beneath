using UnityEngine;

namespace KnocksFromBeneath
{

public class PlayerInteraction : MonoBehaviour
{
    [Header("Настройки луча")]
    public Camera playerCamera;
    public float interactionDistance = 3f;
    public LayerMask interactableLayer;
    public PlayerInventory inventory;

    [Header("Физический захват")]
    public PickupController pickup;

    private bool _isEatingPie = false;
    [SerializeField] private float defaultPieEatDuration = 5f;

    [Header("Звуки действий")]
    public AudioSource actionAudioSource;

    void Update()
    {
        if (playerCamera == null)
            return;

        if (_isEatingPie) return;

        // held запрашивается заново для каждой кнопки, а не один раз в начале кадра:
        // если в этом же кадре E успел бросить/поставить предмет (HandleInteractPressed),
        // проверка ЛКМ ниже раньше видела устаревшее "не пусто" и звала
        // HandleThrowPressed() на уже пустых руках -> NRE в PickupController.ThrowObject.
        if (Input.GetKeyDown(KeyCode.E))
        {
            GameObject held = pickup != null ? pickup.GetHeldObject() : null;
            if (held == null)
            {
                PerformInteraction();
            }
            else
            {
                if (TryStartEatHeldPie(held))
                    return;

                if (TryInteractWhileHolding())
                    return;

                if (pickup != null)
                    pickup.HandleInteractPressed();
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            GameObject heldNow = pickup != null ? pickup.GetHeldObject() : null;
            if (heldNow != null)
                pickup.HandleThrowPressed();
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _isEatingPie = false;
    }

    private Ray GetCenterScreenRay()
    {
        return playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    }

    void PerformInteraction()
    {
        Ray ray = GetCenterScreenRay();
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, PhysicsMasks.WithoutNoRaycast(interactableLayer), QueryTriggerInteraction.Ignore))
        {
            GameObject hitObj = hit.transform.gameObject;

            FloorLogic floor = hit.transform.GetComponentInParent<FloorLogic>();
            if (floor != null)
            {
                floor.TryStartBreak(inventory);
                return;
            }

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
            if (hitObj.CompareTag("Pickable") && pickup != null)
            {
                pickup.TryGrab(hitObj);
                return;
            }

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

            // 6. ПРОДУКТЫ НА УЖИН (день 2, T-15)
            IngredientItem ingredient = hitObj.GetComponent<IngredientItem>();
            if (ingredient != null)
            {
                ingredient.Collect();
                return;
            }

            // 7. ПЛИТА (день 2, T-15)
            StoveInteractable stove = hit.transform.GetComponentInParent<StoveInteractable>();
            if (stove != null)
            {
                stove.Interact();
                return;
            }

            // 8. ЗАПИСКА/ЗВОНОК СОСЕДА (день 2, T-16)
            NeighborNoteInteractable note = hitObj.GetComponent<NeighborNoteInteractable>();
            if (note != null)
            {
                note.Interact();
                return;
            }

            // 9. ЗАМОК ВХОДНОЙ ДВЕРИ (день 2, T-17)
            FrontDoorLockInteractable doorLock = hitObj.GetComponent<FrontDoorLockInteractable>();
            if (doorLock != null)
            {
                doorLock.Interact();
                return;
            }

        }
    }

    bool TryStartEatHeldPie(GameObject heldObj)
    {
        PieQuestItem pie = heldObj != null ? heldObj.GetComponent<PieQuestItem>() : null;
        if (pie == null || !pie.isHeated) return false;

        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null || !intro.CanEatPie()) return false;

        StartCoroutine(EatPieRoutine(pie));
        return true;
    }

    bool TryInteractWhileHolding()
    {
        Ray ray = GetCenterScreenRay();
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, interactionDistance, PhysicsMasks.WithoutNoRaycast(interactableLayer), QueryTriggerInteraction.Ignore))
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

        float eatDuration = pie.eatDuration > 0f ? pie.eatDuration : defaultPieEatDuration;
        yield return new WaitForSeconds(eatDuration);

        GameObject pieObj = pie.gameObject;
        if (pieObj != null)
            Destroy(pieObj);

        if (pickup != null)
            pickup.ForceClearHeld();

        GameEvents.OnPieEaten?.Invoke();

        _isEatingPie = false;
    }

    // --- ЛОГИКА ИНВЕНТАРЯ ---
    void PickUpToInventory(SimpleItem item)
    {
        if (item == null || inventory == null)
            return;

        QuestManager questManager = QuestManager.Instance;
        if (questManager == null || !questManager.IsItemRequired(item.itemType))
        {
            Debug.Log($"[Inventory] Cannot pick up {item.itemType.ToString()} - required item is not needed for current quest!");
            return;
        }

        string itemId = item.itemType.ToString();
        bool alreadyOwned = inventory.HasItem(itemId);
        if (!alreadyOwned && !inventory.AddItem(itemId))
            return;

        item.gameObject.SetActive(false);
        Destroy(item.gameObject);

        questManager.AddProgress(1);
        inventory.Equip(itemId);
    }

    public GameObject GetHeldObject()
    {
        return pickup != null ? pickup.GetHeldObject() : null;
    }

    public GameObject ReleaseHeldObject()
    {
        return pickup != null ? pickup.Release() : null;
    }

    public bool TryGrabObjectFromScript(GameObject obj)
    {
        return pickup != null && pickup.TryGrab(obj);
    }

    void OnDrawGizmos()
    {
        if (playerCamera == null) return;

        Ray ray = GetCenterScreenRay();

        Gizmos.color = Color.green;
        Gizmos.DrawRay(ray.origin, ray.direction * interactionDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(ray.origin + ray.direction * interactionDistance, 0.05f);
    }
}
}
