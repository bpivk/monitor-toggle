using System.Threading;

namespace RigToggle.Core;

/// <summary>
/// INSTANCE-01: a cross-process, OS-kernel single-instance primitive built on a named
/// <see cref="Mutex"/>. This is deliberately NOT the same mechanism as
/// <see cref="ToggleOrchestrator"/>'s in-process <c>_busy</c>
/// <c>Interlocked.CompareExchange</c> reentrancy flag, and shares no field, method, or
/// type with it (PITFALLS.md Pitfall 7): <c>_busy</c> is process-local memory with zero
/// visibility into a second, separately-launched OS process, which by definition starts
/// with its own fresh copy of every in-process field. Single-instance detection needs a
/// kernel object visible across process boundaries on the same login session; reentrancy
/// guarding needs only a shared memory flag inside one process. Conflating the two would
/// silently fail to detect a genuine second process while looking superficially correct.
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
/// <see cref="AbandonedMutexException"/>, which this class's constructor path (a plain
/// <c>new Mutex(...)</c>, not a <c>WaitOne</c>) never surfaces in the first place.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>
    /// Fixed, hardcoded GUID identifying this application's single-instance mutex name.
    /// Never derive this value — see the class doc comment for why.
    /// </summary>
    public const string InstanceId = "8f3a1c42-7b5e-4d19-9a06-2e5c1f8b7d34";

    private readonly Mutex _mutex;
    private int _disposed;

    private SingleInstanceGuard(Mutex mutex, bool isPrimaryInstance, string mutexName)
    {
        _mutex = mutex;
        IsPrimaryInstance = isPrimaryInstance;
        MutexName = mutexName;
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
    /// Acquires (or discovers) the named cross-process mutex identifying this
    /// application. <c>initiallyOwned: true</c> means the current thread requests
    /// ownership atomically as part of construction — if this call returns with
    /// <c>createdNew == true</c>, this process now owns the mutex without a separate
    /// <c>WaitOne()</c> call.
    /// </summary>
    public static SingleInstanceGuard Acquire()
    {
        string mutexName = @"Global\RigToggle-" + InstanceId;
        var mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);
        return new SingleInstanceGuard(mutex, createdNew, mutexName);
    }

    /// <summary>
    /// Releases mutex ownership (only if this instance actually owns it — a
    /// non-primary guard never called <c>WaitOne</c> and must not attempt release) and
    /// disposes the underlying handle. Guarded against double-dispose with
    /// <see cref="Interlocked.Exchange(ref int, int)"/>, mirroring
    /// <see cref="ToggleOrchestrator"/>'s nested
    /// <c>ExclusiveMonitorAccessLease.Dispose()</c> shape — the release effect can only
    /// ever run once per instance, regardless of how many times <see cref="Dispose"/>
    /// is called. <see cref="ApplicationException"/> — the documented exception thrown
    /// when the calling thread does not own the mutex — is swallowed here so disposal
    /// can never throw out of a <c>using</c> block in <c>Main()</c>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (IsPrimaryInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Documented outcome when the calling thread does not own the mutex --
                // must never throw out of Dispose()/Main()'s using block.
            }
        }

        _mutex.Dispose();
    }
}
