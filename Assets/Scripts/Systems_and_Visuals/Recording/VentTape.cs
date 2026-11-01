using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Запись камеры, которую игрок поставил в подвале днём 2: решётку вентиляции выбивают изнутри, монстр выползает из трубы
    // на четвереньках, встаёт, смотрит в объектив и идёт к камере. Он злится на того, кто его снимает: бьёт по камере,
    // штатив падает, плёнка обрывается. Ночью игрок находит камеру на полу, а решётку — выбитой (BasementChapter).
    public static class VentTape
    {
        public const float Duration = 32.5f;
        private const float Floor = BasementLayout.FloorY;
        private const float NightCut = 8f;
        public const float GrilleStart = 12f, GrilleEnd = 12.7f;
        public const float CrawlStart = 12.9f, StoodUp = 19.6f;
        private const float LookIn = 21f, LookFull = 22f, LookHold = 31f, LookOut = Duration;
        private const float WalkStart = 26.5f, WalkEnd = 28.8f;
        public const float StrikeStart = 30f;
        // Через сколько секунд после начала клипа удара рука доходит до объектива (MonsterTapeClipBuilder строит клип по этому числу).
        public const float StrikeImpact = 0.55f;
        public const float Hit = StrikeStart + StrikeImpact;
        public const float StrikeDistance = 0.62f; // монстр бьёт почти в упор: рука не достаёт дальше
        public const float CameraFall = 1.18f; // высота объектива над полом: на столько падает камера

        private static readonly Vector3 Vent = DepthsLayout.Vent;
        private static readonly Vector3 InDuct = Vent + new Vector3(0.6f, 0.4f, 0f);
        private static readonly Vector3 Lens = DepthsLayout.TapeCameraPosition;
        // Где монстр стоит при ударе: в шаге от объектива, на линии «стойка → камера».
        public static readonly Vector3 Striker = StrikerPoint();
        private static readonly Vector3 Away = AwayFromMonster();

        private static Vector3 StrikerPoint()
        {
            Vector3 from = new Vector3(-40.2f, Floor, 117.0f), lens = new Vector3(Lens.x, Floor, Lens.z);
            return lens + (from - lens).normalized * StrikeDistance;
        }

        private static Vector3 AwayFromMonster()
        {
            Vector3 away = new Vector3(Lens.x - Striker.x, 0f, Lens.z - Striker.z);
            return away.normalized;
        }

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
                    // Из трубы за решёткой: выползает через порог на пол и дальше на четвереньках.
                    new TapeKey(CrawlStart, -37.10f, Floor, 117.6f, false, true),
                    new TapeKey(14.0f, -37.55f, Floor, 117.6f, false, true),
                    new TapeKey(16.4f, -39.2f, Floor, 117.5f, false, true),
                    new TapeKey(18.2f, -40.0f, Floor, 117.2f, false, true),
                    new TapeKey(StoodUp, -40.2f, Floor, 117.0f),
                    new TapeKey(WalkStart, -40.2f, Floor, 117.0f),
                    new TapeKey(WalkEnd, Striker.x, Floor, Striker.z),
                    new TapeKey(Duration, Striker.x, Floor, Striker.z)
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
                new TapeEvent(GrilleEnd, TapeSound.Bars, Vent, 0.8f),
                // Скрежет жести под ладонями и коленями, пока ползёт.
                new TapeEvent(13.3f, TapeSound.Bars, InDuct, 0.3f),
                new TapeEvent(14.6f, TapeSound.Bars, Vent, 0.35f),
                new TapeEvent(15.5f, TapeSound.Bars, Vent, 0.25f),
                new TapeEvent(16.5f, TapeSound.Bars, Vent, 0.2f),
                // Удар по камере, штатив падает на бетон.
                new TapeEvent(Hit - 0.03f, TapeSound.Slam, new Vector3(Lens.x, Lens.y, Lens.z), 1f),
                new TapeEvent(Hit + 0.18f, TapeSound.Bars, new Vector3(Lens.x, Floor, Lens.z), 1f),
                new TapeEvent(Hit + 0.5f, TapeSound.Bars, new Vector3(Lens.x, Floor, Lens.z), 0.8f),
                new TapeEvent(Hit + 0.72f, TapeSound.Slam, new Vector3(Lens.x, Floor, Lens.z), 0.6f)
            },
            LookIn, LookFull, LookHold, LookOut).WithStrike(StrikeStart);

        // Решётка: 0 — на месте, 1 — распахнута, как её нашёл игрок.
        public static float GrilleOpen(float time) => TapeTimeline.Smooth(Mathf.InverseLerp(GrilleStart, GrilleEnd, time));

        // Объектив после удара: толчок от монстра, рывок в сторону и падение на пол вместе со штативом, на бок. Дальше — снег.
        public static TapeViewShift ViewShift(float time)
        {
            float t = time - Hit;
            if (t < 0f) return default;
            float knock = TapeTimeline.Smooth(Mathf.Clamp01(t / 0.08f));
            float fall = Mathf.Clamp01((t - 0.06f) / 0.55f);
            float landed = Mathf.Clamp01((t - 0.61f) / 0.25f);
            float height = -CameraFall * fall * fall + 0.06f * Mathf.Sin(landed * Mathf.PI);
            Vector3 position = Away * (0.25f * knock + 0.35f * fall) + Vector3.up * height;
            Vector3 euler = new Vector3(18f * knock + 40f * fall * fall, 30f * knock, 82f * fall * fall);
            return new TapeViewShift(position, euler);
        }

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
