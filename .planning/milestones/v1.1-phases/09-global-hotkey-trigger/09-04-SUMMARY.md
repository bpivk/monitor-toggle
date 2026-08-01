---
phase: 09-global-hotkey-trigger
plan: 04
subsystem: ui
tags: [winforms, registerhotkey, wndproc, wm_hotkey, previewkeydown]

requires:
  - phase: 09-global-hotkey-trigger (waves 1-3)
    provides: HotkeyCombo/HotkeyFormatter core model, GlobalHotkey P/Invoke adapter, MainForm WM_HOTKEY handling, SettingsForm capture UI
provides:
  - Rig-confirmed GO on all 5 checkpoint scenarios (capture UX, save/persist, toggle-from-anywhere, conflict surfacing, Settings-race)
  - Two real bugs found on the actual Windows build/rig and fixed, retest-confirmed
affects: [phase-9-completion, phase-10]

tech-stack:
  added: []
  patterns:
    - "Pre-declare a captured local (mainForm = null!) before a mutually-dependent local-function closure, assign after — breaks C#'s strictly-textual local scoping (CS0841/CS0165) without a forward-declaration hack"
    - "PreviewKeyDownEventArgs.IsInputKey = true is required (not just KeyEventArgs.Handled/SuppressKeyPress) to stop WinForms from routing a dialog key like Escape to Form.ProcessDialogKey/CancelButton before OnKeyDown ever fires"

key-files:
  created: []
  modified:
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/SettingsForm.cs

key-decisions:
  - "mainForm/SettingsFormFactory forward-reference cycle broken via null!-then-assign, not restructuring MainForm's constructor — smallest change, no API shape change for MainForm."
  - "Escape-closes-Settings-during-recording fixed by claiming all keys as input via PreviewKeyDown while _recordingHotkey is true, guarded so idle-state Escape/Enter/Tab keep normal dialog behavior — not by removing or rewiring CancelButton."

patterns-established:
  - "Rig-testing (an actual dotnet build + on-hardware run) is the ground truth for this codebase's WinForms behavior claims — grep-based sandbox verification cannot catch C# compiler errors or WinForms message-pipeline ordering bugs, consistent with Phase 6/8 precedent."

requirements-completed: [TRIG-01]

duration: ~40min (interactive rig session, across three check-ins: build fix, file-lock retry, Escape bug fix)
completed: 2026-07-31
---

# Phase 9: Global Hotkey Trigger - Rig Checkpoint Summary

**Full GO: all 5 checkpoint scenarios confirmed on real rig hardware, including two real bugs (a compile error and a WinForms dialog-key routing bug) found, root-caused, and fixed mid-checkpoint.**

## Performance

- **Duration:** ~40 min interactive (publish attempt 1 → compile fix → publish attempt 2 → file-lock retry → full rig pass → Escape bug found → fix → retest confirmed)
- **Completed:** 2026-07-31

## Checkpoint Results

| # | Scenario | Requirement | Result |
|---|----------|-------------|--------|
| 1 | Capture UX (click to record, combo captures, bare modifier rejected, Escape clears) | TRIG-01 criterion 1 | ❌ **FAILED initially** — Escape cleared the field but also closed the Settings dialog. Root-caused, fixed, and **retest-confirmed PASS**. |
| 2 | Save + persist (survives dialog reopen and full app restart) | TRIG-01 criterion 1 | ✅ PASS |
| 3 | Toggle from anywhere, including tray-hidden | TRIG-01 criterion 1 | ✅ PASS |
| 4 | Conflict surfacing with Moza Companion running (inline warning + startup toast, dialog not lost) | TRIG-01 criterion 2 | ✅ PASS |
| 5 | Settings-race (hotkey inert while Settings open, works again after close) | TRIG-01 criterion 3 | ✅ PASS |

Build was blocked twice before the checkpoint could even start:
- A real C# compile error (`CS0841`/`CS0165`) in `Program.cs`, caught only by the actual Windows `dotnet publish` — the sandboxed executor's grep-based verification had no way to catch this.
- A transient Windows file-lock (`UnauthorizedAccessException`) on the previous build's `.exe` from a lingering process — resolved by closing the stale process and republishing, no code change needed.

## Root Causes & Fixes

**Bug 1 — compile error: `mainForm`/`SettingsFormFactory` forward-reference cycle**

`Program.cs`'s `SettingsFormFactory` local function referenced `mainForm.TryRegisterConfiguredHotkey`, but `mainForm` was declared via `var mainForm = new MainForm(..., SettingsFormFactory)` on a *later* line. The Wave 3 executor's inline comment argued this was safe because the factory is only *invoked* after `mainForm` is assigned — but C# local-variable scope is strictly textual within a block: a local function cannot reference a variable declared later in the same block at all, regardless of when the function is actually called. This produced `CS0841: Cannot use local variable 'mainForm' before it is declared` and `CS0165: Use of unassigned local variable`, confirmed only by the real Windows compiler (this sandbox has no `dotnet` SDK).

