# Roadmap: Rig Toggle

## Overview

Rig Toggle composes three individually-tricky Windows automation techniques — true CCD-level monitor disable, default audio device switching, and cross-process window management — into a single snapshot-and-restore toggle. Because the monitor-disable mechanism has no officially documented public API, the roadmap validates that core assumption first as a throwaway spike, then builds the app in horizontal technical layers: settings/persistence and a GUI shell against fake controllers, followed by real adapters in ascending risk order (app + audio, then monitor), and finally wires everything into the full orchestrated toggle flow and ships it as a standalone .exe.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Monitor-Disable Feasibility Spike** - Prove true OS-level monitor disable works on the actual rig hardware before committing further
- [ ] **Phase 2: Foundations & GUI Shell** - Settings, snapshot persistence, and full GUI wired against fake controllers
- [ ] **Phase 3: App & Audio Control** - Real companion-app launch/focus and default audio device switching
- [ ] **Phase 4: Monitor Control (Production)** - Real primary-monitor disable/restore using the spike-validated mechanism
- [ ] **Phase 5: Orchestration, Full Toggle & Packaging** - Wire all real adapters into the complete toggle flow and ship as a standalone .exe

## Phase Details

### Phase 1: Monitor-Disable Feasibility Spike
**Goal**: Determine whether true OS-level monitor disable (not a power-off) is achievable on the actual rig hardware and GPU driver, before any other architecture or GUI work is treated as settled.
**Depends on**: Nothing (first phase)
**Requirements**: None — this is a throwaway validation spike whose outcome determines the implementation approach for Phase 4's DISPLAY-01/02/03
**Success Criteria** (what must be TRUE):
  1. A throwaway prototype confirms the primary monitor is fully removed from Windows' display enumeration (not merely blanked/powered off) when triggered
  2. A secondary-monitor-launch scenario (e.g. the BeamNG-style self-minimize misbehavior) is resolved because the monitor is genuinely absent from Windows, not just visually secondary
  3. A documented go/no-go decision states which mechanism (SetDisplayConfig topology-path-removal, or a documented fallback) will be used in Phase 4
  4. Elevation requirements for the chosen mechanism are confirmed empirically on this machine
**Plans**: TBD

### Phase 2: Foundations & GUI Shell
**Goal**: User can open the app, configure every toggle setting, and have those settings persist — built against fake controllers so UX can be fully validated with zero hardware risk.
**Depends on**: Phase 1 (sequenced after per risk-first ordering; no technical blocking dependency)
**Requirements**: SETTINGS-01, SETTINGS-02, SETTINGS-03, SETTINGS-04
**Success Criteria** (what must be TRUE):
  1. User can select which monitor is the "primary to disable" from a list of detected displays
  2. User can select which audio devices form the toggle pair (normal device, rig device) from a list of detected audio endpoints
  3. User can specify the file path of the companion app to launch/focus/minimize
  4. Settings persist across app restarts
**Plans**: TBD
**UI hint**: yes

### Phase 3: App & Audio Control
**Goal**: Toggling reliably launches/focuses the companion app and switches the default audio output device, using real Windows APIs in place of Phase 2's fakes.
**Depends on**: Phase 2
**Requirements**: APP-01, APP-02, APP-03, AUDIO-01, AUDIO-02
**Success Criteria** (what must be TRUE):
  1. Toggling to rig mode launches the configured companion app if it isn't already running
  2. If the companion app is already running, toggling to rig mode brings its window to focus instead of launching a duplicate instance
  3. Toggling back to normal mode minimizes the companion app's window (best-effort)
  4. Toggling to rig mode switches the default audio output device to the configured rig speakers
  5. Toggling back to normal mode restores the exact previous default audio device across all relevant audio roles
**Plans**: TBD

### Phase 4: Monitor Control (Production)
**Goal**: Toggling reliably disables and re-enables the primary monitor at the true OS level, using the mechanism validated by Phase 1's spike, with an explicit safety confirmation before disabling.
**Depends on**: Phase 1
**Requirements**: DISPLAY-01, DISPLAY-02, DISPLAY-03
**Success Criteria** (what must be TRUE):
  1. Toggling to rig mode disables the configured primary monitor at the OS level (true CCD-style disconnect, confirmed removed from display enumeration, not just powered off)
  2. Toggling back to normal mode re-enables the primary monitor restored to its exact prior configuration (position, primary designation, orientation)
  3. Before disabling the primary monitor, the app shows a confirmation dialog naming the monitor about to be disabled
**Plans**: TBD

### Phase 5: Orchestration, Full Toggle & Packaging
**Goal**: The complete toggle — monitor, audio, and companion app together — works reliably in both directions from a single GUI action, survives a crash while in rig mode, reports partial failures honestly, and ships as a standalone .exe.
**Depends on**: Phase 2, Phase 3, Phase 4
**Requirements**: CORE-01, CORE-02, CORE-03, CORE-04, CORE-05, PACKAGING-01
**Success Criteria** (what must be TRUE):
  1. User can trigger the toggle to rig mode with one action from the GUI, and monitor, audio, and app all switch together
  2. User can trigger the toggle back to normal mode with one action from the GUI, and monitor, audio, and app all restore together
  3. The app captures a full snapshot of the current monitor and audio configuration before mutating anything, so toggle-back restores exactly what was active before
  4. If any step of a toggle fails partway, the app reports which steps succeeded/failed and stops rather than silently continuing or auto-reverting
  5. Current mode (normal vs. rig) is correctly detected on app startup even after a crash or forced close while in rig mode
  6. The app is distributed as a standalone Windows .exe requiring no separate runtime install
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Monitor-Disable Feasibility Spike | 0/TBD | Not started | - |
| 2. Foundations & GUI Shell | 0/TBD | Not started | - |
| 3. App & Audio Control | 0/TBD | Not started | - |
| 4. Monitor Control (Production) | 0/TBD | Not started | - |
| 5. Orchestration, Full Toggle & Packaging | 0/TBD | Not started | - |
