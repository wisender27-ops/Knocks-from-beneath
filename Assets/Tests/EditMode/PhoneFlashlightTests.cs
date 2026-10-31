using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public class PhoneFlashlightTests
{
    [Test]
    public void Threat_FlickersOnceAndHoldsBlackoutUntilItEnds()
    {
        var state = new PhoneFlashlightState();

        Assert.That(state.UpdateThreat(true), Is.True);
        Assert.That(state.UpdateThreat(true), Is.False);
        Assert.That(state.Blackout, Is.True);
        Assert.That(state.UpdateThreat(false), Is.False);
        Assert.That(state.Blackout, Is.False);
        Assert.That(state.UpdateThreat(true), Is.True);
    }

    [Test]
    public void WakeUpBlink_StartsClosedAndEndsOpen()
    {
        Assert.That(WakeUpSequence.BlinkOpacity(0f), Is.EqualTo(1f));
        Assert.That(WakeUpSequence.BlinkOpacity(1f), Is.EqualTo(0f));
        Assert.That(WakeUpSequence.BlinkOpacity(.5f), Is.GreaterThan(WakeUpSequence.BlinkOpacity(.35f)));
    }

    [Test]
    public void InventoryPhoneHasExistingModelAndCannotBeConsumed()
    {
        var database = UnityEditor.AssetDatabase.LoadAssetAtPath<InventoryDatabase>("Assets/Inventory/InventoryDatabase.asset");
        var phone = database.Find("Phone");
        Assert.That(phone, Is.Not.Null);
        Assert.That(phone.Category, Is.EqualTo(InventoryItemCategory.Item));
        Assert.That(phone.MaxStack, Is.EqualTo(1));
        Assert.That(phone.PreviewPrefab, Is.EqualTo(Resources.Load<GameObject>(PhoneFlashlight.ModelResource)));
    }

    [Test]
    public void PhoneCanBeUsedDuringDayAndNightWithoutDaybreakHidingIt()
    {
        var state = new PhoneFlashlightState();
        state.Toggle();
        Assert.That(state.IsOn, Is.True);
        state.BeginNight();
        state.Toggle();
        Assert.That(state.IsOn, Is.False);
        state.Toggle();
        state.BeginDay();
        Assert.That(state.Available, Is.True);
        Assert.That(state.IsOn, Is.True);
    }

    [Test]
    public void StoryDrainKillsPhonePermanently()
    {
        var state = new PhoneFlashlightState();
        state.BeginNight();
        state.Toggle();
        state.Drain();
        Assert.That(state.IsOn, Is.False);
        Assert.That(state.Available, Is.False);
        state.Toggle();
        Assert.That(state.IsOn, Is.False);
        state.BeginDay();
        state.BeginNight();
        state.Toggle();
        Assert.That(state.IsOn, Is.False);
    }

    [Test]
    public void PuttingPhoneAwayDoesNotDrainBattery()
    {
        var state = new PhoneFlashlightState();
        state.BeginNight();
        state.Toggle();
        state.Hide();
        Assert.That(state.IsOn, Is.False);
        Assert.That(state.Available, Is.True);
        state.Toggle();
        Assert.That(state.IsOn, Is.True);
    }

    [Test]
    public void NeighborBlackoutStartsNearDoorOnGroundFloor()
    {
        var door = new Vector3(-39.71f, 2.07f, 116.79f);
        Assert.That(PhoneFlashlight.IsNearNeighborDoor(new Vector3(-42f, 0.8f, 115f), door), Is.True);
        Assert.That(PhoneFlashlight.IsNearNeighborDoor(new Vector3(-42f, 3.9f, 113.2f), door), Is.False);
        Assert.That(PhoneFlashlight.IsNearNeighborDoor(new Vector3(-45f, 0.8f, 108f), door), Is.False);
        Assert.That(PhoneFlashlight.IsNearNeighborDoor(new Vector3(-40f, 4.1f, 116f), door), Is.False);
    }
}
