---
phase: 15-optional-app-audio-targets
plan: 02
subsystem: core
tags: [csharp, dotnet, toggle-service, audio-controller, xunit, tdd]

# Dependency graph
requires: [15-01]
provides:
  - TryExecuteOptionalStep — Skipped-recording sibling of TryExecuteStep, reused by both toggle directions
  - ToggleToRigMode Audio/App steps optional, keyed off RigAudioDeviceId/CompanionAppPath
  - ToggleToNormalMode Audio step applies NormalAudioDeviceId via SetDefault (AUDIO-04 real effect), never snapshot.Audio
  - ToggleToNormalMode App-unset outcome is Skipped, not NotAttempted
  - IsFullyConfigured/IsSettingsConfigured gate on the monitor set only (D-05)
affects: [15-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "TryExecuteOptionalStep extends TryExecuteStep with a configured-at-all guard rather than duplicating its try/catch/trace body"
    - "Fail-fast preflight relocated into the step body it guards, so D-04's always-3-steps invariant holds even for a configured-but-broken optional target"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs

key-decisions:
  - "TryExecuteOptionalStep delegates to TryExecuteStep rather than inlining a second try/catch, per RESEARCH.md Pattern 1"
  - "The companion-app File.Exists preflight moved from a top-level throw into the App step body — a deliberate behavior change (Monitor/Audio now run before a broken app path is discovered), required by D-04's 'always 3 steps' invariant, not an accidental regression"
  - "The two pre-existing 'audio restore throws' tests were deleted (not kept vacuously passing); their isolate-and-continue coverage (still minimizes, still clears, IsInRigMode() flips false) was re-homed onto a new device-missing test that exercises the same code paths under the new SetDefault-based contract"

patterns-established:
  - "Every optional field gets a paired Skipped (unset) test and a Failed (configured-but-broken) test — never collapse the two into one code path or one test"

requirements-completed: [APP-04, APP-05, AUDIO-03, AUDIO-04, AUDIO-05]

# Metrics
duration: ~25min
completed: 2026-08-04
---

# Phase 15 Plan 02: Optional Audio/App Targets & Normal-Mode Audio Real Effect Summary

**Made the companion-app launch target and both audio-device settings genuinely optional in `ToggleService` via a new `TryExecuteOptionalStep` helper, and gave `NormalAudioDeviceId` real runtime effect by replacing `ToggleToNormalMode`'s snapshot-based `Restore` with a `SetDefault`-or-skip call — the two-fold core behavior change this phase exists to deliver.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-04 (continuing from Plan 01)
- **Completed:** 2026-08-04
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- `TryExecuteOptionalStep(stepName, configuredValue, action, steps)` added as a sibling to the existing `TryExecuteStep` — when the configured value is null/empty it appends a distinct `Skipped` step and returns `true` (does-not-block, same contract as `Succeeded`); otherwise it delegates to `TryExecuteStep` unchanged, so a configured-but-broken target still surfaces as a real `Failed` step
- `ToggleToRigMode`'s Audio step is now keyed off `settings.RigAudioDeviceId` (unset → Skipped; set-but-gone, detected via `IAudioController.TryResolveDevice` → Failed with a friendly D-07 message) and its App step off `settings.CompanionAppPath` (unset → Skipped; set-but-missing, detected via the relocated `File.Exists` check → Failed) — the stop-on-first-failure short-circuit shape is preserved exactly (a Failed Audio step still blocks App via `NotAttempted`; a Skipped Audio step does not)
- The top-level `File.Exists(settings.CompanionAppPath)` preflight throw is gone; the same check now lives inside the App step body, which is the change required to satisfy D-04's "the result always has all 3 steps" — Monitor and Audio now run (and are visible in the result) even when the App step ultimately fails on a missing path
- `ToggleToNormalMode`'s Audio step no longer reads `snapshot.Audio` at all — it now applies `settings.NormalAudioDeviceId` via `SetDefault` (unset → Skipped; set-but-gone → Failed with a friendly D-07 message), the same optional-target pattern as the Rig-mode path, mirrored into the isolate-and-continue try/catch shape so a monitor-restore failure or an audio failure never blocks the other. This is AUDIO-04's real behavior change, not just a validation relaxation — `snapshot.Audio` is now unread dead data (Phase 18 cleanup scope, left untouched here)
- `ToggleToNormalMode`'s unset-App-path branch now records `Skipped` instead of `NotAttempted` (D-03/D-04 — a deliberately-unconfigured target, not blocked-by-an-earlier-failure)
- `IsFullyConfigured`/`IsSettingsConfigured` relaxed to the monitor-set check only (D-05); the stale "both audio devices, and the companion app path" exception string is reworded to reference only the monitor requirement — `grep -rn "both audio devices" src/RigToggle.Core/` returns zero matches
- The Monitor try/catch block and `_monitorController.Restore(snapshot.Monitor)` call inside `ToggleToNormalMode` are byte-for-byte unchanged (verified via `git diff` — only doc comments around it changed), respecting the Phase 16 boundary

## Task Commits

Each task was committed atomically, following the RED/GREEN TDD gate:

1. **Task 1: Rewrite/add ToggleService tests for the Skipped and Failed branches (RED)** - `9780df3` (test) — 9 of 22 `ToggleServiceTests` failed against the pre-Task-2 `ToggleService.cs`, confirming genuine RED before any implementation changed
2. **Task 2: Make Audio/App optional in both directions and give Normal-mode audio real effect (GREEN)** - `4d30089` (feat) — all 75 tests in `RigToggle.Tests` pass

## Files Created/Modified

- `src/RigToggle.Core/ToggleService.cs` — added `TryExecuteOptionalStep`; rewired `ToggleToRigMode`'s Audio/App steps and relocated the app-path preflight into the App step body; rewrote `ToggleToNormalMode`'s Audio block from `Restore(snapshot.Audio)` to the `SetDefault`-or-skip pattern; changed the Normal-mode App-unset outcome to `Skipped`; relaxed `IsFullyConfigured`; reworded the stale guard message; updated doc comments on `ToggleToRigMode`/`ToggleToNormalMode`/`IsSettingsConfigured` to reflect the new optionality contract
- `src/RigToggle.Tests/ToggleServiceTests.cs` — added an `audioDeviceMissing` knob to `CreateService`; inverted the Normal-mode audio test to assert `SetDefault` (not `Restore`); converted the app-path-missing throw test into a Failed-App-step 3-step-result assertion; deleted the two now-invalidated "audio restore throws" tests, re-homing their isolate-and-continue coverage onto a new device-missing test; added 6 new paired Skipped/Failed tests covering Rig Audio, Rig App, Normal Audio, Normal App, and the "both unset" 3-step-Skipped-Skipped case

## Decisions Made

- Kept `TryExecuteOptionalStep` as a thin wrapper delegating to `TryExecuteStep` rather than duplicating try/catch/trace logic, per RESEARCH.md Pattern 1 — this kept the diff to `ToggleService.cs` small and avoided a second failure-handling code path to keep in sync
- Deleted (rather than renamed-and-rewrote) both invalidated "audio restore throws" tests, since the plan's own acceptance criteria treated deletion as the preferred path and the new `audioDeviceMissing`-driven test already fully covers the isolate-and-continue behavior they were protecting
- Updated the `ToggleToRigMode`/`ToggleToNormalMode` class-level XML doc comments beyond the plan's literal line references, since the old text ("verifies the companion app path still exists... D-05 preflight", "the audio restore path always uses IAudioController.Restore, never the forward-mode device-switch call") became actively misleading once the behavior changed — left uncorrected, it would mislead the next reader/agent about what the code actually does now

## Deviations from Plan

### Auto-fixed Issues

None beyond documentation-comment updates covered above (Rule 2 — keeping doc comments accurate is treated as part of "correct operation," not a separate deviation, since stale doc comments in this exact class have already misled past sessions per the file's own extensive history of code-review-caught bugs).

## Issues Encountered

None. The plan's PATTERNS.md code examples matched the actual required implementation closely enough that no exploration or backtracking was needed.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Plan 03 (Settings UI: Clear button for the app path, "(None...)" audio-dropdown sentinel, `ValidateSettingsForm`/`MainForm` relaxation) can now build against a `ToggleService` whose optionality contract is fully implemented and tested — `IsSettingsConfigured()` already gates on the monitor set only, so Plan 03's Save-button relaxation has a stable target to match.
- `src/RigToggle.App/MainForm.cs:292`'s stale "both audio devices, and the companion app" dialog string was NOT touched by this plan (out of this plan's `files_modified` scope, which was Core-only) — Plan 03 must fix it, per RESEARCH.md Pitfall 4's own note that this string lives outside CONTEXT.md's canonical-refs list and is easy to miss.
- `dotnet build`/`dotnet test` on `RigToggle.Core`/`RigToggle.Tests` both succeed with 0 errors; 75/75 tests pass. `RigToggle.Windows.Tests`/the WinForms `RigToggle.App`/`RigToggle.Windows` projects were not built in this Linux sandbox (missing Microsoft.WindowsDesktop.App runtime) — pre-existing environment limitation, not something this plan needed to touch since it made zero changes outside `RigToggle.Core`.
- No blockers for Plan 03.

## Self-Check: PASSED

Both modified files (`src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.Tests/ToggleServiceTests.cs`) and this SUMMARY.md confirmed present on disk; both commits (`9780df3`, `4d30089`) confirmed present in `git log`.

---
*Phase: 15-optional-app-audio-targets*
*Completed: 2026-08-04*
