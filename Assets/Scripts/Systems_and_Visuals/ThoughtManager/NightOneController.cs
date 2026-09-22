using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Первая, спокойная ночь (двухночная структура, день 1 -> ночь 1 -> день 2 -> ночь 2).
// В отличие от полной ночной цепочки (расследование -> фонарик -> лом -> пол -> дыра),
// тут только fade в ночь, пара стуков под полом и fade обратно в день — без опасности.
// Не MonoBehaviour, как MoveInChoresController/PieQuestController (T-08) — получает
// ссылки и делегаты (включая способ запускать корутины) от IntroSequence при создании.
public sealed class NightOneController
{
    private readonly CanvasGroup _fadeScreen;
    private readonly SkyboxSwitcher _skySwitcher;
    private readonly GameObject _knockController;
    private readonly Action _teleportToBed;
    private readonly Func<IEnumerator, Coroutine> _startCoroutine;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onNightFinished;

    private bool _fogBeforeNight;

    private const float FadeDuration = 1.5f;
    private const float KnockWindowSeconds = 6f; // окно активности RandomKnock — при его min/maxDelay (2-5с) даёт максимум пару стуков

    public NightOneController(
        CanvasGroup fadeScreen,
        SkyboxSwitcher skySwitcher,
        GameObject knockController,
        Action teleportToBed,
        Func<IEnumerator, Coroutine> startCoroutine,
        Action<string[], Action> showThoughts,
        Action onNightFinished)
    {
        _fadeScreen = fadeScreen;
        _skySwitcher = skySwitcher;
        _knockController = knockController;
        _teleportToBed = teleportToBed;
        _startCoroutine = startCoroutine;
        _showThoughts = showThoughts;
        _onNightFinished = onNightFinished;
    }

    public void Run()
    {
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

        if (_knockController != null) _knockController.SetActive(true);
        yield return new WaitForSeconds(KnockWindowSeconds);
        if (_knockController != null) _knockController.SetActive(false);

        yield return Fade(1f, 0f);

        _showThoughts(new string[] {
            "Тихо.",
            "Показалось, наверное."
        }, OnCalmNightFinished);
    }

    void OnCalmNightFinished()
    {
        if (_skySwitcher != null) _skySwitcher.isDayTime = true;
        RenderSettings.fog = _fogBeforeNight;
        GameEvents.OnDayStarted?.Invoke();
        _onNightFinished?.Invoke();
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
