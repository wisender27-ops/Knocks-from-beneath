using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Запись камеры, которую игрок поставил в подвале днём 2: решётку вентиляции выбивают изнутри, монстр выползает у самого
    // пола, встаёт, смотрит в объектив и уходит к лестнице. Пока он ползёт, тело ниже пола закрывает сам пол.
    public static class VentTape
    {
        public const float Duration = 38f;
        private const float Floor = BasementLayout.FloorY;
        private const float Crawl = Floor - 1.35f; // над полом остаются голова и плечи, в проём решётки
        private const float NightCut = 8f;
        public const float GrilleStart = 12f, GrilleEnd = 12.7f;
        public const float CrawlStart = 14f, StoodUp = 19f;
        private const float LookIn = 21f, LookFull = 22f, LookHold = 26f, LookOut = 27f;

        private static readonly Vector3 Vent = DepthsLayout.Vent;
        private static readonly Vector3 InDuct = Vent + new Vector3(2f, 0.4f, 0f);

        public static readonly TapeTimeline Timeline = new TapeTimeline(Duration,
            new[]
            {
                new TapeShot(0f, 21 * 3600 + 14 * 60 + 3),
                new TapeShot(NightCut, 1 * 3600 + 37 * 60 + 52)
            },
            new[]
            {
                new[]
                {
                    new TapeKey(CrawlStart, -36.6f, Crawl, Vent.z),
                    new TapeKey(17f, -38.6f, Crawl, Vent.z),
                    new TapeKey(StoodUp, -38.9f, Floor, 117.5f),
                    new TapeKey(27f, -38.9f, Floor, 117.5f),
                    new TapeKey(29f, -39.8f, Floor, 116.9f),
                    new TapeKey(31f, -41f, Floor, 114.9f),
                    new TapeKey(32.4f, -42.9f, Floor, 114.8f),
                    new TapeKey(33.6f, -44.8f, Floor, 115.6f),
                    new TapeKey(34.4f, -44.8f, Floor, 116.6f),
                    new TapeKey(37f, -44.8f, -1.4f, 119.3f, true)
                }
            },
            new[] { NightCut },
            new List<TapeEvent>
            {
                new TapeEvent(3.6f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(3.96f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(4.96f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(9.6f, TapeSound.Bars, InDuct, 0.5f),
                new TapeEvent(10.5f, TapeSound.Bars, InDuct, 0.7f),
                new TapeEvent(11.3f, TapeSound.Bars, Vent, 0.9f),
                new TapeEvent(GrilleStart + 0.05f, TapeSound.Slam, Vent, 1f),
                new TapeEvent(GrilleEnd, TapeSound.Bars, Vent, 0.8f)
            },
            LookIn, LookFull, LookHold, LookOut);

        // Решётка: 0 — на месте, 1 — распахнута, как её нашёл игрок.
        public static float GrilleOpen(float time) => TapeTimeline.Smooth(Mathf.InverseLerp(GrilleStart, GrilleEnd, time));

        // На плёнке горит только лампочка подвала.
        public static Light[] BuildLights(Transform parent)
        {
            Light bulb = NightRecording.NewLight(parent, "Basement Bulb", LightType.Point);
            bulb.transform.position = new Vector3(-41.3f, -2.3f, 114.6f);
            bulb.color = new Color(1f, 0.74f, 0.48f);
            bulb.intensity = 4f;
            bulb.range = 10f;
            bulb.shadows = LightShadows.Soft;
            // Лампа за стойкой: без этого отсвета решётка на записи тонет в шуме.
            Light fill = NightRecording.NewLight(parent, "Vent Fill", LightType.Point);
            fill.transform.position = new Vector3(-39.2f, BasementLayout.FloorY + 1.4f, 116.6f);
            fill.color = new Color(1f, 0.8f, 0.6f);
            fill.intensity = 1.6f;
            fill.range = 5f;
            fill.shadows = LightShadows.None;
            return new[] { bulb, fill };
        }
    }
}
