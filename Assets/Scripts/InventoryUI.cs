using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance;

    [System.Serializable]
    public class SlotUI
    {
        public GameObject slotRoot;      // �������� ������ �����
        public Image background;         // ��� ����� (Slot1, Slot2 �������)
        public Image itemIcon;           // ������ ��������
        public TextMeshProUGUI keyText;  // ����� (1, 2, 3...)
        public TextMeshProUGUI itemName; // �������� ��������

        [HideInInspector] public string assignedItem = ""; // ��� ������ � �����
    }

    [Header("�����")]
    public SlotUI[] slots; // 4 �����

    [Header("������� ����")]
    public Sprite slotNormalSprite;   // ������� ���
    public Sprite slotActiveSprite;   // ��� ����� ���� ������

    [Header("������ ���������")]
    public Sprite crowbarIcon;
    public Sprite flashlightIcon;
    public Sprite hammerIcon;

    [Header("�����")]
    public Color normalColor = new Color(1, 1, 1, 0.5f);
    public Color activeColor = Color.white;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // ���� 1 ������ ����� � ������
        slots[0].slotRoot.SetActive(true);
        slots[0].keyText.text = "1";
        slots[0].itemIcon.enabled = false;
        slots[0].itemName.text = "";

        // ��������� ����� ������
        for (int i = 1; i < slots.Length; i++)
            slots[i].slotRoot.SetActive(false);
    }

    // �������� ������� � ������ ��������� ����
    public void AddItem(string itemName)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            // ���� 0 ������ (������ ����) � ���������� ���� ���� ���������
            if (i == 0 && slots[i].assignedItem == "")
            {
                // �������� ������ ���� ���������
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

    // ���������� �������� ����
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