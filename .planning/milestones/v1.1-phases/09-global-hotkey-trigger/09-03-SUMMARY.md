---
phase: 09-global-hotkey-trigger
plan: 03
subsystem: ui
tags: [hotkey, settings-dialog, winforms, trig-01]

# Dependency graph
requires:
  - phase: 09-01
    provides: HotkeyCombo modifier constants/IsModifierVirtualKey, HotkeyFormatter.ToDisplayString, AppSettings.HotkeyModifiers/HotkeyKey nullable persisted fields
  - phase: 09-02
    provides: MainForm.TryRegisterConfiguredHotkey/RegisterHotkeyAtStartup/UnregisterConfiguredHotkey, GlobalHotkey wrapper, WndProc WM_HOTKEY dispatch
provides:
  - txtHotkey read-only recording textbox in SettingsForm implementing the full UI-SPEC capture state machine (idle/recording/captured/Escape-clear/focus-loss-cancel)
  - SettingsForm load/save of the hotkey combo via AppSettings.HotkeyModifiers/HotkeyKey
  - Non-blocking inline registration-failure warning (errHotkey/lblHotkeyWarning) that keeps the Settings dialog open on failure without rolling back the save
  - Startup-time hotkey registration for both visible and --tray launch paths (Program.cs)
affects: [09-04-rig-checkpoint]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Dedicated inline-warning-pair-per-concern (errHotkey/lblHotkeyWarning), never reusing another section's controls — same precedent as errAutostart/lblAutostartWarning"
    - "Boolean reentrancy/mode-tracking flag (_recordingHotkey) for a control's own event-handler state machine, mirroring _updatingMonitorGridProgrammatically"
    - "Non-blocking Save with DialogResult.None retry-in-place on a recoverable external failure — distinct from the autostart precedent's revert-and-resync pattern, since the user's chosen value (not an external device) is the source of truth"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "txtHotkey capture mode enters only via MouseDown (not GotFocus), per UI-SPEC/D-01 — TabStop=false and Cursor=Hand reinforce the click-to-activate-only contract"
  - "Escape always clears (never 'cancel to old value'); losing focus mid-recording is a silent cancel back to the pre-recording display — these are deliberately different UI-SPEC behaviors, not a single 'cancel' code path"
  - "Save persists the pending hotkey combo unconditionally, then attempts registration separately — a failed registration sets DialogResult.None (keeps dialog open) and shows the dedicated warning, but never reverts the already-persisted combo (D-05, contrasts with the autostart block's revert-and-resync recovery)"
  - "SettingsFormFactory (Program.cs local function) captures mainForm.TryRegisterConfiguredHotkey via closure even though declared textually above `var mainForm = ...` — safe because the method group is only evaluated when the factory is invoked, after mainForm is assigned"

requirements-completed: [TRIG-01]

# Metrics
duration: 35min
completed: 2026-07-31
---

# Phase 9 Plan 3: Hotkey Capture UI & Startup/Save Wiring Summary

**SettingsForm gains a read-only "click to record" hotkey textbox implementing the full UI-SPEC state machine, wired to persist AppSettings.HotkeyModifiers/HotkeyKey and attempt registration through MainForm's shared helper at both Settings-Save and app startup, with a non-blocking inline warning on registration failure.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-07-31 (session start)
- **Completed:** 2026-07-31
- **Tasks:** 3 completed
- **Files modified:** 3

## Accomplishments

- `SettingsForm.Designer.cs`: added `lblHotkeyCaption`/`txtHotkey`/`lblHotkeyWarning` + a dedicated `errHotkey` ErrorProvider (batched into the existing `BeginInit`/`EndInit`/`ContainerControl` sequence alongside the other four providers), at the exact UI-SPEC coordinates; `chkStartWithWindows`, `lblAutostartWarning`, `btnSaveSettings`, `btnDiscardChanges` and `ClientSize` all shifted down to accommodate the new row
- `SettingsForm.cs`: full click-to-record state machine on `txtHotkey` — `MouseDown` enters Recording (`SystemColors.Info` background, the one Accent use this phase permits), `KeyDown` captures a modifier+key combo (rejecting bare modifiers and unmodified keys) or clears on Escape, `LostFocus` mid-recording silently reverts to the pre-recording display. Load populates the pending combo from `AppSettings`; Save persists it unconditionally and then attempts registration via the injected `Func<bool> tryRegisterConfiguredHotkey`, showing a dedicated inline warning and holding the dialog open (`DialogResult.None`) on failure without rolling back the save
- `Program.cs`: `SettingsFormFactory` now passes `mainForm.TryRegisterConfiguredHotkey` as the constructor's 5th argument; `mainForm.RegisterHotkeyAtStartup()` is called unconditionally right after `InitializeTrayState()` and before the `--tray`/visible `Application.Run` branch, so the configured hotkey registers on both startup paths (D-04)

