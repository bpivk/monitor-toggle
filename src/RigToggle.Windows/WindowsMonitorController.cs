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
/// GetAllMonitors/ActivateMonitors/DeactivateMonitors generalize the triad to N
/// monitors starting Phase 6 (06-RESEARCH.md Patterns 1/2/3) — DeactivateMonitors is
/// the direct 1->N generalization of the former single-target Disable(string),
/// implementing 04-RESEARCH.md Pattern 1 (repositioning-aware survivor reconstruction
/// so exactly one survivor lands at (0,0)) + Pattern 3 (verify-and-throw against a
/// fresh GetActivePaths() re-query, D-03, now including a bounding-box overlap
/// check). Restore implements Pattern 4 (live-identity re-resolution via
/// GetAllPaths() matched on stored DevicePath, mode/signal rebuilt from the STORED
/// snapshot, never trusting an inactive path's own live data) + the same
/// verify-and-throw idiom, also N-generalized with the overlap check. None of these
/// methods use the WinForms screen-enumeration API as an oracle (D-04) or attempt
/// automatic rollback on verification failure (D-05) — the exception bubbles to
/// MainForm's existing handler.
/// </summary>
public sealed class WindowsMonitorController : IMonitorController
{
    // In-process fast path (Program.cs constructs exactly one WindowsMonitorController
    // for the app's entire lifetime — verified in the composition root). Caches the
    // exact pre-mutation live PathInfo[] array captured at DeactivateMonitors()-time,
    // so Restore() can replay it directly via the SAME mechanism already proven twice
    // (Phase 1 spike's GO, Plan 01's rig re-test GO: PathInfo.ApplyPathInfos(originalActivePaths,
    // allowChanges: true)) instead of reconstructing from primitive snapshot values.
    // Reconstruction-from-snapshot remains as the fallback ONLY for the rarer case
    // where the process restarted between DeactivateMonitors() and Restore() (CORE-05
    // crash recovery), where no in-memory cache can possibly survive.
    private PathInfo[]? _originalPathsCache;

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

