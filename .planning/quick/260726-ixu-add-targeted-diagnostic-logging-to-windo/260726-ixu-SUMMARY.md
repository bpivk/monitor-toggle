---
phase: quick-260726-ixu
plan: 01
subsystem: app-control
tags: [win32, p-invoke, diagnostic-logging, minimize, toggle-back]

# Dependency graph
requires:
  - phase: quick-260726-idx
    provides: Relaunch-based LaunchOrFocus redesign, existing Log()/Trace.WriteLine wiring, MinimizeIfRunning/FindBestMainWindow path left unchanged
provides:
  - Before/after diagnostic logging around MinimizeIfRunning's ShowWindow(SW_MINIMIZE) call, capturing IsWindowVisible, IsIconic, and the ShowWindow return value
affects: [app-control, toggle-back regression investigation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Diagnostic-only P/Invoke instrumentation: read-only Win32 state queries (IsWindowVisible/IsIconic) captured into locals and logged before/after a mutating call, never used for control flow"

key-files:
  created: []
  modified:
    - src/RigToggle.Windows/NativeMethods.cs
    - src/RigToggle.Windows/WindowsAppController.cs

key-decisions:
  - "Split each Log() interpolation into separate local bool variables per NativeMethods call (rather than inlining calls directly in the format string) so each diagnostic value is independently attributable in code and unambiguously countable by grep-based verification in this sandbox"

patterns-established:
  - "Diagnostic-only instrumentation: query state via read-only P/Invoke, log via existing best-effort Log() helper, never gate behavior on the queried value"

requirements-completed: []

# Metrics
duration: 8min
completed: 2026-07-26
---

# Quick Task 260726-ixu: Targeted Diagnostic Logging for MinimizeIfRunning Summary

**Added before/after IsWindowVisible/IsIconic/ShowWindow-return logging around MinimizeIfRunning's `ShowWindow(SW_MINIMIZE)` call to capture real window state for the next rig test, with zero behavior change.**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-07-26T13:30:00Z
- **Completed:** 2026-07-26T13:38:00Z
- **Tasks:** 1 completed
- **Files modified:** 2

## Accomplishments
- Re-added `IsWindowVisible`/`IsIconic` P/Invoke declarations in `NativeMethods.cs` (correct `[DllImport("user32.dll")]` + `[return: MarshalAs(UnmanagedType.Bool)]` signatures matching the file's existing convention)
- Instrumented `MinimizeIfRunning` with a pre-minimize diagnostic log line (hWnd, IsWindowVisible, IsIconic) and a post-minimize diagnostic log line (hWnd, IsWindowVisible, IsIconic, ShowWindow's captured boolean return value)
- Verified via grep-based checks and manual re-read that the underlying behavior is byte-for-byte unchanged apart from the added log lines: same `FindBestMainWindow` target, same unconditional `ShowWindow(hWnd, SW_MINIMIZE)` call, same `break`

## Task Commits

Each task was committed atomically:

1. **Task 1: Re-add IsWindowVisible/IsIconic P/Invokes and log before/after the minimize call** - `f0bf28a` (feat)

_Note: no plan-metadata commit was made separately — this quick task uses a single-task plan; the metadata commit below (this SUMMARY + STATE.md) is the closing commit._

## Files Created/Modified
- `src/RigToggle.Windows/NativeMethods.cs` - Re-added `IsWindowVisible(IntPtr)` and `IsIconic(IntPtr)` P/Invoke declarations (read-only diagnostic queries, no control-flow usage)
- `src/RigToggle.Windows/WindowsAppController.cs` - `MinimizeIfRunning` now captures pre/post `IsWindowVisible`/`IsIconic` state and the `ShowWindow` return value into locals and logs both via the existing `Log()` helper (routes to `Trace.WriteLine` -> `%LOCALAPPDATA%\RigToggle\debug.log`)

## Decisions Made
- Used separate local `bool` variables for each diagnostic P/Invoke call (`preVisible`, `preIconic`, `showWindowReturned`, `postVisible`, `postIconic`) instead of inlining the calls directly inside the `Log($"...")` interpolated string. Functionally identical, but makes each diagnostic value independently readable in code and lets the plan's grep-based automated verification (`grep -cE "NativeMethods\.(IsWindowVisible|IsIconic)\(hWnd\)"` — which counts matching *lines*, not occurrences) correctly count all 4 diagnostic calls instead of collapsing two-per-line matches down to 2.

## Deviations from Plan

None — plan executed exactly as written. The plan's own automated verify command (`grep -c` based) undercounts same-line matches; this was resolved by refactoring for clarity (one P/Invoke call per statement) rather than by loosening the check, so the final code satisfies both the letter and the intent of the plan's verification.

## Issues Encountered
- This Linux sandbox has no .NET SDK / Windows runtime (confirmed in the 260726-idx SUMMARY and reiterated in this plan's `<verification>` section), so `dotnet build` could not be run. Fell back to the plan-specified grep-based automated verification plus a manual line-by-line re-read of both edited methods confirming: (a) both P/Invokes exist with correct attributes, (b) the `ShowWindow` call is unchanged apart from capturing its return value, (c) exactly two new `Log(...)` calls bracket it (pre and post), (d) no new control flow (no conditionals, no retries, no skip logic) was introduced.
- One unrelated uncommitted change was already present in the working tree at task start: `src/RigToggle.App/Program.cs` (a `TextWriterTraceListener` wiring for `debug.log`, apparently pending from a prior session, not part of this task's `files_modified` list and not committed as part of 260726-idx). Left untouched and unstaged per the plan's explicit "no Program.cs change" constraint — this task's commit contains only the two files it was scoped to.

## User Setup Required

None - no external service configuration required.

**Rig-test needed to capture evidence:** this task is diagnostic-only and does not fix anything. To gather the actual `debug.log` output needed to confirm or refute the toggle-back regression hypothesis, the user must:
1. Build and run the updated app on the rig (`dotnet publish` per the standard packaging flow, or run from VS/`dotnet run`).
2. Reproduce the toggle-back regression: toggle to rig mode, then toggle back to normal mode (the step that reportedly makes the Moza window reappear and become un-closeable).
3. Read `%LOCALAPPDATA%\RigToggle\debug.log` and locate the `MinimizeIfRunning: pre-minimize ...` / `MinimizeIfRunning: post-minimize ...` line pair from that run.
4. Report back the exact `IsWindowVisible`/`IsIconic` values (pre and post) and the `ShowWindowReturned` value — this confirms or refutes the leading hypothesis (a hidden/tray-only window being forced visible-and-minimized by `ShowWindow(SW_MINIMIZE)`).

## Next Phase Readiness
- Instrumentation is in place; no further code changes needed until rig-test evidence comes back.
- Once `debug.log` output is captured, a follow-up quick task should analyze the pre/post state and, if the hypothesis is confirmed, propose a scoped fix (e.g. skip/alter the minimize call when the window is already hidden) — that decision is explicitly deferred, not made here.

---
*Phase: quick-260726-ixu*
*Completed: 2026-07-26*

## Self-Check: PASSED

- FOUND: src/RigToggle.Windows/NativeMethods.cs
- FOUND: src/RigToggle.Windows/WindowsAppController.cs
- FOUND: .planning/quick/260726-ixu-add-targeted-diagnostic-logging-to-windo/260726-ixu-SUMMARY.md
- FOUND: f0bf28a (task commit)
