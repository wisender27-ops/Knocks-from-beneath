using System;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Готовка ужина дня 2 (T-15) — собственный маленький класс по форме PieQuestController,
// НЕ его подкласс и не общее состояние: свой квест-тег, свой объект плиты. Три шага —
// собрать продукты (тот же "collect N" паттерн, что trash-collect) -> приготовить на
// StoveInteractable -> съесть готовое блюдо на столе через DinnerMealInteractable.
public sealed class DinnerCookingController
{
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action _onDinnerFinished;
    private readonly Action<string> _recordCompletion;

    public bool CanCook { get; private set; }

    public DinnerCookingController(
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Action onDinnerFinished,
        Action<string> recordCompletion = null)
    {
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _onDinnerFinished = onDinnerFinished;
        _recordCompletion = recordCompletion;
    }

    public void SetupIngredientQuest()
    {
        CanCook = false;
        _createQuest("Собрать продукты на ужин", 3, OnIngredientsCollected, "dinner-ingredients");
    }

    public void SetupCookDinnerDebug()
    {
        CanCook = true;
        _createQuest("Приготовить ужин на плите", 1, null, "dinner-cook");
    }

    public void SetupEatDinnerDebug()
    {
        CanCook = false;
        _createQuest("Съесть ужин", 1, null, "dinner-eat");
    }

    void OnIngredientsCollected()
    {
        CanCook = true;
        // Как и в PieQuestController, следующий шаг сразу становится квестом — иначе
        // после сбора продуктов на экране не остаётся вообще никакой подсказки (баг:
        // "квест собрать продукты выполнен, а дальше тишина, новый квест не дали").
        _createQuest("Приготовить ужин на плите", 1, null, "dinner-cook");
        _showThoughts(new string[] {
            "Всё есть. Теперь к плите."
        }, null);
    }

    // Вызывается из StoveInteractable через IntroSequence.OnDinnerCooked(), когда готовка
    // на плите завершилась — тот же паттерн прямого вызова следующего шага, что
    // MicrowaveInteractable использует для OnPiePlacedInMicrowave (шаг одноразовый,
    // без AddProgress).
    public void OnDinnerCooked()
    {
        _recordCompletion?.Invoke("dinner-cook");
        _createQuest("Съесть ужин на обеденном столе", 1, null, "dinner-eat");
        _showThoughts(new[] { "Ужин готов. Тарелка на обеденном столе." }, null);
    }

    // Вызывается с тарелки через StoveInteractable.TryEatDinner() и IntroSequence.OnDinnerEaten() —
    // тот же паттерн тонкого проброса, что CanPlacePieInMicrowave/OnPiePlacedInMicrowave.
    public void OnDinnerEaten()
    {
        _recordCompletion?.Invoke("dinner-eat");
        CanCook = false;
        _showThoughts(new string[] {
            "На сытый желудок хоть голова работает."
        }, _onDinnerFinished);
    }
}
}
