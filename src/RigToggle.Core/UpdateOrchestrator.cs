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
///
/// Plan 26-05/UPDATE-06/D-05/D-06/D-07: <see cref="CheckOnDemandAsync"/> shares this
/// class's fetch/compare/confirm/apply sequencer with <see cref="CheckOnLaunchAsync"/>
/// via the private <see cref="CheckAsync"/> body, parameterised by
/// <c>honourSkippedVersion</c> (only the automatic path suppresses a previously
/// skipped release; an explicit on-demand request always prompts, T-26-14 read
/// together with D-02) and <c>reportFailures</c> (only the on-demand path converts a
/// caught fetch/compare exception into the distinct
/// <see cref="UpdateCheckOutcome.CheckFailed"/> outcome instead of collapsing it into
/// <see cref="UpdateCheckOutcome.NotAvailable"/> -- D-07's whole premise is that a
/// failed manual check must never look identical to "you're already up to date").
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
        UpdateCheckResult result = await CheckAsync(
            confirm,
            onApplyStarting,
            wasStartedHidden,
            honourSkippedVersion,
            reportFailures: false,
            cancellationToken).ConfigureAwait(false);

        return result.Outcome;
    }

    /// <summary>
    /// UPDATE-06/D-05: the on-demand entry point reached from the tray menu's "Check
    /// for Updates" item and Settings' "Check for Updates" button. Always passes
    /// <c>honourSkippedVersion: false</c> -- an explicit request overrides a prior
    /// skip, since D-02's suppression is about unprompted nagging, not about
    /// refusing a direct question -- and <c>reportFailures: true</c>, so a caught
    /// fetch/compare exception surfaces as the distinct
    /// <see cref="UpdateCheckOutcome.CheckFailed"/> outcome (carrying the exception's
    /// message as <see cref="UpdateCheckResult.FailureReason"/>) instead of the
    /// automatic path's silent <see cref="UpdateCheckOutcome.NotAvailable"/> (D-07).
    /// </summary>
    public Task<UpdateCheckResult> CheckOnDemandAsync(
        Func<ReleaseInfo, UpdatePromptChoice> confirm,
        Action<ReleaseInfo> onApplyStarting,
        bool wasStartedHidden,
        CancellationToken cancellationToken)
    {
        return CheckAsync(
            confirm,
            onApplyStarting,
            wasStartedHidden,
            honourSkippedVersion: false,
            reportFailures: true,
            cancellationToken);
    }

    /// <summary>
    /// The shared fetch -> compare -> (skip-check) -> confirm -> apply body both
    /// public entry points delegate to, so the two never drift (Plan 26-05 Task 1(a)).
    /// <paramref name="reportFailures"/> is the ONLY behavioral difference in the
    /// fetch/compare segment's exception handling: false (the automatic path)
    /// degrades any exception to NotAvailable exactly as before; true (the on-demand
    /// path) converts it into CheckFailed carrying the reason. Every other branch
    /// (skip-suppression, Later/Skip/UpdateNow) is byte-for-byte identical to plan
    /// 26-04's original CheckOnLaunchAsync body, including the deliberately
    /// UNWRAPPED post-confirm download/apply segment -- see class doc comment.
    /// </summary>
    private async Task<UpdateCheckResult> CheckAsync(
        Func<ReleaseInfo, UpdatePromptChoice> confirm,
        Action<ReleaseInfo> onApplyStarting,
        bool wasStartedHidden,
        bool honourSkippedVersion,
        bool reportFailures,
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

        // Three-component Major.Minor.Patch, matching this project's own vX.Y.Z tag
        // scheme (UpdateVersionComparer's class doc comment) -- never the full
        // four-component System.Version.ToString(), which would read "2.2.1.0" in a
        // balloon. Build is normalized via Math.Max(..., 0) since a two-component
        // running Version reports Build == -1 (see UpdateVersionComparer).
        string runningVersionText = $"{_runningVersion.Major}.{_runningVersion.Minor}.{Math.Max(_runningVersion.Build, 0)}";

        ReleaseInfo release;
        try
        {
            ReleaseInfo? fetched = await _releaseFeed.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
            if (fetched is null || !UpdateVersionComparer.IsNewer(_runningVersion, fetched.TagName))
            {
                return new UpdateCheckResult(UpdateCheckOutcome.NotAvailable, runningVersionText, FailureReason: null);
            }

            release = fetched;
        }
        catch (Exception ex)
        {
            // An automatic check must never surface anything (PITFALLS.md UX
            // Pitfalls table) -- degrade any fetch/compare-segment exception to
            // NotAvailable, the same outcome as "no update exists," UNLESS the
            // caller explicitly asked for failures to be reported (the on-demand
            // path, D-07), in which case it becomes the distinct CheckFailed outcome.
            return reportFailures
                ? new UpdateCheckResult(UpdateCheckOutcome.CheckFailed, runningVersionText, ex.Message)
                : new UpdateCheckResult(UpdateCheckOutcome.NotAvailable, runningVersionText, FailureReason: null);
        }

        if (honourSkippedVersion)
        {
            // Prohibition (T-26-14): "Skip this version" must never become "never
            // check for updates again." Comparing the persisted skipped tag against
            // this release's tag via UpdateVersionComparer (numeric
            // Major.Minor.Patch, three-component) rather than a string match is what
            // lets a strictly-newer release still prompt after an earlier skip -- a
            // string-equality check would suppress only the exact skipped tag by
            // coincidence, with no principled way to let a genuinely newer release
            // through except reimplementing this same numeric comparison. Carrying
            // the patch through here matters: without it, a skip of a two-segment
            // tag like "v2.2" would round-trip as X.Y.0 and swallow every later
            // point release of that same minor (the exact T-26-14 prohibition this
            // comment invokes). Best-effort read: an unreadable settings file must
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
                && UpdateVersionComparer.TryParseTag(skippedVersion, out int skippedMajor, out int skippedMinor, out int skippedPatch)
                && !UpdateVersionComparer.IsNewer(new Version(skippedMajor, skippedMinor, skippedPatch), release.TagName))
            {
                // release.TagName is NOT strictly newer than the skipped tag --
                // suppress without ever invoking confirm.
                return new UpdateCheckResult(UpdateCheckOutcome.Skipped, runningVersionText, FailureReason: null);
            }
        }

        UpdatePromptChoice choice = confirm(release);

        if (choice == UpdatePromptChoice.Later)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.Declined, runningVersionText, FailureReason: null);
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

            return new UpdateCheckResult(UpdateCheckOutcome.Skipped, runningVersionText, FailureReason: null);
        }

        onApplyStarting(release);

        // Deliberately UNWRAPPED -- see class doc comment. The user has already
        // confirmed; a download/apply failure here must propagate to the caller so
        // the App layer can toast it (D-08), not be silently swallowed.
        string stagedPath = await _updateApplier.DownloadAndStageAsync(release, cancellationToken).ConfigureAwait(false);
        _updateApplier.ApplyAndRelaunch(stagedPath, wasStartedHidden);

        return new UpdateCheckResult(UpdateCheckOutcome.Applying, runningVersionText, FailureReason: null);
    }
}

