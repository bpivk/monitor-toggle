namespace RigToggle.Core.Models;

/// <summary>
/// Persisted user settings (selected monitors, audio device pair, companion app path).
/// Serialized as-is to %LocalAppData%\RigToggle\settings.json via ISettingsStore.
/// All fields nullable: a null field means "never configured" (first run), not "stale"
/// (see 02-RESEARCH.md Pattern 2 / Pitfall 3 for the first-run-vs-stale UI distinction).
/// MonitorsToDisable/MonitorsToEnable are the live v1.1 multi-monitor sets (DevicePath
/// strings) applied on toggle-to-rig and mirrored on toggle-back (06-RESEARCH.md).
/// MonitorDevicePath/MonitorFriendlyName are retained ONLY as the legacy v1.0 migration
/// source (JsonSettingsStore.Load(), D-08) — do not delete or repurpose them.
/// HotkeyModifiers/HotkeyKey are both null-until-configured per D-02 (no default global
/// hotkey) and, together, hold the Win32 modifier bit-mask (see HotkeyCombo.Mod*) and
/// virtual-key code of the configured global hotkey (TRIG-01).
/// CloseMinimizesToTray true = window Close (X) hides to tray, false = Close exits the
/// app (default-false on upgrade, D-02); MinimizeToTray true = the minimize button also
/// hides to tray, false = standard OS minimize (default-false, D-05).
/// ThemeOverride null = follow the live Windows theme (System, THEME-01..06 unchanged);
/// AppTheme.Light/AppTheme.Dark locks the app's effective theme regardless of the live
/// OS signal (THEME-09, D-04) -- resolved by RigToggle.Core.OverridableThemeProvider.
/// SkippedUpdateVersion null = no version has been skipped; a non-null value is the
/// exact release tag (e.g. "v2.3") the user chose to skip via UpdatePromptDialog's
/// "Skip this version" button (D-02, UPDATE-06). Any release whose version compares
/// strictly newer than that tag (via UpdateVersionComparer, never a string match)
/// still prompts on the next automatic launch -- a skip suppresses exactly one
/// version, never every future one.
/// </summary>
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public List<string>? MonitorsToDisable { get; set; }
    public List<string>? MonitorsToEnable { get; set; }
    public List<string>? NormalMonitorsToDisable { get; set; }
    public List<string>? NormalMonitorsToEnable { get; set; }
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
    public bool SkipMonitorConfirmation { get; set; }
    public bool EnableDebugLogging { get; set; }
    public bool CloseMinimizesToTray { get; set; }
    public bool MinimizeToTray { get; set; }
    public int? HotkeyModifiers { get; set; }
    public int? HotkeyKey { get; set; }
    public AppTheme? ThemeOverride { get; set; }
    public string? SkippedUpdateVersion { get; set; }
}
