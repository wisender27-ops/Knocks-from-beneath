using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class PickupControllerTests
{
    private static object InvokePrivate(object target, string method)
    {
        return target.GetType().GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);
    }

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
    public void LargeObject_IsCarriedWithoutParentingOrKinematics()
    {
        _itemGo.AddComponent<BoxCollider>();
        _itemGo.transform.SetPositionAndRotation(new Vector3(2f, 1f, 3f), Quaternion.Euler(20f, 65f, 10f));
        _itemGo.transform.localScale = new Vector3(0.4f, 0.6f, 0.3f);
        Physics.SyncTransforms();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_pickup.IsHeldObjectTwoHanded, Is.True);
        Assert.That(_itemGo.transform.parent, Is.Null);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.False);
    }

    [Test]
    public void SmallObject_UsesBothHands()
    {
        _itemGo.AddComponent<SphereCollider>().radius = 0.05f;
        Physics.SyncTransforms();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_pickup.IsHeldObjectTwoHanded, Is.True);
    }

    [Test]
    public void KitchenRelease_RestoresDisabledColliders()
    {
        _itemGo.AddComponent<PickableItem>().placementQuestTag = "room-decoration";
        var collider = _itemGo.AddComponent<BoxCollider>();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(collider.enabled, Is.False);
        _pickup.Release();
        Assert.That(collider.enabled, Is.True);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.False);
    }

    [Test]
    public void ThrowShake_UsesNormalizedCameraPoseInsteadOfStartupHeight()
    {
        _cameraGo.transform.localPosition = Vector3.up * 2.24f;
        InvokePrivate(_pickup, "Start");
        _cameraGo.transform.localPosition = Vector3.zero;
        var effect = (System.Collections.IEnumerator)InvokePrivate(_pickup, "ShakeAndKick");
        Assert.That(effect.MoveNext(), Is.True);
        Assert.That(_cameraGo.transform.localPosition.magnitude, Is.LessThanOrEqualTo(_pickup.shakeIntensity * 1.42f));
        InvokePrivate(_pickup, "ResetCameraShake");
        Assert.That(_cameraGo.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void ThrowShake_RepeatedInterruptionRestoresPoseWithoutDrift()
    {
        InvokePrivate(_pickup, "Start");
        Vector3 pose = new Vector3(0.02f, -0.1f, 0.03f);
        _cameraGo.transform.localPosition = pose;
        for (int i = 0; i < 20; i++)
        {
            var effect = (System.Collections.IEnumerator)InvokePrivate(_pickup, "ShakeAndKick");
            Assert.That(effect.MoveNext(), Is.True);
            InvokePrivate(_pickup, "ResetCameraShake");
            Assert.That(_cameraGo.transform.localPosition, Is.EqualTo(pose));
        }
    }

    [Test]
    public void DisableWithoutShake_PreservesCurrentCameraPose()
    {
        _cameraGo.transform.localPosition = Vector3.up * 2.24f;
        InvokePrivate(_pickup, "Start");
        Vector3 pose = new Vector3(0.02f, 0f, 0.03f);
        _cameraGo.transform.localPosition = pose;
        InvokePrivate(_pickup, "OnDisable");
        Assert.That(_cameraGo.transform.localPosition, Is.EqualTo(pose));
    }

    [Test]
    public void ColdPie_UsesPhysicalCarry()
    {
        _itemGo.AddComponent<PieQuestItem>();
        var collider = _itemGo.AddComponent<BoxCollider>();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_itemGo.transform.parent, Is.Null);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.False);
        Assert.That(collider.enabled, Is.True);
    }

    [Test]
    public void HeatedPie_UsesViewPoseAndRestoresPhysicsOnRelease()
    {
        _itemGo.AddComponent<PieQuestItem>().isHeated = true;
        var collider = _itemGo.AddComponent<BoxCollider>();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_itemGo.transform.parent, Is.EqualTo(_cameraGo.transform));
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.True);
        Assert.That(collider.enabled, Is.False);
        _pickup.Release();
        Assert.That(_itemGo.transform.parent, Is.Null);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.False);
        Assert.That(collider.enabled, Is.True);
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

    // Регресс: игрок стоял в комнатной зоне с коробкой в руках, жал E — и коробка просто
    // падала. PickableItem.activeZone ставится по OnTriggerEnter и обнуляется любым
    // OnTriggerExit, так что по дороге ссылка терялась. Теперь зона ищется ещё и напрямую
    // в момент нажатия.
    [Test]
    public void HandleInteractPressed_PlacesItem_EvenWhenActiveZoneWasNeverSet()
    {
        var zoneGo = new GameObject("Zone");
        var slotGo = new GameObject("Slot");
        var questGo = new GameObject("QuestManager");
        try
        {
            zoneGo.transform.position = new Vector3(100f, 0f, 100f);
            var trigger = zoneGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = Vector3.one * 3f;

            var zone = zoneGo.AddComponent<PlacementZone>();
            slotGo.transform.position = new Vector3(100f, 0f, 100f);
            zone.slots = new System.Collections.Generic.List<Transform> { slotGo.transform };
            zone.progressQuestTags = new string[0];

            QuestManager.Instance = questGo.AddComponent<QuestManager>();

            _itemGo.AddComponent<PickableItem>(); // activeZone намеренно остаётся null
            _itemGo.transform.position = new Vector3(100f, 0f, 100f);
            _holdPointGo.transform.position = _itemGo.transform.position;
            Physics.SyncTransforms();

            Assert.That(_pickup.TryGrab(_itemGo), Is.True);
            _pickup.HandleInteractPressed();

            Assert.That(_pickup.GetHeldObject(), Is.Null, "предмет должен уйти в зону, а не остаться в руках");
            Assert.That(_itemGo.transform.position, Is.EqualTo(slotGo.transform.position));
        }
        finally
        {
            QuestManager.Instance = null;
            Object.DestroyImmediate(questGo);
            Object.DestroyImmediate(slotGo);
            Object.DestroyImmediate(zoneGo);
        }
    }
}
