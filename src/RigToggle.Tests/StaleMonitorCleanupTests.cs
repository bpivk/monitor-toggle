using RigToggle.Core;
using RigToggle.Core.Models;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves StaleMonitorCleanup.Reconcile's three-way behavior: a device path currently
/// detected clears its tracking entry, one newly missing starts being tracked without
/// being removed, and one missing for at least the given expiry is dropped from every
/// monitor set and from tracking.
/// </summary>
public class StaleMonitorCleanupTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reconcile_DevicePathCurrentlyDetected_ClearsExistingTrackingEntry()
    {
        var settings = new AppSettings
        {
            MonitorsToEnable = new List<string> { "A" },
            StaleMonitorFirstMissingUtc = new Dictionary<string, DateTime> { ["A"] = Now.AddDays(-5) },
        };

        bool changed = StaleMonitorCleanup.Reconcile(settings, new HashSet<string> { "A" }, Now, StaleMonitorCleanup.DefaultExpiry);

        Assert.True(changed);
        Assert.Null(settings.StaleMonitorFirstMissingUtc);
        Assert.Equal(new List<string> { "A" }, settings.MonitorsToEnable);
    }

    [Fact]
    public void Reconcile_DevicePathNewlyMissing_StartsTrackingWithoutRemoving()
    {
        var settings = new AppSettings
        {
            MonitorsToEnable = new List<string> { "A" },
        };

        bool changed = StaleMonitorCleanup.Reconcile(settings, new HashSet<string>(), Now, StaleMonitorCleanup.DefaultExpiry);

        Assert.True(changed);
        Assert.Equal(new List<string> { "A" }, settings.MonitorsToEnable);
        Assert.NotNull(settings.StaleMonitorFirstMissingUtc);
        Assert.Equal(Now, settings.StaleMonitorFirstMissingUtc!["A"]);
    }

    [Fact]
    public void Reconcile_DevicePathMissingWithinExpiryWindow_StaysUntouched()
    {
        var settings = new AppSettings
        {
            MonitorsToEnable = new List<string> { "A" },
            StaleMonitorFirstMissingUtc = new Dictionary<string, DateTime> { ["A"] = Now.AddDays(-29) },
        };

        bool changed = StaleMonitorCleanup.Reconcile(settings, new HashSet<string>(), Now, StaleMonitorCleanup.DefaultExpiry);

        Assert.False(changed);
        Assert.Equal(new List<string> { "A" }, settings.MonitorsToEnable);
        Assert.Equal(Now.AddDays(-29), settings.StaleMonitorFirstMissingUtc!["A"]);
    }

    [Fact]
    public void Reconcile_DevicePathMissingPastExpiry_DroppedFromAllFourSetsAndTracking()
    {
        var settings = new AppSettings
        {
            MonitorsToDisable = new List<string> { "A", "B" },
            MonitorsToEnable = new List<string> { "A" },
            NormalMonitorsToDisable = new List<string> { "A" },
            NormalMonitorsToEnable = new List<string> { "A", "C" },
            StaleMonitorFirstMissingUtc = new Dictionary<string, DateTime> { ["A"] = Now.AddDays(-30) },
        };

        bool changed = StaleMonitorCleanup.Reconcile(settings, new HashSet<string> { "C" }, Now, StaleMonitorCleanup.DefaultExpiry);

        Assert.True(changed);
        // "B" is referenced (MonitorsToDisable) and currently undetected too, but has no
        // prior tracking entry -- it starts being tracked from "now", it is NOT expired
        // yet, and so it is correctly NOT dropped in this same call.
        Assert.Equal(new List<string> { "B" }, settings.MonitorsToDisable);
        Assert.Empty(settings.MonitorsToEnable!);
        Assert.Empty(settings.NormalMonitorsToDisable!);
        Assert.Equal(new List<string> { "C" }, settings.NormalMonitorsToEnable);
        Assert.NotNull(settings.StaleMonitorFirstMissingUtc);
        Assert.False(settings.StaleMonitorFirstMissingUtc!.ContainsKey("A"));
        Assert.Equal(Now, settings.StaleMonitorFirstMissingUtc!["B"]);
    }

    [Fact]
    public void Reconcile_NothingReferencedOrMissing_NoOp()
    {
        var settings = new AppSettings();

        bool changed = StaleMonitorCleanup.Reconcile(settings, new HashSet<string>(), Now, StaleMonitorCleanup.DefaultExpiry);

        Assert.False(changed);
        Assert.Null(settings.StaleMonitorFirstMissingUtc);
    }

    [Fact]
    public void Reconcile_TrackedPathNoLongerReferencedByAnySet_TrackingDropped()
    {
        // e.g. the user hand-edited settings.json to remove a device path directly.
        var settings = new AppSettings
        {
            StaleMonitorFirstMissingUtc = new Dictionary<string, DateTime> { ["A"] = Now.AddDays(-1) },
        };

        bool changed = StaleMonitorCleanup.Reconcile(settings, new HashSet<string>(), Now, StaleMonitorCleanup.DefaultExpiry);

        Assert.True(changed);
        Assert.Null(settings.StaleMonitorFirstMissingUtc);
    }
}
