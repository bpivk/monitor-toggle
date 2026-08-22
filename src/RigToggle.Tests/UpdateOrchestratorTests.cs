using System.Threading;
using System.Threading.Tasks;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves UpdateOrchestrator.CheckOnLaunchAsync's six decision branches using two
/// hand-rolled doubles (no mocking library, matching this project's existing
/// FakeMonitorController/FakeAppController convention in Doubles/FakeControllers.cs):
/// a configurable fake IReleaseFeed and a recording fake IUpdateApplier. Async
/// Task test methods throughout, no blocking task operations (xUnit1031).
/// </summary>
public class UpdateOrchestratorTests
{
    private static readonly Version RunningVersion = new(2, 1, 0, 0);

    private static readonly ReleaseInfo NewerRelease = new(
        "v2.2",
        "https://objects.githubusercontent.com/asset.exe",
        "https://github.com/bpivk/monitor-toggle/releases/tag/v2.2",
        DateTimeOffset.UtcNow,
        Prerelease: false,
        Body: "Release notes.",
        ChecksumDownloadUrl: "https://objects.githubusercontent.com/asset.exe.sha256");

    private static readonly ReleaseInfo OlderRelease = NewerRelease with { TagName = "v2.0" };

    /// <summary>UPDATE-02: a null feed result must read identically to "already up to date."</summary>
    [Fact]
    public async Task CheckOnLaunchAsync_FeedReturnsNull_ReturnsNotAvailable_ConfirmAndApplierNeverInvoked()
    {
        var callLog = new List<string>();
        var feed = new FakeReleaseFeed(release: null);
        var applier = new RecordingUpdateApplier(callLog);
        var orchestrator = new UpdateOrchestrator(feed, applier, RunningVersion);
        bool confirmCalled = false;

        UpdateCheckOutcome outcome = await orchestrator.CheckOnLaunchAsync(
            confirm: _ => { confirmCalled = true; return true; },
            onApplyStarting: _ => { },
            wasStartedHidden: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.NotAvailable, outcome);
        Assert.False(confirmCalled);
        Assert.Empty(callLog);
    }

    /// <summary>
    /// UPDATE-02/PITFALLS.md Pitfall 3: a not-strictly-newer tag (older or equal to
    /// the running version) must never prompt the user.
    /// </summary>
    [Fact]
    public async Task CheckOnLaunchAsync_FeedReturnsOlderOrEqualTag_ReturnsNotAvailable_ConfirmNeverInvoked()
    {
        var callLog = new List<string>();
        var feed = new FakeReleaseFeed(release: OlderRelease);
        var applier = new RecordingUpdateApplier(callLog);
        var orchestrator = new UpdateOrchestrator(feed, applier, RunningVersion);
        bool confirmCalled = false;

        UpdateCheckOutcome outcome = await orchestrator.CheckOnLaunchAsync(
            confirm: _ => { confirmCalled = true; return true; },
            onApplyStarting: _ => { },
            wasStartedHidden: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.NotAvailable, outcome);
        Assert.False(confirmCalled);
        Assert.Empty(callLog);
    }

    /// <summary>
    /// PITFALLS.md UX Pitfalls table: an automatic check must never surface
    /// anything -- a feed exception degrades to NotAvailable, not a propagated
    /// exception (contrast with the post-confirm apply-throws case below).
    /// </summary>
    [Fact]
    public async Task CheckOnLaunchAsync_FeedThrows_ReturnsNotAvailable_NoExceptionEscapes()
    {
        var callLog = new List<string>();
        var feed = new FakeReleaseFeed(exceptionToThrow: new InvalidOperationException("simulated feed failure"));
        var applier = new RecordingUpdateApplier(callLog);
        var orchestrator = new UpdateOrchestrator(feed, applier, RunningVersion);

        UpdateCheckOutcome outcome = await orchestrator.CheckOnLaunchAsync(
            confirm: _ => true,
            onApplyStarting: _ => { },
            wasStartedHidden: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.NotAvailable, outcome);
        Assert.Empty(callLog);
    }

    /// <summary>UPDATE-03: declining the confirm dialog must never touch the applier.</summary>
    [Fact]
    public async Task CheckOnLaunchAsync_NewerTagConfirmDeclines_ReturnsDeclined_ApplierNeverInvoked()
    {
        var callLog = new List<string>();
        var feed = new FakeReleaseFeed(release: NewerRelease);
        var applier = new RecordingUpdateApplier(callLog);
        var orchestrator = new UpdateOrchestrator(feed, applier, RunningVersion);

        UpdateCheckOutcome outcome = await orchestrator.CheckOnLaunchAsync(
            confirm: _ => false,
            onApplyStarting: _ => { },
            wasStartedHidden: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.Declined, outcome);
        Assert.Empty(callLog);
    }

