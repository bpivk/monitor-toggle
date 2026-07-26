---
phase: quick-260726-jti
plan: 01
status: complete
subsystem: ui
tags: [winforms, settings, debug-logging, cleanup]

# Dependency graph
requires:
  - phase: quick-260726-idx
    provides: relaunch-based companion app launch/focus redesign that made the
      MainForm companion status line meaningless (no reliable running/not-running
      signal left worth surfacing)
provides:
  - AppSettings.EnableDebugLogging (off-by-default) settings flag
  - Settings-form checkbox that round-trips the flag through load/save
  - Program.cs trace-listener wiring gated behind the loaded flag
  - MainForm with the dead companion-status label and IAppController dependency removed
affects: [settings-form, main-form, program-composition-root]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Settings must load before any best-effort startup wiring that depends on a
       settings flag; failure of that load defaults to the safer/off value rather
       than throwing or defaulting to on."

key-files:
  created: []
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MainForm.Designer.cs

key-decisions:
  - "Debug logging defaults to OFF; if settings.Load() throws during startup, fail
     toward off (new AppSettings()) rather than risk enabling logging or crashing."
  - "Removed lblCompanionStatus and IAppController from MainForm entirely rather than
     leaving a dead/hidden control, since the underlying running/not-running signal
     is no longer meaningful after the relaunch-based launch redesign (260726-idx)."

patterns-established:
  - "Composition root (Program.cs) loads settings once and reuses the same
     JsonSettingsStore instance for both the startup gate and downstream
     ToggleService/SettingsForm wiring — never construct the store twice."

requirements-completed: []

# Metrics
duration: 4min
completed: 2026-07-26
---

# Quick Task 260726-jti: Gate debug.log Behind a Settings Toggle Summary

**Debug.log is now opt-in via a Settings checkbox (default off), and MainForm's dead "Moza Companion: Running/Not running" status line plus its unused IAppController dependency are removed.**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-07-26T14:18:49Z (pre-dispatch plan commit)
- **Completed:** 2026-07-26T14:22:16Z
- **Tasks:** 2
- **Files modified:** 6 (AppSettings.cs, SettingsForm.cs, SettingsForm.Designer.cs, Program.cs, MainForm.cs, MainForm.Designer.cs)

## Accomplishments
- Fresh installs / default settings no longer write `%LOCALAPPDATA%\RigToggle\debug.log` on every run.
- A new "Enable debug logging" checkbox in Settings lets the user opt back in; the flag round-trips through load/save and re-wires the `TextWriterTraceListener` on next restart.
- Removed MainForm's now-meaningless companion-app status line and its `IAppController` field/ctor-param, eliminating a would-be unused-dependency warning.

## Task Commits

Each task was committed atomically:

1. **Task A: Gate debug.log behind an EnableDebugLogging Settings toggle** - `d0e4636` (feat)
2. **Task B: Remove Moza Companion status line and dead IAppController from MainForm** - `309112b` (refactor)

**Plan metadata:** (this commit, docs)

## Files Created/Modified
- `src/RigToggle.Core/Models/AppSettings.cs` - Added `EnableDebugLogging` bool property (defaults false)
- `src/RigToggle.App/SettingsForm.Designer.cs` - Added `chkEnableDebugLogging` checkbox, moved Save/Discard buttons down, grew ClientSize to 420x408
- `src/RigToggle.App/SettingsForm.cs` - Load/save the checkbox value against `AppSettings.EnableDebugLogging`
- `src/RigToggle.App/Program.cs` - Reordered so `settingsStore.Load()` happens before the trace-listener block; wrapped the existing `TextWriterTraceListener` wiring in `if (settings.EnableDebugLogging)`; dropped the `appController` argument from the `MainForm` constructor call (the local itself still flows to `ToggleService`)
- `src/RigToggle.App/MainForm.cs` - Removed `_appController` field/ctor-param/assignment and the `companionRunning`/`lblCompanionStatus` computation in `RefreshUi()`; updated class and method doc comments
- `src/RigToggle.App/MainForm.Designer.cs` - Removed the `lblCompanionStatus` label entirely (instantiation, configuration block, Controls.Add, field declaration)

## Decisions Made
- Fail-toward-off: if `settingsStore.Load()` throws during the pre-trace-listener load in `Program.cs`, default to a fresh `AppSettings()` (which has `EnableDebugLogging = false`) rather than let the exception propagate or default to logging-on.
- Left `MainForm`'s `ClientSize` (320x200) unchanged after removing the status label, per the plan — no other controls needed to shift since the label was the last one at the bottom.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched the plan's interface facts (line numbers, control names, method names) exactly as documented in the plan's `<interfaces>` section.

## Issues Encountered

**No dotnet SDK available in this Linux sandbox** (expected — this is a Windows-only .NET project). Fell back to the plan's documented grep-based verification for both tasks:
- Task A: `grep -q "EnableDebugLogging" AppSettings.cs && grep -q "chkEnableDebugLogging" SettingsForm.Designer.cs && grep -q "if (settings.EnableDebugLogging)" Program.cs` → `GATE_OK`
- Task B: `! grep -q "lblCompanionStatus" MainForm.Designer.cs && ! grep -q "_appController" MainForm.cs && grep -q "new MainForm(toggleService, settingsStore" Program.cs` → `CLEANUP_OK`

A real `dotnet build` has not been run against these changes. Since these are mechanical, well-specified edits (property addition, designer boilerplate following an exact existing pattern, straightforward field/parameter removal with no remaining references confirmed via grep), the risk of a compile error is low, but this should be confirmed with an actual Windows build/run before the next rig session, per "User Setup Required" below.

## User Setup Required

**Recommended before next rig use:** Run `dotnet build` (or open in Visual Studio) on a Windows machine to confirm a clean build with zero warnings/errors, since this sandbox has no dotnet SDK to verify against. Then:
1. Launch the app with default/existing settings and confirm `%LOCALAPPDATA%\RigToggle\debug.log` is NOT created/appended.
2. Open Settings, check "Enable debug logging...", Save, restart the app, and confirm `debug.log` IS now being written.
3. Confirm the MainForm window no longer shows a "Moza Companion: ..." status line and that the window looks correct (no leftover blank space).

## Next Phase Readiness

No further work implied by this quick task. Both cleanups were independent, self-contained follow-ups to the closed H9 investigation and do not block or depend on any other in-flight work.

---
*Phase: quick-260726-jti*
*Completed: 2026-07-26*

## Self-Check: PASSED

All 6 modified files found on disk; both task commits (`d0e4636`, `309112b`) found in git log.
