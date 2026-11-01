using UnityEngine;

namespace KnocksFromBeneath
{
    public enum OwnerKnockMood
    {
        Ordered = 0,   // осмысленный перестук «2·1»
        Frantic = 1,   // беспорядочный
        Desperate = 2  // отчаянный, почти без пауз
    }

    // P-04: невидимая шкала дедлайна 0→1 и то, как по ней нарастает «время кончается». Подача — DiegeticDeadline.
    public static class DeadlineScale
    {
        public const float SilenceSeconds = 3f;
        public const float FlickerFrom = 0.45f;
        public const float BlackoutsFrom = 2f / 3f; // последняя треть — короткие провалы в темноту
        public const float TrembleFrom = 0.5f;
        public const float FrontThought = 0.4f;
        public const float BackThought = 0.72f;

        // Последние секунды — тишина: всё обрывается перед тем, как оно вырвется.
        public static bool IsSilent(float remaining) => remaining > 0f && remaining <= SilenceSeconds;

        // Удары обрываются тишиной около 0.9 шкалы, поэтому самые частые должны прийти к этому моменту.
        public static float BlowInterval(float tension) => Mathf.Lerp(5f, 0.7f, Mathf.Clamp01(tension / 0.9f));
        public static float BlowStrength(float tension) => Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(tension));
        public static float HeartInterval(float tension) => 60f / Mathf.Lerp(72f, 165f, Mathf.Clamp01(tension));
        public static float BreathInterval(float tension) => Mathf.Lerp(4.2f, 1.6f, Mathf.Clamp01(tension));

        public static OwnerKnockMood Mood(float tension) =>
            tension < 0.4f ? OwnerKnockMood.Ordered :
            tension < 0.72f ? OwnerKnockMood.Frantic : OwnerKnockMood.Desperate;

        // Пауза перед следующим стуком; step — номер стука в перестуке, random01 — случайное число 0..1.
        public static float KnockGap(OwnerKnockMood mood, int step, float random01)
        {
            switch (mood)
            {
                case OwnerKnockMood.Ordered:
                    int beat = step % 3;
                    return beat == 0 ? 0.35f : beat == 1 ? 0.95f : 3.2f;
                case OwnerKnockMood.Frantic:
                    return Mathf.Lerp(0.18f, 0.7f, random01);
                default:
                    return Mathf.Lerp(0.07f, 0.22f, random01);
            }
        }

        public static float FlickerThreshold(float tension) =>
            Mathf.Lerp(0.72f, 0.45f, Mathf.InverseLerp(FlickerFrom, 1f, tension));
    }
}
