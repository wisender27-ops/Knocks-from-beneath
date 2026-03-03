using UnityEngine;

public class QuestCollector : MonoBehaviour
{
    [Header("Настройки")]
    public string targetTag = "Pickable"; // Тег твоих мешков
    public int questIndexForThisZone = 0; // Для какого по счету квеста эта зона? (0 - первый)

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем: 
        // 1. Тот ли это объект?
        // 2. Тот ли сейчас активен квест в менеджере?
        if (other.CompareTag(targetTag) && QuestManager.Instance.currentQuestIndex == questIndexForThisZone)
        {
            // Сообщаем менеджеру о прогрессе
            QuestManager.Instance.AddProgress(1);

            // Удаляем мешок
            Destroy(other.gameObject);

            Debug.Log("Предмет засчитан и удален!");
        }
    }
}