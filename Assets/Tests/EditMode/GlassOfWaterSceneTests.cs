using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class GlassOfWaterSceneTests
{
    private static readonly Vector3 Monster = GlassOfWaterScene.MonsterSpot;

    [TestCase(-45.2f, 0.8f, 106.6f, false)]  // у раковины, со стаканом
    [TestCase(-43.4f, 0.8f, 107.6f, false)]  // проход у острова — на грани, но живой
    [TestCase(-42.6f, 0.8f, 106.2f, true)]   // подошёл к нему
    [TestCase(-42.95f, 4.1f, 105.45f, false)] // прямо над ним, на втором этаже
    public void IsTooClose(float x, float y, float z, bool expected)
    {
        Assert.That(GlassOfWaterScene.IsTooClose(new Vector3(x, y, z), Monster), Is.EqualTo(expected));
    }

    [Test]
    public void BedQuestKeepsBedInteractable()
    {
        var quests = new GameObject("Quests").AddComponent<QuestManager>();
        try
        {
            QuestManager.Instance = quests;
            quests.CreateQuest("Вернуться в кровать", 1, null, GlassOfWaterScene.BedQuestTag);
            Assert.That(new GameObject("Bed").AddComponent<BedSleepInteractable>().CanInteract, Is.True);
        }
        finally
        {
            foreach (var bed in Object.FindObjectsByType<BedSleepInteractable>()) Object.DestroyImmediate(bed.gameObject);
            Object.DestroyImmediate(quests.gameObject);
            QuestManager.Instance = null;
        }
    }
}
