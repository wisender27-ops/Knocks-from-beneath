using UnityEngine;

public class QuestCollector : MonoBehaviour
{
    public int questIndexForThisZone; // 0 для мусора, 1 для коробки
    public CollectableItem.ItemType acceptedType; // Какой тип предмета ждем?

    private void OnTriggerEnter(Collider other)
    {
        // 1. Пытаемся взять компонент
        CollectableItem item = other.GetComponent<CollectableItem>();

        // 2. Если скрипта нет — игнорируем
        if (item == null) return;

        // 3. Если тип предмета не совпадает с тем, что ждет зона — игнорируем
        if (item.currentItemType != acceptedType)
        {
            Debug.Log($"Зона ждет {acceptedType}, а принесли {item.currentItemType}");
            return;
        }

        // 4. Проверяем, тот ли сейчас квест активен
        if (QuestManager.Instance.currentQuestIndex == questIndexForThisZone)
        {
            QuestManager.Instance.AddProgress(1);
            Destroy(other.gameObject);
            Debug.Log($"Предмет {item.currentItemType} засчитан!");
        }
    }
}