using UnityEngine;
using System.Collections.Generic;

public class PlacementZone : MonoBehaviour
{
    public List<Transform> slots; // Сюда перетащи Slot_1, Slot_2, Slot_3
    private bool[] isSlotOccupied;

    void Awake()
    {
        // Инициализируем массив "занятости" по количеству слотов
        isSlotOccupied = new bool[slots.Count];
    }

    // Тот самый метод, который вызывает игрок
    public bool TryPlaceBox(GameObject box)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (!isSlotOccupied[i]) // Нашли свободный слот
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
    }
}