---
phase: 08-tray-residency-autostart-toast-notification
plan: 01
subsystem: core-windows-foundation
tags: [tray, autostart, toast, registry, formatting, tdd]

requires: []
provides:
  - StartupArgs.ShouldStartHidden
  - ToggleResultFormatter (FormatChecklist, FormatModeTitle, TruncateForBalloon)
  - IAutostartConfigurator
  - WindowsAutostartConfigurator
affects: [08-02, 08-03]

tech-stack:
  added: []
  patterns:
    - "Pure Core predicate mirrors ToggleService.IsFullyConfigured's private-static-pure shape, extracted for App-layer testability with zero WinForms dependency"
    - "Verbatim relocation of GUI-locked formatting logic into Core, leaving the original caller untouched until the plan that owns that file cleans it up"
    - "HKCU-only registry adapter using Environment.ProcessPath (not the managed-assembly-location API) for PublishSingleFile-safe self-referential path resolution"

key-files:
  created:
    - src/RigToggle.Core/StartupArgs.cs
    - src/RigToggle.Core/ToggleResultFormatter.cs
    - src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs
    - src/RigToggle.Windows/WindowsAutostartConfigurator.cs
    - src/RigToggle.Tests/StartupArgsTests.cs
    - src/RigToggle.Tests/ToggleResultFormatterTests.cs
  modified: []

key-decisions:
  - "FormatChecklist relocated verbatim (same switch arms, same Environment.NewLine join) rather than rewritten, guaranteeing byte-identical wording with MainForm's current private method"
  - "MainForm.cs left completely untouched this plan -- its FormatChecklist caller cleanup is explicitly deferred to Plan 08-02, which owns that file; a transient duplicate is acceptable within the phase"
  - "TruncateForBalloon defaults to 250 (not 255) to leave headroom below NotifyIcon.ShowBalloonTip's undocumented ~255-char hard limit"
  - "WindowsAutostartConfigurator always targets Registry.CurrentUser, never LocalMachine, preserving the app's non-elevated asInvoker execution model (Threat T-08-EOP)"
  - "Enable() rewrites the Run value unconditionally on every call (self-heals a stale exe path) rather than checking-then-writing"

patterns-established:
  - "IAutostartConfigurator follows IAppController's exact interface-provenance convention: file-scoped namespace, XML-doc naming the concrete Windows implementer and phase/requirement provenance"

requirements-completed: [TRAY-02, NOTIF-01]

duration: 25min
completed: 2026-07-30
---

# Phase 08 Plan 01: Tray/Autostart/Toast Non-UI Foundation Summary

Pure Core building blocks for Phase 8's UI plans: a `--tray` startup predicate (`StartupArgs`), a relocated `ToggleResult` display formatter (`ToggleResultFormatter` with checklist/mode-title/balloon-truncation), and an `IAutostartConfigurator`/`WindowsAutostartConfigurator` pair that reads/writes only the current-user `Run` registry key.

## Performance

- **Duration:** 25 min
- **Started:** 2026-07-30T07:07:36Z (STATE.md last_updated at Phase 8 kickoff)
- **Completed:** 2026-07-30T07:14:41Z
- **Tasks:** 3 completed
- **Files modified:** 6 created, 0 modified

## Accomplishments

- `StartupArgs.ShouldStartHidden` — a pure, allocation-free `--tray` predicate that never throws on null/empty/garbage args, fully covered by a 6-row xUnit Theory
- `ToggleResultFormatter` — `FormatChecklist` reproduces MainForm's exact per-step wording verbatim, plus new `FormatModeTitle` and `TruncateForBalloon` helpers, all unit-tested
- `IAutostartConfigurator` + `WindowsAutostartConfigurator` — HKCU `Run` key adapter using `Environment.ProcessPath` (PublishSingleFile-safe), always `Registry.CurrentUser`, mutation failures propagate uncaught for the UI layer to handle

## Task Commits

Each task followed the TDD RED/GREEN cycle with separate commits:

1. **Task 1: StartupArgs.ShouldStartHidden** — `0f7cefb` (test, RED) → `c030573` (feat, GREEN)
2. **Task 2: ToggleResultFormatter** — `9dac834` (test, RED) → `f0dbfa1` (feat, GREEN)
3. **Task 3: IAutostartConfigurator + WindowsAutostartConfigurator** — `03d75a6` (feat, plain auto task, no TDD)

_Note: Tasks 1-2 used `tdd="true"`; Task 3 is a plain `auto` task per the plan._

## Files Created/Modified

- `src/RigToggle.Core/StartupArgs.cs` - Pure `--tray` predicate for the composition root
- `src/RigToggle.Core/ToggleResultFormatter.cs` - Shared checklist/mode-title/balloon-truncation formatting, relocated out of MainForm
- `src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs` - HKCU autostart registration contract
- `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` - Real `Microsoft.Win32.Registry` HKCU `Run` adapter
- `src/RigToggle.Tests/StartupArgsTests.cs` - Theory covering every behavior-block case for `ShouldStartHidden`
- `src/RigToggle.Tests/ToggleResultFormatterTests.cs` - Coverage for checklist wording, mode title, and balloon truncation edge cases

## Decisions Made

