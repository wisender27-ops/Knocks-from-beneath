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

    // U-03: предмет в руках кинематический (ставится от камеры каждый кадр), без родителя;
    // отпустил — снова обычная физика.
    [Test]
    public void LargeObject_IsCarriedKinematicWithoutParenting()
    {
        _itemGo.AddComponent<BoxCollider>();
        _itemGo.transform.SetPositionAndRotation(new Vector3(2f, 1f, 3f), Quaternion.Euler(20f, 65f, 10f));
        _itemGo.transform.localScale = new Vector3(0.4f, 0.6f, 0.3f);
        Physics.SyncTransforms();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_pickup.IsHeldObjectTwoHanded, Is.True);
        Assert.That(_itemGo.transform.parent, Is.Null);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.True);
        _pickup.Release();
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.False);
    }

    private void Follow(float deltaTime)
    {
        _pickup.GetType().GetMethod("FollowCarryPose",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(_pickup, new object[] { deltaTime });
    }

    private void TurnView(float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        _cameraGo.transform.rotation = rotation;
        _holdPointGo.transform.rotation = rotation;
    }

    // Передняя грань мешка (по глубине камеры) должна оставаться за ближней плоскостью.
    private float FrontFaceDepth(BoxCollider box)
    {
        Transform view = _cameraGo.transform;
        float nearest = float.PositiveInfinity;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = _itemGo.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z)));
            nearest = Mathf.Min(nearest, view.InverseTransformPoint(corner).z);
        }
        return nearest;
    }

    private BoxCollider GrabTrashSizedBag()
    {
        var box = _itemGo.AddComponent<BoxCollider>();
        box.size = new Vector3(.63f, .86f, .69f);
        _itemGo.GetComponent<Rigidbody>().mass = 3f;
        _itemGo.transform.position = new Vector3(0f, -1f, 1f);
        Physics.SyncTransforms();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_pickup.IsHeldObjectTwoHanded, Is.True);
        return box;
    }

    // Регресс U-03: при резком развороте мешок догонял цель по прямой — через голову.
    [Test]
    public void CarriedBag_FastTurn_StaysInFrontOfCamera()
    {
        BoxCollider box = GrabTrashSizedBag();
        float near = _pickup.playerCamera.nearClipPlane;
        for (int i = 0; i < 60; i++) Follow(.016f);
        Assert.That(FrontFaceDepth(box), Is.GreaterThan(near));
        for (int step = 1; step <= 6; step++)
        {
            TurnView(step * 30f); // 180° за 6 кадров
            Follow(.016f);
            Assert.That(FrontFaceDepth(box), Is.GreaterThan(near), $"кадр {step}: мешок зашёл за ближнюю плоскость");
        }
    }

    // Регресс U-03: на ходу мешок отставал от камеры и влезал в неё.
    [Test]
    public void CarriedBag_RunningForward_DoesNotLagIntoCamera()
    {
        BoxCollider box = GrabTrashSizedBag();
        float near = _pickup.playerCamera.nearClipPlane;
        for (int i = 0; i < 60; i++) Follow(.016f);
        for (int i = 0; i < 60; i++)
        {
            Vector3 step = Vector3.forward * (6f * .016f);
            _cameraGo.transform.position += step;
            _holdPointGo.transform.position += step;
            Follow(.016f);
            Assert.That(FrontFaceDepth(box), Is.GreaterThan(near), $"кадр {i}: мешок отстал и влез в камеру");
        }
    }

    // Регресс U-03: впереди стена — цель подтягивается к игроку, но не в саму камеру.
    [Test]
    public void CarriedBag_WallInFront_IsNotPulledThroughCamera()
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = new Vector3(0f, 0f, .45f);
            wall.transform.localScale = new Vector3(4f, 4f, .1f);
            BoxCollider box = GrabTrashSizedBag();
            Physics.SyncTransforms();
            for (int i = 0; i < 60; i++) Follow(.016f);
            Assert.That(FrontFaceDepth(box), Is.GreaterThan(_pickup.playerCamera.nearClipPlane));
        }
        finally
        {
            Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void SmallLightObject_IsNotTwoHanded()
    {
        _itemGo.AddComponent<SphereCollider>().radius = 0.05f;
        Physics.SyncTransforms();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_pickup.IsHeldObjectTwoHanded, Is.False);
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
    public void ColdPie_UsesPhysicalCarryAndRestoresCollidersOnRelease()
    {
        _itemGo.AddComponent<PieQuestItem>();
        var collider = _itemGo.AddComponent<BoxCollider>();
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(_itemGo.transform.parent, Is.Null);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.True);
        // Коллайдеры переносимого предмета выключены, чтобы он не цеплялся за стены.
        Assert.That(collider.enabled, Is.False);
        _pickup.Release();
        Assert.That(collider.enabled, Is.True);
        Assert.That(_itemGo.GetComponent<Rigidbody>().isKinematic, Is.False);
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

    // U-04: пирог у камеры без интерполяции (она перебивала локальную позу), после — исходная.
    [Test]
    public void HeatedPie_DisablesInterpolationWhileInViewAndRestoresIt()
    {
        var rb = _itemGo.GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Extrapolate;
        _itemGo.AddComponent<PieQuestItem>().isHeated = true;
        Assert.That(_pickup.TryGrab(_itemGo), Is.True);
        Assert.That(rb.interpolation, Is.EqualTo(RigidbodyInterpolation.None));
        _pickup.Release();
        Assert.That(rb.interpolation, Is.EqualTo(RigidbodyInterpolation.Extrapolate));
    }

    // U-04: плоский пирог в руках не залезает за ближнюю плоскость камеры.
    [Test]
    public void HeatedPie_NearestEdgeStaysBeyondNearClipPlane()
    {
        var mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-.15f, 0f, -.15f), new Vector3(.15f, .04f, .15f), new Vector3(.15f, 0f, -.15f) };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.RecalculateBounds();
        _itemGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var pie = _itemGo.AddComponent<PieQuestItem>();
        pie.isHeated = true;
        pie.heldLocalPosition = new Vector3(0f, -.08f, .1f);
        pie.heldLocalEulerAngles = Vector3.zero;
        _pickup.playerCamera.nearClipPlane = .1f;
        try
        {
            Assert.That(_pickup.TryGrab(_itemGo), Is.True);
            Transform t = _itemGo.transform;
            float nearest = t.localPosition.z +
                PieQuestItem.NearestViewDepthOffset(mesh.bounds, t.localRotation, t.localScale);
            Assert.That(nearest, Is.GreaterThanOrEqualTo(.1f));
            _pickup.Release();
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void NearestViewDepthOffset_AccountsForRotationAndScale()
    {
        var bounds = new Bounds(Vector3.zero, new Vector3(2f, .2f, 2f));
        // Плашмя: ближний край на половине диаметра.
        Assert.That(PieQuestItem.NearestViewDepthOffset(bounds, Quaternion.identity, Vector3.one), Is.EqualTo(-1f).Within(1e-4f));
        // Повёрнут лицом к камере: остаётся полтолщины.
        Assert.That(PieQuestItem.NearestViewDepthOffset(bounds, Quaternion.Euler(-90f, 0f, 0f), Vector3.one), Is.EqualTo(-.1f).Within(1e-4f));
        Assert.That(PieQuestItem.NearestViewDepthOffset(bounds, Quaternion.identity, Vector3.one * 2f), Is.EqualTo(-2f).Within(1e-4f));
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
