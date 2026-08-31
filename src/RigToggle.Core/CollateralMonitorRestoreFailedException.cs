namespace RigToggle.Core;

/// <summary>
/// Debug session monitor-position-regre, round 20 (item B / Option B1 -- user-approved
/// checkpoint decision to round 19's item B, sharing round 20's item A isNestedCorrectionCall
/// flag as its one distinguishing mechanism per this round's own design constraint): thrown by
/// WindowsMonitorController.ActivateMonitors' D-05 verify-and-throw instead of a plain
/// InvalidOperationException when the failing attempt is fix H's own NESTED correction call
/// (restoring a survivor collaterally dropped as a side effect of the caller's own request)
/// rather than the top-level, directly-user-requested call. Subclasses InvalidOperationException
/// -- mirroring ToggleInProgressException's own precedent in this same file/namespace -- so
/// every existing `catch (InvalidOperationException ex)` clause (confirmed by direct read:
/// MainForm.cs lines 1293 and 1350; ToggleService.TryExecuteStep's own `catch (Exception ex)` is
/// even broader) continues to catch it with ZERO behavior change; only code that specifically
/// checks for this concrete type (MonitorEnableFailureMessageBuilder, this round) sees anything
/// different. AffectedDevicePaths carries the collaterally-affected device path(s) this nested
/// call itself could not restore -- separately from Message, so a catcher can build a clarified
/// dialog without parsing the message string.
/// </summary>
public sealed class CollateralMonitorRestoreFailedException : InvalidOperationException
{
    public IReadOnlyList<string> AffectedDevicePaths { get; }

    public CollateralMonitorRestoreFailedException(string message, IReadOnlyList<string> affectedDevicePaths)
        : base(message)
    {
        AffectedDevicePaths = affectedDevicePaths;
    }
}
