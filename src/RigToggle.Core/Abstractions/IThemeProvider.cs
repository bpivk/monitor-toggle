using System.Drawing;
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Windows app-theme (light/dark) contract (THEME-01/THEME-02) plus the live Windows
/// accent color contract (THEME-07). Implemented by RigToggle.Windows.WindowsThemeProvider
/// this phase. The theme source of truth is the current-user registry value
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme
/// (D-06) -- NOT SystemUsesLightTheme, which governs taskbar/tray coloring independently.
/// ThemeChanged fires on a live OS theme flip while the app is running; it may fire off
/// the UI thread, so subscribers must marshal back to their own thread before touching
/// any control.
///
/// AccentColor's primary source is HKCU\Software\Microsoft\Windows\DWM\AccentColor, with
/// DwmGetColorizationColor as a fallback when that registry value is absent or unreadable
/// (D-01). Unlike CurrentTheme, AccentColor is theme-independent -- it is the same color
/// in Light and Dark mode, not a per-theme value. It is read live and never persisted (no
/// AppSettings field, no cached-across-runs value). AccentColorChanged is raised from the
/// same SystemEvents.UserPreferenceChanged handler as ThemeChanged (D-02: exactly one
/// subscription for the whole application), so it carries the identical off-UI-thread
/// marshalling warning -- subscribers must marshal back to their own thread before
/// touching any control.
/// </summary>
public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;

    Color AccentColor { get; }

    event EventHandler? AccentColorChanged;
}
