# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- ✅ **v1.1 Automation & Multi-Monitor** — Phases 6-9, 11 (shipped 2026-08-01) — Phase 10 (CLI trigger) removed, out of scope
- ✅ **v1.2 Visual Polish & Documentation** — Phases 12-14 (shipped 2026-08-04)
- ✅ **v2.0 Configurable Monitors, Optional Targets & Cleanup** — Phases 15-18 (shipped 2026-08-09)
- ✅ **v2.1 Modern UI Redesign & Theme Backlog** — Phases 19-23 (shipped 2026-08-17)
- 🚧 **v2.2 Auto-Update, Single-Instance Guard & Smaller Footprint** — Phases 24-26 (planned)

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

<details>
<summary>✅ v2.1 Modern UI Redesign & Theme Backlog (Phases 19-23) — SHIPPED 2026-08-17</summary>

- [x] Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement (5/5 plans) — completed 2026-08-10
- [x] Phase 20: Custom Toggle-Switch Control (3/3 plans) — completed 2026-08-10
- [x] Phase 21: Accent-Color Reading & Live Update (3/3 plans) — completed 2026-08-11
- [x] Phase 22: SettingsForm Layout Pass (5/5 plans) — completed 2026-08-16
- [x] Phase 23: Manual Light/Dark Override (3/3 plans) — completed 2026-08-17

Full phase details: `.planning/milestones/v2.1-ROADMAP.md`

</details>

### 🚧 v2.2 Auto-Update, Single-Instance Guard & Smaller Footprint (Planned)

**Milestone Goal:** Keep the app current and well-behaved as a background/tray-resident tool — check GitHub for new releases and let the user install them with one confirm, prevent duplicate instances from ever running side-by-side, and continue trimming the self-contained exe's size using only safe (non-trimming) levers.

- [ ] **Phase 24: Self-Contained Exe Size Reduction** - Additional safe MSBuild-only levers shrink the exe further below the v2.1 baseline, no IL trimming/AOT/ReadyToRun
- [ ] **Phase 25: Single-Instance Guard** - A duplicate launch focuses the already-running instance instead of starting a second process; establishes the bypass pattern auto-update's relaunch will reuse
- [ ] **Phase 26: Auto-Update** - On-launch GitHub Releases check, confirm-before-install prompt, safe self-replace-and-relaunch, and a manual "Check for Updates" action

## Phase Details

### Phase 24: Self-Contained Exe Size Reduction

**Goal**: The self-contained exe is measurably smaller than the v2.1 baseline, using only safe, non-trimming MSBuild levers.
**Depends on**: Nothing (independent of Phase 25 and Phase 26)
**Requirements**: PERF-03
**Success Criteria** (what must be TRUE):

  1. A fresh self-contained win-x64 publish produces an exe smaller than the recorded v2.1 baseline (49,356,430 bytes), with exact before/after byte counts documented
  2. A full toggle round trip (Rig → Normal → Rig) and a cold autostart boot both succeed on real rig hardware after the size-reduction changes, with no functional regression
  3. No IL trimming (`PublishTrimmed`), Native AOT (`PublishAot`), or `PublishReadyToRun` properties were introduced — only additive, safe MSBuild feature switches

**Plans**: 1 plan (in progress — Tasks 1-2 complete, Task 3 blocking rig checkpoint pending)

- [ ] 24-01-PLAN.md — Add the `RemoveUnusedDesignerAndVbAssemblies` publish-exclusion target to `RigToggle.App.csproj`, measure fresh before/after byte counts, audit that no excluded lever or dependency change slipped in, and rig-verify the smaller exe (Tasks 1-2 done, commit `67f4bfd`; Task 3 rig verification pending operator)

### Phase 25: Single-Instance Guard

