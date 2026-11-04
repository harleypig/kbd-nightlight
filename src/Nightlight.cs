// What the service does, separate from how Windows delivers events to it.
//
// Every event just wakes the worker, which then checks the actual state:
// if the console is locked (or at the login screen) and the lid is open,
// the backlight is set to the lock level. Unlock and logon set the unlock
// level once. While locked, the worker re-checks every IntervalSeconds, so
// the light comes back whatever turned it off. See issues #3 and #5.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Threading;

namespace KbdNightlight
{
    public sealed class Nightlight
    {
        public const string EventSource = "kbd-nightlight";

        // After a resume the embedded controller may still be settling, so
        // check again a few times shortly after.
        static readonly int[] ResumeRecheckSeconds = { 2, 5, 10 };

        readonly BacklightLevel lockLevel;
        readonly BacklightLevel unlockLevel;
        readonly TimeSpan interval;
        readonly bool console;

        readonly object gate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly List<DateTime> rechecks = new List<DateTime>();
        bool stopping;
        bool unlockPending;
        bool lidOpen = true;
        string lastError;
        Thread worker;

        public Nightlight(bool console)
        {
            this.console = console;
            lockLevel = ReadLevel("LockLevel", BacklightLevel.High);
            unlockLevel = ReadLevel("UnlockLevel", BacklightLevel.Low);
            int seconds;
            if (!int.TryParse(ConfigurationManager.AppSettings["IntervalSeconds"], out seconds) || seconds < 5)
            {
                seconds = 30;
            }
            interval = TimeSpan.FromSeconds(seconds);
        }

        public void Start(string why)
        {
            Log(EventLogEntryType.Information, 1, string.Format(
                "Started ({0}). Lock level {1}, unlock level {2}, re-check every {3} s.",
                why, Name(lockLevel), Name(unlockLevel), (int)interval.TotalSeconds));
            worker = new Thread(Run);
            worker.IsBackground = true;
            worker.Name = "nightlight";
            worker.Start();
        }

        public void Stop()
        {
            lock (gate)
            {
                stopping = true;
            }
            wake.Set();
            if (worker != null)
            {
                worker.Join(TimeSpan.FromSeconds(5));
            }
            Log(EventLogEntryType.Information, 2, "Stopped.");
        }

        public void OnLock()
        {
            Wake();
        }

        public void OnUnlock()
        {
            lock (gate)
            {
                unlockPending = true;
            }
            wake.Set();
        }

        public void OnResume()
        {
            lock (gate)
            {
                DateTime now = DateTime.UtcNow;
                foreach (int s in ResumeRecheckSeconds)
                {
                    rechecks.Add(now.AddSeconds(s));
                }
            }
            wake.Set();
        }

        public void OnLid(bool open)
        {
            lock (gate)
            {
                lidOpen = open;
            }
            wake.Set();
        }

        public void Wake()
        {
            wake.Set();
        }

        void Run()
        {
            while (true)
            {
                bool unlock;
                bool lid;
                lock (gate)
                {
                    if (stopping)
                    {
                        return;
                    }
                    unlock = unlockPending;
                    unlockPending = false;
                    lid = lidOpen;
                    rechecks.RemoveAll(t => t <= DateTime.UtcNow);
                }

                try
                {
                    if (unlock && !Session.IsConsoleLocked())
                    {
                        Ensure(unlockLevel, "unlocked");
                    }
                    else if (lid && Session.IsConsoleLocked())
                    {
                        Ensure(lockLevel, "locked");
                    }
                    lastError = null;
                }
                catch (Exception e)
                {
                    // The same failure every 30 s would flood the log.
                    if (e.Message != lastError)
                    {
                        Log(EventLogEntryType.Error, 20, "Could not set the backlight: " + e.Message);
                        lastError = e.Message;
                    }
                }

                wake.WaitOne(NextDelay());
            }
        }

        TimeSpan NextDelay()
        {
            lock (gate)
            {
                TimeSpan delay = interval;
                DateTime now = DateTime.UtcNow;
                foreach (DateTime t in rechecks)
                {
                    TimeSpan d = t - now;
                    if (d < delay)
                    {
                        delay = d < TimeSpan.Zero ? TimeSpan.Zero : d;
                    }
                }
                return delay;
            }
        }

        void Ensure(BacklightLevel level, string why)
        {
            BacklightLevel current = Backlight.Get();
            if (current == level)
            {
                return;
            }
            Backlight.Set(level);
            Log(EventLogEntryType.Information, 10, string.Format(
                "Backlight {0} -> {1} ({2}).", Name(current), Name(level), why));
        }

        void Log(EventLogEntryType type, int id, string message)
        {
            if (console)
            {
                Console.WriteLine("{0:HH:mm:ss} {1}", DateTime.Now, message);
                return;
            }
            try
            {
                EventLog.WriteEntry(EventSource, message, type, id);
            }
            catch
            {
                // Logging must never stop the service.
            }
        }

        static BacklightLevel ReadLevel(string key, BacklightLevel fallback)
        {
            string text = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrEmpty(text))
            {
                return fallback;
            }
            switch (text.Trim().ToLowerInvariant())
            {
                case "off": return BacklightLevel.Off;
                case "low": return BacklightLevel.Low;
                case "high": return BacklightLevel.High;
            }
            return fallback;
        }

        static string Name(BacklightLevel level)
        {
            return level.ToString().ToLowerInvariant();
        }
    }
}
