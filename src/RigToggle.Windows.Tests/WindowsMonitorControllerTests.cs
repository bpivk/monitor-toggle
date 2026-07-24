using WindowsDisplayAPI.DisplayConfig;
using WindowsDisplayAPI.Native.DisplayConfig;
using WindowsDisplayAPI.Native.Structures;
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
}
