# Rig Toggle

## What This Is

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode" with one click — from the GUI, a tray menu, or a global keyboard shortcut. Toggling to rig mode disables the primary monitor at the OS level (so games default to the rig monitor instead), switches the default audio output to the rig speakers, and launches the Moza Companion app. Toggling back restores the exact previous monitor/audio state and minimizes the Moza Companion app. The app can run tray-resident with autostart, and now supports arbitrary multi-monitor configurations (not just a single primary monitor). Built for a single user's personal sim-racing rig setup.

## Core Value

A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches (e.g. BeamNG.drive minimizing itself) reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## Current State

**Shipped: v1.1 Automation & Multi-Monitor (2026-08-01)**

v1.1 removed the daily-use friction that remained after v1.0: the app no longer requires opening the GUI to toggle (tray residency + global hotkey), and monitor control generalized from one hardcoded primary monitor to arbitrary independently-configurable disable/enable sets. A shared reentrancy-safe orchestration helper (`ToggleOrchestrator`) now guards every trigger path against corrupted state from concurrent toggles. A late-breaking real need — independently configurable tray close/minimize behavior — was scoped and shipped as Phase 11, ahead of the originally-planned Phase 10 (CLI trigger), which was reviewed and permanently dropped at milestone close rather than delivered (see Requirements below).

**Scope decision, not a gap:** CLI trigger + single-instance IPC (TRIG-02/TRIG-03) was scoped for v1.1 but never built, and after review was decided permanently out of scope rather than deferred — tray and hotkey triggers already cover every trigger path this project needs. Full detail: `.planning/milestones/v1.1-REQUIREMENTS.md`.

## Next Milestone Goals

Not yet defined. Run `/gsd:new-milestone` to scope v1.2 (or v2.0). Candidate carried in the v2 backlog: toggle history/log (LOG-01).

<details>
<summary>Archived: v1.0 and v1.1 milestone framing (superseded)</summary>

**v1.0 MVP target features (shipped 2026-07-26):** GUI settings view, one-click toggle in both directions, true OS-level monitor disable, default audio device switching, companion-app launch/focus/minimize, standalone .exe distribution.

**v1.1 Automation & Multi-Monitor target features (shipped 2026-08-01, as scoped 2026-07-26):**
- Tray residency (autostart on boot, minimize-to-tray on close) with a tray icon context menu, configurable via Settings
- Global hotkey trigger (Windows-wide keyboard shortcut)
- CLI trigger (command-line args for macro pads / Stream Deck / external tools) — *scoped but not delivered; dropped to v2 at milestone close*
- Toast/status notification confirming a toggle when triggered without the GUI open
- Multi-monitor enable/disable configuration — generalizes v1.0's single "primary monitor to disable" to arbitrary multi-monitor desks

**Deferred from v1.1:** LOG-01 (toggle history/log) — still a nice-to-have, lower priority than automation/multi-monitor.

</details>

## Requirements

### Validated

- [x] GUI includes a settings view where the user selects: which monitor is the "primary to disable," which audio devices are the toggle pair, and which app (path) to launch/minimize — Validated in Phase 2: Foundations & GUI Shell
- [x] Toggling to rig mode switches the default audio output device to the rig speakers — Validated in Phase 3: App & Audio Control
- [x] Toggling back restores the exact previous default audio device across all relevant audio roles — Validated in Phase 3
- [x] Toggling to rig mode launches the Moza Companion app if it isn't already running; if it's already running, brings it to focus instead of launching a duplicate instance — Validated in Phase 3, mechanism superseded post-ship (H9 focus-manipulation bug, fixed via relaunch-based `ShellExecute` activation, rig-confirmed both directions). Settings generalized to accept any `.lnk`/`.exe` target.
- [x] Toggling back minimizes the Moza Companion app window (best-effort) — Validated in Phase 3; refined post-ship to skip the minimize call when the window is already hidden/tray-only
- [x] Toggling to rig mode disables the primary monitor at the OS level (true disable) — Validated in Phase 4: Monitor Control (Production)
- [x] Toggling back restores the exact monitor configuration that was active immediately before toggling to rig mode — Validated in Phase 4, hardened in Phase 5's crash-recovery fallback
- [x] User can toggle from normal mode to rig mode in one action from a GUI window — Validated in Phase 5: Orchestration, Full Toggle & Packaging
- [x] User can toggle back from rig mode to normal mode in one action — Validated in Phase 5
- [x] Distributed as a standalone Windows .exe (no separate runtime install required to run it) — Validated in Phase 5
- [x] Multi-monitor enable/disable configuration, generalizing the single "primary monitor to disable" setting (DISPLAY-04/05/06/07/08) — Validated in Phase 6: Multi-Monitor Data Model & Controller Generalization. Rig-validated on real 2-monitor hardware after two gap-closure rounds; post-validation code review fixed two further correctness bugs (migration re-corruption of an emptied disable set; a non-exception-safe minimize step).
- [x] Reentrancy guard: a toggle already in progress safely rejects a second concurrent request (CORE-06) — Validated in Phase 7: Shared Toggle-Orchestration Helper Extraction (non-blocking `Interlocked.CompareExchange` busy-flag on a new `ToggleOrchestrator`, 4 deterministic reentrancy tests, `ToggleService.cs` unchanged)
- [x] Tray residency, autostart, minimize-to-tray-on-close, and tray icon context menu (TRAY-01/02/03/04/05) — Validated in Phase 8: Tray Residency, Autostart & Toast Notification. Rig-validated after fixing a genuine `--tray` hidden-startup bug. Revised in Phase 11: TRAY-01's close-to-tray behavior is now an independent `CloseMinimizesToTray` Settings preference (default off), plus a new independent `MinimizeToTray` preference, with tray-icon existence derived live. Phase 11's critical lockout bug (both preferences off + window hidden → no reachable UI) fixed across two commits and re-verified on the real rig at v1.1 milestone close (2026-08-01).
- [x] Toast/status notification on toggle (NOTIF-01) — Validated in Phase 8 (`NotifyIcon.ShowBalloonTip`, shared `ToggleResultFormatter`)
- [x] Global hotkey trigger, with registration-failure surfacing (TRIG-01) — Validated in Phase 9: Global Hotkey Trigger. Rig-confirmed toggle-from-anywhere including tray-hidden, conflict surfacing with Moza Companion running, and a non-corrupting Settings-dialog race.

