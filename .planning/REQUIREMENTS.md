# Requirements: Rig Toggle

**Defined:** 2026-08-04
**Core Value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

**Note:** v2.0 revises the Core Value's "restores exactly how it was before" framing — Normal mode moves from snapshot-restore to an explicitly configured target state (monitors and audio both), matching how Rig mode already works. See DISPLAY-10/AUDIO-04.

## v2.0 Requirements

Requirements for the v2.0 milestone (Configurable Monitors, Optional Targets & Cleanup). Each maps to roadmap phases.

### App

- [ ] **APP-04**: User can leave the companion app launch target unset; toggling skips launch/focus (Rig direction) and minimize (Normal direction) entirely, with no error
- [ ] **APP-05**: A configured-but-missing app path (file no longer exists at toggle time) still surfaces as a real failure, not silently treated as unset

### Audio

- [ ] **AUDIO-03**: User can leave the Rig-mode audio device unset; toggling to Rig mode skips Rig-direction audio switching entirely
- [ ] **AUDIO-04**: User can configure a Normal-mode audio device that actually applies on toggle to Normal mode (replacing today's snapshot-based restore); leaving it unset skips Normal-direction audio switching
- [ ] **AUDIO-05**: A configured-but-invalid audio device (no longer exists on the system) still surfaces as a real failure, not silently skipped

### Display

- [ ] **DISPLAY-09**: User can configure which monitors are enabled/disabled specifically for Normal mode, symmetric to Rig mode's existing monitor set configuration
- [ ] **DISPLAY-10**: Toggling to Normal mode applies the explicitly configured Normal-mode monitor set instead of restoring a pre-toggle snapshot
- [ ] **DISPLAY-11**: App tracks current mode (Rig/Normal) via an explicit persisted flag, independent of whether a snapshot file exists on disk
- [ ] **DISPLAY-12**: The "at least one monitor must remain enabled" safety guard is enforced identically across Rig toggle, Normal toggle, and the new manual panel, from one shared codepath
- [ ] **DISPLAY-13**: A lightweight "toggle in progress" marker persists across the toggle operation, so a crash mid-toggle can be detected and communicated to the user on next launch

### Panel

- [ ] **PANEL-01**: New GUI panel shows one row/tile per detected monitor, with live status shown via icon (not just text)
- [ ] **PANEL-02**: User can enable/disable any monitor directly from this panel, independent of the Rig/Normal toggle action, with immediate effect
- [ ] **PANEL-03**: Panel reflects live monitor state, updating on connect/disconnect while the panel is open
- [ ] **PANEL-04**: Disabling a monitor from the panel is gated by the existing `SkipMonitorConfirmation` setting, same as the Rig/Normal toggle
- [ ] **PANEL-05**: Panel includes an Identify action that briefly overlays a number on each physical screen

### Perf

- [ ] **PERF-01**: Self-contained exe size is reduced via MSBuild-level configuration (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, NAudio meta-package split) — without enabling IL trimming
- [ ] **PERF-02**: Exe-size changes are verified on real rig hardware (cold autostart boot timing plus a full toggle round trip), not just a build-output file-size diff

### Cleanup

- [ ] **CLEANUP-01**: Dead code from the retired snapshot-restore mechanism (`Restore()`/`RestoreViaReconstruction()` and related snapshot models) is removed, after being reviewed for any rig-specific knowledge worth preserving elsewhere first
- [ ] **CLEANUP-02**: General code-quality pass across the codebase — reduced duplication/cruft accumulated across three prior milestones, no user-facing behavior change

## v2 Requirements (Deferred)

Carried forward from prior milestone backlog, still not in scope for v2.0.

### Backlog

- **LOG-01**: Toggle history/log — deferred at v1.0, v1.1, and v1.2 scoping
- **THEME-07**: Accent-color-aware highlight — deferred at v1.2 scoping
- **THEME-08**: Custom-drawn toggle-switch control — deferred at v1.2 scoping
- **THEME-09**: Manual theme override — deferred at v1.2 scoping

## Out of Scope

Explicitly excluded from v2.0. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Independent per-direction app-launch opt-in (e.g. "launch on Rig only, never minimize on Normal") | Unrequested scope creep — keep a single optional `CompanionAppPath`, symmetric across both directions |
| Snapshot-restore kept as a silent fallback for monitors/audio not covered by explicit config | Reintroduces the exact "which state wins" ambiguity this milestone is meant to eliminate |
| "Smart" migration synthesizing Normal-mode config from the retired snapshot at upgrade time | Leave new fields null; require one explicit Settings visit post-upgrade, matching this project's existing migration discipline (null-only guard, no auto-population) |
| Drag-and-drop monitor arrangement / resolution / orientation editing in the manual panel | Windows' own Display settings already owns that; this panel stays narrowly on/off, matching the app's existing disable/enable-only monitor model |
| Persisting manual-panel toggle actions into Rig-mode/Normal-mode config | The manual panel is explicitly independent, ad-hoc, on-demand — not a way to redefine what a mode means |
| IL trimming (`PublishTrimmed=true`) as an exe-size lever | Rejected project-wide — trimming's reachability analysis misidentifies this app's COM-interop/P-Invoke code as dead and strips it |
| CLI trigger + single-instance IPC (TRIG-02/TRIG-03) | Decided permanently out of scope at v1.1 close — tray and hotkey triggers already cover every needed trigger path |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| APP-04 | Phase 15 | Pending |
| APP-05 | Phase 15 | Pending |
| AUDIO-03 | Phase 15 | Pending |
| AUDIO-04 | Phase 15 | Pending |
| AUDIO-05 | Phase 15 | Pending |
| DISPLAY-09 | Phase 16 | Pending |
| DISPLAY-10 | Phase 16 | Pending |
| DISPLAY-11 | Phase 16 | Pending |
| DISPLAY-12 | Phase 17 | Pending |
| DISPLAY-13 | Phase 16 | Pending |
| PANEL-01 | Phase 17 | Pending |
| PANEL-02 | Phase 17 | Pending |
| PANEL-03 | Phase 17 | Pending |
| PANEL-04 | Phase 17 | Pending |
| PANEL-05 | Phase 17 | Pending |
| PERF-01 | Phase 18 | Pending |
| PERF-02 | Phase 18 | Pending |
| CLEANUP-01 | Phase 18 | Pending |
| CLEANUP-02 | Phase 18 | Pending |

**Coverage:**
- v2.0 requirements: 19 total
- Mapped to phases: 19
- Unmapped: 0 ✓

---
*Requirements defined: 2026-08-04*
*Last updated: 2026-08-04 after v2.0 roadmap creation (Phases 15-18)*
