using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public class PhoneFlashlightTests
{
    [Test]
    public void PhoneIsAvailableOnlyAtNightAndTurnsOffAtDaybreak()
    {
        var state = new PhoneFlashlightState();
        state.Toggle();
        Assert.That(state.IsOn, Is.False);
        state.BeginNight();
        state.Toggle();
        Assert.That(state.IsOn, Is.True);
        state.BeginDay();
        Assert.That(state.Available, Is.False);
        Assert.That(state.IsOn, Is.False);
    }

    [Test]
    public void DrainedPhoneStaysUnavailableUntilTheNextDay()
    {
        var state = new PhoneFlashlightState();
        state.BeginNight();
        state.Toggle();
        state.Drain();
        state.BeginNight();
        state.Toggle();
        Assert.That(state.IsOn, Is.False);
        Assert.That(state.Available, Is.False);
        state.BeginDay();
        state.BeginNight();
        state.Toggle();
        Assert.That(state.IsOn, Is.True);
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
