using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events; // Добавь это!

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [System.Serializable]
    public class QuestData
    {
        public string questTitle;
        public int requiredAmount;
        public int currentAmount;
        public UnityEvent onQuestComplete; // Событие для каждого конкретного квеста
    }

    public List<QuestData> questList = new List<QuestData>();
    public int currentQuestIndex = 0;
    public TextMeshProUGUI questUiText;

    void Awake() => Instance = this;
    void Start() => UpdateUI();

    public void AddProgress(int amount)
    {
        if (currentQuestIndex >= questList.Count) return;

        QuestData activeQuest = questList[currentQuestIndex];
        activeQuest.currentAmount += amount;

        if (activeQuest.currentAmount >= activeQuest.requiredAmount)
        {
            Debug.Log($"Квест '{activeQuest.questTitle}' выполнен!");

            // Запускаем событие этого квеста (например, смену дня)
            activeQuest.onQuestComplete?.Invoke();

            currentQuestIndex++;
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        if (questUiText == null) return;
        if (currentQuestIndex < questList.Count)
        {
            QuestData q = questList[currentQuestIndex];
            questUiText.text = $"{q.questTitle}: {q.currentAmount} / {q.requiredAmount}";
        }
        else
        {
            questUiText.text = "Все задания выполнены!";
        }
    }
}