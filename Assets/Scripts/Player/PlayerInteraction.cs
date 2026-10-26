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

    public event System.Action<InteractionFocus> InteractionAttempted;
    public Transform ActionTarget { get; private set; }
    public Vector3 ActionPoint => ActionTarget != null ? ActionTarget.TransformPoint(_actionLocalPoint) : Vector3.zero;
    private Vector3 _actionLocalPoint;
    private FloorLogic _actionFloor;
    private BasementHatchLock _actionLock;
    private BasementHatch _actionHatch;
    private MicrowaveInteractable _actionMicrowave;
    private float _eatStartedAt, _eatDuration;
    private IntroSequence _interactionIntro;
    public bool HasActiveAction => _isEatingPie ||
        (_actionFloor != null && _actionFloor.State == FloorLogic.BreakState.Playing) ||
        (_actionLock != null && _actionLock.IsPlaying) ||
        (_actionHatch != null && (_actionHatch.IsMoving || _actionHatch.IsLockedResponsePlaying)) ||
        (_actionMicrowave != null && _actionMicrowave.IsHeating);
    public float ActionProgress => _isEatingPie ? Mathf.Clamp01((Time.time - _eatStartedAt) / Mathf.Max(.1f, _eatDuration)) :
        _actionFloor != null && _actionFloor.State == FloorLogic.BreakState.Playing ? _actionFloor.InteractionProgress :
        _actionLock != null && _actionLock.IsPlaying ? _actionLock.InteractionProgress :
        _actionHatch != null && _actionHatch.IsMoving ? _actionHatch.InteractionProgress :
        _actionMicrowave != null && _actionMicrowave.IsHeating ? _actionMicrowave.HeatProgress : -1f;

    public bool ShouldUseDoorInput(Door door)
    {
        if (door == null || _isEatingPie || IsBreakingLock || Time.timeScale <= 0f ||
            NarrativeReadingPanel.IsModal || RetroInventoryScreen.IsAnyOpen || QuestJournalScreen.IsAnyOpen) return false;
        var microwave = door.GetComponentInParent<MicrowaveInteractable>();
        if (microwave != null && microwave.IsPieReadyToTake() && door.isOpen) return false;
        GameObject held = GetHeldObject();
        if (held == null) return true;
        if (microwave == null || door != microwave.microwaveDoor || door.isOpen) return false;
        if (_interactionIntro == null) _interactionIntro = FindFirstObjectByType<IntroSequence>();
        return held.GetComponent<PieQuestItem>() != null && _interactionIntro != null && _interactionIntro.CanPlacePieInMicrowave();
    }

    private void CaptureInteraction(RaycastHit hit)
    {
        if (_interactionIntro == null) _interactionIntro = FindFirstObjectByType<IntroSequence>();
        if (!InteractionFocusResolver.TryResolve(this, _interactionIntro, hit, out var focus))
        {
            var microwave = hit.transform.GetComponentInParent<MicrowaveInteractable>();
            var held = GetHeldObject();
            if (microwave == null || held == null || held.GetComponent<PieQuestItem>() == null ||
                _interactionIntro == null || !_interactionIntro.CanPlacePieInMicrowave()) return;
            focus = new InteractionFocus { Target = microwave.transform, Point = hit.point, Prompt = "Поставить пирог" };
        }
        InteractionAttempted?.Invoke(focus);
        if (HasActiveAction && ActionTarget != null) return;
        ActionTarget = focus.Target;
        _actionLocalPoint = ActionTarget.InverseTransformPoint(focus.Point);
        _actionFloor = ActionTarget.GetComponentInParent<FloorLogic>();
        _actionLock = ActionTarget.GetComponentInParent<BasementHatchLock>();
        _actionHatch = ActionTarget.GetComponentInParent<BasementHatch>();
        _actionMicrowave = ActionTarget.GetComponentInParent<MicrowaveInteractable>();
    }

    private bool _isEatingPie = false;
    public bool IsEatingPie => _isEatingPie;
    public bool IsBreakingLock { get; private set; }
    public static bool IsLockSequencePlaying { get; private set; }
    public void SetLockBreaking(bool value)
    {
        IsBreakingLock = value;
        IsLockSequencePlaying = value;
    }
    [SerializeField] private float defaultPieEatDuration = 5f;

    [Header("Звуки действий")]
    public AudioSource actionAudioSource;

    void Update()
    {
        if (PauseMenuController.InputBlocked) return;
        if (playerCamera == null)
            return;

        if (_isEatingPie || IsBreakingLock || RetroInventoryScreen.IsAnyOpen || Time.timeScale == 0f) return;

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

    public bool TryGetInteractionHit(out RaycastHit hit)
    {
        hit = default;
        return playerCamera != null && Physics.Raycast(GetCenterScreenRay(), out hit,
            interactionDistance, PhysicsMasks.WithoutNoRaycast(interactableLayer),
            QueryTriggerInteraction.Ignore);
    }

    void PerformInteraction()
    {
        Ray ray = GetCenterScreenRay();
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, PhysicsMasks.WithoutNoRaycast(interactableLayer), QueryTriggerInteraction.Ignore))
        {
            CaptureInteraction(hit);
            GameObject hitObj = hit.transform.gameObject;

            BasementHatchLock hatchLock = hit.transform.GetComponentInParent<BasementHatchLock>();
            if (hatchLock != null && hatchLock.IsLocked && QuestManager.Instance != null &&
                QuestManager.Instance.IsQuestActive(hatchLock.RequiredQuestTag))
            {
                hatchLock.TryStartBreak(this);
                return;
            }

            BasementHatch hatch = hit.transform.GetComponentInParent<BasementHatch>();
            if (hatch != null)
            {
                IntroSequence intro = hatch.GetComponentInParent<IntroSequence>();
                if (intro == null) intro = FindFirstObjectByType<IntroSequence>();
                if (intro != null && intro.CanBoardBasement(hatch))
                {
                    intro.TryBoardBasement(hatch);
                    return;
                }
                if (hatch.IsLockedResponsePlaying) return;
                if (!hatch.TryOpen() && QuestManager.Instance != null
                    && QuestManager.Instance.IsQuestActive("open-basement"))
                {
                    void CompleteLockedHatchDiscovery()
                    {
                        if (QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("open-basement"))
                            QuestManager.Instance.AddProgress(1);
                    }
                    if (!hatch.TryPlayLockedResponse(CompleteLockedHatchDiscovery))
                        CompleteLockedHatchDiscovery();
                }
                return;
            }

            FloorLogic floor = hit.transform.GetComponentInParent<FloorLogic>();
            if (floor != null)
            {
                floor.TryStartBreak(inventory);
                return;
            }

            // 0. КУЧКА МУСОРА
            TrashPile trashPile = hitObj.GetComponentInParent<TrashPile>();
            if (trashPile != null)
            {
                trashPile.Collect();
                return;
            }

            // 1. СЮЖЕТНЫЕ ТРИГГЕРЫ
            HammerTrap storyItem = hitObj.GetComponentInParent<HammerTrap>();
            if (storyItem != null)
            {
                storyItem.TriggerEvent(inventory);
                return;
            }

            // 2. ВЫКЛЮЧАТЕЛИ И ДВЕРИ
            LightSwitch lightSwitch = hitObj.GetComponentInParent<LightSwitch>();
            if (lightSwitch != null)
            {
                lightSwitch.ToggleLight();
                return;
            }

            // 3. ПРЕДМЕТЫ В ИНВЕНТАРЬ
            SimpleItem item = hitObj.GetComponentInParent<SimpleItem>();
            if (item != null)
            {
                PickUpToInventory(item);
                return;
            }

            // 4. ФИЗИЧЕСКИЙ ЗАХВАТ
            GameObject physical = InteractionFocusResolver.PhysicalItem(hit.collider);
            if (physical != null && pickup != null)
            {
                pickup.TryGrab(physical);
                return;
            }

            // 5. КРОВАТЬ ДЛЯ СНА
            BedSleepInteractable bed = hitObj.GetComponentInParent<BedSleepInteractable>();
            if (bed != null)
            {
                bed.Interact();
                return;
            }

            MicrowaveInteractable microwave = hit.transform.GetComponentInParent<MicrowaveInteractable>();
            if (microwave != null)
            {
                microwave.Interact(hit.transform.GetComponentInParent<Door>() == microwave.microwaveDoor);
                return;
            }

            // 6. ПРОДУКТЫ НА УЖИН (день 2, T-15)
            IngredientItem ingredient = hit.transform.GetComponentInParent<IngredientItem>();
            if (ingredient != null)
            {
                ingredient.Collect();
                return;
            }

            DinnerMealInteractable meal = hit.transform.GetComponentInParent<DinnerMealInteractable>();
            if (meal != null)
            {
                meal.Interact();
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
            NeighborNoteInteractable note = hitObj.GetComponentInParent<NeighborNoteInteractable>();
            if (note != null)
            {
                note.Interact();
                return;
            }

            // 9. УЛИКА — ОСМОТР (M-05)
            InspectableClue clue = hitObj.GetComponentInParent<InspectableClue>();
            if (clue != null)
            {
                clue.Interact();
                return;
            }

            // 10. ЗАМОК ВХОДНОЙ ДВЕРИ (день 2, T-17)
            FrontDoorLockInteractable doorLock = hitObj.GetComponentInParent<FrontDoorLockInteractable>();
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
        GameObject held = GetHeldObject();
        if (held == null || held.GetComponent<PieQuestItem>() == null) return false;
        if (_interactionIntro == null) _interactionIntro = FindFirstObjectByType<IntroSequence>();
        if (_interactionIntro == null || !_interactionIntro.CanPlacePieInMicrowave()) return false;
        Ray ray = GetCenterScreenRay();
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, interactionDistance, PhysicsMasks.WithoutNoRaycast(interactableLayer), QueryTriggerInteraction.Ignore))
            return false;

        MicrowaveInteractable microwave = hit.transform.GetComponentInParent<MicrowaveInteractable>();
        if (microwave == null)
            return false;

        CaptureInteraction(hit);
        microwave.Interact(hit.transform.GetComponentInParent<Door>() == microwave.microwaveDoor);
        return true;
    }

    System.Collections.IEnumerator EatPieRoutine(PieQuestItem pie)
    {
        _isEatingPie = true;
        pie.BeginEatingView();

        if (actionAudioSource != null && pie.eatSfx != null)
            actionAudioSource.PlayOneShot(pie.eatSfx);

        float eatDuration = pie.eatDuration > 0f ? pie.eatDuration : defaultPieEatDuration;
        _eatStartedAt = Time.time;
        _eatDuration = eatDuration;
        ActionTarget = pie.transform;
        _actionLocalPoint = Vector3.zero;
        float bringDuration = Mathf.Min(0.45f, eatDuration * 0.2f);
        yield return AnimatePieEating(pie, 0f, 1f, 0f, 0f, bringDuration);

        int biteCount = pie.EatingBiteCount;
        const float biteMotionDuration = 0.38f;
        const float biteChunkDuration = 0.18f;
        float pauseDuration = Mathf.Max(0f, (eatDuration - bringDuration - biteCount * (biteMotionDuration + biteChunkDuration)) / biteCount);
        for (int i = 0; i < biteCount; i++)
        {
            yield return AnimatePieEating(pie, 0.82f, 1.12f, 0f, 1f, biteMotionDuration * 0.5f);
            yield return pie.ConsumeBite(i, playerCamera, biteChunkDuration);
            yield return AnimatePieEating(pie, 1.12f, 0.82f, 1f, 0f, biteMotionDuration * 0.5f);
            if (pauseDuration > 0f) yield return new WaitForSeconds(pauseDuration);
        }

        GameObject pieObj = pie.gameObject;
        if (pieObj != null)
            Destroy(pieObj);

        if (pickup != null)
            pickup.ForceClearHeld();

        GameEvents.OnPieEaten?.Invoke();

        _isEatingPie = false;
    }

    private static System.Collections.IEnumerator AnimatePieEating(
        PieQuestItem pie, float fromForward, float toForward, float fromDip, float toDip, float duration)
    {
        if (pie == null) yield break;
        if (duration <= 0f)
        {
            pie.SetEatingView(toForward, toDip);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            pie.SetEatingView(Mathf.Lerp(fromForward, toForward, t), Mathf.Lerp(fromDip, toDip, t));
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (pie != null) pie.SetEatingView(toForward, toDip);
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
