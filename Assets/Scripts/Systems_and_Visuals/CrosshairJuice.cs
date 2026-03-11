using UnityEngine;
using UnityEngine.UI;

public class CrosshairJuice : MonoBehaviour
{
    [Header("Ссылки")]
    public Image cursorImage; // Перетащи сюда Image курсора
    public PlayerInteraction interaction; // Твой новый объединенный скрипт

    [Header("Настройки")]
    public float scaleSpeed = 10f;
    public float defaultScale = 1f;
    public float interactScale = 1.5f;

    [Header("Цвета")]
    public Color defaultColor = Color.white;
    public Color interactColor = Color.yellow; // Или красный/зеленый на твой вкус

    private Vector3 _targetScale;
    private Color _targetColor;

    void Start()
    {
        _targetScale = Vector3.one * defaultScale;
        _targetColor = defaultColor;
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
            return;
        }

        Ray ray = interaction.playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        bool hittingSomething = false;

        // Используем дистанцию прямо из твоего PlayerInteraction
        if (Physics.Raycast(ray, out hit, interaction.interactionDistance, interaction.interactableLayer))
        {
            // Проверяем, что расстояние до объекта меньше или равно допустимому
            // (Хотя Physics.Raycast уже ограничен по дистанции третьим параметром, 
            // явная проверка hit.distance полезна, если ты захочешь менять логику на лету)

            if (hit.collider.GetComponent<HammerTrap>() != null ||
                hit.collider.GetComponent<SimpleItem>() != null ||
                hit.collider.GetComponent<LightSwitch>() != null ||
                hit.collider.GetComponent<Door>() != null ||
                hit.collider.CompareTag("Pickable"))
            {
                hittingSomething = true;
            }
        }

        // Логика анимации
        if (hittingSomething)
        {
            _targetScale = Vector3.one * interactScale;
            _targetColor = interactColor;
        }
        else
        {
            _targetScale = Vector3.one * defaultScale;
            _targetColor = defaultColor;
        }
    }

    void ApplyJuice()
    {
        // Плавное изменение размера и цвета через Lerp
        cursorImage.transform.localScale = Vector3.Lerp(cursorImage.transform.localScale, _targetScale, Time.deltaTime * scaleSpeed);
        cursorImage.color = Color.Lerp(cursorImage.color, _targetColor, Time.deltaTime * scaleSpeed);
    }
}