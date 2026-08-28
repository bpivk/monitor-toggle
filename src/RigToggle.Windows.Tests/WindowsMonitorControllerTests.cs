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
}
