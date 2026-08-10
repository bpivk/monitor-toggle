# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- ✅ **v1.1 Automation & Multi-Monitor** — Phases 6-9, 11 (shipped 2026-08-01) — Phase 10 (CLI trigger) removed, out of scope
- ✅ **v1.2 Visual Polish & Documentation** — Phases 12-14 (shipped 2026-08-04)
- ✅ **v2.0 Configurable Monitors, Optional Targets & Cleanup** — Phases 15-18 (shipped 2026-08-09)
- 🚧 **v2.1 Modern UI Redesign & Theme Backlog** — Phases 19-23 (in progress)

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

<details>
<summary>✅ v1.0 MVP (Phases 1-5) — SHIPPED 2026-07-26</summary>

- [x] Phase 1: Monitor-Disable Feasibility Spike (2/2 plans) — completed 2026-07-24
- [x] Phase 2: Foundations & GUI Shell (5/5 plans) — completed 2026-07-24
- [x] Phase 3: App & Audio Control (4/4 plans) — completed 2026-07-24
- [x] Phase 4: Monitor Control (Production) (4/4 plans) — completed 2026-07-24
- [x] Phase 5: Orchestration, Full Toggle & Packaging (3/3 plans) — completed 2026-07-25

Full phase details: `.planning/milestones/v1.0-ROADMAP.md`

</details>

<details>
<summary>✅ v1.1 Automation & Multi-Monitor (Phases 6-9, 11) — SHIPPED 2026-08-01</summary>

- [x] Phase 6: Multi-Monitor Data Model & Controller Generalization (6/6 plans) — completed 2026-07-29
- [x] Phase 7: Shared Toggle-Orchestration Helper Extraction (1/1 plans) — completed 2026-07-29
- [x] Phase 8: Tray Residency, Autostart & Toast Notification (4/4 plans) — completed 2026-07-31
- [x] Phase 9: Global Hotkey Trigger (4/4 plans) — completed 2026-07-31
- [~] Phase 10: CLI Trigger + Single-Instance IPC — scoped, not delivered, decided permanently out of scope (TRIG-02/TRIG-03)
- [x] Phase 11: Configurable Tray Close/Minimize Behavior (4/4 plans) — completed 2026-08-01

Full phase details: `.planning/milestones/v1.1-ROADMAP.md`

</details>

<details>
<summary>✅ v1.2 Visual Polish & Documentation (Phases 12-14) — SHIPPED 2026-08-04</summary>

- [x] Phase 12: Theme Infrastructure & Live Theme-Following (6/6 plans) — completed 2026-08-03
- [x] Phase 13: Tray & App Icon Redesign (4/4 plans) — completed 2026-08-03
- [x] Phase 14: README & Release Documentation (3/3 plans) — completed 2026-08-03

Full phase details: `.planning/milestones/v1.2-ROADMAP.md`

</details>

<details>
<summary>✅ v2.0 Configurable Monitors, Optional Targets & Cleanup (Phases 15-18) — SHIPPED 2026-08-09</summary>

- [x] Phase 15: Optional App & Audio Targets (4/4 plans) — completed 2026-08-04
- [x] Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign (5/5 plans) — completed 2026-08-08
- [x] Phase 17: Manual Monitor Panel & Shared Safety Guard (4/4 plans) — completed 2026-08-08
- [x] Phase 18: Cleanup Pass & Exe-Size Reduction (6/6 plans) — completed 2026-08-09

Full phase details: `.planning/milestones/v2.0-ROADMAP.md`

</details>

### 🚧 v2.1 Modern UI Redesign & Theme Backlog (In Progress)

**Milestone Goal:** Replace both windows' bolted-on-feature layout with an intentional, modern design — MainForm becomes a monitor-tile dashboard leading into the mode toggle, SettingsForm gets a real layout pass, and the deferred theme backlog (accent color, custom toggle switch, manual override) closes out. Pure UI/UX redesign — no change to Core Value, no change to monitor/audio/process-control logic.

