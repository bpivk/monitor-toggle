using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

namespace RigToggle.Windows;

/// <summary>
/// Real monitor enumeration and full-topology capture via WindowsDisplayAPI's CCD
/// wrapper (proven non-elevated on this rig's AMD/DisplayPort hardware by the Phase 1
/// spike, spike/MonitorDetachSpike/Program.cs RunList()). CaptureState() is real
/// starting Plan 02 (04-RESEARCH.md Pattern 2); Disable/Restore remain documented
/// no-op stubs until Plan 03 fills in the real CCD repositioning-aware
/// topology-removal mutation and live-identity restore (04-RESEARCH.md Patterns 1/3/4).
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

    // Real full-topology capture (04-RESEARCH.md Pattern 2): one MonitorPathSnapshot
    // per active PathTargetInfo across ALL active paths — not just the configured
    // target — because Plan 03's repositioning-aware disable shifts every surviving
    // path's coordinates, so restore must undo that shift for every display
    // (DISPLAY-02). The five CCD enums are cast to their underlying int so
    // RigToggle.Core stays free of WindowsDisplayAPI references. TargetDevicePath is
    // set to the active GDI-primary's device path (the monitor the app is configured
    // to disable is the current primary at capture time), falling back to the first
    // captured entry if no path reports itself as primary.
    public MonitorState CaptureState()
    {
        PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);

        var snapshots = activePaths
            .SelectMany(p => p.TargetsInfo.Select(t => new MonitorPathSnapshot(
                t.DisplayTarget.DevicePath,
                t.DisplayTarget.FriendlyName ?? "(unknown display)",
                p.Position.X, p.Position.Y,
                p.Resolution.Width, p.Resolution.Height,
                (int)p.PixelFormat,
                (int)t.Rotation,
                (int)t.Scaling,
                (int)t.OutputTechnology,
                t.FrequencyInMillihertz,
                (int)t.ScanLineOrdering,
                p.IsGDIPrimary)))
            .ToList();

        string targetDevicePath = snapshots.FirstOrDefault(s => s.IsPrimary)?.DevicePath
            ?? snapshots.FirstOrDefault()?.DevicePath
            ?? string.Empty;

        return new MonitorState(snapshots, targetDevicePath);
    }

    public void Disable(string monitorDevicePath)
    {
        // FAKE in Plan 02 — no-op. Real CCD repositioning-aware topology-path-removal
        // via PathInfo.ApplyPathInfos(reducedPaths, allowChanges: true) — excluding the
        // target monitor's path and shifting surviving paths per 04-RESEARCH.md
        // Pattern 1 — lands in Plan 03. Do NOT reuse the spike's RunDisable/VerifyOnce
        // logic here (known primary-monitor repositioning gap, Plan 03 scope).
    }

    public void Restore(MonitorState previousState)
    {
        // FAKE in Plan 02 — no-op. Real PathInfo.ApplyPathInfos(originalActivePaths,
        // allowChanges: true) restore using previousState.Paths (04-RESEARCH.md
        // Pattern 3/4, live-identity restore) lands in Plan 03.
    }
}
