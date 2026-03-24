using UnityEngine;

public class QuestCollector : MonoBehaviour
{
    public CollectableItem.ItemType acceptedType;

    private void OnTriggerEnter(Collider other)
    {
        CollectableItem item = other.GetComponent<CollectableItem>();
        if (item == null) return;

        if (item.currentItemType != acceptedType)
        {
            Debug.Log($"Зона ждет {acceptedType}, а принесли {item.currentItemType}");
            return;
        }

        // Проверяем что сейчас активен квест который ждёт этот тип предмета
        if (QuestManager.Instance.currentQuestIndex < QuestManager.Instance.questList.Count)
        {
            var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];

            // Проверяем по questTag, а не по названиям (устойчиво к локализации/кодировкам)
            bool isCorrectQuest =
                (acceptedType == CollectableItem.ItemType.Trash && activeQuest.questTag == "trash-delivery") ||
                (acceptedType == CollectableItem.ItemType.Box && (activeQuest.questTag == "box-delivery" || activeQuest.questTag == "box-collect")) ||
                // fallback на текст для совместимости со старыми квестами
                (acceptedType == CollectableItem.ItemType.Trash && activeQuest.questTitle.Contains("мусор")) ||
                (acceptedType == CollectableItem.ItemType.Box && activeQuest.questTitle.Contains("коробку"));

            if (isCorrectQuest)
            {
                QuestManager.Instance.AddProgress(1);
                Destroy(other.gameObject);
                Debug.Log($"Предмет {item.currentItemType} засчитан!");
            }
        }
    }
}