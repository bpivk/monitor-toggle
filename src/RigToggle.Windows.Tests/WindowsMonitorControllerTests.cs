using System.Drawing;
using System.Linq;
using RigToggle.Core.Models;
using RigToggle.Windows;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;
using Xunit;

namespace RigToggle.Windows.Tests;

// Covers the pure, unit-testable helpers exposed off WindowsMonitorController via
// InternalsVisibleTo: the AnyRectanglesOverlap bounding-box geometry check, the
// MergeAllMonitors dedup/promotion logic, the ComputeUnexpectedlyActivated
// Extend-side-effect-detection seam (debug session monitor-enable-affects-other),
// and the ComputeUndetectedDevicePaths already-disabled-vs-never-detected
// classification seam (debug session toggle-false-incomplete-error), all reachable
// without live display hardware. The mutating CCD methods (ActivateMonitors/
// DeactivateMonitors) are NOT unit-tested here — they call PathInfo.GetActivePaths()/
// GetAllPaths()/ApplyPathInfos()/ApplyTopology() directly, which are static calls
// into real native CCD APIs with no injectable seam, so they remain verified only
// via live rig testing (see 04-01/04-03 SUMMARY.md).
public class WindowsMonitorControllerTests
{
    // Covers the pure axis-aligned bounding-box overlap helper added in Phase 6 Plan
    // 03 (06-RESEARCH.md "Bounding-box overlap check" code example), used by
    // DeactivateMonitors' verify-and-throw section. Same "pure logic only, no live
    // CCD hardware" constraint as the rest of this file — the helper itself has no
    // dependency on WindowsDisplayAPI beyond System.Drawing.Rectangle.
    [Fact]
    public void AnyRectanglesOverlap_NonOverlappingSideBySide_ReturnsFalse()
    {
        var rects = new[]
        {
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(1920, 0, 2560, 1440),
        };

        Assert.False(WindowsMonitorController.AnyRectanglesOverlap(rects));
    }

    [Fact]
    public void AnyRectanglesOverlap_Overlapping_ReturnsTrue()
    {
        var rects = new[]
        {
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(1000, 0, 1920, 1080),
        };

        Assert.True(WindowsMonitorController.AnyRectanglesOverlap(rects));
    }

    [Fact]
    public void AnyRectanglesOverlap_Empty_ReturnsFalse()
    {
        Assert.False(WindowsMonitorController.AnyRectanglesOverlap(Array.Empty<Rectangle>()));
    }

    // Covers the gap-closure fix for GetAllMonitors()'s rig-confirmed
    // duplicate-rows/multi-primary bug (06-06-SUMMARY.md NO-GO): pure, unit-testable
    // dedup/merge logic extracted into MergeAllMonitors(), same "pure logic only, no
    // live CCD hardware" discipline as the rest of this file. MonitorInfo has a public
    // constructor (record) so inputs are constructed directly — no PathInfo/
    // PathDisplayTarget fakes needed.
    [Fact]
    public void MergeAllMonitors_DuplicatedActiveDevicePath_CollapsesToOneRow()
    {
        var active = new[]
        {
            new MonitorInfo("DEV1", "Monitor A", IsPrimary: true),
            new MonitorInfo("DEV1", "Monitor A", IsPrimary: true),
        };
        var availableTargets = Array.Empty<(string DevicePath, string FriendlyName)>();

        var result = WindowsMonitorController.MergeAllMonitors(active, availableTargets);

        Assert.Single(result);
        Assert.Equal("DEV1", result[0].DevicePath);
    }

    [Fact]
    public void MergeAllMonitors_DevicePathInBothInputs_YieldsOneActiveRow_NotDuplicateDisabledRow()
    {
        var active = new[] { new MonitorInfo("DEV1", "Monitor A", IsPrimary: true) };
        var availableTargets = new[] { ("DEV1", "Monitor A") };

        var result = WindowsMonitorController.MergeAllMonitors(active, availableTargets);

        Assert.Single(result);
        Assert.True(result[0].IsActive);
        Assert.True(result[0].IsPrimary);
    }

