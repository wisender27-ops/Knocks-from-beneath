using System;
using System.Collections;
using System.Collections.Generic;
using KnocksFromBeneath;
using NUnit.Framework;
using UnityEngine;

public class NightOneControllerTests
{
    [Test]
    public void MorningStartsAfterSecondSleepAndQuestWaitsForMorningThoughts()
    {
        var skyObject = new GameObject("Test sky");
        var knocks = new GameObject("Test knocks");
        bool originalFog = RenderSettings.fog;
        int daysStarted = 0;
        Action dayHandler = () => daysStarted++;
        GameEvents.OnDayStarted += dayHandler;
        try
        {
            var sky = skyObject.AddComponent<SkyboxSwitcher>();
            var routines = new Queue<IEnumerator>();
            Action thoughtsComplete = null;
            int teleports = 0;
            int finished = 0;
            var controller = new NightOneController(null, sky, knocks,
                () => teleports++, routine => { routines.Enqueue(routine); return null; },
                (lines, callback) => thoughtsComplete = callback, () => finished++);

            controller.Run();
            controller.Run();
            Assert.That(routines.Count, Is.EqualTo(1), "Repeated bed input must not overlap nights.");
            Complete(routines.Dequeue());
            Assert.That(sky.isDayTime, Is.False);
            Assert.That(daysStarted, Is.Zero);
            Assert.That(knocks.activeSelf, Is.False);

            thoughtsComplete();
            Assert.That(sky.isDayTime, Is.False, "Finishing night thoughts must not reveal daytime immediately.");
            Assert.That(finished, Is.Zero);
            Complete(routines.Dequeue());
            Assert.That(sky.isDayTime, Is.True);
            Assert.That(daysStarted, Is.EqualTo(1));
            Assert.That(teleports, Is.EqualTo(2));
            Assert.That(RenderSettings.fog, Is.EqualTo(originalFog));
            Assert.That(finished, Is.Zero, "Day-two quest waits for waking thoughts.");
            thoughtsComplete();
            Assert.That(finished, Is.EqualTo(1));
        }
        finally
        {
            GameEvents.OnDayStarted -= dayHandler;
            RenderSettings.fog = originalFog;
            UnityEngine.Object.DestroyImmediate(knocks);
            UnityEngine.Object.DestroyImmediate(skyObject);
        }
    }

    private static void Complete(IEnumerator routine)
    {
        while (routine.MoveNext())
            if (routine.Current is IEnumerator child) Complete(child);
    }
}
