# Requirements: Rig Toggle

**Defined:** 2026-08-09
**Core Value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably applies Normal mode's own explicitly configured monitor/audio state on toggle-back.

**Note:** v2.1 is a pure UI/UX redesign — no change to Core Value, no change to monitor/audio/process-control logic. It replaces both windows' organically-grown layout with an intentional design (MainForm becomes a monitor-tile dashboard, SettingsForm gets a real layout pass) and closes out the THEME-07/08/09 backlog. `MonitorPanelForm` (PANEL-01..05, shipped v2.0) is retired this milestone — its capability is absorbed into MainForm's tile dashboard under new TILE-* requirements.

## v2.1 Requirements

Requirements for the v2.1 milestone (Modern UI Redesign & Theme Backlog). Each maps to roadmap phases.

### Tile Dashboard

- [ ] **TILE-01**: One tile per detected monitor appears on MainForm, showing an icon, its number, and live on/off status via icon (not text)
- [ ] **TILE-02**: Clicking a tile toggles that monitor's enabled/disabled state directly, taking effect immediately
- [ ] **TILE-03**: Disabling a monitor via a tile is gated by the existing `SkipMonitorConfirmation` setting, identical to the retired panel's behavior
- [ ] **TILE-04**: An Identify action near the tiles briefly overlays a number on each physical screen
- [ ] **TILE-05**: Tab moves keyboard focus between tiles; Space or Enter toggles the focused tile
- [ ] **TILE-06**: The tile row's status refreshes live when a monitor is connected or disconnected while MainForm is visible (not required while hidden in tray)
- [ ] **TILE-07**: The standalone Monitors panel (`MonitorPanelForm`) and both its entry points (MainForm button, tray context-menu item) are removed

### Main Window

- [ ] **MAIN-01**: The Rig/Normal mode toggle sits directly below the monitor tile row, as the next primary action after the tiles
- [ ] **MAIN-02**: The Settings entry point is relocated to a secondary, de-emphasized position — no longer visually competing with the toggle or the tile row

### Settings Layout

- [ ] **SETTINGS-01**: SettingsForm has no overlapping or crowded controls at its default window size
- [ ] **SETTINGS-02**: Related controls (each mode's monitor grid, audio device pickers, app path, hotkey capture) are visually grouped and consistently spaced

### Theme

- [ ] **THEME-07**: Interactive elements — at minimum the toggle switch's "on" state — pick up the live Windows accent color instead of a fixed palette, updating live if the user changes their accent color while the app is running
- [ ] **THEME-08**: The Rig/Normal toggle is a custom-drawn toggle-switch control (track + thumb), remaining distinguishable by shape/position, not color alone
- [ ] **THEME-09**: A System/Light/Dark setting lets the user manually override the app's theme independent of live Windows theme-follow — System preserves today's live-follow behavior; Light/Dark lock the theme and are not silently overridden by an OS theme flip

## Out of Scope

- LOG-01 (toggle history/log) — deferred at v1.0, v1.1, v1.2, and v2.0 scoping; explicitly dropped by the user at v2.1 scoping, not carried forward as a backlog candidate
- Toggle-switch slide animation — polish, not required for this milestone
- Drag-to-rearrange tiles to match physical desk layout — reopens topology/arrangement-editing scope already rejected for `MonitorPanelForm`
- A second, tile-specific confirmation dialog or a fast path bypassing `SkipMonitorConfirmation` — would fragment the DISPLAY-12 safety guard's single shared implementation
- A fourth theme option (e.g. time-of-day auto-switching) — beyond THEME-09's "manual override" framing
- SettingsForm tab/wizard restructuring — bigger, riskier change than the requested layout pass
- CLI trigger + single-instance IPC (TRIG-02/TRIG-03) — permanently out of scope, decided at v1.1 close

## Traceability

_Filled in by the roadmapper when phases are created._

| Requirement | Phase |
|-------------|-------|
