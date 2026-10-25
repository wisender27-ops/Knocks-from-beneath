using UnityEngine;

namespace KnocksFromBeneath
{
    public enum NailStrike { Drove, NailDone, Glance, Miss }

    // Логика «Гвоздей» (M3): прицел + замах. Удар по шляпке вгоняет гвоздь тем глубже, чем сильнее
    // замах. Снизу оно толкает люк: чем меньше гвоздей держит доски, тем выше подлетают створки и тем
    // больше доски трещат; недобитые гвозди толчок выдёргивает обратно. Доски сорвало — провал.
    public sealed class NailBoarding
    {
        public const int NailCount = 4;
        public const float HitRadius = 0.055f;    // промах по шляпке до стольких метров — удар засчитан
        public const float GlanceRadius = 0.11f;  // задел край шляпки — звон, гвоздь не идёт
        public const float TapDepth = 0.2f;       // глубина за удар без замаха
        public const float PowerDepth = 0.45f;    // плюс столько за полный замах
        public const float FullWindup = 0.45f;    // секунд замаха до полной силы
        public const float RampSeconds = 45f;     // за это время толчки выходят на полную силу
        public const float FirstHeave = 3.5f;
        public const float CalmInterval = 4.5f;
        public const float PanicInterval = 2.2f;
        public const float HeaveDamage = 0.15f;   // урон доскам от полного толчка, если ничего не забито
        public const float HeavePull = 0.15f;     // на столько полный толчок выдёргивает недобитый гвоздь
        public const float WarnSeconds = 0.8f;    // скрежет снизу перед толчком

        private readonly float[] depth = new float[NailCount];
        private float nextHeave = FirstHeave;

        public float Elapsed { get; private set; }
        public float Integrity { get; private set; } = 1f;
        public int NailsDone { get; private set; }
        public int Available { get; private set; }
        public int Heaves { get; private set; }
        public float LastLift { get; private set; }

        public float Depth(int nail) => nail >= 0 && nail < NailCount ? depth[nail] : 0f;
        public bool IsDone(int nail) => Depth(nail) >= 1f;
        public float Ramp01 => Mathf.Clamp01(Elapsed / RampSeconds);
        public float HeaveStrength => Mathf.Lerp(0.35f, 1f, Ramp01);
        public float UntilHeave => nextHeave - Elapsed;
        public bool HeaveWarning => !Finished && UntilHeave <= WarnSeconds;
        public bool Succeeded => NailsDone >= NailCount;
        public bool Failed => !Succeeded && Integrity <= 0f;
        public bool Finished => Succeeded || Failed;

        // Сколько держат гвозди: доля от полностью забитых всех гвоздей.
        public float Hold
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < NailCount; i++) sum += depth[i];
                return sum / NailCount;
            }
        }

        public static float Power(float windupSeconds) => Mathf.Clamp01(windupSeconds / FullWindup);

        // Доска легла — её гвозди можно забивать.
        public void OpenNails(int count) => Available = Mathf.Clamp(Mathf.Max(Available, count), 0, NailCount);

        // Возвращает true, если за этот шаг снизу толкнули.
        public bool Advance(float dt)
        {
            if (Finished || dt <= 0f) return false;
            Elapsed += dt;
            if (Elapsed < nextHeave) return false;
            Heave();
            nextHeave = Elapsed + Mathf.Lerp(CalmInterval, PanicInterval, Ramp01);
            return true;
        }

        // Шум (звон по металлу) — оно слышит и толкает раньше, но не мгновенно.
        public void Provoke(float seconds)
        {
            if (Finished) return;
            nextHeave = Mathf.Max(Elapsed + WarnSeconds, nextHeave - seconds);
        }

        public NailStrike Strike(int nail, float distance, float power)
        {
            if (Finished || nail < 0 || nail >= Available || IsDone(nail)) return NailStrike.Miss;
            if (distance > GlanceRadius) return NailStrike.Miss;
            if (distance > HitRadius) return NailStrike.Glance;
            depth[nail] += TapDepth + PowerDepth * Mathf.Clamp01(power);
            if (depth[nail] < 0.999f) return NailStrike.Drove;
            depth[nail] = 1f;
            NailsDone++;
            return NailStrike.NailDone;
        }

        private void Heave()
        {
            Heaves++;
            float strength = HeaveStrength;
            LastLift = strength * (1f - Hold);
            Integrity = Mathf.Max(0f, Integrity - HeaveDamage * LastLift);
            for (int i = 0; i < NailCount; i++)
                if (depth[i] > 0f && depth[i] < 1f)
                    depth[i] = Mathf.Max(0f, depth[i] - HeavePull * strength);
        }
    }
}
