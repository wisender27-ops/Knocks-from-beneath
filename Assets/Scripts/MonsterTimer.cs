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
    private bool _paused;
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
        _paused = false;
        StartCoroutine(TimerRoutine());
    }

    public void PauseTimer() => _paused = true;

    public void ResumeTimer() => _paused = false;

    public void StopTimer()
    {
        StopAllCoroutines();
        _isRunning = false;
        _paused = false;
        if (timerText != null)
        {
            timerText.text = "";
            // StopAllCoroutines() может оборвать ShowFinalMessage() на середине — вернуть
            // размер/цвет шрифта, иначе следующий обычный квест-таймер останется красным 36pt.
            timerText.fontSize = _defaultFontSize;
            timerText.color = Color.white;
        }
    }

    IEnumerator TimerRoutine()
    {
        // Дедлайн работает без UI; чтение обязательной реплики может приостановить отсчёт.
        _isRunning = true;
        float remaining = timerDuration;

        while (remaining > 0)
        {
            if (timerText != null)
            {
                // Текст меняет цвет — белый → жёлтый → красный
                if (remaining > yellowThreshold)
                    timerText.color = Color.white;
                else if (remaining > redThreshold)
                    timerText.color = Color.yellow;
                else
                    timerText.color = Color.red;

                timerText.text = $"Заколотить подвал: {Mathf.CeilToInt(remaining)} с";
            }
            if (!_paused) remaining -= Time.deltaTime;
            yield return null;
        }

        // Таймер истёк
        if (timerText != null) timerText.text = "";
        OnTimerExpired();
    }

    void OnTimerExpired()
    {
        // Истечение меняет исход следующего взаимодействия с люком, но не запускает нападение.
        GameEvents.OnMonsterTimerExpired?.Invoke();

        if (timerText != null)
            StartCoroutine(ShowFinalMessage());
        else
            _isRunning = false; // ShowFinalMessage() сама сбросит флаг в конце, если запустилась
    }

    IEnumerator ShowFinalMessage()
    {
        timerText.fontSize = finalMessageFontSize;
        timerText.color = Color.red;

        timerText.text = "Время вышло";
        yield return new WaitForSeconds(finalMessageHoldDuration);

        // Убираем надпись
        timerText.text = "";
        timerText.fontSize = _defaultFontSize;
        timerText.color = Color.white;

        _isRunning = false;
    }
}
}
