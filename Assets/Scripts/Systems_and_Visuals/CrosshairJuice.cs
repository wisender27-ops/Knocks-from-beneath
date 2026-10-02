using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KnocksFromBeneath
{

public class CrosshairJuice : MonoBehaviour
{
    [Header("Ссылки")]
    public Image cursorImage;
    public PlayerInteraction interaction;
    public TextMeshProUGUI hintText; // Текстовое поле InteractHint

    [Header("Масштаб")]
    public float scaleSpeed = 10f;
    public float defaultScale = 1f;
    public float interactScale = 1.5f;

    [Header("Цвета")]
    public Color defaultColor = Color.white;
    public Color interactColor = Color.yellow;

    private Vector3 _targetScale;
    private Color _targetColor;
    private string _targetHint = "";
    private IntroSequence _intro;

    void Start()
    {
        _targetScale = Vector3.one * defaultScale;
        _targetColor = defaultColor;
        if (hintText != null) hintText.text = "";
        _intro = FindFirstObjectByType<IntroSequence>();
    }

    void Update()
    {
        CheckUnderCursor();
        ApplyJuice();
    }

    /// <summary>
    /// Подсказка "Нажмите E, чтобы установить коробку" для квестовых предметов
    /// на расстановку. Показывается, только когда игрок действительно стоит
    /// рядом со свободным местом и видит его (иначе подсказка вводила бы в
    /// заблуждение — «нажми E» не сработает).
    /// </summary>
    private bool TryGetBoxPlacementHint(GameObject heldObj)
    {
        CollectableItem collectable = heldObj.GetComponent<CollectableItem>();
        bool isQuestItem = collectable != null || heldObj.GetComponent<PickableItem>() != null;
        if (!isQuestItem) return false;

        // Квест на расстановку должен быть активен, иначе ставить некуда.
        QuestManager quests = QuestManager.Instance;
        string questTag = collectable != null && collectable.currentItemType == CollectableItem.ItemType.Box
            ? "box-delivery"
            : heldObj.GetComponent<PickableItem>() != null
                ? heldObj.GetComponent<PickableItem>().placementQuestTag
                : null;
        if (quests == null || string.IsNullOrEmpty(questTag) || !quests.IsQuestActive(questTag))
            return false;

        PickableItem pickable = heldObj.GetComponent<PickableItem>();
        if (_intro == null) _intro = FindFirstObjectByType<IntroSequence>();
        GameObject[] zones = _intro == null ? null :
            questTag == "box-delivery" ? _intro.roomZones : _intro.decorationZones;
        if (zones == null) return false;

        bool matchingTarget = false;
        for (int i = 0; i < zones.Length; i++)
        {
            PlacementZone zone = zones[i] != null ? zones[i].GetComponent<PlacementZone>() : null;
            if (zone == null || !zone.AcceptsQuest(questTag) || !zone.AcceptsItem(heldObj) ||
                !zone.TryGetFreeSlot(out _)) continue;
            matchingTarget = true;
            if (interaction.pickup == null || !interaction.pickup.CanPlaceInZone(zone)) continue;

            _targetHint = questTag == "room-decoration"
                ? $"E — поставить {pickable.displayName} на место"
                : "E — установить коробку";
            return true;
        }

        if (!matchingTarget) return false;
        _targetHint = questTag == "room-decoration" && !string.IsNullOrEmpty(pickable.destinationHint)
            ? pickable.destinationHint
            : "Отнесите коробку к месту";
        return true;
    }

    void CheckUnderCursor()
    {
        if (interaction == null || interaction.playerCamera == null || interaction.IsBreakingLock)
        {
            _targetScale = Vector3.one * defaultScale;
            _targetColor = defaultColor;
            _targetHint = "";
            return;
        }

        GameObject heldObj = interaction.GetHeldObject();
        if (heldObj != null)
        {
            _targetScale = Vector3.zero;
            _targetColor = new Color(defaultColor.r, defaultColor.g, defaultColor.b, 0);
            _targetHint = "";

            // Коробка (и любой предмет квеста на расстановку) — подсказываем E,
            // когда игрок дошёл до зоны с местом. Раньше этой ветки не было
            // вообще: предмет в руках гасил прицел и подсказку, и игрок с
            // коробкой в руках не видел ни слова о том, что куда-то её надо
            // поставить.
            if (TryGetBoxPlacementHint(heldObj)) return;

            PieQuestItem heldPie = heldObj.GetComponent<PieQuestItem>();
            if (heldPie == null) return;

            if (_intro == null) _intro = FindFirstObjectByType<IntroSequence>();
            IntroSequence intro = _intro;
            if (intro == null) return;

            // Подсказка на поедание пирога в любом месте, когда он уже согрет и квест активен
            if (heldPie.isHeated && intro.CanEatPie())
            {
                _targetHint = "E — съесть";
                return;
            }

            // Подсказка на установку пирога в микроволновку только при наведении и открытой дверце
            Ray heldRay = interaction.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit heldHit;
            if (!Physics.Raycast(heldRay, out heldHit, interaction.interactionDistance, PhysicsMasks.WithoutNoRaycast(interaction.interactableLayer), QueryTriggerInteraction.Ignore))
                return;

            MicrowaveInteractable microwave = heldHit.collider.GetComponentInParent<MicrowaveInteractable>();
            if (microwave == null) return;
            if (!intro.CanPlacePieInMicrowave()) return;
            if (microwave.microwaveDoor == null) return;

            if (!microwave.microwaveDoor.isOpen)
            {
                _targetHint = "Открой дверцу микроволновки";
                return;
            }

            _targetHint = "E — поставить";
            return;
        }

        Ray ray = interaction.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        bool hittingSomething = false;

        if (interaction.TryGetInteractionHit(out hit))
        {
            FloorLogic floor = hit.collider.GetComponentInParent<FloorLogic>();
            if (floor != null && floor.CanStartBreak(interaction.inventory))
            {
                hittingSomething = true;
                _targetHint = "Нажмите E, чтобы сломать пол";
            }

            // Люк в подвал. Проверка идёт ДО всех остальных веток и по GetComponentInParent,
            // как в PlayerInteraction.PerformInteraction: коллайдеры створок висят на
            // дочерних MetalHatchPanel, а сам BasementHatch — на корне люка. Без этого
            // ветка ловила только пустоту, и подсказка у люка не появлялась.
            //
            // Текст зависит от состояния: заперто (нужен лом) / не вскрыто / вскрыто
            // (закрыть). Ссылка нужна и в случае, когда смотришь на сам замок, а не на
            // створки, — тогда родителем коллайдера будет BasementHatchLock.
            BasementHatchLock hatchLock = hit.collider.GetComponentInParent<BasementHatchLock>();
            BasementHatch hatch = hatchLock == null
                ? hit.collider.GetComponentInParent<BasementHatch>()
                : hatchLock.GetComponentInParent<BasementHatch>();

            if (!hittingSomething && (hatch != null || hatchLock != null))
            {
                hittingSomething = true;
                if (_intro == null) _intro = FindFirstObjectByType<IntroSequence>();
                if (_intro != null && _intro.CanBoardBasement(hatch))
                    _targetHint = "Нажмите E, чтобы заколотить люк";
                else if (hatch != null && hatch.IsOpen)
                    _targetHint = "Нажмите E, чтобы закрыть дверь";
                else if (hatchLock != null && hatchLock.IsLocked && QuestManager.Instance != null &&
                    QuestManager.Instance.IsQuestActive(hatchLock.RequiredQuestTag))
                    _targetHint = hatchLock.CanStartBreak(interaction)
                        ? "Нажмите E, чтобы сломать замок" : "Подойдите ближе к замку";
                else
                    _targetHint = "Нажмите E, чтобы открыть дверь";
            }

            MicrowaveInteractable microwave = hit.collider.GetComponentInParent<MicrowaveInteractable>();
            if (!hittingSomething && microwave != null)
            {
                // Готовый пирог внутри: подсказываем открыть дверцу или достать.
                if (microwave.IsPieReadyToTake())
                {
                    hittingSomething = true;
                    if (microwave.microwaveDoor != null && microwave.microwaveDoor.isOpen)
                        _targetHint = "E — достать пирог";
                    else
                        _targetHint = "Открой дверцу микроволновки";
                }
            }

            // Подсказки на русском
            if (!hittingSomething && hit.collider.GetComponent<HammerTrap>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — взять";
            }
            else if (!hittingSomething && hit.collider.GetComponent<SimpleItem>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — взять";
            }
            else if (!hittingSomething && hit.collider.GetComponent<TrashPile>() != null
                     && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("trash-collect"))
            {
                hittingSomething = true;
                _targetHint = "E — собрать";
            }
            else if (!hittingSomething && hit.collider.GetComponent<LightSwitch>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — включить/выключить";
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<Door>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — открыть";
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<IngredientItem>() != null
                     && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("dinner-ingredients"))
            {
                hittingSomething = true;
                _targetHint = "E — собрать";
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<DinnerMealInteractable>() is DinnerMealInteractable meal && meal.CanInteract)
            {
                hittingSomething = true;
                _targetHint = "E — съесть ужин";
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<StoveInteractable>() is StoveInteractable stove)
            {
                if (_intro == null) _intro = FindFirstObjectByType<IntroSequence>();
                if (!stove.IsReady && !stove.IsCooking && _intro != null && _intro.CanCookDinner())
                {
                    hittingSomething = true;
                    _targetHint = "E — готовить";
                }
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<NeighborNoteInteractable>() is NeighborNoteInteractable note && note.CanInteract)
            {
                hittingSomething = true;
                _targetHint = "E — посмотреть";
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<FrontDoorLockInteractable>() is FrontDoorLockInteractable frontLock && frontLock.CanInteract)
            {
                hittingSomething = true;
                _targetHint = "E — запереть засов";
            }
            else if (!hittingSomething && hit.collider.CompareTag("Pickable"))
            {
                hittingSomething = true;
                PickableItem pickable = hit.collider.GetComponent<PickableItem>();
                _targetHint = pickable != null && pickable.placementQuestTag == "room-decoration"
                    ? $"E — поднять {pickable.displayName}"
                    : "E — взять";
            }
            else if (!hittingSomething && hit.collider.TryGetComponent(out BedSleepInteractable bed) && bed.CanInteract)
            {
                hittingSomething = true;
                _targetHint = "E — лечь спать";
            }
        }

        if (hittingSomething)
        {
            _targetScale = Vector3.one * interactScale;
            _targetColor = interactColor;
        }
        else
        {
            _targetScale = Vector3.one * defaultScale;
            _targetColor = defaultColor;
            _targetHint = "";
        }
    }

    void ApplyJuice()
    {
        if (cursorImage == null)
            return;

        cursorImage.transform.localScale = Vector3.Lerp(
            cursorImage.transform.localScale, _targetScale, Time.deltaTime * scaleSpeed);
        cursorImage.color = Color.Lerp(
            cursorImage.color, _targetColor, Time.deltaTime * scaleSpeed);

        // Текст подсказки
        if (hintText != null)
            hintText.text = _targetHint;
    }
}
}
