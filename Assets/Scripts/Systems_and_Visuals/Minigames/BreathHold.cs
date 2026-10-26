using UnityEngine;

namespace KnocksFromBeneath
{
    // Логика мини-игры M7 «Дыхание» (укрытия ночи 2). Игрок задерживает дыхание; воздух кончается — герой
    // судорожно вдыхает на весь дом. Пока дышит, его слышно: чем выше пульс и чем ближе монстр, тем быстрее
    // монстр понимает, где он. Подозрение дошло до 1 — нашёл. Подача — HidingBreathGame.
    public sealed class BreathHold
    {
        public const float HoldSeconds = 7f;        // полный запас воздуха
        public const float RecoverSeconds = 4f;     // от нуля до полного
        public const float GaspLockout = 1.4f;      // после срыва не задержать дыхание
        public const float HearingRadius = 3.5f;

        public float Air { get; private set; } = 1f;
        public float Pulse { get; private set; }
        public float Suspicion { get; private set; }
        public float Noise { get; private set; }
        public bool Holding { get; private set; }
        public bool Found { get; private set; }
        public int Gasps { get; private set; }

        private float exhale, gasp, lockout;

        // true — в этот кадр герой сорвался и вдохнул.
        public bool Advance(float dt, bool holdInput, float monsterDistance, bool moving)
        {
            if (Found || dt <= 0f) return false;
            bool gasped = false;
            if (lockout > 0f) lockout -= dt;
            bool wantHold = holdInput && lockout <= 0f;

            if (wantHold)
            {
                Holding = true;
                Air = Mathf.Max(0f, Air - dt / HoldSeconds);
                if (Air <= 0f)
                {
                    Holding = false;
                    gasp = 1f;
                    lockout = GaspLockout;
                    Gasps++;
                    gasped = true;
                }
            }
            else
            {
                // Выдох после задержки слышен тем громче, чем дольше терпел.
                if (Holding) exhale = Mathf.Max(exhale, 0.3f + 0.5f * (1f - Air));
                Holding = false;
                Air = Mathf.Min(1f, Air + dt / RecoverSeconds);
            }

            float closeness = Closeness(monsterDistance);
            float strain = Holding ? (1f - Air) * 0.6f : 0f;
            float pulseTarget = Mathf.Clamp01(closeness * 0.8f + strain + Suspicion * 0.3f);
            Pulse = Mathf.MoveTowards(Pulse, pulseTarget, (pulseTarget > Pulse ? 0.6f : 0.15f) * dt);

            exhale = Mathf.Max(0f, exhale - dt / 0.8f);
            gasp = Mathf.Max(0f, gasp - dt);
            if (Holding)
                Noise = Air < 0.25f ? 0.15f * (0.25f - Air) / 0.25f : 0f; // на исходе воздуха сипит
            else
                Noise = Mathf.Max(0.15f + 0.5f * Pulse, exhale, gasp > 0f ? 1f : 0f);
            if (moving) Noise = Mathf.Max(Noise, 0.6f);

            float heard = Noise * closeness * closeness;
            if (gasped) Suspicion += closeness * 0.6f;
            // Тихий звук вдали тонет в доме: подозрение остывает, если слышно меньше, чем забывается.
            Suspicion = Mathf.Max(0f, Suspicion + (heard * 1.6f - 0.1f) * dt);
            if (Suspicion >= 1f)
            {
                Suspicion = 1f;
                Found = true;
            }
            return gasped;
        }

        public static float Closeness(float distance) => Mathf.Clamp01(1f - distance / HearingRadius);
    }
}
