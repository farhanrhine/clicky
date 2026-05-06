// Core/StartupRegistration.cs
// Registers or removes Clicky from Windows startup via the Registry.
// HKCU\Software\Microsoft\Windows\CurrentVersion\Run
// Using HKCU (current user) avoids needing admin rights.

using Microsoft.Win32;

namespace ClickyWindows;

internal static class StartupRegistration
{
    private const string RegistryKeyPath
        = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Clicky";

    public static bool IsRegisteredForStartup
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(AppName) != null;
        }
    }

    public static void RegisterForStartup()
    {
        string exePath = System.Diagnostics.Process
            .GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(exePath)) return;

        using var key = Registry.CurrentUser.OpenSubKey(
            RegistryKeyPath, writable: true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void UnregisterFromStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            RegistryKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
