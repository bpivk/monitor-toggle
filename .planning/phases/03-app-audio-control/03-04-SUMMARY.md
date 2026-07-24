---
phase: 03-app-audio-control
plan: 04
subsystem: audio
tags: [naudio, com-interop, ipolicyconfig, xunit, tdd, restore, error-isolation]

# Dependency graph
requires:
  - phase: 03-app-audio-control (waves 1-3)
    provides: WindowsAudioController (IPolicyConfig/NAudio-backed audio control), ToggleService orchestration, FakeAudioController test double
provides:
  - Stale-DeviceId detection in WindowsAudioController.Restore (falls through to friendly-name match instead of throwing)
  - Per-role isolation in Restore's foreach (one role's ApplyAndVerify failure no longer aborts the other two roles)
  - Exception isolation in ToggleService.ToggleToNormalMode (restore failures never block MinimizeIfRunning/snapshot Clear)
  - FakeAudioController.throwOnRestore test double capability for simulating restore failure
  - Regression test ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows
affects: [phase-04-packaging-elevation, phase-05-orchestration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-role try/catch isolation inside a restore loop: one role's failure must not abort the loop — matches the existing per-role capture isolation already used in CaptureState"
    - "Restore-block exception isolation in orchestration methods: wrap only the risky/best-effort restore calls in try/catch, keep unconditional cleanup (MinimizeIfRunning, snapshot Clear) lexically outside/after the try/catch so it always runs"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsAudioController.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/Doubles/FakeControllers.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs

key-decisions:
  - "Stale-ID handling reuses the existing TryResolveDevice + friendly-name fallback rather than duplicating name-match logic — a stale ID is made indistinguishable from a never-captured ID"
  - "Per-role catch in Restore is scoped to InvalidOperationException only (the type ApplyAndVerify throws), while ToggleToNormalMode's outer catch is broader (Exception) since it must also absorb whatever the monitor controller's Restore might throw"
  - "Forward-mode SetDefault/SetDefaultForAllRoles/ApplyAndVerify verify-and-throw contract (D-03/D-04) is left completely untouched — the new catches live only in Restore's loop and ToggleToNormalMode's restore block"

patterns-established:
  - "Gap-closure plans (gap_closure: true) target a single verified defect from a prior wave's VERIFICATION/REVIEW doc and carry threat_model entries referencing the specific threat ID being closed (T-03-04-01/02)"

requirements-completed: [AUDIO-02, APP-03]

# Metrics
duration: 15min
completed: 2026-07-24
---

# Phase 3 Plan 4: Restore Stale-Device Fallback & Toggle-Back Self-Recovery Summary

**Closed the stuck-in-Rig-mode gap: `WindowsAudioController.Restore` now falls through to friendly-name matching for a present-but-stale `DeviceId`, isolates each audio role's apply/verify so one role's failure doesn't abort the others, and `ToggleService.ToggleToNormalMode` now always reaches `MinimizeIfRunning`/`snapshotStore.Clear()` even when restore throws.**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-07-24
- **Tasks:** 2 completed
- **Files modified:** 4

## Accomplishments
- A captured audio `DeviceId` that no longer resolves to a live device (unplugged/replaced since capture) is now treated exactly like a never-captured ID — it falls through to the existing friendly-name match instead of reaching `ApplyAndVerify` and throwing.
- Each of the three audio roles restored in `WindowsAudioController.Restore` is isolated in its own `try/catch (InvalidOperationException)` — a failure on one role no longer prevents the other two from being restored.
- `ToggleService.ToggleToNormalMode` wraps its monitor+audio restore calls in a non-rethrowing `try/catch (Exception)`, guaranteeing `MinimizeIfRunning` (APP-03) and `_snapshotStore.Clear()` always execute afterward — `IsInRigMode()` reliably flips back to `false` even after a restore failure, closing the "permanently stuck in Rig mode" defect from 03-VERIFICATION.md / 03-REVIEW.md CR-01.
- Added a regression test (`ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows`) proving the fix via a new `FakeAudioController.throwOnRestore` test-double capability, following the RED → GREEN TDD sequence.
- Forward-mode `ToggleToRigMode` / `SetDefault` / `SetDefaultForAllRoles` / `ApplyAndVerify` verify-and-throw behavior (D-03/D-04) is completely unchanged — confirmed by manual read and by grep for `throw new InvalidOperationException` and `SetDefaultEndpoint` still present unmodified.

## Task Commits

Each task was committed atomically (Task 2 followed TDD RED → GREEN):

1. **Task 1: Add stale-ID detection and per-role isolation to WindowsAudioController.Restore** - `6effd28` (fix)
2. **Task 2 (RED): Failing regression test for restore-throw stuck-in-rig-mode bug** - `f71709f` (test)
3. **Task 2 (GREEN): Isolate restore failures in ToggleToNormalMode** - `9443ffe` (feat)

_Note: Task 2 used the TDD RED → GREEN cycle (no REFACTOR commit needed — implementation was already minimal)._

## Files Created/Modified
- `src/RigToggle.Windows/WindowsAudioController.cs` - `Restore` now nulls a stale (unresolvable) `DeviceId` via `TryResolveDevice` before falling through to friendly-name match; each role's `ApplyAndVerify` call is wrapped in its own `try/catch (InvalidOperationException)` for per-role isolation. `ApplyAndVerify`/`SetDefault`/`SetDefaultForAllRoles` untouched.
- `src/RigToggle.Core/ToggleService.cs` - `ToggleToNormalMode`'s monitor+audio restore calls are wrapped in a non-rethrowing `try/catch (Exception)`; `MinimizeIfRunning` and `_snapshotStore.Clear()` remain lexically outside/after the try/catch so they always run. `ToggleToRigMode` untouched.
- `src/RigToggle.Tests/Doubles/FakeControllers.cs` - `FakeAudioController` gains an opt-in `throwOnRestore` constructor parameter (default `false`); when `true`, `Restore` logs then throws `InvalidOperationException`, simulating a role whose device is gone.
- `src/RigToggle.Tests/ToggleServiceTests.cs` - `CreateService` gains an `audioThrowsOnRestore` param; new fact `ToggleToNormalMode_StillMinimizesAndClears_WhenAudioRestoreThrows` proves minimize + clear survive a throwing restore and `IsInRigMode()` still flips to `false`.

## Decisions Made
- Reused the existing `TryResolveDevice` + friendly-name-match machinery for stale-ID handling rather than adding a parallel code path — a stale ID and a never-captured ID now hit the exact same fallback branch.
- Kept the per-role catch in `Restore` scoped to `InvalidOperationException` (the specific type `ApplyAndVerify` throws) to avoid accidentally swallowing unrelated bugs, while `ToggleToNormalMode`'s outer catch is the broader `Exception` since it must also cover whatever `IMonitorController.Restore` might throw.
- Confirmed via manual read (not just grep) that `MinimizeIfRunning` and `_snapshotStore.Clear()` sit lexically outside and after the new try/catch block in `ToggleToNormalMode`, and that `ToggleToRigMode`/`ApplyAndVerify`/`SetDefault` received no new try/catch — preserving D-03/D-04's forward-path verify-and-throw contract exactly as required by the plan's scope guard.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `<action>` specs precisely; no architectural changes, no new dependencies, no out-of-scope fixes needed.

## Issues Encountered

`dotnet` is not installed in this Linux execution sandbox, so `dotnet build` and `dotnet test src/RigToggle.Tests --filter "FullyQualifiedName~ToggleServiceTests"` (the plan's Windows-dev-machine verification gates) could not be executed here. This matches the plan's own `<verification>` section, which explicitly splits sandbox-runnable grep gates (all of which pass — see below) from the Windows dev-machine `dotnet build`/`dotnet test` gate. RED/GREEN state for the TDD task was confirmed by code inspection (the new test would hit an uncaught `InvalidOperationException` from `FakeAudioController.Restore` before the fix, since `ToggleToNormalMode` had no try/catch; after the fix, the exception is caught and the assertions on `MinimizeIfRunning`/`snapshot.Clear`/`IsInRigMode()` hold). **`dotnet build` and `dotnet test --filter ToggleServiceTests` (all 7 facts, 6 pre-existing + 1 new) must still be run on the Windows dev machine to close out the plan's full verification.**

Grep gates executed and passed in this sandbox:
- `TryResolveDevice(deviceId)` present inside `Restore`
- `catch (InvalidOperationException)` count = 1 in `WindowsAudioController.cs`
- `throw new InvalidOperationException` still present (x2) and `SetDefaultEndpoint` still present (x4) in `WindowsAudioController.cs` — forward path unchanged
- `throwOnRestore` count = 4 in `FakeControllers.cs`
- `catch (Exception)` present in `ToggleService.cs`
- `StillMinimizesAndClears_WhenAudioRestoreThrows` present in `ToggleServiceTests.cs`

## TDD Gate Compliance

Task 2 (`tdd="true"`) followed the RED → GREEN gate sequence, confirmed in git log:
- RED: `f71709f test(03-04): add failing regression test for restore-throw stuck-in-rig-mode bug`
- GREEN: `9443ffe feat(03-04): isolate restore failures so ToggleToNormalMode always self-recovers` (committed after RED)
- No REFACTOR commit was needed — the GREEN implementation was already minimal (a single try/catch wrapping two existing calls).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- The stuck-in-Rig-mode gap identified in 03-VERIFICATION.md / 03-REVIEW.md CR-01 is closed at the code level; `dotnet build` + `dotnet test --filter ToggleServiceTests` should be run on the Windows dev machine before this phase is considered fully verified.
- No blockers for Phase 4 (packaging/elevation) — this plan touched only `RigToggle.Windows`/`RigToggle.Core`/`RigToggle.Tests`, no packaging or elevation surface.

---
*Phase: 03-app-audio-control*
*Completed: 2026-07-24*
