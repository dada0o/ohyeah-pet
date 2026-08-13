using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PetFriends;

internal static class Compat
{
    private static readonly Lazy<Random> SharedRandom = new(() => new Random());
    private static readonly Lazy<Version> DetectedWindowsVersion = new(GetWindowsVersion);

    public static Random Random => SharedRandom.Value;

    public static int ProcessId => Process.GetCurrentProcess().Id;

    public static string ProcessPath =>
        Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetExecutingAssembly().Location;

    public static bool IsLegacyWindows =>
        WindowsVersion.Major < 6 || WindowsVersion.Major == 6 && WindowsVersion.Minor <= 1;

    public static bool IsWindows11OrLater =>
        WindowsVersion.Major > 10 || WindowsVersion.Major == 10 && WindowsVersion.Build >= 22000;

    public static bool UseSafeRendering => IsLegacyWindows || IsWindows11OrLater;

    public static Version WindowsVersion => DetectedWindowsVersion.Value;

    public static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum) throw new ArgumentException("minimum cannot be greater than maximum", nameof(minimum));
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private static Version GetWindowsVersion()
    {
        var info = new OsVersionInfo
        {
            Size = Marshal.SizeOf(typeof(OsVersionInfo)),
            ServicePack = string.Empty
        };
        try
        {
            if (RtlGetVersion(ref info) == 0)
            {
                return new Version(info.Major, info.Minor, info.Build);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        return Environment.OSVersion.Version;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OsVersionInfo
    {
        public int Size;
        public int Major;
        public int Minor;
        public int Build;
        public int PlatformId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ServicePack;
    }

    [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
    private static extern int RtlGetVersion(ref OsVersionInfo versionInfo);
}
