namespace RigToggle.Core.Models;

/// <summary>
/// Full outcome of a ToggleToRigMode/ToggleToNormalMode call — ordered per-step results,
/// consumed identically by MainForm regardless of toggle direction (D-03). Scoped strictly
/// to the 3 mutation steps (Monitor/Audio/App); preflight guards (unconfigured settings,
/// missing companion app path) remain exception-based and are NOT represented here.
/// </summary>
public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    // Phase 15/D-03: a Skipped step (deliberately unconfigured target) is not a failure —
    // only Failed/NotAttempted should flip Success to false. Do not revert this to a
    // strict `== Succeeded` check; that would make every toggle with any optional target
    // left unset report as "did not fully complete" via MainForm's warning path.
    public bool Success => Steps.All(s => s.Outcome is ToggleStepOutcome.Succeeded or ToggleStepOutcome.Skipped);

    /// <summary>
    /// Debug session monitor-position-regre, round 18 (item 1a): device paths from
    /// settings.json's MonitorsToDisable/MonitorsToEnable (or the NormalMonitorsToDisable/
    /// NormalMonitorsToEnable equivalents) that were NOT live-detected via
    /// IMonitorController.GetAllMonitors() at toggle time, and were therefore filtered out
    /// of this call's ActivateMonitors/DeactivateMonitors sets (ToggleService.
    /// LiveFilterMonitorSets) instead of throwing an opaque "not detected" failure that
    /// blocked the ENTIRE toggle over one stale entry (round 17 evidence: SAM748A/DELA0B8).
    /// A dedicated init-only property (not a primary-constructor parameter) so every
    /// existing `new ToggleResult(steps)` call site remains byte-for-byte valid — this is
    /// additive, not a breaking change to the record's shape. Empty (never null) when
    /// nothing was filtered, which is the overwhelmingly common case.
    /// </summary>
    public IReadOnlyList<string> StaleMonitorsSkipped { get; init; } = Array.Empty<string>();
}
