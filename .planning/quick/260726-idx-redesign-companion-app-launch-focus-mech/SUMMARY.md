---
phase: quick-260726-idx
plan: 01
status: complete
subsystem: app-control
tags: [winforms, process-start, shellexecute, drag-and-drop, win32, p-invoke]

# Dependency graph
requires:
  - phase: quick-260726 (debug session moza-foreground-focus)
    provides: FindBestMainWindow window-selection heuristic, IsRunning process-name matching, debug.log Trace wiring
provides:
  - Unconditional ShellExecute relaunch replacing the window-focus dance in WindowsAppController.LaunchOrFocus
  - Trimmed NativeMethods P/Invoke surface (only what MinimizeIfRunning/FindBestMainWindow still need)
  - Settings drag-and-drop (.lnk/.exe) configuration alongside Browse
  - H9 close-button limitation reframed as believed-resolved pending rig verification
affects: [app-control, settings-ui, known-limitations]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "App-agnostic single-instance activation: relaunch via Process.Start(UseShellExecute=true) instead of window-handle focus/enumeration, relying on the target app's own activation logic"
    - "Settings drag-and-drop: AllowDrop + DragEnter/DragDrop wired declaratively in the Designer partial class, validated by a single-file/extension-whitelist TryGetX pattern shared with DragEnter/DragDrop handlers"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsAppController.cs
    - src/RigToggle.Windows/NativeMethods.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/SettingsForm.Designer.cs
    - .planning/debug/knowledge-base.md
    - .planning/STATE.md

key-decisions:
  - "Relaunch instead of focus: LaunchOrFocus no longer enumerates/manipulates windows at all -- ShellExecute relaunch + single-instance self-activation is simpler and app-agnostic, and eliminates the trigger for the H9 inert-close-button symptom"
  - "MinimizeIfRunning stays exactly as-is (still needs real window control) -- redesign scope was deliberately limited to the launch path only"
  - ".lnk configured as launch target is accepted verbatim (no shortcut resolution) -- ShellExecute handles both .lnk and .exe at launch time, but this means MinimizeIfRunning's process-name derivation may not match a .lnk-configured target; documented as a known interaction, not patched"

patterns-established:
  - "Pattern: single-instance app activation via unconditional relaunch (ShellExecute) rather than window-handle focus manipulation"

requirements-completed: [APP-02]

duration: ~25min
completed: 2026-07-26
---

# Phase quick-260726-idx Plan 01: Redesign Companion App Launch/Focus Mechanism Summary

**Replaced the window-enumeration/focus dance in `WindowsAppController.LaunchOrFocus` with an unconditional `Process.Start(UseShellExecute=true)` relaunch, trimmed the now-dead Win32 P/Invoke surface, and generalized Settings to accept any `.lnk`/`.exe` target via Browse or drag-and-drop.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 3/3 completed
- **Files modified:** 6 (2 source files rewritten in Task 1, 2 source files edited in Task 2, 2 docs files edited in Task 3)

## Accomplishments
- `LaunchOrFocus` is now a single unconditional `Process.Start(new ProcessStartInfo { FileName = ..., UseShellExecute = true })` relaunch — no `IsRunning` check, no window enumeration, no focus calls on the launch path.
- Removed all now-dead code: `FocusWindow`, `LaunchFreshAndFocus`, `IsSystemCloseGrayed`, the H10 poll/fallback fields and block, and every `NativeMethods` P/Invoke/constant with no remaining caller (`SetForegroundWindow`, `SetWindowPos` + z-order constants, `GetForegroundWindow`, `IsWindowVisible`, `IsIconic`, `IsWindowEnabled`, `GetClassName`, `GetWindowRect`, `GetSystemMenu`, `GetMenuState` + menu constants, `SW_SHOW`, `SW_RESTORE`).
- `IsRunning`, `FindBestMainWindow` (selection logic intact, diagnostic-only per-candidate/result logging trimmed), and `MinimizeIfRunning` survive unchanged behaviorally and still compile against the trimmed `NativeMethods` surface.
- Settings accepts and validates an existing `.lnk` or `.exe` (renamed `IsValidExePath` → `IsValidLaunchTarget`), via both the existing Browse flow (dialog filter/title generalized) and new drag-and-drop wired on the app-path group box and text field.
- `AppSettings.CompanionAppPath` persisted field name is unchanged — only UX/labels generalized toward "target app" phrasing.
- `.planning/STATE.md` and `.planning/debug/knowledge-base.md` updated: H9 (Moza close-button inert) reframed as believed-resolved by this redesign, pending rig verification; the `.lnk`-vs-process-name interaction for `MinimizeIfRunning`/`IsRunning` is documented as a known, unpatched interaction.

## Task Commits

1. **Task 1: Replace already-running focus dance with unconditional ShellExecute relaunch + remove dead window-hunting code** - `09c758a` (refactor)
2. **Task 2: Generalize Settings to a "target app" with drag-and-drop (.lnk or .exe) alongside Browse** - `e4c2806` (feat)
3. **Task 3: Update knowledge-base and STATE.md — H9 believed resolved (pending rig verification)** - `792f976` (docs)

**Plan metadata:** (this commit, docs: complete plan — see final commit below)

