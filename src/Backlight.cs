// Reads and sets the ThinkPad keyboard backlight through Lenovo's PM driver.
//
// The control codes and bit layout come from Lenovo's own Vantage keyboard
// add-in (Keyboard_Core.dll, CKeyboardLight.*ByMLCG). See issue #2.
// Works for a normal user; no elevation needed.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KbdNightlight
{
    public enum BacklightLevel
    {
        Off = 0,
        Low = 1,
        High = 2,
    }

    public static class Backlight
    {
        const string DevicePath = @"\\.\IBMPmDrv";

        // "Multiple light control" get/set.
        const uint IoctlGetMultipleLightControl = 0x222680;
        const uint IoctlSetMultipleLightControl = 0x222684;

        const uint ErrorBit = 0x80000000;
        const uint SupportedBits = (1u << 16) | (1u << 18);
        const uint LevelMask = 0xF;

        public static BacklightLevel Get()
        {
            return (BacklightLevel)(Read() & LevelMask);
        }

        public static int MaxLevel()
        {
            return (int)((Read() >> 8) & 0xF);
        }

        public static void Set(BacklightLevel level)
        {
            uint current = Read();
            if ((current & SupportedBits) != SupportedBits)
            {
                throw new NotSupportedException(string.Format(
                    "Keyboard backlight control is not supported (value 0x{0:X8}).", current));
            }

            // Same composition as Lenovo's SetStatusByMLCG: keep their flag
            // bits from the current value, put the new level in the low nibble.
            uint value = ((current >> 13) & 0x100) | (current & 0xF0) | ((uint)level & LevelMask);
            Call(IoctlSetMultipleLightControl, value);
        }

        public static uint Read()
        {
            return Call(IoctlGetMultipleLightControl, 0);
        }

        static uint Call(uint code, uint input)
        {
            IntPtr handle = CreateFileW(DevicePath, GenericRead, FileShareRead | FileShareWrite,
                IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == InvalidHandle)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, string.Format(
                    "Cannot open {0} (error {1}).",
                    DevicePath, error));
            }

            try
            {
                uint output, returned;
                if (!DeviceIoControl(handle, code, ref input, 4, out output, 4, out returned, IntPtr.Zero))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(error, string.Format(
                        "Control code 0x{0:X} failed (error {1}).", code, error));
                }
                if ((output & ErrorBit) != 0)
                {
                    throw new InvalidOperationException(string.Format(
                        "Driver reported an error for control code 0x{0:X} (value 0x{1:X8}).", code, output));
                }
                return output;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        const uint GenericRead = 0x80000000;
        const uint FileShareRead = 1;
        const uint FileShareWrite = 2;
        const uint OpenExisting = 3;
        static readonly IntPtr InvalidHandle = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr handle, uint code, ref uint inBuffer, uint inSize,
            out uint outBuffer, uint outSize, out uint returned, IntPtr overlapped);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);
    }
}
