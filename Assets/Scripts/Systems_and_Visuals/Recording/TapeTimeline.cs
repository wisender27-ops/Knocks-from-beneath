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
        Glitch = 6,
        Slam = 7,
        Bars = 8
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

    public readonly struct TapeShot
    {
        public readonly float Start;
        public readonly int Clock; // секунды от полуночи в начале куска
        public TapeShot(float start, int clock) { Start = start; Clock = clock; }
    }

    public readonly struct TapeKey
    {
        public readonly float Time;
        public readonly Vector3 Position;
        public readonly bool Stairs;
        public TapeKey(float time, float x, float y, float z, bool stairs = false)
        {
            Time = time;
            Position = new Vector3(x, y, z);
            Stairs = stairs;
        }
    }

    // Сценарий плёнки как чистая функция времени: куски с часами камеры, пути фигуры, склейки, звуки.
    // Перемотка и стоп-кадр просто спрашивают позу в любой момент, ничего не накапливается.
    public sealed class TapeTimeline
    {
        public const float StepLength = 0.62f;

        private readonly TapeShot[] shots;
        private readonly TapeKey[][] walks;
        private readonly float[][] keyDistances;
        private readonly float[] cuts;
        private readonly TapeEvent[] events;
        private readonly float lookIn, lookFull, lookHold, lookOut;

        public float Duration { get; }
        public IReadOnlyList<TapeEvent> SoundEvents => events;
        public float FirstSighting => walks[0][0].Time;

        // scripted — звуки вне путей фигуры; голова в объектив — окно lookIn…lookOut.
        public TapeTimeline(float duration, TapeShot[] shots, TapeKey[][] walks, float[] cuts, List<TapeEvent> scripted,
            float lookIn, float lookFull, float lookHold, float lookOut)
        {
            Duration = duration;
            this.shots = shots;
            this.walks = walks;
            this.cuts = cuts;
            this.lookIn = lookIn;
            this.lookFull = lookFull;
            this.lookHold = lookHold;
            this.lookOut = lookOut;
            keyDistances = new float[walks.Length][];
            for (int w = 0; w < walks.Length; w++)
            {
                TapeKey[] keys = walks[w];
                keyDistances[w] = new float[keys.Length];
                for (int i = 1; i < keys.Length; i++)
                    keyDistances[w][i] = keyDistances[w][i - 1] + Vector3.Distance(keys[i - 1].Position, keys[i].Position);
            }
            events = BuildEvents(scripted);
        }

        private TapeEvent[] BuildEvents(List<TapeEvent> scripted)
        {
            var list = new List<TapeEvent>(scripted);
            foreach (float cut in cuts)
                list.Add(new TapeEvent(cut - 0.35f, TapeSound.Glitch, Vector3.zero, 0.8f));
            list.Add(new TapeEvent(Duration - 1.1f, TapeSound.Glitch, Vector3.zero, 0.9f));

            for (int w = 0; w < walks.Length; w++)
            {
                TapeKey[] keys = walks[w];
                float[] dist = keyDistances[w];
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
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return list.ToArray();
        }

        public float Clamp(float time) => Mathf.Clamp(time, 0f, Duration);

        public int ShotIndex(float time)
        {
            int shot = 0;
            for (int i = 1; i < shots.Length; i++)
                if (time >= shots[i].Start) shot = i;
            return shot;
        }

        // Часы камеры в кадре: секунды от полуночи (после полуночи счёт идёт заново).
        public int ClockSeconds(float time)
        {
            time = Clamp(time);
            TapeShot shot = shots[ShotIndex(time)];
            return (shot.Clock + Mathf.FloorToInt(time - shot.Start)) % (24 * 3600);
        }

        // Отпечаток времени, как его впечатывала камера: «PM 11:41:07», «AM  2:46:31».
        public string Stamp(float time)
        {
            int clock = ClockSeconds(time);
            int hours = clock / 3600, minutes = clock / 60 % 60, seconds = clock % 60;
            int h12 = hours % 12 == 0 ? 12 : hours % 12;
            return string.Format("{0} {1,2}:{2:00}:{3:00}", hours < 12 ? "AM" : "PM", h12, minutes, seconds);
        }

        // Потеря сигнала: «снег» в начале, на склейках и в конце плёнки.
        public float SignalLoss(float time)
        {
            float loss = 1f - Mathf.Clamp01(time / 0.6f);
            foreach (float cut in cuts)
                loss = Mathf.Max(loss, 1f - Mathf.Clamp01(Mathf.Abs(time - cut) / 0.4f));
            return Mathf.Max(loss, Mathf.Clamp01((time - (Duration - 1.2f)) / 0.8f));
        }

        // Ошибка трекинга: фоновая, сильнее у склеек и пока оно смотрит в объектив.
        public float Tracking(float time)
        {
            float tracking = 0.08f;
            foreach (float cut in cuts)
                tracking = Mathf.Max(tracking, 0.9f - Mathf.Abs(time - cut) * 0.9f);
            tracking = Mathf.Max(tracking, 0.3f * Window(time, lookIn, lookFull, lookHold, lookOut));
            return Mathf.Clamp01(tracking);
        }

        public TapeFigurePose Figure(float time)
        {
            var pose = new TapeFigurePose();
            for (int w = 0; w < walks.Length; w++)
            {
                TapeKey[] keys = walks[w];
                if (time < keys[0].Time || time > keys[keys.Length - 1].Time) continue;
                pose.Visible = true;
                pose.Position = PathPoint(w, time, out pose.Distance);
                // Курс — по направлению хода с небольшим окном, чтобы на углах он поворачивал, а не щёлкал.
                Vector3 ahead = PathPoint(w, time + 0.3f, out _) - PathPoint(w, time - 0.3f, out _);
                ahead.y = 0f;
                pose.Yaw = ahead.sqrMagnitude > 0.0025f ? Quaternion.LookRotation(ahead).eulerAngles.y : StandYaw(w, time);
                float speed = ahead.magnitude / 0.6f;
                pose.Walk = Mathf.Clamp01(speed / 0.45f);
                pose.Look = Window(time, lookIn, lookFull, lookHold, lookOut);
                return pose;
            }
            return pose;
        }

        private Vector3 PathPoint(int walk, float time, out float distance)
        {
            TapeKey[] keys = walks[walk];
            float[] dist = keyDistances[walk];
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
        private float StandYaw(int walk, float time)
        {
            TapeKey[] keys = walks[walk];
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

        public static float Window(float time, float a, float b, float c, float d)
        {
            if (time <= a || time >= d) return 0f;
            if (time < b) return Smooth((time - a) / (b - a));
            if (time > c) return Smooth(1f - (time - c) / (d - c));
            return 1f;
        }

        public static float Smooth(float k) => k * k * (3f - 2f * k);
    }
}
