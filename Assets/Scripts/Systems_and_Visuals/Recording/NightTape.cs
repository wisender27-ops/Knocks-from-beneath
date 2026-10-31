using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // C-02: что видеокамера сняла в ночь 1 — сценарий как чистая функция времени плёнки.
    // Точки — мировые координаты прихожей (штатив смотрит из гостиной на входную дверь, шкаф и лестницу).
    public static class NightTape
    {
        public const float Duration = 69f;
        public const float StepLength = TapeTimeline.StepLength;
        private const float Floor = 0.79f;

        // Камера писала всю ночь, на плёнке — три куска (запись по движению/паузы), между ними срыв трекинга.
        private static readonly TapeShot[] Shots =
        {
            new TapeShot(0f, 23 * 3600 + 41 * 60 + 7),
            new TapeShot(9f, 2 * 3600 + 46 * 60 + 31),
            new TapeShot(50f, 4 * 3600 + 12 * 60 + 5)
        };

        // Входная дверь → прихожая → встаёт в проёме у дивана и смотрит в объектив → мимо камеры к лестнице → наверх,
        // туда, где спит герой. Под утро — вниз и обратно на улицу. Два одинаковых ключа подряд — стоит.
        private static readonly TapeKey[][] Walks =
        {
            new[]
            {
                new TapeKey(21.0f, -39.25f, Floor, 117.35f),
                new TapeKey(23.0f, -39.25f, Floor, 117.35f),
                new TapeKey(25.0f, -39.6f, Floor, 115.8f),
                new TapeKey(27.3f, -41.6f, Floor, 115.3f),
                new TapeKey(28.9f, -43.0f, Floor, 114.9f),
                new TapeKey(29.8f, -43.25f, Floor, 114.3f),
                new TapeKey(35.5f, -43.25f, Floor, 114.3f),
                new TapeKey(37.9f, -43.35f, Floor, 112.2f),
                new TapeKey(39.4f, -42.3f, Floor, 111.55f),
                new TapeKey(43.6f, -39.7f, 2.4f, 111.7f, true)
            },
            new[]
            {
                new TapeKey(54.5f, -39.7f, 2.4f, 111.7f),
                new TapeKey(57.0f, -42.3f, Floor, 111.55f, true),
                new TapeKey(57.9f, -43.35f, Floor, 112.2f),
                new TapeKey(59.4f, -43.25f, Floor, 114.3f),
                new TapeKey(59.9f, -43.0f, Floor, 114.9f),
                new TapeKey(60.9f, -41.6f, Floor, 115.3f),
                new TapeKey(62.3f, -39.6f, Floor, 115.8f),
                new TapeKey(63.4f, -39.25f, Floor, 117.35f),
                new TapeKey(63.9f, -39.25f, Floor, 118.2f)
            }
        };

        // Голова — в объектив, пока стоит в проёме у дивана.
        private const float LookIn = 30.5f, LookFull = 31.7f, LookHold = 34.8f, LookOut = 35.5f;

        // Входная дверь (вне кадра — видно только полосу уличного света в коридоре): её открывают снаружи,
        // до утра она стоит открытой, потом её прикрывают. Сквозняк от неё сам медленно распахивает
        // жалюзийную дверь в центре кадра — без мистики, но со стороны камеры дверь открывается сама.
        private const float DoorOpenStart = 12f, DoorOpenEnd = 14.2f, DoorCloseStart = 64.0f, DoorCloseEnd = 65.4f;
        public const float DoorOpenAmount = 0.8f;
        private const float DraftStart = 14.4f, DraftEnd = 19.2f;
        public const float DraftOpenAmount = 0.6f;
        public static readonly Vector3 FrontDoorPoint = new Vector3(-39.3f, 1.6f, 116.8f);
        // Жалюзийная дверь между гостиной и подлестничным шкафом (closet_door_02) — по ней её находит запись.
        public static readonly Vector3 DraftDoorPoint = new Vector3(-42.0f, 1.9f, 114.24f);

        private static readonly float[] Cuts = { 9f, 50f };

        public static readonly TapeTimeline Timeline = new TapeTimeline(Duration, Shots, Walks, Cuts, ScriptedEvents(),
            LookIn, LookFull, LookHold, LookOut);

        public static IReadOnlyList<TapeEvent> SoundEvents => Timeline.SoundEvents;
        public static float FirstSighting => Timeline.FirstSighting;
        public static float FaceToLens => LookFull;

        private static List<TapeEvent> ScriptedEvents()
        {
            var list = new List<TapeEvent>
            {
                // Хозяин снизу — три глухих удара ещё до полуночи.
                new TapeEvent(3.9f, TapeSound.Knock, new Vector3(-43.9f, 0.2f, 110f), 0.5f),
                new TapeEvent(4.5f, TapeSound.Knock, new Vector3(-43.9f, 0.2f, 110f), 0.45f),
                new TapeEvent(5.8f, TapeSound.Knock, new Vector3(-43.9f, 0.2f, 110f), 0.55f),
                new TapeEvent(10.6f, TapeSound.HouseCreak, new Vector3(-41f, 3f, 112f), 0.5f),
                new TapeEvent(12.05f, TapeSound.DoorCreak, FrontDoorPoint, 0.9f),
                new TapeEvent(14.5f, TapeSound.DoorCreak, DraftDoorPoint, 0.55f),
                // Наверху: шаги по лестнице за стеной, скрип над головой.
                new TapeEvent(47.6f, TapeSound.HouseCreak, new Vector3(-41f, 4.6f, 116f), 0.55f),
                new TapeEvent(DoorCloseEnd - 0.1f, TapeSound.DoorClose, FrontDoorPoint, 0.8f)
            };
            // Вне кадра: поднимается по второму пролёту, под утро спускается — только звук.
            AddStairSteps(list, 43.9f, 46.6f, new Vector3(-39.7f, 2.6f, 112.8f), new Vector3(-41.6f, 4.1f, 112.9f), 0.55f);
            AddStairSteps(list, 52.0f, 54.4f, new Vector3(-41.6f, 4.1f, 112.9f), new Vector3(-39.7f, 2.6f, 112.8f), 0.6f);
            return list;
        }

        private static void AddStairSteps(List<TapeEvent> list, float from, float to, Vector3 a, Vector3 b, float volume)
        {
            const int Steps = 7;
            for (int i = 0; i < Steps; i++)
            {
                float k = i / (Steps - 1f);
                list.Add(new TapeEvent(Mathf.Lerp(from, to, k), TapeSound.StepStairs, Vector3.Lerp(a, b, k), volume));
            }
        }

        public static float Clamp(float time) => Timeline.Clamp(time);
        public static int ShotIndex(float time) => Timeline.ShotIndex(time);
        public static int ClockSeconds(float time) => Timeline.ClockSeconds(time);
        public static string Stamp(float time) => Timeline.Stamp(time);
        public static float SignalLoss(float time) => Timeline.SignalLoss(time);
        public static float Tracking(float time) => Timeline.Tracking(time);
        public static TapeFigurePose Figure(float time) => Timeline.Figure(time);

        // Входная дверь: 0 — закрыта, 1 — полностью открыта.
        public static float DoorOpen(float time)
        {
            if (time < DoorOpenStart) return 0f;
            float open = Smooth(Mathf.InverseLerp(DoorOpenStart, DoorOpenEnd, time));
            if (time >= DoorCloseStart) open = 1f - Smooth(Mathf.InverseLerp(DoorCloseStart, DoorCloseEnd, time));
            return open * DoorOpenAmount;
        }

        // Жалюзийная дверь: 0 — закрыта, дальше открыта сквозняком до конца плёнки.
        public static float DraftOpen(float time) =>
            time < DraftStart ? 0f : Smooth(Mathf.Sqrt(Mathf.InverseLerp(DraftStart, DraftEnd, time))) * DraftOpenAmount;

        private static float Smooth(float k) => TapeTimeline.Smooth(k);
    }
}
