using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Первая ночь: звуковое присутствие без нападения, затем возвращение к утру.
// Постановку исполняет переданный IntroSequence сценарий; этот контроллер ведёт переходы.
public sealed class NightOneController
{
    private readonly CanvasGroup _fadeScreen;
    private readonly SkyboxSwitcher _skySwitcher;
    private readonly GameObject _knockController;
    private readonly Action _teleportToBed;
    private readonly Func<IEnumerator, Coroutine> _startCoroutine;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onNightFinished;
    private readonly Func<IEnumerator> _nightScare;
    private readonly Func<IEnumerator> _neighborVisit;
    private readonly Func<bool> _didMeetNeighbor;

    private bool _fogBeforeNight;
    private bool _isRunning;

    private const float FadeDuration = 1.5f;
    private const float KnockWindowSeconds = 6f; // окно активности RandomKnock — при его min/maxDelay (2-5с) даёт максимум пару стуков

    public NightOneController(
        CanvasGroup fadeScreen,
        SkyboxSwitcher skySwitcher,
        GameObject knockController,
        Action teleportToBed,
        Func<IEnumerator, Coroutine> startCoroutine,
        Action<string[], Action> showThoughts,
        Action onNightFinished,
        Func<IEnumerator> nightScare = null,
        Func<IEnumerator> neighborVisit = null,
        Func<bool> didMeetNeighbor = null)
    {
        _fadeScreen = fadeScreen;
        _skySwitcher = skySwitcher;
        _knockController = knockController;
        _teleportToBed = teleportToBed;
        _startCoroutine = startCoroutine;
        _showThoughts = showThoughts;
        _onNightFinished = onNightFinished;
        _nightScare = nightScare;
        _neighborVisit = neighborVisit;
        _didMeetNeighbor = didMeetNeighbor;
    }

    public void Run()
    {
        if (_isRunning) return;
        _isRunning = true;
        _startCoroutine(RunRoutine());
    }

    IEnumerator RunRoutine()
    {
        yield return Fade(0f, 1f);

        GameEvents.OnNightStarted?.Invoke();
        if (_skySwitcher != null) _skySwitcher.isDayTime = false;
        // Ночь 1 — единственный отрезок, после которого игра возвращается в день,
        // поэтому туман тут не выключается насовсем, а запоминается и восстанавливается.
        _fogBeforeNight = RenderSettings.fog;
        RenderSettings.fog = false;
        _teleportToBed?.Invoke();

        if (_knockController != null) _knockController.SetActive(false);
        yield return Fade(1f, 0f);

        if (_nightScare != null)
            yield return _nightScare();
        else
        {
            if (_knockController != null) _knockController.SetActive(true);
            yield return new WaitForSeconds(KnockWindowSeconds);
            if (_knockController != null) _knockController.SetActive(false);
        }

        if (_neighborVisit != null) yield return _neighborVisit();

        bool metNeighbor = _didMeetNeighbor != null && _didMeetNeighbor();
        _showThoughts(metNeighbor ? new[] { "Он видел кого-то внизу. Но я всё это время был в кровати.", "Нет. Сам сказал — показалось. Надо спать." } : new string[] {
            "Опять этот стук...",
            "Нет. Не накручивай себя.",
            "Затихло же. Надо спать."
        }, OnCalmNightFinished);
    }

    void OnCalmNightFinished()
    {
        _startCoroutine(WakeUpInMorning());
    }

    IEnumerator WakeUpInMorning()
    {
        yield return Fade(0f, 1f);
        _teleportToBed?.Invoke();
        yield return new WaitForSeconds(2f);

        // SkyboxSwitcher применяет состояние в Update — оставляем кадр под чёрным экраном.
        if (_skySwitcher != null) _skySwitcher.isDayTime = true;
        RenderSettings.fog = _fogBeforeNight;
        GameEvents.OnDayStarted?.Invoke();
        yield return null;
        yield return Fade(1f, 0f);

        _showThoughts(new string[] {
            "Ну вот, дожил до утра.",
            "Сейчас тихо. А дел всё ещё до хрена."
        }, () =>
        {
            _isRunning = false;
            _onNightFinished?.Invoke();
        });
    }

    IEnumerator Fade(float from, float to)
    {
        if (_fadeScreen == null) yield break;

        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.deltaTime;
            _fadeScreen.alpha = Mathf.Lerp(from, to, elapsed / FadeDuration);
            yield return null;
        }
        _fadeScreen.alpha = to;
    }
}
}
