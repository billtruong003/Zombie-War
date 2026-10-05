using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// On a device every logged line with a stack trace costs milliseconds. The 05/10 device run had
    /// a 297 ms frame inside EventSystem.Update while each tap logged errors and DEV notes with full
    /// traces. Plain logs and warnings keep their text but drop the trace; errors keep theirs.
    /// </summary>
    static class LogCost
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Trim()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        }
#endif
    }
}
