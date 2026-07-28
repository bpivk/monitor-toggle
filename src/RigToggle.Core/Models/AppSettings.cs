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
/// </summary>
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public List<string>? MonitorsToDisable { get; set; }
    public List<string>? MonitorsToEnable { get; set; }
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
    public bool SkipMonitorConfirmation { get; set; }
    public bool EnableDebugLogging { get; set; }
}
