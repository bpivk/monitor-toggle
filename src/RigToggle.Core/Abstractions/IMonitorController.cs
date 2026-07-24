using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Monitor enumeration and CCD-level disable/restore contract. Implemented by
/// RigToggle.Windows.WindowsMonitorController. Read methods (GetActiveMonitors,
/// CaptureState) are real starting Phase 2; mutating methods (Disable, Restore)
/// are no-op stubs until Phase 4 (02-RESEARCH.md Pattern 1).
/// </summary>
public interface IMonitorController
{
    IReadOnlyList<MonitorInfo> GetActiveMonitors();
    MonitorState CaptureState(string monitorDevicePath);
    void Disable(string monitorDevicePath);
    void Restore(MonitorState previousState);
}
