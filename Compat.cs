using System.Diagnostics;
using System.Reflection;

namespace PetFriends;

internal static class Compat
{
    private static readonly Lazy<Random> SharedRandom = new(() => new Random());
    private static bool _compatibilityMode;

    public static Random Random => SharedRandom.Value;

    public static int ProcessId => Process.GetCurrentProcess().Id;

    public static string ProcessPath =>
        Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetExecutingAssembly().Location;

    public static bool IsLegacyWindows =>
        WindowsVersion.Major < 6 || WindowsVersion.Major == 6 && WindowsVersion.Minor <= 1;

    public static bool IsWindows11OrLater =>
        WindowsVersion.Major > 10 || WindowsVersion.Major == 10 && WindowsVersion.Build >= 22000;

    public static bool IsWindows11_24H2 =>
        WindowsVersion.Major == 10 && WindowsVersion.Build == 26100;

    public static bool UseCompatibilityMode => _compatibilityMode;

    public static bool UseSafeRendering => IsLegacyWindows || IsWindows11OrLater || UseCompatibilityMode;

    public static bool TrayIconEnabled => !UseCompatibilityMode;

    public static bool NativeWindowIntegrationEnabled => !UseCompatibilityMode;

    // .NET 5+ returns the real Windows version here. Avoid an unnecessary
    // RtlGetVersion P/Invoke during the fragile native startup path.
    public static Version WindowsVersion => Environment.OSVersion.Version;

    public static void ConfigureCompatibilityMode(bool enabled)
    {
        _compatibilityMode = enabled;
    }

    public static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum) throw new ArgumentException("minimum cannot be greater than maximum", nameof(minimum));
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

}
