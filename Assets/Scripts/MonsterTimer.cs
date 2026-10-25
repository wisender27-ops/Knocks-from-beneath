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

    public bool IsRunning => _isRunning;

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
        StartCoroutine(TimerRoutine());
    }

    public void PauseTimer() => _paused = true;

    public void ResumeTimer() => _paused = false;

    public void StopTimer()
    {
        StopAllCoroutines();
        _isRunning = false;
        _paused = false;
    }

    IEnumerator TimerRoutine()
    {
        // Чтение обязательной реплики может приостановить отсчёт.
        _isRunning = true;
        float remaining = timerDuration;
        while (remaining > 0)
        {
            if (!_paused) remaining -= Time.deltaTime;
            yield return null;
        }
        _isRunning = false;
        // Истечение меняет исход следующего взаимодействия с люком, но не запускает нападение.
        GameEvents.OnMonsterTimerExpired?.Invoke();
    }
}
}
