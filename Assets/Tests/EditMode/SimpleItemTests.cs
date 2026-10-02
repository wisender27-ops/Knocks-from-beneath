using KnocksFromBeneath;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class SimpleItemTests
{
    private GameObject _managerObject;
    private GameObject _itemObject;
    private QuestManager _manager;
    private SimpleItem _item;
    private BoxCollider _collider;
    private MeshRenderer _renderer;

    [SetUp]
    public void SetUp()
    {
        _managerObject = new GameObject("QuestManager");
        _manager = _managerObject.AddComponent<QuestManager>();
        QuestManager.Instance = _manager;

        _itemObject = new GameObject("Quest Item");
        _collider = _itemObject.AddComponent<BoxCollider>();
        _renderer = _itemObject.AddComponent<MeshRenderer>();
        _item = _itemObject.AddComponent<SimpleItem>();
        _item.itemType = ItemType.Hammer;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SimpleItem).GetMethod("Awake", flags).Invoke(_item, null);
        typeof(SimpleItem).GetMethod("OnEnable", flags).Invoke(_item, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_item != null)
            typeof(SimpleItem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_item, null);
        Object.DestroyImmediate(_itemObject);
        Object.DestroyImmediate(_managerObject);
        QuestManager.Instance = null;
    }

    [Test]
    public void Item_AppearsOnlyWhileRequiredQuestIsActive()
    {
        Assert.That(_item.IsAvailable, Is.False);
        Assert.That(_collider.enabled, Is.False);
        Assert.That(_renderer.enabled, Is.False);

        _manager.CreateQuest("Найти молоток", 1, questTag: "hammer-find");

        Assert.That(_item.IsAvailable, Is.True);
        Assert.That(_collider.enabled, Is.True);
        Assert.That(_renderer.enabled, Is.True);

        _manager.AddProgress(1);

        Assert.That(_item.IsAvailable, Is.False);
        Assert.That(_collider.enabled, Is.False);
        Assert.That(_renderer.enabled, Is.False);
    }
}
