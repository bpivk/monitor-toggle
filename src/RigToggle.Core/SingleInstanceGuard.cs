using System.Diagnostics;
using System.Threading;

namespace RigToggle.Core;

/// <summary>
/// INSTANCE-01: a cross-process, OS-kernel single-instance primitive built on a named
/// <see cref="Mutex"/>. This is deliberately NOT the same mechanism as this codebase's
/// existing in-process, same-toggle-in-flight reentrancy guard (a separate class in this
/// namespace, intentionally not referenced anywhere in this file — see PITFALLS.md
/// Pitfall 7), and shares no field, method, or type with it: that guard's flag is
/// process-local memory with zero visibility into a second, separately-launched OS
/// process, which by definition starts with its own fresh copy of every in-process
/// field. Single-instance detection needs a kernel object visible across process
/// boundaries on the same login session; reentrancy guarding needs only a shared memory
/// flag inside one process. Conflating the two would silently fail to detect a genuine
/// second process while looking superficially correct.
///
/// <see cref="InstanceId"/> is a fixed, hardcoded GUID rather than anything reflected or
/// derived (e.g. from the assembly name or install path) so the mutex name is stable and
/// predictable across every build and every machine this app ever runs on — a derived
/// name risks silently changing (and breaking single-instance detection) as a side effect
/// of an unrelated rename/refactor/publish-profile change, which a hardcoded literal
/// cannot do.
///
/// Windows automatically releases a mutex's ownership when the owning process terminates
/// for any reason, including a crash or a kill — there is no OS-level "abandoned mutex"
/// state that can permanently wedge future launches; the only documented artifact is that
/// the next <see cref="Mutex.WaitOne()"/>-style acquisition on that name throws
/// <see cref="AbandonedMutexException"/>. The MAIN mutex is acquired via a plain
/// <c>new Mutex(...)</c>, not a <c>WaitOne</c>, so it never surfaces that exception. The
/// SEPARATE readiness mutex is different: <see cref="WaitForInstanceReady(TimeSpan, string?)"/>
/// genuinely does <c>WaitOne</c> it, and DOES surface <see cref="AbandonedMutexException"/>
/// when the primary dies anywhere between <see cref="Acquire(string?)"/> and
/// <see cref="MarkReady"/> (CR-01, 25-REVIEW.md/25-VERIFICATION.md). That case is caught
/// at the <c>WaitOne</c> call site and treated as a successful acquisition, because the
/// wait genuinely did acquire ownership and the release that follows is exactly what
/// clears the abandoned state for the next waiter. See
/// <c>WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing</c>
/// in <c>SingleInstanceGuardTests</c> for the regression proof.
///
/// PITFALLS.md Pitfall 8 / T-25-07: winning the mutex alone does not guarantee the
/// "loser" process can actually wake this instance's window — a separate readiness
/// handshake closes that race. <see cref="ReadyEventName"/> is a second, independent
/// named <see cref="Mutex"/> (not a named <c>EventWaitHandle</c>/<c>Semaphore</c> — see
/// below) modelling a level-triggered "ready" signal: the primary creates and holds it
/// (held == "not ready yet"), <see cref="MarkReady"/> releases it (released == "ready,
/// forever, for any number of future waiters"), and a losing process's
/// <see cref="WaitForInstanceReady(TimeSpan, string?)"/> opens the same name and blocks in
/// <c>WaitOne</c> until it becomes available, then immediately releases it again so the
/// signal remains available to the next waiter too. <see cref="IsGlobalScope"/> is
/// resolved exactly once, at acquisition, and applied identically to
/// <see cref="MutexName"/> and <see cref="ReadyEventName"/> — a mismatch between the two
/// names would leave a loser waiting on a handle this instance never signals, reopening
/// Pitfall 8 permanently. T-25-06: the one recoverable failure mode — this token cannot
/// create or open a name in the <c>Global\</c> namespace — falls back to a session-local
/// (<c>Local\</c>) scope rather than propagating out of <c>Main()</c>, per
/// <see cref="StartupArgs"/>' documented never-crash-on-startup posture.
///
/// WR-01, known accepted limitation: <see cref="IsGlobalScope"/> is resolved
/// independently per-process, so two processes whose tokens disagree about
/// <c>Global\</c> access — one launched elevated and one not, or two login/RDP
/// sessions — can each resolve a different namespace, each observe
/// <c>createdNew == true</c>, and each believe it is primary, silently defeating
/// INSTANCE-01 with two processes writing <c>mode.json</c>, <c>settings.json</c>, and
/// the monitor/audio state with no coordination. Accepted at ASVS level 1 for this
/// single-user personal rig, where every launch is the same non-elevated user in one
/// session, and this has never been observed. The deferred stronger fix — have the
/// primary-concluding path probe the opposite namespace with
/// <see cref="Mutex.TryOpenExisting(string, out Mutex?)"/> before concluding it is
/// primary, so a scope mismatch degrades to "cannot tell, assume duplicate and
/// broadcast" — is deliberately NOT implemented here: it changes single-instance
/// detection semantics and carries more regression risk than the failure it prevents,
/// which has never been observed on this rig.
///
/// Deviation from the original design (documented in 25-01-SUMMARY.md): the readiness
/// signal was originally specified as a named <c>EventWaitHandle</c>. Empirically, named
/// <c>EventWaitHandle</c>/<c>Semaphore</c> throw <c>PlatformNotSupportedException</c> on
/// this project's Linux build/test environment (RigToggle.Core deliberately targets
/// plain <c>net10.0</c>, not <c>net10.0-windows</c>, specifically so its test suite can
/// run here) — only named <see cref="Mutex"/> is supported cross-platform. A second named
/// Mutex, used as described above, is functionally equivalent on the real Windows
/// production target and is the only primitive that also satisfies this codebase's
/// cross-platform-testable requirement.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// Fixed, hardcoded GUID identifying this application's single-instance mutex name.
    /// Never derive this value — see the class doc comment for why.
    /// </summary>
    public const string InstanceId = "8f3a1c42-7b5e-4d19-9a06-2e5c1f8b7d34";

    /// <summary>
    /// Default budget <see cref="WaitForInstanceReady(TimeSpan, string?)"/> is called with by
    /// the losing process before broadcasting the activation signal regardless.
    /// </summary>
    public static readonly TimeSpan DefaultReadyWaitTimeout = TimeSpan.FromSeconds(5);

    // WaitForInstanceReady's open-retry budget: a small, bounded number of attempts at
    // a short interval, so a handle that was never published at all is reported false
    // quickly rather than burning the entire DefaultReadyWaitTimeout.
    private const int ReadyHandleOpenAttempts = 5;
    private static readonly TimeSpan ReadyHandleOpenRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly Mutex _mutex;
    private readonly Mutex? _readyMutex;
    private int _disposed;
    private int _readyMutexReleased;

    private SingleInstanceGuard(
        Mutex mutex,
        bool isPrimaryInstance,
        string mutexName,
        bool isGlobalScope,
        string readyEventName,
        Mutex? readyMutex)
    {
        _mutex = mutex;
        _readyMutex = readyMutex;
        IsPrimaryInstance = isPrimaryInstance;
        MutexName = mutexName;
        IsGlobalScope = isGlobalScope;
        ReadyEventName = readyEventName;
    }

    /// <summary>
    /// The fully-qualified named-kernel-object name this guard acquired, e.g.
    /// <c>Global\RigToggle-{InstanceId}</c>.
    /// </summary>
    public string MutexName { get; }

    /// <summary>
    /// True iff this process is the one that created the named mutex (i.e. no other
    /// Rig Toggle process currently holds it) — the INSTANCE-01 primary/duplicate
    /// signal. Backed by <see cref="Mutex(bool, string, out bool)"/>'s own atomic
    /// "did I create this or did it already exist" check, so there is no race window
    /// in the check itself.
    /// </summary>
    public bool IsPrimaryInstance { get; }

    /// <summary>
    /// True iff <see cref="MutexName"/>/<see cref="ReadyEventName"/> resolved to the
    /// <c>Global\</c> namespace (the normal case); false iff this process's token could
    /// not create/open names in that namespace and both fell back to the session-local
    /// <c>Local\</c> namespace instead. Resolved exactly once, at acquisition — see the
    /// class doc comment.
    /// </summary>
    public bool IsGlobalScope { get; }

    /// <summary>
    /// The named readiness-signal this instance publishes (if primary) once its window
    /// handle provably exists (<see cref="MarkReady"/>), and the name a losing
    /// process's <see cref="WaitForInstanceReady(TimeSpan, string?)"/> call opens. Despite the
    /// name, this is backed by a second named <see cref="Mutex"/>, not a Windows Event
    /// object — see the class doc comment's Deviation note.
    /// </summary>
    public string ReadyEventName { get; }

    /// <summary>
    /// Acquires (or discovers) the named cross-process mutex identifying this
    /// application. <c>initiallyOwned: true</c> means the current thread requests
    /// ownership atomically as part of construction — if this call returns with
    /// <c>createdNew == true</c>, this process now owns the mutex without a separate
    /// <c>WaitOne()</c> call.
    ///
    /// T-25-06: tries the <c>Global\</c> namespace first; if the calling token cannot
    /// create/open a name there, the one documented recoverable outcome (caught below)
    /// triggers a single retry in the session-local <c>Local\</c> namespace instead of
    /// propagating out of <c>Main()</c>. No other exception type is caught here: this
    /// is a correctness-critical call, not a best-effort one.
    ///
    /// <paramref name="instanceId"/> is a TEST-ONLY escape hatch (25-03 Task 3 operator
    /// checkpoint regression): defaults to <see cref="InstanceId"/> for every real
    /// caller. <c>RigToggle.App</c>'s <c>Program.cs</c> must never pass a non-null
    /// value here — doing so would break real single-instance detection against the
    /// actual shipped application. It exists solely so in-process unit tests
    /// (<c>SingleInstanceGuardTests</c>, cross-platform, <c>RigToggle.Tests</c>) can
    /// prove this class's acquire/reject/release/readiness semantics against a
    /// process-unique test name instead of the literal production name --
    /// <c>RigToggle.Windows.Tests</c>' child-process tests run in a SEPARATE VSTest
    /// host process in the same <c>dotnet test</c> invocation and, before this fix,
    /// both projects contended for the exact same machine-wide named kernel object,
    /// producing cross-assembly test flakiness that looked like (but was not) a bug
    /// in the guard itself.
    /// </summary>
    public static SingleInstanceGuard Acquire(string? instanceId = null)
    {
        string effectiveInstanceId = instanceId ?? InstanceId;
        bool isGlobalScope = true;
        string mutexName = BuildName("RigToggle-" + effectiveInstanceId, isGlobalScope);
        Mutex mutex;
        bool createdNew;
        try
        {
            mutex = new Mutex(initiallyOwned: true, mutexName, out createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            isGlobalScope = false;
            mutexName = BuildName("RigToggle-" + effectiveInstanceId, isGlobalScope);
            mutex = new Mutex(initiallyOwned: true, mutexName, out createdNew);
        }

        // IsGlobalScope is resolved exactly once above and applied identically to both
        // MutexName and ReadyEventName below -- see the class doc comment for why a
        // mismatch between the two names would be the single worst failure available
        // here.
        string readyEventName = BuildName("RigToggle-Ready-" + effectiveInstanceId, isGlobalScope);

        // Created here, at acquisition, held (owned) in the "not ready yet" state -- not
        // later -- so a loser arriving microseconds after this call can already find it
        // (Pitfall 8's "make the receiver ready as early as possible" guidance). Only
        // the primary instance publishes it; a non-primary guard has nothing to signal.
        //
        // WR-02/T-25-06: this construction is guarded, unlike the main mutex's
        // correctness-critical construction above. Three reasons. First, why broad
        // here while the main mutex's catch two blocks above is deliberately narrow:
        // the main mutex decides primary-vs-duplicate and is correctness-critical, so
        // swallowing an unexpected exception type there would trade a loud failure for
        // a silent INSTANCE-01 violation; this readiness mutex is a Pitfall-8 latency
        // optimisation layered over an already-tolerant broadcast path, so no failure
        // to create it may ever be allowed to take down startup (Security Domain V5's
        // never-crash-on-startup posture -- the same contract CR-01 violated at the
        // other end of this class). Second, the degraded state is already fully
        // supported and already reachable: MarkReady() no-ops on a null readiness
        // mutex, Dispose() null-guards it, the loser's WaitForInstanceReady already
        // fails fast to false when the handle never appears, and Program.cs broadcasts
        // regardless -- so this adds a failure mode, not a code path, and
        // MarkReady_OnNonPrimaryGuard_DoesNotThrow and
        // WaitForInstanceReady_NoInstancePublished_ReturnsFalseQuickly already cover
        // the resulting behaviour. Third, without this guard the exception escaped
        // Acquire() before its own TryLog line ever ran, so the app crashed on
        // startup with zero diagnostic trail AND leaked the already-owned main mutex
        // handle.
        Mutex? readyMutex = null;
        if (createdNew)
        {
            try
            {
                readyMutex = new Mutex(initiallyOwned: true, readyEventName, out _);
            }
            catch (Exception ex)
            {
                TryLog($"Acquire: failed to construct readiness mutex '{readyEventName}': {ex}. Readiness will never be signalled by this instance (WR-02).");
            }
        }

        TryLog($"Acquire: IsPrimaryInstance={createdNew}, MutexName={mutexName}, IsGlobalScope={isGlobalScope}.");

        return new SingleInstanceGuard(mutex, createdNew, mutexName, isGlobalScope, readyEventName, readyMutex);
    }

    /// <summary>
    /// Signals this instance's readiness by releasing the readiness mutex, waking any
    /// process currently blocked in <see cref="WaitForInstanceReady(TimeSpan, string?)"/> and
    /// leaving it available to any future waiter too (level-triggered, not
    /// single-consumer). No-op (never throws) when this guard is not primary — a
    /// non-primary guard never published a readiness mutex and has nothing to signal.
    /// Idempotent: calling this more than once has no additional effect.
    /// </summary>
    public void MarkReady()
    {
        if (_readyMutex is null)
        {
            TryLog("MarkReady: no-op -- this guard is not primary, nothing to signal.");
            return;
        }

        if (Interlocked.Exchange(ref _readyMutexReleased, 1) == 0)
        {
            ReleaseIgnoringOwnershipError(_readyMutex);
            TryLog("MarkReady: readiness mutex released -- instance is now signalled ready.");
        }
    }

    /// <summary>
    /// Called by a losing process before broadcasting the activation signal. Opens the
    /// primary instance's readiness mutex (trying <c>Global\</c> then <c>Local\</c>,
    /// since the loser does not know in advance which namespace the winner resolved to)
    /// with a small bounded number of retries, then blocks in <c>WaitOne</c> for
    /// whatever budget remains of <paramref name="timeout"/> — releasing it again
    /// immediately on success so the "ready" state stays available to any later waiter.
    ///
    /// Returns false for two structurally different reasons a caller may want to treat
    /// differently: the mutex never opened at all within the open-retry budget (no
    /// genuine Rig Toggle instance ever published readiness — fails fast rather than
    /// burning the whole timeout), or it opened but was never released within the
    /// remaining budget (a real instance exists but did not confirm readiness in time).
    ///
    /// <paramref name="instanceId"/> is the same TEST-ONLY escape hatch documented on
    /// <see cref="Acquire(string?)"/> — defaults to <see cref="InstanceId"/> for every
    /// real caller (both <c>Program.cs</c>'s loser branch and
    /// <c>SingleInstanceProcessTests</c>' in-test-host waits against a real child
    /// process legitimately need the production name; only
    /// <c>SingleInstanceGuardTests</c>' in-process tests pass a non-null override).
    /// </summary>
    public static bool WaitForInstanceReady(TimeSpan timeout, string? instanceId = null)
    {
        string effectiveInstanceId = instanceId ?? InstanceId;
        var stopwatch = Stopwatch.StartNew();

        Mutex? readyMutex = null;
        for (int attempt = 0; attempt < ReadyHandleOpenAttempts && readyMutex is null; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(ReadyHandleOpenRetryDelay);
            }

            if (!Mutex.TryOpenExisting(BuildName("RigToggle-Ready-" + effectiveInstanceId, isGlobalScope: true), out readyMutex))
            {
                Mutex.TryOpenExisting(BuildName("RigToggle-Ready-" + effectiveInstanceId, isGlobalScope: false), out readyMutex);
            }
        }

        if (readyMutex is null)
        {
            // The readiness mutex never appeared -- no genuine Rig Toggle instance ever
            // published it. Fail fast rather than burn the whole timeout.
            TryLog($"WaitForInstanceReady: readiness mutex never opened after {ReadyHandleOpenAttempts} attempts -- no instance ever published readiness. Returning false.");
            return false;
        }

        using (readyMutex)
        {
            TimeSpan elapsed = stopwatch.Elapsed;
            TimeSpan remaining = elapsed < timeout ? timeout - elapsed : TimeSpan.Zero;

            bool acquired;
            try
            {
                acquired = readyMutex.WaitOne(remaining);
            }
            catch (AbandonedMutexException)
            {
                // CR-01: the primary died between Acquire() and MarkReady(), so the OS
                // marked this readiness mutex abandoned. The .NET contract for this
                // exception is that the wait SUCCEEDED and ownership WAS transferred to
                // this thread -- and the readiness mutex carries no shared state beyond a
                // held-or-released signal, so there is no inconsistent state here that a
                // normal successful wait would not also have. Treating this as acquired
                // is what routes control into the release branch below, which clears the
                // abandoned state; returning false instead would skip that release, leave
                // this thread owning the mutex, and re-abandon it the moment this method
                // returns -- turning one dead primary into an unbounded chain of this same
                // exception for every launch that follows.
                acquired = true;
                TryLog("WaitForInstanceReady: readiness mutex was abandoned (primary died before MarkReady) -- treating as a successful acquisition (CR-01).");
            }

            if (acquired)
            {
                // Release immediately -- this models a level-triggered readiness signal,
                // not a single-consumer resource, so the next waiter must also succeed.
                ReleaseIgnoringOwnershipError(readyMutex);
            }

            TryLog($"WaitForInstanceReady: readiness mutex opened, WaitOne(remaining={remaining}) returned {acquired}.");
            return acquired;
        }
    }

    private static string BuildName(string suffix, bool isGlobalScope) =>
        (isGlobalScope ? @"Global\" : @"Local\") + suffix;

    // Best-effort diagnostic logging (25-03 Task 3 operator checkpoint investigation),
    // routed through Trace.WriteLine so RigToggle.App's TextWriterTraceListener (wired
    // in Program.cs, now before the guard acquisition -- see that file's own
    // investigation note) persists it to %LOCALAPPDATA%\RigToggle\debug.log. Never
    // throws -- a logging failure must never affect this correctness-critical guard.
    private static void TryLog(string message)
    {
        try
        {
            Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SingleInstanceGuard: {message}");
        }
        catch
        {
            // Logging is diagnostic-only; never let it affect guard behavior.
        }
    }

    /// <summary>
    /// Releases mutex ownership (only if this instance actually owns it — a
    /// non-primary guard never called <c>WaitOne</c> and must not attempt release) and
    /// disposes the underlying handle. Guarded against double-dispose with
    /// <see cref="Interlocked.Exchange(ref int, int)"/> — the release effect can only
    /// ever run once per instance, regardless of how many times <see cref="Dispose"/>
    /// is called.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (IsPrimaryInstance)
        {
            ReleaseIgnoringOwnershipError(_mutex);
        }

        _mutex.Dispose();

        if (_readyMutex is not null && Interlocked.Exchange(ref _readyMutexReleased, 1) == 0)
        {
            ReleaseIgnoringOwnershipError(_readyMutex);
        }

        _readyMutex?.Dispose();
    }

    /// <summary>
    /// Shared release helper for both <see cref="_mutex"/> (in <see cref="Dispose"/>)
    /// and <see cref="_readyMutex"/> (in <see cref="MarkReady"/>/<see cref="Dispose"/>
    /// and <see cref="WaitForInstanceReady(TimeSpan, string?)"/>'s re-release after a successful
    /// wait). <see cref="ApplicationException"/> — the documented exception thrown when
    /// the calling thread does not own the mutex — is swallowed here so release can
    /// never throw out of a <c>using</c> block in <c>Main()</c>.
    /// </summary>
    private static void ReleaseIgnoringOwnershipError(Mutex mutex)
    {
        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Documented outcome when the calling thread does not own the mutex --
            // must never throw out of Dispose()/Main()'s using block.
        }
    }
}
