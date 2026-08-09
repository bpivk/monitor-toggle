# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- ✅ **v1.1 Automation & Multi-Monitor** — Phases 6-9, 11 (shipped 2026-08-01) — Phase 10 (CLI trigger) removed, out of scope
- ✅ **v1.2 Visual Polish & Documentation** — Phases 12-14 (shipped 2026-08-04)
- ✅ **v2.0 Configurable Monitors, Optional Targets & Cleanup** — Phases 15-18 (shipped 2026-08-09)

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

## Backlog

Requirements not yet scoped into a milestone. See `.planning/milestones/v2.0-REQUIREMENTS.md` "v2 Requirements (Deferred)" for the current deferred list (LOG-01 toggle history/log; THEME-07/08/09 accent-color highlight, custom toggle-switch control, manual theme override — all deferred at v1.2 scoping). CLI trigger/TRIG-02/TRIG-03 was reviewed at v1.1 close and decided permanently out of scope, not carried forward.

---
*Next: `/gsd:new-milestone` to scope the next milestone.*
