using System.Diagnostics;
using Microsoft.Win32;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Windows;

/// <summary>
/// Real HKCU app-theme reader + live-change notifier (THEME-01/THEME-02). Reads the
/// AppsUseLightTheme value (D-06) -- this governs app/window chrome (this project's
/// concern). A separate, differently-named taskbar/tray-coloring value exists in the
/// same registry key and governs independently; conflating the two is a documented
/// pitfall -- this class deliberately reads only AppsUseLightTheme. Registry-read failures
/// (missing key/value, unexpected type) default to AppTheme.Light rather than throwing,
/// matching this codebase's "never throw from a Load-time read" convention (same posture
/// as WindowsAutostartConfigurator.IsEnabled()'s null-safe `?.` chain). ThemeChanged may
/// fire off the UI thread -- SystemEvents.UserPreferenceChanged is not guaranteed to
/// raise on the subscriber's thread -- so callers MUST marshal back to their own thread
/// (e.g. WinForms InvokeRequired/BeginInvoke) before touching any control.
/// </summary>
public sealed class WindowsThemeProvider : IThemeProvider, IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    // WR-02: guards the read-compare-write below and the CurrentTheme getter, per this
    // class's own documented "ThemeChanged may fire off the UI thread" contract -- two
    // rapid UserPreferenceChanged events could otherwise race the diff-and-assign.
    private readonly object _themeLock = new();
    private AppTheme _currentTheme;

    public AppTheme CurrentTheme
    {
        get { lock (_themeLock) { return _currentTheme; } }
        private set { _currentTheme = value; }
    }

    public event EventHandler? ThemeChanged;

    public WindowsThemeProvider()
    {
        CurrentTheme = ReadThemeFromRegistry();
        Log($"Constructed: initial theme resolved to {CurrentTheme}");
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    // UserPreferenceChanged fires for many unrelated preference categories (A1), not
    // just theme changes -- deliberately left unfiltered by UserPreferenceCategory
    // (the safer fallback per research) and instead diffed against the last-known
    // theme so ThemeChanged only raises on a genuine Light<->Dark flip (T-12-02).
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        var resolved = ReadThemeFromRegistry();
        AppTheme previous;
        bool changed;
        lock (_themeLock)
        {
            changed = resolved != _currentTheme;
            previous = _currentTheme;
            if (changed)
            {
                _currentTheme = resolved;
            }
        }

        if (changed)
        {
            Log($"Theme flip detected: {previous} -> {resolved}");
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static AppTheme ReadThemeFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
            var raw = key?.GetValue(ValueName);
            return raw is int i && i == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    // Best-effort diagnostic logging, matching WindowsAutostartConfigurator's convention --
    // routed through Trace.WriteLine so RigToggle.App's TextWriterTraceListener
    // persists it to the same opt-in debug.log. Never throws.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsThemeProvider: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect theme detection.
        }
    }
}
