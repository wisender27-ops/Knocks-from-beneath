namespace KnocksFromBeneath
{
    // Что стучащий делает в ответ на серию ударов игрока.
    public enum KnockReplyKind
    {
        None = 0,
        Repeat = 1,  // повторяет число ударов
        Silence = 2  // беспорядочный стук — затихает и прислушивается
    }

    // Логика K-02 без MonoBehaviour: удары игрока собираются в серию; серия кончается паузой
    // BurstGap. Короткую серию (1..MaxRepeat) стучащий повторяет, длинную — пугается и молчит.
    public sealed class KnockReply
    {
        public float BurstGap = 1.1f;  // пауза, после которой серия считается законченной
        public int MaxRepeat = 4;

        public int PendingTaps { get; private set; }
        public bool IsListening => PendingTaps > 0;

        private float _lastTapTime;

        public void Tap(float time)
        {
            PendingTaps++;
            _lastTapTime = time;
        }

        // Вызывать каждый кадр: возвращает решение один раз, когда серия закончилась.
        public KnockReplyKind Poll(float time, out int count)
        {
            count = 0;
            if (PendingTaps == 0 || time - _lastTapTime < BurstGap) return KnockReplyKind.None;
            count = PendingTaps;
            PendingTaps = 0;
            return count <= MaxRepeat ? KnockReplyKind.Repeat : KnockReplyKind.Silence;
        }

        public void Clear() => PendingTaps = 0;
    }
}
