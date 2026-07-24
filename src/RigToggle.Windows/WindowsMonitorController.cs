using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

namespace RigToggle.Windows;

/// <summary>
/// Real monitor enumeration via WindowsDisplayAPI's CCD wrapper (proven non-elevated
/// on this rig's AMD/DisplayPort hardware by the Phase 1 spike,
/// spike/MonitorDetachSpike/Program.cs RunList()). Disable/Restore are documented
/// no-op stubs until Phase 4 fills in the real CCD topology-removal mutation
/// (02-RESEARCH.md Pattern 1).
/// </summary>
public sealed class WindowsMonitorController : IMonitorController
{
    public IReadOnlyList<MonitorInfo> GetActiveMonitors()
    {
        PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);
        var result = new List<MonitorInfo>();

        foreach (PathInfo path in activePaths)
        {
            foreach (PathTargetInfo targetInfo in path.TargetsInfo)
            {
                PathDisplayTarget target = targetInfo.DisplayTarget;
                result.Add(new MonitorInfo(
                    DevicePath: target.DevicePath,
                    FriendlyName: target.FriendlyName ?? "(unknown display)",
                    IsPrimary: path.IsGDIPrimary));
            }
        }

        return result;
    }

    // Real read today (a stable device-path snapshot); Phase 4 may enrich this with
    // the full DISPLAYCONFIG_PATH_INFO/MODE_INFO arrays needed for a true CCD restore
    // (02-RESEARCH.md Pitfall 7 / Pattern 1).
    public MonitorState CaptureState(string monitorDevicePath)
        => new MonitorState(monitorDevicePath);

    public void Disable(string monitorDevicePath)
    {
        // FAKE in Phase 2 — no-op. Real CCD topology-path-removal via
        // PathInfo.ApplyPathInfos(reducedPaths, allowChanges: true) — excluding the
        // target monitor's path — lands in Phase 4. Do NOT reuse the spike's
        // RunDisable/VerifyOnce logic here (known primary-monitor repositioning gap,
        // Phase 4 scope per 02-CONTEXT.md).
    }

    public void Restore(MonitorState previousState)
    {
        // FAKE in Phase 2 — no-op. Real PathInfo.ApplyPathInfos(originalActivePaths,
        // allowChanges: true) restore lands in Phase 4.
    }
}
