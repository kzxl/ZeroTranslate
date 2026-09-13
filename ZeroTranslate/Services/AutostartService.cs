using Microsoft.Win32;

namespace ZeroTranslate.Services;

/// <summary>
/// Manages the Windows "start with Windows" autostart entry by writing to the
/// per-user Run registry key (HKCU). No admin rights required.
/// </summary>
public static class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "ZeroTranslate";

    /// <summary>
    /// Enable or disable launching ZeroTranslate when the current user logs in.
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = GetExecutablePath();
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AppValueName, $"\"{exePath}\"");
            }
            else if (key.GetValue(AppValueName) != null)
            {
                key.DeleteValue(AppValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Autostart: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns true if the autostart entry currently points at this executable.
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        // Use the actual host process path (works for single-file published apps).
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
            return path;

        return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
    }
}
