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
///
/// Plan 26-04/D-02: <paramref name="confirm"/> now returns a three-way
/// <see cref="UpdatePromptChoice"/> instead of a bool, and an
/// <see cref="ISettingsStore"/> is threaded through so a "Skip this version" choice
/// can be persisted (and a persisted skip can be honoured on the automatic path)
/// without either concern living in the App layer.
/// </summary>
public sealed class UpdateOrchestrator
{
    private readonly IReleaseFeed _releaseFeed;
    private readonly IUpdateApplier _updateApplier;
    private readonly ISettingsStore _settingsStore;
    private readonly Version _runningVersion;

    public UpdateOrchestrator(IReleaseFeed releaseFeed, IUpdateApplier updateApplier, ISettingsStore settingsStore, Version runningVersion)
    {
        _releaseFeed = releaseFeed ?? throw new ArgumentNullException(nameof(releaseFeed));
        _updateApplier = updateApplier ?? throw new ArgumentNullException(nameof(updateApplier));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _runningVersion = runningVersion ?? throw new ArgumentNullException(nameof(runningVersion));
    }

    /// <summary>
    /// Sequences: fetch the latest release, return <see cref="UpdateCheckOutcome.NotAvailable"/>
    /// on a null/not-newer result (or any exception from that segment -- see class
    /// doc comment). When <paramref name="honourSkippedVersion"/> is true (the
    /// automatic on-launch path) and the release is not strictly newer than the
    /// persisted <see cref="AppSettings.SkippedUpdateVersion"/>, returns
    /// <see cref="UpdateCheckOutcome.Skipped"/> WITHOUT invoking <paramref name="confirm"/>
    /// at all -- a skip suppresses exactly one version, never every future one
    /// (T-26-14). Otherwise invokes <paramref name="confirm"/>: <see
    /// cref="UpdatePromptChoice.Later"/> returns <see cref="UpdateCheckOutcome.Declined"/>
    /// with nothing persisted; <see cref="UpdatePromptChoice.Skip"/> best-effort
    /// persists the release tag to <see cref="AppSettings.SkippedUpdateVersion"/> and
    /// returns <see cref="UpdateCheckOutcome.Skipped"/>; <see
    /// cref="UpdatePromptChoice.UpdateNow"/> invokes <paramref name="onApplyStarting"/>,
    /// downloads+stages the asset, applies-and-relaunches, and returns
    /// <see cref="UpdateCheckOutcome.Applying"/>.
    /// </summary>
    public async Task<UpdateCheckOutcome> CheckOnLaunchAsync(
        Func<ReleaseInfo, UpdatePromptChoice> confirm,
        Action<ReleaseInfo> onApplyStarting,
        bool wasStartedHidden,
        bool honourSkippedVersion,
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

        if (honourSkippedVersion)
        {
            // Prohibition (T-26-14): "Skip this version" must never become "never
            // check for updates again." Comparing the persisted skipped tag against
            // this release's tag via UpdateVersionComparer (numeric Major.Minor)
            // rather than a string match is what lets a strictly-newer release still
            // prompt after an earlier skip -- a string-equality check would suppress
            // only the exact skipped tag by coincidence, with no principled way to
            // let a genuinely newer release through except reimplementing this same
            // numeric comparison. Best-effort read: an unreadable settings file must
            // not block an otherwise-valid prompt, so it degrades to "nothing skipped."
            string? skippedVersion = null;
            try
            {
                skippedVersion = _settingsStore.Load().SkippedUpdateVersion;
            }
            catch
            {
                // Treat an unreadable settings file as "nothing skipped" -- see comment above.
            }

            if (!string.IsNullOrEmpty(skippedVersion)
                && UpdateVersionComparer.TryParseTag(skippedVersion, out int skippedMajor, out int skippedMinor)
                && !UpdateVersionComparer.IsNewer(new Version(skippedMajor, skippedMinor), release.TagName))
            {
                // release.TagName is NOT strictly newer than the skipped tag --
                // suppress without ever invoking confirm.
                return UpdateCheckOutcome.Skipped;
            }
        }

        UpdatePromptChoice choice = confirm(release);

        if (choice == UpdatePromptChoice.Later)
        {
            return UpdateCheckOutcome.Declined;
        }

        if (choice == UpdatePromptChoice.Skip)
        {
            try
            {
                AppSettings settings = _settingsStore.Load();
                settings.SkippedUpdateVersion = release.TagName;
                _settingsStore.Save(settings);
            }
            catch
            {
                // Best-effort: a settings-write failure here must downgrade to "we
                // will ask again next launch," never turn a declined/skipped update
                // into a crash.
            }

            return UpdateCheckOutcome.Skipped;
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
    Skipped,
}

/// <summary>
/// D-02: the three choices UpdatePromptDialog can resolve to. UpdateNow triggers
/// download+apply; Later persists nothing and simply re-prompts next launch; Skip
/// persists the release tag to AppSettings.SkippedUpdateVersion so that specific
/// version stops prompting until a strictly newer one ships (T-26-14).
/// </summary>
public enum UpdatePromptChoice
{
    UpdateNow,
    Later,
    Skip,
}
