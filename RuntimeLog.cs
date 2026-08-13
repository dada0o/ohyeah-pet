using System.IO;
using System.Text;

namespace PetFriends;

internal static class RuntimeLog
{
    private static readonly object SyncRoot = new();

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PetFriends",
        "runtime.log");

    public static void Write(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                var directory = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    FilePath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [PID {Compat.ProcessId}] {message}{Environment.NewLine}",
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never prevent the desktop pet from starting.
        }
    }

    public static void WriteException(string source, Exception? exception)
    {
        Write($"Unhandled exception ({source}): {exception?.ToString() ?? "unknown"}");
    }
}
