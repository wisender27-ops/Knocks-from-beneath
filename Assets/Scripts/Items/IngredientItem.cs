using UnityEngine;

namespace KnocksFromBeneath
{

// Продукт для ужина дня 2 (T-15). По структуре — двойник TrashPile: собирается кликом,
// засчитывается только если активный квест — "dinner-ingredients", иначе игнорируется.
public class IngredientItem : MonoBehaviour
{
    public void Collect()
    {
        if (QuestManager.Instance == null) return;
        if (QuestManager.Instance.questList == null ||
            QuestManager.Instance.currentQuestIndex < 0 ||
            QuestManager.Instance.currentQuestIndex >= QuestManager.Instance.questList.Count) return;

        var activeQuest = QuestManager.Instance.questList[QuestManager.Instance.currentQuestIndex];
        if (activeQuest == null) return;
        if (activeQuest.questTag != "dinner-ingredients")
        {
            Debug.Log("[IngredientItem] Сбор продуктов вне этапа 'dinner-ingredients' — игнор.");
            return;
        }

        QuestManager.Instance.AddProgress(1);
        Destroy(gameObject);
    }
}
}
