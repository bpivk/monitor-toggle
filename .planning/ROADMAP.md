# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- ✅ **v1.1 Automation & Multi-Monitor** — Phases 6-9, 11 (shipped 2026-08-01) — Phase 10 (CLI trigger) removed, out of scope
- ✅ **v1.2 Visual Polish & Documentation** — Phases 12-14 (shipped 2026-08-04)
- 🚧 **v2.0 Configurable Monitors, Optional Targets & Cleanup** — Phases 15-18 (planned)

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

### 🚧 v2.0 Configurable Monitors, Optional Targets & Cleanup (Planned)

**Milestone Goal:** Replace snapshot-restore with explicit per-mode monitor configuration, make app/audio targets optional, add a live manual monitor toggle panel, and reduce exe size + clean up code.

- [x] **Phase 15: Optional App & Audio Targets** - Companion app and per-role audio devices become optional; toggle skips cleanly when unset but a configured-but-broken target still surfaces as a real failure (completed 2026-08-04)
- [x] **Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign** - Normal mode applies an explicitly configured monitor set instead of snapshot-restore, with mode tracked via a persisted flag and crash-mid-toggle detection (completed 2026-08-08)
- [x] **Phase 17: Manual Monitor Panel & Shared Safety Guard** - A live panel lets the user enable/disable any monitor on demand, with the "at least one monitor enabled" guard enforced identically everywhere monitors can be mutated (completed 2026-08-08)
- [ ] **Phase 18: Cleanup Pass & Exe-Size Reduction** - Dead snapshot-restore code removed, general code-quality pass, and exe size reduced via MSBuild config, rig-verified

## Phase Details

### Phase 15: Optional App & Audio Targets

