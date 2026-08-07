---
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
plan: 04
subsystem: app
tags: [dotnet, winforms, composition-root, startup-dialogs, mode-store, crash-recovery]

# Dependency graph
requires:
  - phase: 16-01
    provides: IModeStore/IToggleInProgressStore contracts, JsonModeStore/JsonToggleInProgressStore implementations, ToggleMode/ToggleInProgressMarker types
  - phase: 16-03
    provides: ToggleService rewired to take IModeStore (not ISnapshotStore); ToggleOrchestrator rewired to take IToggleInProgressStore + expose IsModeKnown()
provides:
  - "StartupRecoveryChecker.Run(IModeStore, IToggleInProgressStore) — two blocking startup dialogs (mode-corruption, crash-recovery), ordered mode-corruption-first"
  - "Program.cs composition root wired to JsonModeStore/JsonToggleInProgressStore, one-time bootstrap seeding mode.json from legacy snapshot presence"
  - "MainForm mode-known-aware: RefreshUi Unknown branch + IsModeKnown() guards on all three toggle triggers (button, tray, hotkey)"
affects: [16-05 (rig checkpoint — visual/behavioral confirmation of the two dialogs and Unknown label), 18 (ISnapshotStore/StateSnapshot/Restore cleanup, now fully unreferenced by the composition root's toggle path)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "One-time bootstrap seed (Pattern 1, 16-RESEARCH.md): mode.json seeded from snapshotStore.Exists() exactly once, guarded by !modeStore.Exists() — never an unconditional default"
    - "Blocking startup dialog exception to the best-effort-swallow startup idiom (Pattern 3): StartupRecoveryChecker.Run() deliberately not wrapped in try/catch, unlike every other Program.cs startup side effect"
    - "Mode-known guard duplicated per trigger (not shared), matching each trigger's existing chrome convention (MessageBox for GUI, ShowBalloonTip for tray/hotkey, never mixed)"

key-files:
  created:
    - src/RigToggle.App/StartupRecoveryChecker.cs
  modified:
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "snapshotStore is kept constructed in Program.cs (not removed) — still needed for the one-time bootstrap's Exists() read; full ISnapshotStore removal is explicitly Phase 18 scope, matching Plan 03's own decision to leave ISnapshotStore/StateSnapshot untouched"
  - "StartupRecoveryChecker does not check the crash marker when the mode is unknown — the mode-unknown dialog's 'verify manually' instruction already subsumes the crash-recovery guidance, avoiding two dialogs firing for a condition where the mode itself is already unknown"
  - "The marker is cleared BEFORE the crash-recovery dialog is shown (not after) so a repeat crash while the dialog is on screen can never re-surface a stale marker on the next launch"
  - "RefreshUi's unknown-mode branch leaves the notify icon's current Icon untouched (no new 'unknown' glyph introduced this phase) — only Text/label surfaces change"

requirements-completed: [DISPLAY-11, DISPLAY-13]

# Metrics
duration: ~20min
completed: 2026-08-07
---

# Phase 16 Plan 04: App-Tier Composition Root Wiring + Startup Recovery Dialogs Summary

**Wired Plan 01's IModeStore/IToggleInProgressStore and Plan 03's rewired ToggleService/ToggleOrchestrator into Program.cs's composition root with a one-time legacy-snapshot bootstrap seed, added the two blocking startup dialogs (mode-corruption, crash-recovery) in a new StartupRecoveryChecker, and made MainForm's mode label and all three toggle triggers refuse to act on an unknown mode.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-08-07T22:20:31Z
- **Tasks:** 3 completed
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- New `StartupRecoveryChecker.Run(IModeStore, IToggleInProgressStore)` static helper implements the exact two-dialog sequence from 16-RESEARCH.md Pattern 3: mode-corruption dialog when `TryLoad()` returns null (marker deliberately not checked), else a crash-recovery dialog if a marker is found (cleared first, then shown with `{TargetMode}` interpolated) — both use `owner: null`, `MessageBoxIcon.Warning`, and are the one deliberate exception to the app's best-effort-swallow startup idiom
- `Program.cs` now constructs `JsonModeStore(mode.json)` and `JsonToggleInProgressStore(toggle-in-progress.json)` alongside the still-present `JsonSnapshotStore`, seeds `mode.json` exactly once from `snapshotStore.Exists()` when absent (never an unconditional Normal default — this is the single highest-value correctness detail per the plan, preventing a spurious dialog for every existing Rig-mode user on first v2.0 launch), calls `StartupRecoveryChecker.Run()` before any toggle-capable object is constructed and before the tray-safe timing point (so both blocking dialogs run on both the visible and `--tray` startup paths), and rewires `ToggleService`/`ToggleOrchestrator` construction to the new stores per Plan 03's constructors
- `MainForm.RefreshUi()` gained an unknown-mode branch (checked before the `IsInRigMode()` branch) rendering `"Mode: Unknown"` and a neutral `"Toggle"` label instead of defaulting to Rig/Normal wording
- All three toggle triggers (`BtnToggle_Click`, `TrayToggleMenuItem_Click`, `HandleHotkeyToggle`) now guard on `_orchestrator.IsModeKnown()` before their branch decision, refusing to toggle from an unknown mode — the GUI button uses a `MessageBox` (matching the existing WR-01 guard shape), while tray/hotkey use `ShowBalloonTip` (matching their existing no-MessageBox D-08 chrome convention)
- `dotnet build RigToggle.sln` now succeeds with 0 errors (previously failing on `RigToggle.App` per Plan 03's documented, anticipated build gap); `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` still passes all 78 tests (unchanged — this plan touched no test-covered Core logic)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create StartupRecoveryChecker with the two blocking dialogs** - `bca6fe0` (feat)
2. **Task 2: Wire new stores, one-time bootstrap, and StartupRecoveryChecker into Program.cs** - `132ec10` (feat)
3. **Task 3: Make MainForm mode-known-aware — RefreshUi Unknown branch + three trigger guards** - `0692835` (feat)

_Note: SUMMARY.md commit follows this list — this is a worktree-isolated parallel executor, so STATE.md/ROADMAP.md are excluded from the metadata commit and updated centrally by the orchestrator after merge._

## Files Created/Modified
- `src/RigToggle.App/StartupRecoveryChecker.cs` - New static helper: `Run(IModeStore, IToggleInProgressStore)` implementing the mode-corruption (D-06/D-07) and crash-recovery (D-02/D-03) blocking dialogs, ordered mode-corruption-first, with LOCKED UI-SPEC copy
- `src/RigToggle.App/Program.cs` - Constructs `JsonModeStore`/`JsonToggleInProgressStore`; one-time bootstrap seed guarded by `!modeStore.Exists()`; `StartupRecoveryChecker.Run()` invoked after bootstrap, before `ToggleService`/`ToggleOrchestrator` construction and the tray-safe timing point; `ToggleService` now takes `modeStore` (not `snapshotStore`); `ToggleOrchestrator` now takes `markerStore`
- `src/RigToggle.App/MainForm.cs` - `RefreshUi()` gains an unknown-mode branch before the `IsInRigMode()` branch; `BtnToggle_Click`, `TrayToggleMenuItem_Click`, `HandleHotkeyToggle` each gain an `IsModeKnown()` guard using their trigger's existing chrome (MessageBox for GUI, ShowBalloonTip for tray/hotkey)

## Decisions Made
- Followed 16-RESEARCH.md Pattern 1/3 and the plan's own interface listing exactly: bootstrap seed timing, dialog ordering, and guard placement all matched the plan's specification with no architectural adjustments needed
- Kept `snapshotStore` constructed in `Program.cs` (not removed) since the bootstrap seed still needs its `Exists()` read — full `ISnapshotStore` removal remains Phase 18 scope, consistent with Plan 03's own decision to leave it untouched
- Left the notify icon's `Icon` property untouched in the unknown-mode `RefreshUi()` branch (only `Text` changes) since no dedicated "unknown mode" tray glyph exists this phase — the plan explicitly calls this out as acceptable ("leave the notify icon at a safe default")

## Deviations from Plan

None - plan executed exactly as written. The plan's interface listing (constructor signatures, file line references, LOCKED copy) matched the actual current source precisely, and every automated verification grep in the plan passed on the first attempt.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `dotnet build RigToggle.sln` succeeds across all 6 projects (Core, Windows, App, Tests, IconGen, Windows.Tests) — the App-tier build gap Plan 03 documented as expected-until-Plan-04 is now closed.
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` passes all 78 tests, unchanged from Plan 03's close (this plan added no new Core-layer logic, only App-tier wiring/UI).
- Visual/behavioral confirmation of the two startup dialogs and the "Mode: Unknown" label is explicitly deferred to Plan 05's rig checkpoint, per this plan's own `<verification>` block — nothing in this plan's scope requires rig hardware to verify (pure composition-root wiring + UI branch logic), but the actual dialog appearance/timing on a real Windows session has not been visually confirmed.
- No blockers or concerns for Plan 05.

---
*Phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign*
*Completed: 2026-08-07*
