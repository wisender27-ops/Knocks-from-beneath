using UnityEngine;

namespace KnocksFromBeneath
{
    // Релизная сборка пишет в Player.log только ошибки: отладочные Debug.Log игроку не нужны.
    internal static class ReleaseLogging
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Apply() => Debug.unityLogger.filterLogType = LogType.Error;
#endif
    }
}
