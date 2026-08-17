# Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement - Context

**Gathered:** 2026-08-09
**Status:** Ready for planning

<domain>
## Phase Boundary

MainForm becomes a monitor-tile dashboard: one clickable tile per detected monitor (icon + number + on/off status) is the first thing the user sees, followed by a shared Identify action, then the Rig/Normal mode toggle, with Settings relocated to a small de-emphasized control at the bottom. Clicking a tile toggles that monitor directly, gated by the existing `SkipMonitorConfirmation` setting exactly as the retiring panel does today. `MonitorPanelForm` and both its entry points (`btnMonitors` on MainForm, `trayMonitorsMenuItem` in the tray context menu) are removed once the tile dashboard replaces their capability. Does not touch the custom toggle-switch control's visual redesign (Phase 20 — this phase repositions the existing plain-button toggle, doesn't restyle it), accent-color theming (Phase 21), manual theme override (Phase 22), or SettingsForm's layout (Phase 23). Requirements: TILE-01 through TILE-07, MAIN-01, MAIN-02.

</domain>

<decisions>
## Implementation Decisions

### Tile Visual Design
- **D-01:** Each tile's monitor icon reuses the existing monitor-silhouette motif from Phase 13's `RigToggle.IconGen` (the same glyph language already used for the normal-mode tray/exe icon) rather than a new, simpler glyph — keeps the icon vocabulary consistent across tray, exe, and now the tile dashboard.
- **D-02:** On/off status must be unmistakable at a glance, not subtle — the user explicitly rejected a single subtle signal and wants overlapping cues. Implement as a **combined outline + color shift**: an OFF tile renders the monitor icon as a hollow/gray outline (no fill); an ON tile renders it solid-filled in an active color. Two independent visual signals (shape AND color) rather than a single dot/badge.
- **D-03:** The primary monitor's tile gets a small distinguishing badge/marker (exact glyph/placement left to planning — e.g. a small "P" corner badge) since disabling the primary monitor is the app's core action and deserves a visual callout distinct from secondary monitors.

### MainForm Sizing & Layout
- **D-04:** MainForm auto-sizes based on the actual detected monitor count rather than staying a fixed size — the window grows/shrinks to fit however many tiles are currently shown.
- **D-05:** Auto-sizing has a width cap: once enough tiles would make the window unreasonably wide (planning/research to pick the exact tile-count threshold, informally "~4-5 tiles" per discussion), additional tiles wrap onto a second row instead of the window continuing to widen indefinitely.
- **D-06:** The window resizes live when the tile row changes — both on hotplug (TILE-06 already requires live tile refresh while visible) and presumably on first-open — rather than staying fixed at whatever size it opened with and leaving empty space or requiring scrolling.
- **D-07:** Tiles are a **fixed size** regardless of monitor count — they never shrink to cram more into one row. More monitors means more wrapping (per D-05), not smaller tiles. Keeps the icon/number/status legible no matter how many monitors are connected.
- **D-08:** Tiles are ordered by monitor number (1, 2, 3…), not by attempting to infer physical desk position — simple, stable, matches how Windows itself numbers displays, and doesn't reorder on hotplug.

### Settings & Identify Placement
- **D-09:** The vertical order on MainForm, top to bottom: **tile row → shared Identify button → Rig/Normal toggle → (space) → Settings**, per MAIN-01/MAIN-02. Identify sits directly below the tiles (not above them) — you see your monitors' state first, then the utility action to check which physical screen is which, then the primary toggle action.
- **D-10:** Settings becomes a **small icon-only gear button** in a bottom corner — not a labeled text button, even a small one. Matches MAIN-02's "de-emphasized, no longer competing with the toggle/tiles" requirement more strongly than a shrunk text button would.
- **D-11:** Identify stays a **single shared button** (not a per-tile action) that numbers every screen at once when clicked, directly porting `MonitorPanelForm.BtnIdentify_Click`'s existing behavior (TILE-04) rather than adding N per-tile identify affordances.

