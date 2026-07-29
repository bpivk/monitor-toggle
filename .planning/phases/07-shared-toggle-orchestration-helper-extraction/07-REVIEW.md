---
phase: 07-shared-toggle-orchestration-helper-extraction
reviewed: 2026-07-29T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.Core/ToggleInProgressException.cs
  - src/RigToggle.Core/ToggleOrchestrator.cs
  - src/RigToggle.Tests/Doubles/BlockingMonitorController.cs
  - src/RigToggle.Tests/ToggleOrchestratorTests.cs
findings:
  critical: 0
  warning: 3
  info: 3
  total: 6
status: issues_found
---

# Phase 07: Code Review Report

**Reviewed:** 2026-07-29T00:00:00Z
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Reviewed the extraction of `ToggleOrchestrator` (a non-blocking, `Interlocked.CompareExchange`-based single-flight guard wrapping `ToggleService`), its `ToggleInProgressException`, the `MainForm`/`Program.cs` wiring that routes the GUI through the new orchestrator, and the accompanying `BlockingMonitorController` test double plus `ToggleOrchestratorTests`.

The core concurrency primitive is sound: the busy-flag is set and checked atomically via `CompareExchange`, always released in a `finally` (so a preflight `InvalidOperationException` from `ToggleService` cannot permanently wedge the app), and correctly guards both directions with one shared flag. I traced the flag's lifecycle against `ToggleService.ToggleToRigMode`/`ToggleToNormalMode` (not in this review's file set, but load-bearing for correctness) and found no window where the guard is bypassed. I also confirmed `ToggleOrchestrator` is instantiated exactly once in `Program.cs` and shared as a singleton by `MainForm` — required for the flag to mean anything across calls.

No BLOCKER-tier defects were found. The findings below are quality/robustness gaps: a misleading error-dialog reuse for the (currently unreachable, but intentionally forward-looking) busy-rejection path, an inconsistency with the codebase's own "trace every failure" convention, and a test-reliability gap (unbounded waits with no timeout, so a future regression in the guard would hang the test run instead of failing fast).

One structural note worth flagging explicitly: under today's single-threaded WinForms `BtnToggle_Click` (no `async`/`await`, no nested message pump between the confirm dialog closing and the blocking `_orchestrator.Toggle...Mode()` call), `ToggleInProgressException` cannot actually be thrown by the one production caller that exists today — the UI thread cannot dispatch a second click while the first is still executing synchronously. The guard is real infrastructure for Phase 8+ (tray/hotkey/CLI triggers on other threads/contexts), but it is currently exercised only by the unit tests, not by any live code path. This is not a bug, but it does mean the `MainForm` catch-block behavior for this specific exception type is unverified against the real UI runtime.

## Warnings

### WR-01: Busy-rejection reuses the generic "something went wrong" dialog, misleading the user about an expected condition

**File:** `src/RigToggle.App/MainForm.cs:143-161`
**Issue:** `ToggleInProgressException` (thrown when a second toggle is rejected because one is already in flight — an expected, non-error condition per CORE-06) falls into the same `catch (Exception ex)` block as genuine unexpected failures, producing: `"Something went wrong while toggling:\n\nToggleInProgressException: A toggle is already in progress. Wait for it to finish, then try again.\n\nTry again, or check Settings."` Telling the user "something went wrong... check Settings" for what is actually "you clicked too fast, please wait" is confusing messaging for a condition that isn't an error. This is documented as an intentional "zero UI changes" trade-off (D-05), but as multi-trigger surfaces are added in Phase 8+ (tray icon, hotkey — per the class doc on `ToggleOrchestrator`), this generic wording will become the user-facing behavior for a case that will actually occur in practice, not just in unit tests.
**Fix:** Add a dedicated branch before the generic catch (or a `catch (ToggleInProgressException)` before `catch (Exception ex)`) that shows a lighter-weight, accurate message, e.g.:
```csharp
catch (ToggleInProgressException ex)
{
    MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
catch (Exception ex)
{
    // existing generic handling
}
```

### WR-02: Monitor-name resolution failure is silently swallowed with no trace, breaking the codebase's own logging convention

**File:** `src/RigToggle.App/MainForm.cs:94-104`
**Issue:** `catch { allMonitors = Array.Empty<MonitorInfo>(); }` discards the exception from `_monitorController.GetAllMonitors()` with no `Trace.WriteLine`. `ToggleService` (called from these same code paths, e.g. `TryExecuteStep`, the monitor-restore catch, the audio-restore catch) explicitly traces every swallowed exception specifically so that a user running the self-contained .exe with `EnableDebugLogging` on has *something* to look at afterward (see `Program.cs:50-58` and the "IN-02" comments in `ToggleService.cs`). This one catch block breaks that convention: if `GetAllMonitors()` starts failing on a given machine, the confirmation dialog will silently degrade to raw device paths with zero diagnostic trail, even with debug logging enabled.
**Fix:**
```csharp
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"GetAllMonitors failed while resolving names for confirm dialog: {ex}");
    allMonitors = Array.Empty<MonitorInfo>();
}
```

