namespace RigToggle.Core.Models;

/// <summary>
/// Captured monitor state at toggle-time, used to restore the exact prior configuration.
/// Phase-2-minimal: stores only the target monitor's device path. Phase 4 may enrich this
/// with the full DISPLAYCONFIG_PATH_INFO/MODE_INFO arrays needed for a true CCD restore
/// (02-RESEARCH.md Pitfall 7 / Pattern 1).
/// </summary>
public sealed record MonitorState(string MonitorDevicePath);
