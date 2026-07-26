---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: milestone_complete
stopped_at: Milestone complete (Phase 5 was final phase)
last_updated: 2026-07-26T14:15:00Z
last_activity: 2026-07-26 -- Completed quick task 260726-j9y: Fixed toggle-back regression by gating MinimizeIfRunning's ShowWindow(SW_MINIMIZE) on preVisible (skip when already hidden); rig-test needed
progress:
  total_phases: 5
  completed_phases: 4
  total_plans: 18
  completed_plans: 18
  percent: 80
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-07-24)

**Core value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.
**Current focus:** Milestone complete

## Current Position

Phase: 5
Plan: Not started
Status: Milestone complete
Last activity: 2026-07-25

Progress: [██████████] 100%

## Performance Metrics

**Velocity:**

- Total plans completed: 16
- Average duration: - min
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 02 | 5 | - | - |
| 03 | 4 | - | - |
| 04 | 4 | - | - |
| 5 | 3 | - | - |

**Recent Trend:**

- Last 5 plans: -
- Trend: -

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Roadmap: Phase 1 (monitor-disable spike) is sequenced first out of normal dependency order — it's the project's one unvalidated core assumption and must be answered before other work is sunk in.
- Roadmap: Coarse granularity merged research's suggested 8 phases into 5 — data/persistence merged with GUI shell (Phase 2), app control merged with audio control (Phase 3), orchestration wiring merged with packaging (Phase 5).

### Pending Todos

- Rig-test needed: quick task `260726-j9y` fixed the toggle-back regression diagnosed via `260726-ixu`'s logging — `MinimizeIfRunning` now skips its `ShowWindow(SW_MINIMIZE)` call when the target window is already hidden/tray-only (`preVisible == false`), instead of unconditionally forcing it back to a visible minimized state. This fix has NOT itself been rig-tested. The user must build, then: toggle to rig mode → close Moza to tray via its X button → toggle back to normal mode → confirm the window STAYS hidden (does not reappear) and no close-inert symptom occurs, and read back `%LOCALAPPDATA%\RigToggle\debug.log` to confirm a "skipped minimize ... already hidden" line appears for that run.

### Known Limitations

- Moza Companion's window close (X) button, Alt+F4, and taskbar "Close window" all become inert (zero visual reaction) specifically on windows that RigToggle itself brought to the foreground — never on windows Moza opens on its own. Minimize is unaffected. Investigated across 10 rounds in debug session `moza-foreground-focus` (2026-07-24 to 2026-07-26); every passively-observable Win32 mechanism a separate process can query (WS_DISABLED, system-menu MF_GRAYED, FormClosing-visible-revert) was tested and eliminated. The remaining likely cause — Moza intercepting close input at the message level (WM_NCHITTEST/WM_NCLBUTTONDOWN/WM_SYSCOMMAND) inside its own window procedure — is upstream of anything RigToggle's own process can observe or fix without hooking Moza's message loop, which is outside this project's Win32-utility scope (CLAUDE.md/STACK.md). This symptom has two independent directions, tracked and fixed separately:
  - **(a) Toggle-TO-rig-mode direction — fixed:** quick task `.planning/quick/260726-idx-redesign-companion-app-launch-focus-mech` redesigned the launch-to-rig-mode path to relaunch-based (ShellExecute) activation that never touches Moza's window at all, so the trigger for this limitation (RigToggle foregrounding a window it doesn't own) no longer exists on that path.
  - **(b) Toggle-TO-normal-mode (toggle-back) direction — separate bug, now fixed:** diagnostic logging added by quick task `.planning/quick/260726-ixu-add-targeted-diagnostic-logging-to-windo` captured rig evidence that `MinimizeIfRunning` was doing an unconditional raw Win32 `ShowWindow(SW_MINIMIZE)` that, when the window was already hidden/tray-only, forced it back to a visible minimized state (pre `IsWindowVisible=False` → post `True`) — retriggering the close-inert symptom on toggle-back. Quick task `.planning/quick/260726-j9y-fix-minimizeifrunning-to-skip-showwindow` fixed this by skipping the `ShowWindow` call whenever the window is already hidden.
  - **(c) Current overall status:** a fix has been applied for BOTH directions, but direction (b)'s fix has NOT yet been rig-tested. The user still needs to verify: toggle to rig mode → close Moza to tray via its X button → toggle back to normal mode → confirm the window STAYS hidden (does not reappear) and no close-inert symptom occurs. This sandbox has no Windows runtime, so none of this can be run/tested here — the user rig-tests. Full investigation history: `.planning/debug/resolved/moza-foreground-focus.md`.
