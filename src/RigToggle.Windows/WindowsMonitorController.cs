using System.Drawing;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;

namespace RigToggle.Windows;

/// <summary>
/// Real monitor enumeration, full-topology capture, and CCD-level primary-monitor
/// disable via WindowsDisplayAPI's CCD wrapper (proven non-elevated on this rig's
/// AMD/DisplayPort hardware by the Phase 1 spike and re-confirmed by Plan 01's
/// repositioning-aware rig re-test — see spike/PHASE4-RETEST.md GO decision).
/// GetActiveMonitors/CaptureState are real starting Plan 02 (04-RESEARCH.md Pattern 2).
/// GetAllMonitors/ActivateMonitors/DeactivateMonitors generalize the triad to N
/// monitors starting Phase 6 (06-RESEARCH.md Patterns 1/2/3) — DeactivateMonitors is
/// the direct 1->N generalization of the former single-target Disable(string),
/// implementing 04-RESEARCH.md Pattern 1 (repositioning-aware survivor reconstruction
/// so exactly one survivor lands at (0,0)) + Pattern 3 (verify-and-throw against a
/// fresh GetActivePaths() re-query, D-03, now including a bounding-box overlap
/// check). Neither method uses the WinForms screen-enumeration API as an oracle
/// (D-04) or attempts automatic rollback on verification failure (D-05) — the
/// exception bubbles to MainForm's existing handler.
///
/// This controller no longer implements snapshot-based restore: Phase 16 replaced it
/// with explicit Normal-mode target application via ActivateMonitors/
/// DeactivateMonitors, and Phase 18 (CLEANUP-01) removed the now-dead restore
/// subsystem (the prior reconstruction path, the in-process path cache, and the two
/// unreachable CCD reconstruction helpers). The rig-discovered CCD findings that
/// subsystem's doc comments used to record are preserved in
/// .planning/debug/knowledge-base.md under ccd-topology-restore-findings.
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

    // Real enumeration of active AND currently OS-disabled-but-available displays
    // (06-RESEARCH.md Pattern 3) — the enumeration gap GetActiveMonitors() cannot
    // fill, since DISPLAY-05 requires picking exactly an inactive monitor (e.g. a rig
    // monitor normally kept OS-disabled to save power) from a list. Dedups by the
    // stable DevicePath (GetAllPaths() returns one entry per historical/stale CCD
    // path, so a physical monitor can otherwise appear multiple times — rig-confirmed
    // bug, 06-06-SUMMARY.md NO-GO) and sources Active/Primary state EXCLUSIVELY from
    // GetActiveMonitors() (already correct, GetActivePaths()-based) rather than
    // reading IsGDIPrimary/IsPathActive off potentially-stale inactive PathInfo
    // entries — the same "inactive-path fields are unreliable" landmine already
    // worked around elsewhere in this file (Restore()/DeactivateMonitors()). Targets
    // are filtered to IsAvailable before FriendlyName/DevicePath are touched
    // (06-RESEARCH.md Pitfall 1's "pitfall inside the pitfall" — those getters throw
    // TargetNotAvailableException otherwise). Actual dedup/merge logic lives in the
    // pure, unit-tested MergeAllMonitors() seam below.
    public IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        IReadOnlyList<MonitorInfo> activeMonitors = GetActiveMonitors();

        PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
        var availableTargets = allPaths
            .SelectMany(p => p.TargetsInfo)
            .Where(t => t.DisplayTarget.IsAvailable)
            .Select(t => (t.DisplayTarget.DevicePath, t.DisplayTarget.FriendlyName ?? "(unknown display)"))
            .ToList();

        return MergeAllMonitors(activeMonitors, availableTargets);
    }

    // Pure dedup/merge seam (unit-tested, RigToggle.Windows.Tests — no live CCD
    // hardware needed) extracted from GetAllMonitors() so the gap-closure fix for the
    // rig-confirmed duplicate-rows/multi-primary bug (06-06-SUMMARY.md) is directly
    // testable. Active monitors are emitted first (deduped by DevicePath, promoted to
    // IsActive=true via `with`, IsPrimary preserved from GetActiveMonitors()); any
    // availableTarget whose DevicePath was not already emitted is appended as
    // IsPrimary=false/IsActive=false (a disabled monitor cannot be primary). A single
    // `seen` HashSet spanning both loops guarantees a DevicePath present in both
    // inputs — or duplicated within either input — yields exactly one row.
    internal static IReadOnlyList<MonitorInfo> MergeAllMonitors(
        IReadOnlyList<MonitorInfo> activeMonitors,
        IReadOnlyList<(string DevicePath, string FriendlyName)> availableTargets)
    {
        var seen = new HashSet<string>();
        var result = new List<MonitorInfo>();

        foreach (MonitorInfo m in activeMonitors)
        {
            if (seen.Add(m.DevicePath))
            {
                result.Add(m with { IsActive = true });
            }
        }

        foreach ((string devicePath, string friendlyName) in availableTargets)
        {
            if (seen.Add(devicePath))
            {
                result.Add(new MonitorInfo(devicePath, friendlyName, IsPrimary: false, IsActive: false));
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

    // Real Extend-based activation of previously OS-disabled monitors (06-RESEARCH.md
    // Pattern 2) — the load-bearing generalization answer: NEVER manually reconstruct
    // PathTargetInfo/mode info for a previously-inactive target (already tried and
    // abandoned in this exact codebase's Restore() history, three separate rig-tested
    // validation failures — see Restore()'s own doc comment below). Instead reuse the
    // exact same zero-argument PathInfo.ApplyTopology(Extend) call Restore()'s
    // crash-recovery fallback already proves works: Extend takes no path/mode
    // arguments at all and lets the OS pick mode/position from the CCD persistence
    // database's last-known extend layout for currently-available targets.
    //
    // Pitfall 2 ordering contract (06-RESEARCH.md): this method MUST run BEFORE
    // DeactivateMonitors on rig-mode entry. Extend restores the persistence
    // database's last-known layout, which still includes any disable-set monitor(s)
    // as active if DeactivateMonitors' saveToDatabase:false call already ran first —
    // silently undoing the disable. On toggle-back, the mirror-image rule applies to
    // DeactivateMonitors(enableSet): it must run AFTER Restore(), not before, for the
    // same reason (Restore()'s crash-recovery fallback also uses Extend internally).
    // ToggleService is the enforcement point for this ordering; documented here too so
    // a reader of this adapter alone still understands the contract.
    public void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths)
    {
        if (monitorDevicePaths.Count == 0) return;

        PathInfo[] currentActive = PathInfo.GetActivePaths(virtualModeAware: false);
        var currentlyActiveDevicePaths = currentActive
            .SelectMany(p => p.TargetsInfo)
            .Select(t => t.DisplayTarget.DevicePath)
            .ToHashSet();

        // Skip-optimization (Pitfall 3): Extend recomputes the WHOLE topology from the
        // DB record, not just the newly-added target(s) — it can incidentally
        // reposition an unrelated, already-correct third monitor. If every requested
        // device path is already active, there is nothing to do — never call Extend
        // just to be thorough.
        if (monitorDevicePaths.All(currentlyActiveDevicePaths.Contains)) return;

        // Early availability guard (mirrors Restore() Step 1) — a clear,
        // domain-specific error instead of a confusing generic CCD failure if a
        // configured enable-set monitor is physically unplugged/undetected.
        PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
        var missing = monitorDevicePaths.Where(dp => !allPaths.Any(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == dp))).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Cannot enable monitor(s) — not detected: {string.Join(", ", missing)}");
        }

        PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);

        // Verify-and-throw (D-03/D-04 discipline, unchanged): re-query, confirm every
        // requested device path is now active. Never trust a non-throwing return
        // alone, never use Screen.AllScreens as the oracle. No further automatic
        // recovery is attempted on mismatch (D-05).
        PathInfo[] postExtend = PathInfo.GetActivePaths(virtualModeAware: false);
        var stillInactive = monitorDevicePaths.Except(
            postExtend.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath)).ToArray();

        if (stillInactive.Length > 0)
        {
            throw new InvalidOperationException(
                $"Monitor enable did not take effect: {string.Join(", ", stillInactive)}. " +
                "No further automatic recovery is attempted (D-05).");
        }
    }

    // Real repositioning-aware CCD N-target removal (04-RESEARCH.md Pattern 1,
    // generalized 1->N per 06-RESEARCH.md Pattern 1 — empirically confirmed GO on
    // this rig by Plan 01's spike/PHASE4-RETEST.md rig re-test for the single-target
    // case) + verify-and-throw (Pattern 3, D-03, now including a bounding-box overlap
    // check, T-06-05). Never uses the WinForms screen-enumeration API as the
    // verification oracle (D-04) and never attempts an automatic rollback on
    // verification failure (D-05) — the exception bubbles to MainForm's existing
    // handler.
    //
    // D-02: this same method is reused for BOTH the rig-mode-entry disable-set
    // removal AND the toggle-back enable-set teardown (one primitive, two call
    // sites) — the toggle-back call is an unconditional re-disable, not
    // snapshot-based; that asymmetry is intentional (see ToggleService).
    public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
    {
        if (monitorDevicePaths.Count == 0) return; // no-op, e.g. enable-only config on toggle-back

        PathInfo[] currentPaths = PathInfo.GetActivePaths(virtualModeAware: false);

        PathInfo[] targets = currentPaths
            .Where(p => p.TargetsInfo.Any(t => monitorDevicePaths.Contains(t.DisplayTarget.DevicePath)))
            .ToArray();

        // Generalized "not currently active" guard (was a single not-found check
        // pre-06): compute every requested device path NOT present among any
        // currently-active target.
        var missing = monitorDevicePaths.Except(
            currentPaths.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath)).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Configured monitor(s) not currently active: {string.Join(", ", missing)}");
        }

        PathInfo[] survivors = currentPaths.Where(p => !targets.Contains(p)).ToArray();

        if (survivors.Length == 0)
        {
            // ApplyPathInfos would call ValidatePathInfos on an empty array and throw a
            // generic PathChangeException("Invalid paths information.") with no
            // indication of the actual cause (validation fails before any native
            // mutation — this does NOT blank the screen, but the error is useless).
            // Reachable in production: every currently-active display is configured
            // in the disable-set, or a laptop with only its built-in display, when
            // Switch to Rig Mode is pressed.
            throw new InvalidOperationException(
                "Cannot disable all configured monitors — at least one active display must remain.");
        }

        // Unchanged uniform-shift idiom, generalized to a multi-target primary check:
        // shift ALL survivors by the same uniform delta iff ANY removed target was
        // GDI-primary, promoting the first survivor to (0,0) — Position has no public
        // setter, so a fresh PathInfo must be constructed per survivor. No gap-
        // closing/reflow logic beyond this uniform shift is added (D-01 explicitly
        // scoped that out — Windows' own default placement is good enough for the
        // surviving layout otherwise).
        bool anyTargetWasPrimary = targets.Any(t => t.IsGDIPrimary);

        PathInfo[] pathsToApply;
        if (anyTargetWasPrimary)
        {
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
        // the WinForms screen-enumeration API). Generalized to N: none of the
        // requested device paths may still be active, exactly one GDI primary must
        // remain, AND (new, 06-RESEARCH.md/T-06-05) no survivor bounding-box overlap.
        PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);

        bool anyTargetStillActive = verifyPaths
            .SelectMany(p => p.TargetsInfo)
            .Any(t => monitorDevicePaths.Contains(t.DisplayTarget.DevicePath));

        bool exactlyOnePrimary = verifyPaths.Count(p => p.IsGDIPrimary) == 1;

        bool overlap = AnyRectanglesOverlap(verifyPaths
            .Where(p => p.IsModeInformationAvailable)
            .Select(p => new Rectangle(p.Position, p.Resolution))
            .ToList());

        if (anyTargetStillActive || !exactlyOnePrimary || overlap)
        {
            throw new InvalidOperationException(
                $"Monitor disable did not take effect as expected (anyTargetStillActive={anyTargetStillActive}, " +
                $"exactlyOnePrimary={exactlyOnePrimary}, overlap={overlap}). " +
                "No further automatic recovery is attempted (D-05).");
        }
    }

    // Pure axis-aligned bounding-box overlap check (06-RESEARCH.md "Bounding-box
    // overlap check" code example) — used by DeactivateMonitors' verify-and-throw
    // section to catch a mutation that silently leaves an overlapping topology
    // (T-06-05). System.Drawing.Rectangle is already in scope via WindowsDisplayAPI's
    // own Point/Size usage in PathInfo — no new dependency. Internal (not private) +
    // tested directly (RigToggle.Windows.Tests, see InternalsVisibleTo below).
    internal static bool AnyRectanglesOverlap(IReadOnlyList<Rectangle> rects)
    {
        for (int i = 0; i < rects.Count; i++)
        {
            for (int j = i + 1; j < rects.Count; j++)
            {
                if (rects[i].IntersectsWith(rects[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

}
