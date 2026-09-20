using System;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Вечерний обход дома перед второй ночью (T-17) — последний кусок дня 2. Один квест,
// две независимые точки завершения (как trash-delivery/box-delivery — не новый паттерн):
// запереть дверь ИЛИ просто дойти до кровати. Не знает про готовку/декор/соседа — только
// про свой квест и дверь.
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
        _createQuest("Обойти дом перед сном: запереть дверь или просто лечь спать", 1, OnEveningFinished, "evening-walk");
    }

    // Вызывается из FrontDoorLockInteractable через IntroSequence.OnFrontDoorLocked().
    // Возвращает true, если дверь действительно заперли — щеколде это нужно, чтобы
    // не уезжать в закрытое положение, когда запирать ещё рано.
    public bool LockFrontDoor()
    {
        if (QuestManager.Instance == null || !QuestManager.Instance.IsQuestActive("evening-walk")) return false;
        if (GameState.frontDoorLocked) return false;

        GameState.frontDoorLocked = true;
        if (_frontDoor != null) _frontDoor.SetInteractionLocked(true);
        QuestManager.Instance.AddProgress(1);
        return true;
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
            "Пора спать."
        }, _onEveningFinished);
    }
}
}
