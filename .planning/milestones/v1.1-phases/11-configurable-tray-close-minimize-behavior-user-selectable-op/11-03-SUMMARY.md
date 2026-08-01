---
phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op
plan: 03
subsystem: ui
tags: [winforms, settings-dialog, tray-icon, dependency-injection]

# Dependency graph
requires:
  - phase: 11-01
    provides: "AppSettings.CloseMinimizesToTray / MinimizeToTray fields, migration default"
  - phase: 11-02
    provides: "MainForm.ApplyTrayVisibility() public method and the CloseReason.UserClosing / Resize interception it feeds"
provides:
  - "Two SettingsForm checkboxes (chkCloseMinimizesToTray, chkMinimizeToTray) at UI-SPEC coordinates, grouped directly above Start with Windows"
  - "Load/Save round-trip for both checkboxes through AppSettings, plain-assignment pattern (no error path)"
  - "Live tray-icon visibility update on Settings Save via an injected Action (_applyTrayVisibility), wired from Program.cs's composition root to mainForm.ApplyTrayVisibility"
affects: [11-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Injected-delegate DI for cross-form callbacks (Action _applyTrayVisibility mirrors the existing Func<bool> _tryRegisterConfiguredHotkey shape) — SettingsForm still takes no direct MainForm dependency"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "Followed the chkEnableDebugLogging plain-AppSettings-round-trip pattern for both new checkboxes, not chkStartWithWindows's registry-backed try/catch pattern — neither new field has a runtime failure path, so no ErrorProvider/warning label was added for either."
  - "_applyTrayVisibility() is invoked immediately after _settingsStore.Save(settingsToSave) and before the autostart/hotkey blocks, so it always runs even when the later hotkey-registration step resets DialogResult to None and keeps the dialog open."

patterns-established:
  - "Injected-delegate DI for cross-form callbacks: add a constructor Action/Func parameter, null-guard with ArgumentNullException, mirror an existing sibling delegate's shape — reused instead of a direct MainForm reference"

requirements-completed: [TRAY-01]

duration: ~5min
completed: 2026-08-01
---

# Phase 11 Plan 03: Settings Checkboxes + Live Tray-Visibility Apply Summary

**Added the two Settings checkboxes (`chkCloseMinimizesToTray`, `chkMinimizeToTray`) at the UI-SPEC-prescribed coordinates, wired their Load/Save round-trip through `AppSettings`, and made Settings-Save apply the derived tray-icon visibility live via a newly injected `Action` delegate from `Program.cs`'s composition root.**

## Performance

- **Duration:** ~5 min (sandboxed environment; wall-clock not representative of a real session)
- **Tasks:** 2/2 completed
- **Files modified:** 3

## Accomplishments
- `SettingsForm.Designer.cs` now declares/instantiates `chkCloseMinimizesToTray` and `chkMinimizeToTray` at `(12, 600)`/`(12, 632)` respectively, with `chkStartWithWindows`, `lblAutostartWarning`, both Save/Discard buttons, and `ClientSize` all shifted down 64px to `(420, 768)` per the UI-SPEC "D-07 Resolution" table — no new `GroupBox`, no `ErrorProvider`/warning label added for either new checkbox.
- `SettingsForm.cs` loads both checkboxes from `_settings.CloseMinimizesToTray`/`_settings.MinimizeToTray` on open and writes them back into the `settingsToSave` initializer on Save — both are plain, unconditional boolean assignments with no try/catch, matching `chkEnableDebugLogging`'s established pattern, not `chkStartWithWindows`'s registry-backed one.
- `SettingsForm` gained a new `Action applyTrayVisibility` constructor parameter (and `_applyTrayVisibility` field), null-guarded exactly like the existing `_tryRegisterConfiguredHotkey` delegate, so `SettingsForm` still has zero direct dependency on `MainForm`.
- `BtnSaveSettings_Click` now calls `_applyTrayVisibility()` immediately after `_settingsStore.Save(settingsToSave)` and before the autostart/hotkey blocks — satisfies D-08 (tray icon appears/disappears immediately on Save, never only after restart) and ensures the call still fires even if the hotkey-registration step later resets `DialogResult` to `None`.
- `Program.cs`'s `SettingsFormFactory` now passes `mainForm.ApplyTrayVisibility` as the new final constructor argument, using the same pre-declared-`mainForm`-captured-by-reference pattern already established for `mainForm.TryRegisterConfiguredHotkey`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add the two checkbox controls and relayout SettingsForm.Designer.cs** - `4fecd05` (feat)
2. **Task 2: Load/Save wiring + live-apply Action in SettingsForm and Program.cs** - `5c30c86` (feat)

_No TDD tasks in this plan (`tdd="false"` on both)._

## Files Created/Modified
- `src/RigToggle.App/SettingsForm.Designer.cs` - Adds `chkCloseMinimizesToTray`/`chkMinimizeToTray` controls at UI-SPEC coordinates; relayouts `chkStartWithWindows`/`lblAutostartWarning`/`btnSaveSettings`/`btnDiscardChanges`/`ClientSize` down 64px
- `src/RigToggle.App/SettingsForm.cs` - New `Action _applyTrayVisibility` field/constructor param with null-guard; Load/Save round-trip for both new checkboxes; `_applyTrayVisibility()` invoked right after `_settingsStore.Save`
- `src/RigToggle.App/Program.cs` - `SettingsFormFactory` passes `mainForm.ApplyTrayVisibility` as the new constructor argument

## Decisions Made
- Used `chkEnableDebugLogging`'s plain-AppSettings-round-trip pattern for both new checkboxes' behavior (per the plan's explicit warning against copying `chkStartWithWindows`'s registry-backed try/catch pattern), while still following `chkStartWithWindows` as the layout/section-placement precedent (tight, contiguous vertical stacking, no new `GroupBox`).
- Placed the `_applyTrayVisibility()` call directly after the settings persist call and before the autostart/hotkey blocks — this guarantees the live tray-visibility update always runs on Save regardless of what happens later in the method (autostart registry write, hotkey registration outcome), since the settings are already durably saved at that point.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched the plan's `<action>` and `<acceptance_criteria>` blocks precisely; no Rule 1-4 fixes were needed.

## Known Stubs

None - no placeholder/mock data introduced; both checkboxes are fully wired to real `AppSettings` fields with a real, already-existing `MainForm.ApplyTrayVisibility()` target method (delivered in Plan 11-02).

## Threat Flags

None - this plan's only new surface (an injected same-process `Action` delegate calling back into `MainForm` to recompute a local UI property) was already covered by the plan's own `<threat_model>` (T-11-06, T-11-07) with no new, unaddressed surface introduced beyond what was specified.

## Verification

**Environment note:** This sandbox has no `dotnet` SDK installed (`which dotnet` returns nothing), matching the established pattern documented across this project's prior plan summaries (07-01, 08-01, Phase 6 precedent, 11-01, 11-02). Verification here was performed via grep-based source assertions against every acceptance criterion listed in the plan:

- `chkCloseMinimizesToTray` / `chkMinimizeToTray`: field decl, instantiation, property block, and `Controls.Add` all present (9 occurrences each, well above the required minimum of 4)
- Locked wording strings (`"Closing the window (X) minimizes to tray"`, `"Minimizing the window also sends it to tray"`) each appear exactly once
- `chkCloseMinimizesToTray.Location` = `Point(12, 600)`; `chkStartWithWindows.Location` moved to `Point(12, 664)`; `ClientSize` = `Size(420, 768)` — all confirmed present exactly once
- No `errCloseMinimize`/`errMinimizeToTray`/`lblCloseWarning`/`lblMinimizeWarning` tokens anywhere in the Designer file (grep count 0)
- `private readonly Action _applyTrayVisibility;` present once, constructor null-guards it with `ArgumentNullException`
- `chkCloseMinimizesToTray.Checked = _settings.CloseMinimizesToTray;` and `chkMinimizeToTray.Checked = _settings.MinimizeToTray;` each present once in `SettingsForm_Load`
- `CloseMinimizesToTray = chkCloseMinimizesToTray.Checked,` and `MinimizeToTray = chkMinimizeToTray.Checked,` each present once inside the `settingsToSave` initializer
- `_applyTrayVisibility();` confirmed positioned between `_settingsStore.Save(settingsToSave);` and the autostart `try` block (by direct file read, not just grep)
- Both checkboxes appear only as plain assignments in `SettingsForm.cs` — no try/catch, no `_autostartConfigurator` call, no error-provider wiring around either
- `mainForm.ApplyTrayVisibility` present once in `Program.cs`, confirmed the target method (`public void ApplyTrayVisibility()`, MainForm.cs:111) already exists from Plan 11-02
- Confirmed no other call sites construct `SettingsForm` that would need updating for the new constructor parameter (`grep -rn "new SettingsForm(" src/` returns exactly the one Program.cs call site)

**Still required before this plan is considered fully verified:** `dotnet build` (both `RigToggle.App.csproj` and the full solution) and `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` must be run on a host with the .NET SDK (the Windows rig) to catch any compile-time issue grep-based verification cannot detect (e.g. Designer.cs syntax, WinForms-specific attribute ordering). Live-on-Save tray-icon appearance/disappearance behavior is explicitly deferred to the 11-04 human-verify checkpoint per this plan's own `<verification>` section — it is WinForms interaction behavior, not unit-testable here.

## Self-Check: PASSED

- FOUND: `src/RigToggle.App/SettingsForm.Designer.cs` contains `chkCloseMinimizesToTray` and `chkMinimizeToTray`
- FOUND: `src/RigToggle.App/SettingsForm.cs` contains `_applyTrayVisibility` field, constructor param, Load/Save wiring
- FOUND: `src/RigToggle.App/Program.cs` contains `mainForm.ApplyTrayVisibility`
- FOUND: commit `4fecd05` (Task 1) in `git log --oneline`
- FOUND: commit `5c30c86` (Task 2) in `git log --oneline`
