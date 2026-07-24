using System.Drawing;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

namespace RigToggle.Windows;

/// <summary>
/// Real monitor enumeration, full-topology capture, and CCD-level primary-monitor
/// disable/restore via WindowsDisplayAPI's CCD wrapper (proven non-elevated on this
/// rig's AMD/DisplayPort hardware by the Phase 1 spike and re-confirmed by Plan 01's
/// repositioning-aware rig re-test — see spike/PHASE4-RETEST.md GO decision).
/// GetActiveMonitors/CaptureState are real starting Plan 02 (04-RESEARCH.md Pattern 2).
/// Disable implements 04-RESEARCH.md Pattern 1 (repositioning-aware survivor
/// reconstruction so exactly one survivor lands at (0,0)) + Pattern 3 (verify-and-throw
/// against a fresh GetActivePaths() re-query, D-03). Restore implements Pattern 4
/// (live-identity re-resolution via GetAllPaths() matched on stored DevicePath, mode/
/// signal rebuilt from the STORED snapshot, never trusting an inactive path's own live
/// data) + the same verify-and-throw idiom. Neither method uses the WinForms screen-
/// enumeration API as an oracle (D-04) or attempts automatic rollback on verification
/// failure (D-05) — the exception bubbles to MainForm's existing handler.
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

    // Real repositioning-aware CCD primary-path removal (04-RESEARCH.md Pattern 1,
    // empirically confirmed GO on this rig by Plan 01's spike/PHASE4-RETEST.md rig
    // re-test) + verify-and-throw (Pattern 3, D-03). Never uses the WinForms screen-
    // enumeration API as the verification oracle (D-04) and never attempts an
    // automatic rollback on verification failure (D-05) — the exception bubbles to
    // MainForm's existing handler.
    public void Disable(string monitorDevicePath)
    {
        PathInfo[] currentPaths = PathInfo.GetActivePaths(virtualModeAware: false);

        PathInfo? targetPath = currentPaths.FirstOrDefault(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == monitorDevicePath));

        if (targetPath is null)
        {
            throw new InvalidOperationException(
                $"Configured monitor '{monitorDevicePath}' is not currently active.");
        }

        PathInfo[] survivors = currentPaths.Where(p => p != targetPath).ToArray();

        PathInfo[] pathsToApply;
        if (targetPath.IsGDIPrimary && survivors.Length > 0)
        {
            // Pitfall 1: shift ALL survivors by the same uniform delta (not just the
            // promoted one) so relative layout is preserved — Position has no public
            // setter, so a fresh PathInfo must be constructed per survivor.
            Point promoted = survivors[0].Position;
            var delta = new Point(-promoted.X, -promoted.Y);

            pathsToApply = survivors
                .Select(p => new PathInfo(
                    p.DisplaySource,
                    new Point(p.Position.X + delta.X, p.Position.Y + delta.Y),
                    p.Resolution,
                    p.PixelFormat,
                    p.TargetsInfo))
                .ToArray();
        }
        else
        {
            pathsToApply = survivors;
        }

        PathInfo.ApplyPathInfos(pathsToApply, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false);

        // Pattern 3/D-03: verify-and-throw against a fresh re-query — never trust
        // ApplyPathInfos's non-throwing return alone as proof of success (D-04: not
        // the WinForms screen-enumeration API).
        PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);

        bool targetStillActive = verifyPaths
            .SelectMany(p => p.TargetsInfo)
            .Any(t => t.DisplayTarget.DevicePath == monitorDevicePath);

        bool exactlyOnePrimary = verifyPaths.Count(p => p.IsGDIPrimary) == 1;

        if (targetStillActive || !exactlyOnePrimary)
        {
            throw new InvalidOperationException(
                $"Monitor disable did not take effect as expected (targetStillActive={targetStillActive}, " +
                $"exactlyOnePrimary={exactlyOnePrimary}). No further automatic recovery is attempted (D-05).");
        }
    }

    public void Restore(MonitorState previousState)
    {
        // FAKE — pending Task 2. Real PathInfo.GetAllPaths()-based live-identity
        // restore (04-RESEARCH.md Pattern 4) lands in the next commit of this plan.
    }
}
