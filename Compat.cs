using System.Diagnostics;
using System.Reflection;

namespace PetFriends;

internal static class Compat
{
    private static readonly Lazy<Random> SharedRandom = new(() => new Random());

    public static Random Random => SharedRandom.Value;

    public static int ProcessId => Process.GetCurrentProcess().Id;

    public static string ProcessPath =>
        Process.GetCurrentProcess().MainModule?.FileName ?? Assembly.GetExecutingAssembly().Location;

    public static bool IsLegacyWindows
    {
        get
        {
            var version = Environment.OSVersion.Version;
            return version.Major < 6 || version.Major == 6 && version.Minor <= 1;
        }
    }

    public static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum) throw new ArgumentException("minimum cannot be greater than maximum", nameof(minimum));
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