### Active

None currently — fresh milestone requirements to be defined via `/gsd:new-milestone`.

### Out of Scope

- Guaranteed true "close main window, keep process running" — not reliably possible to force externally on an arbitrary app; best-effort minimize is the fallback, not a guarantee
- Elevated/Task-Scheduler autostart — would reintroduce the UIPI cross-process-focus problem the v1.0 H9 debug session worked around; plain non-elevated Registry `Run` key is sufficient
- Hotkey chord/sequence engine — unused complexity for a single binding, single action
- Full Windows App SDK / MSIX toast packaging — conflicts with the standalone self-contained-.exe distribution constraint
- Per-monitor sets keyed by index/position instead of stable `DevicePath` — already burned once in v1.0
- Toggle history/log (LOG-01) — deferred twice now (v1.0, v1.1); tracked in v2 backlog, still lower priority
- CLI trigger + single-instance IPC (TRIG-02/TRIG-03) — scoped as Phase 10 for v1.1, never built. Tray (Phase 8) and global hotkey (Phase 9) already cover toggling without the GUI open; decided permanently out of scope at v1.1 close, not a v2 candidate.

## Context

- Personal single-user tool for a sim-racing setup: a Moza wheel/pedals rig sits to the right of the desk with its own monitor and its own speakers (rig mode audio/video). The primary desk monitor and a headset are the normal-use defaults.
- Problem driving this: games launch on the primary monitor by default, and some games (e.g. BeamNG.drive) actively misbehave (self-minimize) when run on what Windows considers a secondary display. The fix is making the primary monitor genuinely absent from Windows' display list while racing.
- No existing single app does this exact combination (monitor disable + audio switch + companion-app launch + tray/hotkey automation + multi-monitor sets as one preset toggle), though individual building blocks exist elsewhere. This project composes those capabilities into one custom GUI tool.
- Shipped state as of v1.1 close (2026-08-01): ~6,900 LOC C# across a 4-project solution (Core/Windows/App/Tests), self-contained win-x64 single-file publish. v1.1 added 186 commits over ~6 days on top of v1.0's 221-commit, 2-day build.
- Post-v1.0-ship hardening (2026-07-26): Moza Companion window-focus-manipulation bug (H9) root-caused and fixed via relaunch-based (`ShellExecute`) activation instead of raw `SetForegroundWindow`/`ShowWindow` calls — see `.planning/debug/resolved/moza-foreground-focus.md`.
- v1.1 rig-discovered/code-review-found bugs, all fixed and verified: `GetAllMonitors()` duplicate-row/dual-primary dedup; `Restore()` Source-ID staleness for enable-set monitors; migration guard re-corrupting an emptied disable set; non-exception-safe companion-app minimize step; `--tray` hidden-start not actually suppressing the window; autostart save-failure recovery itself throwing unhandled; hotkey owner-window-destroyed timing bug; Escape-closes-Settings-during-hotkey-capture; Phase 11's tray-preference lockout bug (two fix commits, rig-reverified at milestone close).
- Known limitation carried forward unpatched: `LaunchOrFocus`/`MinimizeIfRunning` derive the running-process name from the configured launch-target path via `Path.GetFileNameWithoutExtension` — if the user configures a `.lnk` (not the target `.exe` itself), toggle-back's minimize may silently no-op. Documented, out of scope.

