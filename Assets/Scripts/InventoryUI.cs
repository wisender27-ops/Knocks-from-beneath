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
        public Image background;         // Фон слота (Slot1, Slot2 спрайты)
        public Image itemIcon;           // Иконка предмета
        public TextMeshProUGUI keyText;  // Цифра (1, 2, 3...)
        public TextMeshProUGUI itemName; // Название предмета

        [HideInInspector] public string assignedItem = ""; // Что сейчас в слоте
    }

    [Header("Слоты")]
    public SlotUI[] slots; // 4 слота

    [Header("Спрайты фона")]
    public Sprite slotNormalSprite;   // Обычный фон
    public Sprite slotActiveSprite;   // Фон когда слот выбран

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

    void Start()
    {
        // Слот 1 всегда виден и пустой
        slots[0].slotRoot.SetActive(true);
        slots[0].keyText.text = "1";
        slots[0].itemIcon.enabled = false;
        slots[0].itemName.text = "";

        // Остальные слоты скрыты
        for (int i = 1; i < slots.Length; i++)
            slots[i].slotRoot.SetActive(false);
    }

    // Добавить предмет в первый свободный слот
    public void AddItem(string itemName)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            // Слот 0 пустой (пустая рука) — пропускаем если ищем свободный
            if (i == 0 && slots[i].assignedItem == "")
            {
                // Занимаем первый слот предметом
                AssignItemToSlot(0, itemName);
                return;
            }
            else if (i > 0 && slots[i].assignedItem == "")
            {
                slots[i].slotRoot.SetActive(true);
                slots[i].keyText.text = (i + 1).ToString();
                AssignItemToSlot(i, itemName);
                return;
            }
        }
    }

    void AssignItemToSlot(int index, string itemName)
    {
        slots[index].assignedItem = itemName;
        slots[index].itemName.text = GetItemDisplayName(itemName);
        slots[index].itemIcon.enabled = true;
        slots[index].itemIcon.sprite = GetItemIcon(itemName);
    }

    // Подсветить активный слот
    public void SetActiveSlot(string itemName)
    {
        for (int i = 0; i < slots.Length; i++)
        {
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
        if (index < 0 || index >= slots.Length) return null;
        if (!slots[index].slotRoot.activeSelf) return null;
        return slots[index].assignedItem;
    }

    public int GetActiveSlotCount()
    {
        int count = 0;
        foreach (var slot in slots)
            if (slot.slotRoot.activeSelf) count++;
        return count;
    }
}