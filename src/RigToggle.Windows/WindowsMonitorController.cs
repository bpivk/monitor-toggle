using System.Drawing;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;

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

    // Real live-identity-reconstruction CCD restore (04-RESEARCH.md Pattern 4) +
    // verify-and-throw (D-03). Identity objects (PathDisplaySource/PathDisplayTarget)
    // are always re-resolved live via GetAllPaths() matched on the stable stored
    // DevicePath — never reconstructed from a persisted, session-scoped LUID. Mode/
    // signal values (position/resolution/pixel format/rotation/scaling/frequency/scan-
    // line ordering) come from the STORED snapshot, never from an inactive live path's
    // own TargetsInfo (Pitfall 2 — Microsoft docs: inactive-path mode/signal info is
    // "set to default values"). A missing live match throws rather than silently
    // skipping (Pitfall 5). Never uses the WinForms screen-enumeration API as the
    // oracle (D-04) and never attempts an automatic rollback on verification failure
    // (D-05).
    public void Restore(MonitorState previousState)
    {
        PathInfo[] liveAllPaths = PathInfo.GetAllPaths(virtualModeAware: false);

        // Resolve every snapshot entry to its live match + live target first (two-pass),
        // before deciding source assignment below — order-independent.
        var resolved = new List<(MonitorPathSnapshot Snap, PathInfo LiveMatch, PathTargetInfo LiveTarget)>();
        foreach (MonitorPathSnapshot snap in previousState.Paths)
        {
            // PathDisplayTarget.DevicePath throws TargetNotAvailableException when
            // IsAvailable is false (confirmed against WindowsDisplayAPI source —
            // PathDisplayTarget.cs guards every EDID-derived property, including
            // DevicePath, behind IsAvailable). GetAllPaths() returns many inactive
            // target slots beyond just our two physical monitors (unused GPU output
            // ports etc.), and those report IsAvailable=false — reading .DevicePath
            // on them unconditionally while searching crashed Restore() on the rig
            // (observed: TargetNotAvailableException), even though the target we
            // were actually looking for was never at fault. Guard with IsAvailable
            // first so the search only reads DevicePath on targets where it's safe.
            PathInfo? liveMatch = liveAllPaths.FirstOrDefault(p =>
                p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == snap.DevicePath));

            if (liveMatch is null)
            {
                throw new InvalidOperationException(
                    $"Cannot restore '{snap.FriendlyName}' ({snap.DevicePath}) — no longer detected.");
            }

            PathTargetInfo liveTarget = liveMatch.TargetsInfo.First(t =>
                t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == snap.DevicePath);

            resolved.Add((snap, liveMatch, liveTarget));
        }

        // MonitorPathSnapshot deliberately stores no source-slot identity (Plan 02 —
        // stays JSON-primitive, no WindowsDisplayAPI reference in Core), so the source
        // to reattach an INACTIVE target to must be re-derived here. Trusting the
        // inactive target's own live-reported DisplaySource (from GetAllPaths) is NOT
        // safe — observed live on the rig as PathChangeException ("Invalid paths
        // information.") from SetDisplayConfig's validation, most likely because that
        // reported source collided with the source the still-active survivor monitor
        // is already using (two paths cannot legally share one source). Reserve every
        // ACTIVE target's own (correct, currently-in-use) source first, then assign
        // each INACTIVE target the first source not already reserved.
        var usedSources = new HashSet<PathDisplaySource>(
            resolved.Where(r => r.LiveTarget.IsPathActive).Select(r => r.LiveMatch.DisplaySource));
        PathDisplaySource[] allSources = PathDisplaySource.GetDisplaySources();

        var rebuilt = new List<PathInfo>();
        foreach (var (snap, liveMatch, liveTarget) in resolved)
        {
            PathDisplaySource sourceToUse;
            if (liveTarget.IsPathActive)
            {
                sourceToUse = liveMatch.DisplaySource;
            }
            else
            {
                sourceToUse = allSources.FirstOrDefault(s => !usedSources.Contains(s))
                    ?? throw new InvalidOperationException(
                        $"Cannot restore '{snap.FriendlyName}' ({snap.DevicePath}) — no free display source available.");
                usedSources.Add(sourceToUse);
            }

            var reconstructedTarget = new PathTargetInfo(
                liveTarget.DisplayTarget,
                snap.FrequencyInMillihertz,
                (DisplayConfigScanLineOrdering)snap.ScanLineOrdering,
                (DisplayConfigRotation)snap.Rotation,
                (DisplayConfigScaling)snap.Scaling);

            rebuilt.Add(new PathInfo(
                sourceToUse,
                new Point(snap.PositionX, snap.PositionY),
                new Size(snap.ResolutionWidth, snap.ResolutionHeight),
                (DisplayConfigPixelFormat)snap.PixelFormat,
                new[] { reconstructedTarget }));
        }

        try
        {
            PathInfo.ApplyPathInfos(rebuilt.ToArray(), allowChanges: true);
        }
        catch (WindowsDisplayAPI.Exceptions.PathChangeException ex)
        {
            // Extra diagnostic detail beyond the library's generic message, since
            // ValidatePathInfos discards the underlying Win32 error code entirely —
            // this is the best available signal if source assignment is still wrong.
            string attempted = string.Join("; ", rebuilt.Select(p =>
                $"{p.TargetsInfo.First().DisplayTarget.FriendlyName ?? "?"}@source={p.DisplaySource.SourceId} pos=({p.Position.X},{p.Position.Y})"));
            throw new InvalidOperationException(
                $"Monitor restore failed CCD validation: {ex.Message} Attempted topology: {attempted}", ex);
        }

        // Pattern 4/D-03: verify-and-throw — confirm the configured target is present
        // again and matches its stored position/primary designation.
        PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);
        PathInfo? restoredTarget = verifyPaths.FirstOrDefault(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == previousState.TargetDevicePath));
        MonitorPathSnapshot expectedSnap = previousState.Paths.First(s => s.DevicePath == previousState.TargetDevicePath);

        if (restoredTarget is null ||
            restoredTarget.Position.X != expectedSnap.PositionX ||
            restoredTarget.Position.Y != expectedSnap.PositionY ||
            restoredTarget.IsGDIPrimary != expectedSnap.IsPrimary)
        {
            throw new InvalidOperationException(
                "Monitor restore did not reproduce the exact prior configuration. No further automatic recovery is attempted (D-05).");
        }
    }
}
