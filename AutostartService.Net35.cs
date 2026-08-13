#if NET35
using System;
using Microsoft.Win32;

namespace PetFriends
{
    internal static class AutostartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string SettingsKeyPath = @"Software\PetFriends";
        private const string ValueName = "PetFriends";
        private const string PreferenceValueName = "StartWithWindows";

        public static bool IsEnabled
        {
            get { return !Compat.IsNullOrWhiteSpace(GetRegisteredCommand()); }
        }

        public static void InitializeDefault()
        {
            int? preference = GetPreference();
            if (preference.HasValue && preference.Value == 0) return;

            string expectedCommand = BuildCommand();
            if (string.Equals(GetRegisteredCommand(), expectedCommand, StringComparison.OrdinalIgnoreCase)) return;

            string error;
            TrySetEnabled(true, out error);
        }

        public static bool TrySetEnabled(bool enabled, out string error)
        {
            try
            {
                using (RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                using (RegistryKey settingsKey = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                {
                    if (runKey == null) throw new InvalidOperationException("???????????????");
                    if (settingsKey == null) throw new InvalidOperationException("???????????");

                    if (enabled)
                    {
                        runKey.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
                        settingsKey.SetValue(PreferenceValueName, 1, RegistryValueKind.DWord);
                    }
                    else
                    {
                        settingsKey.SetValue(PreferenceValueName, 0, RegistryValueKind.DWord);
                        runKey.DeleteValue(ValueName, false);
                    }
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static int? GetPreference()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath))
                {
                    if (key == null) return null;
                    object value = key.GetValue(PreferenceValueName);
                    if (value == null) return null;
                    return Convert.ToInt32(value);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string GetRegisteredCommand()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    return key == null ? null : key.GetValue(ValueName) as string;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string BuildCommand()
        {
            return "\"" + Compat.ProcessPath + "\" --autostart";
        }
    }
}
#endif
