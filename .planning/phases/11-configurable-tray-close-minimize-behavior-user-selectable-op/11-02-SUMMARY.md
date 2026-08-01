---
phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op
plan: 02
subsystem: ui
tags: [winforms, notifyicon, tray, mainform]

# Dependency graph
requires:
  - phase: 11-01
    provides: AppSettings.CloseMinimizesToTray / AppSettings.MinimizeToTray boolean fields
provides:
  - Shared SendToTray() hide-to-tray helper used by both Close and Minimize paths
  - Conditional MainForm_FormClosing gated on AppSettings.CloseMinimizesToTray (D-01)
  - Public ApplyTrayVisibility() deriving notifyIcon.Visible from CloseMinimizesToTray || MinimizeToTray (D-08/D-09), wired into InitializeTrayState()
  - MainForm_Resize handler intercepting minimize into hide-to-tray when AppSettings.MinimizeToTray is true (D-04/D-05)
  - Designer notifyIcon.Visible default flipped from true to false (D-11), Resize event subscription added
affects: [11-03, 11-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Shared hide-to-tray helper (SendToTray) reused by both Close and Minimize event handlers to prevent the two paths from drifting apart"
    - "Derived NotifyIcon.Visible computed from an OR of two independent settings, recomputed via a public helper callable both at startup and on demand"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MainForm.Designer.cs

key-decisions:
  - "SendToTray() is a one-line private helper wrapping Hide(), shared by MainForm_FormClosing and MainForm_Resize, per the plan's explicit 'do not duplicate Hide()' instruction"
  - "ApplyTrayVisibility() is public (not private) so it can be called from InitializeTrayState() at startup and, in a future plan, from Settings-Save to apply the derived rule live"
  - "notifyIcon.Visible Designer default changed from true to false so the icon's actual visibility is always startup-derived, never a hardcoded flash"

patterns-established:
  - "Pattern: shared no-arg private helper for a hide/state-transition action invoked from multiple event handlers, to keep multi-entry-point behavior from drifting"

requirements-completed: [TRAY-01]

# Metrics
duration: ~12min
completed: 2026-08-01
---

# Phase 11 Plan 02: MainForm Close/Minimize Behavior Summary

**Close (X) and minimize now conditionally hide to tray based on two independent AppSettings booleans, with tray icon visibility derived live from their OR.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-08-01T19:24:00Z (approx, no explicit start marker recorded)
- **Completed:** 2026-08-01T19:36:14Z
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments
- `MainForm_FormClosing` no longer unconditionally redirects the X button to tray — it now checks `AppSettings.CloseMinimizesToTray` (default false per D-02), so a fresh/upgraded install has X exit the app until the user opts in via Settings
- Added a public `ApplyTrayVisibility()` that derives `notifyIcon.Visible` from `CloseMinimizesToTray || MinimizeToTray` and is called from `InitializeTrayState()` at startup, so the tray icon is only ever present when at least one tray-hiding preference is active (D-08/D-09)
- Added `MainForm_Resize` so minimizing the window also hides it to tray when `MinimizeToTray` is true (D-04), while leaving standard OS minimize untouched when it's false (D-05)
- Both hide paths (Close, Minimize) route through one shared `SendToTray()` helper — there is exactly one `Hide()` call in the file, confirmed via grep

## Task Commits

Each task was committed atomically:

1. **Task 1: Shared SendToTray helper, conditional Close, and derived ApplyTrayVisibility** - `9dde513` (feat)
2. **Task 2: Minimize-to-tray interception via MainForm_Resize** - `fa428df` (feat)

**Plan metadata:** (this commit, made after this file)

## Files Created/Modified
- `src/RigToggle.App/MainForm.cs` - Added `SendToTray()`, made `MainForm_FormClosing` conditional on `CloseMinimizesToTray`, added `ApplyTrayVisibility()` wired into `InitializeTrayState()`, added `MainForm_Resize` gated on `WindowState == Minimized && MinimizeToTray`
- `src/RigToggle.App/MainForm.Designer.cs` - Flipped `notifyIcon.Visible` default from `true` to `false`; added `this.Resize += new System.EventHandler(this.MainForm_Resize);` subscription next to the existing `FormClosing` subscription

## Decisions Made
- Followed the plan's explicit reuse requirement: `SendToTray()` is the single `Hide()` call site in the file, called from both `MainForm_FormClosing` and `MainForm_Resize` — no duplication.
- `ApplyTrayVisibility()` made public (per plan interface note) so a later Settings-Save wiring (deferred to a subsequent plan per the phase's task boundaries — 11-02 only covers MainForm's own behavior) can call it without needing a new accessor.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. `dotnet build`/`dotnet test` could not be run in this sandbox (no .NET SDK installed — consistent with the established convention documented in prior phase summaries, e.g. 11-01-SUMMARY.md, 08-01-SUMMARY.md, 07-01-SUMMARY.md). Verification was performed via grep-based source assertions matching every acceptance criterion listed in the plan (see Self-Check below). **`dotnet build src/RigToggle.App/RigToggle.App.csproj` on a host with the .NET SDK (the Windows rig) is still required before this plan is considered fully verified**, along with the deferred behavioral confirmation (X exits vs. hides, minimize-to-tray, live tray-icon existence) at the 11-04 human-verify checkpoint per the plan's own `<verification>` section.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- MainForm's runtime behavior is now fully settings-driven for Close/Minimize per D-01/D-04; `ApplyTrayVisibility()` is exposed publicly and ready to be called from a future Settings-Save handler so the derived tray-icon-visibility rule takes effect live without an app restart (not yet wired here — out of this plan's file scope, which was limited to `MainForm.cs`/`MainForm.Designer.cs`).
- Behavioral confirmation deferred to 11-04's human-verify checkpoint, and a real `dotnet build` is still needed on the Windows rig before this plan can be marked fully verified.

## Known Stubs

None.

## Threat Flags

None - this plan's changes are pure local WinForms window-behavior logic reading only the user's own settings.json, matching the plan's own threat model (Risk: low/none).

## Self-Check

- `SendToTray()` present, single `Hide()` call site: FOUND (grep confirms count 1 for both)
- `MainForm_FormClosing` references `settings.CloseMinimizesToTray`: FOUND (count 2)
- `ApplyTrayVisibility()` public method present: FOUND
- `notifyIcon.Visible = settings.CloseMinimizesToTray || settings.MinimizeToTray` present: FOUND
- `InitializeTrayState()` calls `ApplyTrayVisibility()`: FOUND
- Designer `notifyIcon.Visible = false` present, `= true` absent: FOUND (false count 1, true count 0)
- `MainForm_Resize` present, gated on `WindowState == FormWindowState.Minimized` and `settings.MinimizeToTray`, calls `SendToTray()`: FOUND
- Designer `this.Resize += new System.EventHandler(this.MainForm_Resize);` present: FOUND
- Commit `9dde513` exists in `git log --oneline`: FOUND
- Commit `fa428df` exists in `git log --oneline`: FOUND

## Self-Check: PASSED

---
*Phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op*
*Completed: 2026-08-01*
