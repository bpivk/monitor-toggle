using System;
using System.Collections.Generic;
using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Debug session monitor-position-regre, round 20 (item B / Option B1 -- user-approved
/// checkpoint decision): proves MonitorEnableFailureMessageBuilder.Build's two branches --
/// a CollateralMonitorRestoreFailedException gets a clarified message referencing BOTH the
/// originally-requested (succeeded) device path and the collaterally-affected device path(s)
/// by name, while a plain InvalidOperationException (the pre-existing, TOP-LEVEL failure
/// shape) is passed through byte-for-byte unchanged. Pure logic, RigToggle.Core-only
/// dependency -- no live CCD hardware or WinForms needed, matching this file's own
/// established discipline (ShouldRetryScopedActivation/ShouldRetryNestedCorrectionActivation
/// are tested the same way in RigToggle.Windows.Tests).
/// </summary>
public class MonitorEnableFailureMessageBuilderTests
{
    [Fact]
    public void Build_CollateralFailure_NamesBothTheSucceededRequestAndTheAffectedMonitor()
    {
        var ex = new CollateralMonitorRestoreFailedException(
            "Monitor enable did not take effect: SAM7489. No further automatic recovery is attempted (D-05).",
            new List<string> { "SAM7489" });

        string message = MonitorEnableFailureMessageBuilder.Build("ACI24A4", ex);

        Assert.Contains("ACI24A4", message);
        Assert.Contains("enabled successfully", message);
        Assert.Contains("SAM7489", message);
        Assert.Contains("side effect", message);
        // The original, technical D-05 detail is still present -- clarified, not replaced.
        Assert.Contains(ex.Message, message);
    }

    [Fact]
    public void Build_CollateralFailure_MultipleAffectedDevicePaths_NamesAllOfThem()
    {
        var ex = new CollateralMonitorRestoreFailedException(
            "Monitor enable did not take effect: SAM7489, DELA0BC. No further automatic recovery is attempted (D-05).",
            new List<string> { "SAM7489", "DELA0BC" });

        string message = MonitorEnableFailureMessageBuilder.Build("ACI24A4", ex);

        Assert.Contains("SAM7489", message);
        Assert.Contains("DELA0BC", message);
    }

    [Fact]
    public void Build_TopLevelFailure_PlainInvalidOperationException_MessageUnchanged()
    {
        // Round 20's own design constraint: the TOP-LEVEL failure shape (a plain
        // InvalidOperationException, exactly what ActivateMonitors already threw before this
        // round for the user's own direct request) must see NO change in dialog text --
        // confirmed here byte-for-byte.
        var ex = new InvalidOperationException(
            "Monitor enable did not take effect: ACI24A4. No further automatic recovery is attempted (D-05).");

        string message = MonitorEnableFailureMessageBuilder.Build("ACI24A4", ex);

        Assert.Equal(ex.Message, message);
    }

    [Fact]
    public void Build_TopLevelFailure_NotDetectedGuard_MessageUnchanged()
    {
        // The early "not detected" availability guard (a DIFFERENT throw site, never
        // touched by round 20) also remains a plain InvalidOperationException either way.
        var ex = new InvalidOperationException("Cannot enable monitor(s) — not detected: SAM748A");

        string message = MonitorEnableFailureMessageBuilder.Build("SAM748A", ex);

        Assert.Equal(ex.Message, message);
    }

    [Fact]
    public void CollateralMonitorRestoreFailedException_IsCaughtByExistingInvalidOperationExceptionClauses()
    {
        // Confirms, directly, the claim every round-20 remark relies on: MainForm.cs's
        // existing `catch (InvalidOperationException ex)` clauses (lines 1293/1350) require
        // zero changes to keep catching this new exception type -- a derived-type instance
        // is always caught by a base-type catch clause, so proving the inheritance
        // relationship directly is equivalent to (and simpler than) exercising an actual
        // throw/catch round-trip here.
        var ex = new CollateralMonitorRestoreFailedException("message", new List<string> { "SAM7489" });

        Assert.IsAssignableFrom<InvalidOperationException>(ex);
    }

    [Fact]
    public void CollateralMonitorRestoreFailedException_ExposesAffectedDevicePathsSeparatelyFromMessage()
    {
        var ex = new CollateralMonitorRestoreFailedException("message", new List<string> { "SAM7489", "DELA0BC" });

        Assert.Equal(new[] { "SAM7489", "DELA0BC" }, ex.AffectedDevicePaths);
        Assert.Equal("message", ex.Message);
    }
}
