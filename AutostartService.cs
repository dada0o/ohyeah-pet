using Microsoft.Win32;

namespace PetFriends;

internal static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string SettingsKeyPath = @"Software\PetFriends";
    private const string ValueName = "PetFriends";
    private const string PreferenceValueName = "StartWithWindows";

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(GetRegisteredCommand());

    public static void InitializeDefault()
    {
        if (GetPreference() == 0) return;

        var registeredCommand = GetRegisteredCommand();
        var expectedCommand = BuildCommand();
        if (string.Equals(registeredCommand, expectedCommand, StringComparison.OrdinalIgnoreCase)) return;

        if (!TrySetEnabled(true, out var error))
        {
            RuntimeLog.Write($"Could not refresh the startup registration: {error}");
        }
    }

    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("无法打开当前用户的启动项设置。");
            using var settings = Registry.CurrentUser.CreateSubKey(SettingsKeyPath, writable: true)
                ?? throw new InvalidOperationException("无法保存自动启动偏好。");
            if (enabled)
            {
                key.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
                settings.SetValue(PreferenceValueName, 1, RegistryValueKind.DWord);
            }
            else
            {
                settings.SetValue(PreferenceValueName, 0, RegistryValueKind.DWord);
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            RuntimeLog.Write($"Startup registration {(enabled ? "enabled" : "disabled")}.");
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            RuntimeLog.WriteException("Startup registration", exception);
            error = exception.Message;
            return false;
        }
    }

    private static int? GetPreference()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, writable: false);
            return key?.GetValue(PreferenceValueName) is int preference ? preference : null;
        }
        catch (Exception exception)
        {
            RuntimeLog.WriteException("Read startup preference", exception);
            return null;
        }
    }

    private static string? GetRegisteredCommand()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) as string;
        }
        catch (Exception exception)
        {
            RuntimeLog.WriteException("Read startup registration", exception);
            return null;
        }
    }

    private static string BuildCommand()
    {
        return $"\"{Compat.ProcessPath}\" --autostart";
    }
}
