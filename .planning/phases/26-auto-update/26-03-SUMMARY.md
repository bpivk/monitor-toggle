---
phase: 26-auto-update
plan: 03
subsystem: infra
tags: [dotnet, winforms, system.text.json, crash-recovery, self-update]

requires:
  - phase: 26-01
    provides: "UpdateApplyEntryPoint's real swap body (wait-for-writable, rename-to-.bak, move-staged-into-place, relaunch) and the T-26-04 same-directory containment guard this plan extends with marker-write and backup-retention"
provides:
  - "UpdateAppliedMarker / UpdateMarkerStage: three-stage (Applied/FirstLaunchAttempted/Reverted) disk-persisted cross-version record"
  - "IUpdateAppliedMarkerStore/JsonUpdateAppliedMarkerStore: atomic JSON persistence with JsonStringEnumConverter for a forward-compatible on-disk format"
  - "UpdateApplyEntryPoint now records the Applied-stage marker after a successful swap and never deletes the retained .bak backup"
  - "UpdateRollbackChecker: above-the-guard startup state machine that advances/restores/reverts across app launches, with ConfirmHealthy as the sole backup-deletion commit point"
  - "MainForm.BeginUpdateHealthWatch (10s message-loop-tick confirmed-healthy signal) and ShowUpdateRevertNotice (Warning-icon revert toast)"
  - "Real child-process test coverage of the swap mechanism's success, idempotency, and refusal paths (UpdateApplyProcessTests)"
affects: [26-05-formatted-release-notes]

actuals:
  tokens: 12038
  tasks: 3
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Above-the-guard startup state machine, a sibling of StartupRecoveryChecker but invoked at a different point in Program.cs (before, not beside, SingleInstanceGuard.Acquire())"
    - "Cross-version disk marker serialized with JsonStringEnumConverter so a future enum-member reorder cannot silently reinterpret an on-disk value written by an earlier shipped version"
    - "Confirmed-healthy commit point gated on a real WinForms message-loop timer tick, never merely 'the process was created' or 'the file move completed'"

key-files:
  created:
    - src/RigToggle.Core/Models/UpdateAppliedMarker.cs
    - src/RigToggle.Core/Abstractions/IUpdateAppliedMarkerStore.cs
    - src/RigToggle.Core/Persistence/JsonUpdateAppliedMarkerStore.cs
    - src/RigToggle.App/UpdateRollbackChecker.cs
    - src/RigToggle.Tests/JsonUpdateAppliedMarkerStoreTests.cs
    - src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs
  modified:
    - src/RigToggle.App/UpdateApplyEntryPoint.cs
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "Task 1 checkpoint:decision resolved to option-a (dedicated update-applied.json marker + a .bak sibling of the exe), per the plan's own recommendation and 26-PATTERNS.md's ToggleInProgressMarker/JsonToggleInProgressStore precedent. Auto-selected under the orchestrator's stated auto-mode-active run (gate=\"blocking\", not \"blocking-human\"), matching the standard checkpoint:decision auto-resolution rule."
  - "UpdateAppliedMarker/JsonUpdateAppliedMarkerStore are a deliberate sibling of, not an extension of, ToggleInProgressMarker/JsonToggleInProgressStore -- two distinct crash-detection concerns kept in separate types, mirroring PITFALLS.md Pitfall 7's 'don't reuse a mechanism for a different concern' lesson"
  - "Stage is serialized as a string (JsonStringEnumConverter) rather than the default ordinal -- this marker is written by one shipped version's helper and read by the next version's startup code, so the on-disk value must be self-describing"
  - "UpdateRollbackChecker.Run is wired in Program.cs strictly above SingleInstanceGuard.Acquire() -- identical reasoning to the pre-existing --apply-update bypass branch (PITFALLS.md Pitfall 4): a FirstLaunchAttempted restore spawns a replacement process, and doing that while holding the mutex would deadlock the very relaunch this check performs"
  - "BeginUpdateHealthWatch's 10-second timer tick is the confirmed-healthy signal, not process creation -- a tick can only fire once the message pump is genuinely running, satisfying 26-CONTEXT.md's Claude's Discretion note on what 'confirmed-healthy' must mean"
  - "Added an assembly-level [assembly: CollectionBehavior(DisableTestParallelization = true)] in the new UpdateApplyProcessTests.cs file (not in SingleInstanceProcessTests.cs, which stays untouched) -- both test classes contend for the same machine-wide single-instance mutex and 'RigToggle.App' process name, and xUnit parallelises across classes by default; without this, the two classes' child processes would race each other's use of that same real OS-level resource"

