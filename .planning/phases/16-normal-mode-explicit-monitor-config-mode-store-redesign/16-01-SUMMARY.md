---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
plan: 01
subsystem: infra
tags: [dotnet, system.text.json, persistence, mode-store, crash-recovery]

# Dependency graph
requires: []
provides:
  - "ToggleMode enum (Normal, Rig) — the explicit persisted current-mode value"
  - "ToggleInProgressMarker record (TargetMode, StartedAtUtc) — crash-detection payload"
  - "IModeStore / IToggleInProgressStore abstractions"
  - "JsonModeStore / JsonToggleInProgressStore atomic, degrade-to-null implementations"
  - "InMemoryModeStore / InMemoryToggleInProgressStore test doubles with shared call-log convention"
affects: [16-02, 16-03, 16-04, 16-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Atomic temp-file + File.Move(overwrite:true) JSON persistence (mirrors JsonSnapshotStore/JsonSettingsStore)"
    - "TryLoad() degrade-to-null on both JsonException and IOException (mirrors JsonSettingsStore's two-catch coverage)"
    - "Shared call-log convention for in-memory test doubles (mode.Save / marker.Save / marker.Clear)"

key-files:
  created:
    - src/RigToggle.Core/Models/ToggleMode.cs
    - src/RigToggle.Core/Models/ToggleInProgressMarker.cs
    - src/RigToggle.Core/Abstractions/IModeStore.cs
    - src/RigToggle.Core/Abstractions/IToggleInProgressStore.cs
    - src/RigToggle.Core/Persistence/JsonModeStore.cs
    - src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs
  modified:
    - src/RigToggle.Tests/Doubles/InMemoryStores.cs

key-decisions:
  - "ToggleMode.Normal listed first in the enum so a fresh install with no mode file seeds to Normal"
  - "ToggleInProgressMarker doc comment explicitly distinguishes it from the unrelated ToggleInProgressException reentrancy guard"
  - "JsonToggleInProgressStore.Exists() kept private (not part of IToggleInProgressStore's contract) — only used internally by Save/Clear"

patterns-established:
  - "New Core persistence primitives mirror ISnapshotStore/JsonSnapshotStore/InMemorySnapshotStore shapes exactly, per 16-PATTERNS.md"

requirements-completed: [DISPLAY-11, DISPLAY-13]

# Metrics
duration: ~15min
completed: 2026-08-05
---

# Phase 16 Plan 01: Mode-Store Persistence Primitives Summary

**New Core persistence primitives — `ToggleMode` enum, `ToggleInProgressMarker` record, `IModeStore`/`IToggleInProgressStore` contracts, their atomic JSON implementations, and matching in-memory test doubles — all mirroring the existing `ISnapshotStore`/`JsonSnapshotStore`/`InMemorySnapshotStore` shapes exactly.**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-08-05T05:12:42Z
- **Tasks:** 3 completed
- **Files modified:** 7 (6 created, 1 modified)

## Accomplishments
- `ToggleMode` enum and `ToggleInProgressMarker` record now exist in `RigToggle.Core.Models`, replacing the retired D-14 snapshot-presence mode inference with an explicit, file-backed value
- `IModeStore`/`IToggleInProgressStore` contracts and their `JsonModeStore`/`JsonToggleInProgressStore` implementations exist, use atomic temp-file + `File.Move(overwrite:true)` writes, and degrade corrupted files to `null` via both `JsonException` and `IOException` catches (T-16-01, T-16-02)
- `InMemoryModeStore`/`InMemoryToggleInProgressStore` test doubles exist using the shared call-log convention (`mode.Save`, `marker.Save`, `marker.Clear`), ready for Plan 03's `ToggleService`/`ToggleOrchestrator` rewrite tests
- Zero App-tier wiring introduced (confirmed via `grep -rn "IModeStore\|IToggleInProgressStore" src/RigToggle.App/` returning no matches) — wiring is deliberately deferred to Plan 04

## Task Commits

Each task was committed atomically:

1. **Task 1: Add ToggleMode enum and ToggleInProgressMarker record** - `e67d8b5` (feat)
2. **Task 2: Add IModeStore/IToggleInProgressStore contracts and their JSON implementations** - `6ad2822` (feat)
3. **Task 3: Add InMemoryModeStore and InMemoryToggleInProgressStore test doubles** - `a96f0d7` (test)

_Note: SUMMARY.md commit follows this list — this is a worktree-isolated parallel executor, so STATE.md/ROADMAP.md are excluded from the metadata commit and updated centrally by the orchestrator after merge._

## Files Created/Modified
- `src/RigToggle.Core/Models/ToggleMode.cs` - Two-value enum { Normal, Rig }, explicit persisted current-mode value
- `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` - Sealed record capturing target mode + start time for crash detection, with an explicit doc-comment distinguishing it from `ToggleInProgressException`
- `src/RigToggle.Core/Abstractions/IModeStore.cs` - `Exists()`/`TryLoad()`/`Save(ToggleMode)` persistence contract
- `src/RigToggle.Core/Abstractions/IToggleInProgressStore.cs` - `TryLoad()`/`Save(marker)`/`Clear()` crash-marker lifecycle contract
- `src/RigToggle.Core/Persistence/JsonModeStore.cs` - Atomic mode.json persistence, degrade-to-null on corruption (both JsonException and IOException)
- `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs` - Same atomic persistence pattern for toggle-in-progress.json, plus `Clear()`
- `src/RigToggle.Tests/Doubles/InMemoryStores.cs` - Added `InMemoryModeStore` and `InMemoryToggleInProgressStore` doubles alongside the existing (unmodified) `InMemorySnapshotStore`/`InMemorySettingsStore`

## Decisions Made
- Followed 16-PATTERNS.md's exact analogs (AppTheme.cs for the enum shape, ToggleStepResult.cs for the record idiom, JsonSnapshotStore.cs for the atomic-write skeleton, JsonSettingsStore.cs for the two-catch corruption coverage) — no deviation from the specified shapes was needed
- `JsonToggleInProgressStore` keeps its `Exists()` helper private since it is not part of the `IToggleInProgressStore` contract (matches `IModeStore`'s public `Exists()` vs. the marker interface's lifecycle-only surface, per the plan's own interface listing)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

`dotnet` was not on `PATH` in this environment (installed at `/home/bpivk/.dotnet/dotnet`); resolved by adding it to `PATH` for build/test verification commands. Not a code change, no deviation tracked.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `IModeStore`/`IToggleInProgressStore` contracts and their JSON + in-memory implementations are ready for Plan 03's `ToggleService` rewrite and Plan 04's `Program.cs` wiring to consume directly, with fixed contracts instead of needing discovery.
- `dotnet build` on both `RigToggle.Core` and `RigToggle.Tests` projects succeeds with 0 errors; full test suite (75 tests) passes with no regressions.
- No blockers or concerns for downstream plans in this phase.

---
*Phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign*
*Completed: 2026-08-05*

## Self-Check: PASSED

All 7 created/modified files verified present on disk; all 4 task/metadata commit hashes (`e67d8b5`, `6ad2822`, `a96f0d7`, `8da70b9`) verified present in git log.
