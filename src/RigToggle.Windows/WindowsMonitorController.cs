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
    // In-process fast path (Program.cs constructs exactly one WindowsMonitorController
    // for the app's entire lifetime — verified in the composition root). Caches the
    // exact pre-mutation live PathInfo[] array captured at Disable()-time, so Restore()
    // can replay it directly via the SAME mechanism already proven twice (Phase 1
    // spike's GO, Plan 01's rig re-test GO: PathInfo.ApplyPathInfos(originalActivePaths,
    // allowChanges: true)) instead of reconstructing from primitive snapshot values.
    // Reconstruction-from-snapshot remains as the fallback ONLY for the rarer case
    // where the process restarted between Disable() and Restore() (CORE-05 crash
    // recovery), where no in-memory cache can possibly survive.
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

        // Cache BEFORE any mutation, regardless of what happens below, so a retry-
        // restore always has the true pre-disable topology available (in-process
        // fast path — see field doc comment).
        _originalPathsCache = currentPaths;

        PathInfo? targetPath = currentPaths.FirstOrDefault(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == monitorDevicePath));

        if (targetPath is null)
        {
            throw new InvalidOperationException(
                $"Configured monitor '{monitorDevicePath}' is not currently active.");
        }

        PathInfo[] survivors = currentPaths.Where(p => p != targetPath).ToArray();

        if (survivors.Length == 0)
        {
            // ApplyPathInfos would call ValidatePathInfos on an empty array and throw a
            // generic PathChangeException("Invalid paths information.") with no
            // indication of the actual cause (validation fails before any native
            // mutation — this does NOT blank the screen, but the error is useless).
            // Reachable in production: rig monitor unplugged/off, or a laptop with only
            // its built-in display, when Switch to Rig Mode is pressed.
            throw new InvalidOperationException(
                $"Cannot disable '{monitorDevicePath}' — it is currently the only active " +
                "display. Connect and enable another display before switching to Rig Mode.");
        }

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
        // In-process fast path: if this exact WindowsMonitorController instance is
        // still holding the pre-disable live PathInfo[] array (i.e. no process
        // restart happened between Disable() and Restore()), replay it directly via
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

                if (fastRestoredTarget is null ||
                    fastRestoredTarget.Position.X != fastExpectedSnap.PositionX ||
                    fastRestoredTarget.Position.Y != fastExpectedSnap.PositionY ||
                    fastRestoredTarget.IsGDIPrimary != fastExpectedSnap.IsPrimary)
                {
                    throw new InvalidOperationException(
                        "Monitor restore did not reproduce the exact prior configuration. No further automatic recovery is attempted (D-05).");
                }

                return;
            }
        }

        // Fallback (crash-recovery path only — no in-process cache survives a restart):
        // reconstruct from the JSON-persisted primitive snapshot.
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
        // CURRENTLY ACTIVE path's source system-wide first (not just ones present in
        // this restore's snapshot — a monitor plugged in after the snapshot was
        // captured is still live and its source still off-limits), then assign each
        // INACTIVE target the first source not already reserved.
        PathInfo[] activePathsForSourceLookup = PathInfo.GetActivePaths(virtualModeAware: false);
        var usedSources = new HashSet<PathDisplaySource>(
            activePathsForSourceLookup.Select(p => p.DisplaySource));
        PathDisplaySource[] allSources = PathDisplaySource.GetDisplaySources();

        // Rig-discovered follow-up to the WR-02 fix above: liveMatch.DisplaySource (from
        // GetAllPaths) is untrustworthy for an INACTIVE target (original WR-02 finding), but
        // empirically it is ALSO untrustworthy for the currently-ACTIVE target — observed on
        // the rig as both the active survivor AND the inactive target resolving to the same
        // source=0, producing the exact "two paths cannot legally share one source" collision
        // WR-02 was supposed to prevent. GetActivePaths() is the only query proven reliable for
        // real, live source assignment, so build a device-path -> source lookup from IT (not
        // GetAllPaths) and prefer it whenever a target is currently active.
        var activeSourceByDevicePath = activePathsForSourceLookup
            .SelectMany(p => p.TargetsInfo
                .Where(t => t.DisplayTarget.IsAvailable)
                .Select(t => (t.DisplayTarget.DevicePath, p.DisplaySource)))
            .ToDictionary(x => x.DevicePath, x => x.DisplaySource);

        var rebuilt = new List<PathInfo>();
        foreach (var (snap, liveMatch, liveTarget) in resolved)
        {
            PathDisplaySource ownSourceForActivePath =
                activeSourceByDevicePath.TryGetValue(snap.DevicePath, out PathDisplaySource trustedActiveSource)
                    ? trustedActiveSource
                    : liveMatch.DisplaySource; // fallback only; shouldn't be reached when IsPathActive is true

            PathDisplaySource sourceToUse = AssignSource(
                liveTarget.IsPathActive,
                ownSourceForActivePath,
                usedSources,
                allSources,
                $"Cannot restore '{snap.FriendlyName}' ({snap.DevicePath})");

            // The (displayTarget, frequency, scanLineOrdering, rotation, scaling) constructor
            // overload never sets IsSignalInformationAvailable=true (confirmed by reading
            // WindowsDisplayAPI's PathTargetInfo.cs source directly — only the overloads that
            // take an explicit PathTargetSignalInfo object set that flag). That means every
            // previously-reconstructed target here supplied a fully-populated SOURCE mode
            // (position/resolution/pixel format, from the PathInfo constructed below) while
            // silently omitting TARGET mode info entirely — an inconsistent supplied-mode-info
            // topology that is a strong suspect for the CCD validation failure observed on the
            // rig during crash-recovery restore (PathChangeException "Invalid paths
            // information.", confirmed reproducible, not a one-off). Constructing an explicit
            // PathTargetSignalInfo instead ensures target mode info is genuinely supplied,
            // matching what an internally-queried (live/active) PathTargetInfo would have.
            // ActiveSize/TotalSize are approximated as equal to the target's resolution — this
            // is the standard convention for digital (DisplayPort/HDMI) signals, which have no
            // analog blanking interval, and is the best available since MonitorPathSnapshot
            // (Plan 02) never captured full CVT/GTF timing detail.
            var signalInfo = new PathTargetSignalInfo(
                activeSize: new Size(snap.ResolutionWidth, snap.ResolutionHeight),
                totalSize: new Size(snap.ResolutionWidth, snap.ResolutionHeight),
                verticalSyncFrequencyInMillihertz: snap.FrequencyInMillihertz,
                scanLineOrdering: (DisplayConfigScanLineOrdering)snap.ScanLineOrdering);

            var reconstructedTarget = new PathTargetInfo(
                liveTarget.DisplayTarget,
                signalInfo,
                (DisplayConfigRotation)snap.Rotation,
                (DisplayConfigScaling)snap.Scaling);

            // No public PathTargetInfo constructor accepts OutputTechnology — every
            // manually-constructed instance silently defaults to
            // DisplayConfigVideoOutputTechnology.Other, even though CaptureState()
            // correctly captured the real value (e.g. DisplayPortExternal) into the
            // snapshot. Telling CCD validation a DisplayPort target is "Other"
            // technology is a strong suspect for the observed PathChangeException
            // ("Invalid paths information."). liveTarget.OutputTechnology is reliable
            // even for an inactive target — it describes the physical connector type,
            // not session mode data (unlike Pitfall 2's frequency/rotation caveat).
            CopyOutputTechnology(reconstructedTarget, liveTarget.OutputTechnology);

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
                $"{p.TargetsInfo.First().DisplayTarget.FriendlyName ?? "?"}@source={p.DisplaySource.SourceId} " +
                $"pos=({p.Position.X},{p.Position.Y}) tech={p.TargetsInfo.First().OutputTechnology} " +
                $"active={p.TargetsInfo.First().IsPathActive} sigInfo={p.TargetsInfo.First().IsSignalInformationAvailable} " +
                $"modeInfo={p.IsModeInformationAvailable}"));
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

        _originalPathsCache = null;
    }

    // WindowsDisplayAPI's PathTargetInfo.OutputTechnology has no public constructor
    // parameter — every manually-built instance defaults to
    // DisplayConfigVideoOutputTechnology.Other regardless of the target's real
    // connector type. This patches the compiler-generated backing field directly
    // (the only way to correct it without depending on the library's internal
    // constructor overload). Throws rather than silently leaving the wrong value if
    // the library's compiled field name ever changes, so a future failure here is
    // loud instead of silently reproducing this same bug.
    // Internal (not private) + tested directly (RigToggle.Windows.Tests, see
    // InternalsVisibleTo below) — this is the one piece of Restore()'s reconstruction
    // logic that's fully unit-testable without live display hardware, and a routine
    // WindowsDisplayAPI package upgrade could silently reintroduce this exact bug
    // (04-03 SUMMARY.md WR-05) if nothing catches it before the rig does.
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

    // Pure, testable extraction of Restore()'s fallback source-assignment rule (WR-02
    // fix, pinned by RigToggle.Windows.Tests): an already-ACTIVE target keeps its own
    // (correct, currently-in-use) source; an INACTIVE target gets the first source not
    // already reserved in usedSources — mutating usedSources as sources are claimed, so
    // sequential calls across multiple targets don't double-assign the same source.
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
