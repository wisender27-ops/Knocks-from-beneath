using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

namespace KnocksFromBeneath
{

public class ThoughtManager : MonoBehaviour
{
    public static ThoughtManager Instance;
    private NarrativeReadingPanel readingPanel;
    private readonly Dictionary<string, float> _interactionHintCooldowns = new Dictionary<string, float>();

    [Header("UI элементы")]
    [SerializeField] private TextMeshProUGUI thoughtText; // Текст из TextMeshPro
    [SerializeField] private GameObject textCanvas;      // Весь объект Canvas (чтобы скрывать целиком)

    [Header("Настройки печати")]
    [SerializeField] private float typingSpeed = 0.05f;   // Скорость появления букв
    [SerializeField] private float displayDuration = 2.5f; // Сколько фраза висит после печати

    [Header("Звук печати")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] typeSounds;     // Массив из 3-х звуков кликов
    [Range(0.8f, 1.2f)][SerializeField] private float minPitch = 0.95f;
    [Range(0.8f, 1.2f)][SerializeField] private float maxPitch = 1.05f;


    void Awake()
    {
        // Делаем синглтон, чтобы обращаться из других скриптов через ThoughtManager.Instance
        if (Instance == null) Instance = this;

        // Скрываем текст при старте
        if (textCanvas != null) textCanvas.SetActive(false);
        if (thoughtText != null) thoughtText.text = "";
        readingPanel = gameObject.AddComponent<NarrativeReadingPanel>();
        readingPanel.Initialize(thoughtText != null ? thoughtText.font : null);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        HideImmediately();
    }

    public void HideImmediately()
    {
        if (readingPanel != null) readingPanel.StopPresentation();
        if (audioSource != null) audioSource.Stop();
        if (textCanvas != null) textCanvas.SetActive(false);
        if (thoughtText != null) thoughtText.text = "";
    }

    // Главная функция, которую мы вызываем из IntroSequence
    // lines - список фраз
    // onComplete - действие, которое выполнится в самом конце (например, выдача квеста)
    private const string ThoughtSpeaker = "МЫСЛИ";

    public void ShowThoughts(string[] lines, Action onComplete = null, Action onStart = null)
    {
        string[] snapshot = lines == null ? Array.Empty<string>() : (string[])lines.Clone();
        NarrativeDisplayQueue.Enqueue(this, () => DisplaySequence(snapshot, onComplete, onStart, false, ThoughtSpeaker, NarrativeStyle.Thought));
    }

    public bool TryShowInteractionThoughts(string key, string[] lines, float cooldown = 0.75f)
    {
        if (!isActiveAndEnabled || !NarrativeDisplayQueue.CanShowInteractionHint ||
            lines == null || lines.Length == 0 || string.IsNullOrEmpty(key)) return false;
        if (_interactionHintCooldowns.TryGetValue(key, out float nextAllowed) &&
            Time.unscaledTime < nextAllowed) return false;

        // Optional reactions never accumulate behind story dialogue or quest messages.
        ShowThoughts(lines, () => _interactionHintCooldowns[key] = Time.unscaledTime + Mathf.Max(0f, cooldown));
        return true;
    }

    public void ShowImportantThoughts(string[] lines, Action onComplete = null, Action onStart = null)
    {
        string[] snapshot = lines == null ? Array.Empty<string>() : (string[])lines.Clone();
        NarrativeDisplayQueue.Enqueue(this, () => DisplaySequence(snapshot, onComplete, onStart, true, ThoughtSpeaker, NarrativeStyle.Thought));
    }

    public void ShowDialogue(string speaker, string[] lines, Action onComplete = null, Action onStart = null)
    {
        string[] snapshot = lines == null ? Array.Empty<string>() : (string[])lines.Clone();
        NarrativeStyle style = speaker == ThoughtSpeaker ? NarrativeStyle.Thought : NarrativeStyle.Speech;
        NarrativeDisplayQueue.Enqueue(this, () => DisplaySequence(snapshot, onComplete, onStart, true, speaker, style));
    }

    // Записка показывается листом бумаги: строки дописываются на один лист по мере чтения.
    public void ShowNote(string title, string[] lines, Action onComplete = null, Action onStart = null)
    {
        string[] snapshot = lines == null ? Array.Empty<string>() : (string[])lines.Clone();
        NarrativeDisplayQueue.Enqueue(this, () => DisplaySequence(snapshot, onComplete, onStart, true, title, NarrativeStyle.Note));
    }

    private IEnumerator DisplaySequence(string[] lines, Action onComplete, Action onStart, bool important, string speaker, NarrativeStyle style)
    {
        if (lines.Length == 0 || readingPanel == null)
        {
            onStart?.Invoke();
            FinishCurrentRequest(onComplete);
            yield break;
        }
        try
        {
            yield return readingPanel.BeginSequence(important, style);
            onStart?.Invoke();
            foreach (string line in lines)
                yield return readingPanel.ShowLine(line, speaker, typingSpeed, Mathf.Max(2.5f, displayDuration));
        }
        finally
        {
            readingPanel.EndSequence();
        }
        FinishCurrentRequest(onComplete);
    }

    private void FinishCurrentRequest(Action onComplete)
    {
        onComplete?.Invoke();
    }
}
}