using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class PlayerInteractionTests
{
    private GameObject _go;
    private PlayerInteraction _interaction;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("Player");
        _interaction = _go.AddComponent<PlayerInteraction>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
    }

    [Test]
    public void GetHeldObject_ReturnsNullWhenPickupNotAssigned()
    {
        Assert.That(_interaction.GetHeldObject(), Is.Null);
    }

    [Test]
    public void ReleaseHeldObject_ReturnsNullWhenPickupNotAssigned()
    {
        Assert.That(_interaction.ReleaseHeldObject(), Is.Null);
    }

    [Test]
    public void TryGrabObjectFromScript_ReturnsFalseWhenPickupNotAssigned()
    {
        var obj = new GameObject("Item");
        try
        {
            Assert.That(_interaction.TryGrabObjectFromScript(obj), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(obj);
        }
    }

    [Test]
    public void GetHeldObject_DelegatesToAssignedPickupController()
    {
        var pickupGo = new GameObject("Pickup");
        var holdPointGo = new GameObject("HoldPoint");
        var itemGo = new GameObject("Item");
        try
        {
            var pickup = pickupGo.AddComponent<PickupController>();
            pickup.holdPoint = holdPointGo.transform;
            _interaction.pickup = pickup;

            itemGo.AddComponent<Rigidbody>();
            bool grabbed = _interaction.TryGrabObjectFromScript(itemGo);

            Assert.That(grabbed, Is.True);
            Assert.That(_interaction.GetHeldObject(), Is.EqualTo(itemGo));
        }
        finally
        {
            Object.DestroyImmediate(itemGo);
            Object.DestroyImmediate(holdPointGo);
            Object.DestroyImmediate(pickupGo);
        }
    }
}
