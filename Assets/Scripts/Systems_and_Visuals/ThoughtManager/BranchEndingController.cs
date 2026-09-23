using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Развилка ночи 2 (T-19/T-20/T-21) — как только монстр появляется из дыры, все доступные
// концовки открываются одновременно; какое действие игрок совершит первым (или НЕ совершит —
// T-21, таймер), то и решает исход. Путь молотка переиспользует существующий
// SetupHammerQuest/финал дословно — это не отдельная концовка "с нуля", а то, что уже было
// единственным путём до этой фичи. Путь укрытия (T-20) — HideEndingController активируется и
// разрешается полностью самостоятельно (через свои триггеры), тут только включение его зон.
public sealed class BranchEndingController
{
    private readonly Action _activateHammerPath;
    private readonly GameObject _escapeDoorTrigger;
    private readonly Action<bool> _setHidePathsActive;
    private readonly Transform _playerTransform;
    private readonly Func<IEnumerator, Coroutine> _startCoroutine;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action<bool> _lockCamera;
    private readonly Action<string> _endGame;

    private bool _resolved;

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
        _playerTransform = playerTransform;
        _startCoroutine = startCoroutine;
        _showThoughts = showThoughts;
        _lockCamera = lockCamera;
        _endGame = endGame;
    }

    public void Activate()
    {
        _resolved = false;

        _activateHammerPath?.Invoke();

        // Если дверь заперта на день 2 (EveningRoundController) — концовка "побег" недоступна.
        if (_escapeDoorTrigger != null)
            _escapeDoorTrigger.SetActive(!GameState.frontDoorLocked);

        _setHidePathsActive?.Invoke(true);
    }

    // Единая защёлка на все концовки ночи 2 (кроме молотка — у него свой закрытый триггер,
    // см. IntroSequence.TryClaimEnding для стыковки с FinaleController). Первое дошедшее до
    // конца действие забирает развилку себе и гасит остальные пути — иначе игрок, вышедший из
    // укрытия прямо у входной двери, успевал запустить две концовки подряд. Заодно единая точка,
    // где глушится MonsterTimer — раньше это делал только FinaleController, и таймер мог тикать
    // и дописывать текст поверх уже идущей концовки побега или пряток (T-21).
    public bool TryResolve()
    {
        if (_resolved) return false;
        _resolved = true;

        if (_escapeDoorTrigger != null) _escapeDoorTrigger.SetActive(false);
        _setHidePathsActive?.Invoke(false);
        MonsterTimer.Instance?.StopTimer();
        return true;
    }

    // Концовка 2: побег через входную дверь. Вызывается из EscapeDoorTrigger через
    // IntroSequence.OnEscapedThroughDoor().
    public void HandleDoorEscape()
    {
        if (!TryResolve()) return;

        // Блокируем камеру на время финальной реплики (T-21) — раньше игрок мог за это время
        // дойти до дыры и запустить конкурирующую концовку молотка поверх уже идущей.
        _lockCamera?.Invoke(true);

        _showThoughts(new string[] {
            "Выбрался.",
            "Больше я сюда не вернусь."
        }, () => _endGame("КОНЕЦ: ПОБЕГ (1/4)"));
    }

    // Концовка 4 (T-21): игрок не успел заколотить дыру за отведённое время (MonsterTimer).
    // Монстр вылезает без предупреждения — тот же приём, что и в HideEndingController.AmbushRoutine
    // (SpawnWatcher рядом с игроком), просто без стадии "прятки/поиск".
    public void HandleTimerExpired()
    {
        if (!TryResolve()) return;
        _startCoroutine(TimerExpiredRoutine());
    }

    IEnumerator TimerExpiredRoutine()
    {
        _lockCamera?.Invoke(true);

        if (MonsterWatcherManager.Instance != null && _playerTransform != null)
            MonsterWatcherManager.Instance.SpawnWatcher(_playerTransform.position, forced: true);

        yield return new WaitForSeconds(1.5f);

        _showThoughts(new string[] {
            "Не успел."
        }, () => _endGame("КОНЕЦ: НЕ УСПЕЛ (4/4)"));
    }
}
}
