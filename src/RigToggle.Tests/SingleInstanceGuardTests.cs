using System;
using System.Diagnostics;
using System.Threading.Tasks;
using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves SingleInstanceGuard's acquire/reject/release/readiness semantics
/// (INSTANCE-01, PITFALLS.md Pitfall 8) entirely in-process, without spawning a real
/// second OS process -- the kernel primitive (named Mutex) is genuinely cross-process,
/// but its "am I the first" answer is provable from two guard instances inside this one
/// test process, exactly like ToggleOrchestratorTests proves ToggleOrchestrator's
/// in-process reentrancy semantics. Each test disposes every guard it creates via
/// `using`/`using var` so the next test starts from a clean, unheld mutex/event -- these
/// tests run sequentially within this class (xUnit's default for methods in one class),
/// so there is no cross-test parallelism to guard against. Real cross-process
/// end-to-end proof (a genuine second launched process) is plan 25-03's responsibility.
///
/// 25-03 Task 3 regression fix: every <c>Acquire()</c>/<c>WaitForInstanceReady(...)</c>
/// call in this file passes <see cref="TestInstanceId"/>, NEVER the default (production)
/// instance id. <c>dotnet test RigToggle.sln</c> runs this project (RigToggle.Tests) and
/// RigToggle.Windows.Tests as two SEPARATE VSTest host processes in parallel; before this
/// fix, this file's in-process tests and RigToggle.Windows.Tests'
/// <c>SingleInstanceProcessTests</c> (which launches real <c>RigToggle.App.exe</c> child
/// processes) both contended for the exact same machine-wide named kernel object
/// (<c>Global\RigToggle-{the hardcoded production GUID}</c>), producing genuine
/// cross-assembly test failures that looked like guard bugs but were actually two
/// unrelated test suites racing on the one real production mutex name. A random GUID
/// generated once per test-assembly load is unique to this run and cannot collide with
/// the real app's name or with any other concurrent test run on the same machine.
/// </summary>
public class SingleInstanceGuardTests : IDisposable
{
    // Deliberately NOT SingleInstanceGuard.InstanceId -- see the class doc comment's
    // 25-03 Task 3 regression note. Computed once per test-assembly load (static
    // readonly), shared by every test method in this class so within-test acquire
    // sequences (e.g. Acquire_WhileFirstInstanceAlive_ReturnsNonPrimaryGuard's
    // first/second pair) still observe each other correctly.
    private static readonly string TestInstanceId = Guid.NewGuid().ToString();

    // No shared external resource to clean up across tests -- every guard this class's
    // tests create is scoped with `using`/`using var` at the point of creation, which
    // deterministically releases the named mutex/event handle (even on assertion
    // failure) before the next test method runs. This Dispose() exists to match this
    // codebase's established IDisposable-test-class convention (ToggleOrchestratorTests)
    // rather than because there is state left to release here.
    public void Dispose()
    {
    }

    [Fact]
    public void Acquire_NothingHoldsMutex_ReturnsPrimaryGuard()
    {
        using var guard = SingleInstanceGuard.Acquire(TestInstanceId);

        Assert.True(guard.IsPrimaryInstance);
    }

    [Fact]
    public void Acquire_WhileFirstInstanceAlive_ReturnsNonPrimaryGuard()
    {
        // INSTANCE-01: the kernel semantic this whole plan rests on -- a second
        // acquisition against a live first must observe "not primary," provable without
        // spawning a process.
        using var first = SingleInstanceGuard.Acquire(TestInstanceId);
        using var second = SingleInstanceGuard.Acquire(TestInstanceId);

        Assert.True(first.IsPrimaryInstance);
        Assert.False(second.IsPrimaryInstance);
    }

