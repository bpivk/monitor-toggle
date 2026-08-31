namespace RigToggle.Core;

/// <summary>
/// Debug session monitor-position-regre, round 20 (item B / Option B1 -- user-approved
/// checkpoint decision): pure seam (unit-tested, RigToggle.Tests -- no live CCD hardware or
/// WinForms needed) for the "what should the ENABLE-branch failure dialog say" decision,
/// extracted so MainForm.OnTileAction's catch block (lines ~1350-1354) stays a thin caller.
/// Round 19's own evidence: a plain InvalidOperationException from ActivateMonitors' D-05
/// verify-and-throw is structurally indistinguishable, to the user, between "your own click
/// failed" and "fix H's internal cleanup, restoring an unrelated monitor collaterally dropped
/// as a side effect of your click, failed" -- the SAME ex.Message text and exception TYPE either
/// way, with the user's own already-succeeded target never referenced. This builder resolves
/// that ambiguity using the SAME isNestedCorrectionCall-derived signal item A's retry-eligibility
/// extension already threads through WindowsMonitorController
/// (CollateralMonitorRestoreFailedException is thrown ONLY when isNestedCorrectionCall was true
/// at the point of the D-05 throw) -- rather than inventing a second, independent way to detect
/// "this is a nested cleanup call" (e.g. string-parsing ex.Message, or re-querying live monitor
/// state). When the nested-only retry extension (round 20 item A) succeeds -- expected to be the
/// common case going forward -- no exception is thrown at all, and this builder is never called;
/// it only ever fires in the remaining case where even the extended nested retry is exhausted.
/// </summary>
public static class MonitorEnableFailureMessageBuilder
{
    public static string Build(string requestedDevicePath, InvalidOperationException ex)
    {
        if (ex is CollateralMonitorRestoreFailedException collateralEx)
        {
            return $"{requestedDevicePath} was enabled successfully, but restoring " +
                   $"{string.Join(", ", collateralEx.AffectedDevicePaths)} (affected as a side " +
                   $"effect) failed. {ex.Message}";
        }

        return ex.Message;
    }
}
