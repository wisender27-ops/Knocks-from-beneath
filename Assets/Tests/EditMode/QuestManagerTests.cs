using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class QuestManagerTests
{
    private GameObject _gameObject;
    private QuestManager _manager;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject("QuestManagerTests");
        _manager = _gameObject.AddComponent<QuestManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_gameObject);
    }

    [Test]
    public void IsItemRequired_UsesQuestTagInsteadOfTitle()
    {
        _manager.CreateQuest("Текст может быть любым", 1, questTag: "hammer-find");

        Assert.That(_manager.IsItemRequired(ItemType.Hammer), Is.True);
        Assert.That(_manager.IsItemRequired(ItemType.Crowbar), Is.False);
    }

    [Test]
    public void IsItemRequired_ReturnsFalseWhenQuestIsFinished()
    {
        _manager.CreateQuest("Найти молоток", 1, questTag: "hammer-find");
        _manager.AddProgress(1);

        Assert.That(_manager.IsItemRequired(ItemType.Hammer), Is.False);
    }

    [Test]
    public void IsItemRequired_ReturnsFalseForRestoredCompletedQuest()
    {
        _manager.CreateQuest("Найти фонарик", 1, questTag: "flashlight-find");
        _manager.questList[0].currentAmount = 1;

        Assert.That(_manager.IsItemRequired(ItemType.Flashlight), Is.False);
    }

    [Test]
    public void AddProgress_IgnoresNonPositiveAmount()
    {
        _manager.CreateQuest("Найти молоток", 2, questTag: "hammer-find");
        _manager.AddProgress(0);
        _manager.AddProgress(-1);

        Assert.That(_manager.questList[0].currentAmount, Is.EqualTo(0));
        Assert.That(_manager.currentQuestIndex, Is.EqualTo(0));
    }
    [Test]
    public void History_OnlyIncludesActuallyCompletedQuests()
    {
        _manager.CreateQuest("Прерванное", 2);
        _manager.AddProgress(1);
        var superseded = _manager.GetActiveQuest();
        _manager.CreateQuest("Выполненное", 1);
        var finished = _manager.GetActiveQuest();
        _manager.AddProgress(1);

        Assert.That(superseded.IsCompleted, Is.False);
        Assert.That(finished.IsCompleted, Is.True);
        Assert.That(_manager.GetActiveQuest(), Is.Null);
    }

    [Test]
    public void CompletionCallback_CanCreateNextQuestWithoutLosingIt()
    {
        _manager.CreateQuest("Первое", 1,
            () => _manager.CreateQuest("Следующее", 2, questTag: "next"));
        _manager.AddProgress(1);

        Assert.That(_manager.questList[0].IsCompleted, Is.True);
        Assert.That(_manager.GetActiveQuest(), Is.SameAs(_manager.questList[1]));
        Assert.That(_manager.GetActiveQuestTag(), Is.EqualTo("next"));
        Assert.That(_manager.GetActiveQuest().currentAmount, Is.Zero);
        Assert.That(_manager.ToastRevision, Is.EqualTo(2));
    }

    [Test]
    public void NewQuestWithSameTitleAndTag_TriggersNewToastButProgressDoesNot()
    {
        _manager.CreateQuest("Коробки", 3, questTag: "box");
        var first = _manager.GetActiveQuest();
        int revision = _manager.ToastRevision;
        _manager.AddProgress(1);
        Assert.That(_manager.ToastRevision, Is.EqualTo(revision));

        _manager.CreateQuest("Коробки", 3, questTag: "box");
        Assert.That(_manager.GetActiveQuest(), Is.Not.SameAs(first));
        Assert.That(_manager.ToastRevision, Is.EqualTo(revision + 1));
        Assert.That(first.IsCompleted, Is.False);
    }

    [Test]
    public void QuestChanged_NotifiesProgressAndNewSameTagQuest()
    {
        int changes = 0;
        _manager.OnQuestChanged += () => changes++;
        _manager.CreateQuest("Коробки", 3, questTag: "box");
        _manager.AddProgress(1);
        _manager.CreateQuest("Коробки", 3, questTag: "box");

        Assert.That(changes, Is.EqualTo(3));
    }
    [Test]
    public void RecordQuestCompletion_CompletesWithoutInvokingStoryCallback()
    {
        int callbackCount = 0;
        _manager.CreateQuest("Пирог", 1, () => callbackCount++, "pie-take");
        var quest = _manager.GetActiveQuest();
        _manager.RecordQuestCompletion("pie-take");
        _manager.RecordQuestCompletion("pie-take");

        Assert.That(quest.IsCompleted, Is.True);
        Assert.That(_manager.GetActiveQuest(), Is.Null);
        Assert.That(callbackCount, Is.Zero);
    }

    [Test]
    public void RecordQuestCompletion_DoesNotCompleteUnrelatedActiveQuest()
    {
        _manager.CreateQuest("Коробки", 3, questTag: "box");
        var quest = _manager.GetActiveQuest();
        _manager.RecordQuestCompletion("pie-take");

        Assert.That(_manager.GetActiveQuest(), Is.SameAs(quest));
        Assert.That(quest.currentAmount, Is.Zero);
        Assert.That(quest.IsCompleted, Is.False);
    }
}
