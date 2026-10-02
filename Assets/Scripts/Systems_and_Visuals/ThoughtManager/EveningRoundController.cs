using System;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Завершение дня 2 у кровати. Имена API и тег сохранены для существующих вызовов.
public sealed class EveningRoundController
{
    private readonly Door _frontDoor;
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onEveningFinished;

    public EveningRoundController(
        Door frontDoor,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onEveningFinished)
    {
        _frontDoor = frontDoor;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onEveningFinished = onEveningFinished;
    }

    public void SetupEveningWalkQuest()
    {
        _createQuest("Лечь спать", 1, OnEveningFinished, "evening-walk");
    }

    // Совместимость со старыми вызовами: вечернее запирание больше недоступно.
    public bool LockFrontDoor()
    {
        return false;
    }

    // Вызывается из IntroSequence через тот же GameEvents.OnBedTriggerReached, на который
    // уже подписан IntroSequence.OnBedTriggerReached (для "go-to-bed" дня 1) — оба
    // независимо проверяют свой тег, конфликта нет.
    public void HandleBedReached()
    {
        if (QuestManager.Instance == null || !QuestManager.Instance.IsQuestActive("evening-walk")) return;
        QuestManager.Instance.AddProgress(1);
    }

    void OnEveningFinished()
    {
        _showThoughts(new string[] {
            "Всё, вымотался. Остальное завтра."
        }, _onEveningFinished);
    }
}
}
