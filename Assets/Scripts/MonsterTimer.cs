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
        // Раньше при timerText == null таймер сразу вызывал OnTimerExpired() — отсутствие
        // текстового поля в инспекторе обнуляло весь 30-секундный отсчёт, и игровая логика
        // (концовка "не успел") зависела от косметической UI-ссылки. Теперь отсчёт всегда
        // идёт полные timerDuration секунд, а обновление текста — отдельный необязательный шаг.
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

                timerText.text = $"Монстр вылезет из дыры через {Mathf.CeilToInt(remaining)} секунд";
            }
            remaining -= Time.deltaTime;
            yield return null;
        }

        // Таймер истёк
        if (timerText != null) timerText.text = "";
        OnTimerExpired();
    }

    void OnTimerExpired()
    {
        // Страшный звук
        if (audioSource != null && monsterEscapeClip != null)
            audioSource.PlayOneShot(monsterEscapeClip);

        // Порядок важен: сначала публикуем событие. Оно синхронно резолвит концовку
        // "НЕ УСПЕЛ" (BranchEndingController.TryResolve), которая заодно и глушит таймер
        // через StopTimer() -> StopAllCoroutines() — раньше ShowFinalMessage() уже была
        // запущена К ЭТОМУ моменту и обрывалась на первом же слове в тот же кадр. Таймер
        // на этой строке уже и так закончился сам (мы внутри его собственного завершения),
        // так что более ранний StopTimer() тут безвреден — а корутину сообщения стартуем
        // ПОСЛЕ, когда её уже точно никто не убьёт в этом кадре.
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

        // Раньше текст ("ТЕБЕ. НУЖНО. ЗАКОЛОТИТЬ. ДЫРУ.") звучал как инструкция к действию —
        // ровно в момент, когда действовать уже поздно. Заменил на реакцию на провал,
        // не пересекающуюся по смыслу с последующей репликой "Не успел." в концовке.
        timerText.text = "ПОЗДНО.";
        yield return new WaitForSeconds(finalMessageWordDelay);

        timerText.text = "ПОЗДНО. ОНО ВЫШЛО.";
        yield return new WaitForSeconds(finalMessageWordDelay);

        timerText.text = "ПОЗДНО. ОНО ВЫШЛО. ОНО СВОБОДНО.";
        yield return new WaitForSeconds(finalMessageWordDelay);

        timerText.text = "ПОЗДНО. ОНО ВЫШЛО. ОНО СВОБОДНО. БЕГИ.";
        yield return new WaitForSeconds(finalMessageHoldDuration);

        // Убираем надпись
        timerText.text = "";
        timerText.fontSize = _defaultFontSize;
        timerText.color = Color.white;

        _isRunning = false;
    }
}
}
