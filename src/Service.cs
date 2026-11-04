// kbd-nightlight service host.
//
// Uses the Service Control Manager API directly instead of ServiceBase,
// because ServiceBase doesn't pass on the data that comes with power
// setting notifications, and that's how the lid state arrives.
//
//   kbd-nightlight.exe            run as the service (started by Windows)
//   kbd-nightlight.exe --console  run the same logic in a console, for testing
//   kbd-nightlight.exe --status   print what the service would see right now

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KbdNightlight
{
    static class Service
    {
        const string ServiceName = "kbd-nightlight";

        static Nightlight nightlight;
        static IntPtr statusHandle;
        static SERVICE_STATUS status;

        // Kept in static fields so the garbage collector never frees
        // delegates that native code still calls.
        static ServiceMainProc serviceMain = ServiceMain;
        static HandlerExProc handler = Handler;

        static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--status")
            {
                return PrintStatus();
            }
            if (args.Length == 1 && args[0] == "--console")
            {
                return RunConsole();
            }

            SERVICE_TABLE_ENTRY[] table =
            {
                new SERVICE_TABLE_ENTRY { Name = ServiceName, Proc = serviceMain },
                new SERVICE_TABLE_ENTRY(),
            };
            if (!StartServiceCtrlDispatcherW(table))
            {
                Console.Error.WriteLine(
                    "kbd-nightlight runs as a Windows service. For testing use --console or --status.");
                return 1;
            }
            return 0;
        }

        static void ServiceMain(int argc, IntPtr argv)
        {
            statusHandle = RegisterServiceCtrlHandlerExW(ServiceName, handler, IntPtr.Zero);
            if (statusHandle == IntPtr.Zero)
            {
                return;
            }

            status.ServiceType = SERVICE_WIN32_OWN_PROCESS;
            Report(SERVICE_START_PENDING, 0);

            nightlight = new Nightlight(false);

            // Windows sends the current lid state right after registering.
            Guid lid = GUID_LIDSWITCH_STATE_CHANGE;
            RegisterPowerSettingNotification(statusHandle, ref lid, DEVICE_NOTIFY_SERVICE_HANDLE);

            nightlight.Start("service start");
            Report(SERVICE_RUNNING, SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN |
                SERVICE_ACCEPT_POWEREVENT | SERVICE_ACCEPT_SESSIONCHANGE);
        }

        static int Handler(int control, int eventType, IntPtr eventData, IntPtr context)
        {
            if (nightlight == null)
            {
                return control == SERVICE_CONTROL_INTERROGATE ? NO_ERROR : ERROR_CALL_NOT_IMPLEMENTED;
            }
            switch (control)
            {
                case SERVICE_CONTROL_STOP:
                case SERVICE_CONTROL_SHUTDOWN:
                    Report(SERVICE_STOP_PENDING, 0);
                    nightlight.Stop();
                    Report(SERVICE_STOPPED, 0);
                    return NO_ERROR;

                case SERVICE_CONTROL_INTERROGATE:
                    return NO_ERROR;

                case SERVICE_CONTROL_POWEREVENT:
                    if (eventType == PBT_APMRESUMESUSPEND || eventType == PBT_APMRESUMEAUTOMATIC)
                    {
                        nightlight.OnResume();
                    }
                    else if (eventType == PBT_POWERSETTINGCHANGE && eventData != IntPtr.Zero)
                    {
                        HandlePowerSetting(eventData);
                    }
                    return NO_ERROR;

                case SERVICE_CONTROL_SESSIONCHANGE:
                    HandleSessionChange(eventType, eventData);
                    return NO_ERROR;
            }
            return ERROR_CALL_NOT_IMPLEMENTED;
        }

        static void HandlePowerSetting(IntPtr data)
        {
            // POWERBROADCAST_SETTING: GUID (16 bytes), DWORD DataLength, data.
            byte[] guid = new byte[16];
            Marshal.Copy(data, guid, 0, 16);
            if (new Guid(guid) == GUID_LIDSWITCH_STATE_CHANGE && Marshal.ReadInt32(data, 16) >= 4)
            {
                nightlight.OnLid(Marshal.ReadInt32(data, 20) != 0);
            }
        }

        static void HandleSessionChange(int eventType, IntPtr data)
        {
            // WTSSESSION_NOTIFICATION: DWORD cbSize, DWORD dwSessionId.
            uint sessionId = data == IntPtr.Zero ? 0 : (uint)Marshal.ReadInt32(data, 4);
            bool console = sessionId == Session.ConsoleSessionId();

            switch (eventType)
            {
                case WTS_SESSION_LOCK:
                    nightlight.OnLock();
                    break;
                case WTS_SESSION_UNLOCK:
                case WTS_SESSION_LOGON:
                    if (console)
                    {
                        nightlight.OnUnlock();
                    }
                    break;
                default:
                    // Console connect/disconnect, logoff, fast user switching:
                    // just re-check.
                    nightlight.Wake();
                    break;
            }
        }

        static void Report(int state, int accepted)
        {
            status.CurrentState = state;
            status.ControlsAccepted = accepted;
            status.Win32ExitCode = NO_ERROR;
            status.WaitHint = state == SERVICE_START_PENDING || state == SERVICE_STOP_PENDING ? 10000 : 0;
            status.CheckPoint = status.WaitHint == 0 ? 0 : status.CheckPoint + 1;
            SetServiceStatus(statusHandle, ref status);
        }

        static int RunConsole()
        {
            Console.WriteLine("Running in the console. Lid and power events are not received here;");
            Console.WriteLine("lock with Win+L to test. Press Ctrl+C to stop.");
            var done = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; done.Set(); };
            nightlight = new Nightlight(true);
            nightlight.Start("console");
            done.WaitOne();
            nightlight.Stop();
            return 0;
        }

        static int PrintStatus()
        {
            uint id = Session.ConsoleSessionId();
            Console.WriteLine("console session: {0}", id == 0xFFFFFFFF ? "none" : id.ToString());
            if (id != 0xFFFFFFFF)
            {
                Console.WriteLine("user:            {0}", Session.UserName(id));
                Console.WriteLine("session flags:   {0} (0 locked, 1 unlocked, -1 unknown)", Session.SessionFlags(id));
            }
            Console.WriteLine("locked:          {0}", Session.IsConsoleLocked());
            try
            {
                Console.WriteLine("backlight:       {0}", Backlight.Get().ToString().ToLowerInvariant());
            }
            catch (Exception e)
            {
                Console.WriteLine("backlight:       error: {0}", e.Message);
            }
            return 0;
        }

        // Service Control Manager API.

        const int NO_ERROR = 0;
        const int ERROR_CALL_NOT_IMPLEMENTED = 120;

        const int SERVICE_WIN32_OWN_PROCESS = 0x10;
        const int SERVICE_STOPPED = 1;
        const int SERVICE_START_PENDING = 2;
        const int SERVICE_STOP_PENDING = 3;
        const int SERVICE_RUNNING = 4;

        const int SERVICE_ACCEPT_STOP = 0x1;
        const int SERVICE_ACCEPT_SHUTDOWN = 0x4;
        const int SERVICE_ACCEPT_POWEREVENT = 0x40;
        const int SERVICE_ACCEPT_SESSIONCHANGE = 0x80;

        const int SERVICE_CONTROL_STOP = 1;
        const int SERVICE_CONTROL_INTERROGATE = 4;
        const int SERVICE_CONTROL_SHUTDOWN = 5;
        const int SERVICE_CONTROL_POWEREVENT = 13;
        const int SERVICE_CONTROL_SESSIONCHANGE = 14;

        const int PBT_APMRESUMESUSPEND = 0x7;
        const int PBT_APMRESUMEAUTOMATIC = 0x12;
        const int PBT_POWERSETTINGCHANGE = 0x8013;

        const int WTS_SESSION_LOGON = 5;
        const int WTS_SESSION_LOCK = 7;
        const int WTS_SESSION_UNLOCK = 8;

        const int DEVICE_NOTIFY_SERVICE_HANDLE = 1;
        static readonly Guid GUID_LIDSWITCH_STATE_CHANGE = new Guid("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");

        delegate void ServiceMainProc(int argc, IntPtr argv);
        delegate int HandlerExProc(int control, int eventType, IntPtr eventData, IntPtr context);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SERVICE_TABLE_ENTRY
        {
            public string Name;
            public ServiceMainProc Proc;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SERVICE_STATUS
        {
            public int ServiceType;
            public int CurrentState;
            public int ControlsAccepted;
            public int Win32ExitCode;
            public int ServiceSpecificExitCode;
            public int CheckPoint;
            public int WaitHint;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool StartServiceCtrlDispatcherW(SERVICE_TABLE_ENTRY[] table);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr RegisterServiceCtrlHandlerExW(string name, HandlerExProc handler, IntPtr context);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool SetServiceStatus(IntPtr handle, ref SERVICE_STATUS status);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid setting, int flags);
    }
}
