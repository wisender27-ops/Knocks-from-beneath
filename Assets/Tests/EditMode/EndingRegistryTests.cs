using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using KnocksFromBeneath;

public class EndingRegistryTests
{
    // Тесты пишут в настоящие PlayerPrefs — сохраняем и возвращаем открытые концовки.
    const string SeenKey = "kfb.endings.seen";
    int? backup;

    [SetUp]
    public void SetUp()
    {
        backup = PlayerPrefs.HasKey(SeenKey) ? PlayerPrefs.GetInt(SeenKey) : (int?)null;
        PlayerPrefs.DeleteKey(SeenKey);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        if (backup.HasValue) PlayerPrefs.SetInt(SeenKey, backup.Value);
        PlayerPrefs.Save();
    }

    [Test]
    public void Numbers_AreSequentialAndUnique()
    {
        var ids = new HashSet<EndingId>();
        for (int i = 0; i < EndingRegistry.Count; i++)
        {
            EndingId id = EndingRegistry.Endings[i].Id;
            Assert.That(ids.Add(id), Is.True, "Повтор id " + id);
            Assert.That(EndingRegistry.Number(id), Is.EqualTo(i + 1));
        }
        Assert.That(EndingRegistry.Count, Is.EqualTo(System.Enum.GetValues(typeof(EndingId)).Length));
    }

    [Test]
    public void ScreenTitle_HasNameAndNumberFromRegistry()
    {
        Assert.That(EndingRegistry.ScreenTitle(EndingId.Hidden), Is.EqualTo("КОНЕЦ: ТЫ ОСТАЛСЯ ЗДЕСЬ (3/5)"));
        Assert.That(EndingRegistry.ScreenTitle(EndingId.Boarded), Does.StartWith("КОНЕЦ").And.EndsWith("(1/5)"));
    }

    [Test]
    public void Seen_IsRememberedPerEnding()
    {
        Assert.That(EndingRegistry.SeenCount, Is.EqualTo(0));
        EndingRegistry.MarkSeen(EndingId.Pushed);
        EndingRegistry.MarkSeen(EndingId.Pushed);
        EndingRegistry.MarkSeen(EndingId.Answer);

        Assert.That(EndingRegistry.IsSeen(EndingId.Pushed), Is.True);
        Assert.That(EndingRegistry.IsSeen(EndingId.Answer), Is.True);
        Assert.That(EndingRegistry.IsSeen(EndingId.Boarded), Is.False);
        Assert.That(EndingRegistry.SeenCount, Is.EqualTo(2));
    }
}
