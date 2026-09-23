using System;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Готовка ужина дня 2 (T-15) — собственный маленький класс по форме PieQuestController,
// НЕ его подкласс и не общее состояние: свой квест-тег, свой объект плиты. Три шага —
// собрать продукты (тот же "collect N" паттерн, что trash-collect) -> приготовить на
// StoveInteractable -> съесть (повторный Interact() с плитой, не общий с поеданием пирога
// код в PlayerInteraction — оставляет тот код нетронутым).
public sealed class DinnerCookingController
{
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onDinnerFinished;

    public bool CanCook { get; private set; }

    public DinnerCookingController(
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onDinnerFinished)
    {
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onDinnerFinished = onDinnerFinished;
    }

    public void SetupIngredientQuest()
    {
        CanCook = false;
        _createQuest("Собрать продукты на ужин", 3, OnIngredientsCollected, "dinner-ingredients");
    }

    void OnIngredientsCollected()
    {
        CanCook = true;
        // Как и в PieQuestController, следующий шаг сразу становится квестом — иначе
        // после сбора продуктов на экране не остаётся вообще никакой подсказки (баг:
        // "квест собрать продукты выполнен, а дальше тишина, новый квест не дали").
        _createQuest("Приготовить ужин на плите", 1, null, "dinner-cook");
        _showThoughts(new string[] {
            "Хватит. Пора готовить."
        }, null);
    }

    // Вызывается из StoveInteractable через IntroSequence.OnDinnerCooked(), когда готовка
    // на плите завершилась — тот же паттерн прямого вызова следующего шага, что
    // MicrowaveInteractable использует для OnPiePlacedInMicrowave (шаг одноразовый,
    // без AddProgress).
    public void OnDinnerCooked()
    {
        _createQuest("Съесть ужин", 1, null, "dinner-eat");
    }

    // Вызывается из StoveInteractable через IntroSequence.OnDinnerEaten() —
    // тот же паттерн тонкого проброса, что CanPlacePieInMicrowave/OnPiePlacedInMicrowave.
    public void OnDinnerEaten()
    {
        CanCook = false;
        _showThoughts(new string[] {
            "Уже намного лучше."
        }, _onDinnerFinished);
    }
}
}
