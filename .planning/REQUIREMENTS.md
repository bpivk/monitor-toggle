# Requirements: Rig Toggle

**Defined:** 2026-08-02
**Core Value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably restores everything to exactly how it was before.

## v1.2 Requirements

Requirements for the v1.2 milestone (Visual Polish & Documentation). Introduces new categories (THEME, ICON, DOCS) for the visual-polish and documentation work — no existing functional behavior changes.

### Theming

- [x] **THEME-01**: App detects the current Windows light/dark theme setting on startup and applies it to MainForm and SettingsForm
- [x] **THEME-02**: Theme changes made in Windows Settings while the app is running are picked up live, without requiring an app restart
- [x] **THEME-03**: The title bar of MainForm and SettingsForm reflects the active theme (dark title bar in dark mode)
- [x] **THEME-04**: All controls on MainForm and SettingsForm, including the multi-monitor settings grid, are legibly recolored to match the active theme — no stock white/gray controls left over in dark mode
- [x] **THEME-05**: Buttons and panels use flat, modern styling instead of legacy 3D bevel/gradient chrome
- [x] **THEME-06**: On Windows 11, window corners are rounded and a Mica-style backdrop is applied; on Windows 10 or earlier (or if the API is unavailable), the app falls back gracefully with no visual glitches or crashes

### Icon

- [x] **ICON-01**: The tray icon pair (rig mode / normal mode) uses genuinely distinct silhouettes, not just a color swap, so the two modes are distinguishable even to colorblind users
- [x] **ICON-02**: Tray icons are legible at the actual 16x16 tray size and remain visible against both light and dark taskbar backgrounds
- [x] **ICON-03**: Icons are embedded as multi-resolution .ico files (at minimum 16/20/24/32px) so they render sharply at every DPI-scaled size Windows requests
- [x] **ICON-04**: The same icon artwork/motif is reused at larger sizes for the .exe/taskbar icon, not just the tray glyph

### Documentation

- [x] **DOCS-01**: README.md includes a feature overview with screenshots (or clearly marked placeholders for user-supplied screenshots) of the toggle in both modes
- [x] **DOCS-02**: README.md includes instructions for downloading/installing the released .exe and for building from source
- [x] **DOCS-03**: README.md includes GitHub badges (build status, license, latest release version)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Diagnostics

- **LOG-01**: App keeps a toggle history/log for diagnosing incorrect restores

### Theming (future consideration, deferred at v1.2 scoping)

- **THEME-07**: Windows accent-color-aware highlight on the core toggle control/status indicator
- **THEME-08**: Custom-drawn toggle-switch control replacing the core action button
- **THEME-09**: Manual theme override (force light/dark regardless of system setting)

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Full custom-owner-drawn control library (rounded buttons, ripple/hover animations, restyled scrollbars) | WinForms has no supported theming hook for this; open-ended effort with high risk of an inconsistent, half-finished patchwork for a 2-window personal utility |
| Frameless custom-chrome window with hand-built minimize/close buttons | Reintroduces Aero Snap, drag-resize, and multi-monitor/DPI edge cases the native title bar + DWM dark-mode call sidesteps for free |
| Color-only tray icon mode differentiation (identical glyph, red vs. green tint only) | Fails Microsoft's own icon guidance (color alone is explicitly insufficient) and fails colorblind users; superseded by ICON-01's silhouette requirement |
| Theme-swapping tray icon variants (2 mode × 2 taskbar-theme = 4 icon assets plus swap-detection) | Microsoft's own guidance frames theme-sensitive icon assets as optional; a single self-contained-contrast design (ICON-02) solves the same problem more cheaply |
| Migrating to WPF/WinUI3 for theming "for free" | A framework migration is a rewrite of a ~6,900 LOC, 4-project solution to solve a styling problem — wildly disproportionate for a visual-polish milestone |
| CLI trigger + single-instance IPC (TRIG-02/TRIG-03) | Reviewed and permanently dropped at v1.1 close — tray + hotkey triggers already cover every trigger path needed |
| Toggle history/log (LOG-01) | Deferred twice now (v1.0, v1.1); still lower priority than visual polish |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| THEME-01 | Phase 12 | Complete |
| THEME-02 | Phase 12 | Complete |
| THEME-03 | Phase 12 | Complete |
| THEME-04 | Phase 12 | Complete |
| THEME-05 | Phase 12 | Complete |
| THEME-06 | Phase 12 | Complete |
| ICON-01 | Phase 13 | Complete |
| ICON-02 | Phase 13 | Complete |
| ICON-03 | Phase 13 | Complete |
| ICON-04 | Phase 13 | Complete |
| DOCS-01 | Phase 14 | Complete |
| DOCS-02 | Phase 14 | Complete |
| DOCS-03 | Phase 14 | Complete |

**Coverage:**
- v1.2 requirements: 13 total
- Mapped to phases: 13 (100%)
- Unmapped: 0

---
*Requirements defined: 2026-08-02*
