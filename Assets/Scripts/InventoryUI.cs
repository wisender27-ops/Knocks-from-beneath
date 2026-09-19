using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance;

    [System.Serializable]
    public class SlotUI
    {
        public GameObject slotRoot;      // Корневой объект слота
        public Image background;         // Фон слота (Slot1, Slot2 и т.д.)
        public Image itemIcon;           // Иконка предмета
        public TextMeshProUGUI keyText;  // Номер (1, 2, 3...)
        public TextMeshProUGUI itemName; // Название предмета

        [HideInInspector] public string assignedItem = ""; // Какой предмет в слоте
    }

    [Header("Слоты")]
    public SlotUI[] slots; // 4 слота

    [Header("Спрайты слота")]
    public Sprite slotNormalSprite;   // Обычный вид
    public Sprite slotActiveSprite;   // Вид, когда слот активен

    [Header("Иконки предметов")]
    public Sprite crowbarIcon;
    public Sprite flashlightIcon;
    public Sprite hammerIcon;

    [Header("Цвета")]
    public Color normalColor = new Color(1, 1, 1, 0.5f);
    public Color activeColor = Color.white;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (slots == null || slots.Length == 0 || slots[0] == null)
            return;

        // Слот 1 всегда виден с начала игры
        if (slots[0].slotRoot != null) slots[0].slotRoot.SetActive(true);
        if (slots[0].keyText != null) slots[0].keyText.text = "1";
        if (slots[0].itemIcon != null) slots[0].itemIcon.enabled = false;
        if (slots[0].itemName != null) slots[0].itemName.text = "";

        // Остальные слоты скрыты
        for (int i = 1; i < slots.Length; i++)
            if (slots[i] != null && slots[i].slotRoot != null)
                slots[i].slotRoot.SetActive(false);
    }

    // Добавляет предмет в первый свободный слот
    public void AddItem(string itemName)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            // Слот 0 особый (всегда виден) — просто заполняем его
            if (i == 0 && slots[i].assignedItem == "")
            {
                // Слот уже активен, включать не нужно
                AssignItemToSlot(0, itemName);
                return;
            }
            else if (i > 0 && slots[i].assignedItem == "")
            {
                if (slots[i].slotRoot != null) slots[i].slotRoot.SetActive(true);
                if (slots[i].keyText != null) slots[i].keyText.text = (i + 1).ToString();
                AssignItemToSlot(i, itemName);
                return;
            }
        }
    }

    void AssignItemToSlot(int index, string itemName)
    {
        if (slots == null || index < 0 || index >= slots.Length || slots[index] == null)
            return;

        slots[index].assignedItem = itemName;
        if (slots[index].itemName != null)
            slots[index].itemName.text = GetItemDisplayName(itemName);
        if (slots[index].itemIcon != null)
        {
            slots[index].itemIcon.enabled = true;
            slots[index].itemIcon.sprite = GetItemIcon(itemName);
        }
    }

    // Подсвечивает активный слот
    public void SetActiveSlot(string itemName)
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].background == null) continue;

            bool isActive = slots[i].assignedItem == itemName;
            slots[i].background.color = isActive ? activeColor : normalColor;

            if (slotActiveSprite != null && slotNormalSprite != null)
                slots[i].background.sprite = isActive ? slotActiveSprite : slotNormalSprite;
        }
    }

    Sprite GetItemIcon(string itemName)
    {
        switch (itemName)
        {
            case "Crowbar": return crowbarIcon;
            case "Flashlight": return flashlightIcon;
            case "Hammer": return hammerIcon;
            default: return null;
        }
    }

    string GetItemDisplayName(string itemName)
    {
        switch (itemName)
        {
            case "Crowbar": return "Лом";
            case "Flashlight": return "Фонарик";
            case "Hammer": return "Молоток";
            default: return "";
        }
    }

    public string GetItemInSlot(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length || slots[index] == null) return null;
        if (slots[index].slotRoot == null || !slots[index].slotRoot.activeSelf) return null;
        return slots[index].assignedItem;
    }

    public int GetActiveSlotCount()
    {
        int count = 0;
        if (slots == null) return count;

        foreach (var slot in slots)
            if (slot != null && slot.slotRoot != null && slot.slotRoot.activeSelf) count++;
        return count;
    }
}