requirements-completed: [UPDATE-05]

coverage:
  - id: D1
    description: "Marker-store round-trip (all four members), missing/malformed-file degradation to null, no-op Clear on a missing file, parent-directory creation on Save, and the Stage-persists-as-a-quoted-string property (UPDATE-05, D-09)"
    requirement: "UPDATE-05"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/JsonUpdateAppliedMarkerStoreTests.cs (6 Fact cases)"
        status: pass
    human_judgment: false
  - id: D2
    description: "A successful swap retains the .bak backup and records an Applied-stage marker; the running-exe/target-exe version strings are read before either path is touched"
    requirement: "UPDATE-05"
    verification:
      - kind: integration
        ref: "src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs#ApplyUpdate_ValidPayload_SwapsAndRetainsBackup"
        status: unknown
    human_judgment: true
    rationale: "This project's Windows.Tests testhost requires Microsoft.WindowsDesktop.App, absent in this Linux build sandbox -- the test compiles cleanly here (0 errors) but only executes in CI on windows-latest, per this project's established SingleInstanceProcessTests precedent."
  - id: D3
    description: "Running the identical --apply-update command line twice leaves exactly one exe at the target path and produces no second relaunch (UPDATE-05 idempotency edge)"
    requirement: "UPDATE-05"
    verification:
      - kind: integration
        ref: "src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs#ApplyUpdate_SameCommandLineRunTwice_SecondRunIsNoOp"
        status: unknown
    human_judgment: true
    rationale: "Same Windows-only execution constraint as D2 -- compiles here, executes in CI on windows-latest."
  - id: D4
    description: "A malformed payload and an out-of-directory target are both refused as a total no-op (garbage-payload contract from plan 26-01, plus the new out-of-directory refusal test); the untouched Phase 25 ApplyUpdateBypass_* regression tests still compile and were not edited"
    requirement: "UPDATE-05"
    verification:
      - kind: integration
        ref: "src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs#ApplyUpdate_MalformedPayload_ExitsUnchangedAndTouchesNothing, #ApplyUpdate_TargetOutsideStagedDirectory_RefusedAndUntouched"
        status: unknown
    human_judgment: true
    rationale: "Same Windows-only execution constraint as D2/D3."
  - id: D5
    description: "The above-the-guard rollback state machine: a new exe that never reaches confirmed-healthy is restored and relaunched at the identical path on its next launch, the marker is consumed so it can never retry-loop, and the rollback path never contends for the single-instance mutex"
    requirement: "UPDATE-05"
    verification: []
    human_judgment: true
    rationale: "No automated test can kill a process mid-crash-loop and observe a real next-boot restore; this is deliberately deferred to plan 26-05's operator checkpoint on real Windows hardware, per this plan's own assumption_delta section (backstop-verification, not automatable). Static verification performed here: UpdateRollbackChecker.Run's call site in Program.cs is confirmed above SingleInstanceGuard.Acquire() by line number, and the build/grep acceptance criteria for all three stages (Applied/FirstLaunchAttempted/Reverted) pass."
  - id: D6
    description: "The retained backup is deleted only after a confirmed-healthy message-loop tick, never merely because the file move completed"
    requirement: "UPDATE-05"
    verification: []
    human_judgment: true
    rationale: "Requires a real WinForms message pump on Windows hardware; not exercisable in this Linux build sandbox. Deferred to plan 26-05's rig verification alongside D5."

duration: 12min
completed: 2026-08-22
status: complete
---

# Phase 26 Plan 03: Never-Stranded Update Recovery Summary

