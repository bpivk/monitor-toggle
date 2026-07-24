namespace RigToggle.Core.Models;

/// <summary>
/// Persisted user settings (selected monitor, audio device pair, companion app path).
/// Serialized as-is to %LocalAppData%\RigToggle\settings.json via ISettingsStore.
/// All fields nullable: a null field means "never configured" (first run), not "stale"
/// (see 02-RESEARCH.md Pattern 2 / Pitfall 3 for the first-run-vs-stale UI distinction).
/// </summary>
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
}