/// <summary>
/// Plan 26-05: the result of a single fetch/compare/confirm/apply pass, returned by
/// <see cref="UpdateOrchestrator.CheckOnDemandAsync"/> so the App layer's manual-check
/// toast can distinguish "already up to date" from "the check itself failed" (D-06/
/// D-07) -- a bare <see cref="UpdateCheckOutcome"/> enum member alone cannot carry the
/// running-version text or a failure reason string.
/// </summary>
/// <param name="Outcome">Which branch the shared check sequencer took.</param>
/// <param name="RunningVersionText">
/// The currently-running build's Major.Minor.Patch version text (e.g. "2.2.1"),
/// populated for every outcome; used by the "already on the latest version" toast (D-06).
/// </param>
/// <param name="FailureReason">
/// Non-null only when <see cref="Outcome"/> is <see cref="UpdateCheckOutcome.CheckFailed"/>
/// -- the caught exception's message, for the "Couldn't check for updates" toast (D-07).
/// </param>
public sealed record UpdateCheckResult(UpdateCheckOutcome Outcome, string? RunningVersionText, string? FailureReason);

/// <summary>
/// UpdateOrchestrator's possible outcomes. <see cref="CheckFailed"/> (Plan 26-05,
/// renamed from the placeholder <c>Failed</c> member plan 26-04 declared in advance
/// for exactly this purpose -- see git history) is produced only by
/// <see cref="UpdateOrchestrator.CheckOnDemandAsync"/>'s fetch/compare segment: the
/// on-demand path's manual-check toast must show a distinct failure state (D-07)
/// rather than propagating an exception. <see cref="UpdateOrchestrator.CheckOnLaunchAsync"/>'s
/// fetch/compare segment still degrades every failure to <see cref="NotAvailable"/>
/// (see class doc comment) and never produces this member.
/// </summary>
public enum UpdateCheckOutcome
{
    NotAvailable,
    Declined,
    Applying,
    CheckFailed,
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
