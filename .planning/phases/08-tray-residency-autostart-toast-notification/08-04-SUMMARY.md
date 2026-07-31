---
phase: 08-tray-residency-autostart-toast-notification
plan: 04
subsystem: ui
tags: [winforms, notifyicon, tray, registry, applicationcontext]

requires:
  - phase: 08-tray-residency-autostart-toast-notification (waves 1-3)
    provides: NotifyIcon/ContextMenuStrip tray residency, autostart checkbox, --tray startup flag
provides:
  - Rig-confirmed GO on all 10 checkpoint scenarios (TRAY-01/02/03/04/05, NOTIF-01, ghost-icon-on-exit, --tray hidden start, Assumption A2)
  - A real bug found, fixed, and retest-confirmed in the --tray hidden-start mechanism (D-06)
affects: [phase-8-completion, phase-9, phase-10]

tech-stack:
  added: []
  patterns: ["ApplicationContext with no MainForm reference for a true hidden WinForms startup, superseding the ApplicationContext(mainForm) theory in 08-RESEARCH.md"]

key-files:
  created: []
  modified:
    - src/RigToggle.App/Program.cs

key-decisions:
  - "D-06's mechanism corrected: Application.Run(new ApplicationContext(mainForm)) does NOT suppress Show() on this runtime (rig-confirmed, contradicting 08-RESEARCH.md's citation-backed theory). Fixed to Application.Run(new ApplicationContext()) with no MainForm reference; mainForm is shown later, on demand, via the tray icon's existing handlers."

patterns-established:
  - "Rig-testing is the ground truth for WinForms message-loop/startup behavior claims — even citation-backed research theories must be treated as unconfirmed until verified on real Windows, consistent with this project's established rig-validation discipline (Phase 1, Phase 6)."

requirements-completed: [TRAY-01, TRAY-02, TRAY-03, TRAY-04, TRAY-05, NOTIF-01]

duration: ~30min (interactive rig session, across two check-ins)
completed: 2026-07-31
---

# Phase 8: Tray Residency, Autostart & Toast Notification - Rig Checkpoint Summary

**Full GO: all 10 checkpoint scenarios confirmed on real rig hardware, including a retest of the D-06 hidden-start bug that was found, root-caused, and fixed mid-checkpoint.**

## Performance

- **Duration:** ~30 min interactive (build + first test pass + bug fix + retest)
- **Completed:** 2026-07-31

## Checkpoint Results

| # | Scenario | Requirement | Result |
|---|----------|-------------|--------|
| 1 | Close (X) hides to tray, app keeps running | TRAY-01 | ✅ PASS |
| 2 | Left-click tray icon restores + focuses | TRAY-05 | ✅ PASS |
| 3 | Right-click shows menu (Switch/Settings/sep/Exit), doesn't restore | TRAY-03 | ✅ PASS |
| 4 | Icon shape + tooltip reflect mode, correct on first paint | TRAY-04 | ✅ PASS |
| 5 | Tray-menu toggle fires balloon toast matching GUI checklist | NOTIF-01 | ✅ PASS |
| 6 | Settings checkbox writes/removes HKCU Run value | TRAY-02 | ✅ PASS |
| 7 | `--tray` startup shows no window | TRAY-02 / D-06 | ❌ **FAILED initially** — window appeared. Root-caused, fixed, and **retest-confirmed PASS** after the fix. |
| 8 | Exit while started `--tray` and never shown (Assumption A2) | TRAY-02 / D-06 | ✅ PASS — confirmed after #7's fix; clean termination, no ghost tray icon. |
| 9 | Ghost-icon check on normal-start Exit | TRAY-03/04 | ✅ PASS (confirmed alongside #3) |

## Root Cause & Fix

**Bug:** `Application.Run(new ApplicationContext(mainForm))` was expected (per `08-RESEARCH.md`, citation-backed) to run the message loop without showing `mainForm`. Rig-tested and found **false** on this runtime — the window appeared under `--tray` exactly as if `Application.Run(mainForm)` had been called directly.

**Fix (commit `91c11df`):** Changed the hidden-start branch in `Program.cs` to `Application.Run(new ApplicationContext())` — an `ApplicationContext` with **no** `MainForm` reference at all. `mainForm` remains a local object reference the tray icon's `Click`/`TraySettingsMenuItem_Click` handlers already hold; it is shown for the first time only when the user requests it via the tray icon. `Application.Exit()` from the tray's Exit item still terminates the message loop correctly — it closes every form and message loop on the thread regardless of `ApplicationContext.MainForm` wiring.

`08-RESEARCH.md` has been annotated with a `RIG-TESTED CORRECTION` notice at the top recording this finding, since the original (disproven) theory is cited extensively throughout that document and should not be trusted by a future reader.

## Decisions Made

- The `ApplicationContext`-with-no-MainForm pattern is now the confirmed, working mechanism for this codebase's hidden-startup requirement — documented in `Program.cs`'s inline comment for future reference.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rig-discovered defect] D-06 hidden-start mechanism did not work as researched**
- **Found during:** Task 2 (rig checkpoint), scenario "D-06 hidden start"
- **Issue:** `Application.Run(new ApplicationContext(mainForm))` shows the window anyway, contradicting `08-RESEARCH.md`'s Microsoft-doc-cited theory.
- **Fix:** Switched to `Application.Run(new ApplicationContext())` (no `MainForm` reference).
- **Files modified:** `src/RigToggle.App/Program.cs`
- **Verification:** Root-caused via code + WinForms lifecycle reasoning after the rig test surfaced the symptom; retest-confirmed PASS on real hardware after the fix (and after the code review pass found no regression in it).
- **Committed in:** `91c11df`

---

**Total deviations:** 1 rig-discovered-and-fixed (mechanism correction, no scope change)
**Impact on plan:** No scope change — D-06's intent (hidden `--tray` startup) is unaffected; only the implementation mechanism changed.

## Issues Encountered

- The rig checkpoint's Task 2 spanned two check-ins: scenario #7 (D-06 hidden start) failed on first attempt and was root-caused and fixed, but the user was away from their PC to immediately retest. A code review pass (08-REVIEW.md) ran in the interim and found no regression in the fix. The user retested on returning and confirmed both #7 and the dependent #8 (Assumption A2) pass.

## Next Phase Readiness

- All 6 Phase 8 requirements (TRAY-01/02/03/04/05, NOTIF-01) are rig-confirmed and code-review-clean. Full GO.
- REQUIREMENTS.md is flipped to Complete for all six via the orchestrator's `phase.complete` step.

---
*Phase: 08-tray-residency-autostart-toast-notification*
*Completed: 2026-07-31*