### Claude's Discretion
- Exact tile dimensions (pixel size), spacing between tiles, and the specific width threshold (in tile-count or pixels) that triggers wrapping to a second row — informed by D-05/D-07 but not pinned to exact numbers; planning should pick values that keep the window looking proportionate at 2 monitors (the user's actual rig) through at least 4-5.
- Exact visual treatment of the primary-monitor badge (D-03) — icon/glyph choice, corner placement, size — as long as it's clearly a marker distinct from the on/off outline+fill signal (D-02).
- Exact gear-icon geometry for the Settings button (D-10) — hand-drawn via GDI+ consistent with the project's existing icon-generation conventions (`RigToggle.IconGen`), or a simpler inline `OnPaint` glyph; either is fine as long as it reads clearly as "settings" at small size.
- Whether the live window-resize (D-06) uses an animated/smooth transition or an instant resize — not discussed, default to instant unless research surfaces a strong reason otherwise.
- Exact mechanism for porting `MonitorPanelForm`'s mutation logic, `BeginExclusiveMonitorAccess()` lease, hotplug subscription, and `MonitorConfirmDialog` gating into `MainForm` — architecturally scoped in `.planning/research/ARCHITECTURE.md` (dumb `MonitorTile` UserControl raising events, `MainForm` remains sole `IMonitorController` caller) and `.planning/research/PITFALLS.md` (lease/race reintroduction, event-subscription lifecycle mismatch) — implementation detail for planning, not re-litigated here.
- Whether wrapping to a second row also affects the shared Identify button/toggle/Settings vertical positions (i.e., do they shift down as the tile area grows in height) — natural consequence of D-04/D-06's auto-sizing, left to planning to lay out correctly.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (TILE-01 through TILE-07, MAIN-01, MAIN-02 — mapped to this phase; Out of Scope section rules out drag-to-rearrange tiles and a second tile-specific confirmation dialog)
- `.planning/ROADMAP.md` (Phase 19 section — goal, success criteria, "UI hint: yes", first phase of v2.1)
- `.planning/PROJECT.md` (Current Milestone: v2.1 section — full milestone framing, retirement of `MonitorPanelForm`)

### Research (this milestone — read before planning)
- `.planning/research/SUMMARY.md` §"Phase A: Monitor-Tile Dashboard + MonitorPanelForm Retirement" — recommended build order (standalone tile control → read-only population → mutation logic ported verbatim → hotplug + Identify ported → `MonitorPanelForm` deleted last)
- `.planning/research/ARCHITECTURE.md` — `MonitorTile` as a dumb/presentational `UserControl` (never calls `IMonitorController` directly, only raises events); `MainForm` remains the sole controller/orchestrator caller, preserving DISPLAY-12's single-shared-guard property
- `.planning/research/PITFALLS.md` — Pitfall 1 (new custom-drawn controls silently falling outside the hand-maintained `ThemeApplier` pipeline — both `OnThemeChanged` AND `InitializeTrayState()` must theme the new tiles), Pitfall 2 (GraphicsPath seam artifacts — reuse Phase 13's validated stroke-then-fill compositing for tile icons), Pitfall 4/5 (lease/race reintroduction if `BeginExclusiveMonitorAccess()` is "simplified away" during the Form-absorption refactor), Pitfall 5 (event-subscription lifecycle mismatch — `MainForm` is hidden-not-closed during tray-resident operation, unlike the transient `MonitorPanelForm`), Pitfall 9 (DPI/`AutoScaleMode.Font` pixel-math breakage in new `OnPaint` tile geometry — verify at 125%/150% scale on real hardware)
- `.planning/research/FEATURES.md` — table-stakes tile behaviors (PANEL-01..05 carryover), explicit anti-features (no drag-to-rearrange, no second confirmation dialog)

### Prior phases (icon/theming precedent — this phase must follow, not regress)
- `.planning/milestones/v1.2-phases/13-tray-app-icon-redesign/13-CONTEXT.md` — D-01 (monitor silhouette motif, the icon this phase's tiles reuse per D-01 above), D-02 (flat filled Fluent-style, procedurally drawn via GDI+), D-03 (icons are code-drawn geometry, not sourced artwork)
- `.planning/milestones/v1.2-phases/12-theme-infrastructure-live-theme-following/12-CONTEXT.md` — D-08 (theme application must not live in `Form_Load`/`OnShown` alone — must also fire via `OnHandleCreated`/`InitializeTrayState()` so `--tray` hidden-start still themes correctly; directly relevant since new tiles must be wired into both existing theming call sites per Pitfall 1 above)
- `.planning/milestones/v2.0-phases/16-normal-mode-explicit-monitor-config-mode-store-redesign/16-CONTEXT.md` — established pattern of fail-loud/never-silently-guess for mode-adjacent state, and the "never collapse two different states into one" discipline this phase's on/off tile signal (D-02) follows in spirit

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/MainForm.cs` / `MainForm.Designer.cs` — current controls: `lblMode` (16,16), `btnToggle` (16,60, 288x40, "Switch to Rig Mode"), `btnSettings` (16,108, 288x32), `btnMonitors` (16,148, 288x32, to be removed), `notifyIcon`, `trayMonitorsMenuItem` (to be removed); current `ClientSize` is a fixed 320x200 — this phase's D-04/D-05/D-06 replace the fixed size with auto-sizing
- `src/RigToggle.App/MonitorPanelForm.cs` / `MonitorPanelForm.Designer.cs` — the form being retired; contains the exact behaviors to port: `IMonitorController`-backed enumeration/mutation, `CreateStatusDot()` (status-dot drawing precedent for the new tile status rendering), `BeginExclusiveMonitorAccess()` lease usage around `MonitorConfirmDialog.ShowDialog()`'s nested message loop, `OnDisplaySettingsChanged` hotplug handler (with its documented disposed-race/cross-thread `BeginInvoke` guards), `BtnIdentify_Click` (Identify overlay creation, ported per D-11)
- `src/RigToggle.App/MonitorConfirmDialog.cs` — the existing confirmation dialog gated by `SkipMonitorConfirmation`, reused as-is when a tile disables the last/primary monitor
- `src/RigToggle.App/MonitorIdentifyOverlay.cs` — the standalone per-monitor overlay Identify already creates; reused as-is, just triggered from the new shared Identify button location (D-09/D-11)
- `src/RigToggle.App/ThemeApplier.cs` — existing per-control theming pipeline; new tile controls and the new gear-icon Settings button must be added to this pipeline (Pitfall 1)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MonitorPanelForm.CreateStatusDot()` — existing GDI+ status-indicator drawing code; conceptually the closest precedent for the new tile's outline-vs-filled status rendering (D-02), even though the exact visual mechanism differs (outline/fill on the monitor icon itself, not a separate dot).
- `RigToggle.IconGen`'s monitor-silhouette drawing code (Phase 13) — the icon geometry D-01 explicitly reuses for tile icons.
- `MonitorConfirmDialog`, `MonitorIdentifyOverlay`, `BeginExclusiveMonitorAccess()` — all reused unchanged from the retiring `MonitorPanelForm`, just called from `MainForm` instead.

### Established Patterns
- Fail-loud, never-silently-guess for state (mode-store precedent, Phase 16) — while not directly about monitor state, the same "make status unmistakable" instinct is what drove D-02's rejection of a subtle single-signal status indicator.
- `ThemeApplier`'s explicit per-control theming (not a recursive Controls-tree walk) — every new control (tiles, Identify button, gear-icon Settings button) must be explicitly added to both `OnThemeChanged` and `InitializeTrayState()`.
- Icons/glyphs are procedurally drawn via GDI+, never sourced artwork — established in Phase 13, continued here for tile icons and the new gear icon.

### Integration Points
- `MainForm`'s constructor/composition-root wiring (`Program.cs`) currently constructs `MonitorPanelForm` via a factory (Phase 17); this factory and its cached-instance/entry-point wiring (`btnMonitors` click handler, `trayMonitorsMenuItem` click handler) go away entirely once `MonitorPanelForm` is retired.
- `MainForm.RefreshUi()` (mode label/tray icon/tooltip) is the natural place the tile row's population/refresh logic slots in alongside existing mode-dependent UI updates.

</code_context>

<specifics>
## Specific Ideas

- User's own words for the status signal: "Something that will quickly and at a glance show if it's on or off without any doubt" — drove D-02's combined outline+color-shift decision over a single subtle cue.
- User's own words from milestone scoping: tiles should show "an icon and number of the monitor," the toggle button comes right after the tile row, and Settings goes "somewhere at the bottom" — all reflected in D-09.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. THEME-08 (toggle-switch visual redesign) was explicitly acknowledged as Phase 20's scope, not this phase's — Phase 19 only repositions the existing plain-button toggle per MAIN-01, it does not restyle it.

</deferred>

---

*Phase: 19-Monitor-Tile-Dashboard-MonitorPanelForm-Retirement*
*Context gathered: 2026-08-09*
