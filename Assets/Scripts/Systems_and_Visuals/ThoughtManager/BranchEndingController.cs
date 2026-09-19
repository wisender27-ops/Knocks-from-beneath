using System;
using UnityEngine;

namespace KnocksFromBeneath
{

// Развилка ночи 2 (T-19/T-20) — как только монстр появляется из дыры, все доступные
// концовки открываются одновременно; какое действие игрок совершит первым, то и решает
// исход. Путь молотка переиспользует существующий SetupHammerQuest/финал дословно —
// это не отдельная концовка "с нуля", а то, что уже было единственным путём до этой фичи.
// Путь укрытия (T-20) — HideEndingController активируется и разрешается полностью
// самостоятельно (через свои триггеры), тут только включение его зон.
public sealed class BranchEndingController
{
    private readonly Action _activateHammerPath;
    private readonly GameObject _escapeDoorTrigger;
    private readonly Action _activateHidePaths;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _endGame;

    private bool _resolved;

    public BranchEndingController(
        Action activateHammerPath,
        GameObject escapeDoorTrigger,
        Action activateHidePaths,
        Action<string[], Action> showThoughts,
        Action endGame)
    {
        _activateHammerPath = activateHammerPath;
        _escapeDoorTrigger = escapeDoorTrigger;
        _activateHidePaths = activateHidePaths;
        _showThoughts = showThoughts;
        _endGame = endGame;
    }

    public void Activate()
    {
        _resolved = false;

        _activateHammerPath?.Invoke();

        // Если дверь заперта на день 2 (EveningRoundController) — концовка "побег" недоступна.
        if (_escapeDoorTrigger != null)
            _escapeDoorTrigger.SetActive(!GameState.frontDoorLocked);

        _activateHidePaths?.Invoke();
    }

    // Концовка 2: побег через входную дверь. Вызывается из EscapeDoorTrigger через
    // IntroSequence.OnEscapedThroughDoor().
    public void HandleDoorEscape()
    {
        if (_resolved) return;
        _resolved = true;

        if (_escapeDoorTrigger != null) _escapeDoorTrigger.SetActive(false);

        _showThoughts(new string[] {
            "Выбрался.",
            "Больше я сюда не вернусь."
        }, _endGame);
    }
}
}
