---
phase: quick-260726-j9y
plan: 01
status: complete
subsystem: app-control
tags: [win32, p-invoke, minimize, toggle-back, regression-fix]

# Dependency graph
requires:
  - phase: quick-260726-ixu
    provides: Diagnostic pre/post IsWindowVisible/IsIconic/ShowWindow-return logging around MinimizeIfRunning's minimize call, which supplied the rig evidence this fix is based on
  - phase: quick-260726-idx
    provides: Relaunch-based LaunchOrFocus redesign, existing Log()/Trace.WriteLine wiring, MinimizeIfRunning/FindBestMainWindow path left otherwise unchanged
provides:
  - MinimizeIfRunning gates its ShowWindow(SW_MINIMIZE) call on preVisible, skipping it entirely when the target window is already hidden/tray-only
affects: [app-control, toggle-back path, H9 close-inert symptom (toggle-back direction)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Conditional mutation gated on a pre-captured read-only P/Invoke state: query state once into a local (preVisible), branch the mutating call on it, and only compute/log post-state inside the branch where the mutation actually ran"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/WindowsAppController.cs
    - .planning/STATE.md
    - .planning/debug/knowledge-base.md

key-decisions:
  - "Kept the pre-minimize diagnostic log line unconditional (still logged on every pass) so the log continues to show which branch (minimize vs. skip) was taken for every toggle-back run, not just the ones where ShowWindow actually fired"
  - "Moved showWindowReturned/postVisible/postIconic declarations inside the if(preVisible) branch rather than declaring them outside and leaving them unset on the skip path, so no stale/default post-state values could ever be logged"

requirements-completed: [APP-02]

# Metrics
duration: 12min
completed: 2026-07-26
---

# Quick Task 260726-j9y: Fix MinimizeIfRunning to Skip ShowWindow When Already Hidden Summary

**Gated MinimizeIfRunning's `ShowWindow(SW_MINIMIZE)` call on `preVisible`, so an already-hidden/tray-only Moza window is left untouched on toggle-back instead of being forced back to a visible minimized taskbar icon.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-26T14:00:00Z
- **Completed:** 2026-07-26T14:15:00Z
- **Tasks:** 2 completed
- **Files modified:** 3

## Accomplishments
- Fixed the confirmed toggle-back regression in `MinimizeIfRunning` (`src/RigToggle.Windows/WindowsAppController.cs`): the unconditional `ShowWindow(hWnd, SW_MINIMIZE)` call is now wrapped in `if (preVisible)`; when the window is already hidden it is left alone and a distinct "skipped minimize ... already hidden" line is logged instead
- Kept the pre-minimize diagnostic log unconditional (still fires every pass) and moved the post-minimize log/state capture inside the visible branch only, so no stale post-state is ever logged on the skip path
- Kept `break` outside/after the if/else so the loop still stops after the first matched process with a real window, regardless of which branch fired
- Updated `.planning/STATE.md` and `.planning/debug/knowledge-base.md` to describe the H9 close-inert symptom as two independent, now-separately-fixed directions: (a) toggle-TO-rig-mode fixed by `260726-idx`'s relaunch redesign, (b) toggle-TO-normal-mode (toggle-back) fixed by this task, (c) overall status is "fix applied for both directions, direction (b) pending rig verification"
- Cross-referenced all three related quick task directories (`260726-idx`, `260726-ixu`, `260726-j9y`) in both docs

## Task Commits

Each task was committed atomically:

1. **Task 1: Gate the minimize call on preVisible in MinimizeIfRunning** - `e6e1989` (fix)
2. **Task 2: Update STATE.md and knowledge-base.md to reflect the two-direction H9 status** - `7731923` (docs)

## Files Created/Modified
- `src/RigToggle.Windows/WindowsAppController.cs` - `MinimizeIfRunning`'s minimize call is now conditional on `preVisible`; visible-window path is functionally unchanged (still minimizes, still logs post-state); hidden-window path now skips `ShowWindow` entirely and logs a distinct skip line
- `.planning/STATE.md` - Known Limitations H9 entry rewritten with (a)/(b)/(c) two-direction framing; Pending Todos entry replaced with this round's rig-verify todo; new Quick Tasks Completed row for `260726-j9y`; frontmatter `last_updated`/`last_activity` bumped
- `.planning/debug/knowledge-base.md` - `moza-foreground-focus` entry's known-limitation paragraph rewritten with the same (a)/(b)/(c) framing, cross-referencing `260726-idx`/`260726-ixu`/`260726-j9y`

## Decisions Made
- Pre-minimize log stays unconditional (evidence trail for every toggle-back run, not just minimize-fired runs)
- Post-minimize locals (`showWindowReturned`, `postVisible`, `postIconic`) scoped inside the `if (preVisible)` branch to avoid ever logging default/stale values on the skip path

## Deviations from Plan

None — plan executed exactly as written. Both tasks' automated grep-based verification gates passed on the first attempt; no auto-fixes, blockers, or architectural questions arose.

## Issues Encountered
- This Linux sandbox has no .NET SDK / Windows runtime (consistent with `260726-idx` and `260726-ixu` SUMMARYs), so `dotnet build` could not be run. Verification fell back to the plan's grep-based automated checks (all passed) plus a manual re-read of the edited `MinimizeIfRunning` method confirming: (a) pre-minimize log is unconditional, (b) `ShowWindow` + post-log live only inside `if (preVisible)`, (c) the `else` branch logs the skip line and makes no `ShowWindow` call, (d) `break` is outside the if/else. The user must build and rig-test per the checklist below.
- One unrelated uncommitted change remains in the working tree at `src/RigToggle.App/Program.cs` (a `TextWriterTraceListener` wiring for `debug.log`), noted as pre-existing/unrelated in the `260726-ixu` SUMMARY. It is outside this task's `files_modified` scope (not touched here) and was left untouched and unstaged, per the plan's explicit "do NOT touch Program.cs" constraint.

## User Setup Required

None — no external service configuration required.

**Rig-test completed and confirmed on 2026-07-26:** the user rig-tested this fix and confirmed "Yes. This works now." The `debug.log` evidence (2026-07-26, 16:05–16:06) shows both the toggle-back skip-minimize path (`MinimizeIfRunning: skipped minimize hWnd=0x2811EC — window already hidden (IsWindowVisible=false)`) and the normal visible-window minimize path (`post-minimize ... IsWindowVisible=True, IsIconic=True, ShowWindowReturned=True`) behaving correctly in the same session. No further action needed.

## Next Phase Readiness
- Both directions of the H9 close-inert symptom now have applied fixes, and both have been rig-verified and confirmed by the user on 2026-07-26 ("Yes. This works now.") — the symptom is fully resolved end-to-end.
- No further code changes are planned. See quick task `260726-jm3` for the docs-only closeout that marks H9 fully resolved across STATE.md and knowledge-base.md.

---
*Phase: quick-260726-j9y*
*Completed: 2026-07-26*

## Self-Check: PASSED

- FOUND: src/RigToggle.Windows/WindowsAppController.cs
- FOUND: .planning/STATE.md
- FOUND: .planning/debug/knowledge-base.md
- FOUND: e6e1989 (Task 1 commit)
- FOUND: 7731923 (Task 2 commit)
