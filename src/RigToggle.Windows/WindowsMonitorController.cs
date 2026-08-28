using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
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
///
/// Debug session monitor-enable-reactivates-others-again, round 5: ActivateMonitors now
/// attempts a scoped, explicit-path activation (TryBuildScopedActivationPlan +
/// PathInfo.ApplyPathInfos) BEFORE falling back to the whole-topology
/// PathInfo.ApplyTopology(Extend) call rounds 1-4 relied on exclusively — see
/// ActivateMonitors' own remarks for the full rationale. This reuses (never repeats)
/// ccd-topology-restore-findings finding 1's reflection-patch technique for exactly one
/// field (PathTargetInfo.IsPathActive) on a reused, freshly-queried object — it does not
/// reintroduce manual mode/position/resolution reconstruction, which remains the
/// rig-disproven approach documented there.
///
/// Debug session monitor-position-resets-to-de: round 5's scoped activation deliberately
/// supplied no mode info (Position/Resolution/PixelFormat) for the target being
/// reactivated, letting the driver's best-mode-logic pick — which does not preserve the
/// target's own pre-disable position, causing it to reset to a driver default every time a
/// monitor was disabled and re-enabled. Fixed by caching each target's real, live mode
/// (Position/Resolution/PixelFormat) in DeactivateMonitors immediately before that target
/// is removed — while the data is still valid, per ccd-topology-restore-findings finding
/// 5b ("take mode/signal values from the stored snapshot taken while the path was
/// active") — and having TryBuildScopedActivationPlan supply that cached mode (when
/// present) instead of leaving mode info blank. This is NOT the rig-disproven manual
/// reconstruction path: it reuses the same PathInfo(source, position, resolution,
/// pixelFormat, targets) constructor overload DeactivateMonitors' own survivor
/// repositioning already uses safely elsewhere in this file, populated from data captured
/// moments earlier while genuinely live — never from a stale/cached snapshot spanning a
/// mutation boundary. Falls back to today's blank-mode (driver-picks) behavior when no
/// cache entry exists (e.g. app restarted between disable and enable) — so this can only
/// improve on round 5's behavior, never regress it.
///
/// Debug session monitor-position-resets-to-de, Symptom 2 (round 2 of this same debug
/// session, after the position-cache fix above was rig-confirmed to resolve Symptom 1 but
/// left Symptom 2 open): a fresh rig debug.log showed round 5's scoped ApplyPathInfos path
/// is unreliable specifically for a full Rig/Normal monitor SWAP — ActivateMonitors adding
/// the new target(s) while every currently-active monitor is still transiently kept active
/// too, immediately followed by ToggleService's own DeactivateMonitors call removing that
/// old, largely-disjoint set within the same "Monitor" step. Directly observed on the rig:
/// the forward-direction (Rig-mode) apply logged a clean, verified-OK success, then
/// silently reverted to the pre-toggle topology ~3.2 seconds later with no intervening app
/// call (a fresh OnDisplaySettingsChanged firing on its own); the reverse-direction
/// (Normal-mode) apply instead threw PathChangeException("Invalid paths information.")
/// outright and fell back to ApplyTopology(Extend), which succeeded. ActivateMonitors now
/// takes an explicit isPartOfMonitorSwap flag (see IMonitorController's doc comment) and
/// skips the scoped-activation attempt entirely when true, going straight to the
/// whole-topology Extend fallback that has been rig-proven reliable for this exact
/// multi-monitor swap shape across this file's entire debug history — reserving scoped
/// activation (and its flash-avoidance benefit) for the narrower single-target
/// reactivation case it was built for and remains rig-validated on (a bare dashboard-tile
/// enable, with no immediately-following deactivation of a different set). Does not touch,
/// weaken, or revert TryBuildScopedActivationPlan itself, the position-mode cache above,
/// the settle-poll-then-correct loop, ComputeUnexpectedlyActivated, or DeactivateMonitors —
/// all left fully in place, since Extend's own correction/verify machinery already handles
/// its documented reactivate-unrelated-monitor side effect regardless of which caller
/// triggered the fallback.
///
/// Debug session monitor-position-resets-to-de, Symptom 2, round 3: a THIRD rig test (one
/// monitor, DELA0B8, already independently disabled before the toggle began — the ORIGINAL
/// Symptom 2 repro shape) showed round 2's Extend-fallback mitigation above is ALSO
/// unreliable, in a new way: the settle-poll immediately after ApplyTopology(Extend) never
/// observed the actually-requested target (SAM748A) active at all, while DELA0B8 —
/// deliberately, independently disabled beforehand and never part of this call's request —
/// came back active anyway. Decompiling WindowsDisplayAPI 1.3.0.13 (PathInfo.ApplyTopology)
/// confirms why: it calls SetDisplayConfig(0, null, 0, null, SDC_APPLY | topology-flags),
/// supplying NO path or mode array at all — Windows' own internal algorithm decides the
/// entire resulting topology from its own notion of "the extended desktop," which this
/// code has no way to constrain or even observe in advance. Extend was never capable of
/// reliably enacting a specific desired active set; rounds 1-4 only appeared to prove it
/// reliable because, for those repro shapes, its opaque internal choice happened to match
/// what was wanted. Given that, ActivateMonitors now attempts scoped, explicit-path
/// activation (TryBuildScopedActivationPlan + ApplyPathInfos) UNCONDITIONALLY — including
/// for a full swap — rather than bypassing it in favor of Extend. TryBuildScopedActivationPlan
/// is now swap-aware: it excludes monitorSwapDisableSet's device paths from the survivors it
/// preserves, so the SAME single ApplyPathInfos call that activates the new target(s) also
/// implicitly deactivates the swap's disable-set as a side effect (any active path excluded
/// from a supplied array becomes inactive — the exact mechanism DeactivateMonitors' own
/// ApplyPathInfos call already relies on) — going directly from the pre-toggle topology to
/// the exact desired post-toggle topology in one CCD call, never transiently holding MORE
/// monitors active than either state. This simultaneously addresses round 2's forward-
/// direction durability problem (no more 3-active transient state for the driver to revert)
/// and its reverse-direction PathChangeException (excluding the disable-set frees its
/// PathDisplaySource(s) for the new target(s) to claim, instead of two new targets
/// contending for whatever sources remain unclaimed by a survivor that is about to be
/// removed anyway). Because these swap-excluded survivors may now go inactive without ever
/// reaching DeactivateMonitors' own "cache live mode before removal" code (DeactivateMonitors
/// may find them already inactive and take its no-op fast path), ActivateMonitors itself
/// caches their live mode — via the same CacheLiveModes helper DeactivateMonitors uses,
/// extracted below — immediately before excluding them, so Symptom 1's position-cache fix
/// continues to hold for the swap case too. Extend is now used ONLY as a last-resort
/// fallback when a scoped plan cannot even be constructed (e.g. the IsPathActive reflection
/// field is missing after a future WindowsDisplayAPI upgrade) — documented as unreliable for
/// correctly targeting a specific monitor, not trusted as the primary mechanism it was
/// before this round.
///
/// Debug session monitor-position-resets-to-de, Symptom 2, round 4: a FOURTH rig test (an
/// app-startup mode-restore call — ActivateMonitors(SAM748A, disableSet=[ACI24A4,DELA0B8]),
/// before any user action) showed round 3's fix has its own failure mode: excluding the
/// swap's entire disable-set from the scoped plan's array (rather than merely from the
/// "sources claimed" bookkeeping) can leave the array too degenerate for the CCD API to
/// accept — reproduced here as a single-entry (target-only) array rejected outright with
/// PathChangeException("Invalid paths information."), falling back to the known-unreliable
/// Extend path. Researched directly against Microsoft's own SetDisplayConfig/Desktop-Layout
/// guidance (not just this wrapper library) two independent, corroborating requirements
/// round 3's plan violated: (1) a path being deactivated must remain PRESENT in the supplied
/// array with its DISPLAYCONFIG_PATH_ACTIVE flag cleared and its mode index invalidated —
/// never simply omitted (confirmed by an OSR Developer Community CCD thread and a
/// copyprogramming.com SetDisplayConfig write-up, both independently describing "clear the
/// active flag, do not remove the path from the array" as the correct pattern); and (2)
/// GDI's desktop-layout contract requires exactly one ACTIVE path positioned at the origin
/// (0,0) — the "primary" — confirmed by Microsoft's own Desktop-Layout driver documentation
/// ("no gaps, no overlaps" positioning) and a real-world SetDisplayConfig usage write-up
/// (blog.lohr.dev, "Changing the primary display on Windows by code") describing the exact
/// mirror-image algorithm DeactivateMonitors below already implements for the deactivation
/// direction (promote a survivor to (0,0), uniformly shift everyone else by the same delta).
/// Round 3's plan violated (2) whenever the swap's disable-set consumed every OTHER active
/// survivor (this round's repro exactly): the sole remaining active entry (the newly-
/// activated target) kept its OWN stale, non-origin cached position from when it was
/// previously a non-primary monitor, submitting a topology with no path at all at (0,0).
/// TryBuildScopedActivationPlan now keeps every swap-excluded survivor IN the array as an
/// explicit, mode-blanked, reflection-patched-inactive entry (freeing its PathDisplaySource
/// for reuse exactly as round 3 intended) instead of omitting it, and a new pure helper,
/// PromoteToOriginIfNeeded, normalizes the resulting active-entry set so exactly one path
/// sits at (0,0) whenever none already does. Neither change touches the position-mode cache
/// itself, CacheLiveModes, the settle-poll-then-correct loop, ComputeUnexpectedlyActivated,
/// or DeactivateMonitors — this round is scoped entirely to TryBuildScopedActivationPlan's
/// own array construction. Still rig-unverified (this sandbox cannot execute real Windows
/// CCD calls) — self-verified via build, the existing test suite, and new unit tests for the
/// pure PromoteToOriginIfNeeded seam only.
///
/// Debug session monitor-position-resets-to-de, round 7: a SEVENTH rig test (a plain,
/// non-swap tile-enable — ActivateMonitors(ACI24A4), monitorSwapDisableSet empty, with
/// SAM748A already active going in) showed round 5's scoped ApplyPathInfos plan can still
/// throw PathChangeException even for this simple, non-swap shape — falling back to
/// ApplyTopology(Extend) — and Extend's own opaque, database-driven layout did not include
/// SAM748A (active before this call, never any part of its disable-set) in its result.
/// ActivateMonitors' correction loop and its final verify-and-throw ONLY ever checked for
/// "did the requested target(s) become active" and "did an UNREQUESTED path become active
/// that wasn't before" (ComputeUnexpectedlyActivated) — a previously-active, unrequested
/// survivor silently going INACTIVE as a side effect had no correction path and no
/// verify-and-throw check at all, so this call reported EXIT success with a WRONG final
/// state (SAM748A permanently off), which was then baked into MainForm's own intent
/// baseline (ArmIntentGuard snapshots whatever GetAllMonitors() observes right after this
/// call returns) — so even the reactive watchdog (rounds 3/5) could never catch it, since
/// it believed SAM748A being off was the correct, intended state. Fixed by adding
/// ComputeUnexpectedlyDeactivated, the mirror image of ComputeUnexpectedlyActivated: the
/// correction loop now also detects any pre-call-active, non-swap-excluded device path
/// that is not active after a round, and corrects it via a nested, non-swap-aware
/// ActivateMonitors call for exactly that path (reusing this same public method, not a new
/// primitive — mirrors DeactivateMonitors' own reuse for the opposite direction); the
/// final verify-and-throw now also fails loudly if any such survivor is still missing
/// after the bounded correction rounds are exhausted, instead of silently reporting
/// success. Does NOT address Symptom 2 part B (the delayed silent revert itself, still
/// unresolved after seven rounds — see class history in earlier remarks / the debug
/// session file) or explain WHY the scoped ApplyPathInfos plan threw PathChangeException
/// for this specific SAM748A+ACI24A4 pairing in the first place (a companion rig trial
/// showed an apparently-identical-shaped SAM748A+DELA0B8 call succeed cleanly — genuinely
/// unexplained, deliberately not guessed at this round per this session's established
/// research-vs-reasoning discipline); this fix targets the CONSEQUENCE (a previously-
/// active survivor silently lost, with no correction and no error), which is independently
/// real and actionable regardless of that upstream cause.
/// </summary>
public sealed class WindowsMonitorController : IMonitorController
{
    // Debug session monitor-position-resets-to-de: in-memory, per-process cache of each
    // device path's real, live mode (Position/Resolution/PixelFormat) at the moment it was
    // last deactivated — populated by DeactivateMonitors AND (round 3) by ActivateMonitors
    // itself for a swap's about-to-be-excluded survivors (see CacheLiveModes below),
    // consumed by TryBuildScopedActivationPlan. Scoped to this controller instance, which
    // Program.cs constructs exactly once at the composition root and shares across both the
    // tile dashboard (MainForm) and the Rig/Normal toggle flow (ToggleService) — so a
    // monitor disabled via either path and re-enabled via either path, within the same
    // running session, gets its position restored. Does NOT survive an app restart (no
    // cross-session persistence) — a target with no cache entry falls back to round 5's
    // existing blank-mode (driver-picks) behavior, never an error.
    private readonly Dictionary<string, PathInfo> _lastKnownActiveModeByDevicePath = new();

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
    // worked around elsewhere in this file (DeactivateMonitors()). Targets
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

