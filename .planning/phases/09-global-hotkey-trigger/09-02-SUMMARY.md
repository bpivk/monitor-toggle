---
phase: 09-global-hotkey-trigger
plan: 02
subsystem: windows-interop
tags: [hotkey, p-invoke, win32, wndproc, tray, trig-01]

# Dependency graph
requires:
  - phase: 09-01
    provides: HotkeyCombo modifier constants and AppSettings.HotkeyModifiers/HotkeyKey nullable persisted fields
  - phase: 08-tray-residency-autostart-toast-notification
    provides: NotifyIcon.ShowBalloonTip toast surface, ToggleResultFormatter, TrayToggleMenuItem_Click structural precedent
provides:
  - RigToggle.Windows.GlobalHotkey public wrapper (Register/Unregister + WM_HOTKEY/MOD_* constants)
  - MainForm.TryRegisterConfiguredHotkey / UnregisterConfiguredHotkey / RegisterHotkeyAtStartup
  - MainForm WndProc WM_HOTKEY interception dispatching to HandleHotkeyToggle
affects: [09-03-startup-and-settings-wiring, 09-04-rig-checkpoint]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Public-adapter convention (RigToggle.Windows.GlobalHotkey) re-exposing an internal NativeMethods P/Invoke surface across the assembly boundary, matching WindowsAutostartConfigurator/WindowsAppController/WindowsAudioController/WindowsMonitorController"
    - "Unregister-first registration pattern for idempotent re-registration (TryRegisterConfiguredHotkey), reused by both the Settings-Save path (09-03) and the Settings-dialog-bracketing path (this plan)"

key-files:
  created:
    - src/RigToggle.Windows/GlobalHotkey.cs
  modified:
    - src/RigToggle.Windows/NativeMethods.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "GlobalHotkey is a thin public static class in RigToggle.Windows delegating straight to internal NativeMethods, rather than granting RigToggle.App an InternalsVisibleTo exemption — preserves the existing internal/public encapsulation boundary every other Windows adapter in this project already follows"
  - "Single fixed GlobalHotkeyId (0x9001) constant — only one hotkey exists in this app, so no id-allocation scheme is needed"
  - "OpenSettingsDialog unregisters the hotkey for the entire Settings dialog lifetime (not just around the Save click) — simpler and races-safe by construction versus queuing/ignoring a mid-edit WM_HOTKEY (D-07)"

requirements-completed: [TRIG-01]

# Metrics
duration: 20min
completed: 2026-07-31
---

# Phase 9 Plan 2: Global Hotkey Registration & WndProc Handler Summary

**MainForm now intercepts WM_HOTKEY via a WndProc override and toggles through ToggleOrchestrator with toast-only feedback, backed by a new public GlobalHotkey wrapper in RigToggle.Windows over the internal RegisterHotKey/UnregisterHotKey P/Invoke.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-07-31 (session start)
- **Completed:** 2026-07-31
- **Tasks:** 2 completed
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments

- `NativeMethods` (still `internal`) extended with `RegisterHotKey`/`UnregisterHotKey` user32.dll P/Invoke signatures, matching the file's existing `[DllImport]`/`[return: MarshalAs(UnmanagedType.Bool)]` style
- New `RigToggle.Windows.GlobalHotkey` public static class — the cross-assembly surface RigToggle.App consumes — exposing `WmHotkey`, `ModAlt`/`ModControl`/`ModShift`/`ModWin`/`ModNoRepeat` constants and `Register`/`Unregister` methods that delegate straight to `NativeMethods`
- `MainForm.WndProc` override intercepts `WM_HOTKEY` (matched against the fixed `GlobalHotkeyId`) and dispatches to `HandleHotkeyToggle`, always calling `base.WndProc(ref m)` unconditionally afterward
- `HandleHotkeyToggle` is structurally identical to the Phase 8 `TrayToggleMenuItem_Click` — same try/catch shape (`ToggleInProgressException`, generic `Exception`), same toast-only feedback via `notifyIcon.ShowBalloonTip`, never `MessageBox`
- Three new public MainForm helpers: `TryRegisterConfiguredHotkey()` (unregister-first, reads `AppSettings.HotkeyModifiers`/`HotkeyKey`, returns `true` when nothing is configured or registration succeeds), `UnregisterConfiguredHotkey()` (idempotent no-op guard via `_hotkeyRegistered`), `RegisterHotkeyAtStartup()` (try/catch wrapping `TryRegisterConfiguredHotkey`, traces + toasts the exact D-06 warning wording on failure, never rethrows)
- `OpenSettingsDialog()` now brackets the modal dialog lifetime with `UnregisterConfiguredHotkey()` before `ShowDialog` and `TryRegisterConfiguredHotkey()` after, per D-07

## Task Commits

Each task was committed atomically:

1. **Task 1: RegisterHotKey/UnregisterHotKey P/Invoke + public GlobalHotkey wrapper** - `73313c0` (feat)
2. **Task 2: MainForm WndProc + WM_HOTKEY handler + registration helpers + Settings bracketing** - `3cad06f` (feat)

