namespace RigToggle.Core.Models;

/// <summary>
/// One toggle step's outcome — step name, result, and (if Failed) the reason. Reason is
/// null for Succeeded/NotAttempted; populated with the underlying exception's message for
/// Failed (same "surface the real error" posture as MainForm's existing exception-detail
/// MessageBox text, D-13/T-02-FAKEFAIL).
/// </summary>
public sealed record ToggleStepResult(string StepName, ToggleStepOutcome Outcome, string? Reason);
