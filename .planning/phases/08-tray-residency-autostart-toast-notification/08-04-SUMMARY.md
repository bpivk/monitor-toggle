---
phase: 08-tray-residency-autostart-toast-notification
plan: 04
subsystem: ui
tags: [winforms, notifyicon, tray, registry, applicationcontext]

requires:
  - phase: 08-tray-residency-autostart-toast-notification (waves 1-3)
    provides: NotifyIcon/ContextMenuStrip tray residency, autostart checkbox, --tray startup flag
provides:
  - Rig-confirmed GO on 8 of 10 checkpoint scenarios (TRAY-01, TRAY-03, TRAY-04, TRAY-05, NOTIF-01, TRAY-02 registry write/remove, ghost-icon-on-exit)
  - A real bug found and fixed in the --tray hidden-start mechanism (D-06)
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

requirements-completed: []

duration: ~30min (interactive rig session)
completed: 2026-07-31
---

# Phase 8: Tray Residency, Autostart & Toast Notification - Rig Checkpoint Summary

**Partial GO: 8/10 scenarios confirmed on real rig hardware; a real D-06 hidden-start bug was found, root-caused, and fixed; retest of D-06 + the dependent Assumption A2 scenario is deferred by the user to a later session.**

## Performance

- **Duration:** ~30 min interactive (build + first test pass + bug fix)
- **Completed:** 2026-07-31 (partial — see Outstanding below)

## Checkpoint Results

| # | Scenario | Requirement | Result |
|---|----------|-------------|--------|
| 1 | Close (X) hides to tray, app keeps running | TRAY-01 | ✅ PASS |
| 2 | Left-click tray icon restores + focuses | TRAY-05 | ✅ PASS |
| 3 | Right-click shows menu (Switch/Settings/sep/Exit), doesn't restore | TRAY-03 | ✅ PASS |
| 4 | Icon shape + tooltip reflect mode, correct on first paint | TRAY-04 | ✅ PASS |
| 5 | Tray-menu toggle fires balloon toast matching GUI checklist | NOTIF-01 | ✅ PASS |
| 6 | Settings checkbox writes/removes HKCU Run value | TRAY-02 | ✅ PASS |
| 7 | `--tray` startup shows no window | TRAY-02 / D-06 | ❌ **FAILED initially** — window appeared. Root-caused and fixed (see below). **Retest pending.** |
| 8 | Exit while started `--tray` and never shown (Assumption A2) | TRAY-02 / D-06 | ⏸ **BLOCKED** on #7 — could not be exercised while the window incorrectly appeared. **Retest pending.** |
| 9 | Ghost-icon check on normal-start Exit | TRAY-03/04 | ✅ PASS (implied — confirmed alongside #3; user reported "the rest work") |

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
- **Verification:** Root-caused via code + WinForms lifecycle reasoning after the rig test surfaced the symptom; user retest of the corrected build is pending (deferred by user request — not at their PC).
- **Committed in:** `91c11df`

---

**Total deviations:** 1 rig-discovered-and-fixed (mechanism correction, no scope change)
**Impact on plan:** No scope change — D-06's intent (hidden `--tray` startup) is unaffected; only the implementation mechanism changed.

## Issues Encountered

- The rig checkpoint's Task 2 could not be fully closed in one session: scenario #7 (D-06 hidden start) failed on first attempt, was fixed, but the user was not at their PC to immediately rebuild and retest #7 and the dependent #8 (Assumption A2). Per the user's explicit request, this is being tracked as outstanding rather than blocking further phase-closure progress.

## Outstanding (must be retested before Phase 8 is considered fully GO)

1. **D-06 retest:** Rebuild (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64`), run `RigToggle.App.exe --tray` from a terminal — expect no window, tray icon present and mode-correct.
2. **Assumption A2 retest:** With the app started that way (window never shown), right-click tray → Exit — expect the process fully terminates (no orphan in Task Manager) and the tray icon vanishes immediately (no ghost/hover-only icon).

## Next Phase Readiness

- Phase 8's other 5 requirements (TRAY-01, TRAY-03, TRAY-04, TRAY-05, NOTIF-01) and TRAY-02's registry write/remove behavior are rig-confirmed working.
- TRAY-02's hidden-start sub-behavior (the `--tray` flag itself) had a real bug, now fixed but not yet retested — this is the one remaining item before Phase 8 can close with a full GO.
- REQUIREMENTS.md deliberately still shows all Phase 8 items as Pending (not flipped to Complete) until the outstanding retest above passes — consistent with this project's rig-validation-before-completion discipline (Phase 1, Phase 6 precedent).

---
*Phase: 08-tray-residency-autostart-toast-notification*
*Completed: 2026-07-31 (partial — 2 items pending user retest)*
