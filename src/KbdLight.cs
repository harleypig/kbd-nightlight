// kbdlight: get or set the ThinkPad keyboard backlight.

using System;

namespace KbdNightlight
{
    static class KbdLight
    {
        const string Usage =
            "usage: kbdlight get\n" +
            "       kbdlight set off|low|high\n" +
            "       kbdlight raw";

        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && args[0] == "get")
                {
                    Console.WriteLine(Backlight.Get().ToString().ToLowerInvariant());
                    return 0;
                }
                if (args.Length == 1 && args[0] == "raw")
                {
                    Console.WriteLine("0x{0:X8}", Backlight.Read());
                    return 0;
                }
                if (args.Length == 2 && args[0] == "set")
                {
                    BacklightLevel level;
                    if (!TryParseLevel(args[1], out level))
                    {
                        Console.Error.WriteLine(Usage);
                        return 2;
                    }
                    Backlight.Set(level);
                    Console.WriteLine(Backlight.Get().ToString().ToLowerInvariant());
                    return 0;
                }

                Console.Error.WriteLine(Usage);
                return 2;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("kbdlight: " + e.Message);
                return 1;
            }
        }

        static bool TryParseLevel(string text, out BacklightLevel level)
        {
            switch (text.ToLowerInvariant())
            {
                case "off": level = BacklightLevel.Off; return true;
                case "low": level = BacklightLevel.Low; return true;
                case "high": level = BacklightLevel.High; return true;
            }
            level = BacklightLevel.Off;
            return false;
        }
    }
}