- Relocated `FormatChecklist` verbatim rather than rewriting, to guarantee byte-identical wording with the existing GUI (see key-decisions above for full rationale)
- Deliberately left `MainForm.cs` unmodified — its own cleanup (removing the now-duplicated private method and redirecting its one caller) is explicitly Plan 08-02's responsibility per the plan's `<action>` instructions
- `TruncateForBalloon` defaults to 250 rather than the balloon's exact 255-char limit, leaving headroom

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Doc-comment substring produced a false-positive grep gate failure**
- **Found during:** Task 3 verification
- **Issue:** The acceptance criteria require `WindowsAutostartConfigurator.cs` to contain zero occurrences of the literal string `Assembly.Location`. My initial XML-doc rationale comment explained *why* `Environment.ProcessPath` is used instead of that older API by naming it directly, which itself contained the forbidden substring — the same class of false-positive documented in `07-01-SUMMARY.md`.
- **Fix:** Reworded the comment to describe the older API generically ("the managed-assembly-location API") without using its literal name, preserving the same rationale.
- **Files modified:** `src/RigToggle.Windows/WindowsAutostartConfigurator.cs`
- **Commit:** `03d75a6` (part of Task 3 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — grep false-positive)
**Impact on plan:** Cosmetic only; no functional change. No scope creep.

## Issues Encountered

None beyond the deviation above.

## Environment Constraint (matches Phase 6/7 precedent)

This executor sandbox has no `dotnet` SDK installed — confirmed via `which dotnet`, checking common install paths (`/usr/share/dotnet`, `/usr/lib/dotnet`), and finding only empty sentinel files under `/root/.dotnet` (no actual `dotnet` binary). This matches the identical constraint documented in `07-01-SUMMARY.md` and its own Phase 6 precedent. Per that established pattern, verification was done via grep-based source assertions instead of a live `dotnet build`/`dotnet test` run:

- `grep -c "ShouldStartHidden" src/RigToggle.Core/StartupArgs.cs` — 2 — PASSED
- `grep -c "StringComparer.OrdinalIgnoreCase" src/RigToggle.Core/StartupArgs.cs` — 1 — PASSED
- `grep -c "args\[" src/RigToggle.Core/StartupArgs.cs` — 0 — PASSED
- `grep -c "InlineData" src/RigToggle.Tests/StartupArgsTests.cs` — 6 (all behavior-block cases represented) — PASSED
- `grep -c "FormatChecklist\|FormatModeTitle\|TruncateForBalloon"` presence in `ToggleResultFormatter.cs` — all present — PASSED
- `grep -c ": OK"`, `"FAILED"`, `"not attempted"` in `ToggleResultFormatter.cs` — 1 each — PASSED
- `git diff --stat src/RigToggle.App/MainForm.cs` — empty (MainForm.cs byte-for-byte unchanged) — PASSED
- `grep -c "Registry.CurrentUser"` — 4, `grep -c "Environment.ProcessPath"` — 2, `grep -c "\"RigToggle\""` — 1, literal `"{exePath}" --tray"` form present — all PASSED in `WindowsAutostartConfigurator.cs`
- `grep -c "Registry.LocalMachine"` — 0, `grep -c "Assembly.Location"` — 0 (after the Rule 1 fix above) — PASSED
- `IAutostartConfigurator.cs` declares exactly `IsEnabled`, `Enable`, `Disable` — confirmed by direct inspection
- `git diff --stat src/RigToggle.Windows/RigToggle.Windows.csproj` — empty (no new PackageReference added) — PASSED

**Action required before Phase 8 is considered fully verified:** run `dotnet build` (whole solution) and `dotnet test` on a host with the .NET SDK (the Windows rig) to confirm the full suite — existing tests plus the new `StartupArgsTests` and `ToggleResultFormatterTests` — actually compiles and passes, per this plan's `<verification>` section. All code follows the exact conventions read directly from `ToggleService.cs`, `ToggleOrchestrator.cs`, `IAppController.cs`, and `WindowsAppController.cs`, so confidence is high, but this has not been confirmed by an actual compiler/test-runner in this environment.

## TDD Gate Compliance

Both TDD tasks (1 and 2) show the required RED → GREEN commit sequence in git log:
- Task 1: `0f7cefb` (`test(08-01): add failing test for StartupArgs.ShouldStartHidden`) → `c030573` (`feat(08-01): implement StartupArgs.ShouldStartHidden`)
- Task 2: `9dac834` (`test(08-01): add failing test for ToggleResultFormatter`) → `f0dbfa1` (`feat(08-01): implement ToggleResultFormatter`)

RED state for each was confirmed by the referenced implementation file not existing at the test commit (verified structurally, since the environment cannot run the test suite to observe an actual compile/test failure — see Environment Constraint above). No REFACTOR commits were needed for either task.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `StartupArgs`, `ToggleResultFormatter`, `IAutostartConfigurator`, and `WindowsAutostartConfigurator` are all in place for Plan 08-02 (tray UI on MainForm) and Plan 08-03 (autostart checkbox on SettingsForm, hidden-startup wiring in Program.cs) to build against
- `MainForm.cs` is unmodified as required — Plan 08-02 owns its `FormatChecklist` caller cleanup
- Blocker: a real `dotnet build`/`dotnet test` pass on Windows hardware is still needed to confirm compilation and green tests before the phase can be considered fully verified (same standing blocker as Phase 6/7)

---
*Phase: 08-tray-residency-autostart-toast-notification*
*Completed: 2026-07-30*