**Goal**: Users can never end up with two Rig Toggle processes running side by side; a duplicate launch attempt surfaces the existing instance instead, and the guard exposes a deliberate bypass for the app's own future internal relaunches.
**Depends on**: Nothing (independent of Phase 24; must precede Phase 26, which reuses its bypass pattern)
**Requirements**: INSTANCE-01, INSTANCE-02, UPDATE-07
**Success Criteria** (what must be TRUE):

  1. Attempting to launch a second copy of the app while one is already running does not start a second process — exactly one Rig Toggle process exists at any time, confirmed via a scripted rapid-relaunch test
  2. When a duplicate launch is attempted, the already-running instance is brought to focus and made visible (restored from minimized/tray-hidden) rather than the duplicate launch silently exiting or erroring
  3. The startup sequence has an explicit, deliberately-built bypass path that an internal self-relaunch (e.g. an update-apply relaunch in Phase 26) can use to skip the single-instance mutex check without racing it, verified by a scripted simulated relaunch rather than left to be improvised later

**Plans**: TBD

### Phase 26: Auto-Update

**Goal**: Users are notified when a newer release is available and can install it with one confirmation, without ever being left with a broken or non-launchable app.
**Depends on**: Phase 25 (consumes its single-instance bypass pattern for the update-apply relaunch, per UPDATE-07)
**Requirements**: UPDATE-01, UPDATE-02, UPDATE-03, UPDATE-04, UPDATE-05, UPDATE-06
**Success Criteria** (what must be TRUE):

  1. The running build carries a version identifier reflecting the actual released git tag; on launch, the app checks GitHub Releases and detects when a newer version is available
  2. When a newer version is found, the user sees a confirmation prompt naming the new version before any download or apply happens — nothing installs silently or automatically
  3. Confirming the prompt downloads the new release, applies it in place, and the app relaunches running the new version, with autostart still pointed at the correct exe path
  4. If the update is interrupted partway (killed download, simulated disk-full/locked-file), the original exe is left intact and still launchable — the app is never stranded in a non-launchable state
  5. A manual "Check for Updates" entry in the app's menu triggers the same check on demand and reports when already up to date, independent of the automatic on-launch check

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
| 15. Optional App & Audio Targets | v2.0 | 4/4 | Complete | 2026-08-04 |
| 16. Normal-Mode Explicit Monitor Config & Mode-Store Redesign | v2.0 | 5/5 | Complete | 2026-08-08 |
| 17. Manual Monitor Panel & Shared Safety Guard | v2.0 | 4/4 | Complete | 2026-08-08 |
| 18. Cleanup Pass & Exe-Size Reduction | v2.0 | 6/6 | Complete | 2026-08-09 |
| 19. Monitor-Tile Dashboard & MonitorPanelForm Retirement | v2.1 | 5/5 | Complete | 2026-08-10 |
| 20. Custom Toggle-Switch Control | v2.1 | 3/3 | Complete | 2026-08-10 |
| 21. Accent-Color Reading & Live Update | v2.1 | 3/3 | Complete | 2026-08-11 |
| 22. SettingsForm Layout Pass | v2.1 | 5/5 | Complete | 2026-08-16 |
| 23. Manual Light/Dark Override | v2.1 | 3/3 | Complete | 2026-08-17 |
| 24. Self-Contained Exe Size Reduction | v2.2 | 0/1 | In Progress (blocking checkpoint pending) | - |
| 25. Single-Instance Guard | v2.2 | 0/TBD | Not started | - |
| 26. Auto-Update | v2.2 | 0/TBD | Not started | - |

## Backlog

Requirements not yet scoped into a milestone. LOG-01 (toggle history/log) was deferred at v1.0, v1.1, v1.2, and v2.0 scoping, then explicitly dropped by the user at v2.1 scoping — no longer tracked as a backlog candidate. CLI trigger/TRIG-02/TRIG-03 was reviewed at v1.1 close and decided permanently out of scope. THEME-07/08/09 (accent-color highlight, custom toggle-switch control, manual theme override), deferred since v1.2, shipped in v2.1 (Phases 20, 21, and 23). UPDATE-08 (richer WM_COPYDATA/named-pipe IPC between instances) and DIST-01 (Velopack/installer packaging reconsideration) are tracked in `.planning/REQUIREMENTS.md`'s Future Requirements section, not currently scoped into any milestone.

---
*Next: `/gsd-plan-phase 24`*