        // Cache BEFORE any mutation, regardless of what happens below, so a retry-
        // restore always has the true pre-disable topology available (in-process
        // fast path — see field doc comment).
        _originalPathsCache = currentPaths;

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
                "Cannot disable all configured monitors — at least one active display must " +
                "remain. Connect and enable another display before switching to Rig Mode.");
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
        // In-process fast path: if this exact WindowsMonitorController instance is
        // still holding the pre-disable live PathInfo[] array (i.e. no process
        // restart happened between DeactivateMonitors() and Restore()), replay it directly via
        // the SAME mechanism already proven twice on this rig (Phase 1 spike GO,
        // Plan 01 rig re-test GO) instead of reconstructing from primitive snapshot
        // values — sidesteps every reconstruction pitfall (source assignment,
        // OutputTechnology, mode-info shape) entirely, because nothing is rebuilt.
        // Sanity-checked against previousState.Paths' device-path set before trusting
        // it, so a mismatched/stale cache never gets silently applied.
        if (_originalPathsCache is not null)
        {
            var cachedDevicePaths = _originalPathsCache
                .SelectMany(p => p.TargetsInfo)
                .Where(t => t.DisplayTarget.IsAvailable)
                .Select(t => t.DisplayTarget.DevicePath)
                .ToHashSet();
            var expectedDevicePaths = previousState.Paths.Select(s => s.DevicePath).ToHashSet();

            if (cachedDevicePaths.SetEquals(expectedDevicePaths))
            {
                PathInfo.ApplyPathInfos(_originalPathsCache, allowChanges: true);
                _originalPathsCache = null;

                PathInfo[] fastVerifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);
                PathInfo? fastRestoredTarget = fastVerifyPaths.FirstOrDefault(p =>
                    p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == previousState.TargetDevicePath));
                MonitorPathSnapshot fastExpectedSnap =
                    previousState.Paths.First(s => s.DevicePath == previousState.TargetDevicePath);

                // N-generalized (06-RESEARCH.md/T-06-05): also reject a restore that
                // reproduces the target's own position/primary designation correctly
                // but leaves the survivor set overlapping.
                bool fastOverlap = AnyRectanglesOverlap(fastVerifyPaths
                    .Where(p => p.IsModeInformationAvailable)
                    .Select(p => new Rectangle(p.Position, p.Resolution))
                    .ToList());

                if (fastRestoredTarget is null ||
                    fastRestoredTarget.Position.X != fastExpectedSnap.PositionX ||
                    fastRestoredTarget.Position.Y != fastExpectedSnap.PositionY ||
                    fastRestoredTarget.IsGDIPrimary != fastExpectedSnap.IsPrimary ||
                    fastOverlap)
                {
                    throw new InvalidOperationException(
                        "Monitor restore did not reproduce the exact prior configuration. No further automatic recovery is attempted (D-05).");
                }

                return;
            }
        }

        // Fallback (crash-recovery path only — no in-process cache survives a restart).
        //
        // Rig-discovered: manually reconstructing PathTargetInfo/PathInfo field-by-field from
        // the stored primitive snapshot (source assignment, then signal info, then source
        // assignment again) hit THREE separate CCD validation failures across three rig-tested
        // iterations — every failure traced back to a property Windows does not reliably report
        // for INACTIVE paths (Microsoft docs: inactive-path mode/signal info is "set to default
        // values" — Pitfall 2). Rather than keep guessing which field is still wrong, this uses
        // the exact same two-step strategy DeactivateMonitors() already uses successfully (twice,
        // rig-confirmed): NEVER manually construct target/mode info from scratch — only ever
        // reuse REAL, live-queried PathInfo/PathTargetInfo objects wholesale, touching nothing
        // but Position.
        //
        // Step 1: an early "is the monitor still physically present" guard — before touching
        // anything, since Extend below would otherwise fail with a confusing generic error if a
        // configured display was unplugged.
        PathInfo[] liveAllPaths = PathInfo.GetAllPaths(virtualModeAware: false);
        foreach (MonitorPathSnapshot snap in previousState.Paths)
        {
            bool stillPresent = liveAllPaths.Any(p =>
                p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == snap.DevicePath));

            if (!stillPresent)
            {
                throw new InvalidOperationException(
                    $"Cannot restore '{snap.FriendlyName}' ({snap.DevicePath}) — no longer detected.");
            }
        }

        // Step 2: PathInfo.ApplyTopology(Extend) — a single built-in CCD topology switch with NO
        // manually-supplied path/mode structs at all, so none of the three failure modes above
        // are even reachable. Brings the previously-disabled monitor back to SOME active state;
        // exact position/arrangement don't matter yet — Step 3 corrects that.
        try
        {
            PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);
        }
        catch (Exception ex)
        {
            // WR-03 (code review): without this wrapper, a native CCD topology-switch
            // failure here propagates as the library's raw, comparatively cryptic
            // exception, unlike the sibling ApplyPathInfos call below (Step 3), which is
            // already wrapped for diagnosability. This is the crash-recovery fallback
            // path only reachable when the process restarted between DeactivateMonitors() and
            // Restore() — exactly the scenario where the user has the least context to
            // debug a bare library message.
            throw new InvalidOperationException(
                $"Monitor restore failed while switching to Extend topology: {ex.Message}", ex);
        }

        // Step 3: re-query now that both targets are genuinely active, and reuse each REAL,
        // live PathInfo's own TargetsInfo array UNCHANGED — the same "reuse real active
        // TargetsInfo, only touch Position" idiom DeactivateMonitors() uses for its survivor reconstruction
        // (Pitfall 1) — to move each display to its exact stored pre-disable position (and, for
        // the target that was primary, back to (0,0), which is what makes it primary again —
        // IsGDIPrimary is position-derived, per PathInfo.IsGDIPrimary). Resolution/pixel format
        // are taken from the LIVE post-Extend query (not the stored snapshot) — Windows just
        // confirmed those values are valid for this target moments ago, avoiding yet another
        // category of "supplied mode info the driver doesn't currently consider valid" failure.
        // Known limitation: rotation/scaling come from whatever Extend's own defaults are, not
        // the stored snapshot, since TargetsInfo is reused unchanged — acceptable because Extend
        // defaults to no rotation for the near-universal case of an unrotated monitor, and the
        // verify-and-throw below (matching this method's existing scope) does not check rotation
        // either.
        PathInfo[] postExtendActivePaths = PathInfo.GetActivePaths(virtualModeAware: false);

        var corrected = new List<PathInfo>();
        foreach (MonitorPathSnapshot snap in previousState.Paths)
        {
            PathInfo? activeMatch = postExtendActivePaths.FirstOrDefault(p =>
                p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == snap.DevicePath));

            if (activeMatch is null)
            {
                throw new InvalidOperationException(
                    $"Cannot restore '{snap.FriendlyName}' ({snap.DevicePath}) — the Extend topology switch did " +
                    "not bring it back to an active state. No further automatic recovery is attempted (D-05).");
            }

            corrected.Add(new PathInfo(
                activeMatch.DisplaySource,
                new Point(snap.PositionX, snap.PositionY),
                activeMatch.Resolution,
                activeMatch.PixelFormat,
                activeMatch.TargetsInfo));
        }

        try
        {
            PathInfo.ApplyPathInfos(corrected.ToArray(), allowChanges: true);
        }
        catch (WindowsDisplayAPI.Exceptions.PathChangeException ex)
        {
            // Extra diagnostic detail beyond the library's generic message, since
            // ValidatePathInfos discards the underlying Win32 error code entirely.
            string attempted = string.Join("; ", corrected.Select(p =>
                $"{p.TargetsInfo.First().DisplayTarget.FriendlyName ?? "?"}@source={p.DisplaySource.SourceId} " +
                $"pos=({p.Position.X},{p.Position.Y}) tech={p.TargetsInfo.First().OutputTechnology}"));
            throw new InvalidOperationException(
                $"Monitor restore failed CCD validation: {ex.Message} Attempted topology: {attempted}", ex);
        }

        // Pattern 4/D-03: verify-and-throw — confirm the configured target is present
        // again and matches its stored position/primary designation, AND (new,
        // 06-RESEARCH.md/T-06-05) that the restored survivor set doesn't overlap.
        PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);
        PathInfo? restoredTarget = verifyPaths.FirstOrDefault(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == previousState.TargetDevicePath));
        MonitorPathSnapshot expectedSnap = previousState.Paths.First(s => s.DevicePath == previousState.TargetDevicePath);

        bool overlap = AnyRectanglesOverlap(verifyPaths
            .Where(p => p.IsModeInformationAvailable)
            .Select(p => new Rectangle(p.Position, p.Resolution))
            .ToList());

        if (restoredTarget is null ||
            restoredTarget.Position.X != expectedSnap.PositionX ||
            restoredTarget.Position.Y != expectedSnap.PositionY ||
            restoredTarget.IsGDIPrimary != expectedSnap.IsPrimary ||
            overlap)
        {
            throw new InvalidOperationException(
                "Monitor restore did not reproduce the exact prior configuration. No further automatic recovery is attempted (D-05).");
        }

        _originalPathsCache = null;
    }

    // Pure axis-aligned bounding-box overlap check (06-RESEARCH.md "Bounding-box
    // overlap check" code example) — shared by both DeactivateMonitors' and
    // Restore's verify-and-throw sections to catch a mutation that silently leaves
    // an overlapping topology (T-06-05). System.Drawing.Rectangle is already in
    // scope via WindowsDisplayAPI's own Point/Size usage in PathInfo — no new
    // dependency. Internal (not private) + tested directly (RigToggle.Windows.Tests,
    // see InternalsVisibleTo below).
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

    // Phase 05 code review WR-02: NOT currently called by DeactivateMonitors() or Restore() in
    // this file — Restore() was rewritten (see its own doc comment above) to never
    // manually reconstruct PathTargetInfo/mode-info from scratch, so this
    // backing-field patch is unused by any reachable production code path today.
    // Kept intentionally as a documented known-good fallback from the earlier
    // manual-reconstruction approach, in case a future WindowsDisplayAPI upgrade or
    // hardware edge case forces a return to it — deleting it would lose already-
    // solved, rig-hard-won knowledge, namely: WindowsDisplayAPI's
    // PathTargetInfo.OutputTechnology has no public constructor parameter — every
    // manually-built instance defaults to DisplayConfigVideoOutputTechnology.Other
    // regardless of the target's real connector type. This patches the
    // compiler-generated backing field directly (the only way to correct it without
    // depending on the library's internal constructor overload). Throws rather than
    // silently leaving the wrong value if the library's compiled field name ever
    // changes, so a future failure here is loud instead of silently reproducing this
    // same bug.
    // Internal (not private) + tested directly (RigToggle.Windows.Tests, see
    // InternalsVisibleTo below) so it stays correct and ready to reuse even while
    // unreachable from production code (originally added per 04-03 SUMMARY.md WR-05).
    internal static void CopyOutputTechnology(PathTargetInfo target, DisplayConfigVideoOutputTechnology technology)
    {
        var field = typeof(PathTargetInfo).GetField(
            "<OutputTechnology>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field is null)
        {
            throw new InvalidOperationException(
                "Cannot patch PathTargetInfo.OutputTechnology — backing field not found (WindowsDisplayAPI internals changed).");
        }

        field.SetValue(target, technology);
    }

    // Phase 05 code review WR-02: like CopyOutputTechnology above, NOT currently
    // called by DeactivateMonitors() or Restore() in this file — the current Restore() only
    // ever reuses real, live-queried PathInfo/PathTargetInfo objects wholesale via
    // the two-step ApplyTopology(Extend) + reposition approach, so no source is ever
    // manually assigned today. Kept intentionally as a documented known-good
    // fallback from the earlier manual-reconstruction approach (originally added per
    // 04-03's own WR-02 fix): pure, testable extraction of that approach's
    // source-assignment rule, pinned by RigToggle.Windows.Tests so it stays correct
    // and ready to reuse if a future package upgrade or hardware edge case forces a
    // return to manual reconstruction — an already-ACTIVE target keeps its own
    // (correct, currently-in-use) source; an INACTIVE target gets the first source
    // not already reserved in usedSources — mutating usedSources as sources are
    // claimed, so sequential calls across multiple targets don't double-assign the
    // same source.
    internal static PathDisplaySource AssignSource(
        bool isPathActive,
        PathDisplaySource ownSource,
        HashSet<PathDisplaySource> usedSources,
        IReadOnlyList<PathDisplaySource> allSources,
        string errorContext)
    {
        if (isPathActive)
        {
            return ownSource;
        }

        PathDisplaySource sourceToUse = allSources.FirstOrDefault(s => !usedSources.Contains(s))
            ?? throw new InvalidOperationException($"{errorContext} — no free display source available.");
        usedSources.Add(sourceToUse);
        return sourceToUse;
    }
}
