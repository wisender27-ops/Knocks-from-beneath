using UnityEngine;
using System.Collections;
using TMPro;

namespace KnocksFromBeneath
{

// Дедлайн ночи 2 работает без цифр на экране (решение автора, M-03): время читается только
// через происходящее в мире (P-04). timerText оставлен ради ссылки в сцене и лишь очищается.
public class MonsterTimer : MonoBehaviour
{
    public static MonsterTimer Instance;

    [Header("UI")]
    public TextMeshProUGUI timerText;

    [Header("Настройки")]
    public float timerDuration = 30f;

    [Header("Звук когда таймер истекает")]
    public AudioSource audioSource;
    public AudioClip monsterEscapeClip;

    private bool _isRunning = false;
    private bool _paused;
    private float _remaining;

    public event System.Action Stopped;

    public bool IsRunning => _isRunning;
    public float Remaining => _isRunning ? _remaining : 0f;
    // Невидимая шкала P-04: 0 — только начался, 1 — вышло время. Подача — DiegeticDeadline.
    public float Progress => _isRunning && timerDuration > 0f ? Mathf.Clamp01(1f - _remaining / timerDuration) : 0f;
    public bool IsTicking => _isRunning && !_paused && !NarrativeReadingPanel.IsModal && !MinigameWindow.IsAnyOpen;

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
        if (timerText != null) timerText.text = "";
    }

    public void StartTimer()
    {
        if (_isRunning) return;
        _paused = false;
        _remaining = timerDuration;
        _isRunning = true;
        StartCoroutine(TimerRoutine());
    }

    public void PauseTimer() => _paused = true;

    public void ResumeTimer() => _paused = false;

    public void StopTimer()
    {
        StopAllCoroutines();
        _isRunning = false;
        _paused = false;
        Stopped?.Invoke();
    }

    IEnumerator TimerRoutine()
    {
        // Чтение обязательной реплики или окно выбора приостанавливают отсчёт.
        while (_remaining > 0)
        {
            if (IsTicking) _remaining -= Time.deltaTime;
            yield return null;
        }
        _isRunning = false;
        // Истечение меняет исход следующего взаимодействия с люком, но не запускает нападение.
        GameEvents.OnMonsterTimerExpired?.Invoke();
    }
}
}
