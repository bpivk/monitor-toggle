---
phase: 25-single-instance-guard
reviewed: 2026-08-21T00:00:00Z
depth: standard
files_reviewed: 11
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/UpdateApplyEntryPoint.cs
  - src/RigToggle.Core/SingleInstanceGuard.cs
  - src/RigToggle.Core/StartupArgs.cs
  - src/RigToggle.Tests/SingleInstanceGuardTests.cs
  - src/RigToggle.Tests/StartupArgsTests.cs
  - src/RigToggle.Windows/ActivationSignal.cs
  - src/RigToggle.Windows/NativeMethods.cs
  - src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj
  - src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs
findings:
  critical: 0
  warning: 2
  info: 1
  total: 3
status: issues_found
---

# Phase 25: Code Review Report (Re-Review After 25-04 Gap Closure)

**Reviewed:** 2026-08-21
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

This is a re-review of the single-instance guard implementation after plan 25-04 landed
two targeted fixes for the prior review's CR-01 and WR-02 findings. Both were verified
directly against current source (not trusted from the prior review's characterization)
and against the two 25-04 commits (`eb30b78`, `9d555f8`):

- **CR-01 (closed, confirmed):** `SingleInstanceGuard.WaitForInstanceReady` (SingleInstanceGuard.cs:330-350)
  now wraps `readyMutex.WaitOne(remaining)` in `try/catch (AbandonedMutexException)`,
  treats the abandoned-but-transferred wait as `acquired = true`, and routes into the
  existing release branch that clears the abandoned state for the next waiter. The
  regression test `WaitForInstanceReady_PrimaryAbandonedReadinessMutex_ReturnsTrueWithoutThrowing`
  (SingleInstanceGuardTests.cs:226-280) genuinely reproduces abandonment via an owning
  thread exiting without releasing (not `Dispose()`, which would prove nothing), and
  asserts both "no `AbandonedMutexException` escapes" and "the wait reports true." This
  is a real fix, not a cosmetic one — traced end to end, no gaps found.

- **WR-02 (closed, confirmed):** `Acquire()`'s readiness-mutex construction
  (SingleInstanceGuard.cs:236-247) is now wrapped in `try/catch (Exception ex)`,
  degrading to a logged `null` handle instead of letting construction failure escape
  `Acquire()` (which previously would have crashed startup with zero diagnostic trail
  and leaked the already-owned main mutex handle). Every downstream consumer of a
  possibly-null `_readyMutex` (`MarkReady()`, `Dispose()`, `WaitForInstanceReady`'s
  fail-fast path, `Program.cs`'s unconditional broadcast) already null-guards correctly,
  and existing tests (`MarkReady_OnNonPrimaryGuard_DoesNotThrow`,
  `WaitForInstanceReady_NoInstancePublished_ReturnsFalseQuickly`) already exercise that
  degraded state. Confirmed closed.

- **WR-01 (deferred, honestly represented):** The class doc comment
  (SingleInstanceGuard.cs:60-74) accurately describes the accepted, unfixed limitation —
  two processes whose tokens disagree about `Global\` access can each conclude they are
  primary — states the accepted risk level (ASVS L1, single-user rig), names the trigger,
  and names the deferred stronger fix (probe-before-conclude via `TryOpenExisting`)
  without claiming it was implemented. No contradiction between the comment and the
  code: `IsGlobalScope` is still resolved once per process with no cross-process probe.
  This is a genuine, non-silent deferral, not a dropped fix.

No new Critical findings. Two Warnings and one Info item below are new observations
from this pass, independent of the CR-01/WR-01/WR-02 verification above.

## Warnings

### WR-01: `Acquire()`'s `Local\` fallback construction is unguarded, contradicting its own doc comment's "never propagate out of Main()" claim

**File:** `src/RigToggle.Core/SingleInstanceGuard.cs:194-203`
**Issue:** The class doc comment (lines 55-58) and the method doc comment (lines 167-171)
both assert that the `Global\`→`Local\` fallback means startup failure here "falls back
to a session-local (`Local\`) scope rather than propagating out of `Main()`." The code
only delivers on that promise for the *first* attempt:

```csharp
try
{
    mutex = new Mutex(initiallyOwned: true, mutexName, out createdNew);
}
catch (UnauthorizedAccessException)
{
    isGlobalScope = false;
    mutexName = BuildName("RigToggle-" + effectiveInstanceId, isGlobalScope);
    mutex = new Mutex(initiallyOwned: true, mutexName, out createdNew);   // <-- unguarded
}
```

If the second (`Local\`) construction also throws — `UnauthorizedAccessException` again,
or any other exception type (e.g. a name-collision `WaitHandleCannotBeOpenedException` if
another kernel object already holds that exact name) — it propagates straight out of
`Acquire()`. `Program.cs` calls `SingleInstanceGuard.Acquire()` with no surrounding
try/catch (by design — the main mutex path is documented as "correctness-critical...
not best-effort"), so this becomes an unhandled exception that crashes the app on
startup with zero diagnostic trail (the crash happens before any `TryLog` call in this
method can run) — the exact failure mode WR-02 was just fixed to prevent for the
readiness mutex. `Local\` construction failing is extremely unlikely on an interactive,
non-elevated Windows session (this app's only supported context per CLAUDE.md), which is
presumably why this was accepted, but the doc comment currently overstates what the code
guarantees.
**Fix:** Either wrap the `Local\` fallback construction in its own guard (matching the
posture WR-02 just established for the readiness mutex) and decide what "guard
acquisition genuinely failed" should mean for `Program.cs` (e.g. treat as non-primary
and broadcast-only, or let it propagate but say so explicitly in the doc), or narrow the
doc comment to state the true, weaker guarantee ("recovers from the single documented
`Global\`-namespace-denied case; a second failure in `Local\` still propagates").
Leaving the code as-is without correcting the comment risks a future reader trusting a
guarantee the code does not provide.

### WR-02: `SingleInstanceProcessTests.TightRaceLaunch_ExactlyOneProcessSurvives` can throw an unhandled exception instead of a clean assertion failure on a benign race

**File:** `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs:305-327`
**Issue:** After the polling loop confirms `aliveCount <= 1`, the test re-queries
`Process.GetProcessesByName(AppProcessName)` and then calls
`KillAndConfirmExit(survivors[0])` (line 315) with no exception guard. `Dispose()`
(lines 153-178) — which performs the structurally identical "kill a process this file
started" operation — explicitly wraps the same kind of call in
`catch (InvalidOperationException)` / `catch (Win32Exception)` because a process can
legitimately exit between the last liveness check and the `Kill()` call. This call site
lacks that guard: if the "survivor" identified by the fresh `GetProcessesByName` query
happens to exit in the small window before `KillAndConfirmExit` runs (e.g. it crashes
shortly after winning the single-instance race), `process.Kill()` inside
`KillAndConfirmExit` throws an unguarded `InvalidOperationException`/`Win32Exception`,
failing the test with a confusing low-level exception instead of the descriptive
`Assert.True` messages this file otherwise favors everywhere else.
**Fix:** Wrap the `KillAndConfirmExit(survivor)` call (and ideally extract the
try/catch pattern already used in `Dispose()` into a shared helper) so an
already-exited survivor is treated as "confirmed exited," not an unhandled failure:
```csharp
try
{
    KillAndConfirmExit(survivor);
}
catch (InvalidOperationException) { /* already exited between check and kill */ }
catch (Win32Exception) { /* already exiting/exited at the OS level */ }
```

## Info

### IN-01: `ActivationSignal.MessageId`'s lazily-initialized cache is not thread-safe

**File:** `src/RigToggle.Windows/ActivationSignal.cs:75-82`
**Issue:** `public static uint MessageId => _messageId ??= NativeMethods.RegisterWindowMessage(MessageName);`
is not synchronized. If two threads race on first access (unlikely in this app's normal
single-UI-thread usage, but `MessageId` is a public static member reachable from any
caller, e.g. a future background thread), both could call `RegisterWindowMessage`
concurrently. This is low-impact in practice — `RegisterWindowMessage` is idempotent
(the OS returns the same id for the same string every time), so a torn race merely
means the call runs twice, not that an incorrect id gets cached — but it is a
data race on a shared field by the strict definition, and worth a one-line
acknowledgment or a `lock`/`Interlocked` for defensiveness given how central this id
is to the whole activation-broadcast mechanism.
**Fix:** Low priority; either leave as-is with a short comment noting the race is
benign (idempotent OS call), or use `LazyInitializer.EnsureInitialized(ref _messageId, ...)`
for a fully race-free cache if this codebase wants zero data races as a hard rule.

---

_Reviewed: 2026-08-21_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
