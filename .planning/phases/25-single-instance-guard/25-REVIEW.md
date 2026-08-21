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
  critical: 1
  warning: 2
  info: 1
  total: 4
status: issues_found
---

# Phase 25: Code Review Report

**Reviewed:** 2026-08-21T00:00:00Z
**Depth:** standard
**Files Reviewed:** 11
**Status:** issues_found

## Summary

Reviewed the single-instance guard mechanism (`SingleInstanceGuard`, `ActivationSignal`,
`StartupArgs`, `UpdateApplyEntryPoint`), its wiring into `Program.cs`/`MainForm.cs`
(activation-broadcast `WndProc` branch, `RestoreAndFocus`), the supporting P/Invoke
surface (`NativeMethods`), and the accompanying unit/process tests.

The design is unusually well-documented and most of the documented edge cases (Pitfall
8 tight-race, foreground-activation heuristic, cross-assembly test-name collisions,
`Global\` vs `Local\` fallback) are genuinely handled and covered by tests. However, one
genuine crash-causing gap survives: `WaitForInstanceReady`'s call to
`Mutex.WaitOne(TimeSpan)` on the readiness mutex is not guarded against
`AbandonedMutexException`, and the exact real-world trigger condition (a primary
process dying mid-startup while a duplicate is concurrently blocked waiting for
readiness) is the same class of failure this codebase's own "25-03 Task 3" debug
history proves actually happens on real hardware. This is the review's one BLOCKER.

Two further correctness gaps were found around the `Global\`/`Local\` namespace
fallback in `SingleInstanceGuard.Acquire()`: the fallback resolution can silently
diverge between two processes with different security contexts (defeating the
single-instance guarantee itself), and the readiness-mutex construction that follows
the main-mutex fallback has no equivalent retry/guard of its own.

## Critical Issues

### CR-01: `WaitForInstanceReady` crashes the duplicate process on an abandoned readiness mutex

**File:** `src/RigToggle.Core/SingleInstanceGuard.cs:270-285` (call site: `src/RigToggle.App/Program.cs:247`)

**Issue:** `WaitForInstanceReady` calls `readyMutex.WaitOne(remaining)` directly, with no
try/catch for `System.Threading.AbandonedMutexException`. The class's own doc comment
(lines 26-31) correctly notes that the *main* single-instance mutex can never surface
this exception because it is acquired via the `new Mutex(...)` constructor path, not
`WaitOne`. But the *readiness* mutex is a completely separate object, and it genuinely
is waited on via `WaitOne` here — the one call site in this file that is subject to
`AbandonedMutexException`, and the doc comment's blanket "no abandoned-mutex risk"
claim does not actually cover it.

Concrete trigger: instance A wins the main-mutex race and creates+holds the readiness
mutex (`Acquire()`, lines 186-192) before it has called `MarkReady()`. If A terminates
abnormally in that window — a crash, a kill, an unhandled exception during
`ApplicationConfiguration.Initialize()`/`StartupRecoveryChecker.Run`/`MainForm`
construction/`InitializeTrayState()`/`RegisterHotkeyAtStartup()`, all of which run
between `Acquire()` and `MarkReady()` in `Program.cs` — while instance B is concurrently
blocked in `WaitForInstanceReady`'s `WaitOne` call (exactly the "Pitfall 8 tight race"
scenario this codebase already has dedicated tests for, `TightRaceLaunch_ExactlyOneProcessSurvives`),
the OS marks the readiness mutex abandoned. B's `WaitOne` call then throws
`AbandonedMutexException` instead of returning `true`.

That exception is not caught anywhere: not in `WaitForInstanceReady`, and not at its
only production call site in `Program.cs:247`
(`bool becameReady = SingleInstanceGuard.WaitForInstanceReady(...)`, unlike the
`TryLog` calls immediately around it, which are individually try/caught). There is also
no `AppDomain.UnhandledException`/`Application.ThreadException` handler registered
anywhere in `RigToggle.App` to catch it as a last resort (verified: no matches for
`UnhandledException`/`ThreadException` in the App project). The result: the duplicate
process crashes with an unhandled exception instead of gracefully broadcasting
activation and exiting — precisely the "a real duplicate launch look[s] like a broken
restore path" failure class the 25-03 investigation comments elsewhere in this same
file/`Program.cs` were written to prevent, just via a different code path.

`SingleInstanceGuardTests`/`SingleInstanceProcessTests` do not cover this path (they
only ever exercise a graceful `MarkReady()`-then-wait sequence), so nothing in CI would
catch a regression here.

**Fix:**
```csharp
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
        // The primary died after creating the readiness mutex but before calling
        // MarkReady()/disposing cleanly. This wait still succeeded in acquiring
        // ownership -- treat it exactly like a normal successful wait rather than
        // letting the exception crash this (duplicate) process.
        acquired = true;
    }

    if (acquired)
    {
        ReleaseIgnoringOwnershipError(readyMutex);
    }

    TryLog($"WaitForInstanceReady: readiness mutex opened, WaitOne(remaining={remaining}) returned {acquired}.");
    return acquired;
}
```
Consider also adding a `SingleInstanceGuardTests` case that acquires the readiness
mutex, disposes the guard object *without* calling `MarkReady()` first (simulating an
abandoned mutex), and asserts `WaitForInstanceReady` still returns `true` rather than
throwing.

## Warnings

### WR-01: `Global\`/`Local\` namespace resolution can diverge between two processes, silently defeating single-instance detection

**File:** `src/RigToggle.Core/SingleInstanceGuard.cs:162-197`

**Issue:** `Acquire()` resolves `isGlobalScope` independently, per-process: it tries
`Global\RigToggle-{id}` first and only falls back to `Local\RigToggle-{id}` if creating
or opening the `Global\` name throws `UnauthorizedAccessException` for *this* process's
token. If two Rig Toggle processes run under security contexts that disagree on
`Global\` access — e.g. one launched normally and a second launched via "Run as
Administrator" against an object whose DACL doesn't grant the other integrity level
access, or two different login/RDP sessions — the second process's `Global\` attempt
can throw while the first process's already succeeded. The second process then falls
back to `Local\RigToggle-{id}`, a namespace the first process never touched, and
`createdNew` comes back `true` there: process B genuinely becomes a *second* primary
instance, fully bypassing the single-instance guarantee this whole phase exists to
provide. Both processes then independently mutate the same on-disk `mode.json`/
`settings.json`/monitor and audio state with no coordination.

The class doc comment (lines 33-59) discusses this fallback purely from the
single-process resilience angle ("this token cannot create or open a name … falls back
… rather than propagating out of `Main()`") and never addresses the cross-process
consistency risk that two independently-resolved scopes create. No test in
`SingleInstanceGuardTests`/`SingleInstanceProcessTests` exercises mismatched security
contexts (both test scaffolds run same-session, same-token processes only), so this gap
is unverified either way.

**Fix:** At minimum, document this as a known, accepted limitation next to the
`Global\`/`Local\` fallback (the current doc comment reads as though the fallback is
fully safe). A stronger fix: have the *loser* path in `WaitForInstanceReady`/
`Acquire()`'s failure branch also attempt to detect a same-user primary in the other
namespace before concluding it is primary (e.g. via `Mutex.TryOpenExisting` against the
opposite scope before creating a new one), so a scope mismatch degrades to "can't tell,
assume duplicate and broadcast" rather than "assume primary."

### WR-02: Readiness-mutex construction has no fallback/guard of its own

**File:** `src/RigToggle.Core/SingleInstanceGuard.cs:186-192`

**Issue:** After the main mutex's `Global\`→`Local\` fallback resolves `isGlobalScope`,
the readiness mutex is constructed unconditionally in that same scope with no
try/catch:
```csharp
Mutex? readyMutex = createdNew
    ? new Mutex(initiallyOwned: true, readyEventName, out _)
    : null;
