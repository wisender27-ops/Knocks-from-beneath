namespace KnocksFromBeneath
{
    // B-05: истинная концовка E5 «Ответ». Спуск в подвал на развилке ночи 2 доступен, когда выполнены
    // любые два из трёх условий (решение автора): стучащий доверяет, игрок видел запись, нашёл улики.
    public static class AnswerEnding
    {
        public const int TrustNeeded = 3;
        public const int CluesNeeded = 2;
        public const int ConditionsNeeded = 2;

        public static int ConditionsMet(int knockTrust, bool sawRecording, int cluesFound)
        {
            int met = 0;
            if (knockTrust >= TrustNeeded) met++;
            if (sawRecording) met++;
            if (cluesFound >= CluesNeeded) met++;
            return met;
        }

        public static bool IsAvailable(int knockTrust, bool sawRecording, int cluesFound) =>
            ConditionsMet(knockTrust, sawRecording, cluesFound) >= ConditionsNeeded;

        public static bool IsAvailable() =>
            IsAvailable(StoryFlags.KnockTrust, StoryFlags.SawRecording, StoryFlags.CluesFound);
    }
}
