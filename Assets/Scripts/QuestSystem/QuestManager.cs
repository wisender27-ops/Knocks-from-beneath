using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [System.Serializable]
    public class QuestData {
        public string questTitle;   // "Вынести мусор"
        public int requiredAmount;  // 5
        public int currentAmount;   // 0
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

        // Если квест выполнен
        if (activeQuest.currentAmount >= activeQuest.requiredAmount)
        {
            Debug.Log($"Квест '{activeQuest.questTitle}' выполнен!");
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