**Fix (commit `ad40600`):** Pre-declare `MainForm mainForm = null!;` before the factory closure, then assign `mainForm = new MainForm(...)` after. The closure still captures `mainForm` by reference — by the time it's actually invoked (Settings dialog opened), `mainForm` holds the real instance.

**Bug 2 — Escape both cleared the hotkey field and closed the Settings dialog**

`TxtHotkey_KeyDown` set `e.Handled = true` / `e.SuppressKeyPress = true` for Escape, which the Wave 3 plan's own comment claimed would "suppress every key from reaching normal dialog processing." This is incorrect for dialog keys: WinForms routes Escape/Enter/Tab/arrows through `Form.ProcessDialogKey` (which checks `CancelButton`, wired to `btnDiscardChanges`) *before* `OnKeyDown`/`KeyDown` ever fires, gated by `Control.IsInputKey` — a check that happens earlier in the pipeline than anything `KeyDown`'s event args can influence. So Escape simultaneously ran the capture handler's clear logic *and* triggered `CancelButton`, closing the whole Settings dialog.

**Fix (commit `8046004`):** Added a `TxtHotkey_PreviewKeyDown` handler that sets `PreviewKeyDownEventArgs.IsInputKey = true` for every key while `_recordingHotkey` is true — this claims the keystroke as ordinary input, routing it to `TxtHotkey_KeyDown` instead of `ProcessDialogKey`. Guarded on recording state so idle-state Escape/Enter/Tab still behave as normal dialog navigation/cancel (only capture mode needs every key to reach the handler — Enter and Tab are both valid recordable hotkey keys too, not just Escape).

## Decisions Made

- Both fixes were applied directly during the checkpoint session (not deferred to a separate gap-closure plan) since they were small, well-understood, and immediately retestable on the same rig session — consistent with how Phase 8's `08-04` checkpoint handled its D-06 bug.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rig-discovered defect] Compile error in Program.cs (mainForm/SettingsFormFactory forward reference)**
- **Found during:** Task 1 (Release build attempt before publish)
- **Issue:** `CS0841`/`CS0165` — local function referenced a local variable declared later in the same block.
- **Fix:** Pre-declared `mainForm` as `null!`, assigned after the factory closure.
- **Files modified:** `src/RigToggle.App/Program.cs`
- **Verification:** Rig rebuild succeeded (`RigToggle.App.dll` compiled clean on retry).
- **Committed in:** `ad40600`

**2. [Rig-discovered defect] Escape closed Settings during hotkey capture**
- **Found during:** Task 2 (rig checkpoint), scenario 1 (capture UX)
- **Issue:** `KeyEventArgs.Handled`/`SuppressKeyPress` in `KeyDown` cannot prevent `Form.ProcessDialogKey`/`CancelButton` from also firing on Escape — that routing happens earlier in the WinForms pipeline, gated by `IsInputKey`.
- **Fix:** Added `PreviewKeyDown` handler setting `IsInputKey = true` while recording.
- **Files modified:** `src/RigToggle.App/SettingsForm.cs`
- **Verification:** User retested scenario 1 on the rig after the fix — confirmed Escape now clears the field without closing the dialog; no regression in the other 4 scenarios on retest.
- **Committed in:** `8046004`

---

**Total deviations:** 2 auto-fixed (2 rig-discovered defects, both compile/runtime-pipeline bugs invisible to sandboxed grep-based verification)
**Impact on plan:** Both fixes were necessary for TRIG-01 to actually work; no scope creep — no behavior beyond what Waves 1-3 already specified was added.

## Issues Encountered

- A Windows file-lock (`UnauthorizedAccessException` on the previous build's `.exe`, likely a lingering process) blocked the second publish attempt — resolved by the user closing the stale process and republishing. No code change; not a defect in this project's code.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 9 (Global Hotkey Trigger, TRIG-01) is fully rig-confirmed — ready to close out and hand off to Phase 10 (CLI trigger / single-instance IPC), which builds on the same `ToggleOrchestrator` entry point this phase's hotkey handler used.
- No open blockers for Phase 9.
- A new, out-of-scope feature request surfaced during this checkpoint session (configurable close-to-tray vs. exit behavior for the X button, and a minimize-to-tray option) — intentionally NOT folded into this phase; needs its own scoped plan since it extends Phase 8's tray/close-behavior surface.

---
*Phase: 09-global-hotkey-trigger*
*Completed: 2026-07-31*