- The relaunch-based launch redesign's `MinimizeIfRunning`/`IsRunning` (toggle-back) still derive the process name from the configured launch-target path via `Path.GetFileNameWithoutExtension`. If the user configures a `.lnk` (rather than the target `.exe` itself) as the launch target, that derived name will typically not match the real running process name, so toggle-back's minimize call may silently no-op. Documented, not patched (out of scope for the redesign — `MinimizeIfRunning` is explicitly unchanged).

### Blockers/Concerns

- Phase 1: RESOLVED — spike/RESULTS-TEMPLATE.md now records a GO decision (non-elevated CCD topology-path-removal confirmed working on this rig's AMD Radeon/DisplayPort hardware for a non-primary display). Remaining caveat carried into Phase 4: disabling the PRIMARY monitor specifically threw `PathChangeException` twice (Windows requires a display at (0,0); `WindowsDisplayAPI.PathInfo.Position` has no public setter to reposition the remaining display first) — a scoped, known engineering task for Phase 4, not a feasibility blocker. Fallback (pnputil/elevation) was not needed and not tested.
- Phase 4/5: Elevation requirements differ per subsystem (monitor/audio/window focus); requesting broad admin rights would break cross-process window focus on the companion app via UIPI — must default to asInvoker and confirm per-operation needs empirically. (Confirmed non-elevated for audio in Phase 3; monitor mechanism also non-elevated per Phase 1 spike.)

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260726-idx | Redesign companion app launch/focus mechanism: unconditional ShellExecute relaunch replaces window-focus dance; Settings adds .lnk/.exe drag-and-drop | 2026-07-26 | 09c758a..792f976 | [260726-idx-redesign-companion-app-launch-focus-mech](./quick/260726-idx-redesign-companion-app-launch-focus-mech/) |
| 260726-ixu | Diagnostic-only: log IsWindowVisible/IsIconic/ShowWindow-return before+after MinimizeIfRunning's minimize call, to gather rig-test evidence for the toggle-back regression reported after 260726-idx | 2026-07-26 | f0bf28a | [260726-ixu-add-targeted-diagnostic-logging-to-windo](./quick/260726-ixu-add-targeted-diagnostic-logging-to-windo/) |
| 260726-j9y | Skip ShowWindow(SW_MINIMIZE) in MinimizeIfRunning when the window is already hidden — fixes toggle-back regression confirmed by 260726-ixu's diagnostic evidence; rig-test still pending | 2026-07-26 | e6e1989 (pending closing commit) | [260726-j9y-fix-minimizeifrunning-to-skip-showwindow](./quick/260726-j9y-fix-minimizeifrunning-to-skip-showwindow/) |

## Deferred Items

Items acknowledged and carried forward from previous milestone close:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| v2 | TRIG-01 (global hotkey trigger) | Deferred to v2 | Initial requirements definition |
| v2 | TRAY-01 (tray residency / autostart) | Deferred to v2 | Initial requirements definition |
| v2 | NOTIF-01 (toast notification on toggle) | Deferred to v2 | Initial requirements definition |
| v2 | LOG-01 (toggle history/log) | Deferred to v2 | Initial requirements definition |

## Session Continuity

Last session: 2026-07-24T21:48:46.566Z
Stopped at: Phase 5 context gathered
Resume file: .planning/phases/05-orchestration-full-toggle-packaging/05-CONTEXT.md
