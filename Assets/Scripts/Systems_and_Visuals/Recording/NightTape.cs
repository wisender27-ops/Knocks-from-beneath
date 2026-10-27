using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    public enum TapeSound
    {
        Knock = 0,
        HouseCreak = 1,
        DoorCreak = 2,
        DoorClose = 3,
        StepWood = 4,
        StepStairs = 5,
        Glitch = 6
    }

    public readonly struct TapeEvent
    {
        public readonly float Time;
        public readonly TapeSound Sound;
        public readonly Vector3 Position;
        public readonly float Volume;

        public TapeEvent(float time, TapeSound sound, Vector3 position, float volume)
        {
            Time = time;
            Sound = sound;
            Position = position;
            Volume = volume;
        }
    }

    public struct TapeFigurePose
    {
        public bool Visible;
        public Vector3 Position;
        public float Yaw;
        public float Walk;     // 0 — стоит, 1 — идёт (вес клипа шага)
        public float Distance; // пройденный путь, м — фаза шага без скольжения ступней
        public float Look;     // доворот головы в объектив, 0..1
    }

    // C-02: что видеокамера сняла в ночь 1 — сценарий как чистая функция времени плёнки.
    // Перемотка и стоп-кадр (C-03) просто спрашивают позу в любой момент, ничего не накапливается.
    // Точки — мировые координаты прихожей (штатив смотрит из гостиной на входную дверь, шкаф и лестницу).
    public static class NightTape
    {
        public const float Duration = 69f;
        public const float StepLength = 0.62f;
        private const float Floor = 0.79f;

        private readonly struct Shot
        {
            public readonly float Start;
            public readonly int Clock; // секунды от полуночи в начале куска
            public Shot(float start, int clock) { Start = start; Clock = clock; }
        }

        // Камера писала всю ночь, на плёнке — три куска (запись по движению/паузы), между ними срыв трекинга.
        private static readonly Shot[] Shots =
        {
            new Shot(0f, 23 * 3600 + 41 * 60 + 7),
            new Shot(9f, 2 * 3600 + 46 * 60 + 31),
            new Shot(50f, 4 * 3600 + 12 * 60 + 5)
        };

        private readonly struct Key
        {
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly bool Stairs;
            public Key(float time, float x, float y, float z, bool stairs = false)
            {
                Time = time;
                Position = new Vector3(x, y, z);
                Stairs = stairs;
            }
        }

        // Входная дверь → прихожая → встаёт в проёме у дивана и смотрит в объектив → мимо камеры к лестнице → наверх,
        // туда, где спит герой. Под утро — вниз и обратно на улицу. Два одинаковых ключа подряд — стоит.
        private static readonly Key[][] Walks =
        {
            new[]
            {
                new Key(21.0f, -39.25f, Floor, 117.35f),
                new Key(23.0f, -39.25f, Floor, 117.35f),
                new Key(25.0f, -39.6f, Floor, 115.8f),
                new Key(27.3f, -41.6f, Floor, 115.3f),
                new Key(28.9f, -43.0f, Floor, 114.9f),
                new Key(29.8f, -43.25f, Floor, 114.3f),
                new Key(35.5f, -43.25f, Floor, 114.3f),
                new Key(37.9f, -43.35f, Floor, 112.2f),
                new Key(39.4f, -42.3f, Floor, 111.55f),
                new Key(43.6f, -39.7f, 2.4f, 111.7f, true)
            },
            new[]
            {
                new Key(54.5f, -39.7f, 2.4f, 111.7f),
                new Key(57.0f, -42.3f, Floor, 111.55f, true),
                new Key(57.9f, -43.35f, Floor, 112.2f),
                new Key(59.4f, -43.25f, Floor, 114.3f),
                new Key(59.9f, -43.0f, Floor, 114.9f),
                new Key(60.9f, -41.6f, Floor, 115.3f),
                new Key(62.3f, -39.6f, Floor, 115.8f),
                new Key(63.4f, -39.25f, Floor, 117.35f),
                new Key(63.9f, -39.25f, Floor, 118.2f)
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
        private static readonly TapeEvent[] Events;
        private static readonly float[][] KeyDistances;

        public static IReadOnlyList<TapeEvent> SoundEvents => Events;
        public static float FirstSighting => Walks[0][0].Time;
        public static float FaceToLens => LookFull;

        static NightTape()
        {
            KeyDistances = new float[Walks.Length][];
            for (int w = 0; w < Walks.Length; w++)
            {
                Key[] keys = Walks[w];
                KeyDistances[w] = new float[keys.Length];
                for (int i = 1; i < keys.Length; i++)
                    KeyDistances[w][i] = KeyDistances[w][i - 1] + Vector3.Distance(keys[i - 1].Position, keys[i].Position);
            }
            Events = BuildEvents();
        }

        private static TapeEvent[] BuildEvents()
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
            foreach (float cut in Cuts)
                list.Add(new TapeEvent(cut - 0.35f, TapeSound.Glitch, Vector3.zero, 0.8f));
            list.Add(new TapeEvent(Duration - 1.1f, TapeSound.Glitch, Vector3.zero, 0.9f));

            for (int w = 0; w < Walks.Length; w++)
            {
                Key[] keys = Walks[w];
                float[] dist = KeyDistances[w];
                float next = StepLength * 0.5f;
                for (int i = 1; i < keys.Length; i++)
                {
                    float span = dist[i] - dist[i - 1];
                    while (span > 0.001f && next <= dist[i])
                    {
                        float k = (next - dist[i - 1]) / span;
                        float time = Mathf.Lerp(keys[i - 1].Time, keys[i].Time, k);
                        Vector3 at = Vector3.Lerp(keys[i - 1].Position, keys[i].Position, k);
                        bool stairs = keys[i].Stairs;
                        list.Add(new TapeEvent(time, stairs ? TapeSound.StepStairs : TapeSound.StepWood, at, stairs ? 0.75f : 0.6f));
                        next += stairs ? StepLength * 0.55f : StepLength;
                    }
                }
            }

            // Вне кадра: поднимается по второму пролёту, под утро спускается — только звук.
            AddStairSteps(list, 43.9f, 46.6f, new Vector3(-39.7f, 2.6f, 112.8f), new Vector3(-41.6f, 4.1f, 112.9f), 0.55f);
            AddStairSteps(list, 52.0f, 54.4f, new Vector3(-41.6f, 4.1f, 112.9f), new Vector3(-39.7f, 2.6f, 112.8f), 0.6f);
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return list.ToArray();
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

        public static float Clamp(float time) => Mathf.Clamp(time, 0f, Duration);

        public static int ShotIndex(float time)
        {
            int shot = 0;
            for (int i = 1; i < Shots.Length; i++)
                if (time >= Shots[i].Start) shot = i;
            return shot;
        }

        // Часы камеры в кадре: секунды от полуночи (после полуночи счёт идёт заново).
        public static int ClockSeconds(float time)
        {
            time = Clamp(time);
            Shot shot = Shots[ShotIndex(time)];
            return (shot.Clock + Mathf.FloorToInt(time - shot.Start)) % (24 * 3600);
        }

        // Отпечаток времени, как его впечатывала камера: «PM 11:41:07», «AM  2:46:31».
        public static string Stamp(float time)
        {
            int clock = ClockSeconds(time);
            int hours = clock / 3600, minutes = clock / 60 % 60, seconds = clock % 60;
            int h12 = hours % 12 == 0 ? 12 : hours % 12;
            return string.Format("{0} {1,2}:{2:00}:{3:00}", hours < 12 ? "AM" : "PM", h12, minutes, seconds);
        }

        // Потеря сигнала: «снег» в начале, на склейках и в конце плёнки.
        public static float SignalLoss(float time)
        {
            float loss = 1f - Mathf.Clamp01(time / 0.6f);
            foreach (float cut in Cuts)
                loss = Mathf.Max(loss, 1f - Mathf.Clamp01(Mathf.Abs(time - cut) / 0.4f));
            return Mathf.Max(loss, Mathf.Clamp01((time - (Duration - 1.2f)) / 0.8f));
        }

        // Ошибка трекинга: фоновая, сильнее у склеек и пока оно смотрит в объектив.
        public static float Tracking(float time)
        {
            float tracking = 0.08f;
            foreach (float cut in Cuts)
                tracking = Mathf.Max(tracking, 0.9f - Mathf.Abs(time - cut) * 0.9f);
            tracking = Mathf.Max(tracking, 0.3f * Window(time, LookIn, LookFull, LookHold, LookOut));
            return Mathf.Clamp01(tracking);
        }

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

        public static TapeFigurePose Figure(float time)
        {
            var pose = new TapeFigurePose();
            for (int w = 0; w < Walks.Length; w++)
            {
                Key[] keys = Walks[w];
                if (time < keys[0].Time || time > keys[keys.Length - 1].Time) continue;
                pose.Visible = true;
                pose.Position = PathPoint(w, time, out pose.Distance);
                // Курс — по направлению хода с небольшим окном, чтобы на углах он поворачивал, а не щёлкал.
                Vector3 ahead = PathPoint(w, time + 0.3f, out _) - PathPoint(w, time - 0.3f, out _);
                ahead.y = 0f;
                pose.Yaw = ahead.sqrMagnitude > 0.0025f ? Quaternion.LookRotation(ahead).eulerAngles.y : StandYaw(w, time);
                float speed = ahead.magnitude / 0.6f;
                pose.Walk = Mathf.Clamp01(speed / 0.45f);
                pose.Look = Window(time, LookIn, LookFull, LookHold, LookOut);
                return pose;
            }
            return pose;
        }

        private static Vector3 PathPoint(int walk, float time, out float distance)
        {
            Key[] keys = Walks[walk];
            float[] dist = KeyDistances[walk];
            if (time <= keys[0].Time) { distance = 0f; return keys[0].Position; }
            for (int i = 1; i < keys.Length; i++)
            {
                if (time > keys[i].Time) continue;
                float k = Mathf.InverseLerp(keys[i - 1].Time, keys[i].Time, time);
                distance = Mathf.Lerp(dist[i - 1], dist[i], k);
                return Vector3.Lerp(keys[i - 1].Position, keys[i].Position, k);
            }
            distance = dist[dist.Length - 1];
            return keys[keys.Length - 1].Position;
        }

        // Стоя смотрит туда же, куда шёл; в самом начале — туда, куда пойдёт.
        private static float StandYaw(int walk, float time)
        {
            Key[] keys = Walks[walk];
            int last = 0;
            for (int i = 1; i < keys.Length && keys[i].Time <= time; i++) last = i;
            for (int i = last; i > 0; i--)
                if (Flat(keys[i].Position - keys[i - 1].Position, out float yaw)) return yaw;
            for (int i = last + 1; i < keys.Length; i++)
                if (Flat(keys[i].Position - keys[i - 1].Position, out float yaw)) return yaw;
            return 0f;
        }

        private static bool Flat(Vector3 delta, out float yaw)
        {
            delta.y = 0f;
            yaw = delta.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(delta).eulerAngles.y : 0f;
            return delta.sqrMagnitude > 0.0001f;
        }

        private static float Window(float time, float a, float b, float c, float d)
        {
            if (time <= a || time >= d) return 0f;
            if (time < b) return Smooth((time - a) / (b - a));
            if (time > c) return Smooth(1f - (time - c) / (d - c));
            return 1f;
        }

        private static float Smooth(float k) => k * k * (3f - 2f * k);
    }
}
