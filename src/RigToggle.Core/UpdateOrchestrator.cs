using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// UI-free check -> compare -> confirm-callback -> apply sequencer (UPDATE-02/
/// UPDATE-03/UPDATE-04), mirroring ToggleOrchestrator's Core-sequences/App-executes
/// split: no WinForms type and no balloon/dialog call may appear anywhere in this
/// file. <c>confirm</c> and <c>onApplyStarting</c> are delegates the App layer
/// supplies so this class never references RigToggle.App directly, the same way
/// ToggleService never references a concrete IMonitorController/IAudioController
/// implementation.
///
/// Exception-handling split (deliberate -- matches PITFALLS.md's "an automatic
/// check must never surface anything" UX requirement against D-08's "a confirmed-
/// and-then-failed apply must be told to the user"): the fetch+compare segment
/// below is wrapped so ANY exception degrades to <see cref="UpdateCheckOutcome.NotAvailable"/>,
/// since this segment can run silently on every launch with nobody watching. The
/// post-confirm download+apply segment is deliberately NOT wrapped -- the user has
/// already explicitly clicked "Update Now," so a failure from here must propagate
/// to the App-layer caller, which is responsible for catching it and showing the
/// Warning-icon toast (D-08).
/// </summary>
public sealed class UpdateOrchestrator
{
    private readonly IReleaseFeed _releaseFeed;
    private readonly IUpdateApplier _updateApplier;
    private readonly Version _runningVersion;

    public UpdateOrchestrator(IReleaseFeed releaseFeed, IUpdateApplier updateApplier, Version runningVersion)
    {
        _releaseFeed = releaseFeed ?? throw new ArgumentNullException(nameof(releaseFeed));
        _updateApplier = updateApplier ?? throw new ArgumentNullException(nameof(updateApplier));
        _runningVersion = runningVersion ?? throw new ArgumentNullException(nameof(runningVersion));
    }

    /// <summary>
    /// Sequences: fetch the latest release, return <see cref="UpdateCheckOutcome.NotAvailable"/>
    /// on a null/not-newer result (or any exception from that segment -- see class
    /// doc comment), invoke <paramref name="confirm"/> and return
    /// <see cref="UpdateCheckOutcome.Declined"/> when it returns false, otherwise
    /// invoke <paramref name="onApplyStarting"/>, download+stage the asset, apply-
    /// and-relaunch, and return <see cref="UpdateCheckOutcome.Applying"/>.
    /// </summary>
    public async Task<UpdateCheckOutcome> CheckOnLaunchAsync(
        Func<ReleaseInfo, bool> confirm,
        Action<ReleaseInfo> onApplyStarting,
        bool wasStartedHidden,
        CancellationToken cancellationToken)
    {
        if (confirm is null)
        {
            throw new ArgumentNullException(nameof(confirm));
        }

        if (onApplyStarting is null)
        {
            throw new ArgumentNullException(nameof(onApplyStarting));
        }

        ReleaseInfo release;
        try
        {
            ReleaseInfo? fetched = await _releaseFeed.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (fetched is null || !UpdateVersionComparer.IsNewer(_runningVersion, fetched.TagName))
            {
                return UpdateCheckOutcome.NotAvailable;
            }

            release = fetched;
        }
        catch
        {
            // An automatic check must never surface anything (PITFALLS.md UX
            // Pitfalls table) -- degrade any fetch/compare-segment exception to
            // NotAvailable, the same outcome as "no update exists."
            return UpdateCheckOutcome.NotAvailable;
        }

        if (!confirm(release))
        {
            return UpdateCheckOutcome.Declined;
        }

        onApplyStarting(release);

        // Deliberately UNWRAPPED -- see class doc comment. The user has already
        // confirmed; a download/apply failure here must propagate to the caller so
        // the App layer can toast it (D-08), not be silently swallowed.
        string stagedPath = await _updateApplier.DownloadAndStageAsync(release, cancellationToken).ConfigureAwait(false);
        _updateApplier.ApplyAndRelaunch(stagedPath, wasStartedHidden);

        return UpdateCheckOutcome.Applying;
    }
}

/// <summary>
/// UpdateOrchestrator.CheckOnLaunchAsync's possible outcomes. <see cref="Failed"/> is
/// reserved for a future/manual-check caller that wants to represent "the check
/// itself failed" as a return value rather than a propagated exception (D-07,
/// manual check must show a distinct failure toast) -- CheckOnLaunchAsync's own
/// fetch/compare segment currently degrades every failure to
/// <see cref="NotAvailable"/> (see class doc comment), so this member is not
/// produced by that method today, but is declared now so callers can match
/// exhaustively without a future breaking enum change.
/// </summary>
public enum UpdateCheckOutcome
{
    NotAvailable,
    Declined,
    Applying,
    Failed,
}