    [Fact]
    public void MergeAllMonitors_AvailableButNotActive_BecomesDisabledNonPrimaryRow()
    {
        var active = Array.Empty<MonitorInfo>();
        var availableTargets = new[] { ("DEV2", "Monitor B") };

        var result = WindowsMonitorController.MergeAllMonitors(active, availableTargets);

        MonitorInfo row = Assert.Single(result);
        Assert.Equal("DEV2", row.DevicePath);
        Assert.False(row.IsPrimary);
        Assert.False(row.IsActive);
    }

    [Fact]
    public void MergeAllMonitors_ActiveRows_PromotedToIsActiveTrue_WithPrimaryCarriedFromInput()
    {
        var active = new[]
        {
            new MonitorInfo("DEV1", "Monitor A", IsPrimary: true),
            new MonitorInfo("DEV2", "Monitor B", IsPrimary: false),
        };
        var availableTargets = Array.Empty<(string DevicePath, string FriendlyName)>();

        var result = WindowsMonitorController.MergeAllMonitors(active, availableTargets);

        Assert.Equal(2, result.Count);
        MonitorInfo primary = result.Single(r => r.DevicePath == "DEV1");
        MonitorInfo secondary = result.Single(r => r.DevicePath == "DEV2");
        Assert.True(primary.IsActive);
        Assert.True(primary.IsPrimary);
        Assert.True(secondary.IsActive);
        Assert.False(secondary.IsPrimary);
    }

    [Fact]
    public void MergeAllMonitors_RigRegressionScenario_ExactlyOneRowPerDevicePath_ExactlyOnePrimary()
    {
        // Mirrors the 06-06 rig NO-GO: 2 physical monitors, one active-primary and one
        // active-non-primary, plus stale duplicate PathInfo entries for both in
        // availableTargets, plus one genuinely-disabled third monitor.
        var active = new[]
        {
            new MonitorInfo("DEV1", "VG248", IsPrimary: true),
            new MonitorInfo("DEV2", "Dell U2415", IsPrimary: false),
        };
        var availableTargets = new[]
        {
            ("DEV1", "VG248"),
            ("DEV1", "VG248"),
            ("DEV2", "Dell U2415"),
            ("DEV2", "Dell U2415"),
            ("DEV2", "Dell U2415"),
            ("DEV3", "Disabled Monitor"),
        };

        var result = WindowsMonitorController.MergeAllMonitors(active, availableTargets);

        Assert.Equal(3, result.Count);
        Assert.Equal(3, result.Select(r => r.DevicePath).Distinct().Count());
        Assert.True(result.Count(r => r.IsPrimary) == 1);

        MonitorInfo disabled = result.Single(r => r.DevicePath == "DEV3");
        Assert.False(disabled.IsPrimary);
        Assert.False(disabled.IsActive);
    }

    // Covers the debug-session monitor-enable-affects-other fix: ActivateMonitors'
    // ApplyTopology(Extend) call is whole-topology, not scoped to the caller's
    // requested device path(s) — it can reactivate an unrelated, still-should-stay-
    // disabled monitor as a side effect. ComputeUnexpectedlyActivated is the pure
    // seam that detects this so the caller can correct it via DeactivateMonitors.
    // Same "pure logic only, no live CCD hardware" discipline as the rest of this file.
    [Fact]
    public void ComputeUnexpectedlyActivated_RigRegressionScenario_ExtendReactivatesOtherDisabledMonitor_FlagsIt()
    {
        // Mirrors the reported 3-monitor rig bug exactly: DEV1 (primary) stays active
        // throughout; DEV2 and DEV3 are both disabled before the tile click; the user
        // clicks DEV2's tile (requesting only DEV2); Extend comes back with DEV2 AND
        // DEV3 active. Only DEV3 (active, not previously active, not requested) should
        // be flagged for correction.
        var preActive = new HashSet<string> { "DEV1" };
        var postActive = new HashSet<string> { "DEV1", "DEV2", "DEV3" };
        var requested = new HashSet<string> { "DEV2" };

        var result = WindowsMonitorController.ComputeUnexpectedlyActivated(preActive, postActive, requested);

        Assert.Single(result);
        Assert.Contains("DEV3", result);
    }

