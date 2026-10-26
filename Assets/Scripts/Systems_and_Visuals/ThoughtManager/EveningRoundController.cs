using System;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Завершение дня 2 у кровати. Имена API и тег сохранены для существующих вызовов.
// B-01: пока идёт вечерний квест, засов входной двери можно задвинуть (необязательно, квест не
// продвигает). Отодвинуть можно всегда — иначе ночью 2 игрок заперт в доме. Последствия для
// концовок — в B-02 (отложено до совместной сессии с автором).
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
        _showThoughts(new[] { "Может, на ночь задвинуть засов на входной двери?" }, null);
    }

    public bool CanLockFrontDoor =>
        !GameState.frontDoorLocked && QuestManager.Instance != null && QuestManager.Instance.IsQuestActive("evening-walk");

    public bool LockFrontDoor()
    {
        if (!CanLockFrontDoor) return false;
        GameState.frontDoorLocked = true;
        if (_frontDoor != null) _frontDoor.SetInteractionLocked(true);
        _showThoughts(new[] { "Вот так. Теперь хоть кто-то снаружи не войдёт." }, null);
        return true;
    }

    public bool UnlockFrontDoor()
    {
        if (!GameState.frontDoorLocked) return false;
        GameState.frontDoorLocked = false;
        if (_frontDoor != null) _frontDoor.SetInteractionLocked(false);
        return true;
    }

    // Чекпойнт главы вернул флаг «заперто» — дверь должна быть заперта и на деле.
    public void ApplyRestoredLock()
    {
        if (_frontDoor != null) _frontDoor.SetInteractionLocked(GameState.frontDoorLocked);
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
