using System;

namespace KnocksFromBeneath
{

// Развилка ночи 2 (B-02). Activate — после «Оно исчезло»: квест молотка, выходы закрыты. OpenExits — после пряток
// под кроватью: кроме люка (E1/E2 у самого люка, FinaleController) открыты задние двери → задний двор (E3) и
// дорога мимо люка (E4). Все концовки ночи 2 делят одну защёлку TryResolve. Дедлайн меняет только исход у люка.
public sealed class BranchEndingController
{
    private readonly Action _activateHammerPath;
    private readonly Action<bool> _setExitsOpen;
    private readonly Action<bool> _setHidePathsActive;
    private readonly Action<bool> _lockCamera;
    private readonly Action<EndingId> _endGame;

    private bool _resolved;
    public bool DeadlineExpired { get; private set; }
    public bool ExitsOpen { get; private set; }

    public BranchEndingController(
        Action activateHammerPath,
        Action<bool> setExitsOpen,
        Action<bool> setHidePathsActive,
        Action<bool> lockCamera,
        Action<EndingId> endGame)
    {
        _activateHammerPath = activateHammerPath;
        _setExitsOpen = setExitsOpen;
        _setHidePathsActive = setHidePathsActive;
        _lockCamera = lockCamera;
        _endGame = endGame;
    }

    public void Activate()
    {
        _resolved = false;
        DeadlineExpired = false;
        SetExits(false);

        _activateHammerPath?.Invoke();
        _setHidePathsActive?.Invoke(false);
    }

    // Монстр ушёл вниз после пряток — герой выбирает: заколотить люк или бежать.
    public void OpenExits()
    {
        if (_resolved) return;
        SetExits(true);
    }

    // Одно взаимодействие с люком может запустить только один финал.
    public bool TryResolve()
    {
        if (_resolved) return false;
        _resolved = true;

        SetExits(false);
        _setHidePathsActive?.Invoke(false);
        MonsterTimer.Instance?.StopTimer();
        return true;
    }

    // Вышел через заднюю дверь на задний двор — засада (сцена — B-03), E3.
    public void HandleBackyardReached() => EndThroughExit(EndingId.Hidden);

    // Прошёл мимо люка по дороге — погоня (сцена — B-03), E4.
    public void HandleRoadReached() => EndThroughExit(EndingId.Escaped);

    // Решение остаётся доступным после дедлайна, но его исход уже изменился.
    public void HandleTimerExpired()
    {
        if (!_resolved) DeadlineExpired = true;
    }

    void EndThroughExit(EndingId ending)
    {
        if (!ExitsOpen || !TryResolve()) return;
        _lockCamera?.Invoke(true);
        _endGame?.Invoke(ending);
    }

    void SetExits(bool open)
    {
        ExitsOpen = open;
        _setExitsOpen?.Invoke(open);
    }
}
}