## Task Commits

Each task was committed atomically:

1. **Task 1: SettingsForm.Designer — hotkey caption/textbox/warning/errHotkey + downstream shift** - `ef9d7f7` (feat)
2. **Task 2: SettingsForm capture state machine + load/save + inline registration warning** - `afc167b` (feat)
3. **Task 3: Program.cs — startup registration + factory wired with registration callback** - `ab1aceb` (feat)

**Plan metadata:** committed separately after this SUMMARY (see final commit step)

## Files Created/Modified

- `src/RigToggle.App/SettingsForm.Designer.cs` — `lblHotkeyCaption`/`txtHotkey`/`lblHotkeyWarning`/`errHotkey` declared, configured, and fielded; downstream controls and `ClientSize` shifted per UI-SPEC
- `src/RigToggle.App/SettingsForm.cs` — 5th constructor parameter (`Func<bool> tryRegisterConfiguredHotkey`), `_pendingHotkeyModifiers`/`_pendingHotkeyKey`/`_recordingHotkey` state, `RenderHotkeyIdleDisplay`/`TxtHotkey_MouseDown`/`TxtHotkey_KeyDown`/`TxtHotkey_LostFocus` handlers, Load population, Save persistence + registration-failure warning
- `src/RigToggle.App/Program.cs` — `SettingsFormFactory` wired with `mainForm.TryRegisterConfiguredHotkey`; `mainForm.RegisterHotkeyAtStartup()` call added after `InitializeTrayState()`

## Decisions Made

- Kept the Escape-clears / focus-loss-silently-cancels distinction as two separate code paths (not a unified "cancel" helper) — they are genuinely different UI-SPEC behaviors (Escape clears an existing configured value; losing focus does not) and collapsing them would risk silently changing one into the other during a future edit
- Did not reuse `errAutostart`/`lblAutostartWarning` for the hotkey warning — per D-05/UI-SPEC's explicit "dedicated pair, not reusing an unrelated section" rule (citing `08-REVIEW.md`'s precedent)
- The registration-failure branch deliberately does NOT mirror the autostart block's "revert checkbox to actual state" recovery-read — there is no external device state to resync from; the user's chosen combo is the source of truth regardless of registration outcome (documented inline with an XML-doc-style rationale comment as a guardrail against a future "fix")

## Deviations from Plan

None - plan executed exactly as written. All three tasks' acceptance criteria (grep-verified: `txtHotkey`/`lblHotkeyWarning` occurrence counts, `errHotkey` BeginInit/EndInit/ContainerControl presence, `ClientSize` value, `_tryRegisterConfiguredHotkey`/`HotkeyFormatter.ToDisplayString`/`IsModifierVirtualKey` call-site counts, `SystemColors.Info` usage, `mainForm.RegisterHotkeyAtStartup()`/`mainForm.TryRegisterConfiguredHotkey` call sites in Program.cs) were checked directly against the modified files.

## Issues Encountered

None. The sandbox has no .NET SDK installed (confirmed: `dotnet` not on PATH) — this is an established, accepted constraint carried over from Phases 6, 7, 8, and 09-01/09-02 (see their summaries). Verification was performed via targeted `grep`-based acceptance-criteria checks plus a full manual read-through of all three modified files, including a brace-balance check (`{`/`}` counts matched in all three files) in lieu of a live `dotnet build`. `System.Drawing`/`System.Windows.Forms` types (`SystemColors`, `Keys`, `MouseEventArgs`, `KeyEventArgs`) are consumed unqualified, consistent with every other unqualified WinForms-type usage already present in this file (e.g. `Label`, `ComboBox`, `MessageBox`) — the `UseWindowsForms=true` SDK's implicit global usings cover both namespaces, matching the project's existing convention of never explicitly importing them.

**A real `dotnet build`/`dotnet test` pass on the Windows rig is still needed before this plan (and the phase) is fully verified — same standing note as prior phases.**

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 09-04's rig checkpoint can now exercise the full TRIG-01 user flow: open Settings, click `txtHotkey`, record a combo, Save (registers immediately with inline failure feedback if conflicted), close/reopen the app (registers at both visible and `--tray` startup), and press the hotkey to toggle from anywhere including while hidden to tray
- No blockers for proceeding to plan 09-04

---
*Phase: 09-global-hotkey-trigger*
*Completed: 2026-07-31*

## Self-Check: PASSED

All modified files verified present on disk (SettingsForm.Designer.cs, SettingsForm.cs, Program.cs, 09-03-SUMMARY.md); all task commit hashes (ef9d7f7, afc167b, ab1aceb) verified present in git log.
