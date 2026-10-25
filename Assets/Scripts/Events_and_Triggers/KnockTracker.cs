using UnityEngine;

namespace KnocksFromBeneath
{
    // Шум, который слышит стучащий снизу: присед/медленный шаг/стояние — тихо.
    public enum KnockNoise
    {
        Quiet = 0,
        Walk = 1,
        Run = 2
    }

    // Логика K-01 без MonoBehaviour: стучащий под полом идёт к последнему месту, где услышал
    // игрока. Шум копит «внимание»; набрав его, стучащий узнаёт текущую позицию игрока.
    // В тишине внимание гаснет, и он топчется у последней известной точки — «теряет» игрока.
    public sealed class KnockTracker
    {
        public float WalkHearSeconds = 2.5f;   // столько нужно идти, чтобы тебя услышали
        public float RunHearSeconds = 0.6f;    // бег слышен почти сразу
        public float ForgetSeconds = 4f;       // тишина, за которую внимание падает с 1 до 0
        public float CreepSpeed = 0.7f;        // м/с — ползёт, пока игрок ходит
        public float HuntSpeed = 1.8f;         // м/с — после бега
        public float HuntSeconds = 3f;         // сколько держится быстрый темп после бега
        public float ArriveDistance = 0.35f;
        public Vector2 BoundsMin = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        public Vector2 BoundsMax = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

        public Vector2 Source { get; private set; }     // XZ стучащего
        public Vector2 KnownTarget { get; private set; } // XZ, где он последний раз слышал игрока
        public float Attention { get; private set; }
        public bool HasPlayer { get; private set; }      // слышит игрока прямо сейчас

        private float _huntLeft;

        public void Reset(Vector2 source)
        {
            Source = Clamp(source);
            KnownTarget = Source;
            Attention = 0f;
            HasPlayer = false;
            _huntLeft = 0f;
        }

        public void Step(Vector2 player, KnockNoise noise, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (noise == KnockNoise.Quiet)
            {
                Attention = Mathf.Max(0f, Attention - deltaTime / Mathf.Max(0.01f, ForgetSeconds));
                if (Attention <= 0f) HasPlayer = false;
            }
            else
            {
                float hear = noise == KnockNoise.Run ? RunHearSeconds : WalkHearSeconds;
                Attention = Mathf.Min(1f, Attention + deltaTime / Mathf.Max(0.01f, hear));
                if (noise == KnockNoise.Run) _huntLeft = HuntSeconds;
                if (Attention >= 1f) HasPlayer = true;
            }

            if (HasPlayer && noise != KnockNoise.Quiet) KnownTarget = Clamp(player);
            _huntLeft = Mathf.Max(0f, _huntLeft - deltaTime);

            Vector2 toTarget = KnownTarget - Source;
            float distance = toTarget.magnitude;
            if (distance <= ArriveDistance) return;
            float speed = _huntLeft > 0f ? HuntSpeed : CreepSpeed;
            float step = Mathf.Min(distance - ArriveDistance, speed * deltaTime);
            Source = Clamp(Source + toTarget / distance * step);
        }

        // K-02: игрок постучал в пол — стучащий сразу знает, где он.
        public void Hear(Vector2 player)
        {
            Attention = 1f;
            HasPlayer = true;
            KnownTarget = Clamp(player);
        }

        private Vector2 Clamp(Vector2 point) => new Vector2(
            Mathf.Clamp(point.x, BoundsMin.x, BoundsMax.x),
            Mathf.Clamp(point.y, BoundsMin.y, BoundsMax.y));
    }
}
