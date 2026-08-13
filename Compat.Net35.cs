#if NET35
using System;
using System.Diagnostics;
using System.Reflection;

namespace PetFriends
{
    internal static class Compat
    {
        private static readonly object RandomLock = new object();
        private static readonly Random SharedRandom = new Random();

        public static Random Random
        {
            get { return SharedRandom; }
        }

        public static int ProcessId
        {
            get { return Process.GetCurrentProcess().Id; }
        }

        public static string ProcessPath
        {
            get
            {
                Process process = Process.GetCurrentProcess();
                if (process.MainModule != null && !string.IsNullOrEmpty(process.MainModule.FileName))
                {
                    return process.MainModule.FileName;
                }
                return Assembly.GetExecutingAssembly().Location;
            }
        }

        public static bool IsLegacyWindows
        {
            get { return true; }
        }

        public static double Clamp(double value, double minimum, double maximum)
        {
            if (minimum > maximum) throw new ArgumentException("minimum cannot be greater than maximum", "minimum");
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        public static bool IsNullOrWhiteSpace(string value)
        {
            return value == null || value.Trim().Length == 0;
        }
    }
}
#endif
