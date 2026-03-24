using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrosshairJuice : MonoBehaviour
{
    [Header("������")]
    public Image cursorImage;
    public PlayerInteraction interaction;
    public TextMeshProUGUI hintText; // �������� ���� InteractHint

    [Header("���������")]
    public float scaleSpeed = 10f;
    public float defaultScale = 1f;
    public float interactScale = 1.5f;

    [Header("�����")]
    public Color defaultColor = Color.white;
    public Color interactColor = Color.yellow;

    private Vector3 _targetScale;
    private Color _targetColor;
    private string _targetHint = "";

    void Start()
    {
        _targetScale = Vector3.one * defaultScale;
        _targetColor = defaultColor;
        if (hintText != null) hintText.text = "";
    }

    void Update()
    {
        CheckUnderCursor();
        ApplyJuice();
    }

    void CheckUnderCursor()
    {
        GameObject heldObj = interaction.GetHeldObject();
        if (heldObj != null)
        {
            _targetScale = Vector3.zero;
            _targetColor = new Color(defaultColor.r, defaultColor.g, defaultColor.b, 0);
            _targetHint = "";

            PieQuestItem heldPie = heldObj.GetComponent<PieQuestItem>();
            if (heldPie == null) return;

            IntroSequence intro = FindObjectOfType<IntroSequence>();
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
            if (!Physics.Raycast(heldRay, out heldHit, interaction.interactionDistance, interaction.interactableLayer))
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

        if (Physics.Raycast(ray, out hit, interaction.interactionDistance, interaction.interactableLayer))
        {
            MicrowaveInteractable microwave = hit.collider.GetComponentInParent<MicrowaveInteractable>();
            if (microwave != null)
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
            else if (!hittingSomething && hit.collider.GetComponent<TrashPile>() != null)
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
            else if (!hittingSomething && hit.collider.CompareTag("Pickable"))
            {
                hittingSomething = true;
                _targetHint = "E — взять";
            }
            else if (!hittingSomething && hit.collider.GetComponent<BedSleepInteractable>() != null)
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
        cursorImage.transform.localScale = Vector3.Lerp(
            cursorImage.transform.localScale, _targetScale, Time.deltaTime * scaleSpeed);
        cursorImage.color = Color.Lerp(
            cursorImage.color, _targetColor, Time.deltaTime * scaleSpeed);

        // ����� ���������
        if (hintText != null)
            hintText.text = _targetHint;
    }
}