## Files Created/Modified
- `src/RigToggle.Windows/WindowsAppController.cs` - `LaunchOrFocus` rewritten to unconditional ShellExecute relaunch; dead focus/poll code removed; `FindBestMainWindow`/`MinimizeIfRunning`/`IsRunning` preserved
- `src/RigToggle.Windows/NativeMethods.cs` - Trimmed to only the P/Invoke surface `FindBestMainWindow`/`MinimizeIfRunning` still use (`EnumWindows`, `GetWindowThreadProcessId`, `GetWindow`+`GW_OWNER`, `GetWindowTextLength`, `GetWindowText`, `GetWindowPlacement`+structs, `ShowWindow`+`SW_MINIMIZE`)
- `src/RigToggle.App/SettingsForm.cs` - `IsValidExePath` → `IsValidLaunchTarget` (accepts `.exe` or `.lnk`); added `AppPath_DragEnter`/`AppPath_DragDrop`/`TryGetSingleDroppedLaunchTarget`; generalized stale-warning/placeholder wording
- `src/RigToggle.App/SettingsForm.Designer.cs` - `grpAppPath`/`txtAppPath` `AllowDrop = true` + `DragEnter`/`DragDrop` wiring; `dlgOpenExe` filter/title generalized to `.lnk`/`.exe`; group box label "Target App"
- `.planning/debug/knowledge-base.md` - `moza-foreground-focus` entry's known-limitation line updated to note the believed-resolved follow-up redesign, cross-referencing this quick task
- `.planning/STATE.md` - Known Limitations: H9 entry reframed as believed-resolved pending rig verification; new entry documenting the `.lnk`/process-name minimize interaction

## Decisions Made
- Relaunch-based activation was chosen over continuing to patch the window-focus heuristics, because it is both simpler and structurally removes the trigger for the H9 inert-close-button symptom (RigToggle no longer ever calls `SetForegroundWindow`/`SetWindowPos`/etc. on a window it doesn't own).
- `MinimizeIfRunning` was deliberately left untouched (per the plan's explicit scope boundary) since toggle-back legitimately needs real window control and no evidence suggests it has the same problem as the launch path.
- Accepted the documented (not patched) `.lnk`-vs-process-name interaction for `MinimizeIfRunning`/`IsRunning` rather than expanding scope to resolve `.lnk` targets to their underlying `.exe` — out of scope for this redesign per the plan.

## Deviations from Plan

None — plan executed exactly as written. All three tasks completed per their `<action>` blocks; no Rule 1-4 deviations were needed.

## Issues Encountered

- No dotnet SDK is available in this sandbox (Linux, Windows-only .NET project), so the plan's `dotnet build` verification steps could not run. Per the plan's own instructions, fell back to grep-based zero-reference verification for every symbol in the DEAD list (all confirmed 0 real code references repo-wide — the few remaining hits for `SetForegroundWindow`/`IsWindowVisible`/`IsIconic`/`GetWindowRect` are prose mentions inside doc comments in the two edited files, not declarations or call sites) and confirmed the new drag-and-drop wiring and `.lnk` handling via targeted grep. No build/runtime verification was possible; the user must build and rig-test.

## User Setup Required

None — no external service configuration required. However, **the user must build and rig-test this change** (see "Next Phase Readiness" below) since this sandbox cannot compile or run the .NET/WinForms code.

## Next Phase Readiness

**Ready for the user to build and rig-test.** Specifically verify on the rig:
1. Toggling to rig mode with Moza Companion **not** running: the app launches normally.
2. Toggling to rig mode with Moza Companion **already running**: the app self-activates/comes to the foreground (single-instance behavior) instead of a duplicate instance launching.
3. **H9 check:** with the companion app foregrounded via toggle, confirm its window close (X) button, Alt+F4, and taskbar "Close window" now work normally (this was the inert-close-button limitation this redesign is believed to resolve).
4. Toggle back to normal mode: `MinimizeIfRunning` still minimizes the companion window as before (unchanged code path).
5. In Settings: configure the target app via Browse (should accept both `.exe` and `.lnk` files) and via drag-and-drop of a `.lnk` shortcut or `.exe` onto the "Target App" box.
6. If a `.lnk` is configured as the target: note that toggle-back minimize may no-op (documented, expected, not a bug) since process-name matching is derived from the configured path.

No blockers. If rig verification finds the close-button issue persists, the STATE.md/knowledge-base entries should be updated from "believed resolved" back to an open limitation, and a fresh debug session opened rather than re-chasing the prior read-only diagnostics.

## Self-Check: PASSED

- FOUND: src/RigToggle.Windows/WindowsAppController.cs
- FOUND: src/RigToggle.Windows/NativeMethods.cs
- FOUND: src/RigToggle.App/SettingsForm.cs
- FOUND: src/RigToggle.App/SettingsForm.Designer.cs
- FOUND: .planning/debug/knowledge-base.md
- FOUND: .planning/STATE.md
- FOUND commit 09c758a in git log
- FOUND commit e4c2806 in git log
- FOUND commit 792f976 in git log

---
*Phase: quick-260726-idx*
*Completed: 2026-07-26*
