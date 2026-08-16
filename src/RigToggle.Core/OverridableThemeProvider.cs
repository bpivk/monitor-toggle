using System.Drawing;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// Decorator over IThemeProvider that resolves the app's effective theme as
/// preview ?? persisted ThemeOverride ?? live OS signal (THEME-09, D-04). This is
/// the single shared resolver every IsDark/IsDarkTheme copy in the app reads
/// through once wired in at the composition root (Program.cs) -- Pitfall 6's
/// prescribed fix for the three independently-mirrored resolution points
/// (MainForm.IsDark, SettingsForm.IsDarkTheme, MonitorConfirmDialog.IsDark). None
/// of those three property bodies change; the composition-root swap alone gives
/// them override awareness.
///
/// The persisted override is memoized and re-read only in the constructor and
/// RefreshOverride() -- never per CurrentTheme read. IsDark is read from inside
/// OnPaint handlers (e.g. MainForm.BtnIdentify_Paint), so a per-read settings-file
/// load would become a per-repaint file load (T-23-01). _inner.CurrentTheme is
/// always read live, never memoized -- only the persisted override is cached.
///
/// AccentColor/AccentColorChanged are pure pass-throughs to the inner provider --
/// THEME-09 is scoped to the light/dark axis only and must not alter accent
/// behavior.
///
/// ThemeChanged is re-raised unconditionally on every inner OS flip, deliberately
/// NOT diffed against the effective theme (a documented deviation from
/// ARCHITECTURE.md's suggested refinement, see 23-01-PLAN.md interfaces): every
/// subscriber's re-application of the effective color mode is idempotent and
/// cosmetic-only, and an unconditional relay actively repairs any native-control
/// drift WinForms' own OS-following may have introduced underneath a locked
/// override -- suppressing the event would leave that drift uncorrected.
/// </summary>
public sealed class OverridableThemeProvider : IThemeProvider
{
    private readonly IThemeProvider _inner;
    private readonly ISettingsStore _settingsStore;

    // Mirrors WindowsThemeProvider's _themeLock discipline: assign preview/memo
    // state under the lock, raise events outside it. The inner ThemeChanged relay
    // can arrive on a non-UI thread while the UI thread concurrently sets a
    // preview (T-23-06).
    private readonly object _lock = new();
    private bool _hasPreview;
    private AppTheme? _previewOverride;
    private AppTheme? _persistedOverride;

    public event EventHandler? ThemeChanged;

    public OverridableThemeProvider(IThemeProvider inner, ISettingsStore settingsStore)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _persistedOverride = ReadPersistedOverride();
        _inner.ThemeChanged += OnInnerThemeChanged;
    }

    public AppTheme CurrentTheme
    {
        get
        {
            lock (_lock)
            {
                if (_hasPreview)
                {
                    return _previewOverride ?? _inner.CurrentTheme;
                }

                return _persistedOverride ?? _inner.CurrentTheme;
            }
        }
    }

    public Color AccentColor => _inner.AccentColor;

    public event EventHandler? AccentColorChanged
    {
        add => _inner.AccentColorChanged += value;
        remove => _inner.AccentColorChanged -= value;
    }

    /// <summary>
    /// Records an unsaved live-preview override (D-01), including previewing null
    /// (System) while Light/Dark is persisted -- distinguished from "no preview
    /// active" via a separate boolean flag, not a nullable-of-nullable. Always
    /// raises ThemeChanged, even when the effective theme did not change, so every
    /// subscriber re-applies the effective color mode.
    /// </summary>
    public void SetPreviewOverride(AppTheme? previewOverride)
    {
        lock (_lock)
        {
            _hasPreview = true;
            _previewOverride = previewOverride;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Drops any active preview and re-reads the persisted override from the
    /// store into the memo. Serves both post-Save (D-01: the just-saved value
    /// becomes the new persisted truth) and revert-on-Discard/close (D-02/D-03:
    /// dropping an unsaved preview falls back to whatever was last actually
    /// persisted) -- one method, two roles, by design; do not add a second method
    /// for the other role.
    /// </summary>
    public void RefreshOverride()
    {
        lock (_lock)
        {
            _hasPreview = false;
            _previewOverride = null;
            _persistedOverride = ReadPersistedOverride();
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnInnerThemeChanged(object? sender, EventArgs e) => ThemeChanged?.Invoke(this, EventArgs.Empty);

    // Fail-silent read helper (hard constraint 6): a store whose Load() throws, or
    // a persisted ThemeOverride value that is not a defined AppTheme member
    // (System.Text.Json deserializes an out-of-range integer into an undefined
    // enum value without throwing), both degrade to null (System/live-follow). No
    // logging, no rethrow, no user-facing error -- this is a cosmetic path (Phase
    // 12/21 convention).
    private AppTheme? ReadPersistedOverride()
    {
        try
        {
            var value = _settingsStore.Load().ThemeOverride;
            return value is { } theme && Enum.IsDefined(theme) ? theme : null;
        }
        catch
        {
            return null;
        }
    }
}