- [x] **Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement** - MainForm becomes a monitor-tile dashboard absorbing the standalone Monitors panel's capability; toggle repositioned below the tiles, Settings de-emphasized (completed 2026-08-10)
- [ ] **Phase 20: Custom Toggle-Switch Control** - The Rig/Normal toggle becomes a custom-drawn track+thumb switch (THEME-08)
- [ ] **Phase 21: Accent-Color Reading & Live Update** - Interactive elements pick up the live Windows accent color (THEME-07)
- [ ] **Phase 22: Manual Light/Dark Override** - A System/Light/Dark setting overrides live theme-follow (THEME-09)
- [ ] **Phase 23: SettingsForm Layout Pass** - SettingsForm's controls are regrouped and respaced, no overlap at default size

## Phase Details

### Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement
**Goal**: MainForm becomes a monitor-tile dashboard — users manage per-monitor enable/disable directly from clickable tiles on the main window — and the standalone Monitors panel is fully retired.
**Depends on**: Nothing (first phase of v2.1)
**Requirements**: TILE-01, TILE-02, TILE-03, TILE-04, TILE-05, TILE-06, TILE-07, MAIN-01, MAIN-02
**Success Criteria** (what must be TRUE):
  1. MainForm shows one tile per detected monitor, each with an icon, its number, and live on/off status conveyed via icon (not text)
  2. Clicking a tile immediately toggles that monitor's enabled/disabled state, gated by the existing `SkipMonitorConfirmation` setting when disabling
  3. User can Tab between tiles and press Space or Enter to toggle the currently focused tile
  4. An Identify action near the tiles briefly overlays a number on each physical screen
  5. The Rig/Normal toggle sits directly below the tile row and Settings is relocated to a secondary, de-emphasized position; the standalone `MonitorPanelForm` and both its entry points (MainForm button, tray menu item) no longer exist
**Plans**: 5 plans
Plans:
**Wave 1**

- [x] 19-01-PLAN.md — Standalone building blocks: ported GDI+ monitor-icon geometry helper + the MonitorTile presentational UserControl (unwired)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 19-02-PLAN.md — MainForm hosts the tile dashboard: layout, read-only population from canonical ordering, hotplug refresh, theming both call sites

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 19-03-PLAN.md — Tiles go live: ported mutation path (lease → confirmation gate → IMonitorController call → refresh) + Identify handler

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 19-04-PLAN.md — Retire MonitorPanelForm and both entry points (TILE-07)

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 19-05-PLAN.md — Full-solution regression gate, four static audits, and rig verification of all five success criteria

**UI hint**: yes

### Phase 20: Custom Toggle-Switch Control
**Goal**: The Rig/Normal mode action reads as a modern toggle switch instead of a plain button.
**Depends on**: Phase 19 (repositions the same control this phase redraws)
**Requirements**: THEME-08
**Success Criteria** (what must be TRUE):
  1. The Rig/Normal toggle renders as a custom-drawn track+thumb switch, not a standard `Button`
  2. The switch's on/off state is distinguishable by track/thumb shape and position alone, without relying on color
  3. The switch remains keyboard-operable (Tab focus, Space/Enter activates) and renders correctly themed in both light and dark mode, including on tray-hidden startup
**Plans**: TBD
**UI hint**: yes

