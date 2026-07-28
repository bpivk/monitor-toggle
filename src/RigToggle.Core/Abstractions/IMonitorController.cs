using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Monitor enumeration and CCD-level activate/deactivate/restore contract. Implemented
/// by RigToggle.Windows.WindowsMonitorController. Read methods (GetActiveMonitors,
/// GetAllMonitors, CaptureState) are real starting Phase 2/6; mutating methods
/// (ActivateMonitors, DeactivateMonitors, Restore) are real starting Phase 4/6
/// (02-RESEARCH.md Pattern 1; 04-RESEARCH.md Patterns 1/2/3/4; 06-RESEARCH.md
/// Patterns 1/2/3 for the N-monitor generalization).
/// </summary>
public interface IMonitorController
{
    /// <summary>Currently-active displays only. Used by CaptureState's existing snapshot logic.</summary>
    IReadOnlyList<MonitorInfo> GetActiveMonitors();

    /// <summary>
    /// Active AND currently OS-disabled-but-available displays (06-RESEARCH.md Pattern 3).
    /// Required to resolve an enable-set monitor's friendly name/identity at confirm-time,
    /// since it is inactive by definition before ActivateMonitors runs and therefore cannot
    /// resolve via GetActiveMonitors().
    /// </summary>
    IReadOnlyList<MonitorInfo> GetAllMonitors();

    MonitorState CaptureState();

    /// <summary>
    /// Activates the given OS-disabled monitors via CCD Extend topology (06-RESEARCH.md
    /// Pattern 2) — the N-monitor generalization of the enable-set half of the rig-mode
    /// Monitor step. Must run BEFORE DeactivateMonitors on rig-mode entry (Pitfall 2
    /// ordering constraint).
    /// </summary>
    void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths);

    /// <summary>
    /// Deactivates (true CCD-level detach, not power-off) the given monitors
    /// (06-RESEARCH.md Pattern 1) — the N-monitor generalization of the former
    /// single-target Disable(string). Reused for both the rig-mode disable-set removal
    /// and the toggle-back enable-set teardown (D-02).
    /// </summary>
    void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths);

    void Restore(MonitorState previousState);
}
