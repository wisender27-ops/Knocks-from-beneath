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

        PlacementZone[] zones = FindActiveQuestZones(questTag);
        if (zones == null) return false;

        Vector3 playerPosition = transform.position;
        for (int i = 0; i < zones.Length; i++)
        {
            PlacementZone zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled) continue;
            if (!zone.IsPlayerNearFreeSlot(playerPosition, 1.8f) &&
                !zone.IsPlayerNearFreeSlot(heldObj.transform.position, 1.8f))
                continue;

            _targetHint = "Нажмите E, чтобы установить коробку";
            return true;
        }

        // Зоны есть и квест активен, но игрок ещё не у места — подсказываем
        // дорогу, чтобы предмет в руках не выглядел бесполезным.
        _targetHint = "Отнесите коробку к месту";
        return true;
    }

    private static PlacementZone[] FindActiveQuestZones(string questTag)
    {
        IntroSequence intro = UnityEngine.Object.FindAnyObjectByType<IntroSequence>();
        if (intro == null) return null;
        GameObject[] zoneObjects = questTag == "box-delivery" ? intro.roomZones : intro.decorationZones;
        if (zoneObjects == null) return null;

        var result = new System.Collections.Generic.List<PlacementZone>(zoneObjects.Length);
        for (int i = 0; i < zoneObjects.Length; i++)
        {
            if (zoneObjects[i] == null) continue;
            PlacementZone zone = zoneObjects[i].GetComponent<PlacementZone>();
            if (zone != null) result.Add(zone);
        }
        return result.Count > 0 ? result.ToArray() : null;
    }

    void CheckUnderCursor()
    {
        if (interaction == null || interaction.playerCamera == null)
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

        if (Physics.Raycast(ray, out hit, interaction.interactionDistance, PhysicsMasks.WithoutNoRaycast(interaction.interactableLayer), QueryTriggerInteraction.Ignore))
        {
            FloorLogic floor = hit.collider.GetComponentInParent<FloorLogic>();
            if (floor != null && floor.CanStartBreak(interaction.inventory))
            {
                hittingSomething = true;
                _targetHint = "Нажмите E, чтобы сломать пол";
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
            else if (!hittingSomething && hit.collider.GetComponent<Door>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — открыть";
            }
            else if (!hittingSomething && hit.collider.GetComponent<IngredientItem>() != null
                     && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("dinner-ingredients"))
            {
                hittingSomething = true;
                _targetHint = "E — собрать";
            }
            else if (!hittingSomething && hit.collider.GetComponentInParent<StoveInteractable>() is StoveInteractable stove)
            {
                if (_intro == null) _intro = FindFirstObjectByType<IntroSequence>();
                if (stove.IsReady)
                {
                    hittingSomething = true;
                    _targetHint = "E — забрать ужин";
                }
                else if (!stove.IsCooking && _intro != null && _intro.CanCookDinner())
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
                _targetHint = "E — взять";
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