**Goal**: The companion-app launch target and the Rig/Normal audio devices can each be left unset, causing the corresponding toggle step to be skipped cleanly with no error — while a target that's configured but genuinely broken (missing file, removed device) still surfaces as a real failure, never silently downgraded to "skipped."
**Depends on**: Nothing (first phase of v2.0)
**Requirements**: APP-04, APP-05, AUDIO-03, AUDIO-04, AUDIO-05
**Success Criteria** (what must be TRUE):

  1. Leaving the companion app path unset in Settings makes toggle-to-Rig skip launch/focus and toggle-to-Normal skip minimize entirely, with no error (APP-04)
  2. A configured app path pointing to a file that no longer exists at toggle time surfaces as a real `Failed` step, not treated as unset (APP-05)
  3. Leaving the Rig-mode audio device unset makes toggle-to-Rig skip Rig-direction audio switching entirely, with no error (AUDIO-03)
  4. Configuring a Normal-mode audio device makes it actually apply on toggle-to-Normal (replacing today's snapshot-based restore); leaving it unset skips Normal-direction audio switching (AUDIO-04)
  5. A configured audio device ID that no longer exists on the system surfaces as a real `Failed` step, not silently skipped (AUDIO-05)

**Plans**: 4 plans
Plans:
**Wave 1**

- [x] 15-01-PLAN.md — Core contracts: Skipped outcome, Success predicate, checklist arm, IAudioController.TryResolveDevice + fake
- [x] 15-03-PLAN.md — Settings UI: Clear button, "(None...)" audio sentinel, relaxed Save gate, MainForm message reword

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 15-02-PLAN.md — ToggleService optional Audio/App both directions, Normal-mode SetDefault audio, relaxed gate + tests

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 15-04-PLAN.md — Full-solution regression gate + rig verification of all five success criteria

### Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign

**Goal**: Normal mode gets its own explicitly configured monitor set — symmetric with Rig mode's existing config — applied directly on toggle instead of restoring a pre-toggle snapshot, and the app's notion of "which mode am I in" becomes an explicit persisted flag instead of a proxy inferred from snapshot-file presence, with a lightweight crash-recovery marker covering a toggle interrupted mid-flight.
**Depends on**: Phase 15 (sequenced after per research risk-ordering — lowest-risk optional-target work first to build confidence before this phase's mode-tracking redesign; not a hard architectural dependency)
**Requirements**: DISPLAY-09, DISPLAY-10, DISPLAY-11, DISPLAY-13
**Success Criteria** (what must be TRUE):

  1. User can configure which monitors are enabled/disabled specifically for Normal mode in Settings, independent of and symmetric to Rig mode's existing monitor set configuration (DISPLAY-09)
  2. Toggling to Normal mode applies the explicitly configured Normal-mode monitor set directly, not a snapshot restored from before the last toggle (DISPLAY-10)
  3. The app correctly reports which mode (Rig/Normal) it's in immediately after an app restart, even when no snapshot file exists on disk (DISPLAY-11)
  4. If the app crashes or is killed mid-toggle, the next launch detects the interrupted toggle from a persisted marker and communicates it to the user (DISPLAY-13)

**Plans**: 5 plans
Plans:
**Wave 1**

- [x] 16-01-PLAN.md — Core mode/marker stores: ToggleMode, ToggleInProgressMarker, IModeStore/IToggleInProgressStore + JSON impls + test doubles
- [x] 16-02-PLAN.md — Settings UI: AppSettings Normal-mode fields + second "Normal Mode" grid, reflow, theming, stale-prose fixes (DISPLAY-09)

**Wave 2** *(blocked on Wave 1)*

- [x] 16-03-PLAN.md — ToggleService/Orchestrator/MonitorController rewrite: explicit Normal apply, IModeStore-backed mode, shared CR-01 helper, marker lifecycle, tests (DISPLAY-10/11/13)

**Wave 3** *(blocked on Wave 2)*

- [x] 16-04-PLAN.md — App wiring: Program.cs bootstrap + StartupRecoveryChecker dialogs + MainForm unknown-mode guards (DISPLAY-11/13)

**Wave 4** *(blocked on Wave 3)*

- [x] 16-05-PLAN.md — Full-solution regression gate + rig verification of all four success criteria

### Phase 17: Manual Monitor Panel & Shared Safety Guard

**Goal**: The user gets a new GUI panel for live, on-demand monitor enable/disable independent of the Rig/Normal toggle, with per-monitor status shown via icon and an Identify action — and the "at least one monitor must remain enabled" safety guard is enforced identically across the Rig toggle, the Normal toggle, and this new panel, from one shared codepath (not three separate checks).
**Depends on**: Phase 16 (reuses its unified `ActivateMonitors`/`DeactivateMonitors` controller-call shape and monitor-set model rather than introducing a second implementation)
**Requirements**: DISPLAY-12, PANEL-01, PANEL-02, PANEL-03, PANEL-04, PANEL-05
**Success Criteria** (what must be TRUE):

  1. A new panel shows one row/tile per detected monitor with its on/off status shown via icon, not just text (PANEL-01)
  2. User can enable/disable any individual monitor directly from the panel, independent of the Rig/Normal toggle action, and it takes effect immediately (PANEL-02)
  3. The panel's monitor list and status update live if a monitor is connected or disconnected while the panel is open (PANEL-03)
  4. Disabling a monitor from the panel is gated by the same `SkipMonitorConfirmation` setting as the Rig/Normal toggle (PANEL-04)
  5. The panel includes an Identify action that briefly overlays a number on each physical screen (PANEL-05)
  6. Attempting to disable the last remaining enabled monitor is rejected identically whether attempted via the Rig toggle, the Normal toggle, or the manual panel (DISPLAY-12)

**Plans**: 4 plans
Plans:
**Wave 1**

- [x] 17-01-PLAN.md — Panel prerequisites: ToggleOrchestrator.BeginExclusiveMonitorAccess() lease + tests, and the borderless MonitorIdentifyOverlay (PANEL-05)

**Wave 2** *(blocked on Wave 1)*

- [x] 17-02-PLAN.md — MonitorPanelForm (Designer + code-behind): rows/status icons, live hotplug refresh, row actions, confirmation gate, shared safety guard, Identify (PANEL-01..05, DISPLAY-12)

**Wave 3** *(blocked on Wave 2)*

- [x] 17-03-PLAN.md — Entry points: MainForm `Monitors…` button + tray `Monitors` item, non-modal cached instance, Program.cs factory wiring

**Wave 4** *(blocked on Wave 3)*

- [x] 17-04-PLAN.md — Full-solution regression gate, DISPLAY-12 single-implementation audit, and rig verification of all six success criteria

**UI hint**: yes

### Phase 18: Cleanup Pass & Exe-Size Reduction

**Goal**: The now-dead snapshot-restore subsystem is removed (after preserving any rig-specific knowledge it encoded), a general code-quality pass reduces duplication/cruft accumulated across three prior milestones with no user-facing behavior change, and the self-contained exe is measurably smaller via MSBuild-level configuration alone — verified on real rig hardware, not just a build-output size diff.
**Depends on**: Phase 16 (the snapshot/restore subsystem is only confirmed dead once Normal mode's monitor-set rewrite has shipped), Phase 17 (cleanup follows all functional changes so nothing still-in-use gets deleted)
**Requirements**: PERF-01, PERF-02, CLEANUP-01, CLEANUP-02
**Success Criteria** (what must be TRUE):

  1. `Restore()`/`RestoreViaReconstruction()` and related snapshot-restore models are removed from the codebase, with any rig-specific knowledge they encoded reviewed and preserved elsewhere first (CLEANUP-01)
  2. The codebase shows measurably less duplication/cruft than before this phase (dead fields, dead code paths, redundant helpers removed), with no user-facing behavior change (CLEANUP-02)
  3. The self-contained exe is measurably smaller after applying `EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, and the NAudio meta-package split — without enabling IL trimming (PERF-01)
  4. A full toggle round trip and a cold autostart boot are verified working correctly on real rig hardware after the exe-size changes, not just a build-output file-size diff (PERF-02)

**Plans**: 6 plans
Plans:
**Wave 1**

- [x] 18-01-PLAN.md — Delete the snapshot-persistence subsystem + audio-side Restore; rewire the Program.cs legacy-mode bootstrap to a bare File.Exists check (CLEANUP-01)
- [x] 18-02-PLAN.md — Preserve the rig-discovered CCD knowledge, then delete monitor Restore/RestoreViaReconstruction/_originalPathsCache/CopyOutputTechnology/AssignSource and their 6 tests (CLEANUP-01)
- [x] 18-03-PLAN.md — Apply the four MSBuild exe-size levers (compression, satellite languages, invariant globalization, NAudio.Wasapi split) and measure the byte delta (PERF-01)
- [x] 18-04-PLAN.md — Close the four reviewed code-quality findings: IN-04 dead branch, IN-03 sentinel name, IN-02 checklist wording + test, WR-03 branch tracing (CLEANUP-02)

**Wave 2** *(blocked on Wave 1)*

- [x] 18-05-PLAN.md — Strip Restore from the test doubles, close IN-01's dead test knob, drop the vacuous assertions, and run the tree-wide zero-reference audit (CLEANUP-01/02)

**Wave 3** *(blocked on Wave 2)*

- [ ] 18-06-PLAN.md — Merged-tree regression gate, final exe-size measurement, and rig verification of cold autostart boot + full toggle round trip (PERF-01/PERF-02)

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
| 18. Cleanup Pass & Exe-Size Reduction | v2.0 | 5/6 | In Progress|  |

## Backlog

Requirements not yet scoped into a milestone. See `.planning/REQUIREMENTS.md` "v2 Requirements (Deferred)" for the current deferred list (LOG-01 toggle history/log; THEME-07/08/09 accent-color highlight, custom toggle-switch control, manual theme override — all deferred at v1.2 scoping). CLI trigger/TRIG-02/TRIG-03 was reviewed at v1.1 close and decided permanently out of scope, not carried forward.

---
*Next: `/gsd:execute-phase 15` to execute Phase 15: Optional App & Audio Targets.*