### Phase 21: Accent-Color Reading & Live Update
**Goal**: Key interactive elements reflect the user's actual live Windows accent color instead of a fixed palette.
**Depends on**: Phase 20 (the toggle switch's "on" state is the primary accent-tinted element)
**Requirements**: THEME-07
**Success Criteria** (what must be TRUE):
  1. The toggle switch's "on" state (and any other designated interactive element) visibly uses the current Windows accent color
  2. Changing the Windows accent color while the app is running updates the app's accent-tinted elements live, without restarting the app
  3. The accent color shown in the app matches what Settings > Colors displays, including for a custom (non-default) accent color
**Plans**: TBD
**UI hint**: yes

### Phase 22: Manual Light/Dark Override
**Goal**: Users can lock the app's theme to Light or Dark independent of live Windows theme-follow, or keep today's live-follow behavior.
**Depends on**: Phase 21 (decorates the theme interface Phase 21 extends with accent color)
**Requirements**: THEME-09
**Success Criteria** (what must be TRUE):
  1. Settings offers a System/Light/Dark choice, defaulting to System
  2. Selecting Light or Dark immediately locks the app's theme and applies without restarting the app
  3. Once locked to Light or Dark, a live OS theme flip does not silently override the app's theme; selecting System restores today's live-follow behavior
**Plans**: TBD
**UI hint**: yes

### Phase 23: SettingsForm Layout Pass
**Goal**: SettingsForm reads as an intentionally laid-out screen instead of a crowded, organically-grown one.
**Depends on**: Nothing (independent of Phases 19-22; can execute any time in the sequence)
**Requirements**: SETTINGS-01, SETTINGS-02
**Success Criteria** (what must be TRUE):
  1. SettingsForm has no overlapping or crowded controls at its default window size
  2. Each mode's monitor grid, the audio device pickers, the app path control, and the hotkey capture box are visually grouped and consistently spaced
**Plans**: TBD
**UI hint**: yes

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|-----------------|--------|-----------|
| 1. Monitor-Disable Feasibility Spike | v1.0 | 2/2 | Complete | 2026-07-24 |
| 2. Foundations & GUI Shell | v1.0 | 5/5 | Complete | 2026-07-24 |
| 3. App & Audio Control | v1.0 | 4/4 | Complete | 2026-07-24 |
| 4. Monitor Control (Production) | v1.0 | 4/4 | Complete | 2026-07-24 |
| 5. Orchestration, Full Toggle & Packaging | v1.0 | 3/3 | Complete | 2026-07-25 |
| 6. Multi-Monitor Data Model & Controller Generalization | v1.1 | 6/6 | Complete | 2026-07-29 |
| 7. Shared Toggle-Orchestration Helper Extraction | v1.1 | 1/1 | Complete | 2026-07-29 |
| 8. Tray Residency, Autostart & Toast Notification | v1.1 | 4/4 | Complete | 2026-07-31 |
| 9. Global Hotkey Trigger | v1.1 | 4/4 | Complete | 2026-07-31 |
| 10. CLI Trigger + Single-Instance IPC | v1.1 | 0/TBD | Removed — out of scope | - |
| 11. Configurable Tray Close/Minimize Behavior | v1.1 | 4/4 | Complete | 2026-08-01 |
| 12. Theme Infrastructure & Live Theme-Following | v1.2 | 6/6 | Complete | 2026-08-03 |
| 13. Tray & App Icon Redesign | v1.2 | 4/4 | Complete | 2026-08-03 |
| 14. README & Release Documentation | v1.2 | 3/3 | Complete | 2026-08-03 |
| 15. Optional App & Audio Targets | v2.0 | 4/4 | Complete    | 2026-08-04 |
| 16. Normal-Mode Explicit Monitor Config & Mode-Store Redesign | v2.0 | 5/5 | Complete   | 2026-08-08 |
| 17. Manual Monitor Panel & Shared Safety Guard | v2.0 | 4/4 | Complete   | 2026-08-08 |
| 18. Cleanup Pass & Exe-Size Reduction | v2.0 | 6/6 | Complete   | 2026-08-09 |
| 19. Monitor-Tile Dashboard & MonitorPanelForm Retirement | v2.1 | 5/5 | Complete   | 2026-08-10 |
| 20. Custom Toggle-Switch Control | v2.1 | 1/3 | In Progress|  |
| 21. Accent-Color Reading & Live Update | v2.1 | 0/TBD | Not started | - |
| 22. Manual Light/Dark Override | v2.1 | 0/TBD | Not started | - |
| 23. SettingsForm Layout Pass | v2.1 | 0/TBD | Not started | - |

## Backlog

Requirements not yet scoped into a milestone. LOG-01 (toggle history/log) was deferred at v1.0, v1.1, v1.2, and v2.0 scoping, then explicitly dropped by the user at v2.1 scoping — no longer tracked as a backlog candidate. CLI trigger/TRIG-02/TRIG-03 was reviewed at v1.1 close and decided permanently out of scope. THEME-07/08/09 (accent-color highlight, custom toggle-switch control, manual theme override), deferred since v1.2, are scoped into v2.1 Phases 20-22.

---
*Next: `/gsd:plan-phase 19`*
