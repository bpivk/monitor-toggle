using System.Diagnostics;
using Microsoft.Win32;
using RigToggle.Core.Abstractions;

namespace RigToggle.Windows;

/// <summary>
/// Real HKCU "start with Windows" registration (TRAY-02). Always targets
/// Registry.CurrentUser, never LocalMachine -- HKLM would require elevation and
/// reintroduce the UIPI class of bug the v1.0 H9 session worked around (Threat
/// T-08-EOP); the app's asInvoker, non-elevated execution model is a hard constraint
/// (CLAUDE.md, 02-RESEARCH.md Pitfall 6). Enable() resolves the exe path via
/// Environment.ProcessPath, NOT the managed-assembly-location API -- that older API
/// returns an empty string under a PublishSingleFile self-contained publish
/// (08-RESEARCH.md Pitfall 5), which would silently write a broken Run entry. Genuine
/// registry
/// mutation failures are allowed to propagate uncaught -- the UI layer (Plan 08-03)
/// decides how to degrade a failed Enable/Disable, this adapter does not swallow them.
/// </summary>
public sealed class WindowsAutostartConfigurator : IAutostartConfigurator
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RigToggle";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("Could not resolve the running executable's path.");
        }

        // Re-written unconditionally on every call so a stale path (e.g. after moving
        // the exe) self-heals the next time the user re-confirms autostart in Settings.
        // The --tray suffix ties this Run entry to Plan 08-03's hidden-startup branch.
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{exePath}\" --tray");
        Log($"Enable: wrote Run value '{ValueName}' = \"{exePath}\" --tray");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
        Log($"Disable: removed Run value '{ValueName}' (no-op if already absent)");
    }

    // Best-effort diagnostic logging, matching WindowsAppController's convention --
    // routed through Trace.WriteLine so RigToggle.App's TextWriterTraceListener
    // persists it to the same opt-in debug.log. Never throws.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsAutostartConfigurator: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect autostart behavior.
        }
    }
}