    [Fact]
    public void ComputeUnexpectedlyActivated_ExtendOnlyActivatesRequestedMonitor_ReturnsEmpty()
    {
        // The pre-upgrade 2-monitor case (and the well-behaved 3-monitor case): Extend
        // activates exactly what was requested and nothing else — no correction needed.
        var preActive = new HashSet<string> { "DEV1" };
        var postActive = new HashSet<string> { "DEV1", "DEV2" };
        var requested = new HashSet<string> { "DEV2" };

        var result = WindowsMonitorController.ComputeUnexpectedlyActivated(preActive, postActive, requested);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeUnexpectedlyActivated_AlreadyActiveBeforeExtend_NotFlaggedEvenThoughUnrequested()
    {
        // A device path that was ALREADY active before Extend ran must never be
        // flagged for correction, even though it isn't in the requested set — it
        // wasn't caused by this call, so DeactivateMonitors must not touch it.
        var preActive = new HashSet<string> { "DEV1", "DEV3" };
        var postActive = new HashSet<string> { "DEV1", "DEV2", "DEV3" };
        var requested = new HashSet<string> { "DEV2" };

        var result = WindowsMonitorController.ComputeUnexpectedlyActivated(preActive, postActive, requested);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeUnexpectedlyActivated_NoNewActivations_ReturnsEmpty()
    {
        var preActive = new HashSet<string> { "DEV1", "DEV2" };
        var postActive = new HashSet<string> { "DEV1", "DEV2" };
        var requested = new HashSet<string> { "DEV2" };

        var result = WindowsMonitorController.ComputeUnexpectedlyActivated(preActive, postActive, requested);

        Assert.Empty(result);
    }

    // Covers the debug-session monitor-position-resets-to-de round-7 fix: the MIRROR
    // IMAGE of ComputeUnexpectedlyActivated above. Rig-reproduced scenario (round-6
    // checkpoint response, finding 6): a plain, non-swap ActivateMonitors(DEV2) call
    // (DEV1 already active, not part of any disable-set) has its scoped ApplyPathInfos
    // throw, falls back to whole-topology Extend, and Extend's own opaque persisted
    // layout does not include DEV1 -- DEV1 (active going in, never requested to be
    // touched, not in any disable-set) silently drops out. ComputeUnexpectedlyDeactivated
    // is the pure seam that detects this so the caller can correct it via a nested
    // ActivateMonitors call.
    [Fact]
    public void ComputeUnexpectedlyDeactivated_RigRegressionScenario_ExtendDropsUnrelatedActiveSurvivor_FlagsIt()
    {
        var preActive = new HashSet<string> { "DEV1" };
        var postActive = new HashSet<string> { "DEV2" };
        var monitorSwapDisableSet = new HashSet<string>();

        var result = WindowsMonitorController.ComputeUnexpectedlyDeactivated(preActive, postActive, monitorSwapDisableSet);

        Assert.Single(result);
        Assert.Contains("DEV1", result);
    }

    [Fact]
    public void ComputeUnexpectedlyDeactivated_SurvivorStaysActive_ReturnsEmpty()
    {
        // The common, well-behaved case: DEV1 was active before this call and is still
        // active after -- no correction needed.
        var preActive = new HashSet<string> { "DEV1" };
        var postActive = new HashSet<string> { "DEV1", "DEV2" };
        var monitorSwapDisableSet = new HashSet<string>();

        var result = WindowsMonitorController.ComputeUnexpectedlyDeactivated(preActive, postActive, monitorSwapDisableSet);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeUnexpectedlyDeactivated_DeliberateSwapExclusion_NotFlaggedEvenThoughInactive()
    {
        // A path in monitorSwapDisableSet going inactive is the INTENDED outcome of a
        // Rig/Normal swap (fix A) -- must never be flagged as an unexpected drop.
        var preActive = new HashSet<string> { "DEV1", "DEV2" };
        var postActive = new HashSet<string> { "DEV3" };
        var monitorSwapDisableSet = new HashSet<string> { "DEV1", "DEV2" };

        var result = WindowsMonitorController.ComputeUnexpectedlyDeactivated(preActive, postActive, monitorSwapDisableSet);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeUnexpectedlyDeactivated_MixedSwapAndNonSwapSurvivors_OnlyFlagsTheNonSwapOne()
    {
        // DEV1 is deliberately excluded (swap); DEV2 is a plain, unrelated survivor that
        // should have stayed active but didn't -- only DEV2 is flagged.
        var preActive = new HashSet<string> { "DEV1", "DEV2" };
        var postActive = new HashSet<string> { "DEV3" };
        var monitorSwapDisableSet = new HashSet<string> { "DEV1" };

        var result = WindowsMonitorController.ComputeUnexpectedlyDeactivated(preActive, postActive, monitorSwapDisableSet);

        Assert.Single(result);
        Assert.Contains("DEV2", result);
    }

    [Fact]
    public void ComputeUnexpectedlyDeactivated_NoPriorActiveSurvivors_ReturnsEmpty()
    {
        var preActive = new HashSet<string>();
        var postActive = new HashSet<string> { "DEV1" };
        var monitorSwapDisableSet = new HashSet<string>();

        var result = WindowsMonitorController.ComputeUnexpectedlyDeactivated(preActive, postActive, monitorSwapDisableSet);

        Assert.Empty(result);
    }

    // Covers the debug session toggle-false-incomplete-error fix: DeactivateMonitors'
    // pre-mutation guard previously conflated "requested but already inactive"
    // (already satisfies the disable postcondition — not an error) with "requested
    // but never detected by Windows at all" (a real, actionable error — unplugged/
    // disconnected monitor). ComputeUndetectedDevicePaths is the pure seam extracted
    // to make the corrected classification directly testable. Same "pure logic only,
    // no live CCD hardware" discipline as the rest of this file.
    [Fact]
    public void ComputeUndetectedDevicePaths_RequestedButAlreadyInactive_NotFlaggedAsMissing()
    {
        // Mirrors the reported bug exactly: DEV1 is manually disabled before the
        // toggle runs, and is also in Rig mode's disable-set — Windows still detects
        // it (it's in availableDevicePaths), it's just not currently active. This must
        // NOT be treated as an error.
        var requested = new HashSet<string> { "DEV1" };
        var available = new HashSet<string> { "DEV1", "DEV2" };

        var result = WindowsMonitorController.ComputeUndetectedDevicePaths(requested, available);

        Assert.Empty(result);
    }

    [Fact]
    public void ComputeUndetectedDevicePaths_RequestedButNeverDetected_FlaggedAsMissing()
    {
        // A genuinely unplugged/disconnected monitor is still a real error — this is
        // the case the original guard was protecting against and must still catch.
        var requested = new HashSet<string> { "DEV1", "DEV3" };
        var available = new HashSet<string> { "DEV1", "DEV2" };

        var result = WindowsMonitorController.ComputeUndetectedDevicePaths(requested, available);

        Assert.Single(result);
        Assert.Contains("DEV3", result);
    }

    [Fact]
    public void ComputeUndetectedDevicePaths_MixOfActiveAndAlreadyInactiveRequested_OnlyTrulyMissingFlagged()
    {
        // Mixed disable-set: DEV1 is currently active (needs deactivating), DEV2 is
        // already inactive (no-op, satisfied), DEV3 is not detected at all (error).
        // Only DEV3 should be flagged — DEV1 and DEV2 are both "available" regardless
        // of their active/inactive state, since availability is orthogonal to
        // activeness (callers derive `available` from GetAllPaths()+IsAvailable, not
        // from GetActivePaths()).
        var requested = new HashSet<string> { "DEV1", "DEV2", "DEV3" };
        var available = new HashSet<string> { "DEV1", "DEV2" };

        var result = WindowsMonitorController.ComputeUndetectedDevicePaths(requested, available);

        Assert.Single(result);
        Assert.Contains("DEV3", result);
    }

    [Fact]
    public void ComputeUndetectedDevicePaths_AllRequestedDetected_ReturnsEmpty()
    {
        var requested = new HashSet<string> { "DEV1", "DEV2" };
        var available = new HashSet<string> { "DEV1", "DEV2", "DEV3" };

        var result = WindowsMonitorController.ComputeUndetectedDevicePaths(requested, available);

        Assert.Empty(result);
    }

    // Covers the debug session monitor-position-resets-to-de round-4 fix: GDI requires exactly
    // one ACTIVE path in any supplied topology to sit at the desktop origin (0,0) -- a
    // requirement round 3's scoped-activation plan could silently violate whenever the swap's
    // disable-set excluded the current primary and left no other survivor to inherit that role.
    // PromoteToOriginIfNeeded is the pure seam that normalizes the final active-entry set, same
    // "pure logic only, no live CCD hardware" discipline as the rest of this file --
    // PathDisplaySource/PathDisplayAdapter/LUID all have public, hardware-independent
    // constructors, so no real CCD query is needed to build these fixtures.
    private static PathDisplaySource Source(uint sourceId) =>
        new(new PathDisplayAdapter(default), sourceId);

    private static PathInfo WithMode(uint sourceId, int x, int y, int width = 1920, int height = 1080) =>
        new(Source(sourceId), new Point(x, y), new Size(width, height), DisplayConfigPixelFormat.PixelFormat32Bpp);

    private static PathInfo NoMode(uint sourceId) => new(Source(sourceId));

    [Fact]
    public void PromoteToOriginIfNeeded_KeptSurvivorAlreadyAtOrigin_NoRepositioning()
    {
        var keptActiveSurvivors = new[] { WithMode(1, 0, 0) };
        var newPaths = new[] { WithMode(2, 999, 999) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Position == new Point(0, 0));
        Assert.Contains(result, p => p.Position == new Point(999, 999));
    }

    [Fact]
    public void PromoteToOriginIfNeeded_NewPathAlreadyAtOrigin_NoRepositioning()
    {
        var keptActiveSurvivors = Array.Empty<PathInfo>();
        var newPaths = new[] { WithMode(1, 0, 0) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        Assert.Single(result);
        Assert.Equal(new Point(0, 0), result[0].Position);
    }

    [Fact]
    public void PromoteToOriginIfNeeded_SoleNewTargetHasStaleNonOriginPosition_PromotedToOrigin()
    {
        // Mirrors the round-4 rig repro exactly: every kept survivor was excluded by the swap's
        // disable-set (empty here), and the sole newly-activated target's cached mode is a
        // stale, non-origin position from when it was previously a non-primary monitor.
        var keptActiveSurvivors = Array.Empty<PathInfo>();
        var newPaths = new[] { WithMode(1, 1920, 0) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        PathInfo promoted = Assert.Single(result);
        Assert.Equal(new Point(0, 0), promoted.Position);
    }

    [Fact]
    public void PromoteToOriginIfNeeded_NoEntryAtOrigin_KeptSurvivorPromotedOverNewTarget_OthersShiftedBySameDelta()
    {
        var keptActiveSurvivors = new[] { WithMode(1, 1920, 0) };
        var newPaths = new[] { WithMode(2, 3840, 0) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        Assert.Equal(2, result.Count);
        PathInfo promotedSurvivor = result.Single(p => p.DisplaySource.Equals(Source(1)));
        PathInfo shiftedNewTarget = result.Single(p => p.DisplaySource.Equals(Source(2)));
        Assert.Equal(new Point(0, 0), promotedSurvivor.Position);
        Assert.Equal(new Point(1920, 0), shiftedNewTarget.Position);
    }

    [Fact]
    public void PromoteToOriginIfNeeded_SurvivorAndNewTargetBothClaimOrigin_NewTargetsStaleClaimDropped()
    {
        // Real-rig repro (2026-08-31): the new target was solo-active the last time its mode was
        // cached (trivially at (0,0) with nothing else on screen), then gets re-added alongside a
        // survivor that is CURRENTLY, genuinely primary at (0,0). Before the fix, both entries kept
        // their origin claim and Windows rejected the plan outright (PathChangeException). The
        // survivor's claim must win; the new target's stale cached mode must be dropped to blank
        // (IsModeInformationAvailable=false), not merely repositioned elsewhere.
        var keptActiveSurvivors = new[] { WithMode(1, 0, 0) };
        var newPaths = new[] { WithMode(2, 0, 0) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        Assert.Equal(2, result.Count);
        PathInfo survivor = result.Single(p => p.DisplaySource.Equals(Source(1)));
        PathInfo newTarget = result.Single(p => p.DisplaySource.Equals(Source(2)));
        Assert.Equal(new Point(0, 0), survivor.Position);
        Assert.True(survivor.IsModeInformationAvailable);
        Assert.False(newTarget.IsModeInformationAvailable);
    }

    [Fact]
    public void PromoteToOriginIfNeeded_NoEntryHasCachedMode_LeftUntouched()
    {
        var keptActiveSurvivors = Array.Empty<PathInfo>();
        var newPaths = new[] { NoMode(1) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        PathInfo sole = Assert.Single(result);
        Assert.False(sole.IsModeInformationAvailable);
    }

    [Fact]
    public void PromoteToOriginIfNeeded_ModelessNewTargetAlongsideRepositionedEntries_ModelessLeftUntouched()
    {
        var keptActiveSurvivors = new[] { WithMode(1, 1920, 0) };
        var newPaths = new[] { NoMode(2) };

        IReadOnlyList<PathInfo> result = WindowsMonitorController.PromoteToOriginIfNeeded(keptActiveSurvivors, newPaths);

        Assert.Equal(2, result.Count);
        PathInfo promotedSurvivor = result.Single(p => p.DisplaySource.Equals(Source(1)));
        PathInfo modeless = result.Single(p => p.DisplaySource.Equals(Source(2)));
        Assert.Equal(new Point(0, 0), promotedSurvivor.Position);
        Assert.False(modeless.IsModeInformationAvailable);
    }

    // Debug session monitor-position-regre, round 11 (reopened deep investigation into the
    // resolved session's still-open Resolution.root_cause (8)): DescribeSource is the new
    // diagnostic-only PathDisplaySource identifier logged by TryBuildScopedActivationPlan so a
    // rig log can show which GPU adapter/source pairing gets chosen for a target across
    // successive activation attempts. Pure formatting, hardware-independent construction (same
    // Source(uint) fixture helper used by the PromoteToOriginIfNeeded tests above) -- unlike
    // DescribeScopedPathEntry (not unit-tested this round, see its own remarks: its per-target
    // DevicePath read requires a live CCD query).
    [Fact]
    public void DescribeSource_IncludesAdapterAndSourceIdentity()
    {
        string description = WindowsMonitorController.DescribeSource(Source(3));

        Assert.Contains("adapter=", description);
        Assert.Contains("sourceId=3", description);
    }

    [Fact]
    public void DescribeSource_DifferentSourceIds_ProduceDistinguishableDescriptions()
    {
        string first = WindowsMonitorController.DescribeSource(Source(1));
        string second = WindowsMonitorController.DescribeSource(Source(2));

        Assert.NotEqual(first, second);
    }

    // Debug session monitor-position-regre, round 14 (fix B): SelectSourceForActivation is the
    // pure "prefer reclaiming a target's own previously-cached PathDisplaySource" decision
    // extracted from TryBuildScopedActivationPlan's per-target candidate selection -- closes
    // round 8's own long-documented, never-fixed source-claim-greediness blind spot. Same
    // hardware-independent Source(uint) fixture helper used by the PromoteToOriginIfNeeded and
    // DescribeSource tests above -- no live CCD hardware needed.
    [Fact]
    public void SelectSourceForActivation_CachedSourcePresentAndUnclaimed_PrefersReclaimingIt()
    {
        // Mirrors round 13's evidence entry 2 exactly: the target's own previously-cached
        // source (sourceId=2) is present among the unclaimed candidates alongside another
        // (sourceId=1) that a purely greedy "first unclaimed" pick would have chosen instead.
        var unclaimedCandidates = new[] { Source(1), Source(2) };

        PathDisplaySource? selected = WindowsMonitorController.SelectSourceForActivation(unclaimedCandidates, Source(2));

        Assert.Equal(Source(2), selected);
    }

    [Fact]
    public void SelectSourceForActivation_NoCacheEntry_FallsBackToFirstUnclaimed_GreedyBehaviorUnchanged()
    {
        var unclaimedCandidates = new[] { Source(1), Source(2) };

        PathDisplaySource? selected = WindowsMonitorController.SelectSourceForActivation(unclaimedCandidates, previouslyCachedSource: null);

        Assert.Equal(Source(1), selected);
    }

    [Fact]
    public void SelectSourceForActivation_CachedSourceNoLongerUnclaimed_FallsBackToFirstUnclaimed()
    {
        // The cached source (sourceId=2) is not present in this call's own unclaimed candidate
        // list (e.g. already claimed by an active survivor or another target this same batch) --
        // must fall back to the greedy first-unclaimed pick exactly as before round 14, never
        // fail or throw.
        var unclaimedCandidates = new[] { Source(1), Source(3) };

        PathDisplaySource? selected = WindowsMonitorController.SelectSourceForActivation(unclaimedCandidates, Source(2));

        Assert.Equal(Source(1), selected);
    }

    [Fact]
    public void SelectSourceForActivation_NoUnclaimedCandidatesAtAll_ReturnsNull()
    {
        // The existing "no unclaimed PathDisplaySource found" failure case -- unchanged by
        // round 14, whether or not a cache entry exists.
        PathDisplaySource? selected = WindowsMonitorController.SelectSourceForActivation(
            Array.Empty<PathDisplaySource>(), Source(2));

        Assert.Null(selected);
    }

    [Fact]
    public void SelectSourceForActivation_CachedSourceIsSoleUnclaimedCandidate_Reclaimed()
    {
        var unclaimedCandidates = new[] { Source(5) };

        PathDisplaySource? selected = WindowsMonitorController.SelectSourceForActivation(unclaimedCandidates, Source(5));

        Assert.Equal(Source(5), selected);
    }

    // Debug session monitor-position-regre, round 14 (fix A): ShouldRetryScopedActivation is the
    // pure "should ActivateMonitors' bounded automatic retry fire" decision extracted from its
    // own retry loop -- deliberately narrow, per round 13's own evidence: only true when the
    // scoped ApplyPathInfos call reported success (no exception), at least one of the CALL'S OWN
    // requested targets is still inactive after the full settle-poll+correction budget, and the
    // retry budget is not yet exhausted.
    [Fact]
    public void ShouldRetryScopedActivation_ScopedSucceededButRequestedTargetStillInactive_BudgetRemains_ReturnsTrue()
    {
        // Mirrors round 10/13's rig-confirmed D-05 shape exactly: scoped ApplyPathInfos reported
        // success, but the requested target never settled active.
        bool result = WindowsMonitorController.ShouldRetryScopedActivation(
            usedScopedActivation: true, requestedStillInactiveCount: 1, attemptNumber: 1, maxRetryAttempts: 2);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryScopedActivation_ExtendFallbackWasUsedInstead_NeverRetried()
    {
        // A scoped-plan PathChangeException + Extend-fallback failure (root_cause (8)'s FIRST
        // observed shape) is a DIFFERENT failure shape than round 13 proved recoverable -- fix A
        // must never retry it, even if a requested target is still inactive and budget remains.
        bool result = WindowsMonitorController.ShouldRetryScopedActivation(
            usedScopedActivation: false, requestedStillInactiveCount: 1, attemptNumber: 1, maxRetryAttempts: 2);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryScopedActivation_OnlyAnUnrelatedSurvivorStillInactive_NeverRetried()
    {
        // Fix H's lost-survivor correction handles this DIFFERENT case -- fix A's retry is scoped
        // strictly to the CALL'S OWN requested target(s), never to a leftover survivor.
        bool result = WindowsMonitorController.ShouldRetryScopedActivation(
            usedScopedActivation: true, requestedStillInactiveCount: 0, attemptNumber: 1, maxRetryAttempts: 2);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryScopedActivation_RetryBudgetExhausted_ReturnsFalse_NeverUnbounded()
    {
        bool result = WindowsMonitorController.ShouldRetryScopedActivation(
            usedScopedActivation: true, requestedStillInactiveCount: 1, attemptNumber: 3, maxRetryAttempts: 2);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryScopedActivation_LastAllowedAttemptNumber_StillReturnsTrue()
    {
        // attemptNumber == maxRetryAttempts is still within budget (the NEXT attempt would be
        // attemptNumber + 1, i.e. the (maxRetryAttempts + 1)th total attempt -- the final one).
        bool result = WindowsMonitorController.ShouldRetryScopedActivation(
            usedScopedActivation: true, requestedStillInactiveCount: 1, attemptNumber: 2, maxRetryAttempts: 2);

        Assert.True(result);
    }

    // Debug session monitor-position-regre, round 20 (item A / Option A2 -- user-approved
    // checkpoint decision): ShouldRetryNestedCorrectionActivation is the pure "should THIS
    // nested fix-H correction call get the extended retry" decision, extracted the same way
    // ShouldRetryScopedActivation above was. Deliberately mirrors that method's (b)/(c)
    // conditions exactly -- only (a) differs (isNestedCorrectionCall instead of
    // usedScopedActivation) -- per round 19's own evidence and round 20's explicit design
    // constraint: extend retry-eligibility ONLY for fix H's own internal cleanup calls, never
    // for the top-level, directly-user-requested call.
    [Fact]
    public void ShouldRetryNestedCorrectionActivation_NestedCallExtendFallback_StillInactive_BudgetRemains_ReturnsTrue()
    {
        // Round 19's exact rig shape: the NESTED call's own scoped ApplyPathInfos ALSO threw
        // and fell back to Extend (usedScopedActivation would be false) -- but because this
        // is fix H's own cleanup call (isNestedCorrectionCall=true), it is now retried anyway.
        bool result = WindowsMonitorController.ShouldRetryNestedCorrectionActivation(
            isNestedCorrectionCall: true, requestedStillInactiveCount: 1, attemptNumber: 1, maxRetryAttempts: 2);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRetryNestedCorrectionActivation_TopLevelCall_NeverRetried_EvenWithExtendFallback()
    {
        // isNestedCorrectionCall=false (the top-level, directly-user-requested call) must
        // NEVER become eligible via this gate -- ShouldRetryScopedActivation's own,
        // already-approved exclusion for the top-level case remains the ONLY thing that
        // governs it.
        bool result = WindowsMonitorController.ShouldRetryNestedCorrectionActivation(
            isNestedCorrectionCall: false, requestedStillInactiveCount: 1, attemptNumber: 1, maxRetryAttempts: 2);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryNestedCorrectionActivation_NestedCall_NothingStillInactive_ReturnsFalse()
    {
        bool result = WindowsMonitorController.ShouldRetryNestedCorrectionActivation(
            isNestedCorrectionCall: true, requestedStillInactiveCount: 0, attemptNumber: 1, maxRetryAttempts: 2);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryNestedCorrectionActivation_NestedCall_RetryBudgetExhausted_ReturnsFalse_NeverUnbounded()
    {
        bool result = WindowsMonitorController.ShouldRetryNestedCorrectionActivation(
            isNestedCorrectionCall: true, requestedStillInactiveCount: 1, attemptNumber: 3, maxRetryAttempts: 2);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRetryNestedCorrectionActivation_NestedCall_LastAllowedAttemptNumber_StillReturnsTrue()
    {
        bool result = WindowsMonitorController.ShouldRetryNestedCorrectionActivation(
            isNestedCorrectionCall: true, requestedStillInactiveCount: 1, attemptNumber: 2, maxRetryAttempts: 2);

        Assert.True(result);
    }
}
