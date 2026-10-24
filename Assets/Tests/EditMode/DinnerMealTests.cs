using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class DinnerMealTests
{
    private GameObject _stoveObject;
    private GameObject _plate;
    private GameObject _food;
    private GameObject _quests;
    private StoveInteractable _stove;
    private DinnerMealInteractable _meal;

    [SetUp]
    public void SetUp()
    {
        _stoveObject = new GameObject("Stove");
        _stove = _stoveObject.AddComponent<StoveInteractable>();
        _plate = new GameObject("DinnerPlate");
        _food = new GameObject("PanFood");
        _stove.readyMealObject = _plate;
        _stove.cookingFoodObject = _food;
        _meal = _plate.AddComponent<DinnerMealInteractable>();
        typeof(DinnerMealInteractable).GetField("stove", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_meal, _stove);
        _quests = new GameObject("DinnerQuests");
        QuestManager.Instance = _quests.AddComponent<QuestManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_stoveObject);
        Object.DestroyImmediate(_plate);
        Object.DestroyImmediate(_food);
        Object.DestroyImmediate(_quests);
        QuestManager.Instance = null;
    }

    [Test]
    public void ReadyMeal_RequiresEatingQuest()
    {
        _stove.SetReadyForDebug();
        QuestManager.Instance.CreateQuest("Готовить", 1, questTag: "dinner-cook");
        Assert.That(_meal.CanInteract, Is.False);
        Assert.That(_stove.TryEatDinner(), Is.False);
        Assert.That(_plate.activeSelf, Is.True);

        QuestManager.Instance.CreateQuest("Съесть", 1, questTag: "dinner-eat");
        Assert.That(_meal.CanInteract, Is.True);
    }

    [Test]
    public void DebugCookRestart_HidesReadyPlateAndPanFood()
    {
        _stove.SetReadyForDebug();
        Assert.That(_stove.IsReady, Is.True);
        Assert.That(_food.activeSelf, Is.False);
        _stove.ResetForCookDebug();
        Assert.That(_stove.IsReady, Is.False);
        Assert.That(_stove.IsCooking, Is.False);
        Assert.That(_plate.activeSelf, Is.False);
        Assert.That(_food.activeSelf, Is.False);
    }
}