```
If this throws for any reason (e.g. an asymmetric permission failure between the main
mutex name and the readiness name, or any other `Mutex` construction failure), the
exception propagates straight out of the static `Acquire()` method — before the
`TryLog("Acquire: ...")` line is ever reached, so there is zero diagnostic trail — and
also before a `SingleInstanceGuard` instance exists to dispose. The already-created
main mutex handle (`mutex`, which this process now legitimately owns as primary) is
never released or disposed on this path, and `Program.cs`'s `using var guard =
SingleInstanceGuard.Acquire();` line has no surrounding try/catch (deliberately, per
its own comment, but that comment's rationale is about a *live competing primary*, not
an unexpected construction failure), so the whole app would crash on startup with no
log line explaining why.

**Fix:** Wrap the readiness-mutex construction in the same defensive pattern already
used for the main mutex, e.g. falling back to a null `readyMutex` (degrading to
"no readiness signal published, losers fall back to the retry-broadcast tolerance
already built into `ActivationSignal`") rather than letting an exception escape
`Acquire()` entirely:
```csharp
Mutex? readyMutex = null;
if (createdNew)
{
    try
    {
        readyMutex = new Mutex(initiallyOwned: true, readyEventName, out _);
    }
    catch (Exception ex)
    {
        TryLog($"Acquire: failed to create readiness mutex '{readyEventName}': {ex}. Readiness will never be signalled by this instance.");
    }
}
```

## Info

### IN-01: `UpdateApplyEntryPoint`'s doc comment overstates ordering relative to `Program.Main()`

**File:** `src/RigToggle.App/UpdateApplyEntryPoint.cs:8-9`

**Issue:** The doc comment states the bypass is "Reached only from the very first
branch of Program.Main(), before … any other bootstrap step," but `Program.cs`
actually runs `Application.SetColorMode(...)` and `ApplicationConfiguration.Initialize()`
(including its own `SystemEvents` background-thread spin-up, documented at length
elsewhere in the same file) before the `--apply-update` check at line 116. `Program.cs`'s
own comment is more accurate ("Main()'s very first branch *after the two
position-sensitive calls above*"). The load-bearing property (before the
single-instance guard, before settings/mode-store bootstrap) still holds either way, so
this is documentation-only, but the inconsistency between the two files' doc comments
could mislead a future editor into believing `UpdateApplyEntryPoint.Run` is reached with
zero prior process state, when `SetColorMode`'s `SystemEvents` subscription has already
run by that point.

**Fix:** Reword `UpdateApplyEntryPoint.cs`'s doc comment to match `Program.cs`'s more
precise phrasing ("the first branch after the two position-sensitive `SetColorMode`/
`ApplicationConfiguration.Initialize()` calls, and strictly before the single-instance
guard and all other bootstrap steps").

---

_Reviewed: 2026-08-21T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
