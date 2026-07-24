---
phase: 03-app-audio-control
plan: 03
subsystem: infra
tags: [win32, pinvoke, process-control, dotnet, csharp]

# Dependency graph
requires:
  - phase: 02-foundations-gui-shell
    provides: IAppController abstraction, WindowsAppController.IsRunning, Settings persistence (AppSettings.CompanionAppPath)
provides:
  - Real user32.dll ShowWindow/SetForegroundWindow P/Invoke wrapper (NativeMethods)
  - Real WindowsAppController.LaunchOrFocus (launch-if-absent with Refresh()-aware poll, focus-if-present, no duplicate launch)
  - Real WindowsAppController.MinimizeIfRunning (best-effort minimize, zero-handle no-op)
  - D-05 companion-app-path preflight in ToggleService.ToggleToRigMode (fail-fast before any capture/persist/mutation)
affects: [phase-04-monitor-restore, phase-05-orchestration-packaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Hand-rolled internal static NativeMethods class for user32.dll P/Invoke (ShowWindow/SetForegroundWindow), no PInvoke.User32 package dependency"
    - "Process.Refresh()-aware poll loop (250ms interval, 10s timeout) before every MainWindowHandle read on a freshly-launched process"
    - "Get-processes-by-name / try / dispose-all-in-finally idiom reused for both focus and minimize re-enumeration"
    - "Guard-clause preflight ordering in ToggleService: IsFullyConfigured -> File.Exists(CompanionAppPath) -> CaptureState -> Save -> mutate"

key-files:
  created:
    - src/RigToggle.Windows/NativeMethods.cs
  modified:
    - src/RigToggle.Windows/WindowsAppController.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/ToggleServiceTests.cs

key-decisions:
  - "LaunchOrFocus polls (Refresh()-aware, 250ms/10s) only on the fresh-launch branch; the already-running branch checks the window handle once with no polling, per D-06's tray-only-case rule."
  - "D-05 preflight is a plain inline File.Exists check in ToggleService, not a new IAppController interface method — matches 03-RESEARCH.md Open Question 2's recommendation."
  - "Happy-path test fixture CompanionAppPath switched from a fictional Program Files path to a real Path.GetTempFileName() file so the new D-05 preflight doesn't break pre-existing passing facts."

requirements-completed: [APP-01, APP-02, APP-03]

# Metrics
duration: 12min
completed: 2026-07-24
---

# Phase 3 Plan 3: Real App Launch/Focus/Minimize + D-05 Preflight Summary

**Hand-rolled user32 P/Invoke drives real companion-app launch/focus/minimize control, and a File.Exists guard in ToggleService now fails fast before touching any state when the companion app path is missing.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-24T16:48Z (context load)
- **Completed:** 2026-07-24T16:53:26Z
- **Tasks:** 2/2 completed
- **Files modified:** 4 (1 created, 3 modified)

## Accomplishments
- `NativeMethods.cs` created with hand-rolled `ShowWindow`/`SetForegroundWindow` `DllImport("user32.dll")` signatures — no third-party P/Invoke wrapper package.
- `WindowsAppController.LaunchOrFocus` replaced its Phase-2 no-op: launches via `Process.Start` when absent (with a `Refresh()`-aware 250ms/10s poll loop before best-effort `SetForegroundWindow`), and re-enumerates + best-effort focuses the existing window when already running — never launching a duplicate, and never polling in the tray-only zero-handle case.
- `WindowsAppController.MinimizeIfRunning` replaced its Phase-2 no-op: best-effort `ShowWindow(handle, SW_MINIMIZE)` when a live window handle exists; a zero handle or not-running state is a silent no-op.
- `ToggleService.ToggleToRigMode` gained a second guard clause — `File.Exists(settings.CompanionAppPath)` — inserted after the existing `IsFullyConfigured` check and before `CaptureState`, so a missing/moved companion app fails before any monitor/audio state is captured, persisted, or mutated (D-05).
- New regression test `ToggleToRigMode_Throws_WhenCompanionAppPathDoesNotExist` proves the throw occurs with zero `snapshot.Save` calls recorded.

## Task Commits

Each task was committed atomically:

1. **Task 1: NativeMethods P/Invoke + real LaunchOrFocus/MinimizeIfRunning** - `2b9b55f` (feat)
2. **Task 2: D-05 app-path preflight in ToggleService + regression test** - `714d9a2` (feat)

_Note: both tasks are `type="auto"`, no TDD gate applies to this plan (frontmatter has no `tdd="true"`)._

## Files Created/Modified
- `src/RigToggle.Windows/NativeMethods.cs` - New internal static class: `ShowWindow`/`SetForegroundWindow` DllImports + `SW_MINIMIZE` constant
- `src/RigToggle.Windows/WindowsAppController.cs` - `LaunchOrFocus`/`MinimizeIfRunning` rewritten from no-op stubs to real Win32 process/window control; class `<summary>` updated to describe the real mechanism and cite D-06/D-07
- `src/RigToggle.Core/ToggleService.cs` - Added `File.Exists(settings.CompanionAppPath)` guard clause in `ToggleToRigMode`; updated method `<summary>` to document the new preflight and its ordering guarantee
- `src/RigToggle.Tests/ToggleServiceTests.cs` - Added `ExistingCompanionAppPath` (real temp file) fixture, repointed `ConfiguredSettings.CompanionAppPath` at it, added `ToggleToRigMode_Throws_WhenCompanionAppPathDoesNotExist`

## Decisions Made
- Followed 03-RESEARCH.md Pattern 4 (Refresh()-aware poll, 250ms/10s) and Pattern 5 (P/Invoke signatures) verbatim for the launch/focus mechanism.
- Followed 03-RESEARCH.md Open Question 2's recommendation: D-05 preflight is a plain inline `File.Exists` call, not a new `IAppController` interface method — keeps the interface unchanged and the check colocated with the other `ToggleToRigMode` guard clause.
- Test fixture change (temp file instead of a fictional path) was required by the plan itself (Task 2 action) to keep all four pre-existing happy-path facts passing under the new preflight — not a deviation, this was explicitly specified.

## Deviations from Plan

None - plan executed exactly as written. Both tasks' action/acceptance-criteria/done clauses were implemented as specified, including the explicit "don't poll on the already-running zero-handle branch" and "object-initializer copy, not `with` expression" details called out in the plan's `<interfaces>`/`<action>` blocks.

## Issues Encountered

`dotnet` is not installed in this sandbox environment (confirmed via `which dotnet`), so the plan's own `<verify>` steps (`dotnet build src/RigToggle.Windows/RigToggle.Windows.csproj` and `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj`) could not be executed here — this matches the plan's own `<verification>` note ("no dotnet in this sandbox — build/run on the Windows dev machine per Environment Availability note"). Code was manually reviewed against the plan's acceptance criteria (P/Invoke signatures, no-duplicate-launch branch guard, zero-handle no-poll/no-fail paths, guard-clause ordering, no `with` expression, no `FindWindow`/`AttachThreadInput`/elevation additions, no new NuGet packages) instead. **The user should run `dotnet build` and `dotnet test` on the Windows dev machine before relying on this code.**

## User Setup Required

None - no external service configuration required. Recommended manual verification step: run `dotnet build src/RigToggle.Windows/RigToggle.Windows.csproj` and `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` on the Windows dev machine (this sandbox has no `dotnet` installed).

## Next Phase Readiness

APP-01/APP-02/APP-03 are functionally complete pending the Windows-machine build/test verification noted above. This plan is independent of Plans 01/02 (audio control) per its own frontmatter (`depends_on: []`, wave 1) — no interface or shared-file changes were made that would affect those plans' merge. `ToggleService.cs` now has two sequential guard clauses (`IsFullyConfigured`, then `File.Exists`) before any state capture; future phases modifying `ToggleToRigMode` should preserve this ordering.

---
*Phase: 03-app-audio-control*
*Completed: 2026-07-24*
