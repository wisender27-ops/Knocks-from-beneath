using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class RoomRevealZoneTests
{
    private GameObject _zoneGo;
    private PlacementZone _zone;
    private GameObject _slotGo;
    private GameObject _item1;
    private GameObject _item2;
    private GameObject _box;

    private RoomRevealZone CreateZoneWithItems()
    {
        _zoneGo = new GameObject("RoomZone");
        _zone = _zoneGo.AddComponent<PlacementZone>();
        _slotGo = new GameObject("Slot1");
        _zone.slots = new List<Transform> { _slotGo.transform };

        // Left active on purpose — RoomRevealZone.Awake() is now responsible for hiding
        // them itself, an author should no longer need to pre-disable items in the scene.
        _item1 = new GameObject("HiddenItem1");
        _item2 = new GameObject("HiddenItem2");

        var reveal = _zoneGo.AddComponent<RoomRevealZone>();

        // itemsToReveal is a private [SerializeField] with no public setter (by design — an
        // author wires it in the Inspector, not from code). Set it via reflection to test
        // RevealItems()'s actual activation behavior rather than just that it doesn't throw.
        var field = typeof(RoomRevealZone).GetField("itemsToReveal", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(reveal, new[] { _item1, _item2 });

        // AddComponent doesn't reliably fire Awake() in this test runner (same reason
        // FloorLogicTests/EveningRoundControllerTests set QuestManager.Instance by hand) —
        // invoke it explicitly so both the onBoxPlaced subscription and the initial
        // HideItems() call happen exactly like they would on real scene load.
        var awake = typeof(RoomRevealZone).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        awake.Invoke(reveal, null);

        return reveal;
    }

    [TearDown]
    public void TearDown()
    {
        if (_zoneGo != null) Object.DestroyImmediate(_zoneGo);
        if (_slotGo != null) Object.DestroyImmediate(_slotGo);
        if (_item1 != null) Object.DestroyImmediate(_item1);
        if (_item2 != null) Object.DestroyImmediate(_item2);
        if (_box != null) Object.DestroyImmediate(_box);
    }

    [Test]
    public void Awake_HidesConfiguredItems_EvenIfLeftActiveInScene()
    {
        CreateZoneWithItems();

        Assert.That(_item1.activeSelf, Is.False);
        Assert.That(_item2.activeSelf, Is.False);
    }

    [Test]
    public void RevealItems_ActivatesAllConfiguredItems()
    {
        var reveal = CreateZoneWithItems();

        reveal.RevealItems();

        Assert.That(_item1.activeSelf, Is.True);
        Assert.That(_item2.activeSelf, Is.True);
    }

    [Test]
    public void RevealItems_CalledTwice_StaysIdempotent()
    {
        var reveal = CreateZoneWithItems();

        reveal.RevealItems();
        _item1.SetActive(false); // simulate something else turning it back off
        reveal.RevealItems(); // must not re-activate — _revealed guard should block this

        Assert.That(_item1.activeSelf, Is.False);
    }

    [Test]
    public void PlacingABox_TriggersRevealItemsThroughPlacementZoneEvent()
    {
        var reveal = CreateZoneWithItems();
        _box = new GameObject("Box");

        _zone.TryPlaceBox(_box);

        Assert.That(_item1.activeSelf, Is.True);
        Assert.That(_item2.activeSelf, Is.True);
    }

    [Test]
    public void PlacingABox_FiresOnBoxPlaced_WhichRoomRevealZoneListensTo()
    {
        _zoneGo = new GameObject("RoomZone");
        _zone = _zoneGo.AddComponent<PlacementZone>();
        _slotGo = new GameObject("Slot1");
        _zone.slots = new List<Transform> { _slotGo.transform };

        bool eventFired = false;
        _zone.onBoxPlaced.AddListener(() => eventFired = true);

        _box = new GameObject("Box");

        bool placed = _zone.TryPlaceBox(_box);

        Assert.That(placed, Is.True);
        Assert.That(eventFired, Is.True, "PlacementZone.onBoxPlaced must fire on successful placement — RoomRevealZone relies on this to reveal items");
    }
}
