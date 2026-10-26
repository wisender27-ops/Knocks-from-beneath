using UnityEngine;

namespace KnocksFromBeneath
{
    // Логика «микрофона игрока» в M7 (прятки под кроватью): игрок должен молчать по-настоящему.
    // Шумоподавление: первую секунду меряется фон комнаты, дальше фон подстраивается — вниз быстро, вверх медленно
    // и только пока игрок не шумит (голос не «съедает» сам себя). Лимит: тихий шёпот проходит, голос — нет.
    // Громче лимита — монстр слышит: чем громче и чем ближе он, тем быстрее растёт подозрение; дошло до 1 — нашёл.
    public sealed class VoiceNoiseGate
    {
        public const float SilenceDb = -80f;
        public const float CalibrationSeconds = 1f;
        public const float Margin = 15f;           // насколько громче фона комнаты можно (тихий шёпот)
        public const float LimitFloorDb = -38f;    // в тихой комнате лимит не опускается ниже этого
        public const float HearingRadius = 8f;     // голос слышно по всему этажу; дальше — всё равно слышно, медленнее
        public const float Headroom = 1.5f;        // шкала показывает до полутора лимитов

        public float NoiseFloor { get; private set; } = -60f;
        public float Level { get; private set; } = SilenceDb;
        public float Limit => Mathf.Max(NoiseFloor + Margin, LimitFloorDb);
        public bool Calibrated => calibrated >= CalibrationSeconds;
        public bool Loud { get; private set; }
        public float Suspicion { get; private set; }
        public float Pulse { get; private set; }
        public bool Found { get; private set; }

        private float calibrated, calibrationSum;

        // 0 — тишина (фон), 1 — ровно лимит, выше — громче лимита (до Headroom).
        public float Loudness
        {
            get
            {
                float span = Mathf.Max(1f, Limit - NoiseFloor);
                return Mathf.Clamp((Level - NoiseFloor) / span, 0f, Headroom);
            }
        }

        // Только слушать (игрок ещё не спрятался): калибровка и подстройка фона, без подозрения.
        public void Observe(float dt, float levelDb)
        {
            if (dt <= 0f) return;
            Level = Mathf.Max(SilenceDb, levelDb);
            if (!Calibrated)
            {
                calibrationSum += Level * dt;
                calibrated += dt;
                NoiseFloor = Mathf.Clamp(calibrationSum / calibrated, SilenceDb, -10f);
                Loud = false;
                return;
            }
            Loud = Level > Limit;
            if (Level < NoiseFloor) NoiseFloor = Mathf.MoveTowards(NoiseFloor, Level, 12f * dt);
            else if (!Loud) NoiseFloor = Mathf.MoveTowards(NoiseFloor, Level, 1f * dt);
            NoiseFloor = Mathf.Max(SilenceDb, NoiseFloor);
        }

        // true — в этот кадр игрок громче лимита.
        public bool Advance(float dt, float levelDb, float monsterDistance)
        {
            if (Found || dt <= 0f) return false;
            float limit = Limit;
            Observe(dt, levelDb);
            if (!Calibrated) return false;

            float closeness = Closeness(monsterDistance);
            if (Loud)
            {
                // Обычный голос рядом выдаёт за доли секунды, издалека (внизу, на лестнице) — за секунду-другую.
                float over = Level - limit;
                Suspicion += (1f + over / 6f) * Mathf.Lerp(1.2f, 3f, closeness) * dt;
            }
            else Suspicion = Mathf.Max(0f, Suspicion - 0.08f * dt);

            float pulseTarget = Mathf.Clamp01(closeness * 0.85f + Suspicion * 0.3f);
            Pulse = Mathf.MoveTowards(Pulse, pulseTarget, (pulseTarget > Pulse ? 0.6f : 0.15f) * dt);

            if (Suspicion >= 1f)
            {
                Suspicion = 1f;
                Found = true;
            }
            return Loud;
        }

        public static float Closeness(float distance) => Mathf.Clamp01(1f - distance / HearingRadius);

        // RMS отрезка сэмплов в dBFS.
        public static float ToDb(float[] samples, int count)
        {
            if (samples == null || count <= 0) return SilenceDb;
            double sum = 0;
            for (int i = 0; i < count; i++) sum += samples[i] * samples[i];
            float rms = (float)System.Math.Sqrt(sum / count);
            return rms <= 1e-5f ? SilenceDb : Mathf.Max(SilenceDb, 20f * Mathf.Log10(rms));
        }
    }
}
