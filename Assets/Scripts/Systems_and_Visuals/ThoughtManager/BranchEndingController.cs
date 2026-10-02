using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Исход взаимодействия с люком: дедлайн меняет результат, но сам не завершает игру.
public sealed class BranchEndingController
{
    private readonly Action _activateHammerPath;
    private readonly GameObject _escapeDoorTrigger;
    private readonly Action<bool> _setHidePathsActive;

    private bool _resolved;
    public bool DeadlineExpired { get; private set; }

    public BranchEndingController(
        Action activateHammerPath,
        GameObject escapeDoorTrigger,
        Action<bool> setHidePathsActive,
        Transform playerTransform,
        Func<IEnumerator, Coroutine> startCoroutine,
        Action<string[], Action> showThoughts,
        Action<bool> lockCamera,
        Action<string> endGame)
    {
        _activateHammerPath = activateHammerPath;
        _escapeDoorTrigger = escapeDoorTrigger;
        _setHidePathsActive = setHidePathsActive;
    }

    public void Activate()
    {
        _resolved = false;
        DeadlineExpired = false;

        _activateHammerPath?.Invoke();

        // Старые пути отключены; финал запускается только у люка.
        if (_escapeDoorTrigger != null)
            _escapeDoorTrigger.SetActive(false);

        _setHidePathsActive?.Invoke(false);
    }

    // Одно взаимодействие с люком может запустить только один финал.
    public bool TryResolve()
    {
        if (_resolved) return false;
        _resolved = true;

        if (_escapeDoorTrigger != null) _escapeDoorTrigger.SetActive(false);
        _setHidePathsActive?.Invoke(false);
        MonsterTimer.Instance?.StopTimer();
        return true;
    }

    // API сохранён для старых триггеров сцены.
    public void HandleDoorEscape()
    {
    }

    // Решение остаётся доступным после дедлайна, но его исход уже изменился.
    public void HandleTimerExpired()
    {
        if (!_resolved) DeadlineExpired = true;
    }
}
}
