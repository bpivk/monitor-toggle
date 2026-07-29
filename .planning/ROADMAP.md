# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- 🚧 **v1.1 Automation & Multi-Monitor** — Phases 6-10 (planning)

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

### 🚧 v1.1 Automation & Multi-Monitor (Planning)

**Milestone Goal:** Remove the remaining daily-use friction (must open the GUI to toggle; only one monitor can be disabled) by adding background/automated triggering and generalizing monitor control to arbitrary multi-monitor rigs.

- [x] **Phase 6: Multi-Monitor Data Model & Controller Generalization** - Independently-configurable disable/enable monitor sets, with a rig-validated CCD checkpoint and a v1.0-settings migration (completed 2026-07-29)
- [x] **Phase 7: Shared Toggle-Orchestration Helper Extraction** - One reentrancy-safe pipeline every trigger (button/tray/hotkey/CLI) runs through (completed 2026-07-29)
- [ ] **Phase 8: Tray Residency, Autostart & Toast Notification** - Minimize-to-tray, autostart, tray context menu, and toast confirmation for headless triggers
- [ ] **Phase 9: Global Hotkey Trigger** - Configurable Windows-wide keyboard shortcut with surfaced registration failures
- [ ] **Phase 10: CLI Trigger + Single-Instance IPC** - Command-line toggle/status args that signal a resident instance instead of spawning duplicates

## Phase Details

### Phase 6: Multi-Monitor Data Model & Controller Generalization

**Goal**: Users can configure independent sets of monitors to disable and enable when entering rig mode (not limited to one monitor each), and a user upgrading from v1.0 keeps their existing single-monitor configuration working automatically.
**Depends on**: Phase 5 (v1.0) — first phase of the v1.1 milestone, builds on the shipped, rig-validated monitor controller
**Requirements**: DISPLAY-04, DISPLAY-05, DISPLAY-06, DISPLAY-07, DISPLAY-08
**Success Criteria** (what must be TRUE):

  1. User can select multiple monitors to disable when entering rig mode, not limited to one (DISPLAY-04)
  2. User can select multiple monitors to enable when entering rig mode, e.g. a rig monitor normally kept OS-disabled to save power (DISPLAY-05)
  3. Settings refuses to save a configuration that would disable every monitor, with a clear explanation (DISPLAY-06)
  4. The pre-disable confirmation modal names every monitor being disabled and every monitor being enabled, not just one (DISPLAY-07)
  5. A user upgrading from a genuine v1.0-era settings.json sees their previously-configured single monitor already selected as the disable-set on first launch, with no re-configuration required (DISPLAY-08)

**Plans**: 6 plans (waves: 1 → 2 → 3 → 4)
Plans:
**Wave 1**

- [x] 06-01-PLAN.md — Core data model (plural monitor sets, MonitorInfo.IsActive) + silent v1.0→v1.1 migration (DISPLAY-08)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 06-02-PLAN.md — Generalized IMonitorController triad + ToggleService orchestration (ordering, D-07, D-02) + Core tests

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 06-03-PLAN.md — Windows CCD adapter: GetAllMonitors/ActivateMonitors/DeactivateMonitors + N-generalized Restore/overlap verify
- [x] 06-04-PLAN.md — SettingsForm multi-select grid (D-03/D-04) + DISPLAY-06/D-07 validation + merged-set save
- [x] 06-05-PLAN.md — Multi-monitor confirmation dialog (D-06) + MainForm name resolution via GetAllMonitors (DISPLAY-07)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 06-06-PLAN.md — Mandatory rig-validation checkpoint (go/no-go gate: reboot re-enable + combined topology)

**UI hint**: yes
**Notes** (completion gate, not optional groundwork):

