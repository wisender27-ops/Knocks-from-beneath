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
    private InteractionPresentation _presentation;
    public InteractionFocus CurrentFocus { get; private set; }
    public string CurrentHint => _targetHint;

    void Awake()
    {
        _presentation = gameObject.AddComponent<InteractionPresentation>();
        _presentation.Initialize(this);
    }

    void OnEnable()
    {
        NarrativeDisplayQueue.InteractionHintAvailabilityChanged += OnHintAvailabilityChanged;
        OnHintAvailabilityChanged(NarrativeDisplayQueue.CanShowInteractionHint);
    }

    void OnDisable()
    {
        NarrativeDisplayQueue.InteractionHintAvailabilityChanged -= OnHintAvailabilityChanged;
        if (hintText != null) hintText.text = "";
    }

    private void OnHintAvailabilityChanged(bool available)
    {
        // Hide immediately, even when a monologue starts after this frame's HUD Update.
        if (!available && hintText != null) hintText.text = "";
    }

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
        CurrentFocus = default;
        _targetHint = "";
        _targetScale = Vector3.one * defaultScale;
        _targetColor = defaultColor;
        if (interaction == null || interaction.playerCamera == null || interaction.IsBreakingLock) return;
        GameObject heldObj = interaction.GetHeldObject();
        if (heldObj != null)
        {
            _targetScale = Vector3.zero;
            _targetColor = new Color(defaultColor.r, defaultColor.g, defaultColor.b, 0);
            _targetHint = "";

            if (CameraPlacementController.Instance != null &&
                CameraPlacementController.Instance.TryGetHeldHint(interaction.pickup, out string cameraHint))
            {
                _targetHint = cameraHint;
                return;
            }

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
            if (microwave.microwaveDoor == null || microwave.pieInsidePoint == null || microwave.IsHeating) return;

            if (!microwave.microwaveDoor.isOpen)
            {
                _targetHint = "E — открыть дверцу";
                return;
            }

            _targetHint = "E — поставить";
            return;
        }


        if (_intro == null) _intro = FindFirstObjectByType<IntroSequence>();
        if (interaction.TryGetInteractionHit(out RaycastHit hit) &&
            InteractionFocusResolver.TryResolve(interaction, _intro, hit, out var focus))
        {
            CurrentFocus = focus;
            _targetHint = focus.Prompt;
            _targetScale = Vector3.one * interactScale;
            _targetColor = focus.Blocked ? defaultColor : interactColor;
        }
    }

    void ApplyJuice()
    {
        if (_presentation != null && _presentation.isActiveAndEnabled)
        {
            if (hintText != null) hintText.text = "";
            return;
        }
        if (hintText != null)
            hintText.text = NarrativeDisplayQueue.CanShowInteractionHint ? _targetHint : "";

        if (cursorImage == null)
            return;

        cursorImage.transform.localScale = Vector3.Lerp(
            cursorImage.transform.localScale, _targetScale, Time.deltaTime * scaleSpeed);
        cursorImage.color = Color.Lerp(
            cursorImage.color, _targetColor, Time.deltaTime * scaleSpeed);

    }
}
}
