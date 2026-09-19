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
    public void AddProgress_IgnoresNonPositiveAmount()
    {
        _manager.CreateQuest("Найти молоток", 2, questTag: "hammer-find");
        _manager.AddProgress(0);
        _manager.AddProgress(-1);

        Assert.That(_manager.questList[0].currentAmount, Is.EqualTo(0));
        Assert.That(_manager.currentQuestIndex, Is.EqualTo(0));
    }
}
