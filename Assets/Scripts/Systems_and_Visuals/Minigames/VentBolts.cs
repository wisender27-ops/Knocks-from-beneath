using UnityEngine;

namespace KnocksFromBeneath
{
    // Логика откручивания болтов решётки, без сцены: удержание над болтом откручивает его, отпускание не сбрасывает.
    public sealed class VentBolts
    {
        public const int Count = 4;
        public const float HoldSeconds = 1.5f;

        private readonly float[] progress = new float[Count];

        public int Removed { get; private set; }
        public bool Finished => Removed == Count;

        public float Progress(int bolt) => progress[bolt];
        public bool IsRemoved(int bolt) => progress[bolt] >= 1f;

        // Возвращает true в тот кадр, когда болт вышел полностью.
        public bool Hold(int bolt, float dt)
        {
            if (bolt < 0 || bolt >= Count || IsRemoved(bolt) || dt <= 0f) return false;
            progress[bolt] = Mathf.Clamp01(progress[bolt] + dt / HoldSeconds);
            if (!IsRemoved(bolt)) return false;
            Removed++;
            return true;
        }
    }
}
