using UnityEngine;
using TMPro;
using System;
using System.Collections;

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

    void Awake()
    {
        // Делаем синглтон, чтобы обращаться из других скриптов через ThoughtManager.Instance
        if (Instance == null) Instance = this;

        // Скрываем текст при старте
        if (textCanvas != null) textCanvas.SetActive(false);
        if (thoughtText != null) thoughtText.text = "";
    }

    // Главная функция, которую мы вызываем из IntroSequence
    // lines - список фраз
    // onComplete - действие, которое выполнится в самом конце (например, выдача квеста)
    public void ShowThoughts(string[] lines, Action onComplete = null)
    {
        if (_isDisplaying) return; // Если уже что-то печатаем — игнорируем новый вызов
        StartCoroutine(DisplaySequence(lines, onComplete));
    }

    private IEnumerator DisplaySequence(string[] lines, Action onComplete)
    {
        _isDisplaying = true;
        textCanvas.SetActive(true);

        foreach (string line in lines)
        {
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
        onComplete?.Invoke();
    }
}