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
        if (audioSource != null) audioSource.Stop();
        if (textCanvas != null) textCanvas.SetActive(false);
        if (thoughtText != null) thoughtText.text = "";
    }

    // Главная функция, которую мы вызываем из IntroSequence
    // lines - список фраз
    // onComplete - действие, которое выполнится в самом конце (например, выдача квеста)
    public void ShowThoughts(string[] lines, Action onComplete = null, Action onStart = null)
    {
        string[] snapshot = lines == null ? Array.Empty<string>() : (string[])lines.Clone();
        NarrativeDisplayQueue.Enqueue(this, () => DisplaySequence(snapshot, onComplete, onStart));
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

    private IEnumerator DisplaySequence(string[] lines, Action onComplete, Action onStart)
    {
        onStart?.Invoke();

        if (lines.Length == 0 || textCanvas == null || thoughtText == null)
        {
            FinishCurrentRequest(onComplete);
            yield break;
        }

        textCanvas.SetActive(true);

        foreach (string rawLine in lines)
        {
            string line = rawLine ?? string.Empty;
            thoughtText.text = ""; // Очищаем поле перед новой фразой

            foreach (char letter in line.ToCharArray())
            {
                thoughtText.text += letter;

                // Проигрываем случайный звук клика с разным питчем
                if (typeSounds != null && typeSounds.Length > 0 && audioSource != null)
                {
                    audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
                    audioSource.PlayOneShot(typeSounds[UnityEngine.Random.Range(0, typeSounds.Length)], 0.4f);
                }

                // Пауза после знаков препинания для естественности
                if (letter == ',' || letter == '.' || letter == '!' || letter == '?')
                    yield return new WaitForSeconds(typingSpeed * 3f);
                else
                    yield return new WaitForSeconds(typingSpeed);
            }

            // Ждем, пока игрок дочитает фразу
            yield return new WaitForSeconds(displayDuration);

            // Небольшая пауза пустой строки перед следующей
            thoughtText.text = "";
            yield return new WaitForSeconds(0.5f);
        }

        // Всё закончилось — скрываем UI
        textCanvas.SetActive(false);

        // ВЫПОЛНЯЕМ ДЕЙСТВИЕ, которое передали (например, включение квеста)
        FinishCurrentRequest(onComplete);
    }

    private void FinishCurrentRequest(Action onComplete)
    {
        onComplete?.Invoke();
    }
}
}
