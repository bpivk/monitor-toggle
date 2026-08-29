using System;

namespace RigToggle.Core;

/// <summary>
/// Quick-260829-ga9/UPDATE-06: single source of truth for manual-check outcome
/// copy, mirroring ToggleResultFormatter's shape (a small static Core formatter,
/// no WinForms/UpdateOrchestrator-internal dependency, unit-testable in isolation).
/// After this plan, AboutForm's inline status label AND MainForm's tray-balloon
/// wording both call <see cref="FormatStatus"/>, so the two channels cannot drift.
///
/// Declined and Skipped return <see cref="string.Empty"/> per D-05's existing rule
/// that re-announcing a choice the user just made in a dialog is noise.
/// NotAvailable (D-06) and CheckFailed (D-07) return distinct, non-empty sentences
/// so a failed manual check is never mistakable for a successful one. CheckFailed
/// deliberately does NOT name the tray menu or Settings as a retry path (unlike the
/// balloon wording this method replaces) -- Plan 260829-ga9 Task 2 removes both
/// surfaces, so that sentence would be a lie once this formatter is wired in.
/// </summary>
public static class UpdateCheckMessageFormatter
{
    /// <summary>
    /// Shown in AboutForm's status label the instant the button is clicked, before
    /// the awaited check has produced any <see cref="UpdateCheckResult"/> to format.
    /// </summary>
    public const string CheckingMessage = "Checking for updates…";

    /// <summary>
    /// Shown when <c>PerformManualUpdateCheckAsync</c> returns <see langword="null"/>
    /// -- the CR-01 reentrancy guard rejected this call because a check (automatic
    /// or manual) is already in flight.
    /// </summary>
    public const string AlreadyRunningMessage = "A check is already running.";

    /// <summary>
    /// Formats a single fetch/compare/confirm/apply outcome into the sentence shown
    /// to the user, or <see cref="string.Empty"/> for the two outcomes that need no
    /// announcement.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public static string FormatStatus(UpdateCheckResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        return result.Outcome switch
        {
            UpdateCheckOutcome.NotAvailable => FormatNotAvailable(result.RunningVersionText),
            UpdateCheckOutcome.CheckFailed => FormatCheckFailed(result.FailureReason),
            UpdateCheckOutcome.Applying => "Downloading and installing update…",
            UpdateCheckOutcome.Declined => string.Empty,
            UpdateCheckOutcome.Skipped => string.Empty,
            _ => string.Empty,
        };
    }

    private static string FormatNotAvailable(string? runningVersionText)
    {
        // Guard rather than render a blank/dangling parenthetical when the running
        // version couldn't be resolved -- drop the version clause entirely instead.
        return string.IsNullOrEmpty(runningVersionText)
            ? "You're already on the latest version."
            : $"You're already on the latest version (v{runningVersionText}).";
    }

    private static string FormatCheckFailed(string? failureReason)
    {
        // Guard rather than render a dangling em-dash when no reason is available
        // (e.g. a synthesized failure with no exception message).
        return string.IsNullOrEmpty(failureReason)
            ? "Couldn't check for updates."
            : $"Couldn't check for updates — {failureReason}.";
    }
}
