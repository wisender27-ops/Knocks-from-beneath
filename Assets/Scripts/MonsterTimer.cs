using UnityEngine;
using System.Collections;
using TMPro;

namespace KnocksFromBeneath
{

public class MonsterTimer : MonoBehaviour
{
    public static MonsterTimer Instance;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Настройки")]
    public float timerDuration = 30f;
    [SerializeField] private float yellowThreshold = 20f;
    [SerializeField] private float redThreshold = 10f;

    [Header("Звук когда таймер истекает")]
    public AudioSource audioSource;
    public AudioClip monsterEscapeClip;

    [Header("Финальное сообщение")]
    [SerializeField] private float finalMessageFontSize = 36f;
    [SerializeField] private float finalMessageWordDelay = 0.6f;
    [SerializeField] private float finalMessageHoldDuration = 3f;

    private bool _isRunning = false;
    private float _defaultFontSize;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (timerText != null)
        {
            timerText.text = "";
            _defaultFontSize = timerText.fontSize;
        }
    }

    public void StartTimer()
    {
        if (_isRunning) return;
        StartCoroutine(TimerRoutine());
    }

    public void StopTimer()
    {
        StopAllCoroutines();
        _isRunning = false;
        if (timerText != null) timerText.text = "";
    }

    IEnumerator TimerRoutine()
    {
        _isRunning = true;
        float remaining = timerDuration;

        if (timerText == null)
        {
            _isRunning = false;
            OnTimerExpired();
            yield break;
        }

        while (remaining > 0)
        {
            // Текст меняет цвет — белый → жёлтый → красный
            if (remaining > yellowThreshold)
                timerText.color = Color.white;
            else if (remaining > redThreshold)
                timerText.color = Color.yellow;
            else
                timerText.color = Color.red;

            timerText.text = $"Монстр вылезет из дыры через {Mathf.CeilToInt(remaining)} секунд";
            remaining -= Time.deltaTime;
            yield return null;
        }

        // Таймер истёк
        timerText.text = "";
        OnTimerExpired();
    }

    void OnTimerExpired()
    {
        // Страшный звук
        if (audioSource != null && monsterEscapeClip != null)
            audioSource.PlayOneShot(monsterEscapeClip);

        // Надпись на экране
        if (timerText != null)
            StartCoroutine(ShowFinalMessage());
    }

    IEnumerator ShowFinalMessage()
    {
        timerText.fontSize = finalMessageFontSize;
        timerText.color = Color.red;

        // Каждое слово появляется отдельно с паузой
        timerText.text = "ТЕБЕ.";
        yield return new WaitForSeconds(finalMessageWordDelay);

        timerText.text = "ТЕБЕ. НУЖНО.";
        yield return new WaitForSeconds(finalMessageWordDelay);

        timerText.text = "ТЕБЕ. НУЖНО. ЗАКОЛОТИТЬ.";
        yield return new WaitForSeconds(finalMessageWordDelay);

        timerText.text = "ТЕБЕ. НУЖНО. ЗАКОЛОТИТЬ. ДЫРУ.";
        yield return new WaitForSeconds(finalMessageHoldDuration);

        // Убираем надпись
        timerText.text = "";
        timerText.fontSize = _defaultFontSize;
        timerText.color = Color.white;

        _isRunning = false;
    }
}
}
