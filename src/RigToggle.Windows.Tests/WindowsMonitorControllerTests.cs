using System.Drawing;
using System.Linq;
using RigToggle.Core.Models;
using RigToggle.Windows;
using Xunit;

namespace RigToggle.Windows.Tests;

// Covers the pure, unit-testable helpers exposed off WindowsMonitorController via
// InternalsVisibleTo: the AnyRectanglesOverlap bounding-box geometry check and the
// MergeAllMonitors dedup/promotion logic, both reachable without live display
// hardware. The mutating CCD methods (ActivateMonitors/DeactivateMonitors) are NOT
// unit-tested here — they call PathInfo.GetActivePaths()/GetAllPaths()/
// ApplyPathInfos() directly, which are static calls into real native CCD APIs with
// no injectable seam, so they remain verified only via live rig testing (see
// 04-01/04-03 SUMMARY.md).
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
}
