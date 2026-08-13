using System.Diagnostics;
using System.Security;
using System.Text;

namespace PetFriends.Mac;

internal static class AutostartService
{
    private const string Label = "io.github.dada0o.petfriends";

    private static string LaunchAgentPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "LaunchAgents",
        $"{Label}.plist");

    private static string DisabledMarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "PetFriends",
        "autostart-disabled");

    public static bool IsEnabled => File.Exists(LaunchAgentPath);

    public static void InitializeDefault()
    {
        if (File.Exists(DisabledMarkerPath)) return;
        TrySetEnabled(true, out _);
    }

    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        string? temporaryPath = null;
        try
        {
            if (!enabled)
            {
                var settingsDirectory = Path.GetDirectoryName(DisabledMarkerPath)
                    ?? throw new InvalidOperationException("无法保存自动启动偏好。");
                Directory.CreateDirectory(settingsDirectory);
                File.WriteAllText(DisabledMarkerPath, "disabled\n", new UTF8Encoding(false));
                File.Delete(LaunchAgentPath);
                error = null;
                return true;
            }

            var processPath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                throw new InvalidOperationException("无法确定桌宠程序的位置。");
            }
            if (processPath.StartsWith("/Volumes/", StringComparison.Ordinal) ||
                processPath.Contains("/AppTranslocation/", StringComparison.Ordinal))
            {
                error = "请先把桌宠拖到“应用程序”文件夹，再开启自动启动。";
                return false;
            }

            var directory = Path.GetDirectoryName(LaunchAgentPath)
                ?? throw new InvalidOperationException("无法确定启动项目录。");
            Directory.CreateDirectory(directory);
            temporaryPath = $"{LaunchAgentPath}.tmp-{Environment.ProcessId}";
            File.WriteAllText(temporaryPath, BuildPlist(processPath), new UTF8Encoding(false));
            File.Move(temporaryPath, LaunchAgentPath, overwrite: true);
            temporaryPath = null;
            if (OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    LaunchAgentPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
            File.Delete(DisabledMarkerPath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }
    }

    private static string BuildPlist(string processPath)
    {
        var escapedPath = SecurityElement.Escape(processPath)
            ?? throw new InvalidOperationException("桌宠程序路径无效。");
        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key>
  <string>{Label}</string>
  <key>ProgramArguments</key>
  <array>
    <string>{escapedPath}</string>
    <string>--autostart</string>
  </array>
  <key>RunAtLoad</key>
  <true/>
  <key>KeepAlive</key>
  <false/>
</dict>
</plist>
""";
    }
}