    [Fact]
    public void Dispose_ReleasesMutex_SubsequentAcquireIsPrimaryAgain()
    {
        var first = SingleInstanceGuard.Acquire(TestInstanceId);
        Assert.True(first.IsPrimaryInstance);
        first.Dispose();

        using var second = SingleInstanceGuard.Acquire(TestInstanceId);
        Assert.True(second.IsPrimaryInstance);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var guard = SingleInstanceGuard.Acquire(TestInstanceId);
        guard.Dispose();

        var exception = Record.Exception(() => guard.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_NonPrimaryGuard_DoesNotThrow()
    {
        // A non-primary guard never owned the mutex, so its release path must be
        // skipped entirely, not attempted (and failed) against a mutex it never held.
        using var primary = SingleInstanceGuard.Acquire(TestInstanceId);
        var nonPrimary = SingleInstanceGuard.Acquire(TestInstanceId);
        Assert.False(nonPrimary.IsPrimaryInstance);

        var exception = Record.Exception(() => nonPrimary.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void MutexNameAndReadyEventName_ShareSameNamespacePrefix()
    {
        // The namespace-scope decision (Global\ vs Local\) must be made exactly once
        // and applied identically to both names -- a mismatch would leave the loser
        // waiting on a handle the winner never signals (Pitfall 8 reopened permanently).
        using var guard = SingleInstanceGuard.Acquire(TestInstanceId);
        string expectedPrefix = guard.IsGlobalScope ? @"Global\" : @"Local\";

        Assert.StartsWith(expectedPrefix, guard.MutexName);
        Assert.StartsWith(expectedPrefix, guard.ReadyEventName);
    }

    [Fact]
    public void MarkReady_OnPrimaryGuard_MakesWaitForInstanceReadyReturnTrue()
    {
        using var guard = SingleInstanceGuard.Acquire(TestInstanceId);
        guard.MarkReady();

        bool result = SingleInstanceGuard.WaitForInstanceReady(TimeSpan.FromSeconds(2), TestInstanceId);

        Assert.True(result);
    }

    [Fact]
    public void MarkReady_OnNonPrimaryGuard_DoesNotThrow()
    {
        using var primary = SingleInstanceGuard.Acquire(TestInstanceId);
        using var nonPrimary = SingleInstanceGuard.Acquire(TestInstanceId);
        Assert.False(nonPrimary.IsPrimaryInstance);

        var exception = Record.Exception(() => nonPrimary.MarkReady());

        Assert.Null(exception);
    }

    [Fact]
    public void WaitForInstanceReady_NoInstancePublished_ReturnsFalseQuickly()
    {
        // No guard is acquired anywhere in this test -- the readiness handle genuinely
        // does not exist. WaitForInstanceReady must fail fast off its own bounded
        // open-retry budget rather than burn the entire DefaultReadyWaitTimeout, since
        // the handle's absence means no genuine Rig Toggle instance ever published
        // readiness, not merely "not yet."
        var stopwatch = Stopwatch.StartNew();

        bool result = SingleInstanceGuard.WaitForInstanceReady(SingleInstanceGuard.DefaultReadyWaitTimeout, TestInstanceId);

        stopwatch.Stop();
        Assert.False(result);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(3),
            $"Expected a fast fail well under the {SingleInstanceGuard.DefaultReadyWaitTimeout} default timeout, took {stopwatch.Elapsed}.");
    }

    /// <summary>
    /// Pitfall 8's actual shape: a loser waiting on the readiness handle must still see
    /// it become true when the winner signals concurrently -- not just when readiness
    /// was already published before the wait began. Deterministic (no Thread.Sleep
    /// timing guess, 07-RESEARCH.md Pitfall 2 discipline): the readiness mutex is
    /// level-triggered once released, so MarkReady() being called anywhere before or
    /// during the background WaitForInstanceReady call still makes the bounded join
    /// below succeed deterministically.
    ///
    /// Deliberately a synchronous (not `async`/`await`) test method, matching
    /// ToggleOrchestratorTests' established `.GetAwaiter().GetResult()` blocking-wait
    /// convention (xUnit1031 warning accepted, same as that file's existing 4
    /// instances) rather than `await`: a named <see cref="Mutex"/> has THREAD affinity
    /// on the acquiring side (the `using var guard` above must be disposed on the exact
    /// thread that acquired it). An `async Task` test method's continuation after an
    /// `await` can legitimately resume on a different thread-pool thread than the one
    /// that ran the method's synchronous prologue -- which would silently break the
    /// guard's release (the swallowed <see cref="ApplicationException"/> from a
    /// wrong-thread <c>ReleaseMutex</c> call) and leak the mutex into subsequent tests.
    /// Keeping the whole test body synchronous keeps `guard` on one thread throughout.
    /// </summary>
    private static readonly TimeSpan ConcurrentReadinessJoinTimeout = TimeSpan.FromSeconds(6);

    [Fact]
    public void WaitForInstanceReady_ReadinessPublishedWhileWaitInProgress_ReturnsTrue()
    {
        using var guard = SingleInstanceGuard.Acquire(TestInstanceId);

        var waitTask = Task.Run(() => SingleInstanceGuard.WaitForInstanceReady(SingleInstanceGuard.DefaultReadyWaitTimeout, TestInstanceId));

        guard.MarkReady();

        Assert.True(waitTask.Wait(ConcurrentReadinessJoinTimeout), "Background WaitForInstanceReady call never returned.");
        Assert.True(waitTask.Result);
    }
}
