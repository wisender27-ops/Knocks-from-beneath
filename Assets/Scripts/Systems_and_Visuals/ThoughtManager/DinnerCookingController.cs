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
        _showThoughts(new string[] {
            "Хватит. Пора готовить."
        }, null);
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
