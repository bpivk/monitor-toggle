# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- ✅ **v1.1 Automation & Multi-Monitor** — Phases 6-9, 11 (shipped 2026-08-01) — Phase 10 (CLI trigger) removed, out of scope
- ✅ **v1.2 Visual Polish & Documentation** — Phases 12-14 (shipped 2026-08-04)
- ✅ **v2.0 Configurable Monitors, Optional Targets & Cleanup** — Phases 15-18 (shipped 2026-08-09)
- ✅ **v2.1 Modern UI Redesign & Theme Backlog** — Phases 19-23 (shipped 2026-08-17)
- ✅ **v2.2 Auto-Update, Single-Instance Guard & Smaller Footprint** — Phases 24-26 (shipped 2026-08-29)

## Phases

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

<details>
<summary>✅ v2.1 Modern UI Redesign & Theme Backlog (Phases 19-23) — SHIPPED 2026-08-17</summary>

- [x] Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement (5/5 plans) — completed 2026-08-10
- [x] Phase 20: Custom Toggle-Switch Control (3/3 plans) — completed 2026-08-10
- [x] Phase 21: Accent-Color Reading & Live Update (3/3 plans) — completed 2026-08-11
- [x] Phase 22: SettingsForm Layout Pass (5/5 plans) — completed 2026-08-16
- [x] Phase 23: Manual Light/Dark Override (3/3 plans) — completed 2026-08-17

Full phase details: `.planning/milestones/v2.1-ROADMAP.md`

</details>

<details>
<summary>✅ v2.2 Auto-Update, Single-Instance Guard & Smaller Footprint (Phases 24-26) — SHIPPED 2026-08-29</summary>

- [x] Phase 24: Self-Contained Exe Size Reduction (1/1 plans) — completed 2026-08-19
- [x] Phase 25: Single-Instance Guard (4/4 plans) — completed 2026-08-21
- [x] Phase 26: Auto-Update (5/5 plans) — completed 2026-08-29 (UAT test 1 rig-verified live; tests 2-5 closed by operator sign-off — see STATE.md and `26-UAT.md`)

Full phase details: `.planning/milestones/v2.2-ROADMAP.md`

</details>

## Backlog

Requirements not yet scoped into a milestone. LOG-01 (toggle history/log) was deferred at v1.0, v1.1, v1.2, and v2.0 scoping, then explicitly dropped by the user at v2.1 scoping — no longer tracked as a backlog candidate. CLI trigger/TRIG-02/TRIG-03 was reviewed at v1.1 close and decided permanently out of scope. THEME-07/08/09 (accent-color highlight, custom toggle-switch control, manual theme override), deferred since v1.2, shipped in v2.1 (Phases 20, 21, and 23). UPDATE-08 (richer WM_COPYDATA/named-pipe IPC between instances) and DIST-01 (Velopack/installer packaging reconsideration) are tracked in `.planning/REQUIREMENTS.md`'s Future Requirements section, not currently scoped into any milestone.
