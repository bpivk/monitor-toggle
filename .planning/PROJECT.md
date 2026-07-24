# Rig Toggle

## What This Is

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode" with one click. Toggling to rig mode disables the primary monitor at the OS level (so games default to the rig monitor instead), switches the default audio output to the rig speakers, and launches the Moza Companion app. Toggling back restores the exact previous monitor/audio state and minimizes the Moza Companion app. Built for a single user's personal sim-racing rig setup.

## Core Value

A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches (e.g. BeamNG.drive minimizing itself) reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] User can toggle from normal mode to rig mode in one action from a GUI window
- [ ] Toggling to rig mode disables the primary monitor at the OS level (true disable, e.g. via Windows CCD/display API or MultiMonitorTool-equivalent — not just a DDC/CI power-off, since Windows must see only the rig monitor)
- [ ] Toggling to rig mode switches the default audio output device to the rig speakers
- [ ] Toggling to rig mode launches the Moza Companion app if it isn't already running; if it's already running, brings it to focus instead of launching a duplicate instance
- [ ] User can toggle back from rig mode to normal mode in one action
- [ ] Toggling back restores the exact monitor and audio configuration that was active immediately before toggling to rig mode (not a fixed hardcoded preset)
- [ ] Toggling back minimizes the Moza Companion app window (best-effort — true "close window but keep process alive" only if the app itself supports minimize-to-tray-on-close; otherwise just minimize)
- [ ] GUI includes a settings view where the user selects: which monitor is the "primary to disable," which audio devices are the toggle pair, and which app (path) to launch/minimize
- [ ] Distributed as a standalone Windows .exe (no separate runtime install required to run it)

### Out of Scope

- Global hotkey trigger — deferred, GUI-only trigger for v1
- System tray residency / auto-start on Windows boot — deferred, manual launch only for v1
- Guaranteed true "close main window, keep process running" — not reliably possible to force externally on an arbitrary app; best-effort minimize is the fallback, not a v1 guarantee

## Context

- Personal single-user tool for a sim-racing setup: a Moza wheel/pedals rig sits to the right of the desk with its own monitor and its own speakers (rig mode audio/video). The primary desk monitor and a headset are the normal-use defaults.
- Problem driving this: games launch on the primary monitor by default, and some games (e.g. BeamNG.drive) actively misbehave (self-minimize) when run on what Windows considers a secondary display — even if manually moved there. The fix is making the primary monitor genuinely absent from Windows' display list while racing, not just visually secondary.
- No existing single app does this exact combination (monitor disable + audio switch + companion-app launch as one preset toggle), though individual building blocks exist (NirSoft MultiMonitorTool for monitor enable/disable, AudioDeviceCmdlets/NirSoft SoundVolumeView for default audio device switching, Windows CCD/Win32 APIs for both). This project composes those capabilities into one custom GUI tool.

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
| True OS-level monitor disable, not power-off | Games must see only one display; power-off doesn't remove it from Windows' display list | — Pending |
| Remember-previous-state restore, not a fixed "normal" preset | Toggle-back should always match whatever was actually active before, avoiding surprises | — Pending |
| Standalone .exe packaging | No runtime-install friction — just run the file | — Pending |
| Manual launch only, no autostart/tray/hotkey in v1 | Keep v1 scope tight; these are easy additions later if wanted | — Pending |
| Best-effort minimize instead of guaranteed close-without-kill for Moza Companion | Can't be forced externally unless the target app supports it itself | — Pending |

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
*Last updated: 2026-07-24 after initialization*
