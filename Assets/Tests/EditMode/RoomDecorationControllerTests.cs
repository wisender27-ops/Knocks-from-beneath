using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using KnocksFromBeneath;

public class RoomDecorationControllerTests
{
    private sealed class Recorder
    {
        public readonly List<string> CreatedQuestTags = new List<string>();
        public readonly List<int> CreatedQuestAmounts = new List<int>();
        public bool ShowThoughtsCalled;
        public Action LastOnComplete;
        public bool DecorationFinishedCalled;
        public UnityAction LastQuestCompleteCallback;

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

        public void OnDecorationFinished() => DecorationFinishedCalled = true;
    }

    private GameObject[] _zones;
    private Recorder _recorder;

    private RoomDecorationController CreateController()
    {
        _zones = new[] { new GameObject("DecoZone1"), new GameObject("DecoZone2") };
        _recorder = new Recorder();

        return new RoomDecorationController(
            _zones, _recorder.CreateQuest, _recorder.ShowThoughts,
            _recorder.OnDecorationFinished);
    }

    [TearDown]
    public void TearDown()
    {
        if (_zones == null) return;
        foreach (var z in _zones)
            if (z != null) UnityEngine.Object.DestroyImmediate(z);
    }

    [Test]
    public void IsAtKitchen_OnlyNearKitchenOnFirstFloor()
    {
        var kitchen = new Vector3(-46.3f, 1.8f, 106.6f);
        Assert.That(RoomDecorationController.IsAtKitchen(new Vector3(-44f, 1.6f, 107f), kitchen), Is.True);
        Assert.That(RoomDecorationController.IsAtKitchen(new Vector3(-44f, 4.9f, 107f), kitchen), Is.False, "спальня над кухней");
        Assert.That(RoomDecorationController.IsAtKitchen(new Vector3(-44f, 1.6f, 116f), kitchen), Is.False, "гостиная");
    }

    [Test]
    public void IsIndoors_SeparatesHouseFromPorchAndYard()
    {
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-39.5f, 1.6f, 115f)), Is.True, "прихожая");
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-44f, 1.6f, 118f)), Is.True, "лестничное крыло");
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-39.25f, 1.6f, 118f)), Is.False, "крыльцо");
        Assert.That(NightTwoExits.IsIndoors(new Vector3(-43f, 1.6f, 103f)), Is.False, "задний двор");
    }

    [Test]
    public void SetupDecorationQuest_ActivatesZonesWithAmountMatchingZoneCount()
    {
        var controller = CreateController();

        controller.SetupDecorationQuest();

        Assert.That(_zones, Has.All.Matches<GameObject>(z => z.activeSelf));
        Assert.That(_recorder.CreatedQuestTags, Does.Contain("room-decoration"));
        Assert.That(_recorder.CreatedQuestAmounts, Does.Contain(_zones.Length));
    }

    [Test]
    public void SetupDecorationQuest_ConfiguresPlacementPreviews()
    {
        var controller = CreateController();
        var zone = _zones[0].AddComponent<PlacementZone>();
        zone.progressQuestTags = new[] { "room-decoration" };

        controller.SetupDecorationQuest();
        Assert.That(zone.AcceptsQuest("room-decoration"), Is.True);

        _recorder.LastQuestCompleteCallback.Invoke();
        Assert.That(zone.AcceptsQuest("room-decoration"), Is.False);
    }

    [Test]
    public void SetupKitchenQuest_ScattersItemsBeforePlayerReachesKitchen()
    {
        var controller = CreateController();
        var zone = _zones[0].AddComponent<PlacementZone>();
        var slotObject = new GameObject("Slot");
        slotObject.transform.SetParent(_zones[0].transform, false);
        var marker = slotObject.AddComponent<FurnitureSlot>();
        zone.slots = new List<Transform> { slotObject.transform };

        var item = new GameObject("Pan");
        item.transform.SetParent(_zones[1].transform, false);
        var pickable = item.AddComponent<PickableItem>();
        var scattered = new GameObject("Scattered_Pan");
        scattered.transform.SetParent(_zones[1].transform, false);
        scattered.transform.position = new Vector3(3f, 0.5f, -2f);
        pickable.scatteredPose = scattered.transform;
        marker.ItemForSlot = item;

        try
        {
            // Без игрока контроллер сразу уходит в уборку — поэтому проверяем, что разброс
            // произошёл, а квест «Пойти на кухню» ещё не завершён (только создан).
            controller.SetupKitchenQuest();

            Assert.That(item.transform.parent, Is.Null);
            Assert.That(item.transform.position, Is.EqualTo(scattered.transform.position));
            Assert.That(item.CompareTag("Pickable"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void ScatterItems_RunsBeforeDecorationQuestAndIsIdempotent()
    {
        var controller = CreateController();
        var zone = _zones[0].AddComponent<PlacementZone>();
        var slotObject = new GameObject("Slot");
        slotObject.transform.SetParent(_zones[0].transform, false);
        var marker = slotObject.AddComponent<FurnitureSlot>();
        zone.slots = new List<Transform> { slotObject.transform };
        var item = new GameObject("Mug");
        var pickable = item.AddComponent<PickableItem>();
        var scattered = new GameObject("Scattered_Mug");
        scattered.transform.SetParent(_zones[0].transform, false);
        scattered.transform.position = new Vector3(1f, 0f, 1f);
        pickable.scatteredPose = scattered.transform;
        marker.ItemForSlot = item;

        try
        {
            controller.ScatterItems();
            Assert.That(_recorder.CreatedQuestTags, Is.Empty, "разброс не создаёт квест");
            Assert.That(item.transform.position, Is.EqualTo(scattered.transform.position));

            // Игрок сдвинул вещь — старт уборки не должен вернуть её на место разброса.
            item.transform.position = new Vector3(5f, 0f, 5f);
            controller.SetupDecorationQuest();
            Assert.That(item.transform.position, Is.EqualTo(new Vector3(5f, 0f, 5f)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void PlacementSpot_InsideSupportCollider_IsVisible_ButWallBlocks()
    {
        var stove = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stove.transform.position = Vector3.zero;
        stove.transform.localScale = new Vector3(0.7f, 1.1f, 0.8f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(0f, 1f, -1.5f);
        wall.transform.localScale = new Vector3(3f, 3f, 0.1f);
        var buffer = new RaycastHit[16];
        try
        {
            Physics.SyncTransforms();
            Vector3 burner = new Vector3(0f, 0.45f, 0f); // ниже верха коробки плиты (0.55)
            Assert.That(PickupController.IsPlacementSpotVisible(
                new Vector3(1.2f, 1.6f, 0f), burner, ~0, buffer), Is.True, "плита под сковородой — опора");
            Assert.That(PickupController.IsPlacementSpotVisible(
                new Vector3(0f, 1.2f, -3f), burner, ~0, buffer), Is.False, "стена между игроком и меткой");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stove);
            UnityEngine.Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void QuestCompletion_DeactivatesZonesAndChainsToOnDecorationFinished()
    {
        var controller = CreateController();
        controller.SetupDecorationQuest();

        // Simulate QuestManager.AddProgress reaching amount and firing the completion
        // callback that SetupDecorationQuest registered (private OnDecorationFinished).
        _recorder.LastQuestCompleteCallback.Invoke();

        Assert.That(_zones, Has.None.Matches<GameObject>(z => z.activeSelf));
        Assert.That(_recorder.ShowThoughtsCalled, Is.True);

        _recorder.LastOnComplete?.Invoke();
        Assert.That(_recorder.DecorationFinishedCalled, Is.True);
    }
}
