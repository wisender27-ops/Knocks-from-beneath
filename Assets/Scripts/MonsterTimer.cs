using UnityEngine;
using System.Collections;
using TMPro;

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

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (timerText != null) timerText.text = "";
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

        while (remaining > 0)
        {
            // Текст меняет цвет — белый → жёлтый → красный
            if (remaining > 20f)
                timerText.color = Color.white;
            else if (remaining > 10f)
                timerText.color = Color.yellow;
            else
                timerText.color = Color.red;

            timerText.text = $"Monster will emerge from the hole in {Mathf.CeilToInt(remaining)} seconds";
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
        StartCoroutine(ShowFinalMessage());
    }

    IEnumerator ShowFinalMessage()
    {
        timerText.fontSize = 36f;
        timerText.color = Color.red;

        // Каждое слово появляется отдельно с паузой
        timerText.text = "ТЕБЕ.";
        yield return new WaitForSeconds(0.6f);

        timerText.text = "ТЕБЕ. НУЖНО.";
        yield return new WaitForSeconds(0.6f);

        timerText.text = "ТЕБЕ. НУЖНО. ЗАКОЛОТИТЬ.";
        yield return new WaitForSeconds(0.6f);

        timerText.text = "ТЕБЕ. НУЖНО. ЗАКОЛОТИТЬ. ДЫРУ.";
        yield return new WaitForSeconds(3f);

        // Убираем надпись
        timerText.text = "";
        timerText.fontSize = 24f;
        timerText.color = Color.white;

        _isRunning = false;
    }
}