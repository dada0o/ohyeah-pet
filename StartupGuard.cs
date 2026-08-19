using System.IO;
using System.Text;

namespace PetFriends;

internal static class StartupGuard
{
    private static readonly object SyncRoot = new();

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PetFriends",
        "startup-in-progress.txt");

    public static bool Begin()
    {
        try
        {
            lock (SyncRoot)
            {
                var previousStartupWasIncomplete = File.Exists(FilePath);
                WriteState("bootstrap");
                return previousStartupWasIncomplete;
            }
        }
        catch
        {
            return false;
        }
    }

    public static void UpdatePhase(string phase)
    {
        try
        {
            lock (SyncRoot)
            {
                WriteState(phase);
            }
        }
        catch
        {
            // A diagnostic marker must never prevent startup.
        }
    }

    public static void MarkStable()
    {
        try
        {
            lock (SyncRoot)
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
        }
        catch
        {
            // A stale marker only causes the next launch to use compatibility mode.
        }
    }

    private static void WriteState(string phase)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            FilePath,
            $"{DateTimeOffset.Now:O}|PID={Compat.ProcessId}|Version={typeof(App).Assembly.GetName().Version}|Phase={phase}{Environment.NewLine}",
            Encoding.UTF8);
    }
}
