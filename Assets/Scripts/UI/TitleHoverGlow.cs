using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnocksFromBeneath
{

// Название в главном меню теплеет при наведении курсора, как в свете лампы.
// Висит на объекте, который получает события указателя (дочерние графики пробрасывают их вверх по иерархии).
public sealed class TitleHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Graphic[] tinted;
    [SerializeField] private Color hoverColor = new Color(1f, .82f, .5f);
    [SerializeField] private float fadeSeconds = .18f;

    private Color[] baseColors;
    private float amount, target;

    void Awake()
    {
        baseColors = new Color[tinted.Length];
        for (int i = 0; i < tinted.Length; i++)
            if (tinted[i] != null) baseColors[i] = tinted[i].color;
    }

    void OnDisable()
    {
        if (baseColors == null) return;
        amount = target = 0f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => FadeTo(1f);

    public void OnPointerExit(PointerEventData eventData) => FadeTo(0f);

    private void FadeTo(float value) => target = value;

    // Компонент не выключаем: отключённый, он не получал бы события указателя.
    void Update()
    {
        if (Mathf.Approximately(amount, target)) return;
        amount = Mathf.MoveTowards(amount, target, Time.unscaledDeltaTime / fadeSeconds);
        Apply();
    }

    private void Apply()
    {
        for (int i = 0; i < tinted.Length; i++)
            if (tinted[i] != null) tinted[i].color = Color.Lerp(baseColors[i], hoverColor, amount);
    }
}
}
