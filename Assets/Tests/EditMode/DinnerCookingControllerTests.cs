using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.Events;
using KnocksFromBeneath;

public class DinnerCookingControllerTests
{
    private sealed class Recorder
    {
        public readonly List<string> CreatedQuestTags = new List<string>();
        public readonly List<int> CreatedQuestAmounts = new List<int>();
        public UnityAction LastQuestCompleteCallback;
        public bool ShowThoughtsCalled;
        public Action LastOnComplete;
        public bool DinnerFinishedCalled;

        public void CreateQuest(string title, int amount, UnityAction callback, string tag)
        {
            CreatedQuestTags.Add(tag);
            CreatedQuestAmounts.Add(amount);
            LastQuestCompleteCallback = callback;
        }

        public void ShowThoughts(string[] lines, Action onComplete)
        {
            ShowThoughtsCalled = true;
            LastOnComplete = onComplete;
        }

        public void OnDinnerFinished() => DinnerFinishedCalled = true;
    }

    private Recorder _recorder;

    private DinnerCookingController CreateController()
    {
        _recorder = new Recorder();
        return new DinnerCookingController(_recorder.CreateQuest, _recorder.ShowThoughts, _recorder.OnDinnerFinished);
    }

    [Test]
    public void SetupIngredientQuest_CreatesQuestForThreeIngredientsAndResetsCanCook()
    {
        var controller = CreateController();

        controller.SetupIngredientQuest();

        Assert.That(_recorder.CreatedQuestTags, Does.Contain("dinner-ingredients"));
        Assert.That(_recorder.CreatedQuestAmounts, Does.Contain(3));
        Assert.That(controller.CanCook, Is.False, "can't cook before ingredients are collected");
    }

    [Test]
    public void IngredientQuestCompletion_EnablesCooking()
    {
        var controller = CreateController();
        controller.SetupIngredientQuest();

        _recorder.LastQuestCompleteCallback.Invoke(); // simulates QuestManager finishing the 3/3 quest

        Assert.That(controller.CanCook, Is.True);
    }

    [Test]
    public void OnDinnerEaten_DisablesCookingAndChainsToOnDinnerFinished()
    {
        var controller = CreateController();
        controller.SetupIngredientQuest();
        _recorder.LastQuestCompleteCallback.Invoke();
        Assert.That(controller.CanCook, Is.True, "precondition: cooking should be available");

        controller.OnDinnerEaten();

        Assert.That(controller.CanCook, Is.False);
        Assert.That(_recorder.ShowThoughtsCalled, Is.True);

        _recorder.LastOnComplete?.Invoke();
        Assert.That(_recorder.DinnerFinishedCalled, Is.True);
    }
}
