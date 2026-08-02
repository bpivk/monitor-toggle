using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Windows app-theme (light/dark) contract (THEME-01/THEME-02). Implemented by
/// RigToggle.Windows.WindowsThemeProvider this phase. The source of truth is the
/// current-user registry value HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\
/// Personalize\AppsUseLightTheme (D-06) -- NOT SystemUsesLightTheme, which governs
/// taskbar/tray coloring independently. ThemeChanged fires on a live OS theme flip
/// while the app is running; it may fire off the UI thread, so subscribers must marshal
/// back to their own thread before touching any control.
/// </summary>
public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;
}
