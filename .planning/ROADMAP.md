# Roadmap: Rig Toggle

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-07-26)
- ✅ **v1.1 Automation & Multi-Monitor** — Phases 6-9, 11 (shipped 2026-08-01) — Phase 10 (CLI trigger) removed, out of scope
- 🚧 **v1.2 Visual Polish & Documentation** — Phases 12-14 (in progress)

Full phase details (shipped milestones): `.planning/milestones/v1.1-ROADMAP.md`

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

### 🚧 v1.2 Visual Polish & Documentation (In Progress)

**Milestone Goal:** Replace the default-WinForms look with a theme-aware, modern UI, give the tray icons real visual distinction, and ship a GitHub-ready README.

- [x] **Phase 12: Theme Infrastructure & Live Theme-Following** - MainForm and SettingsForm follow the Windows light/dark theme, including title bar and every control, live and without restart (completed 2026-08-03)
- [ ] **Phase 13: Tray & App Icon Redesign** - Shape-distinct, DPI-sharp rig/normal mode icon pair replaces the current plain pair
- [ ] **Phase 14: README & Release Documentation** - GitHub-ready README with screenshots, install/build instructions, and badges

## Phase Details

### Phase 12: Theme Infrastructure & Live Theme-Following

**Goal**: MainForm and SettingsForm visually match the current Windows light/dark theme — including the title bar and every control — and stay in sync live if the user changes the Windows theme while the app is running, with graceful degradation where the underlying Windows 11 API is unavailable.
**Depends on**: Phase 11 (v1.1)
**Requirements**: THEME-01, THEME-02, THEME-03, THEME-04, THEME-05, THEME-06
**Success Criteria** (what must be TRUE):
  1. On launch, MainForm's and SettingsForm's colors and title bar match the current Windows light/dark theme setting, including when the app starts hidden via `--tray` (THEME-01)
  2. Changing the Windows theme setting while the app is already running updates both forms' colors and title bar without an app restart, whether or not a window is currently visible (THEME-02)
  3. The title bar of MainForm and SettingsForm is dark in dark mode and light in light mode (THEME-03)
  4. Every control on both forms — including the multi-monitor settings grid — is legibly recolored in dark mode, with no stock white/gray controls left over (THEME-04)
  5. Buttons and panels render with flat, modern styling instead of legacy 3D bevel/gradient chrome (THEME-05)
  6. On Windows 11, window corners are rounded with a Mica-style backdrop; on Windows 10 or when the API is unavailable, the app runs with no visual glitches or crashes (THEME-06)
**Plans**: 6 plans (4 original + 2 gap closure from the 12-04 rig failures)
  - [x] 12-01-PLAN.md — Theme provider infrastructure (Core IThemeProvider/AppTheme, Windows registry+SystemEvents provider, DWM P/Invoke façade)
  - [x] 12-02-PLAN.md — Composition wiring, base SetColorMode, live-follow + DWM chrome for MainForm & MonitorConfirmDialog
  - [x] 12-03-PLAN.md — SettingsForm control gaps (DataGridView, GroupBox→Panel, txtHotkey) + ThemeApplier + live-follow
  - [x] 12-04-PLAN.md — Rig human-verify checkpoint (FAILED: title bar, buttons, audio combos stayed light in dark mode)
  - [x] 12-05-PLAN.md — Gap closure: immersive dark title bar (THEME-03), explicit-color flat buttons (THEME-05), audio ComboBox theming (THEME-04) + Dispose/lock hardening
  - [x] 12-06-PLAN.md — Gap closure rig re-verify: confirm the 3 previously-failing checks on Windows 11

### Phase 13: Tray & App Icon Redesign

**Goal**: The rig-mode and normal-mode tray icons (and the .exe/taskbar icon) use a genuinely distinct, legible, DPI-sharp design that replaces the current functional-but-plain pair.
**Depends on**: Nothing new (architecturally independent of Phase 12; sequenced second by convention, not dependency)
**Requirements**: ICON-01, ICON-02, ICON-03, ICON-04
**Success Criteria** (what must be TRUE):
  1. A user can tell rig mode from normal mode by icon shape/silhouette alone, without relying on color (ICON-01)
  2. Both tray icons stay legible at the actual 16x16 tray size and remain visible against both light and dark taskbar backgrounds (ICON-02)
  3. Icons are embedded as multi-resolution `.ico` files (at minimum 16/20/24/32px) so they render sharply at every DPI-scaled size Windows requests (ICON-03)
  4. The .exe/taskbar icon reuses the same artwork/motif as the tray icons at a larger size, not a different, unrelated glyph (ICON-04)
**Plans**: 3 plans (2 waves of implementation + 1 rig verification checkpoint)
  - [x] 13-01-PLAN.md — RigToggle.IconGen dev-time generator (GDI+ geometry + hand-rolled multi-frame .ico writer) → regenerated normal.ico/rig.ico + new color app.ico
  - [ ] 13-02-PLAN.md — App integration: MainForm SystemInformation.SmallIconSize DPI fix (Pitfall 1) + <ApplicationIcon> exe-icon wiring
  - [ ] 13-03-PLAN.md — Rig human-verify checkpoint: shape/legibility/DPI-sharpness/exe-icon on real Windows 11
**UI hint**: yes

### Phase 14: README & Release Documentation

**Goal**: A new GitHub-ready README.md communicates what the app does, how to get it, and how to build it, referencing the finished visual polish from Phases 12-13.
**Depends on**: Phase 12, Phase 13 (documents the finished visual result and uses the new icon artwork)
**Requirements**: DOCS-01, DOCS-02, DOCS-03
**Success Criteria** (what must be TRUE):
  1. README's feature overview includes screenshots of the toggle in both modes, or clearly marked placeholders for user-supplied screenshots where a real screenshot isn't available (DOCS-01)
  2. README documents both downloading the released .exe and building the app from source (DOCS-02)
  3. README displays GitHub badges for build status, license, and latest release version (DOCS-03)
**Plans**: TBD

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
| 12. Theme Infrastructure & Live Theme-Following | v1.2 | 6/6 | Complete    | 2026-08-03 |
| 13. Tray & App Icon Redesign | v1.2 | 1/3 | In Progress|  |
| 14. README & Release Documentation | v1.2 | 0/TBD | Not started | - |

## Backlog

Requirements not yet scoped into a milestone. See `.planning/REQUIREMENTS.md` "v2 Requirements" for the current deferred list (LOG-01 toggle history/log; THEME-07/08/09 accent-color highlight, custom toggle-switch control, manual theme override — all deferred at v1.2 scoping. CLI trigger/TRIG-02/TRIG-03 was reviewed at v1.1 close and decided permanently out of scope, not carried forward).

---
*Next: `/gsd:plan-phase 12` to plan Phase 12: Theme Infrastructure & Live Theme-Following.*
