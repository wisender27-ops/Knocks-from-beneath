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

    private bool _isDisplaying = false;
    private readonly Queue<ThoughtRequest> _pendingRequests = new Queue<ThoughtRequest>();

    private sealed class ThoughtRequest
    {
        public readonly string[] lines;
        public readonly Action onComplete;
        public readonly Action onStart;

        public ThoughtRequest(string[] lines, Action onComplete, Action onStart)
        {
            this.lines = lines;
            this.onComplete = onComplete;
            this.onStart = onStart;
        }
    }

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

    // Главная функция, которую мы вызываем из IntroSequence
    // lines - список фраз
    // onComplete - действие, которое выполнится в самом конце (например, выдача квеста)
    public void ShowThoughts(string[] lines, Action onComplete = null, Action onStart = null)
    {
        if (lines == null || lines.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (_isDisplaying)
        {
            _pendingRequests.Enqueue(new ThoughtRequest(lines, onComplete, onStart));
            return;
        }

        StartCoroutine(DisplaySequence(lines, onComplete, onStart));
    }

    private IEnumerator DisplaySequence(string[] lines, Action onComplete, Action onStart)
    {
        _isDisplaying = true;
        onStart?.Invoke();

        if (textCanvas == null || thoughtText == null)
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
        _isDisplaying = false;

        // ВЫПОЛНЯЕМ ДЕЙСТВИЕ, которое передали (например, включение квеста)
        FinishCurrentRequest(onComplete);
    }

    private void FinishCurrentRequest(Action onComplete)
    {
        _isDisplaying = false;
        onComplete?.Invoke();

        if (!_isDisplaying && _pendingRequests.Count > 0)
        {
            ThoughtRequest next = _pendingRequests.Dequeue();
            StartCoroutine(DisplaySequence(next.lines, next.onComplete, next.onStart));
        }
    }
}
}
