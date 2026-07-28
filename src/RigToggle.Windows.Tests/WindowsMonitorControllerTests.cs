using System.Drawing;
using System.Linq;
using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;
using WindowsDisplayAPI.Native.Structures;
using RigToggle.Core.Models;
using RigToggle.Windows;
using Xunit;

namespace RigToggle.Windows.Tests;

// Covers the two pieces of WindowsMonitorController.Restore()'s reconstruction logic
// that are unit-testable without live display hardware (04-03 SUMMARY.md WR-05/WR-06):
// the OutputTechnology reflection patch, and the source-assignment rule that fixed the
// source-collision bug (WR-02). Restore()/Disable() themselves are NOT unit-tested here
// — they call PathInfo.GetActivePaths()/GetAllPaths()/ApplyPathInfos() directly, which
// are static calls into real native CCD APIs with no injectable seam, so they remain
// verified only via live rig testing (see 04-01/04-03 SUMMARY.md).
public class WindowsMonitorControllerTests
{
    // PathDisplayTarget's public constructor calls the live GetDisplayTargets() query
    // internally (WindowsDisplayAPI source-confirmed) — this is expected to run fine on
    // any Windows test machine (it's a basic OS display-subsystem query, not dependent
    // on specific connected hardware), just returning IsAvailable=false for a
    // constructed fake target ID that matches nothing real. AssignSource's tests below
    // deliberately avoid needing this at all.
    private static PathTargetInfo CreateFakeTarget(uint targetId = 999)
    {
        var adapter = new PathDisplayAdapter(new LUID(1, 0));
        var displayTarget = new PathDisplayTarget(adapter, targetId);
        return new PathTargetInfo(displayTarget, frequencyInMillihertz: 60000UL);
    }

    [Fact]
    public void CopyOutputTechnology_DefaultsToOther_BeforePatch()
    {
        var target = CreateFakeTarget();

        Assert.Equal(DisplayConfigVideoOutputTechnology.Other, target.OutputTechnology);
    }

    [Fact]
    public void CopyOutputTechnology_PatchesBackingField_ToRequestedValue()
    {
        var target = CreateFakeTarget();

        WindowsMonitorController.CopyOutputTechnology(target, DisplayConfigVideoOutputTechnology.DisplayPortExternal);

        Assert.Equal(DisplayConfigVideoOutputTechnology.DisplayPortExternal, target.OutputTechnology);
    }

    private static PathDisplaySource FakeSource(uint sourceId, uint adapterLow = 1)
        => new(new PathDisplayAdapter(new LUID(adapterLow, 0)), sourceId);

    [Fact]
    public void AssignSource_ActiveTarget_KeepsItsOwnSource()
    {
        var ownSource = FakeSource(sourceId: 0);
        var usedSources = new HashSet<PathDisplaySource>();
        var allSources = new[] { FakeSource(0), FakeSource(1) };

        var result = WindowsMonitorController.AssignSource(
            isPathActive: true, ownSource, usedSources, allSources, "test");

        Assert.Equal(ownSource, result);
        // Active-path assignment does not consume from the free-source pool.
        Assert.Empty(usedSources);
    }

    [Fact]
    public void AssignSource_InactiveTarget_GetsFirstSourceNotAlreadyUsed()
    {
        var source0 = FakeSource(0);
        var source1 = FakeSource(1);
        var usedSources = new HashSet<PathDisplaySource> { source0 };
        var allSources = new[] { source0, source1 };

        var result = WindowsMonitorController.AssignSource(
            isPathActive: false, ownSource: source0, usedSources, allSources, "test");

        Assert.Equal(source1, result);
        Assert.Contains(source1, usedSources);
    }

    [Fact]
    public void AssignSource_TwoSequentialInactiveTargets_DoNotCollide()
    {
        // Regression test for WR-02 / the original live-rig source-collision bug: two
        // inactive targets restored in the same pass must never be assigned the same
        // source, even though neither has an "own" active source to defer to.
        var source0 = FakeSource(0);
        var source1 = FakeSource(1);
        var usedSources = new HashSet<PathDisplaySource>();
        var allSources = new[] { source0, source1 };

        var first = WindowsMonitorController.AssignSource(
            isPathActive: false, ownSource: source0, usedSources, allSources, "first");
        var second = WindowsMonitorController.AssignSource(
            isPathActive: false, ownSource: source0, usedSources, allSources, "second");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void AssignSource_NoFreeSourceAvailable_Throws()
    {
        var source0 = FakeSource(0);
        var usedSources = new HashSet<PathDisplaySource> { source0 };
        var allSources = new[] { source0 };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            WindowsMonitorController.AssignSource(
                isPathActive: false, ownSource: source0, usedSources, allSources, "no free source"));

        Assert.Contains("no free source", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Covers the pure axis-aligned bounding-box overlap helper added in Phase 6 Plan
    // 03 (06-RESEARCH.md "Bounding-box overlap check" code example), shared by both
    // DeactivateMonitors' and Restore's verify-and-throw sections. Same "pure logic
    // only, no live CCD hardware" constraint as the rest of this file — the helper
    // itself has no dependency on WindowsDisplayAPI beyond System.Drawing.Rectangle.
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
}
