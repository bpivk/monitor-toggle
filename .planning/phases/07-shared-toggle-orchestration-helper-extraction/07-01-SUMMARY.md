---
phase: 07-shared-toggle-orchestration-helper-extraction
plan: 01
subsystem: core-orchestration
tags: [concurrency, reentrancy-guard, refactor, composition-root]
requires: []
provides:
  - ToggleOrchestrator
  - ToggleInProgressException
affects:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/Program.cs
tech-stack:
  added: []
  patterns:
    - "Interlocked.CompareExchange-based non-blocking single-flight guard (reject, not queue)"
    - "Dedicated preflight exception subclassing InvalidOperationException, caught for free by an existing generic catch block"
key-files:
  created:
    - src/RigToggle.Core/ToggleOrchestrator.cs
    - src/RigToggle.Core/ToggleInProgressException.cs
    - src/RigToggle.Tests/Doubles/BlockingMonitorController.cs
    - src/RigToggle.Tests/ToggleOrchestratorTests.cs
  modified:
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/Program.cs
decisions:
  - "Non-blocking Interlocked.CompareExchange busy-flag (D-01), not a blocking lock and not a queue — a second toggle request is rejected immediately, never serialized behind the first"
  - "One shared _busy flag guards both ToggleToRigMode and ToggleToNormalMode (D-02) — a rig-mode toggle in flight also rejects a normal-mode request and vice versa"
  - "ToggleInProgressException subclasses InvalidOperationException (D-05), matching ToggleService's existing preflight-exception precedent, so MainForm's existing catch (Exception ex) block surfaces it with zero UI changes"
  - "ToggleService remains completely untouched (D-03) — the guard lives entirely in the new ToggleOrchestrator wrapper layer"
metrics:
  duration_minutes: 40
  completed: "2026-07-29"
---

# Phase 07 Plan 01: Shared Toggle-Orchestration Helper Extraction Summary

A reentrancy-safe `ToggleOrchestrator` now wraps `ToggleService` with an `Interlocked.CompareExchange` busy-flag guard, rejecting any second toggle request that arrives while one is already in flight — same-direction or cross-direction — with a dedicated `ToggleInProgressException`, and `MainForm`/`Program.cs` are refactored to route every toggle call through it.

## What Was Built

- **`ToggleOrchestrator`** (`src/RigToggle.Core/ToggleOrchestrator.cs`): a sealed class composing `ToggleService`. `ToggleToRigMode()`/`ToggleToNormalMode()` are guarded via a private `RunGuarded` helper using `Interlocked.CompareExchange(ref _busy, 1, 0)` — a non-blocking atomic test-and-set. If the flag is already claimed, it throws `ToggleInProgressException` immediately (no waiting, no queueing). The flag is always released in a `finally` block, even when `ToggleService` throws its own preflight exceptions. `IsInRigMode()`/`IsSettingsConfigured()` are unguarded pass-throughs, safe to call at any time including mid-toggle.
- **`ToggleInProgressException`** (`src/RigToggle.Core/ToggleInProgressException.cs`): a sealed exception subclassing `InvalidOperationException`, mirroring `ToggleService`'s existing preflight-guard precedent so no new catch handling is needed anywhere.
- **`BlockingMonitorController`** (`src/RigToggle.Tests/Doubles/BlockingMonitorController.cs`): a test-only `IMonitorController` double whose `DeactivateMonitors` signals a `ManualResetEventSlim` on entry and blocks on a second one until released — enables fully deterministic reentrancy tests with no `Thread.Sleep` or timing guesses.
- **`ToggleOrchestratorTests`** (`src/RigToggle.Tests/ToggleOrchestratorTests.cs`): 7 tests covering idle delegation (both directions), same-direction reentrancy rejection, cross-direction reentrancy rejection (D-02), unguarded pass-through correctness while a toggle is in flight, flag-release-after-preflight-exception (proving the orchestrator never wedges), and the `ToggleInProgressException`-is-an-`InvalidOperationException` type contract.
- **`MainForm`/`Program.cs` refactor**: `MainForm`'s constructor now takes a `ToggleOrchestrator` instead of `ToggleService` (field renamed `_toggleService` → `_orchestrator`); all five existing call sites (`IsInRigMode()` x2, `ToggleToNormalMode()`, `IsSettingsConfigured()`, `ToggleToRigMode()`) redirect to the orchestrator. `Program.cs`'s composition root constructs `ToggleService` exactly as before, then wraps it in one new `ToggleOrchestrator` line before injecting it into `MainForm`. No other lines in either file changed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Doc-comment substrings produced false-positive grep gate failures**
- **Found during:** Task 1 verification
- **Issue:** The plan's acceptance criteria require `grep -c "lock ("` == 0 in `ToggleOrchestrator.cs` and `grep -c "Thread.Sleep"` == 0 in `ToggleOrchestratorTests.cs`. My initial XML-doc rationale comments included the phrase `finally` **block (**not after...`, whose substring `lock (` matched the grep pattern, and comments describing the tests as having "no Thread.Sleep" literally contained the forbidden string.
- **Fix:** Reworded both comments (`block (` → `clause — not placed after`; "no Thread.Sleep" → "no fixed-duration wait(s)") to preserve the same meaning without containing the grep-matched substrings.
- **Files modified:** `src/RigToggle.Core/ToggleOrchestrator.cs`, `src/RigToggle.Tests/ToggleOrchestratorTests.cs`
- **Commit:** `4c95689`

