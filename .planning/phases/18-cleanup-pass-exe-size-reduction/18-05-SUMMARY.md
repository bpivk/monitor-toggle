---
phase: 18-cleanup-pass-exe-size-reduction
plan: 05
subsystem: testing
tags: [cleanup, dead-code-removal, test-doubles, audio-restore, monitor-restore]

# Dependency graph
requires:
  - phase: 18-cleanup-pass-exe-size-reduction
    provides: "18-01 removed IAudioController.Restore/WindowsAudioController.Restore; 18-02 removed IMonitorController.Restore/WindowsMonitorController.Restore — this plan removed the doubles' now-orphaned Restore members those two plans deliberately left behind"
provides:
  - "Test doubles (FakeMonitorController, FakeAudioController, BlockingMonitorController) implementing exactly the post-cleanup IMonitorController/IAudioController member sets, no extras"
  - "15-REVIEW.md IN-01 closed: CreateService's dead audioThrowsOnRestore knob removed"
  - "Tree-wide zero-reference audit confirming CLEANUP-01 is genuinely closed"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - src/RigToggle.Tests/Doubles/FakeControllers.cs
    - src/RigToggle.Tests/Doubles/BlockingMonitorController.cs
    - src/RigToggle.Core/Models/MonitorState.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs

key-decisions:
  - "Left the residual bare-word 'Restore' comments inside src/RigToggle.Windows/WindowsMonitorController.cs untouched — that file is 18-02's exclusive ownership per this plan's scope_boundary, and 18-02's own SUMMARY already dispositioned those exact comments as intentional historical cross-references"

requirements-completed: [CLEANUP-01, CLEANUP-02]

# Metrics
duration_min: 20
completed: 2026-08-09
---

# Phase 18 Plan 05: Close Out Restored-Path Test Debt & Tree-Wide Audit Summary

**Deleted the last two orphaned `Restore` methods and the dead `throwOnRestore` knob from the test doubles, closed `15-REVIEW.md` IN-01's dead `CreateService` parameter, and ran the tree-wide CLEANUP-01 closing audit — zero API-level references to the retired restore subsystem remain anywhere in `src/`.**

## Performance

- **Duration:** ~20 min
- **Tasks:** 2/2 completed
- **Files modified:** 4

## Accomplishments

