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

            // Проверяем по названию квеста а не по индексу
            bool isCorrectQuest =
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