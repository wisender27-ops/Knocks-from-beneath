using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Чем кончился раунд «Перестука» (K-03).
    public enum KnockCallOutcome
    {
        None = 0,
        Correct = 1,
        Wrong = 2,
        Ignored = 3
    }

    // M1 «Перестук»: ритм из групп ударов. Внутри группы — короткая пауза, между группами — длинная.
    // Уровень 1 проверяет только число ударов, 2–3 — ещё и где игрок сделал длинные паузы.
    public sealed class KnockPattern
    {
        public const float ShortGap = 0.35f;
        public const float LongGap = 1f;
        // Длинная пауза игрока — заметно длиннее его самой короткой (темп у всех свой), но не короче MinLongGap.
        public const float MinLongGap = 0.55f;
        public const float LongRatio = 1.5f;

        private static readonly int[][][] ByLevel =
        {
            new[] { new[] { 2 }, new[] { 3 } },
            new[] { new[] { 2, 1 }, new[] { 1, 2 } },
            new[] { new[] { 2, 1, 2 }, new[] { 1, 2, 1 }, new[] { 3, 2 } }
        };

        public readonly int Level;
        private readonly int[] _groups;

        public KnockPattern(int level, params int[] groups)
        {
            Level = Mathf.Clamp(level, 1, ByLevel.Length);
            _groups = groups != null && groups.Length > 0 ? (int[])groups.Clone() : new[] { 1 };
            foreach (int group in _groups) TapCount += Mathf.Max(1, group);
        }

        public int TapCount { get; }
        public int GroupCount => _groups.Length;
        public static int VariantCount(int level) => ByLevel[Mathf.Clamp(level, 1, ByLevel.Length) - 1].Length;

        public static KnockPattern ForLevel(int level, int variant)
        {
            int[][] variants = ByLevel[Mathf.Clamp(level, 1, ByLevel.Length) - 1];
            int index = ((variant % variants.Length) + variants.Length) % variants.Length;
            return new KnockPattern(level, variants[index]);
        }

        // Паузы перед каждым ударом после первого — как стучит хозяин.
        public float GapBefore(int tap)
        {
            int edge = 0;
            foreach (int group in _groups)
            {
                edge += Mathf.Max(1, group);
                if (tap == edge) return LongGap;
                if (tap < edge) break;
            }
            return ShortGap;
        }

        public float Duration
        {
            get
            {
                float total = 0f;
                for (int i = 1; i < TapCount; i++) total += GapBefore(i);
                return total;
            }
        }

        // Совпадает ли ответ игрока (времена ударов по возрастанию) с ритмом.
        public bool Matches(IReadOnlyList<float> taps)
        {
            if (taps == null || taps.Count != TapCount) return false;
            if (Level <= 1 || TapCount < 2) return true;
            float shortest = float.PositiveInfinity;
            for (int i = 1; i < taps.Count; i++) shortest = Mathf.Min(shortest, taps[i] - taps[i - 1]);
            float threshold = Mathf.Max(MinLongGap, shortest * LongRatio);
            for (int i = 1; i < taps.Count; i++)
            {
                bool playerLong = taps[i] - taps[i - 1] >= threshold;
                if (playerLong != GapBefore(i) > ShortGap) return false;
            }
            return true;
        }
    }

    // Собирает удары игрока после того, как хозяин отстучал ритм. Время — «игровое» (не идёт на паузе).
    public sealed class KnockAnswer
    {
        public float EndGap = 1.6f;          // столько тишины — ответ закончен
        public float ResponseWindow = 7f;    // столько ждём первого удара

        private readonly List<float> _taps = new List<float>();
        private float _startTime;

        public KnockPattern Pattern { get; private set; }
        public int TapCount => _taps.Count;
        public IReadOnlyList<float> Taps => _taps;

        public void Begin(KnockPattern pattern, float time)
        {
            Pattern = pattern;
            _taps.Clear();
            _startTime = time;
        }

        public void Tap(float time)
        {
            if (Pattern != null) _taps.Add(time);
        }

        // None — ещё ждём; иначе итог (выдаётся, пока не вызван Begin заново).
        public KnockCallOutcome Poll(float time)
        {
            if (Pattern == null) return KnockCallOutcome.None;
            if (_taps.Count == 0)
                return time - _startTime >= ResponseWindow ? KnockCallOutcome.Ignored : KnockCallOutcome.None;
            // Явно больше, чем нужно, — дальше слушать незачем.
            if (_taps.Count > Pattern.TapCount + 2) return KnockCallOutcome.Wrong;
            if (time - _taps[_taps.Count - 1] < EndGap) return KnockCallOutcome.None;
            return Pattern.Matches(_taps) ? KnockCallOutcome.Correct : KnockCallOutcome.Wrong;
        }
    }
}