- Deleted `FakeMonitorController.Restore(MonitorState)` and `FakeAudioController.Restore(AudioState)` — both were the only writers of the `monitor.Restore:`/`audio.Restore:` call-log labels and had been unreachable in production since 18-01/18-02 removed `Restore` from `IAudioController`/`IMonitorController`
- Deleted the dead `_throwOnRestore` field, `throwOnRestore` constructor parameter, and its assignment from `FakeAudioController` — `FakeAudioController` now takes exactly three parameters (`callLog`, `capturedDefaultDeviceId`, `deviceExists`)
- Deleted `BlockingMonitorController`'s no-op `Restore` stub; corrected its class doc comment's member count from "five" to "four" (the interface now declares four members total)
- Rewrote `FakeControllers.cs`'s class doc comment's call-order example (the old "snapshot Save must precede any mutation" example referenced a subsystem that no longer exists) to reference the live enable-set-before-disable-set ordering instead
- Rewrote `MonitorState.cs`'s doc comment to describe the record's real live purpose (CR-01's unchanged-vs-mutated comparison after a failed monitor step) rather than the retired restore mechanism, while preserving the DISPLAY-02/Phase-4 provenance as explicit past-tense history
- Closed `15-REVIEW.md` IN-01: deleted `CreateService`'s dead `bool audioThrowsOnRestore = false` parameter — no test in the suite ever passed `true`, and the double it drove no longer has a `Restore` path to exercise
- Deleted the three vacuous `Assert.DoesNotContain(callLog, entry => entry.StartsWith("...Restore"))` assertions (two in `ToggleToNormalMode_AppliesNormalAudioDeviceViaSetDefault_AndAppliesExplicitMonitorSet_NeverRestore`, one in `ToggleToNormalMode_SkipsAudio_WhenNormalAudioDeviceIdUnset`) — these asserted the absence of a method that no longer exists anywhere in the codebase and would pass unconditionally, giving false assurance; the positive assertions in the same tests (`audio.SetDefault`, `monitor.ActivateMonitors`, `monitor.DeactivateMonitors`) carry the real meaning and were left untouched
- Renamed the vestigial `_NeverRestore`-suffixed test to `ToggleToNormalMode_AppliesNormalAudioDeviceViaSetDefault_AndAppliesExplicitMonitorSet` and reworded its comment to state positively what it proves
- Ran the tree-wide CLEANUP-01 closing audit (all four greps below) — zero test lost, full solution builds, `RigToggle.Tests` passes 81/81 (unchanged), `RigToggle.Windows.Tests` builds 0 errors (not executed, dev-host limitation)

## Task Commits

Each task was committed atomically:

1. **Task 1: Strip the Restore members and the dead throwOnRestore knob from the test doubles** - `eb77a75` (refactor)
2. **Task 2: Close IN-01's dead test knob, drop the vacuous assertions, and run the tree-wide zero-reference audit** - `9783a29` (test)

_Note: this plan runs in worktree isolation — plan metadata (SUMMARY.md) is committed separately after merge; STATE.md/ROADMAP.md are not touched by this agent._

## Files Created/Modified

- `src/RigToggle.Tests/Doubles/FakeControllers.cs` - Deleted `FakeMonitorController.Restore`, `FakeAudioController.Restore`, the `_throwOnRestore` field/parameter/assignment triple; rewrote the class doc comment's call-order example
- `src/RigToggle.Tests/Doubles/BlockingMonitorController.cs` - Deleted the no-op `Restore` stub; corrected the class doc comment's member count ("five" -> "four")
- `src/RigToggle.Core/Models/MonitorState.cs` - Rewrote the record's doc comment to describe its real live purpose (CR-01 comparison), keeping the DISPLAY-02 provenance as explicit history rather than a current-API reference
- `src/RigToggle.Tests/ToggleServiceTests.cs` - Deleted `CreateService`'s dead `audioThrowsOnRestore` parameter (IN-01) and updated its one construction call site; deleted three vacuous `DoesNotContain(...Restore...)` assertions; renamed and reworded the `_NeverRestore`-suffixed test

## Decisions Made

- Left the residual bare-word `Restore` comments inside `src/RigToggle.Windows/WindowsMonitorController.cs` untouched. That file is out of this plan's scope per `<scope_boundary>` ("anything under `src/RigToggle.Windows*` (plans 18-02 and 18-03 own those)"), and 18-02's own SUMMARY already explicitly dispositioned those exact comment lines as intentional historical cross-references it chose not to rewrite. See the audit justification table below for the individual lines.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `<action>` and `<acceptance_criteria>` blocks exactly; no auto-fixes, no blocking issues, no architectural questions arose.

## Issues Encountered

None.

## Verbatim Audit Output

### Grep 1 — `\.Restore(` (must return nothing)

```
$ grep -rn "\.Restore(" src --include="*.cs"
(no output, exit 1)
```

### Grep 2 — Snapshot types (must return nothing)

```
$ grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot\|InMemorySnapshotStore" src --include="*.cs"
(no output, exit 1)
```

### Grep 3 — Removed CCD helpers / dead knob (must return nothing)

```
$ grep -rn "RestoreViaReconstruction\|CopyOutputTechnology\|AssignSource\|_originalPathsCache\|throwOnRestore" src --include="*.cs"
(no output, exit 1)
```

### Grep 4 — Bare-word `Restore` residue (enumerated and justified below)

```
$ grep -rn "Restore" src --include="*.cs"
src/RigToggle.App/MainForm.cs:583:        /// unchanged. Restore happens through the existing NotifyIcon_MouseClick
src/RigToggle.Tests/ToggleServiceTests.cs:202:        // reach a guarded DeactivateMonitors failure at all, since Restore() could
src/RigToggle.Tests/ToggleServiceTests.cs:392:        // NormalMonitorsToEnable sets — the old "Restore before re-disabling the
src/RigToggle.Windows/WindowsMonitorController.cs:66:    // worked around elsewhere in this file (Restore()/DeactivateMonitors()). Targets
src/RigToggle.Windows/WindowsMonitorController.cs:158:    // abandoned in this exact codebase's Restore() history, three separate rig-tested
src/RigToggle.Windows/WindowsMonitorController.cs:159:    // validation failures — see Restore()'s own doc comment below). Instead reuse the
src/RigToggle.Windows/WindowsMonitorController.cs:160:    // exact same zero-argument PathInfo.ApplyTopology(Extend) call Restore()'s
src/RigToggle.Windows/WindowsMonitorController.cs:170:    // DeactivateMonitors(enableSet): it must run AFTER Restore(), not before, for the
src/RigToggle.Windows/WindowsMonitorController.cs:171:    // same reason (Restore()'s crash-recovery fallback also uses Extend internally).
src/RigToggle.Windows/WindowsMonitorController.cs:191:        // Early availability guard (mirrors Restore() Step 1) — a clear,
```

### Per-hit justification

| File:Line | Hit | Justification |
|---|---|---|
| `MainForm.cs:583` | "unchanged. Restore happens through..." | (a) The English word, unrelated context — describes the OS window-restore-from-minimize interaction (`Show(); WindowState = Normal; Activate()` via the tray icon's left-click path), not the retired audio/monitor restore mechanism. |
| `ToggleServiceTests.cs:202` | "...since Restore() could never leave zero monitors active by construction." | (b) Explicitly past-tense historical note, part of this plan's own Task 2 scope. Explains why `ToggleToNormalMode_LeavesModeUnchanged_WhenDisableThrowsAfterPartiallyMutating` needed a seeded prior mode: the old design's `Restore()`-based flow could never reach the code path this test exercises, so a real toggle can't set up the fixture. Reads unambiguously as history — retained per the plan's explicit instruction not to delete this comment, only ensure it can't be misread as current behavior. |
| `ToggleServiceTests.cs:392` | "...the old 'Restore before re-disabling the enable-set' D-02 asymmetry is retired..." | (b) Explicitly past-tense historical note, also in this plan's Task 2 scope. States directly that the referenced asymmetry "is retired" — unambiguous history explaining why Normal mode now mirrors Rig mode's Activate-then-Deactivate ordering symmetrically. Retained per the plan's explicit instruction. |
| `WindowsMonitorController.cs:66,158,159,160,170,171,191` (7 hits) | Various — e.g. "worked around elsewhere in this file (Restore()/DeactivateMonitors())", "abandoned in this exact codebase's Restore() history", "mirrors Restore() Step 1" | (b) Explicitly past-tense/historical design-rationale notes explaining why `ActivateMonitors`/`GetAllMonitors` are implemented the way they are (reusing the same Extend-topology technique and availability-guard pattern the now-deleted `Restore()` method pioneered). This file is 18-02's exclusive ownership per this plan's `<scope_boundary>` ("Do not touch: ... anything under `src/RigToggle.Windows*` (plans 18-02 and 18-03 own those)"), and 18-02's own SUMMARY.md explicitly dispositioned these exact lines: "Left stray in-comment mentions of 'Restore()' inside `GetAllMonitors`'s and `ActivateMonitors`'s doc comments untouched ... the plan's own acceptance criteria explicitly excludes comment lines from the 'no `Restore` identifier survives' check ... only the class doc summary and `AnyRectanglesOverlap`'s comment were called out for rewrite." Not fixed here — out of this plan's scope, already reviewed and intentionally retained by the plan that owns the file. |

## Before/After `[Fact]`/`[Theory]` Count

| File | Before this plan | After this plan | Delta |
|---|---|---|---|
| `ToggleServiceTests.cs` | 23 | 23 | 0 (no test removed, per plan's explicit constraint) |

## Final Build and Test Output

```
$ PATH="$HOME/.dotnet:$PATH" dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded.
    4 Warning(s)   <- pre-existing xUnit1031 warnings in ToggleOrchestratorTests.cs (documented baseline, out of scope)
    0 Error(s)

$ PATH="$HOME/.dotnet:$PATH" dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed!  - Failed: 0, Passed: 81, Skipped: 0, Total: 81, Duration: 83 ms - RigToggle.Tests.dll (net10.0)
```

Total 81 matches the plan's own measured baseline exactly: 85 (pre-Phase-18) minus 5 (removed by 18-01) plus 1 (added by 18-04) = 81. This plan removed zero tests, as required.

```
$ PATH="$HOME/.dotnet:$PATH" dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj -p:EnableWindowsTargeting=true
Build succeeded.
    0 Warning(s)
    0 Error(s)
(built, not executed — dev-host limitation: Microsoft.WindowsDesktop.App runtime absent on this Linux dev host)
```

## Known Stubs

None.

## Threat Flags

None — this plan only deletes dead test-double code, closes a dead test knob, and rewrites doc comments; no new network endpoints, auth paths, file access patterns, or schema changes were introduced. See the plan's own `<threat_model>` (T-18-05-01 through T-18-05-04, T-18-05-SC) — all dispositioned `mitigate` and satisfied as described: the vacuous assertions were removed while the positive assertions they accompanied survived (T-18-05-01), the live `_deviceExists`/`_disableWasCalled` knobs survived at their exact expected occurrence counts (T-18-05-02), the `[Fact]`/`[Theory]` count and suite Total are both pinned and unchanged (T-18-05-03), and the tree-wide audit closed CLEANUP-01 with every residual bare-word hit individually justified above (T-18-05-04).

## Next Phase Readiness

CLEANUP-01 and CLEANUP-02 are both fully closed by this plan combined with 18-01/18-02: zero API-level `Restore` references remain in `src/`, the snapshot-persistence subsystem is gone, and every surviving bare-word `Restore` mention is either an unrelated English usage or an explicitly past-tense historical note. This was the last plan depending on 18-01/18-02's contract changes; remaining Phase 18 plans (18-03, 18-04, 18-06) are independent or already complete.

## Self-Check: PASSED

- FOUND: src/RigToggle.Tests/Doubles/FakeControllers.cs
- FOUND: src/RigToggle.Tests/Doubles/BlockingMonitorController.cs
- FOUND: src/RigToggle.Core/Models/MonitorState.cs
- FOUND: src/RigToggle.Tests/ToggleServiceTests.cs
- FOUND: .planning/phases/18-cleanup-pass-exe-size-reduction/18-05-SUMMARY.md
- FOUND commit: eb77a75 (Task 1)
- FOUND commit: 9783a29 (Task 2)
- FOUND commit: 9ee706e (docs: SUMMARY.md)
