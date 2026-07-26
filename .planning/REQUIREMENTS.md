# Requirements: Rig Toggle

**Defined:** 2026-07-26
**Core Value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## v1.1 Requirements

Requirements for the v1.1 milestone (Automation & Multi-Monitor). Each maps to roadmap phases. Continues numbering from v1.0's shipped categories (DISPLAY, CORE) where the same functional area is being generalized, and introduces new categories (TRAY, TRIG, NOTIF) for genuinely new capability areas.

### Tray & Automation

- [ ] **TRAY-01**: User can run the app tray-resident — closing the main window minimizes it to the tray instead of exiting
- [ ] **TRAY-02**: User can enable "start with Windows" via a Settings checkbox (off by default)
- [ ] **TRAY-03**: User can right-click the tray icon for a context menu (Switch to Rig/Normal Mode, Settings, Exit)
- [ ] **TRAY-04**: The tray icon visually reflects the current mode (normal vs. rig)
- [ ] **TRAY-05**: Left-clicking the tray icon restores the main window

### Trigger

- [ ] **TRIG-01**: User can toggle via a configurable global hotkey; registration failures (e.g. conflict with other rig software) are surfaced, not silently swallowed
- [ ] **TRIG-02**: User can toggle via a CLI argument (e.g. `--rig`/`--normal`) whether or not an instance of the app is already running
- [ ] **TRIG-03**: User can query the current mode via a CLI argument (e.g. `--status`) without triggering a toggle

### Notification

- [ ] **NOTIF-01**: User sees a toast/status notification confirming a toggle when it's triggered without the GUI open (hotkey/CLI/tray menu), showing what changed (mode + per-step outcome), matching the GUI's existing partial-failure detail

### Display

- [ ] **DISPLAY-04**: User can configure a set of monitors to disable when entering rig mode (not limited to one)
- [ ] **DISPLAY-05**: User can configure a set of monitors to enable when entering rig mode (e.g. a rig monitor normally kept OS-disabled to save power)
- [ ] **DISPLAY-06**: Settings prevents saving a configuration that would disable every monitor
- [ ] **DISPLAY-07**: The pre-disable confirmation dialog names every monitor being disabled and enabled, not just one
- [ ] **DISPLAY-08**: A user upgrading from v1.0 keeps their previously-configured single monitor working automatically (no re-configuration required)

### Reliability

- [ ] **CORE-06**: If a toggle is triggered while another toggle is already in progress, the app safely rejects the second request rather than risking corrupted state

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Diagnostics

- **LOG-01**: App keeps a toggle history/log for diagnosing incorrect restores

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Elevated/Task-Scheduler autostart | Would silently reintroduce the UIPI cross-process-focus problem the v1.0 H9 debug session already worked around; plain non-elevated Registry `Run` key is sufficient and matches the app's existing execution model |
| Hotkey chord/sequence engine | Unused complexity for a single binding, single action — this is a two-state personal tool, not a macro platform |
| Full Windows App SDK / MSIX toast packaging | Conflicts with the standalone self-contained-.exe distribution constraint; `NotifyIcon.ShowBalloonTip` is sufficient and has zero packaging requirements |
| Per-monitor sets keyed by index/position instead of stable `DevicePath` | Already burned once in v1.0 (index-based matching is fragile) — must not be reintroduced at N-monitor scale |
| CLI trigger that force-launches a brand-new process per invocation | Defeats tray residency and risks concurrent file access on `settings.json`/`state.json`; CLI must signal the resident instance via IPC instead |
| Toggle history/log (LOG-01) | Already deferred once in v1.0; still lower priority than this milestone's automation/multi-monitor scope |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| TRAY-01 | TBD | Pending |
| TRAY-02 | TBD | Pending |
| TRAY-03 | TBD | Pending |
| TRAY-04 | TBD | Pending |
| TRAY-05 | TBD | Pending |
| TRIG-01 | TBD | Pending |
| TRIG-02 | TBD | Pending |
| TRIG-03 | TBD | Pending |
| NOTIF-01 | TBD | Pending |
| DISPLAY-04 | TBD | Pending |
| DISPLAY-05 | TBD | Pending |
| DISPLAY-06 | TBD | Pending |
| DISPLAY-07 | TBD | Pending |
| DISPLAY-08 | TBD | Pending |
| CORE-06 | TBD | Pending |

**Coverage:**
- v1.1 requirements: 15 total
- Mapped to phases: 0 (0%) — pending roadmap creation
- Unmapped: 15

---
*Requirements defined: 2026-07-26*
*Last updated: 2026-07-26 after v1.1 requirements definition*
