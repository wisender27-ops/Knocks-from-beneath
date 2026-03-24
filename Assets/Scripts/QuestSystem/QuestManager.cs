using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Collections;

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
        public string questTag; // explicit quest type / purpose (trash, box, flashlight, crowbar, etc.)
    }

    public List<QuestData> questList = new List<QuestData>();
    public int currentQuestIndex = 0;
    public TextMeshProUGUI questUiText;

    [Header("Quest Text Vibe")]
    [SerializeField] private Color baseQuestColor = new Color(0.9019608f, 0.88235295f, 0.8392157f, 1f); // #E6E1D6
    [SerializeField] private Color highlightQuestColor = new Color(0.8509804f, 0.6901961f, 0.41568628f, 1f); // #D9B06A
    [SerializeField] private float highlightHoldDuration = 0.2f;
    [SerializeField] private float fadeToBaseDuration = 0.6f;

    private string _lastRenderedQuestText = "";
    private Coroutine _questColorRoutine;

    void Awake() => Instance = this;

    // В Start теперь UpdateUI не вызываем, чтобы не было пустых 0/0
    void Start()
    {
        if (questUiText != null)
        {
            questUiText.text = "";
            questUiText.color = baseQuestColor;
        }
    }

    public void AddProgress(int amount)
    {
        if (currentQuestIndex >= questList.Count) return;

        QuestData activeQuest = questList[currentQuestIndex];
        activeQuest.currentAmount += amount;

        Debug.Log($"[QuestManager] Quest '{activeQuest.questTitle}' (tag='{activeQuest.questTag}') progress {activeQuest.currentAmount}/{activeQuest.requiredAmount}");

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

    public void CreateQuest(string title, int amount, UnityEngine.Events.UnityAction onCompleteAction = null, string questTag = "")
    {
        QuestData newQuest = new QuestData();
        newQuest.questTitle = title;
        newQuest.requiredAmount = amount;
        newQuest.currentAmount = 0;
        newQuest.questTag = questTag;
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
            string newText;

            // Для одношаговых квестов выводим только цель.
            // Для многошаговых — цель + сколько осталось в формате "(N)".
            if (q.requiredAmount <= 1)
            {
                newText = q.questTitle;
            }
            else
            {
                int remaining = Mathf.Max(0, q.requiredAmount - q.currentAmount);
                newText = $"{q.questTitle} ({remaining})";
            }

            ApplyQuestText(newText);
        }
        else
        {
            // Если все квесты выполнены или их еще нет — просто очищаем текст
            questUiText.text = "";
            _lastRenderedQuestText = "";
            if (_questColorRoutine != null)
            {
                StopCoroutine(_questColorRoutine);
                _questColorRoutine = null;
            }
            questUiText.color = baseQuestColor;
        }
    }

    void ApplyQuestText(string newText)
    {
        if (questUiText == null) return;

        bool changed = _lastRenderedQuestText != newText;
        questUiText.text = newText;

        if (changed)
        {
            _lastRenderedQuestText = newText;
            if (_questColorRoutine != null) StopCoroutine(_questColorRoutine);
            _questColorRoutine = StartCoroutine(PlayQuestTextColorTransition());
        }
    }

    IEnumerator PlayQuestTextColorTransition()
    {
        questUiText.color = highlightQuestColor;

        if (highlightHoldDuration > 0f)
            yield return new WaitForSeconds(highlightHoldDuration);

        float t = 0f;
        float duration = Mathf.Max(0.01f, fadeToBaseDuration);
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / duration);
            questUiText.color = Color.Lerp(highlightQuestColor, baseQuestColor, lerp);
            yield return null;
        }

        questUiText.color = baseQuestColor;
        _questColorRoutine = null;
    }
}