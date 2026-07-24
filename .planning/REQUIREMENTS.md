# Requirements: Rig Toggle

**Defined:** 2026-07-24
**Core Value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Display

- [ ] **DISPLAY-01**: User can disable the configured primary monitor at the OS level (true CCD-style disconnect, not a DDC power-off) when toggling to rig mode
- [ ] **DISPLAY-02**: User can re-enable the primary monitor, restored to its exact prior configuration (position, primary designation, orientation), when toggling back to normal mode
- [ ] **DISPLAY-03**: Before disabling the primary monitor, the app shows a confirmation dialog naming the monitor about to be disabled

### Audio

- [x] **AUDIO-01**: User can switch the default audio output device to the configured rig speakers when toggling to rig mode
- [x] **AUDIO-02**: User can restore the exact previous default audio device (across all relevant audio roles) when toggling back to normal mode

### Companion App

- [x] **APP-01**: Toggling to rig mode launches the configured companion app if it isn't already running
- [x] **APP-02**: If the companion app is already running when toggling to rig mode, its window is brought to focus instead of launching a duplicate instance
- [x] **APP-03**: Toggling back to normal mode minimizes the companion app's window (best-effort — true close-without-kill only if the app itself supports it)

### Settings

- [ ] **SETTINGS-01**: User can select which monitor is the "primary to disable" from a list of detected displays
- [ ] **SETTINGS-02**: User can select which audio devices form the toggle pair (normal device, rig device) from a list of detected audio endpoints
- [ ] **SETTINGS-03**: User can specify the file path of the companion app to launch/focus/minimize
- [ ] **SETTINGS-04**: Settings persist across app restarts

### Core Toggle

- [ ] **CORE-01**: User can trigger the toggle to rig mode with one action from the GUI
- [ ] **CORE-02**: User can trigger the toggle back to normal mode with one action from the GUI
- [ ] **CORE-03**: The app captures a full snapshot of the current monitor and audio configuration before mutating anything, so toggle-back restores exactly what was active before
- [ ] **CORE-04**: If any step of a toggle fails partway, the app reports which steps succeeded/failed and stops rather than silently continuing or auto-reverting
- [ ] **CORE-05**: Current mode (normal vs. rig) is correctly detected on app startup even after a crash or forced close while in rig mode

### Packaging

- [ ] **PACKAGING-01**: The app is distributed as a standalone Windows .exe requiring no separate runtime install

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Trigger

- **TRIG-01**: User can toggle via a global hotkey in addition to the GUI

### Tray

- **TRAY-01**: App can run tray-resident and auto-start on Windows boot

### Feedback

- **NOTIF-01**: App shows a toast/status notification confirming a successful toggle
- **LOG-01**: App keeps a toggle history/log for diagnosing incorrect restores

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Auto-trigger toggle on specific game/app launch | Reintroduces the exact display-detection-timing problem this tool exists to solve manually; adds process-watching and per-game config surface area |
| Per-app/per-game auto-switch rules engine | Disproportionate surface area for a single-user, two-state personal tool |
| N-way / arbitrary-count profile manager | This tool has exactly two states (normal, rig) by design, not a general profile system |
| Plugin/scripting system | No current need beyond the author's own two-state toggle; adds extension-API and sandboxing burden for zero users |
| Multi-user / role-based configuration | Single-user personal tool — there is no second user |
| Auto-update mechanism | Personal tool with one user (the author); rebuild-and-replace is sufficient |
| Telemetry/analytics/crash reporting | No product-market questions to answer; pure overhead for a personal tool |
| Cloud sync / multi-device profile sync | Manages exactly one machine; redo the one-time settings screen if the rig PC ever changes |
| Licensing/activation/DRM | Not a commercial product — nothing to protect, no one to gate |
| Guaranteed true "close window, keep process alive" for companion app | Not reliably forceable externally unless the target app supports it itself; best-effort minimize is the v1 behavior |
| Global hotkey trigger | Deferred to v1.x — validate the GUI-click flow first |
| System tray residency / auto-start | Deferred to v1.x — manual launch is sufficient to validate the core mechanic |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| DISPLAY-01 | Phase 4 | Pending |
| DISPLAY-02 | Phase 4 | Pending |
| DISPLAY-03 | Phase 4 | Pending |
| AUDIO-01 | Phase 3 | Complete |
| AUDIO-02 | Phase 3 | Complete |
| APP-01 | Phase 3 | Complete |
| APP-02 | Phase 3 | Complete |
| APP-03 | Phase 3 | Complete |
| SETTINGS-01 | Phase 2 | Pending |
| SETTINGS-02 | Phase 2 | Pending |
| SETTINGS-03 | Phase 2 | Pending |
| SETTINGS-04 | Phase 2 | Pending |
| CORE-01 | Phase 5 | Pending |
| CORE-02 | Phase 5 | Pending |
| CORE-03 | Phase 5 | Pending |
| CORE-04 | Phase 5 | Pending |
| CORE-05 | Phase 5 | Pending |
| PACKAGING-01 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 18 total
- Mapped to phases: 18 (100%)
- Unmapped: 0 ✓

---
*Requirements defined: 2026-07-24*
*Last updated: 2026-07-24 after roadmap creation*