**Plan metadata:** committed separately after this SUMMARY (see final commit step)

## Files Created/Modified

- `src/RigToggle.Windows/NativeMethods.cs` — Added `RegisterHotKey`/`UnregisterHotKey` P/Invoke signatures with an XML-doc block explaining the internal/public boundary rationale; no existing member touched
- `src/RigToggle.Windows/GlobalHotkey.cs` — New file: `public static class GlobalHotkey` with `WmHotkey`/`Mod*` constants and `Register`/`Unregister` delegating to `NativeMethods`
- `src/RigToggle.App/MainForm.cs` — Added `using RigToggle.Windows;`, `GlobalHotkeyId`/`_hotkeyRegistered` fields, `WndProc` override, `HandleHotkeyToggle`, `TryRegisterConfiguredHotkey`, `UnregisterConfiguredHotkey`, `RegisterHotkeyAtStartup`, and D-07 bracketing in `OpenSettingsDialog`

## Decisions Made

- Kept `NativeMethods` `internal` and did not add an `InternalsVisibleTo("RigToggle.App")` grant — the public `GlobalHotkey` wrapper is the deliberate encapsulation boundary, matching every other cross-assembly Windows adapter in this project (`WindowsAutostartConfigurator`, `WindowsAppController`, etc.)
- `TryRegisterConfiguredHotkey` treats a null `HotkeyModifiers`/`HotkeyKey` pair as "nothing to register" (returns `true`, not a failure) — matches the plan's explicit contract and the T-09-04 tampering mitigation for a garbage/incomplete settings.json pair
- `RegisterHotkeyAtStartup` wraps the entire operation (both the false-return branch and any thrown exception) in a single try/catch that never rethrows, so a hotkey-registration failure can never block `Application.Run` at startup

## Deviations from Plan

None - plan executed exactly as written. All three acceptance-criteria groups (Task 1's NativeMethods/GlobalHotkey shape, Task 2's WndProc/helper/bracketing shape) were verified via targeted `grep` checks matching the plan's own `<acceptance_criteria>` blocks.

One pre-existing, out-of-scope observation: the plan's Task 1 acceptance criterion `grep -c "FindWindow" src/RigToggle.Windows/NativeMethods.cs == 0` does not hold — but this is because the file's *existing* (pre-plan) XML-doc header comment mentions "FindWindow/FindWindowEx" by name while explaining why they are deliberately NOT used (a documentation reference, not an added P/Invoke signature). No `FindWindow`/`FindWindowEx` P/Invoke signature was added by this plan or exists anywhere in the file. Logged here for transparency; not fixed, as it predates this plan and the underlying intent (no FindWindow-based lookup) genuinely holds.

## Issues Encountered

None. The sandbox has no .NET SDK installed (confirmed: `dotnet` not on PATH) — this is an established, accepted constraint carried over from Phases 6, 7, 8, and 09-01 (see `08-01-SUMMARY.md`, `09-01-SUMMARY.md`). Verification was performed via targeted `grep`-based acceptance-criteria checks (accessibility modifiers, constant values, method presence, call-site patterns, `MessageBox` absence inside `HandleHotkeyToggle`) plus a full manual read-through of the modified/created files for brace/syntax correctness, rather than a live `dotnet build`/`dotnet test` run.

Special attention was paid to the accessibility fix the plan calls out explicitly: `GlobalHotkey` is `public` (in `RigToggle.Windows`), `NativeMethods` stays `internal` (verified: `internal static class NativeMethods` count == 1, unchanged), and `MainForm.cs` calls only `GlobalHotkey.*` — verified `grep -c "NativeMethods" src/RigToggle.App/MainForm.cs` == 0, confirming the CS0122 compile-breaking bug the plan-checker flagged during planning cannot recur.

**A real `dotnet build`/`dotnet test` pass on the Windows rig is still needed before this plan (and the phase) is fully verified — same standing note as prior phases.**

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `MainForm.TryRegisterConfiguredHotkey()`/`UnregisterConfiguredHotkey()`/`RegisterHotkeyAtStartup()` are ready for plan 09-03's `Program.cs` (call `RegisterHotkeyAtStartup()` once after `MainForm` construction) and `SettingsForm` (call `TryRegisterConfiguredHotkey()` after a successful Save) to consume directly
- `GlobalHotkey.Register`/`Unregister` are the only Win32 hotkey entry points RigToggle.App is permitted to call — any future trigger-source plan (e.g. 10-cli-trigger, if it ever needs hotkey awareness) should go through this same public wrapper, not a new P/Invoke
- Blocker carried from STATE.md/09-01-SUMMARY.md: `RegisterHotKey` must still be rig-tested with Moza Companion actually running (the 09-04 rig checkpoint) — silent conflicts with other rig software are the realistic failure mode TRIG-01 exists to catch; not testable in this sandbox
- No blockers for proceeding to plan 09-03

---
*Phase: 09-global-hotkey-trigger*
*Completed: 2026-07-31*