- This phase is not "done" when code compiles and unit tests pass. It requires a dedicated rig-validation checkpoint before being considered complete, mirroring v1.0 Phase 1's spike-first discipline: (a) disable a monitor, then sleep/wake or reboot the rig PC, then attempt to re-enable it, confirming it is still enumerable and comes back at a sane resolution; (b) apply a combined disable+enable topology using the real configured sets in one operation, confirming exactly one GDI primary results with no position overlap. Both are go/no-go gates on this phase, not follow-up hardening. (Planned as 06-06-PLAN.md.)
- The one-time settings migration (legacy singular `AppSettings.MonitorDevicePath` -> new plural `MonitorsToDisable`/`MonitorsToEnable` fields) is an explicit task in this phase's scope, with an acceptance test that loads a genuine v1.0-era `settings.json` file and confirms the migrated result — not an afterthought bolted onto the model change. (Planned as 06-01-PLAN.md Task 2.)
- Open design question resolved during discussion (D-07): `IsFullyConfigured` no longer requires a non-empty disable-set — it requires disable-set OR enable-set non-empty (implemented in 06-02-PLAN.md).

### Phase 7: Shared Toggle-Orchestration Helper Extraction

**Goal**: Every toggle trigger (button, tray menu, hotkey, CLI) runs through one shared, reentrancy-safe pipeline, so a toggle already in progress can never be corrupted by a second concurrent request.
**Depends on**: Phase 6 (recommended sequencing — refactors against the final multi-monitor confirmation-dialog shape; could technically proceed in parallel with Phase 6 since it touches App-layer code while Phase 6 touches Core/Windows, but this roadmap sequences it second for a clean, stable baseline)
**Requirements**: CORE-06
**Success Criteria** (what must be TRUE):

  1. If a toggle is triggered while another toggle is already in progress, the app safely rejects the second request (e.g. a clear "toggle already in progress" response) rather than risking corrupted state (CORE-06)
  2. Rapidly double-clicking (or otherwise double-firing) the toggle trigger results in exactly one toggle executing, never two overlapping ones (CORE-06)
  3. The existing GUI toggle button's behavior (confirmation dialog, per-step outcome report) is unchanged after the refactor, confirming the extraction didn't regress the one trigger path that existed before this milestone (CORE-06)

**Plans**: 1 plan (wave: 1)
Plans:
- [x] 07-01-PLAN.md — ToggleOrchestrator (Interlocked non-blocking busy-guard) + ToggleInProgressException + deterministic reentrancy tests, then MainForm/Program.cs routed through the orchestrator (CORE-06)

**Notes**:

- This phase must decide and implement the reentrancy guard design (lock vs. busy-flag vs. queue) as part of its own scope. This is the single most consequential design decision surfaced by research and is a Phase 7 deliverable, not something deferred to whichever later phase happens to notice the gap. Resolved during discussion (D-01/D-02): a non-blocking busy-flag (`Interlocked.CompareExchange`), one shared flag guarding both directions, rejecting the second request immediately (not a queue).

### Phase 8: Tray Residency, Autostart & Toast Notification

**Goal**: Users can run the app tray-resident, have it start automatically with Windows if desired, control it entirely from the tray icon, and get a toast notification confirming what changed whenever a toggle happens without the GUI open.
**Depends on**: Phase 7 (tray-menu and headless toggles need the shared, reentrancy-safe orchestration entry point)
**Requirements**: TRAY-01, TRAY-02, TRAY-03, TRAY-04, TRAY-05, NOTIF-01
**Success Criteria** (what must be TRUE):

  1. Closing the main window (X) minimizes it to the tray instead of exiting, and left-clicking the tray icon restores it (TRAY-01, TRAY-05)
  2. User can enable "start with Windows" via a Settings checkbox, off by default (TRAY-02)
  3. Right-clicking the tray icon shows a context menu (Switch to Rig/Normal Mode, Settings, Exit) (TRAY-03)
  4. The tray icon's appearance changes to reflect the current mode, normal vs. rig (TRAY-04)
  5. Triggering a toggle without the GUI open (tray menu) shows a toast/balloon notification confirming what changed — mode plus per-step outcome — matching the GUI's existing partial-failure detail (NOTIF-01)

**Plans**: TBD
**UI hint**: yes

### Phase 9: Global Hotkey Trigger

