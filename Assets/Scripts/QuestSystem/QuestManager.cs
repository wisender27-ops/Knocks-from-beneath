using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using System.Collections;
using System;

namespace KnocksFromBeneath
{

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    public static event Action<string> OnActiveQuestTagChanged;
    public event Action OnQuestChanged;
    public int ToastRevision { get; private set; }
    public bool IsToastVisible => questUiText != null && questUiText.alpha > 0f;

    [System.Serializable]
    public class QuestData
    {
        public bool IsCompleted => requiredAmount > 0 && currentAmount >= requiredAmount;
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

    private QuestData _lastPresentedQuest;
    private Vector2 _toastOrigin;
    private bool _toastOriginCaptured;
    private string _lastQueuedText;
    private string _lastActiveQuestTag = null;

    void Awake()
    {
        Instance = this;
        HideToast();
        if (questList == null)
            questList = new List<QuestData>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnEnable()
    {
        // Broadcast initial state for listeners that enable after QuestManager.
        NotifyActiveQuestTagIfChanged(force: true);
    }

    void Start()
    {
        UpdateUI();
    }

    void OnDisable()
    {
        HideToast();
    }

    public QuestData GetActiveQuest()
    {
        if (questList == null || currentQuestIndex < 0 || currentQuestIndex >= questList.Count)
            return null;
        QuestData quest = questList[currentQuestIndex];
        return quest != null && !quest.IsCompleted ? quest : null;
    }

    public void AddProgress(int amount)
    {
        if (amount <= 0 || currentQuestIndex < 0 || currentQuestIndex >= questList.Count) return;

        QuestData activeQuest = questList[currentQuestIndex];
        if (activeQuest == null) return;
        activeQuest.currentAmount += amount;

        Debug.Log($"[QuestManager] Quest '{activeQuest.questTitle}' (tag='{activeQuest.questTag}') progress {activeQuest.currentAmount}/{activeQuest.requiredAmount}");

        if (activeQuest.currentAmount >= activeQuest.requiredAmount)
        {
            Debug.Log($"Квест '{activeQuest.questTitle}' выполнен!");

            // Сначала увеличиваем индекс, чтобы UpdateUI понял, что квестов больше нет
            currentQuestIndex++;

            GameAudio.PlayQuestCue(true);
            // Теперь вызываем событие (например, мысли игрока о следующем задании)
            activeQuest.onQuestComplete?.Invoke();
        }

        UpdateUI();
        NotifyActiveQuestTagIfChanged();
        OnQuestChanged?.Invoke();
    }

    public void RecordQuestCompletion(string questTag)
    {
        QuestData quest = GetActiveQuest();
        if (quest == null || string.IsNullOrEmpty(questTag) ||
            !string.Equals(quest.questTag, questTag, StringComparison.Ordinal)) return;
        // Direct story transitions already invoke their next step themselves.
        quest.currentAmount = quest.requiredAmount;
        currentQuestIndex++;
        GameAudio.PlayQuestCue(true);
        UpdateUI();
        NotifyActiveQuestTagIfChanged();
        OnQuestChanged?.Invoke();
    }

    public void CreateQuest(string title, int amount, UnityEngine.Events.UnityAction onCompleteAction = null, string questTag = "")
    {
        QuestData newQuest = new QuestData();
        newQuest.questTitle = title ?? string.Empty;
        newQuest.requiredAmount = Mathf.Max(1, amount);
        newQuest.currentAmount = 0;
        newQuest.questTag = questTag;
        newQuest.onQuestComplete = new UnityEvent();

        if (onCompleteAction != null)
            newQuest.onQuestComplete.AddListener(onCompleteAction);

        questList.Add(newQuest);

        // Always sync to the newest quest — CreateQuest is only
        // ever called from story callbacks, so a new quest = active quest
        currentQuestIndex = questList.Count - 1;
        GameAudio.PlayQuestCue(false);

        UpdateUI();
        NotifyActiveQuestTagIfChanged();
        OnQuestChanged?.Invoke();
    }

    public string GetActiveQuestTag()
    {
        if (currentQuestIndex < 0 || currentQuestIndex >= questList.Count) return "";
        QuestData activeQuest = questList[currentQuestIndex];
        return activeQuest != null ? activeQuest.questTag ?? "" : "";
    }

    public bool IsQuestActive(string questTag)
    {
        if (string.IsNullOrEmpty(questTag)) return false;
        return string.Equals(GetActiveQuestTag(), questTag, StringComparison.Ordinal);
    }

    public bool IsItemRequired(ItemType itemType)
    {
        string requiredQuestTag;
        switch (itemType)
        {
            case ItemType.Crowbar:
                requiredQuestTag = "crowbar-find";
                break;
            case ItemType.Hammer:
                requiredQuestTag = "hammer-find";
                break;
            case ItemType.Flashlight:
                requiredQuestTag = "flashlight-find";
                break;
            default:
                return false;
        }

        if (!IsQuestActive(requiredQuestTag)) return false;

        QuestData activeQuest = questList[currentQuestIndex];
        return activeQuest != null && activeQuest.currentAmount < activeQuest.requiredAmount;
    }

    void NotifyActiveQuestTagIfChanged(bool force = false)
    {
        string currentTag = GetActiveQuestTag();
        if (!force && string.Equals(_lastActiveQuestTag, currentTag, StringComparison.Ordinal))
            return;

        _lastActiveQuestTag = currentTag;
        OnActiveQuestTagChanged?.Invoke(currentTag);
    }

    void UpdateUI()
    {
        QuestData quest = GetActiveQuest();
        bool newQuest = !ReferenceEquals(_lastPresentedQuest, quest);
        _lastPresentedQuest = quest;
        if (quest == null)
        {
            _lastQueuedText = null;
            return;
        }

        if (newQuest) ToastRevision++;
        if (questUiText == null) return;
        if (!_toastOriginCaptured)
        {
            _toastOrigin = questUiText.rectTransform.anchoredPosition;
            _toastOriginCaptured = true;
        }
        string text = quest.requiredAmount <= 1
            ? quest.questTitle
            : $"{quest.questTitle} ({Mathf.Max(0, quest.requiredAmount - quest.currentAmount)})";
        if (!newQuest && text == _lastQueuedText) return;
        _lastQueuedText = text;
        if (isActiveAndEnabled && Application.isPlaying)
            NarrativeDisplayQueue.Enqueue(this, () => PlayQuestTextColorTransition(text));
    }

    public void HideToast()
    {
        if (questUiText == null) return;
        questUiText.color = baseQuestColor;
        questUiText.alpha = 0f;
        if (_toastOriginCaptured) questUiText.rectTransform.anchoredPosition = _toastOrigin;
        if (_toastBacking != null) _toastBacking.enabled = false;
    }

    // Уведомление о цели оформлено как субтитр: чёрная подложка точно по тексту.
    UnityEngine.UI.Image _toastBacking;

    void SyncToastBacking()
    {
        RectTransform text = questUiText.rectTransform;
        if (_toastBacking == null)
        {
            var backing = new GameObject("QuestToastBacking", typeof(RectTransform)).GetComponent<RectTransform>();
            backing.SetParent(text.parent, false);
            backing.SetSiblingIndex(text.GetSiblingIndex());
            _toastBacking = backing.gameObject.AddComponent<UnityEngine.UI.Image>();
            _toastBacking.raycastTarget = false;
        }
        Bounds bounds = questUiText.textBounds;
        _toastBacking.enabled = questUiText.alpha > 0f && bounds.size.x > 0f;
        if (!_toastBacking.enabled) return;
        float padX = questUiText.fontSize * .3f, padY = questUiText.fontSize * .14f;
        RectTransform rect = _toastBacking.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = Vector2.zero;
        rect.localRotation = text.localRotation;
        rect.localScale = text.localScale;
        rect.localPosition = text.localPosition + text.localRotation * Vector3.Scale(bounds.min - new Vector3(padX, padY), text.localScale);
        rect.sizeDelta = new Vector2(bounds.size.x + padX * 2f, bounds.size.y + padY * 2f);
        _toastBacking.color = new Color(0f, 0f, 0f, .84f * questUiText.alpha);
    }

    IEnumerator PlayQuestTextColorTransition(string text)
    {
        if (questUiText == null) yield break;
        questUiText.text = text;
        questUiText.ForceMeshUpdate(true);
        const float fadeIn = .2f;
        const float hold = 3f;
        const float fadeOut = .45f;
        float elapsed = 0f;
        while (isActiveAndEnabled && questUiText != null && elapsed < fadeIn + hold + fadeOut)
        {
            elapsed += Time.deltaTime;
            float colorProgress = Mathf.Clamp01((elapsed - highlightHoldDuration) / Mathf.Max(.01f, fadeToBaseDuration));
            Color color = Color.Lerp(highlightQuestColor, baseQuestColor, colorProgress);
            float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - fadeIn - hold) / fadeOut));
            color.a = baseQuestColor.a * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeIn)) * (1f - exit);
            questUiText.color = color;
            questUiText.rectTransform.anchoredPosition = _toastOrigin + Vector2.up * (12f * exit);
            SyncToastBacking();
            yield return null;
        }
        HideToast();
    }
}
}
