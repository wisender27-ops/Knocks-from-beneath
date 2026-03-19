using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [System.Serializable]
    public class QuestData
    {
        public string questTitle;
        public int requiredAmount;
        public int currentAmount;
        public UnityEvent onQuestComplete;
    }

    public List<QuestData> questList = new List<QuestData>();
    public int currentQuestIndex = 0;
    public TextMeshProUGUI questUiText;

    void Awake() => Instance = this;

    // В Start теперь UpdateUI не вызываем, чтобы не было пустых 0/0
    void Start()
    {
        if (questUiText != null) questUiText.text = "";
    }

    public void AddProgress(int amount)
    {
        if (currentQuestIndex >= questList.Count) return;

        QuestData activeQuest = questList[currentQuestIndex];
        activeQuest.currentAmount += amount;

        if (activeQuest.currentAmount >= activeQuest.requiredAmount)
        {
            Debug.Log($"Квест '{activeQuest.questTitle}' выполнен!");

            // Сначала увеличиваем индекс, чтобы UpdateUI понял, что квестов больше нет
            currentQuestIndex++;

            // Теперь вызываем событие (например, мысли игрока о следующем задании)
            activeQuest.onQuestComplete?.Invoke();
        }

        UpdateUI();
    }

    public void CreateQuest(string title, int amount, UnityEngine.Events.UnityAction onCompleteAction = null)
    {
        QuestData newQuest = new QuestData();
        newQuest.questTitle = title;
        newQuest.requiredAmount = amount;
        newQuest.currentAmount = 0;
        newQuest.onQuestComplete = new UnityEvent();

        if (onCompleteAction != null)
            newQuest.onQuestComplete.AddListener(onCompleteAction);

        questList.Add(newQuest);

        // Always sync to the newest quest — CreateQuest is only
        // ever called from story callbacks, so a new quest = active quest
        currentQuestIndex = questList.Count - 1;

        UpdateUI();
    }

    void UpdateUI()
    {
        if (questUiText == null) return;

        // Если есть активный квест — показываем его
        if (currentQuestIndex < questList.Count)
        {
            QuestData q = questList[currentQuestIndex];
            questUiText.text = $"{q.questTitle}: {q.currentAmount} / {q.requiredAmount}";
        }
        else
        {
            // Если все квесты выполнены или их еще нет — просто очищаем текст
            questUiText.text = "";
        }
    }
}