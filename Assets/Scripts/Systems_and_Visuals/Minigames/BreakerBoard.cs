using UnityEngine;

namespace KnocksFromBeneath
{
    public enum BreakerFlip { None, Raised, Lowered, MainTripped, Restored }

    // Логика «Щитка» (M4): выбиты несколько групповых автоматов и вводной. Порядок как на записке хозяина:
    // сначала все группы, вводной — последним; вводной при опущенной группе тут же выбивает (громко — стук
    // подходит быстрее). Автомат кухни сам щёлкает обратно через пару секунд после подъёма — его поднимают
    // последним и сразу вводной. Стук снизу всё время подходит к щитку; дошёл — провал.
    public sealed class BreakerBoard
    {
        public const int GroupCount = 16;          // два ряда по 8, как в модели щитка
        public const int Faulty = 0;               // КУХНЯ: линия под кухней, выбивает сама
        public const float FailSeconds = 75f;      // за столько стук доходит до щитка, если не шуметь
        public const float MainTripNoise = 0.1f;   // вводной выбило — стук ближе на столько
        public const float FirstRetrip = 2.2f;     // первый раз кухня держится дольше: успеть понять
        public const float CalmRetrip = 3.6f;
        public const float PanicRetrip = 1.8f;
        public static readonly string[] Labels = {
            "КУХНЯ", "ПРИХОЖАЯ", "ГОСТИНАЯ", "ЛЕСТНИЦА", "СПАЛЬНЯ", "СПАЛЬНЯ 2", "ГАРДЕРОБ", "САНУЗЕЛ",
            "ВАННАЯ", "ВАННАЯ 2", "ГАРАЖ", "КРЫЛЬЦО", "ДВОР", "РОЗЕТКИ", "БОЙЛЕР", "ПОДВАЛ" };

        private readonly bool[] up = new bool[GroupCount];
        private float retripAt = -1f;

        public float Elapsed { get; private set; }
        public float Approach { get; private set; }
        public bool MainUp { get; private set; }
        public bool Finished { get; private set; }
        public bool Succeeded { get; private set; }
        public int MainTrips { get; private set; }
        public int Retrips { get; private set; }

        public bool Failed => Finished && !Succeeded;
        public bool IsUp(int group) => group >= 0 && group < GroupCount && up[group];
        public float KnockInterval => Mathf.Lerp(3.2f, 0.8f, Approach);
        public bool RetripPending => retripAt >= 0f;

        public int GroupsDown
        {
            get
            {
                int down = 0;
                for (int i = 0; i < GroupCount; i++) if (!up[i]) down++;
                return down;
            }
        }

        // Выбиты кухня и ещё extraTripped других групп (кроме подвала — его не трогали годами).
        public BreakerBoard(int extraTripped = 2, System.Random random = null)
        {
            random ??= new System.Random();
            for (int i = 0; i < GroupCount; i++) up[i] = true;
            up[Faulty] = false;
            int candidates = GroupCount - 2;
            extraTripped = Mathf.Clamp(extraTripped, 0, candidates);
            while (extraTripped > 0)
            {
                int group = 1 + random.Next(candidates);
                if (!up[group]) continue;
                up[group] = false;
                extraTripped--;
            }
        }

        public BreakerFlip Toggle(int group)
        {
            if (Finished || group < 0 || group >= GroupCount) return BreakerFlip.None;
            up[group] = !up[group];
            if (group == Faulty)
                retripAt = up[group]
                    ? Elapsed + (Retrips == 0 ? FirstRetrip : Mathf.Lerp(CalmRetrip, PanicRetrip, Approach))
                    : -1f;
            return up[group] ? BreakerFlip.Raised : BreakerFlip.Lowered;
        }

        public BreakerFlip ToggleMain()
        {
            if (Finished) return BreakerFlip.None;
            if (GroupsDown > 0)
            {
                MainTrips++;
                Approach = Mathf.Min(1f, Approach + MainTripNoise);
                if (Approach >= 1f) Finished = true;
                return BreakerFlip.MainTripped;
            }
            MainUp = true;
            Succeeded = true;
            Finished = true;
            retripAt = -1f;
            return BreakerFlip.Restored;
        }

        // true — кухня только что сама щёлкнула вниз.
        public bool Advance(float dt)
        {
            if (Finished) return false;
            Elapsed += dt;
            Approach = Mathf.Min(1f, Approach + dt / FailSeconds);
            if (Approach >= 1f)
            {
                Finished = true;
                return false;
            }
            if (retripAt < 0f || Elapsed < retripAt) return false;
            retripAt = -1f;
            up[Faulty] = false;
            Retrips++;
            return true;
        }
    }
}