### WR-03: Unbounded `ManualResetEventSlim.Wait()` calls risk hanging the test run instead of failing fast

**File:** `src/RigToggle.Tests/ToggleOrchestratorTests.cs:96, 116, 137` and `src/RigToggle.Tests/Doubles/BlockingMonitorController.cs:51`
**Issue:** All four reentrancy tests call `enteredGuardedRegion.Wait()` with no timeout, and `BlockingMonitorController.DeactivateMonitors` calls `_releaseFirstCall.Wait()` with no timeout either. Two independent hang scenarios exist:
1. If a future regression in `ToggleService` causes `ToggleToRigMode()` to throw *before* reaching the `Monitor` step (e.g. a broken preflight check), `_enteredGuardedRegion` is never signaled and the test-thread `Wait()` blocks forever — the test run hangs with no assertion failure to explain why.
2. In `ToggleToRigMode_RejectsSecondCallWhileFirstInFlight_SameDirection` and `ToggleToRigMode_InFlight_RejectsCrossDirectionToggleToNormalMode`, if the `Assert.Throws<ToggleInProgressException>(...)` assertion itself fails (i.e., the guard being tested is broken), the assertion exception propagates out of the test method *before* `releaseFirstCall.Set()` is reached. The background `Task.Run` thread is then left permanently blocked inside `DeactivateMonitors`, orphaning a thread-pool thread for the rest of the test process's life — exactly the scenario ("guard is broken") these tests exist to catch produces the worst possible failure mode (an indefinite hang) instead of a clear, fast assertion message.
**Fix:** Use a bounded wait and assert it succeeded, and/or wrap the release in `try/finally`:
```csharp
Assert.True(enteredGuardedRegion.Wait(TimeSpan.FromSeconds(5)), "Blocking step was never entered.");
try
{
    Assert.Throws<ToggleInProgressException>(() => orchestrator.ToggleToRigMode());
}
finally
{
    releaseFirstCall.Set();
}
var firstResult = firstCallTask.GetAwaiter().GetResult();
```

## Info

### IN-01: `ToggleInProgressException` omits the standard exception constructors

**File:** `src/RigToggle.Core/ToggleInProgressException.cs:11-14`
**Issue:** Only a single `(string message)` constructor is provided. The BCL/analyzer convention (CA1032) for a custom exception type is to also provide a parameterless constructor, a `(string message, Exception innerException)` constructor, and ideally a serialization constructor. None of these are strictly required for current usage, but their absence means the type can't wrap an inner exception (not needed today, but limits future reuse) and will trip `CA1032` if analyzers are ever enabled for `RigToggle.Core`.
**Fix:**
```csharp
public sealed class ToggleInProgressException : InvalidOperationException
{
    public ToggleInProgressException() { }
    public ToggleInProgressException(string message) : base(message) { }
    public ToggleInProgressException(string message, Exception innerException) : base(message, innerException) { }
}
```

### IN-02: `_settingsStore.Load()` is called twice per rig-mode toggle attempt

**File:** `src/RigToggle.App/MainForm.cs:67, 88`
**Issue:** `_orchestrator.IsSettingsConfigured()` internally calls `ToggleService.IsSettingsConfigured()` → `_settingsStore.Load()`, and then `MainForm` immediately calls `_settingsStore.Load()` again a few lines later to read `MonitorsToDisable`/`MonitorsToEnable`/`SkipMonitorConfirmation`. This is two redundant file reads (`JsonSettingsStore.Load()` does disk I/O) per toggle-to-rig click, and opens a (currently benign, single-user/single-thread) window where the two `Load()` calls could theoretically observe different settings if something else in the process mutated `settings.json` between them.
**Fix:** Load settings once and reuse the result:
```csharp
var settings = _settingsStore.Load();
if (!_orchestrator.IsSettingsConfigured()) { ... }
```
(would require exposing an `IsFullyConfigured(AppSettings)`-style overload, or simply inlining the check in `MainForm` against the already-loaded `settings`).

### IN-03: `ManualResetEventSlim` instances are never disposed in the reentrancy tests

**File:** `src/RigToggle.Tests/ToggleOrchestratorTests.cs:90-91, 110-111, 131-132`
**Issue:** `ManualResetEventSlim` implements `IDisposable`. Three test methods each allocate two instances (`enteredGuardedRegion`, `releaseFirstCall`) and never dispose them. Harmless in a short-lived test process, but inconsistent with proper resource cleanup and will be flagged by analyzers (CA2000) if enabled.
**Fix:** Wrap in `using` or call `.Dispose()` at the end of each test.

---

_Reviewed: 2026-07-29T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
