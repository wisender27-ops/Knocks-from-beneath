using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class PickupControllerTests
{
    private GameObject _playerGo;
    private GameObject _cameraGo;
    private GameObject _holdPointGo;
    private GameObject _itemGo;
    private PickupController _pickup;

    [SetUp]
    public void SetUp()
    {
        _playerGo = new GameObject("Player");
        _pickup = _playerGo.AddComponent<PickupController>();

        _cameraGo = new GameObject("Camera");
        _pickup.playerCamera = _cameraGo.AddComponent<Camera>();

        _holdPointGo = new GameObject("HoldPoint");
        _pickup.holdPoint = _holdPointGo.transform;

        _itemGo = new GameObject("Item");
        _itemGo.AddComponent<Rigidbody>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_itemGo);
        Object.DestroyImmediate(_holdPointGo);
        Object.DestroyImmediate(_cameraGo);
        Object.DestroyImmediate(_playerGo);
    }

    [Test]
    public void TryGrab_FailsWithoutRigidbody()
    {
        var noRb = new GameObject("NoRb");
        try
        {
            Assert.That(_pickup.TryGrab(noRb), Is.False);
            Assert.That(_pickup.GetHeldObject(), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(noRb);
        }
    }

    [Test]
    public void TryGrab_SucceedsAndDisablesGravity()
    {
        bool grabbed = _pickup.TryGrab(_itemGo);

        Assert.That(grabbed, Is.True);
        Assert.That(_pickup.GetHeldObject(), Is.EqualTo(_itemGo));
        Assert.That(_itemGo.GetComponent<Rigidbody>().useGravity, Is.False);
    }

    [Test]
    public void TryGrab_FailsWhenAlreadyHoldingSomething()
    {
        var second = new GameObject("Second");
        second.AddComponent<Rigidbody>();
        try
        {
            _pickup.TryGrab(_itemGo);
            bool secondGrab = _pickup.TryGrab(second);

            Assert.That(secondGrab, Is.False);
            Assert.That(_pickup.GetHeldObject(), Is.EqualTo(_itemGo));
        }
        finally
        {
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void TryGrab_FailsWithoutHoldPoint()
    {
        _pickup.holdPoint = null;

        Assert.That(_pickup.TryGrab(_itemGo), Is.False);
        Assert.That(_pickup.GetHeldObject(), Is.Null);
    }

    [Test]
    public void Release_RestoresGravityAndClearsHeldObject()
    {
        _pickup.TryGrab(_itemGo);

        GameObject released = _pickup.Release();

        Assert.That(released, Is.EqualTo(_itemGo));
        Assert.That(_pickup.GetHeldObject(), Is.Null);
        Assert.That(_itemGo.GetComponent<Rigidbody>().useGravity, Is.True);
    }

    [Test]
    public void Release_ReturnsNullWhenNothingHeld()
    {
        Assert.That(_pickup.Release(), Is.Null);
    }

    [Test]
    public void ForceClearHeld_ClearsStateWithoutRestoringPhysics()
    {
        _pickup.TryGrab(_itemGo);

        _pickup.ForceClearHeld();

        Assert.That(_pickup.GetHeldObject(), Is.Null);
        // Rigidbody physics intentionally NOT restored here — caller (PlayerInteraction
        // eating a pie) is about to Destroy() the object anyway.
        Assert.That(_itemGo.GetComponent<Rigidbody>().useGravity, Is.False);
    }
}
