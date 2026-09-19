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

        GameObject held = pickup != null ? pickup.GetHeldObject() : null;

        if (Input.GetKeyDown(KeyCode.E))
        {
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

        if (Input.GetMouseButtonDown(0) && held != null && pickup != null)
            pickup.HandleThrowPressed();
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

        if (Physics.Raycast(ray, out hit, interactionDistance, interactableLayer, QueryTriggerInteraction.Ignore))
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
            if (hitObj.CompareTag("Pickable") && pickup != null)
                pickup.TryGrab(hitObj);

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
        if (!Physics.Raycast(ray, out hit, interactionDistance, interactableLayer, QueryTriggerInteraction.Ignore))
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

        if (QuestManager.Instance != null && QuestManager.Instance.IsItemRequired(item.itemType))
            QuestManager.Instance.AddProgress(1);

        Destroy(item.gameObject);
    }

    bool CanPickUpItem(SimpleItem item)
    {
        return item != null && QuestManager.Instance != null && QuestManager.Instance.IsItemRequired(item.itemType);
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