**Goal**: Users can toggle the mode from anywhere in Windows via a configurable keyboard shortcut, with registration failures surfaced instead of silently swallowed.
**Depends on**: Phase 8 (the hotkey needs to remain useful once the window is hidden in the tray, and reuses its keep-alive-on-close behavior)
**Requirements**: TRIG-01
**Success Criteria** (what must be TRUE):

  1. User can configure a global hotkey in the Settings form that toggles the mode from anywhere in Windows, including while the main window is hidden in the tray (TRIG-01)
  2. If the configured hotkey fails to register — e.g. it conflicts with Moza Companion or other rig software already using that combination — the failure is surfaced to the user in Settings, not silently swallowed (TRIG-01)
  3. Pressing the hotkey while the Settings dialog is open has defined, non-corrupting behavior (explicitly suppressed or queued, not left to race the in-progress edit) (TRIG-01)

**Plans**: TBD
**UI hint**: yes
**Notes**:

- `RegisterHotKey`'s return value must always be checked, with conflicts surfaced in Settings — this API can fail silently if another app already owns the combination. Include a rig-test note to verify hotkey registration/conflict behavior with Moza Companion actually running (not just an isolated dev machine), since that is the realistic conflict scenario this requirement exists for.

### Phase 10: CLI Trigger + Single-Instance IPC

**Goal**: Users can toggle or query the current mode from a command-line argument — usable from macro pads, Stream Deck, or other external tools — whether or not the app is already running, without spawning duplicate processes.
**Depends on**: Phase 8 (CLI signaling a resident instance needs the tray-resident single-instance model to have any point), Phase 7 (routes into the shared reentrancy-safe orchestration helper)
**Requirements**: TRIG-02, TRIG-03
**Success Criteria** (what must be TRUE):

  1. Running the exe with a toggle argument (e.g. `--rig`/`--normal`) toggles the mode whether or not an instance of the app is already running (TRIG-02)
  2. Running the exe with `--status` reports the current mode without triggering a toggle (TRIG-03)
  3. When no resident instance is running, a CLI toggle invocation still results in the toggle executing correctly and the process exiting cleanly afterward, not left running as an orphaned background process (TRIG-02)
  4. A CLI trigger sent while the resident instance is already mid-toggle is handled safely per Phase 7's reentrancy guard — rejected with a clear response — rather than hanging indefinitely (TRIG-02)

**Plans**: TBD
**Notes**:

- Must decide and implement concrete behavior for all four combinations of (resident instance running or not) x (autostart on/off) — not just the happy path.
- Size the IPC response/timeout generously against the toggle's real multi-second duration (monitor CCD apply + audio switch + app launch can take several seconds) rather than assuming a fast round-trip; consider acknowledging receipt immediately rather than blocking the CLI client until the full toggle completes.

## Progress

**Execution Order:**
Phases execute in numeric order: 6 → 7 → 8 → 9 → 10

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|-----------------|--------|-----------|
| 1. Monitor-Disable Feasibility Spike | v1.0 | 2/2 | Complete | 2026-07-24 |
| 2. Foundations & GUI Shell | v1.0 | 5/5 | Complete | 2026-07-24 |
| 3. App & Audio Control | v1.0 | 4/4 | Complete | 2026-07-24 |
| 4. Monitor Control (Production) | v1.0 | 4/4 | Complete | 2026-07-24 |
| 5. Orchestration, Full Toggle & Packaging | v1.0 | 3/3 | Complete | 2026-07-25 |
| 6. Multi-Monitor Data Model & Controller Generalization | v1.1 | 6/6 | Complete    | 2026-07-29 |
| 7. Shared Toggle-Orchestration Helper Extraction | v1.1 | 1/1 | Complete   | 2026-07-29 |
| 8. Tray Residency, Autostart & Toast Notification | v1.1 | 0/TBD | Not started | - |
| 9. Global Hotkey Trigger | v1.1 | 0/TBD | Not started | - |
| 10. CLI Trigger + Single-Instance IPC | v1.1 | 0/TBD | Not started | - |
