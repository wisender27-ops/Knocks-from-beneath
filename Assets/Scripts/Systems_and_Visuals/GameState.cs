namespace KnocksFromBeneath
{

// Небольшой флаг состояния, переживающий день/ночь, но не завязанный на конкретную сцену
// или квест — по той же логике, что синглтоны QuestManager.Instance/MonsterTimer.Instance,
// только тут ничего кроме bool хранить не нужно (T-17).
public static class GameState
{
    // Заперта ли входная дверь на день 2 (EveningRoundController.LockFrontDoor) — если да,
    // концовка "побег через дверь" на ночь 2 недоступна (BranchEndingController, T-19).
    public static bool frontDoorLocked;
}
}
