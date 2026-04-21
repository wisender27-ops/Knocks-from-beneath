using UnityEngine;

public class TrashPile : MonoBehaviour
{
    public void Collect()
    {
        if (QuestManager.Instance == null) return;
        if (QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count) return;

        var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
        if (activeQuest.questTag != "trash-collect")
        {
            Debug.Log("[TrashPile] Сбор мусора вне этапа 'trash-collect' — игнор.");
            return;
        }

        QuestManager.Instance.AddProgress(1);
        if (TrashManager.Instance != null)
            TrashManager.Instance.OnPileCollected();
        Destroy(gameObject);
    }
}