using System.Diagnostics;
using System.Drawing;
using Microsoft.Win32;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Windows;

/// <summary>
/// Real HKCU app-theme reader + live-change notifier (THEME-01/THEME-02), plus the live
/// Windows accent-color reader + notifier (THEME-07). Reads the AppsUseLightTheme value
/// (D-06) -- this governs app/window chrome (this project's concern). A separate,
/// differently-named taskbar/tray-coloring value exists in the same registry key and
/// governs independently; conflating the two is a documented pitfall -- this class
/// deliberately reads only AppsUseLightTheme. Registry-read failures (missing key/value,
/// unexpected type) default to AppTheme.Light rather than throwing, matching this
/// codebase's "never throw from a Load-time read" convention (same posture as
/// WindowsAutostartConfigurator.IsEnabled()'s null-safe `?.` chain). ThemeChanged and
/// AccentColorChanged may both fire off the UI thread -- SystemEvents.UserPreferenceChanged
/// is not guaranteed to raise on the subscriber's thread -- so callers MUST marshal back
/// to their own thread (e.g. WinForms InvokeRequired/BeginInvoke) before touching any
/// control.
///
/// AccentColor resolves registry-first (HKCU\Software\Microsoft\Windows\DWM\AccentColor)
/// with a DwmGetColorizationColor fallback (D-01), never throwing from either read.
/// </summary>
public sealed class WindowsThemeProvider : IThemeProvider, IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    private const string AccentKeyPath = @"Software\Microsoft\Windows\DWM";
    private const string AccentValueName = "AccentColor";

    // WR-02: guards the read-compare-write below and the CurrentTheme getter, per this
    // class's own documented "ThemeChanged may fire off the UI thread" contract -- two
    // rapid UserPreferenceChanged events could otherwise race the diff-and-assign.
    private readonly object _themeLock = new();
    private AppTheme _currentTheme;

    // Separate lock from _themeLock -- accent reads must never serialize behind theme
    // reads (and vice versa); the two values are independent (T-21-05).
    private readonly object _accentLock = new();
    private Color _accentColor;

    public AppTheme CurrentTheme
    {
        get { lock (_themeLock) { return _currentTheme; } }
        private set { _currentTheme = value; }
    }

    public event EventHandler? ThemeChanged;

    public Color AccentColor
    {
        get { lock (_accentLock) { return _accentColor; } }
    }

    public event EventHandler? AccentColorChanged;

    public WindowsThemeProvider()
    {
        CurrentTheme = ReadThemeFromRegistry();
        _accentColor = ReadAccentColor();
        Log($"Constructed: initial theme resolved to {CurrentTheme}, accent resolved to {_accentColor}");
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    // UserPreferenceChanged fires for many unrelated preference categories (A1), not
    // just theme changes -- deliberately left unfiltered by UserPreferenceCategory
    // (the safer fallback per research) and instead diffed against the last-known
    // value so ThemeChanged/AccentColorChanged only raise on a genuine flip (T-12-02).
    // This same unfiltered-then-diffed treatment now covers two independent values.
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

        var resolvedAccent = ReadAccentColor();
        Color previousAccent;
        bool accentChanged;
        lock (_accentLock)
        {
            // WR-02: compare by ARGB pixel value, not Color's full struct equality.
            // ReadAccentColorFromDwm()'s failure paths return SystemColors.Highlight (a
            // KnownColor-backed Color), while every successful read on both paths
            // returns a plain Color.FromArgb(...) value. Color's == operator compares
            // the full internal state (including KnownColor/state flags), so two colors
            // with numerically identical R/G/B still compare != if one is a known-color
            // and the other isn't -- ToArgb() only compares the 32-bit pixel value and
            // is immune to that, matching this method's documented "only raise on a
            // genuine flip" contract.
            accentChanged = resolvedAccent.ToArgb() != _accentColor.ToArgb();
            previousAccent = _accentColor;
            if (accentChanged)
            {
                _accentColor = resolvedAccent;
            }
        }

        if (accentChanged)
        {
            Log($"Accent color flip detected: {previousAccent} -> {resolvedAccent}");
            AccentColorChanged?.Invoke(this, EventArgs.Empty);
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

    // Registry primary read for AccentColor (D-01). The stored DWORD is ABGR (R in the
    // low byte), evidenced by 21-RESEARCH.md's own cited worked example: 0xffc77e35
    // resolves to #357EC7 (a blue, matching the source's claim of Windows-10 default
    // blue), not #C77E35 (an orange) -- the "IMPORTANT correction" paragraph in
    // 21-RESEARCH.md and 21-UI-SPEC.md's source table row 1 both assert this uses the
    // same arithmetic as the DWM path below, but that is contradicted by the same
    // document's own worked example. Plan 03's rig checklist resolves this numerically
    // against the live machine as the final decider.
    private static Color? ReadAccentColorFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AccentKeyPath, writable: false);
            var raw = key?.GetValue(AccentValueName);
            if (raw is not int i)
            {
                return null;
            }

            return FromAbgrRegistryDword(unchecked((uint)i));
        }
        catch
        {
            return null;
        }
    }

    // WR-01: pure, side-effect-free bit-math extracted out of ReadAccentColorFromRegistry
    // so it is directly unit-testable with a synthetic uint (no registry I/O needed) --
    // see WindowsThemeProviderTests.cs, which pins the exact worked example cited above
    // (0xffc77e35 -> #357EC7) so a future edit that flips a mask fails the build instead
    // of waiting for the next manual rig pass.
    internal static Color FromAbgrRegistryDword(uint v) =>
        Color.FromArgb(
            (byte)(v & 0x000000FF),
            (byte)((v >> 8) & 0x000000FF),
            (byte)((v >> 16) & 0x000000FF));

    // DWM fallback read for AccentColor, used only when the registry value above is
    // absent or unreadable (D-01). DwmGetColorizationColor is documented by Microsoft as
    // 0xAARRGGBB (R at bit 16) -- the opposite byte order from the registry read above.
    // See ReadAccentColorFromRegistry's comment for the full byte-order justification.
    private static Color ReadAccentColorFromDwm()
    {
        try
        {
            int hr = NativeMethods.DwmGetColorizationColor(out uint colorization, out _);
            if (hr != 0)
            {
                return SystemColors.Highlight;
            }

            return FromArgbDwmDword(colorization);
        }
        catch
        {
            return SystemColors.Highlight;
        }
    }

    // WR-01: pure, side-effect-free bit-math extracted out of ReadAccentColorFromDwm so
    // it is directly unit-testable with a synthetic uint (no live DWM call needed) --
    // see WindowsThemeProviderTests.cs.
    internal static Color FromArgbDwmDword(uint v) =>
        Color.FromArgb(
            (byte)((v >> 16) & 0x000000FF),
            (byte)((v >> 8) & 0x000000FF),
            (byte)(v & 0x000000FF));

    // Registry primary, DWM fallback only when the registry value is absent or
    // unreadable (D-01).
    private static Color ReadAccentColor() => ReadAccentColorFromRegistry() ?? ReadAccentColorFromDwm();

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
