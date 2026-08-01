# Rig Toggle

## What This Is

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode" with one click. Toggling to rig mode disables the primary monitor at the OS level (so games default to the rig monitor instead), switches the default audio output to the rig speakers, and launches the Moza Companion app. Toggling back restores the exact previous monitor/audio state and minimizes the Moza Companion app. Built for a single user's personal sim-racing rig setup.

## Core Value

A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches (e.g. BeamNG.drive minimizing itself) reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## Current Milestone: v1.1 Automation & Multi-Monitor

**Goal:** Remove the remaining daily-use friction (must open the GUI to toggle; only one monitor can be disabled) by adding background/automated triggering and generalizing monitor control to arbitrary multi-monitor rigs.

**Target features:**
- Tray residency (autostart on boot, minimize-to-tray on close) with a tray icon context menu (Switch to Rig/Normal Mode, Settings, Exit), configurable via Settings
- Global hotkey trigger (Windows-wide keyboard shortcut)
- CLI trigger (command-line args for macro pads / Stream Deck / external tools)
- Toast/status notification confirming a toggle when triggered without the GUI open
- Multi-monitor enable/disable configuration: independently-configurable "disable" and "enable" monitor sets applied when entering rig mode (mirrored on toggle-back) — generalizes v1.0's single "primary monitor to disable" to arbitrary multi-monitor desks and a rig monitor that's normally kept OS-disabled to save power

**Deferred from this milestone:** LOG-01 (toggle history/log) — still a nice-to-have, lower priority than the above.

## Requirements

### Validated

