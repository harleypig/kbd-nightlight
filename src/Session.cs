// Console session state: is the person at the keyboard looking at the lock
// or login screen?

using System;
using System.Runtime.InteropServices;

namespace KbdNightlight
{
    public static class Session
    {
        const uint NoConsoleSession = 0xFFFFFFFF;
        const int WTSUserName = 5;
        const int WTSSessionInfoEx = 25;
        const int WtsSessionStateLock = 0;

        // WTSINFOEXW: DWORD Level, then the union, which is 8-byte aligned
        // because WTSINFOEX_LEVEL1_W holds LARGE_INTEGERs. So SessionFlags
        // sits after Level + padding + SessionId + SessionState.
        const int SessionFlagsOffset = 16;

        public static uint ConsoleSessionId()
        {
            return WTSGetActiveConsoleSessionId();
        }

        // True when nobody is logged on at the console, or the console
        // session is locked. Also true while the console is switching
        // sessions, which happens at the login screen.
        public static bool IsConsoleLocked()
        {
            uint id = WTSGetActiveConsoleSessionId();
            if (id == NoConsoleSession)
            {
                return true;
            }
            if (string.IsNullOrEmpty(UserName(id)))
            {
                return true;
            }
            return SessionFlags(id) == WtsSessionStateLock;
        }

        public static string UserName(uint sessionId)
        {
            IntPtr buffer;
            int bytes;
            if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, WTSUserName, out buffer, out bytes))
            {
                return null;
            }
            try
            {
                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }

        // WTS_SESSIONSTATE_LOCK (0), WTS_SESSIONSTATE_UNLOCK (1) or
        // WTS_SESSIONSTATE_UNKNOWN (-1).
        public static int SessionFlags(uint sessionId)
        {
            IntPtr buffer;
            int bytes;
            if (!WTSQuerySessionInformationW(IntPtr.Zero, sessionId, WTSSessionInfoEx, out buffer, out bytes))
            {
                return -1;
            }
            try
            {
                if (Marshal.ReadInt32(buffer, 0) != 1)
                {
                    return -1;
                }
                return Marshal.ReadInt32(buffer, SessionFlagsOffset);
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }

        [DllImport("kernel32.dll")]
        static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        static extern bool WTSQuerySessionInformationW(IntPtr server, uint sessionId, int infoClass,
            out IntPtr buffer, out int bytes);

        [DllImport("wtsapi32.dll")]
        static extern void WTSFreeMemory(IntPtr memory);
    }
}