None beyond this — plan executed as written otherwise.

## Environment Constraint (matches Phase 6 precedent)

The executor sandbox has no `dotnet` SDK installed (`command -v dotnet` and common install paths all empty — confirmed identically to Phase 6's `06-01-SUMMARY.md`). Per that established precedent, verification was done via the plan's own grep-based `<acceptance_criteria>` source assertions instead of a live `dotnet build`/`dotnet test` run:

- `git diff --stat src/RigToggle.Core/ToggleService.cs` — empty (D-03: `ToggleService` byte-for-byte unchanged across both commits) — PASSED
- `grep -c "Interlocked.CompareExchange" src/RigToggle.Core/ToggleOrchestrator.cs` — 3 (>=1 required) — PASSED
- `grep -c "lock (" src/RigToggle.Core/ToggleOrchestrator.cs` — 0 — PASSED
- `grep -c "Thread.Sleep" src/RigToggle.Tests/ToggleOrchestratorTests.cs` — 0 — PASSED
- `grep -c "_toggleService" src/RigToggle.App/MainForm.cs` — 0 — PASSED
- `grep -c "ToggleOrchestrator" src/RigToggle.App/MainForm.cs` — 3 (>=2 required) — PASSED
- `grep -c "new ToggleOrchestrator" src/RigToggle.App/Program.cs` — 1 — PASSED
- `grep -c "catch (ToggleInProgressException" src/RigToggle.App/MainForm.cs` — 0 (no new catch branch added, per D-05) — PASSED
- `ToggleInProgressException : InvalidOperationException` — confirmed by direct source inspection

**Action required before Phase 7 is considered fully verified:** run `dotnet build` (whole solution) and `dotnet test` on a host with the .NET SDK (the Windows rig) to confirm the full suite — existing `ToggleServiceTests` plus the 7 new `ToggleOrchestratorTests` — actually compiles and passes, per this plan's `<verification>` section. All code was written and reviewed against the exact patterns drafted in `07-RESEARCH.md`/`07-PATTERNS.md` (which were themselves verified against official `Interlocked.CompareExchange` documentation), so confidence is high, but this has not been confirmed by an actual compiler/test-runner in this environment.

## Self-Check

- FOUND: src/RigToggle.Core/ToggleOrchestrator.cs
- FOUND: src/RigToggle.Core/ToggleInProgressException.cs
- FOUND: src/RigToggle.Tests/Doubles/BlockingMonitorController.cs
- FOUND: src/RigToggle.Tests/ToggleOrchestratorTests.cs
- FOUND: commit 4c95689 (Task 1 — ToggleOrchestrator, ToggleInProgressException, BlockingMonitorController, ToggleOrchestratorTests)
- FOUND: commit d911005 (Task 2 — MainForm/Program.cs routed through ToggleOrchestrator)

## Self-Check: PASSED
