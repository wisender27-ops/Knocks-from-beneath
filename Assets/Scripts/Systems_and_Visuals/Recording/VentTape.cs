using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Запись камеры, которую игрок поставил в подвале днём 2. Сделана как скример: монстр почти не в кадре, страх держат паузы.
    // Глухие удары в трубе, долгая тишина. Сбой записи, часы прыгают вперёд — и решётка уже открыта, как она открылась, на плёнке нет.
    // Тишина. На долю секунды у трубы стоит фигура и смотрит в объектив; сбой — её нет. Снова тишина, и он сразу в упор: бьёт по камере,
    // штатив падает, плёнка обрывается. Ночью игрок находит камеру на полу, а решётку — выбитой (BasementChapter).
    public static class VentTape
    {
        public const float Duration = 24f;
        private const float Floor = BasementLayout.FloorY;
        private const float NightCut = 8f;
        // Пропавшее время: решётка меняется под снегом, игрок видит только результат.
        private const float GapCut = 15f;
        public const float GrilleStart = GapCut - 0.1f, GrilleEnd = GapCut;
        public const float StandStart = 18.3f, StandEnd = 18.75f; // единственный спокойный взгляд на него
        public const float StrikeStart = 21.6f;
        // Через сколько секунд после начала клипа удара рука доходит до объектива (MonsterTapeClipBuilder строит клип по этому числу).
        public const float StrikeImpact = 0.55f;
        public const float Hit = StrikeStart + StrikeImpact;
        public const float StrikeDistance = 0.62f; // монстр бьёт почти в упор: рука не достаёт дальше
        public const float CameraFall = 1.18f; // высота объектива над полом: на столько падает камера
        private const float RushTime = 0.32f, RushDistance = 2.4f;
        public const float Gone = Hit + 0.75f; // камера уже на боку, в кадре пусто

        private static readonly Vector3 Vent = DepthsLayout.Vent;
        private static readonly Vector3 InDuct = Vent + new Vector3(0.6f, 0f, 0f);
        private static readonly Vector3 Lens = DepthsLayout.TapeCameraPosition;
        // Где монстр стоит при ударе: в шаге от объектива, на линии «стойка → камера».
        public static readonly Vector3 Striker = StrikerPoint(StrikeDistance);
        private static readonly Vector3 RushFrom = StrikerPoint(RushDistance);
        // Стоит под коробом и смотрит на камеру; второй ключ на пару сантиметров ближе, чтобы задать поворот к объективу.
        private static readonly Vector3 Stand = new Vector3(-41.25f, Floor, 117.3f);
        private static readonly Vector3 StandTurn = Stand + (new Vector3(Lens.x, Floor, Lens.z) - Stand).normalized * 0.02f;
        private static readonly Vector3 Away = AwayFromMonster();

        private static Vector3 StrikerPoint(float distance)
        {
            Vector3 from = new Vector3(-41.25f, Floor, 117.0f), lens = new Vector3(Lens.x, Floor, Lens.z);
            return lens + (from - lens).normalized * distance;
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
                new TapeShot(NightCut, 1 * 3600 + 37 * 60 + 52),
                new TapeShot(GapCut, 3 * 3600 + 12 * 60 + 41)
            },
            new[]
            {
                new[]
                {
                    new TapeKey(StandStart, Stand.x, Floor, Stand.z),
                    new TapeKey(StandEnd, StandTurn.x, Floor, StandTurn.z)
                },
                new[]
                {
                    // Из ниоткуда и сразу в упор: разгона в кадре нет.
                    new TapeKey(StrikeStart, RushFrom.x, Floor, RushFrom.z),
                    new TapeKey(StrikeStart + RushTime, Striker.x, Floor, Striker.z),
                    new TapeKey(Gone, Striker.x, Floor, Striker.z)
                }
            },
            new[] { NightCut, GapCut },
            new List<TapeEvent>
            {
                new TapeEvent(3.6f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(3.96f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(4.96f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(9.4f, TapeSound.Bars, InDuct, 0.15f),
                new TapeEvent(10.6f, TapeSound.Knock, InDuct, 0.5f),
                new TapeEvent(11.1f, TapeSound.Knock, InDuct, 0.6f),
                new TapeEvent(11.6f, TapeSound.Knock, InDuct, 0.7f),
                new TapeEvent(StandStart, TapeSound.Glitch, Vector3.zero, 0.5f),
                new TapeEvent(StandEnd, TapeSound.Glitch, Vector3.zero, 0.4f),
                // Удар по камере, штатив падает на бетон.
                new TapeEvent(StrikeStart - 0.02f, TapeSound.Sting, Lens, 1f),
                new TapeEvent(Hit - 0.03f, TapeSound.Slam, new Vector3(Lens.x, Lens.y, Lens.z), 1f),
                new TapeEvent(Hit + 0.18f, TapeSound.Bars, new Vector3(Lens.x, Floor, Lens.z), 1f),
                new TapeEvent(Hit + 0.5f, TapeSound.Bars, new Vector3(Lens.x, Floor, Lens.z), 0.8f),
                new TapeEvent(Hit + 0.72f, TapeSound.Slam, new Vector3(Lens.x, Floor, Lens.z), 0.6f)
            },
            StandStart, StandStart + 0.05f, Duration - 1f, Duration).WithStrike(StrikeStart).WithBlips(StandStart, StandEnd);

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
            fill.transform.position = new Vector3(-41.6f, BasementLayout.FloorY + 1.2f, 116.5f);
            fill.color = new Color(1f, 0.8f, 0.6f);
            fill.intensity = 1.6f;
            fill.range = 5f;
            fill.shadows = LightShadows.None;
            return new[] { bulb, fill };
        }
    }
}
