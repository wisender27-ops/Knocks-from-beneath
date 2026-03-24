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
        if (interaction.GetHeldObject() != null)
        {
            _targetScale = Vector3.zero;
            _targetColor = new Color(defaultColor.r, defaultColor.g, defaultColor.b, 0);
            _targetHint = "";
            return;
        }

        Ray ray = interaction.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        bool hittingSomething = false;

        if (Physics.Raycast(ray, out hit, interaction.interactionDistance, interaction.interactableLayer))
        {
            // Подсказки на русском
            if (hit.collider.GetComponent<HammerTrap>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — взять";
            }
            else if (hit.collider.GetComponent<SimpleItem>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — взять";
            }
            else if (hit.collider.GetComponent<TrashPile>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — собрать";
            }
            else if (hit.collider.GetComponent<LightSwitch>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — включить/выключить";
            }
            else if (hit.collider.GetComponent<Door>() != null)
            {
                hittingSomething = true;
                _targetHint = "E — открыть";
            }
            else if (hit.collider.CompareTag("Pickable"))
            {
                hittingSomething = true;
                _targetHint = "E — взять";
            }
            else if (hit.collider.GetComponent<BedSleepInteractable>() != null)
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