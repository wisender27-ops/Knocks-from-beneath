using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace KnocksFromBeneath
{

public class PlacementZone : MonoBehaviour
{
    public List<Transform> slots; // Сюда перетащи Slot_1, Slot_2, Slot_3

    [Tooltip("Срабатывает на каждую успешно установленную коробку — например, RoomRevealZone на этом же объекте.")]
    public UnityEvent onBoxPlaced = new UnityEvent();

    [Tooltip("Теги квестов, которым эта зона засчитывает прогресс. Коробки дня 1 — box-delivery/box-collect, декор дня 2 — room-decoration.")]
    public string[] progressQuestTags = { "box-delivery", "box-collect" };

    private bool[] isSlotOccupied;

    void Awake()
    {
        // Инициализируем массив "занятости" по количеству слотов
        isSlotOccupied = new bool[slots != null ? slots.Count : 0];
    }

    // Тот самый метод, который вызывает игрок
    public bool TryPlaceBox(GameObject box)
    {
        if (box == null || slots == null)
            return false;

        // Защита на случай, если Awake() ещё не отработал (в EditMode-тестах AddComponent
        // не гарантирует немедленный вызов Awake — см. RoomRevealZoneTests). В реальной игре
        // это no-op, Awake уже отработал к моменту вызова TryPlaceBox игроком.
        if (isSlotOccupied == null || isSlotOccupied.Length != slots.Count)
            isSlotOccupied = new bool[slots.Count];

        for (int i = 0; i < slots.Count; i++)
        {
            if (!isSlotOccupied[i] && slots[i] != null) // Нашли свободный слот
            {
                FinalizePlacement(box, i);
                return true; // Говорим игроку: "Всё ок, я забрала!"
            }
        }
        Debug.Log("Зона: Мест нет!");
        return false; // Говорим игроку: "Мест нет, бросай на пол"
    }

    void FinalizePlacement(GameObject box, int index)
    {
        if (box == null || slots == null || index < 0 || index >= slots.Count || slots[index] == null)
            return;

        // 1. Убиваем физику
        Rigidbody rb = box.GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 2. Ставим в слот
        box.transform.parent = null; 
        box.transform.position = slots[index].position;
        box.transform.rotation = slots[index].rotation;

        // 3. Слот теперь занят
        isSlotOccupied[index] = true;

        // --- ВОТ ЭТА СТРОЧКА ОТКЛЮЧАЕТ ВЗАИМОДЕЙСТВИЕ ---
        box.tag = "Untagged"; // Меняем тег на стандартный, который не "берется"
        
        // Если на коробке висит скрипт PickableItem, его тоже можно выключить для оптимизации
        if (box.GetComponent<PickableItem>()) {
            box.GetComponent<PickableItem>().enabled = false;
        }

        Debug.Log($"Зона: Коробка зафиксирована в слоте {index + 1}");

        // Сценарий: если активный квест - доставка коробок, считаем прогресс
        if (QuestManager.Instance != null &&
            QuestManager.Instance.currentQuestIndex >= 0 &&
            QuestManager.Instance.currentQuestIndex < QuestManager.Instance.questList.Count)
        {
            var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
            if (activeQuest != null && IsProgressQuest(activeQuest.questTag))
            {
                // Считаем как одно действие по доставке (ставим одну прогресс-единицу)
                QuestManager.Instance.AddProgress(1);
                Debug.Log("[PlacementZone] Прогресс доставки коробки добавлен.");
            }
        }

        onBoxPlaced?.Invoke();
    }

    bool IsProgressQuest(string questTag)
    {
        if (progressQuestTags == null || string.IsNullOrEmpty(questTag)) return false;
        for (int i = 0; i < progressQuestTags.Length; i++)
        {
            if (progressQuestTags[i] == questTag) return true;
        }
        return false;
    }
}
}