- [x] GUI includes a settings view where the user selects: which monitor is the "primary to disable," which audio devices are the toggle pair, and which app (path) to launch/minimize — Validated in Phase 2: Foundations & GUI Shell (real enumeration wired into a modal Settings dialog with Save-gating, stale-device detection, and persistence confirmed across a true app restart on the rig)
- [x] Toggling to rig mode switches the default audio output device to the rig speakers — Validated in Phase 3: App & Audio Control (real hand-embedded `IPolicyConfig` COM interop, all three audio roles, verify-and-throw on mismatch)
- [x] Toggling back restores the exact previous default audio device across all relevant audio roles — Validated in Phase 3 (per-role capture + restore, stale-device-ID fallback via friendly-name match, per-role failure isolation added in gap-closure plan 03-04)
- [x] Toggling to rig mode launches the Moza Companion app if it isn't already running; if it's already running, brings it to focus instead of launching a duplicate instance — Validated in Phase 3, mechanism superseded post-ship: rig-testing surfaced a bug (H9 — Moza's close button going permanently inert after `SetForegroundWindow`/window-focus manipulation) traced across a 10-round evidence-driven debug session, then fixed by redesigning `LaunchOrFocus` to an unconditional `Process.Start(UseShellExecute=true)` relaunch that never touches a window it doesn't own, relying on the target app's own single-instance activation — rig-confirmed working for both directions (launch-to-rig and toggle-back). Settings also generalized from a Moza-specific path to any `.lnk`/`.exe` target (drag-and-drop or Browse), so this requirement now holds for any well-behaved single-instance Windows app, not just Moza Companion.
- [x] Toggling back minimizes the Moza Companion app window (best-effort) — Validated in Phase 3 (`ShowWindow`/`SW_MINIMIZE`, always runs even if audio restore throws); refined post-ship to skip the minimize call entirely when the window is already hidden/tray-only, after rig evidence showed the unconditional call was forcing a hidden window back to visible and re-triggering the H9 close-inert symptom on that path too
- [x] Toggling to rig mode disables the primary monitor at the OS level (true disable) — Validated in Phase 4: Monitor Control (Production) (repositioning-aware CCD `ApplyPathInfos`, verify-and-throw against a fresh `GetActivePaths()` re-query)
- [x] Toggling back restores the exact monitor configuration that was active immediately before toggling to rig mode — Validated in Phase 4 (full-topology snapshot, live-identity re-resolution restore) and hardened in Phase 5's crash-recovery fallback (`ApplyTopology(Extend)` + reposition-from-live-objects, rig-tested including a forced-close retest)
- [x] User can toggle from normal mode to rig mode in one action from a GUI window — Validated in Phase 5: Orchestration, Full Toggle & Packaging (single-click, monitor+audio+app all switch together, rig-confirmed)
- [x] User can toggle back from rig mode to normal mode in one action — Validated in Phase 5 (single-click, monitor+audio+app all restore together, rig-confirmed)
- [x] Distributed as a standalone Windows .exe (no separate runtime install required to run it) — Validated in Phase 5 (self-contained/single-file/untrimmed win-x64 publish, rig-confirmed launch with no runtime-install prompt)
- [x] Multi-monitor enable/disable configuration, generalizing the single "primary monitor to disable" setting (DISPLAY-04/05/06/07/08) — Validated in Phase 6: Multi-Monitor Data Model & Controller Generalization (N-monitor disable/enable sets via generalized `IMonitorController` triad and real CCD `ActivateMonitors`/`DeactivateMonitors`, save-time guard against disabling every monitor, confirmation dialog naming every monitor in both sets, silent v1.0→v1.1 settings migration). Rig-validated on real 2-monitor hardware after two gap-closure rounds (`GetAllMonitors()` duplicate-row/dual-primary dedup fix; `Restore()` Source-staleness fix routing enable-set monitors through live reconstruction). Post-validation code review then found and fixed two further correctness bugs: a migration guard that re-corrupted a deliberately-emptied disable set, and a non-exception-safe companion-app minimize step that could strand the UI in "Mode: Rig."
- [x] Tray residency, autostart, minimize-to-tray-on-close, and tray icon context menu (TRAY-01/02/03/04/05) — Validated in Phase 8: Tray Residency, Autostart & Toast Notification (`NotifyIcon`+`ContextMenuStrip` hosted on `MainForm`, close-to-tray via `CloseReason.UserClosing` gating, mode-reflecting silhouette-distinct tray icons, HKCU `Run`-key autostart via a new `IAutostartConfigurator`/`WindowsAutostartConfigurator` adapter). Rig-validated on real hardware after a genuine bug was found and fixed mid-checkpoint: the `--tray` hidden-startup mechanism (`Application.Run(new ApplicationContext(mainForm))`) did not actually suppress showing the window, contradicting the citation-backed research theory — fixed to a `MainForm`-less `ApplicationContext`. Post-validation code review then found and fixed one further crash-risk bug (autostart save-failure recovery could itself throw unhandled from inside its own error-recovery path) plus several smaller warnings (icon disposal, null-safety gaps). Revised in Phase 11: Configurable Tray Close/Minimize Behavior — TRAY-01's previously-fixed close-to-tray behavior is now a `CloseMinimizesToTray` Settings preference (default off: X exits the app), plus a new independent `MinimizeToTray` preference; the tray icon's own existence is now derived (`CloseMinimizesToTray || MinimizeToTray`) and applied live on Settings-Save. Code review found and fixed a critical lockout bug (disabling both preferences from Settings while the window was hidden to tray could leave the app with no reachable UI surface at all) across two fix commits — the second fix commit itself postdates the human-verify checkpoint, so on-rig re-confirmation of the fix and a regression spot-check are tracked as pending in `11-HUMAN-UAT.md`, not yet closed.
- [x] Toast/status notification on toggle (NOTIF-01) — Validated in Phase 8 (`NotifyIcon.ShowBalloonTip`, not a packaged toast API; fires unconditionally on every tray-menu-triggered toggle; content reuses the GUI's own per-step checklist wording via a new shared `RigToggle.Core.ToggleResultFormatter`, relocated out of `MainForm` for reuse and unit-testability).

### Active

- [ ] Global hotkey trigger (v1.1)
- [ ] CLI trigger for external tools (v1.1)

### Out of Scope

- Guaranteed true "close main window, keep process running" — not reliably possible to force externally on an arbitrary app; best-effort minimize is the fallback, not a v1 guarantee
- Toggle history/log (LOG-01) — deferred again past v1.1, still lower priority than the automation/multi-monitor features

## Context

- Personal single-user tool for a sim-racing setup: a Moza wheel/pedals rig sits to the right of the desk with its own monitor and its own speakers (rig mode audio/video). The primary desk monitor and a headset are the normal-use defaults.
- Problem driving this: games launch on the primary monitor by default, and some games (e.g. BeamNG.drive) actively misbehave (self-minimize) when run on what Windows considers a secondary display — even if manually moved there. The fix is making the primary monitor genuinely absent from Windows' display list while racing, not just visually secondary.
- No existing single app does this exact combination (monitor disable + audio switch + companion-app launch as one preset toggle), though individual building blocks exist (NirSoft MultiMonitorTool for monitor enable/disable, AudioDeviceCmdlets/NirSoft SoundVolumeView for default audio device switching, Windows CCD/Win32 APIs for both). This project composes those capabilities into one custom GUI tool.
- Shipped state: ~3,673 LOC C# across a 4-project solution (Core/Windows/App/Tests), self-contained win-x64 single-file publish, 221 commits over the 2-day v1.0 build.
- Post-ship hardening (2026-07-26, same day as v1.0 close): rig use surfaced a real regression — Moza Companion's own window close (X/Alt+F4/taskbar-close) went permanently inert specifically on windows RigToggle had brought to the foreground. Root-caused across a 10-round evidence-driven debug session (`.planning/debug/resolved/moza-foreground-focus.md`) to raw external Win32 window-state manipulation (`SetForegroundWindow`/`ShowWindow`) desyncing something in Moza's own window procedure. Fixed by eliminating that class of call entirely: `LaunchOrFocus` now just relaunches the configured target via `ShellExecute` and trusts the target's own single-instance activation, and `MinimizeIfRunning` skips its `ShowWindow(SW_MINIMIZE)` call when the window is already hidden. Both directions rig-confirmed. Also generalized Settings to accept any app (drag-and-drop `.lnk`/`.exe`, not just a Moza-specific path) as a side effect of the mechanism no longer being Moza-specific, and made the diagnostic `debug.log` opt-in via a Settings checkbox (off by default) now that the investigation that needed it is closed.

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
| Remember-previous-state restore, not a fixed "normal" preset | Toggle-back should always match whatever was actually active before, avoiding surprises | Validated Phase 3 (audio), Phase 4 (monitor), hardened Phase 5 (crash-recovery restore) |
| Standalone .exe packaging | No runtime-install friction — just run the file | Validated Phase 5 |
| Manual launch only, no autostart/tray/hotkey in v1 | Keep v1 scope tight; validate the GUI-click flow before adding automation | Held for v1 — TRIG-01/TRAY-01/NOTIF-01 taken up in v1.1 now that the core flow is rig-validated and hardened; LOG-01 still deferred |
| Best-effort minimize instead of guaranteed close-without-kill for Moza Companion | Can't be forced externally unless the target app supports it itself | Validated Phase 3 |
| Stop-on-first-failure for toggle-to-rig, isolate-and-continue for toggle-to-normal | Forward steps have real dependencies (no point switching audio if the monitor never disabled); restore steps recover independent hardware state, so one failing shouldn't block the others | Validated Phase 5, rig-confirmed |
| Relaunch-based (`ShellExecute`) app activation instead of window-handle focus manipulation | Rig-testing proved any raw external `SetForegroundWindow`/`ShowWindow` call on Moza's window desyncs something in its own window procedure, permanently disabling its close button; a well-behaved single-instance app already handles "already running, activate me" internally when relaunched, so RigToggle never needs to touch a window it doesn't own | Validated post-ship 2026-07-26, rig-confirmed both directions (`.planning/debug/resolved/moza-foreground-focus.md`) |
| Settings accepts any app (drag-and-drop `.lnk`/`.exe`), not a Moza-specific hardcoded path | Free side effect of the relaunch redesign no longer depending on Moza-specific window-finding logic; costs nothing extra and generalizes the tool | Validated post-ship 2026-07-26 |
| Diagnostic `debug.log` gated behind an off-by-default Settings checkbox rather than always-on or fully removed | Keeps the capability available for a future issue without a rebuild, but stops unconditional disk writes now that the investigation needing it is closed | Validated post-ship 2026-07-26 |
| `GetAllMonitors()` dedups by stable `DevicePath`, sourcing Active/Primary state only from `GetActiveMonitors()` | Rig-confirmed bug: `GetAllPaths()` returns one entry per historical CCD path, causing duplicate rows and dual-primary in the Settings grid | Validated Phase 6, rig-confirmed |
| `Restore()`'s cache-replay fast path requires an exact `SetEquals` between cache and previous state; any enable-set monitor or stale cache routes through live reconstruction instead | Rig-confirmed: the cache can legitimately contain an enable-set monitor whose Source-ID was renumbered by an intervening CCD mutation between capture and replay, silently leaving it inactive if blindly replayed | Validated Phase 6, rig-confirmed (`.planning/debug/resolved/monitor-not-active-on-restore.md`) |
| Settings-migration guard keys off `MonitorsToDisable is null` only, never on an empty-but-non-null list | Code review (06-REVIEW.md CR-01) found the prior null-or-empty check re-injected the legacy v1.0 monitor into the disable set on every load, even after a user deliberately emptied it (e.g. moving to enable-only) — `MonitorDevicePath` is never scrubbed post-migration, so that condition could never become permanently false | Validated Phase 6 |
| `ToggleToNormalMode`'s companion-app minimize step wrapped in try/catch like every other restore step | Code review (06-REVIEW.md CR-02) found this was the only unguarded step, contradicting the class's own isolate-and-continue invariant — a throw here would skip `snapshot.Clear()` and permanently strand the UI showing "Mode: Rig" | Validated Phase 6 |
| Reentrancy guard (CORE-06) is a new `ToggleOrchestrator` wrapper around `ToggleService`, not logic added inside `ToggleService` itself — a non-blocking `Interlocked.CompareExchange` busy-flag shared across both toggle directions, rejecting a second in-flight request immediately (never queuing/blocking it) | Roadmap explicitly required "rejects", not "eventually executes"; keeps `ToggleService` a pure, already-unit-tested step sequencer with no concurrency concerns, and gives Phase 8-10's future trigger sources (tray/hotkey/CLI) one obvious, already-guarded entry point instead of duplicating guard logic per trigger | Validated Phase 7 — `ToggleService.cs` confirmed byte-unchanged across the phase; 35/35 tests pass including 4 deterministic reentrancy tests |
| `--tray` hidden-startup uses `Application.Run(new ApplicationContext())` with no `MainForm` reference, not `Application.Run(new ApplicationContext(mainForm))` | Rig-discovered: passing `mainForm` into `ApplicationContext` was expected (per Microsoft-doc-cited research) to suppress `Show()`, but empirically still showed the window on this runtime. Giving `ApplicationContext` no main form at all is the actual working pattern — `mainForm` is held as a local reference and shown later, on demand, via the tray icon's own handlers; `Application.Exit()` still terminates the loop correctly without any `ApplicationContext.MainForm` wiring | Validated Phase 8, rig-confirmed both the hidden start itself and the dependent Exit-while-never-shown scenario |
| Autostart save-failure recovery path (`SettingsForm.BtnSaveSettings_Click`'s catch block) wraps its own `IsEnabled()` read in a nested try/catch | Code review (08-REVIEW.md CR-01) found the recovery read — whose whole purpose is to degrade gracefully when the registry write fails — was itself unguarded, so a second registry failure crashed the app from inside the error-recovery path meant to prevent exactly that | Validated Phase 8 |

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
*Last updated: 2026-08-01 — Phase 11 (Configurable Tray Close/Minimize Behavior) complete: TRAY-01's fixed close-to-tray behavior revised into two independent Settings preferences with derived tray-icon visibility. Code review found and fixed a critical lockout bug across two commits; on-rig re-confirmation of the fix and a regression spot-check remain pending in `11-HUMAN-UAT.md`. This was the last planned phase of v1.1 Automation & Multi-Monitor.*
