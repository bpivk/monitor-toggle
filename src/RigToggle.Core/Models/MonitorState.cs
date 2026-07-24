namespace RigToggle.Core.Models;

/// <summary>
/// Captured monitor state at toggle-time, used to restore the exact prior configuration.
/// Phase-4 full-topology shape: one MonitorPathSnapshot per active display path present
/// at capture time (not just the target), because Plan 03's repositioning-aware disable
/// shifts every surviving path's coordinates — restore must undo that shift for every
/// display, not just reactivate the disabled one (DISPLAY-02). TargetDevicePath names
/// which entry is the configured monitor to disable, so Disable/Restore need no second
/// settings read.
/// </summary>
public sealed record MonitorState(IReadOnlyList<MonitorPathSnapshot> Paths, string TargetDevicePath);