**Retained-backup + applied-but-unconfirmed marker + above-the-guard auto-rollback state machine, so a failed or crash-looping update can never leave the app stranded (UPDATE-05, D-09) — proven by 6 marker-store unit tests and 4 real child-process swap tests.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-08-22T17:11:29Z
- **Completed:** 2026-08-22T17:23:47Z
- **Tasks:** 3 (1 checkpoint:decision auto-resolved, 2 auto)
- **Files modified:** 9 (6 created, 3 modified)

## Accomplishments

- `UpdateAppliedMarker`/`UpdateMarkerStage` (Core): a three-stage, strictly one-way disk-persisted record (`Applied` → `FirstLaunchAttempted` → `Reverted`) mirroring the codebase's existing `ToggleInProgressMarker` shape, but deliberately a separate type per this codebase's "distinct crash-detection concerns stay in separate types" convention
- `JsonUpdateAppliedMarkerStore`: atomic temp-file-then-`File.Move` JSON persistence with `JsonStringEnumConverter`, so the on-disk `Stage` value survives a future enum-member reorder across shipped versions
- `UpdateApplyEntryPoint` now writes the `Applied`-stage marker (with both version strings read before either exe path is touched) immediately after a successful swap, and — unchanged from plan 26-01 — never deletes the `.bak` it creates
- `UpdateRollbackChecker` (App, new `internal static class`): the above-the-guard state machine wired strictly before `SingleInstanceGuard.Acquire()` in `Program.cs` — advances the marker on the new version's first launch, restores the retained backup and relaunches at the identical path if that attempt never confirmed healthy, and surfaces a Warning-icon "reverted to vY" toast naming both versions on the restored version's first launch back
- `MainForm.BeginUpdateHealthWatch`/`ShowUpdateRevertNotice`: a 10-second one-shot `System.Windows.Forms.Timer` whose tick is the deliberate confirmed-healthy signal, and the revert notification, both wired into `Program.cs` after `mainForm.InitializeTrayState()`
- 6 new marker-store unit tests (round-trip, corruption tolerance, string-enum persistence) plus 4 new real child-process tests proving the swap's success/backup-retention, idempotency, and refusal paths — the untouched Phase 25 `ApplyUpdateBypass_*` regression tests still compile unmodified

## Task Commits

1. **Task 1: Decide the on-disk shape of the update-applied marker and the retained backup** - checkpoint:decision, auto-resolved to `option-a` (no commit — decision only)
2. **Task 2: Retained backup, applied-but-unconfirmed marker, and the above-the-guard rollback state machine** - `51f9e08` (feat)
3. **Task 3: Marker-store unit tests and a real child-process proof of the swap** - `252fce1` (test)

## Files Created/Modified

- `src/RigToggle.Core/Models/UpdateAppliedMarker.cs` - Three-stage disk-persisted cross-version record + `UpdateMarkerStage` enum
- `src/RigToggle.Core/Abstractions/IUpdateAppliedMarkerStore.cs` - Persistence contract (`TryLoad`/`Save`/`Clear`)
- `src/RigToggle.Core/Persistence/JsonUpdateAppliedMarkerStore.cs` - Atomic JSON store with `JsonStringEnumConverter`
- `src/RigToggle.App/UpdateApplyEntryPoint.cs` - Reads both version strings before the swap, writes the `Applied` marker after a successful swap
- `src/RigToggle.App/UpdateRollbackChecker.cs` - The above-the-guard state machine (`Run`, `ConfirmHealthy`)
- `src/RigToggle.App/Program.cs` - `updateMarkerStore` construction, `UpdateRollbackChecker.Run` above the guard, health-watch/revert-notice wiring after `InitializeTrayState()`
- `src/RigToggle.App/MainForm.cs` - `BeginUpdateHealthWatch`, `ShowUpdateRevertNotice`
- `src/RigToggle.Tests/JsonUpdateAppliedMarkerStoreTests.cs` - 6 marker-store unit tests
- `src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs` - 4 real child-process swap tests + assembly-level `CollectionBehavior(DisableTestParallelization = true)`