    // Activation of previously OS-disabled monitors (06-RESEARCH.md Pattern 2) — the
    // load-bearing generalization answer: NEVER manually reconstruct PathTargetInfo/mode
    // info for a previously-inactive target BY HAND (already tried and abandoned in this
    // codebase's now-deleted Restore() history — three separate rig-tested validation
    // failures, preserved in .planning/debug/knowledge-base.md's
    // ccd-topology-restore-findings entry, Phase 18).
    //
    // Debug session monitor-enable-reactivates-others-again, round 5: as of this round,
    // this method first ATTEMPTS a scoped activation (TryBuildScopedActivationPlan below
    // + PathInfo.ApplyPathInfos(forceModeEnumeration: true)) that never references any
    // device path outside the caller's own request — see TryBuildScopedActivationPlan's
    // own remarks for why this is not the same "manual reconstruction" that failed three
    // times before (the driver, not this code, still fills in mode information). If that
    // attempt cannot build a valid plan or throws, this falls back to the original,
    // rig-proven zero-argument PathInfo.ApplyTopology(Extend) call (rounds 1-4's
    // mechanism, byte-for-byte unchanged as a fallback): Extend takes no path/mode
    // arguments at all and lets the OS pick mode/position from the CCD persistence
    // database's last-known extend layout for currently-available targets.
    //
    // Pitfall 2 ordering contract (06-RESEARCH.md): this method MUST run BEFORE
    // DeactivateMonitors on rig-mode entry. Extend restores the persistence
    // database's last-known layout, which still includes any disable-set monitor(s)
    // as active if DeactivateMonitors' saveToDatabase:false call already ran first —
    // silently undoing the disable. On toggle-back, the mirror-image rule applies to
    // DeactivateMonitors(enableSet): it must run AFTER ActivateMonitors, not before,
    // for the same reason. ToggleService is the enforcement point for this ordering;
    // documented here too so a reader of this adapter alone still understands the
    // contract. This contract is specific to the Extend FALLBACK path (it reads the
    // persistence database); round 5's scoped attempt reads only live GetActivePaths()/
    // GetAllPaths() state immediately before applying and does not depend on it, but the
    // existing ordering is left unchanged regardless, since ToggleService's call order
    // must still be correct for whichever path this method ends up taking.
    //
    // Rig-confirmed (3-monitor rig, debug session monitor-enable-affects-other): Extend
    // being whole-topology cuts both ways — it doesn't just risk repositioning an
    // unrelated already-active monitor (Pitfall 3 below), it can REACTIVATE an
    // unrelated monitor that was deliberately left OS-disabled, if the persistence
    // database's last-known extend layout still includes it. With only one candidate
    // disabled monitor (the pre-upgrade 2-monitor case) this was unobservable; with two
    // or more simultaneously-disabled-but-available monitors, calling Extend to
    // reactivate just one of them can reactivate the other(s) too. The correction below
    // detects any device path that came back active but was neither previously active
    // nor requested, and turns it back off via the same already rig-proven
    // DeactivateMonitors CCD-removal path — never via manual reconstruction. This
    // correction loop is left fully in place and unchanged by round 5 — it is the safety
    // net for both the Extend fallback path (where it is load-bearing, exactly as before)
    // and the new scoped path (where it should normally find nothing to correct, but
    // remains active in case of any OTHER, independent reactivation mechanism).
    public void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths, IReadOnlySet<string> monitorSwapDisableSet)
    {
        // Debug session monitor-position-resets-to-de, round 3: isPartOfMonitorSwap is now
        // derived from the caller-supplied disable-set itself rather than passed as a
        // separate bool — see IMonitorController's doc comment for the full rationale.
        bool isPartOfMonitorSwap = monitorSwapDisableSet.Count > 0;

        // Debug session monitor-enable-reactivates-others-again, round 4 ("make debug log
        // actually debug stuff"): unconditional ENTER/EXIT logging on every branch of this
        // method, not just the "did something" path — round 3's rig trial reported a
        // completely empty debug.log, and one of the ruled-in candidates was simply that
        // this method's early-return/no-op paths were always silent, so even a healthy,
        // logging-enabled run of the "nothing to do" case would show nothing. Every return
        // and throw below now has a matching Log() call.
        Log($"ActivateMonitors: ENTER requested=[{string.Join(", ", monitorDevicePaths)}] isPartOfMonitorSwap={isPartOfMonitorSwap} monitorSwapDisableSet=[{string.Join(", ", monitorSwapDisableSet)}]");

        if (monitorDevicePaths.Count == 0)
        {
            Log("ActivateMonitors: EXIT no-op (empty request set).");
            return;
        }

        var currentlyActiveDevicePaths = QueryActiveDevicePaths();

        // Skip-optimization (Pitfall 3): Extend recomputes the WHOLE topology from the
        // DB record, not just the newly-added target(s) — it can incidentally
        // reposition an unrelated, already-correct third monitor. If every requested
        // device path is already active, there is nothing to do — never call Extend
        // just to be thorough.
        if (monitorDevicePaths.All(currentlyActiveDevicePaths.Contains))
        {
            Log($"ActivateMonitors: EXIT no-op (every requested path already active) preExtendActive=[{string.Join(", ", currentlyActiveDevicePaths)}].");
            return;
        }

        // Early availability guard — a clear,
        // domain-specific error instead of a confusing generic CCD failure if a
        // configured enable-set monitor is physically unplugged/undetected.
        PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
        var missing = monitorDevicePaths.Where(dp => !allPaths.Any(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == dp))).ToArray();

        if (missing.Length > 0)
        {
            Log($"ActivateMonitors: EXIT throwing -- not detected: [{string.Join(", ", missing)}].");
            throw new InvalidOperationException(
                $"Cannot enable monitor(s) — not detected: {string.Join(", ", missing)}");
        }

        Log($"ActivateMonitors: requested=[{string.Join(", ", monitorDevicePaths)}] preExtendActive=[{string.Join(", ", currentlyActiveDevicePaths)}].");

        // Debug session monitor-enable-reactivates-others-again, round 5: round 4's rig trial (full
        // verbatim debug.log reviewed this round) confirmed the round 2/3 detect-and-correct machinery
        // below is working exactly as designed -- the FINAL settled CCD state after the tile click is
        // unambiguously correct (SAM748A inactive, held stable across three further
        // OnDisplaySettingsChanged firings). The operator's "still not fixed" this round refutes neither
        // that mechanism nor its correctness; verified against the log, SAM748A's active-path flips
        // active-then-corrected TWICE within ~5.6s (once during the Extend call itself at
        // 37.778-40.106, once again externally at 42.930-43.368) before settling -- i.e. by construction,
        // a detect-AFTER-the-fact design can only shorten how long an unrequested monitor's CCD path (and,
        // per the operator's own round-1 report of literally watching "the monitor turns on", very likely
        // its physical signal/backlight) is active; it can never prevent that window from opening at all.
        // Only not asking the OS to consider SAM748A as a topology candidate in the first place can do
        // that -- which a whole-topology ApplyTopology(Extend) call structurally cannot do (Extend has no
        // per-target scoping; this is the same limitation the round-2 fix's own class remarks document).
        //
        // TryBuildScopedActivationPlan below constructs an explicit PathInfo[] -- every already-active
        // survivor path plus one newly-active PathInfo per requested device path -- that never references
        // SAM748A (or any other currently-inactive monitor not in this call's request) at all, and applies
        // it via the same SDC_USE_SUPPLIED_DISPLAY_CONFIG mechanism DeactivateMonitors below already uses
        // successfully (ApplyPathInfos), just inverted (adding a target instead of removing one), with
        // forceModeEnumeration:true so the driver -- not this code -- fills in the newly-activated
        // target's mode information. Confirmed against Microsoft's own SetDisplayConfig documentation
        // (not just this wrapper library): supplying an explicit path array where some paths carry a valid
        // mode index and others (the target being newly activated) carry
        // DISPLAYCONFIG_PATH_MODE_IDX_INVALID, combined with SDC_FORCE_MODE_ENUMERATION, is a documented,
        // intended scenario -- "SetDisplayConfig uses best mode logic to create the source and target mode
        // information" for the invalid-index paths -- not an undocumented hack. This sidesteps the THREE
        // separate rig-proven manual-reconstruction failures recorded in ccd-topology-restore-findings:
        // none of this PathInfo's resolution/position/pixelFormat/rotation/scaling/frequency fields are
        // ever manually filled in here -- the driver picks them, which is exactly the "let Windows figure
        // out the mode" capability that was missing when those three attempts hand-filled mode fields from
        // stale/default reads instead. The one field that DOES need correcting on a reused, freshly-queried
        // (never stale/cached) PathTargetInfo is IsPathActive (false on an inactive GetAllPaths() entry,
        // needed true here) -- there is no public API to set it, so this reuses the exact same
        // reflection-patch-a-readonly-backing-field technique ccd-topology-restore-findings finding 1
        // already rig-proved for OutputTechnology (a different field, same technique, same library
        // version), and throws loudly (never silently no-ops) if that backing field is ever not found by a
        // future WindowsDisplayAPI upgrade -- per that finding's own explicit recommendation.
        //
        // This is new, sandbox-unverifiable-on-real-CCD-hardware territory, so it is attempted FIRST but
        // never trusted blindly. ApplyPathInfos validates the supplied array (SDC_VALIDATE) before ever
        // touching live topology and throws cleanly if invalid, so a bad plan never reaches the hardware;
        // any failure to build a valid scoped plan (e.g. no unclaimed PathDisplaySource is available for a
        // requested target -- ccd-topology-restore-findings finding 2's source-collision rule, adapted) or
        // any exception from ApplyPathInfos itself falls back to the exact same, already rig-confirmed
        // ApplyTopology(Extend) call this method used before round 5 -- so this round can only ever IMPROVE
        // on today's behavior (no more visible flash) or leave it byte-for-byte unchanged (silent, logged
        // fallback); it cannot regress it. The settle-poll-then-correct loop immediately below and
        // MainForm's out-of-call reactive watchdog (rounds 2/3) are deliberately left fully unchanged,
        // regardless of which path is taken here -- they remain the safety net against any OTHER
        // reactivation mechanism (e.g. round 3's own finding that an independent OS/driver-level
        // auto-extend can fire seconds later, unrelated to which CCD call this method makes).
        var devicePathsToActivate = monitorDevicePaths.Where(dp => !currentlyActiveDevicePaths.Contains(dp)).ToHashSet();
        PathInfo[] activePathsForScopedPlan = PathInfo.GetActivePaths(virtualModeAware: false);
        bool usedScopedActivation = false;

        // Debug session monitor-position-resets-to-de, Symptom 2, round 3: a full Rig/Normal
        // swap no longer bypasses scoped activation in favor of Extend -- round 3's rig log
        // proved Extend itself unreliable for this exact shape too (it can fail to activate
        // the requested target while ALSO reactivating an unrelated, independently-disabled
        // monitor -- see class remarks). Instead, when monitorSwapDisableSet is non-empty,
        // its device paths are excluded from the survivors TryBuildScopedActivationPlan
        // preserves, AND their live mode is cached now (via CacheLiveModes, the same helper
        // DeactivateMonitors uses) before that exclusion -- because DeactivateMonitors' own
        // subsequent call may find them already inactive (a side effect of the scoped
        // ApplyPathInfos call below) and take its no-op fast path, skipping its own capture.
        if (isPartOfMonitorSwap)
        {
            PathInfo[] swapExcludedSurvivors = activePathsForScopedPlan
                .Where(p => p.TargetsInfo.Any(t => monitorSwapDisableSet.Contains(t.DisplayTarget.DevicePath)))
                .ToArray();
            CacheLiveModes(swapExcludedSurvivors);
            Log($"ActivateMonitors: isPartOfMonitorSwap=true -- cached live mode for swap-excluded survivors=[{string.Join(", ", swapExcludedSurvivors.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath))}] -- these will be excluded from the scoped activation plan below so the same ApplyPathInfos call both activates the new target(s) and implicitly deactivates them.");
        }

        if (TryBuildScopedActivationPlan(devicePathsToActivate, activePathsForScopedPlan, monitorSwapDisableSet, out PathInfo[] scopedPlan, out string scopedFailureReason))
        {
            Log($"ActivateMonitors: round 5 -- scoped activation plan built for targets=[{string.Join(", ", devicePathsToActivate)}], planPaths=[{string.Join(", ", scopedPlan.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath))}] -- attempting scoped PathInfo.ApplyPathInfos(forceModeEnumeration: true) INSTEAD OF whole-topology ApplyTopology(Extend).");

            try
            {
                PathInfo.ApplyPathInfos(scopedPlan, allowChanges: true, saveToDatabase: false, forceModeEnumeration: true);
                usedScopedActivation = true;
                Log("ActivateMonitors: round 5 -- scoped activation ApplyPathInfos completed without throwing.");
            }
            catch (Exception ex)
            {
                Log($"ActivateMonitors: round 5 -- scoped activation ApplyPathInfos threw ({ex.GetType().Name}: {ex.Message}) -- falling back to whole-topology ApplyTopology(Extend). Round 3: this fallback is now KNOWN unreliable for correctly targeting a specific monitor (it may fail to activate the requested target and/or reactivate an unrelated, independently-disabled one) -- used here only because no better option remains.");
            }
        }
        else
        {
            Log($"ActivateMonitors: round 5 -- scoped activation plan not available ({scopedFailureReason}) -- falling back to whole-topology ApplyTopology(Extend). Round 3: this fallback is now KNOWN unreliable for correctly targeting a specific monitor (it may fail to activate the requested target and/or reactivate an unrelated, independently-disabled one) -- used here only because no better option remains.");
        }

        if (!usedScopedActivation)
        {
            Log("ActivateMonitors: calling whole-topology PathInfo.ApplyTopology(Extend).");
            PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);
        }

        // Debug session monitor-enable-reactivates-others-again, round 2: round 1's
        // fix (a single settle-poll then a single correction pass) did not hold up on
        // a live rig re-test. Reported symptom: the unexpectedly-activated monitor's
        // tile transiently disappeared, then reappeared ACTIVE. A single settle+correct
        // pass cannot defend against either mechanism that explains that: (a)
        // PollUntilStableActiveDevicePaths' "two consecutive reads agree" criterion can
        // report a false-positive "stable" reading during a mid-ramp plateau -- if a
        // still-negotiating target (EDID/HDCP renegotiation after Extend, timing wholly
        // undocumented by Microsoft, see ccd-topology-restore-findings) happens not to
        // change between two successive 150ms-spaced reads, the poll concludes "stable"
        // before that target has actually finished coming online, making it invisible
        // to ComputeUnexpectedlyActivated for that round; or (b) the target genuinely
        // gets corrected off, then comes back on by itself moments later (e.g. this
        // rig's signal chain re-triggering Windows' own "new display connected"
        // auto-extend once the target's handshake finishes, independent of and after
        // this method's own correction call). Both look identical to the caller: "the
        // correction looked fine but the monitor is on anyway." Re-run the whole
        // settle+correct cycle for a bounded number of rounds, and only trust a
        // "nothing unexpected" verdict once it holds for two CONSECUTIVE rounds
        // (mirroring the settle-poll's own stability idiom one level up) -- so a
        // late-arriving activation that slipped past an earlier round's premature
        // "stable" read, or one that reappeared after an earlier round's correction, is
        // still observed and corrected within this same ActivateMonitors call.
        const int RequiredConsecutiveCleanRounds = 2;
        int consecutiveCleanRounds = 0;

        for (int round = 1; round <= MaxCorrectionRounds; round++)
        {
            var settledActiveDevicePaths = PollUntilStableActiveDevicePaths();

            // Whole-topology correction (rig-confirmed, see class-level remarks above):
            // Extend can reactivate device paths beyond the requested set. Any device
            // path that is active now, was NOT active before this call, and was NOT
            // requested must be turned back off — otherwise a single-tile "enable one
            // monitor" action could silently reactivate an unrelated, deliberately-
            // disabled monitor. Always compared against the ORIGINAL pre-Extend active
            // set and the original requested set (not a round-local baseline), so a
            // monitor corrected in an earlier round and still off is never re-flagged.
            IReadOnlySet<string> unexpectedlyActivated = ComputeUnexpectedlyActivated(
                currentlyActiveDevicePaths, settledActiveDevicePaths, monitorDevicePaths);

            // Debug session monitor-position-resets-to-de, round 7: the MIRROR IMAGE
            // correction -- rig-confirmed (checkpoint response, finding 6): a plain,
            // non-swap scoped ApplyPathInfos call threw PathChangeException, fell back to
            // whole-topology ApplyTopology(Extend), and Extend's own opaque persisted
            // layout did not include a monitor (SAM748A) that was active BEFORE this call
            // and was never any part of this call's disable-set -- it silently dropped
            // out. Before this fix, only "gained an unwanted activation" was ever
            // corrected; "lost an activation nobody asked to lose" had no correction path
            // at all, so ActivateMonitors reported EXIT success with a WRONG final state
            // (see ComputeUnexpectedlyDeactivated's own remarks for the full mechanism).
            // Always compared against the ORIGINAL pre-call active set and
            // monitorSwapDisableSet (not a round-local baseline), mirroring
            // unexpectedlyActivated's own convention above.
            IReadOnlySet<string> unexpectedlyDeactivated = ComputeUnexpectedlyDeactivated(
                currentlyActiveDevicePaths, settledActiveDevicePaths, monitorSwapDisableSet);

            Log($"ActivateMonitors: correction round {round}/{MaxCorrectionRounds} postExtendSettledActive=[{string.Join(", ", settledActiveDevicePaths)}] unexpectedlyActivated=[{string.Join(", ", unexpectedlyActivated)}] unexpectedlyDeactivated=[{string.Join(", ", unexpectedlyDeactivated)}]");

            if (unexpectedlyActivated.Count > 0 || unexpectedlyDeactivated.Count > 0)
            {
                consecutiveCleanRounds = 0;

                if (unexpectedlyActivated.Count > 0)
                {
                    // Reuses the already rig-proven CCD-removal path (repositioning-aware
                    // ApplyPathInfos + its own verify-and-throw) — never a manual
                    // PathTargetInfo/mode reconstruction (see class-level remarks).
                    DeactivateMonitors(unexpectedlyActivated);
                    Log($"ActivateMonitors: correction round {round}/{MaxCorrectionRounds} DeactivateMonitors({string.Join(", ", unexpectedlyActivated)}) completed without throwing.");
                }

                if (unexpectedlyDeactivated.Count > 0)
                {
                    // Round 7: reuses this SAME public method (a plain, non-swap-aware
                    // re-activation of exactly the dropped survivor(s)) rather than a new
                    // primitive — mirrors DeactivateMonitors' reuse above. Not unbounded
                    // recursion: each nested call only ever targets device paths this
                    // round found genuinely inactive, and the OUTER MaxCorrectionRounds/
                    // RequiredConsecutiveCleanRounds bound still governs how many rounds
                    // this loop keeps re-checking, exactly as for the opposite direction.
                    ActivateMonitors(unexpectedlyDeactivated, monitorSwapDisableSet: new HashSet<string>());
                    Log($"ActivateMonitors: correction round {round}/{MaxCorrectionRounds} nested ActivateMonitors({string.Join(", ", unexpectedlyDeactivated)}) completed without throwing.");
                }
            }
            else
            {
                consecutiveCleanRounds++;
                if (consecutiveCleanRounds >= RequiredConsecutiveCleanRounds)
                {
                    break;
                }
            }
        }

        // Verify-and-throw (D-03/D-04 discipline): re-query, confirm every requested
        // device path is now active AND (round 7) every device path that was active
        // BEFORE this call and is not in monitorSwapDisableSet is STILL active — the
        // correction loop above should already have restored it within its bounded
        // rounds, but if it could not (correction budget exhausted, or the nested
        // ActivateMonitors retry itself failed), this must surface as a thrown
        // exception, never a silently-wrong EXIT success (D-05: no further automatic
        // recovery is attempted, but a genuine failure must never be reported as
        // success). Never trust a non-throwing return alone, never use Screen.AllScreens
        // as the oracle.
        PathInfo[] postCorrection = PathInfo.GetActivePaths(virtualModeAware: false);
        var postCorrectionActiveDevicePaths = postCorrection.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath).ToHashSet();
        var stillInactive = monitorDevicePaths.Except(postCorrectionActiveDevicePaths)
            .Concat(currentlyActiveDevicePaths.Where(dp => !monitorSwapDisableSet.Contains(dp) && !postCorrectionActiveDevicePaths.Contains(dp)))
            .Distinct()
            .ToArray();

        if (stillInactive.Length > 0)
        {
            Log($"ActivateMonitors: EXIT throwing -- still inactive after correction: [{string.Join(", ", stillInactive)}].");
            throw new InvalidOperationException(
                $"Monitor enable did not take effect: {string.Join(", ", stillInactive)}. " +
                "No further automatic recovery is attempted (D-05).");
        }

        // Round 7: reuses postCorrectionActiveDevicePaths (computed above for the
        // extended verify-and-throw check) instead of re-deriving an identical set —
        // no behavior change, just removes a duplicate computation.
        Log($"ActivateMonitors: EXIT success -- all requested paths active. finalActive=[{string.Join(", ", postCorrectionActiveDevicePaths)}].");
        ObservePostApplyStability("ActivateMonitors", postCorrectionActiveDevicePaths);
    }

    // Debug session monitor-position-resets-to-de, round 3: shared "cache each of these
    // paths' real, live mode (Position/Resolution/PixelFormat) right now, while it is still
    // genuinely active" helper -- extracted from DeactivateMonitors (whose own call site is
    // unchanged in behavior) so ActivateMonitors' swap-exclusion logic can populate the SAME
    // cache for a survivor it is about to exclude from a scoped activation plan, before
    // DeactivateMonitors ever runs for that path (which may find it already inactive, per
    // round 3's fix, and skip its own capture via its no-op fast path). A PathInfo can carry
    // multiple TargetsInfo entries -- caches under every DevicePath it exposes, all pointing
    // at the same live PathInfo.
    private void CacheLiveModes(IEnumerable<PathInfo> paths)
    {
        foreach (PathInfo path in paths)
        {
            foreach (PathTargetInfo targetInfo in path.TargetsInfo)
            {
                _lastKnownActiveModeByDevicePath[targetInfo.DisplayTarget.DevicePath] = path;
            }
        }
    }

    // Debug session monitor-enable-reactivates-others-again, round 5: builds an explicit, scoped
    // PathInfo[] plan -- every currently active path (survivors, passed in fresh, never a stale/cached
    // array per ccd-topology-restore-findings finding 3) plus one newly-active PathInfo per requested
    // device path -- that a currently-inactive monitor NOT in devicePathsToActivate (e.g. SAM748A) is
    // structurally never part of, because this never enumerates "the whole topology"; it only ever
    // touches the specific device paths passed in. Prefers, per requested device path, the first
    // GetAllPaths() candidate whose PathDisplaySource is not already claimed by an active survivor or an
    // earlier target in this same batch (ccd-topology-restore-findings finding 2's source-collision
    // rule: two targets sharing one PathDisplaySource causes the apply to silently mis-land). Returns
    // false (with a logged reason, never a thrown exception for the "no candidate" case) if no valid
    // plan can be built for every requested path, so the caller can cleanly fall back to
    // ApplyTopology(Extend) instead of risking a partially-scoped, partially-whole-topology mutation.
    //
    // Debug session monitor-position-resets-to-de: instance method (not static, unlike round 5's
    // original) so it can consult _lastKnownActiveModeByDevicePath — when a requested device path has a
    // cached mode (captured live by DeactivateMonitors the last time this exact target was disabled),
    // that real Position/Resolution/PixelFormat is supplied instead of leaving mode info blank, so the
    // target lands back where it was instead of wherever the driver's best-mode-logic defaults it.
    //
    // Round 4: monitorSwapDisableSet-matching currentActivePaths entries are now KEPT in the
    // array (as an explicit, mode-blanked, reflection-patched-inactive entry -- see the loop
    // below) rather than dropped entirely -- round 3's "just drop them" approach produced an
    // array degenerate enough for the CCD API to reject outright on a fourth rig test (see
    // class remarks). Their caller-side live-mode caching still already happened in
    // ActivateMonitors (CacheLiveModes) before this method was called -- this method's own
    // mode-blanking of their in-array entry is a SEPARATE concern (representing "present but
    // inactive" for THIS call's array) from that caching (preserving position for a FUTURE
    // re-activation), never a conflict between the two. An empty monitorSwapDisableSet (the
    // non-swap, single-target case) makes this identical to round 5's original, byte-for-byte
    // -- explicitlyInactiveDisableSet stays empty and PromoteToOriginIfNeeded is a no-op
    // whenever an existing active survivor is already at (0,0), which is always true for that
    // case since nothing was excluded.
    private bool TryBuildScopedActivationPlan(
        IReadOnlySet<string> devicePathsToActivate,
        PathInfo[] currentActivePaths,
        IReadOnlySet<string> monitorSwapDisableSet,
        out PathInfo[] plan,
        out string failureReason)
    {
        FieldInfo isPathActiveField;
        try
        {
            isPathActiveField = ResolveIsPathActiveBackingField();
        }
        catch (InvalidOperationException ex)
        {
            plan = Array.Empty<PathInfo>();
            failureReason = ex.Message;
            return false;
        }

        // Path-level grouping (any target on the path matches monitorSwapDisableSet) mirrors
        // DeactivateMonitors' own `targets` filter above -- consistent with this file's
        // existing "one target per path in practice" assumption.
        var keptActiveSurvivors = new List<PathInfo>();
        var explicitlyInactiveDisableSet = new List<PathInfo>();

        foreach (PathInfo path in currentActivePaths)
        {
            bool isSwapDisableSetPath = path.TargetsInfo.Any(t => monitorSwapDisableSet.Contains(t.DisplayTarget.DevicePath));

            if (isSwapDisableSetPath)
            {
                // Round 4: reuses the SAME reflection-patch-a-readonly-backing-field technique used
                // below to force a target ACTIVE, here toggling the opposite direction (true -> false)
                // on a path that IS currently active -- researched and confirmed as the correct pattern
                // ("clear the active flag, keep the path in the array, never omit it") against an OSR
                // Developer Community CCD thread and a SetDisplayConfig usage write-up, both independent
                // of and corroborating each other. The no-mode PathInfo constructor below leaves
                // IsModeInformationAvailable false, which GetDisplayConfigPathInfos (confirmed by
                // decompile) maps to an invalid mode index -- matching the researched "inactive paths'
                // mode index must be invalidated" requirement -- and frees this path's
                // PathDisplaySource for the new target(s) below to claim (never added to
                // claimedSources).
                foreach (PathTargetInfo targetInfo in path.TargetsInfo)
                {
                    isPathActiveField.SetValue(targetInfo, false);
                }

                explicitlyInactiveDisableSet.Add(new PathInfo(path.DisplaySource, path.TargetsInfo));
            }
            else
            {
                keptActiveSurvivors.Add(path);
            }
        }

        var claimedSources = new HashSet<PathDisplaySource>(keptActiveSurvivors.Select(p => p.DisplaySource));
        PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
        var newPaths = new List<PathInfo>();

        foreach (string devicePath in devicePathsToActivate)
        {
            // 06-RESEARCH.md Pitfall 1's "pitfall inside the pitfall" (already load-bearing elsewhere in
            // this file, e.g. GetAllMonitors()/ActivateMonitors' own missing-target guard): DevicePath and
            // FriendlyName throw TargetNotAvailableException on a PathDisplayTarget whose IsAvailable is
            // false. GetAllPaths() returns many candidate (source,target) pairs for targets that are not
            // currently connected at all -- IsAvailable MUST be checked first (short-circuit &&), never
            // after, or this would crash on the very first unavailable candidate instead of skipping it.
            PathInfo? candidate = allPaths.FirstOrDefault(p =>
                !claimedSources.Contains(p.DisplaySource) &&
                p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == devicePath));

            if (candidate == null)
            {
                plan = Array.Empty<PathInfo>();
                failureReason = $"no unclaimed PathDisplaySource found for {devicePath} " +
                    "(every GetAllPaths() candidate source for this target is already in use by an " +
                    "active survivor or another target in this same activation batch)";
                return false;
            }

            PathTargetInfo targetInfo = candidate.TargetsInfo.First(
                t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == devicePath);

            // Reuses the SAME already rig-proven reflection-patch-a-readonly-backing-field technique
            // ccd-topology-restore-findings finding 1 documents for OutputTechnology -- here for
            // IsPathActive instead, on a reused (never manually reconstructed) PathTargetInfo object
            // taken directly from this call's own fresh GetAllPaths() read, so every OTHER field
            // (OutputTechnology, Rotation, Scaling, FrequencyInMillihertz, ScanLineOrdering,
            // DisplayTarget identity) is the driver's own real, current value -- never hand-filled.
            isPathActiveField.SetValue(targetInfo, true);

            claimedSources.Add(candidate.DisplaySource);

            // Debug session monitor-position-resets-to-de: prefer the target's own real, live mode from
            // the last time DeactivateMonitors saw it active (captured moments before removal, per
            // ccd-topology-restore-findings finding 5b) over letting the driver guess. This reuses the
            // same PathInfo(source, position, resolution, pixelFormat, targets) constructor overload
            // DeactivateMonitors' own survivor repositioning already uses safely elsewhere in this file —
            // not the rig-disproven manual-reconstruction path, since the data was captured live, never
            // from a stale/cached snapshot spanning a mutation boundary. Only when no cache entry exists
            // (e.g. app restarted between disable and enable, or this target was never seen active by
            // this controller instance) does this fall back to round 5's original blank-mode constructor
            // — IsModeInformationAvailable stays false, which is what tells SetDisplayConfig (via
            // DISPLAYCONFIG_PATH_MODE_IDX_INVALID, per ApplyPathInfos' own marshalling) to let the driver
            // pick mode information itself when forceModeEnumeration:true is passed by the caller.
            if (_lastKnownActiveModeByDevicePath.TryGetValue(devicePath, out PathInfo? cachedMode))
            {
                newPaths.Add(new PathInfo(
                    candidate.DisplaySource, cachedMode.Position, cachedMode.Resolution, cachedMode.PixelFormat,
                    new[] { targetInfo }));
            }
            else
            {
                newPaths.Add(new PathInfo(candidate.DisplaySource, new[] { targetInfo }));
            }
        }

        IReadOnlyList<PathInfo> activeEntries = PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        plan = activeEntries.Concat(explicitlyInactiveDisableSet).ToArray();
        failureReason = string.Empty;
        return true;
    }

    // Round 4: pure seam (unit-tested, RigToggle.Windows.Tests -- no live CCD hardware needed)
    // enforcing GDI's hard requirement that exactly one ACTIVE path in any supplied topology sit
    // at the desktop origin (0,0) -- confirmed via Microsoft's Desktop-Layout driver docs ("no
    // gaps, no overlaps" positioning) and a real-world SetDisplayConfig usage write-up
    // (blog.lohr.dev, "Changing the primary display on Windows by code") researched this round.
    // Mirrors DeactivateMonitors' own existing uniform-shift idiom
    // (`anyTargetWasPrimary` -> shift survivors so the first lands at (0,0)) for the activation
    // direction instead: if no entry across BOTH lists already sits at the origin (which happens
    // whenever the swap's disable-set excluded the CURRENT primary and left no other kept
    // survivor to inherit that role -- this round's rig-failing repro), promotes the first
    // mode-carrying entry (kept survivors preferred over brand-new targets -- an already-visible
    // monitor keeps its visibility while gaining primary status, rather than trusting a
    // newly-activated target's cached position, captured under a different, unrelated prior
    // arrangement, as the new origin) to (0,0) and shifts every OTHER mode-carrying entry by the
    // same delta.
    //
    // Entries with no cached mode (IsModeInformationAvailable=false -- a devicePathsToActivate
    // target this controller instance has no live-mode history for) are left completely
    // untouched; their position is left to the driver's own best-mode-logic
    // (forceModeEnumeration:true), exactly matching round 5's original, already-accepted
    // behavior for a target with no cache entry. If NO entry across both lists has any cached
    // mode at all, this is a no-op (nothing to reposition) -- same as pre-round-4 behavior.
    internal static IReadOnlyList<PathInfo> PromoteToOriginIfNeeded(
        IReadOnlyList<PathInfo> keptActiveSurvivors, IReadOnlyList<PathInfo> newPaths)
    {
        bool IsAtOrigin(PathInfo p) => p.IsModeInformationAvailable && p.Position.IsEmpty;

        if (keptActiveSurvivors.Any(IsAtOrigin) || newPaths.Any(IsAtOrigin))
        {
            return keptActiveSurvivors.Concat(newPaths).ToArray();
        }

        PathInfo? promoted = keptActiveSurvivors.FirstOrDefault(p => p.IsModeInformationAvailable)
            ?? newPaths.FirstOrDefault(p => p.IsModeInformationAvailable);

        if (promoted == null)
        {
            return keptActiveSurvivors.Concat(newPaths).ToArray();
        }

        var delta = new Point(-promoted.Position.X, -promoted.Position.Y);

        PathInfo Shift(PathInfo p) => p.IsModeInformationAvailable
            ? new PathInfo(
                p.DisplaySource,
                new Point(p.Position.X + delta.X, p.Position.Y + delta.Y),
                p.Resolution,
                p.PixelFormat,
                p.TargetsInfo)
            : p;

        return keptActiveSurvivors.Select(Shift).Concat(newPaths.Select(Shift)).ToArray();
    }

    private static FieldInfo? _isPathActiveBackingFieldCache;

    // Lazily resolved (not a static-initializer throw, which would poison every future call in the
    // AppDomain via TypeInitializationException) so a failure here only ever affects one
    // TryBuildScopedActivationPlan attempt, which is already caught and treated as a clean fallback to
    // ApplyTopology(Extend) by its caller -- never a crash of the whole enable action.
    private static FieldInfo ResolveIsPathActiveBackingField()
    {
        if (_isPathActiveBackingFieldCache != null)
        {
            return _isPathActiveBackingFieldCache;
        }

        FieldInfo? field = typeof(PathTargetInfo).GetField(
            "<IsPathActive>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (field == null)
        {
            // Thrown loudly, not silently swallowed, per ccd-topology-restore-findings finding 1's own
            // explicit recommendation for this exact reflection-patch technique -- caught by
            // TryBuildScopedActivationPlan's caller and treated as "scoped plan unavailable, fall back to
            // Extend", but this message makes a future WindowsDisplayAPI package upgrade's internal field
            // rename immediately diagnosable in debug.log instead of silently degrading forever.
            throw new InvalidOperationException(
                "WindowsDisplayAPI.DisplayConfig.PathTargetInfo no longer exposes a " +
                "'<IsPathActive>k__BackingField' field -- round 5's scoped-activation reflection patch " +
                "can no longer force a reused inactive PathTargetInfo active. A WindowsDisplayAPI package " +
                "upgrade likely changed its internal field layout; TryBuildScopedActivationPlan must be " +
                "revisited before scoped activation can be used again.");
        }

        _isPathActiveBackingFieldCache = field;
        return field;
    }

    // Pure seam (unit-tested, RigToggle.Windows.Tests — no live CCD hardware needed)
    // extracted from ActivateMonitors so the rig-confirmed "Extend reactivates
    // unrelated disabled monitors" correction is directly testable. A device path
    // belongs in the result iff it is active after Extend, was NOT active before
    // Extend, and was NOT part of the caller's requested set — i.e. it came back
    // "for free" as an unrequested side effect of the whole-topology Extend call.
    internal static IReadOnlySet<string> ComputeUnexpectedlyActivated(
        IReadOnlySet<string> preActiveDevicePaths,
        IReadOnlySet<string> postActiveDevicePaths,
        IReadOnlySet<string> requestedDevicePaths)
    {
        return postActiveDevicePaths
            .Where(dp => !preActiveDevicePaths.Contains(dp) && !requestedDevicePaths.Contains(dp))
            .ToHashSet();
    }

    // Debug session monitor-position-resets-to-de, round 7: the MIRROR IMAGE of
    // ComputeUnexpectedlyActivated above -- pure seam (unit-tested, RigToggle.Windows.Tests
    // -- no live CCD hardware needed) extracted from ActivateMonitors' correction loop so
    // the round-7 fix for a rig-confirmed "wrong final state" defect is directly testable.
    // A device path belongs in the result iff it was active BEFORE this ActivateMonitors
    // call, is NOT one of the paths this call deliberately intends to deactivate
    // (monitorSwapDisableSet -- the swap-exclusion case, where going inactive is the
    // INTENDED outcome, never a defect), and is NOT active after the call/correction round
    // -- i.e. a previously-active, non-excluded survivor silently dropped out as an
    // unrequested side effect (rig-confirmed: the whole-topology ApplyTopology(Extend)
    // fallback path restores the CCD persistence database's own last-known layout, which
    // is not guaranteed to include every monitor that happened to be active going in --
    // see class remarks). Before this fix, ActivateMonitors' correction loop and its own
    // verify-and-throw ONLY checked the caller's requested set (monitorDevicePaths) for
    // "did the target(s) I asked for end up active" -- a previously-active, unrequested
    // survivor silently going inactive as a side effect was invisible to both, so
    // ActivateMonitors reported EXIT success with a WRONG final state (the survivor
    // permanently off, with no further correction attempted), and that wrong state was
    // then baked into MainForm's own intent baseline (ArmIntentGuard snapshots whatever
    // GetAllMonitors() observes right after this call returns), so even the reactive
    // watchdog could never catch it -- it believed the survivor being off was correct.
    internal static IReadOnlySet<string> ComputeUnexpectedlyDeactivated(
        IReadOnlySet<string> preActiveDevicePaths,
        IReadOnlySet<string> postActiveDevicePaths,
        IReadOnlySet<string> monitorSwapDisableSet)
    {
        return preActiveDevicePaths
            .Where(dp => !monitorSwapDisableSet.Contains(dp) && !postActiveDevicePaths.Contains(dp))
            .ToHashSet();
    }

    // Debug session monitor-enable-reactivates-others-again: the shared "which device
    // paths are active right now" idiom, extracted so ActivateMonitors' pre-Extend read
    // and its (now polling) post-Extend read use identically-shaped queries.
    private static HashSet<string> QueryActiveDevicePaths()
    {
        return PathInfo.GetActivePaths(virtualModeAware: false)
            .SelectMany(p => p.TargetsInfo)
            .Select(t => t.DisplayTarget.DevicePath)
            .ToHashSet();
    }

    // Debug session monitor-enable-reactivates-others-again: bounded settle-poll for
    // the post-Extend active-device-path snapshot ActivateMonitors' correction step
    // relies on. SetDisplayConfig(Extend) returning is not documented anywhere as
    // proof every topology-restored target has finished coming online at the
    // driver/EDID level -- a single immediate re-query can under-report the active
    // set, silently hiding an unrequested monitor from ComputeUnexpectedlyActivated.
    // Reads repeatedly (attempt 1 with zero delay, matching the pre-fix cost for the
    // common already-settled case) until two consecutive reads agree, or gives up
    // after MaxSettlePollAttempts and returns the last read. Every attempt is logged
    // (best-effort, EnableDebugLogging-gated via Program.cs's TextWriterTraceListener)
    // so a rig trial's debug.log directly shows whether/when the snapshot changed
    // between attempts.
    private static readonly TimeSpan SettlePollDelay = TimeSpan.FromMilliseconds(150);
    private const int MaxSettlePollAttempts = 5;

    // Debug session monitor-enable-reactivates-others-again, round 2: bounds how many
    // times ActivateMonitors re-runs the settle-poll-then-correct cycle after Extend.
    // See the round-2 remarks inside ActivateMonitors for why one pass is not enough
    // (a settle-poll that looks "stable" too early, or a monitor that reactivates
    // again after being corrected, are both invisible to a single pass).
    private const int MaxCorrectionRounds = 3;

    private static HashSet<string> PollUntilStableActiveDevicePaths()
    {
        HashSet<string> previous = QueryActiveDevicePaths();
        Log($"Post-Extend settle poll, attempt 1/{MaxSettlePollAttempts}: [{string.Join(", ", previous)}]");

        for (int attempt = 2; attempt <= MaxSettlePollAttempts; attempt++)
        {
            Thread.Sleep(SettlePollDelay);
            HashSet<string> current = QueryActiveDevicePaths();
            Log($"Post-Extend settle poll, attempt {attempt}/{MaxSettlePollAttempts}: [{string.Join(", ", current)}]");

            if (current.SetEquals(previous))
            {
                return current;
            }

            previous = current;
        }

        Log("Post-Extend settle poll: did not stabilize within the attempt budget; using the last read.");
        return previous;
    }

    // Debug session monitor-position-resets-to-de, round 5: PURE EVIDENCE-GATHERING, not a
    // fix -- deliberately does not attempt any correction of its own (that remains
    // MainForm's TryReactivelyCorrectAgainstLastIntent's job). Four rounds of reactive
    // corrections (rounds 2-4) have each addressed a DIFFERENT, real, code-level gap this
    // debug session's own history documents, but none has explained -- or even directly
    // observed -- the underlying delayed (~2-3.5s, every round so far), silent OS-level
    // revert of an apparently-successful, verified CCD apply back toward the pre-apply
    // topology (Symptom 2 part B/3). The only visibility into it across all five rounds has
    // been the OS's own coarse-grained OnDisplaySettingsChanged notification plus whatever
    // state MainForm.RefreshMonitorTiles happened to observe at that moment -- there is no
    // rig evidence anywhere in this file of what the active-path set looks like DURING the
    // revert, only "verified correct right after apply" and "wrong again by the time the next
    // notification fired". This starts a bounded (PostApplyObservationDuration), fine-grained
    // (PostApplyObservationInterval) background poll of the live active-path set immediately
    // after EVERY successful ActivateMonitors/DeactivateMonitors call, on its own thread (so it
    // never blocks the caller, never competes with MainForm's correction-lease/budget
    // discipline, and never delays returning control to whichever code path -- toggle or tile
    // -- is waiting on this call). Logs every observed CHANGE (not every poll tick, to keep the
    // log readable) with a millisecond-precision offset from the moment this method started
    // observing, so the next rig trial's debug.log directly answers: is the revert abrupt (one
    // clean flip) or gradual (a target flickers/renegotiates before settling)? Is its timing a
    // suspiciously fixed interval across repeated trials (suggesting a driver/OS timer-based
    // mechanism) or variable (suggesting a negotiation/handshake-based one, e.g. EDID/HDCP
    // re-handshake noted elsewhere in this file's remarks)? Does it ever NOT happen (i.e. does
    // the topology stay stable for the full observation window on some fraction of calls)?
    // Answering these is a prerequisite for forming a genuinely falsifiable root-cause
    // hypothesis for Symptom 2 part B/3 -- guessing at a specific external mechanism (a GPU
    // vendor control-panel service, a Windows display-database reconciliation timer, etc.)
    // without first seeing the revert's own shape firsthand would repeat this session's
    // established pattern of speculative, reactive-only fixes.
    private static void ObservePostApplyStability(string context, IReadOnlySet<string> baselineActiveDevicePaths)
    {
        var thread = new Thread(() =>
        {
            try
            {
                var previous = new HashSet<string>(baselineActiveDevicePaths);
                var stopwatch = Stopwatch.StartNew();
                Log($"ObservePostApplyStability[{context}]: baseline=[{string.Join(", ", previous)}] -- polling every {PostApplyObservationInterval.TotalMilliseconds:F0}ms for {PostApplyObservationDuration.TotalSeconds:F0}s (evidence-gathering only, no correction attempted here).");

                while (stopwatch.Elapsed < PostApplyObservationDuration)
                {
                    Thread.Sleep(PostApplyObservationInterval);

                    // Debug session monitor-position-resets-to-de, round 6 (checkpoint
                    // response, item 4a): QueryActiveDevicePaths calls
                    // PathDisplayTarget.DevicePath with no IsAvailable filter (unlike
                    // GetAllMonitors/TryBuildScopedActivationPlan elsewhere in this file,
                    // which both filter first specifically because that getter throws
                    // WindowsDisplayAPI.Exceptions.TargetNotAvailableException on an
                    // unavailable target) -- a target that was active a moment ago can
                    // transiently report unavailable while a CCD topology mutation is still
                    // in flight elsewhere, since this background thread has no coordination
                    // with any concurrent Activate/DeactivateMonitors call, by design. Before
                    // this fix, an exception here was only caught by the OUTER try/catch
                    // below, which ends the whole observation thread -- rig-confirmed: a 6s
                    // window died at +898ms with zero further data for the remainder. This is
                    // pure evidence-gathering instrumentation, never a correction -- a
                    // transient per-tick read failure must cost only that tick, never the
                    // rest of the window.
                    try
                    {
                        HashSet<string> current = QueryActiveDevicePaths();

                        if (!current.SetEquals(previous))
                        {
                            Log($"ObservePostApplyStability[{context}]: CHANGE detected at +{stopwatch.Elapsed.TotalMilliseconds:F0}ms -- was=[{string.Join(", ", previous)}] now=[{string.Join(", ", current)}].");
                            previous = current;
                        }
                    }
                    catch (Exception tickEx)
                    {
                        Log($"ObservePostApplyStability[{context}]: poll tick at +{stopwatch.Elapsed.TotalMilliseconds:F0}ms failed ({tickEx.GetType().Name}: {tickEx.Message}) -- skipping this tick, observation continues.");
                    }
                }

                Log($"ObservePostApplyStability[{context}]: observation window closed at +{stopwatch.Elapsed.TotalMilliseconds:F0}ms -- final=[{string.Join(", ", previous)}].");
            }
            catch (Exception ex)
            {
                // Diagnostic-only -- never let an observation-thread failure surface anywhere
                // (matches every other Log()/best-effort convention in this file). Round 6:
                // this outer catch remains as a last-resort backstop for anything OUTSIDE the
                // per-tick try/catch above (e.g. a hypothetical Log()-internal failure that
                // still somehow propagates, or Stopwatch/HashSet construction) -- the common,
                // rig-observed transient-query-failure case is now handled per-tick above and
                // no longer reaches here.
                try
                {
                    Log($"ObservePostApplyStability[{context}]: observation thread failed: {ex}");
                }
                catch
                {
                    // Logging is diagnostic-only.
                }
            }
        })
        {
            IsBackground = true,
            Name = "RigToggle-PostApplyObserver",
        };
        thread.Start();
    }

    private static readonly TimeSpan PostApplyObservationInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PostApplyObservationDuration = TimeSpan.FromSeconds(6);

    // Best-effort diagnostic logging (debug session monitor-enable-reactivates-others-again),
    // matching the one logging convention already used elsewhere in this codebase
    // (e.g. WindowsAppController.Log, ToggleService's Trace.WriteLine calls) so
    // RigToggle.App's TextWriterTraceListener (wired in Program.cs, gated behind
    // AppSettings.EnableDebugLogging) persists it to %LOCALAPPDATA%\RigToggle\debug.log
    // for the user to read back after a rig test. Never throws -- a logging failure
    // must never affect the monitor toggle itself.
    private static void Log(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsMonitorController: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect toggle behavior.
        }
    }

    // Pure seam (unit-tested, RigToggle.Windows.Tests — no live CCD hardware needed)
    // extracted from DeactivateMonitors (debug session toggle-false-incomplete-error)
    // so the "already-disabled is not an error" fix is directly testable. A requested
    // device path belongs in the result iff Windows does not currently detect it at
    // all (absent from availableDevicePaths, which callers derive from
    // GetAllPaths()+IsAvailable — the same "is this device known to Windows"
    // oracle ActivateMonitors' own missing-target guard already uses). A requested
    // path that IS detected but simply inactive (already disabled) is deliberately
    // NOT included here — it already satisfies DeactivateMonitors' "end up disabled"
    // postcondition and must not be treated as an error.
    internal static IReadOnlySet<string> ComputeUndetectedDevicePaths(
        IReadOnlySet<string> requestedDevicePaths,
        IReadOnlySet<string> availableDevicePaths)
    {
        return requestedDevicePaths
            .Where(dp => !availableDevicePaths.Contains(dp))
            .ToHashSet();
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
        // Round 4 ("make debug log actually debug stuff"): same unconditional ENTER/EXIT
        // logging discipline as ActivateMonitors above -- this method's every early-return
        // path (no-op, already-disabled, missing) was previously silent, which is exactly
        // the kind of gap that can make a healthy, logging-enabled rig run still produce an
        // ambiguous or thin log.
        Log($"DeactivateMonitors: ENTER requested=[{string.Join(", ", monitorDevicePaths)}]");

        if (monitorDevicePaths.Count == 0)
        {
            Log("DeactivateMonitors: EXIT no-op (empty request set, e.g. enable-only config on toggle-back).");
            return; // no-op, e.g. enable-only config on toggle-back
        }

        PathInfo[] currentPaths = PathInfo.GetActivePaths(virtualModeAware: false);

        PathInfo[] targets = currentPaths
            .Where(p => p.TargetsInfo.Any(t => monitorDevicePaths.Contains(t.DisplayTarget.DevicePath)))
            .ToArray();

        // debug session toggle-false-incomplete-error: a requested-to-disable device
        // path that is NOT currently active is not necessarily an error — it may
        // already be disabled (e.g. manually disabled via the tile dashboard before
        // this toggle ran, and also configured as disabled in the target mode), which
        // already satisfies this method's "end up disabled" postcondition for that
        // path with nothing left to do. The only genuine error is a requested device
        // path Windows doesn't detect AT ALL (unplugged/disconnected) — checked
        // against GetAllPaths()+IsAvailable, the same "is this device known to
        // Windows at all" oracle ActivateMonitors already uses for its own
        // missing-target guard above. Using GetActivePaths() alone (the pre-fix
        // check) could never distinguish "already disabled" from "never detected".
        PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
        var availableDevicePaths = allPaths
            .SelectMany(p => p.TargetsInfo)
            .Where(t => t.DisplayTarget.IsAvailable)
            .Select(t => t.DisplayTarget.DevicePath)
            .ToHashSet();
        var missing = ComputeUndetectedDevicePaths(monitorDevicePaths, availableDevicePaths);

        if (missing.Count > 0)
        {
            Log($"DeactivateMonitors: EXIT throwing -- not detected: [{string.Join(", ", missing)}].");
            throw new InvalidOperationException(
                $"Configured monitor(s) not detected: {string.Join(", ", missing)}");
        }

        if (targets.Length == 0)
        {
            // Every requested device path is already inactive (mirrors ActivateMonitors'
            // own "nothing to do" skip-optimization above, Pitfall 3) — already disabled,
            // already satisfies the postcondition. Never call ApplyPathInfos just to
            // reapply the unchanged current topology.
            Log("DeactivateMonitors: EXIT no-op (every requested path already inactive).");
            return;
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
            Log("DeactivateMonitors: EXIT throwing -- every currently active display is in the requested set, none would survive.");
            throw new InvalidOperationException(
                "Cannot disable all configured monitors — at least one active display must remain.");
        }

        // Debug session monitor-position-resets-to-de: cache each target's real, live mode
        // (Position/Resolution/PixelFormat) NOW, immediately before it is removed below --
        // while the data is still genuinely live, per ccd-topology-restore-findings finding
        // 5b ("take mode/signal values from the stored snapshot taken while the path was
        // active"). Consumed by TryBuildScopedActivationPlan so a later re-activation of
        // this exact device path can restore its position instead of falling back to the
        // driver's best-mode-logic default (round 5's original behavior, which caused the
        // position to reset). Round 3: extracted to CacheLiveModes so ActivateMonitors'
        // own swap-exclusion logic can call the identical capture for a survivor it is
        // about to exclude from a scoped activation plan, before this method ever runs for
        // that path (which, after round 3's fix, will often already be inactive by then).
        CacheLiveModes(targets);

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

        Log($"DeactivateMonitors: applying -- targets=[{string.Join(", ", targets.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath))}] survivors=[{string.Join(", ", survivors.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath))}] anyTargetWasPrimary={anyTargetWasPrimary}.");

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
            Log($"DeactivateMonitors({string.Join(", ", monitorDevicePaths)}): EXIT throwing -- verify FAILED " +
                $"(anyTargetStillActive={anyTargetStillActive}, exactlyOnePrimary={exactlyOnePrimary}, overlap={overlap}) " +
                $"activeAfter=[{string.Join(", ", verifyPaths.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath))}].");
            throw new InvalidOperationException(
                $"Monitor disable did not take effect as expected (anyTargetStillActive={anyTargetStillActive}, " +
                $"exactlyOnePrimary={exactlyOnePrimary}, overlap={overlap}). " +
                "No further automatic recovery is attempted (D-05).");
        }

        // Debug session monitor-enable-reactivates-others-again, round 2 (checkpoint
        // diagnostic request c): log the post-mutation verify state even on the
        // non-throwing (success) path, not just inside the exception message above --
        // ActivateMonitors' correction loop calls this method to turn an unexpectedly-
        // activated monitor back off, and a rig trial needs to see directly whether
        // that call's OWN verify observed a clean deactivation, to distinguish "this
        // call's mutation genuinely succeeded and something else re-activated the
        // monitor afterward" from "this call's own verify was already unreliable."
        var finalActiveDevicePathsAfterDeactivate = verifyPaths.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath).ToHashSet();
        Log($"DeactivateMonitors({string.Join(", ", monitorDevicePaths)}): EXIT success -- verified OK -- activeAfter=[{string.Join(", ", finalActiveDevicePathsAfterDeactivate)}] exactlyOnePrimary={exactlyOnePrimary} overlap={overlap}.");
        ObservePostApplyStability("DeactivateMonitors", finalActiveDevicePathsAfterDeactivate);
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
