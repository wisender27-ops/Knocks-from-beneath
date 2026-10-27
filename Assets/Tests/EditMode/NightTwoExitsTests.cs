using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class NightTwoExitsTests
{
    private static readonly Vector3 Hatch = new Vector3(-44.8f, 0.65f, 120.5f);

    [TestCase(-43.4f, 1.1f, 103.5f, true)]   // шаг за сдвижную дверь кухни
    [TestCase(-38.0f, 1.1f, 102f, true)]     // за дверью гаража
    [TestCase(-43.4f, 1.1f, 105.5f, false)]  // ещё на кухне
    [TestCase(-43.4f, 5.1f, 103.5f, false)]  // второй этаж
    [TestCase(-60f, 1.1f, 100f, false)]      // за пределами участка
    [TestCase(-44.8f, 1.1f, 121.5f, false)]  // у люка перед домом
    public void IsInBackyard(float x, float y, float z, bool expected)
    {
        Assert.That(NightTwoExits.IsInBackyard(new Vector3(x, y, z)), Is.EqualTo(expected));
    }

    [TestCase(-44.8f, 125f, false)]  // двор перед домом
    [TestCase(-44.8f, 129f, false)]  // ступил на дорогу, но до люка меньше 10 м
    [TestCase(-44.8f, 131f, true)]   // прошёл по дороге 10 м от люка
    [TestCase(-36f, 129f, true)]     // ушёл по дороге вбок
    [TestCase(-30f, 120f, false)]    // далеко от люка, но не на дороге
    public void IsOnRoad(float x, float z, bool expected)
    {
        Assert.That(NightTwoExits.IsOnRoad(new Vector3(x, 1.1f, z), Hatch), Is.EqualTo(expected));
    }
}
