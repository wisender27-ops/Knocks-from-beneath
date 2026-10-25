using UnityEngine;

namespace KnocksFromBeneath
{
    public enum NailStrike { Drove, NailDone, Bent }

    // Логика «Гвоздей» (M3): гвоздь качается, бить нужно, когда он стоит ровно. Промах гнёт гвоздь
    // и бьёт по запасу прочности досок; запас кончился — доски выбиты.
    public sealed class NailBoarding
    {
        public const int NailCount = 4;
        public const int HitsPerNail = 2;
        public const float Budget = 20f;       // секунд до того, как доски выбьют
        public const float MissCost = 4f;      // каждый погнутый гвоздь «съедает» столько секунд
        public const float Amplitude = 26f;    // наклон гвоздя в крайней точке, градусы
        public const float UprightAngle = 8f;  // «стоит ровно»
        public const float CalmPeriod = 1.2f;  // полный мах туда-обратно в начале
        public const float PanicPeriod = 0.75f; // и когда доски вот-вот выбьют

        private float phase;
        public int NailsDone { get; private set; }
        public int HitsOnNail { get; private set; }
        public float Pressure { get; private set; }
        public int Bent { get; private set; }

        public float Pressure01 => Mathf.Clamp01(Pressure / Budget);
        public float Angle => Amplitude * Mathf.Sin(phase);
        public bool IsUpright => Mathf.Abs(Angle) <= UprightAngle;
        public bool Succeeded => NailsDone >= NailCount;
        public bool Failed => !Succeeded && Pressure >= Budget;
        public bool Finished => Succeeded || Failed;

        public NailBoarding() => phase = Mathf.PI * 0.5f;

        public void Advance(float dt)
        {
            if (Finished || dt <= 0f) return;
            Pressure = Mathf.Min(Budget, Pressure + dt);
            float period = Mathf.Lerp(CalmPeriod, PanicPeriod, Pressure01);
            phase = Mathf.Repeat(phase + dt * 2f * Mathf.PI / period, 2f * Mathf.PI);
        }

        public NailStrike Strike()
        {
            if (Finished) return NailStrike.Bent;
            if (!IsUpright)
            {
                Bent++;
                HitsOnNail = 0;
                Pressure = Mathf.Min(Budget, Pressure + MissCost);
                phase = Mathf.PI * 0.5f;
                return NailStrike.Bent;
            }
            if (++HitsOnNail < HitsPerNail) return NailStrike.Drove;
            HitsOnNail = 0;
            NailsDone++;
            phase = Mathf.PI * 0.5f;
            return NailStrike.NailDone;
        }
    }
}