    /// <summary>
    /// UPDATE-03/UPDATE-04: confirming triggers onApplyStarting exactly once, then
    /// DownloadAndStageAsync, then ApplyAndRelaunch, in that exact order, with the
    /// staged path returned by DownloadAndStageAsync threaded through unchanged into
    /// ApplyAndRelaunch's argument.
    /// </summary>
    [Fact]
    public async Task CheckOnLaunchAsync_NewerTagConfirmed_InvokesApplyStartingThenDownloadThenApply_InThatOrder_ReturnsApplying()
    {
        var callLog = new List<string>();
        const string stagedPath = "C:\\Program Files\\RigToggle\\RigToggle.App.update.exe";
        var feed = new FakeReleaseFeed(release: NewerRelease);
        var applier = new RecordingUpdateApplier(callLog, stagedPathToReturn: stagedPath);
        var orchestrator = new UpdateOrchestrator(feed, applier, RunningVersion);
        int onApplyStartingCallCount = 0;

        UpdateCheckOutcome outcome = await orchestrator.CheckOnLaunchAsync(
            confirm: _ => true,
            onApplyStarting: _ =>
            {
                onApplyStartingCallCount++;
                callLog.Add("onApplyStarting");
            },
            wasStartedHidden: false,
            cancellationToken: CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.Applying, outcome);
        Assert.Equal(1, onApplyStartingCallCount);
        Assert.Equal(
            new[] { "onApplyStarting", "applier.DownloadAndStageAsync", "applier.ApplyAndRelaunch" },
            callLog);
        Assert.Equal(stagedPath, applier.StagedPathPassedToApply);
    }

    /// <summary>
    /// D-08: a download/apply failure AFTER the user has explicitly confirmed must
    /// propagate to the caller (so the App layer can toast it), never be swallowed --
    /// the opposite exception-handling posture from the automatic fetch/compare
    /// segment above.
    /// </summary>
    [Fact]
    public async Task CheckOnLaunchAsync_ApplierThrowsAfterConfirmation_ExceptionPropagates_IsNotSwallowed()
    {
        var callLog = new List<string>();
        var feed = new FakeReleaseFeed(release: NewerRelease);
        var applier = new RecordingUpdateApplier(callLog, applyExceptionToThrow: new InvalidOperationException("simulated apply failure"));
        var orchestrator = new UpdateOrchestrator(feed, applier, RunningVersion);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.CheckOnLaunchAsync(
            confirm: _ => true,
            onApplyStarting: _ => { },
            wasStartedHidden: false,
            cancellationToken: CancellationToken.None));

        Assert.Equal("simulated apply failure", ex.Message);
    }

    /// <summary>Fake IReleaseFeed configurable to return a supplied release, null, or throw.</summary>
    private sealed class FakeReleaseFeed : IReleaseFeed
    {
        private readonly ReleaseInfo? _release;
        private readonly Exception? _exceptionToThrow;

        public FakeReleaseFeed(ReleaseInfo? release = null, Exception? exceptionToThrow = null)
        {
            _release = release;
            _exceptionToThrow = exceptionToThrow;
        }

        public Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            if (_exceptionToThrow is not null)
            {
                throw _exceptionToThrow;
            }

            return Task.FromResult(_release);
        }
    }

    /// <summary>
    /// Recording fake IUpdateApplier -- appends to the shared call-log so tests can
    /// assert the exact call sequence (not just counts), records the staged path it
    /// was handed, returns a canned staged path, and can be configured to throw from
    /// ApplyAndRelaunch to prove D-08's unwrapped-propagation contract.
    /// </summary>
    private sealed class RecordingUpdateApplier : IUpdateApplier
    {
        private readonly List<string> _callLog;
        private readonly string _stagedPathToReturn;
        private readonly Exception? _applyExceptionToThrow;

        public RecordingUpdateApplier(
            List<string> callLog,
            string stagedPathToReturn = "staged.exe",
            Exception? applyExceptionToThrow = null)
        {
            _callLog = callLog;
            _stagedPathToReturn = stagedPathToReturn;
            _applyExceptionToThrow = applyExceptionToThrow;
        }

        public string? StagedPathPassedToApply { get; private set; }

        public Task<string> DownloadAndStageAsync(ReleaseInfo release, CancellationToken cancellationToken)
        {
            _callLog.Add("applier.DownloadAndStageAsync");
            return Task.FromResult(_stagedPathToReturn);
        }

        public void ApplyAndRelaunch(string stagedExePath, bool wasStartedHidden)
        {
            _callLog.Add("applier.ApplyAndRelaunch");
            StagedPathPassedToApply = stagedExePath;

            if (_applyExceptionToThrow is not null)
            {
                throw _applyExceptionToThrow;
            }
        }
    }
}