## Decisions Made

See `key-decisions` in the frontmatter above for the full list, most notably:
- Task 1's checkpoint resolved to option-a (dedicated JSON marker + `.bak` sibling), auto-selected under the orchestrator's stated auto-mode-active run since the checkpoint carried `gate="blocking"` (not `"blocking-human"`)
- `UpdateRollbackChecker.Run` wired strictly above `SingleInstanceGuard.Acquire()` — the identical PITFALLS.md Pitfall 4 reasoning already applied to the `--apply-update` bypass branch
- Added assembly-level `CollectionBehavior(DisableTestParallelization = true)` in the new test file (not touching `SingleInstanceProcessTests.cs`) so the new child-process tests cannot race that existing suite's use of the same real single-instance mutex and process name

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Two doc-comment strings accidentally matched their own negative acceptance-criteria greps**
- **Found during:** Task 2 verification
- **Issue:** A doc comment in `Program.cs` referenced the literal method name `ConfirmHealthy` a second time (in prose), and a doc comment in `MainForm.cs` referenced the literal string `ToolTipIcon.Error` while explaining that icon is never used — both accidentally tripped the plan's own literal-grep acceptance criteria (`grep -rc 'ConfirmHealthy' Program.cs` must equal 1; `! grep -q 'ToolTipIcon.Error' MainForm.cs` must hold)
- **Fix:** Reworded both comments to describe the same thing without repeating the exact matched substring (`"the callback below"` instead of naming `ConfirmHealthy` again; `"the Error-level icon"` instead of `ToolTipIcon.Error`)
- **Files modified:** `src/RigToggle.App/Program.cs`, `src/RigToggle.App/MainForm.cs`
- **Verification:** Re-ran both grep checks after the edit — both pass; full solution rebuild still 0 errors
- **Committed in:** `51f9e08` (Task 2 commit — caught and fixed before committing)

---

**Total deviations:** 1 auto-fixed (Rule 1, both instances part of the same root cause)
**Impact on plan:** Cosmetic-only (doc-comment wording); no functional or scope change.

## Issues Encountered

- **This build sandbox is Linux, not Windows** (same constraint documented in 26-01-SUMMARY.md). `dotnet build RigToggle.sln -c Release` and `dotnet test src/RigToggle.Tests` both ran and passed directly in this session (158/158 tests green, including the 6 new marker-store tests). `dotnet build src/RigToggle.Windows.Tests` also succeeded (0 errors, 0 warnings), proving the 4 new `UpdateApplyProcessTests` compile — but real execution of those 4 tests (and of the deliberately-interrupted-update / reboot-autostart scenarios D5/D6 describe) requires a live Windows process host and is deferred to CI (windows-latest) and plan 26-05's operator checkpoint respectively, per this plan's own `<verification>` block.
- **Pre-existing warnings, unrelated to this plan's files, remain in the solution.** A clean (`rm -rf bin obj`) full-solution build reports 6 `xUnit1031` warnings in `SingleInstanceGuardTests.cs`/`ToggleOrchestratorTests.cs` — neither file is in this plan's `files_modified` list, and neither was touched. Not fixed, per the executor's SCOPE BOUNDARY rule; same pre-existing condition documented in 26-01-SUMMARY.md. This plan's own 9 files introduce 0 new warnings.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The three-stage marker/backup/rollback mechanism is fully wired and unit/integration-tested (compile-verified everywhere, execution-verified on the cross-platform 158/158 suite). Plan 26-05 (formatted release notes) — the phase's final plan and the one carrying the operator rig checkpoint — inherits a working, provably-swap-idempotent, always-recoverable update-apply path to build its remaining UI-facing work on top of, and owns the real deliberately-interrupted-update and reboot-autostart verification this plan's D5/D6 coverage entries defer to it. `UpdateRollbackChecker`'s three stages, `ConfirmHealthy`'s commit-point discipline, and the mutex-ordering guarantee are all ready for that real-hardware pass; no known blockers.

---
*Phase: 26-auto-update*
*Completed: 2026-08-22*
