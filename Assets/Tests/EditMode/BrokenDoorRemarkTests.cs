using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class BrokenDoorRemarkTests
{
    private static readonly Vector3 Door = new Vector3(-40.3f, 0.9f, 115.6f);

    [TestCase(0f, 0f, -2f, 0f, 0f, 1f, true)]    // в двух метрах, смотрит прямо на дверь
    [TestCase(0f, 0f, -2f, 0.5f, 0f, 1f, true)]  // чуть в сторону — всё ещё в поле зрения
    [TestCase(0f, 0f, -2f, 1f, 0f, 0f, false)]   // стоит рядом, но смотрит вбок
    [TestCase(0f, 0f, -2f, 0f, 0f, -1f, false)]  // спиной к двери
    [TestCase(0f, 0f, -5f, 0f, 0f, 1f, false)]   // смотрит, но далеко
    public void Notices(float dx, float dy, float dz, float fx, float fy, float fz, bool expected)
    {
        Vector3 eye = Door + new Vector3(dx, dy + 0.8f, dz);
        Assert.That(BrokenDoorRemark.Notices(eye, new Vector3(fx, fy - 0.3f, fz), Door), Is.EqualTo(expected));
    }
}