## Constraints

- **Platform**: Windows only — no cross-platform requirement
- **Distribution**: Standalone .exe — implies a compiled/self-contained runtime (e.g. .NET self-contained publish), not a bare interpreted script requiring a separately-installed runtime
- **Monitor control**: Must achieve true OS-level display disable/enable (Windows CCD API or equivalent), not merely a monitor power signal — power-off leaves Windows still treating the display as connected/active
- **Audio control**: Must be able to set the Windows default audio playback device programmatically
- **App control**: Must be able to detect if the Moza Companion app is already running (to avoid duplicate launches) and manipulate its window (focus / minimize) via Win32 window APIs
- **State restore**: Must snapshot the active monitor + audio configuration at toggle-time so toggle-back can restore that exact prior state, not a fixed default

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| True OS-level monitor disable, not power-off | Games must see only one display; power-off doesn't remove it from Windows' display list | Validated Phase 4 |
| Remember-previous-state restore, not a fixed "normal" preset | Toggle-back should always match whatever was actually active before | Validated Phase 3 (audio), Phase 4 (monitor), hardened Phase 5 |
| Standalone .exe packaging | No runtime-install friction | Validated Phase 5 |
| Manual launch only, no autostart/tray/hotkey in v1 | Keep v1 scope tight; validate the GUI-click flow before adding automation | Held for v1.0 — TRIG-01/TRAY-01/NOTIF-01 taken up and shipped in v1.1 |
| Best-effort minimize instead of guaranteed close-without-kill for Moza Companion | Can't be forced externally unless the target app supports it itself | Validated Phase 3 |
| Stop-on-first-failure for toggle-to-rig, isolate-and-continue for toggle-to-normal | Forward steps have real dependencies; restore steps recover independent hardware state | Validated Phase 5, rig-confirmed |
| Relaunch-based (`ShellExecute`) app activation instead of window-handle focus manipulation | Raw external `SetForegroundWindow`/`ShowWindow` desyncs Moza's own window procedure, permanently disabling its close button | Validated post-ship 2026-07-26, rig-confirmed both directions |
| Settings accepts any app (drag-and-drop `.lnk`/`.exe`), not a Moza-specific hardcoded path | Free side effect of the relaunch redesign no longer needing Moza-specific window-finding logic | Validated post-ship 2026-07-26 |
| Diagnostic `debug.log` gated behind an off-by-default Settings checkbox | Keeps the capability available without unconditional disk writes | Validated post-ship 2026-07-26 |
| `GetAllMonitors()` dedups by stable `DevicePath`, sourcing Active/Primary state only from `GetActiveMonitors()` | `GetAllPaths()` returns one entry per historical CCD path, causing duplicate rows and dual-primary | Validated Phase 6, rig-confirmed |
| `Restore()`'s cache-replay fast path requires an exact `SetEquals`; any enable-set monitor or stale cache routes through live reconstruction | An intervening CCD mutation can renumber a Source-ID between capture and replay | Validated Phase 6, rig-confirmed |
| Settings-migration guard keys off `MonitorsToDisable is null` only, never null-or-empty | Prior check re-injected the legacy v1.0 monitor into the disable set even after deliberate emptying | Validated Phase 6 |
| Reentrancy guard (CORE-06) is a new `ToggleOrchestrator` wrapper, not logic inside `ToggleService` | Keeps `ToggleService` a pure, unit-tested step sequencer; gives every future trigger source one obvious, already-guarded entry point | Validated Phase 7, 35/35 tests pass |
| `--tray` hidden-startup uses `Application.Run(new ApplicationContext())` with no `MainForm` reference | The Microsoft-doc-cited `ApplicationContext(mainForm)` pattern did not actually suppress `Show()` on this runtime | Validated Phase 8, rig-confirmed |
| Tray icon existence derived as `CloseMinimizesToTray \|\| MinimizeToTray`, applied live on Settings-Save | Lets close-to-tray and minimize-to-tray be configured as two independent preferences instead of one combined flag | Validated Phase 11, rig-confirmed after fixing a lockout bug found by code review |
| Phase 10 (CLI trigger + single-instance IPC, TRIG-02/TRIG-03) permanently out of scope, not delivered | Phase 8/9's tray and hotkey triggers already deliver the "toggle without opening the GUI" core value this milestone targeted; a CLI/IPC path for external tools was judged not needed | Decided at v1.1 close 2026-08-01 |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-08-01 after v1.1 milestone close. v1.1 shipped tray residency, global hotkey trigger, multi-monitor sets, and reentrancy-safe orchestration; Phase 10 (CLI trigger) dropped to v2 backlog. Next: `/gsd:new-milestone`.*
