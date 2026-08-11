# Phase 22: SettingsForm Layout Pass - Context

**Gathered:** 2026-08-11
**Status:** Ready for planning

<domain>
## Phase Boundary

SettingsForm is restructured into a two-column-plus-shared layout: a **Normal** column (Normal-mode monitor grid + Normal audio device picker) and a **Rig** column (Rig-mode monitor grid + Rig audio device picker) side by side, with a full-width **shared/global section** below for everything that isn't mode-specific (Target App path/Browse/Clear, Hotkey capture, debug-logging checkbox, tray/autostart checkboxes, and a reserved slot for Phase 23's future Theme radio group). The whole form migrates from today's plain-`Panel`/hardcoded-pixel-position layout to `TableLayoutPanel`/`FlowLayoutPanel`, sized to its content (no fixed target dimensions), with resizing enabled (`FormBorderStyle.Sizable`, no maximize button). No new settings fields beyond the layout container migration itself — Phase 23's radio group is reserved space only, not built here. Requirements: SETTINGS-01, SETTINGS-02.

</domain>

<decisions>
## Implementation Decisions

### Grouping Structure
- **D-01:** Replace today's category-based grouping (all monitor grids together, then a separate shared Audio Devices panel containing both Normal's and Rig's audio pickers, then App Path, etc. — all stacked in one 396px-wide column while the right half of the 828px-wide form sits empty) with **mode-based grouping**: a **Normal column** (Normal's monitor grid + Normal's audio device picker) and a **Rig column** (Rig's monitor grid + Rig's audio device picker), side by side. This splits the current `pnlAudioDevices` panel's two pickers apart, moving each into its respective mode's column — an intentional regrouping, not just a spacing fix, and explicitly not the "re-tabbing into a wizard/multi-tab flow" anti-pattern FEATURES.md warns against (this stays a single-page form).
- **D-02:** Everything that is **not mode-specific** — Target App path + Browse/Clear buttons, Hotkey capture box, debug-logging checkbox, the three tray/autostart checkboxes (`chkCloseMinimizesToTray`, `chkMinimizeToTray`, `chkStartWithWindows`) — goes in **one shared, full-width section below the two mode columns**, not sub-grouped into further boxes.

### Layout Technology
- **D-03:** Migrate the **entire form** (not just the new two-column area) from plain `Panel` + hardcoded `Location`/`Size` to `TableLayoutPanel`/`FlowLayoutPanel`. Explicitly chosen over keeping absolute positioning despite the research's flagged DPI risk (Pitfall 9 — this migration is only verifiable on real hardware at 125%/150% scale, this build environment has no Windows GUI) and despite every other form in this codebase (MainForm, MonitorPanelForm) using the plain-Panel pattern — user prioritized one coherent layout system over minimizing new-territory risk. Accept that this needs a dedicated multi-round rig-verification pass at non-100% Windows display scale before this phase can be considered done, same discipline as prior phases' rig-verification requirements.

### Phase 23 Coordination
- **D-04:** Reserve a row/cell in the shared global section (per D-02) for Phase 23's future System/Light/Dark radio group — placed among the other global settings, not given a separate more-prominent spot. Phase 22 does NOT build the radio group itself (no new `AppSettings` fields, no new logic) — this is purely leaving layout room so Phase 23 doesn't have to reflow an already-finished TableLayoutPanel to fit itself in.

### Window Sizing & Resizing
- **D-05:** The form sizes itself to whatever the new `TableLayoutPanel` content naturally needs — no fixed/target width or height to hit or verify against.
- **D-06:** Enable resizing: change `FormBorderStyle` from `FixedDialog` to `Sizable`. Keep `MaximizeBox = false` (no maximize button) — draggable-edge resizing only, not full standard resizable-window chrome. This is a deliberate behavior change from today's fixed-size dialog, not incidental to the layout migration.

### Claude's Discretion
- Exact `TableLayoutPanel`/`FlowLayoutPanel` row/column structure, cell sizing (`Percent`/`AutoSize`/`Absolute`), and how nested containers compose (e.g., whether each mode column is itself a nested `TableLayoutPanel` or a `FlowLayoutPanel`) — informed by D-01/D-03 but not pinned to an exact structure.
- Whether `MinimizeBox` stays `false` (matching today) or becomes `true` now that `FormBorderStyle` is changing to `Sizable` (D-06) — not discussed; default to leaving it `false` (matching today's minimize-button absence) unless `Sizable` makes that visually inconsistent with typical Windows resizable-window chrome, in which case planning should pick whichever reads as more standard.
- Exact `TableLayoutPanel` cell width split between the Normal and Rig columns (50/50 vs. content-driven) — should look balanced; the two DataGridViews are structurally identical in content type so an even split is the natural default, but not pinned.
- How the existing THEME-05 "flat bordered Panel replacing GroupBox bevel" visual treatment (Phase 12 precedent — `ThemeApplier` cannot recolor a native `GroupBox`'s 3D border, so this codebase uses plain bordered `Panel`s as GroupBox-style section containers) carries into the new grouping — each new logical section (Normal column, Rig column, shared global section) most likely still wants this same flat-bordered-panel visual language, just relocated/resized, but the exact panel boundaries within the new TableLayoutPanel structure are left to planning.
- Exact DPI/rig-verification checklist steps (125%/150% scale checks per Pitfall 9) — Claude should produce a concrete checklist during planning, following the same pattern established in Phase 21's rig-verification requirement, since this environment cannot self-verify Windows display-scale rendering.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (SETTINGS-01, SETTINGS-02 — this phase's requirements)
- `.planning/ROADMAP.md` §"Phase 22: SettingsForm Layout Pass" (goal, 2 success criteria, "UI hint: yes", reordered 2026-08-11 to precede Phase 23/Manual Light-Dark Override)
- `.planning/PROJECT.md` (Current Milestone: v2.1 section — full milestone framing)

### Research (this milestone — read before planning)
- `.planning/research/SUMMARY.md` §"Phase E: SettingsForm Layout Pass" — delivers/avoids summary, explicit note that Phase D's (now Phase 23's) radio group "can slot into this pass or its own" (this phase chose the latter, per D-04's reserve-space-only decision)
- `.planning/research/FEATURES.md` — table-stakes ("no overlapping controls," "logically grouped sections"), the explicit anti-scope-creep entry against re-tabbing into a wizard/multi-tab flow (D-01's mode-based regrouping stays within this boundary — single page, no new navigation model)
- `.planning/research/PITFALLS.md` — Pitfall 9 (DPI/`AutoScaleMode.Font` pixel-math breakage, specifically calling out that this codebase's `SettingsForm.Designer.cs` currently has NO `TableLayoutPanel`/`FlowLayoutPanel` anywhere, and that migrating to one changes how the two `DataGridView`s' internal scroll/column-width behavior interacts with `Dock`/`Anchor` — must be checked at 125%/150% scale on real hardware, this build environment cannot exercise Windows display scaling at all)
- `.planning/research/ARCHITECTURE.md` §"OverridableThemeProvider" — background on what Phase 23's reserved radio group will eventually wire to (not needed for this phase's implementation, useful context for why the slot exists)

### Prior phases (precedent this phase must follow, not regress)
- `.planning/milestones/v1.2-phases/12-theme-infrastructure-live-theme-following/12-CONTEXT.md` — THEME-05's "flat bordered Panel replacing GroupBox bevel" pattern (the reason this form uses `Panel`, not `GroupBox`, for its section containers today — `ThemeApplier` cannot recolor a native `GroupBox`'s 3D border)
- `.planning/phases/21-accent-color-reading-live-update/21-CONTEXT.md` and `21-03-PLAN.md` — precedent for how this project structures a rig-verification checklist for changes this Linux dev environment cannot visually confirm (registry/DWM checks there; DPI-scale checks here, per Pitfall 9)

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/SettingsForm.Designer.cs` (677 lines) — the entire surface area of this phase. Current structure (measured directly): `pnlMonitor` (Rig grid, 12,12, 396×234), `pnlMonitorNormal` (Normal grid, 420,12, 396×234) — these two already sit side by side; `pnlAudioDevices` (12,258, 396×132, contains BOTH `cboAudioNormal` and `cboAudioRig` together — this is the panel D-01 splits apart); `pnlAppPath` (12,402, 396×76); then `chkEnableDebugLogging`, `lblHotkeyCaption`/`txtHotkey`, `chkCloseMinimizesToTray`, `chkMinimizeToTray`, `chkStartWithWindows`, `lblAutostartWarning`, `btnSaveSettings`/`btnDiscardChanges` all stacked from y=484 to y=752, all at `Size.Width=396` — none of them use the right half of the 828px-wide `ClientSize`. `AutoScaleMode.Font` (line 578, same as every other form). `FormBorderStyle.FixedDialog`, `MaximizeBox=false`, `MinimizeBox=false` (lines 580-582, all changing per D-06). `ClientSize = 828×768` (line 579, becomes content-driven per D-05).
- `src/RigToggle.App/SettingsForm.cs` (1176 lines) — no dynamic/runtime layout code exists; all positioning is Designer-fixed. Confirms this phase's changes are Designer.cs-centric, though `SettingsForm.cs`'s constructor/Save/Load logic must still be read to understand what each control does and its data-binding, before repositioning it.
- `src/RigToggle.App/ThemeApplier.cs` — the existing per-control theming pipeline (`ThemeButton`, and whatever panel-background theming SettingsForm already has) must keep working after the container-type migration; verify every relocated/regrouped control is still reached by SettingsForm's theming call site(s).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- THEME-05's flat-bordered-`Panel`-as-GroupBox pattern (Phase 12) — the established section-container visual language this codebase already uses instead of native `GroupBox`; the new Normal column, Rig column, and shared global section most likely each want this same treatment.
- The two `DataGridView`s' existing `AutoSizeMode.Fill` column configuration — already handles internal column-width behavior; the risk (per Pitfall 9) is specifically how they behave inside a new `TableLayoutPanel` cell with `Dock=Fill`, not their own column logic, which doesn't need to be touched.

### Established Patterns
- `AutoScaleMode.Font` is used by every form in this codebase (`MainForm`, `SettingsForm`, `MonitorPanelForm`) — no `ApplicationHighDpiMode`/manifest override exists at the project level. This phase's `TableLayoutPanel` migration must work correctly under this existing scaling mode, not assume `Dpi`-mode scaling.
- Fail-loud/never-silently-guess and "verify on real rig hardware, not just design-time 100% scale" are established project-wide conventions (Phase 12/13 precedent, reused explicitly for Phase 21's accent-color work) — this phase's DPI-scale verification should follow the same discipline.

### Integration Points
- `SettingsForm`'s constructor wiring (data-binding each control to `AppSettings`/`IMonitorController`/`IAudioDeviceEnumerator` etc.) is untouched by this phase — only `Location`/`Size`/`Dock`/`Anchor`/container-type change, not what each control does or binds to.
- Phase 23 (Manual Light/Dark Override) will read whatever reserved slot/row this phase leaves in the shared global section (D-04) and place its System/Light/Dark radio group there — Phase 22's plan should make that slot's existence/location easy for Phase 23's planner to find (e.g., a clearly-named empty `TableLayoutPanel` row or a code comment marking the reserved space).

</code_context>

<specifics>
## Specific Ideas

- User's own framing for the reflow: "put it side by side. One side is for normal mode and normal audio and the second for rig mode and audio" — the exact grouping-by-mode structure D-01 captures, explicitly preferred over the current grouping-by-category (all monitors together, then all audio together) structure.
- User explicitly chose the higher-risk TableLayoutPanel migration over the lower-risk absolute-positioning fix, and explicitly chose to enable resizing (a real behavior change) rather than keep today's FixedDialog — both against this discussion's own "(recommended)" default option, so these are firm, deliberate calls, not defaults to second-guess during planning.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Phase 23's System/Light/Dark radio group was discussed only as a layout-reservation question (D-04); building it is explicitly Phase 23's scope, not this phase's, and no new `AppSettings` fields or logic are introduced here.

</deferred>

---

*Phase: 22-SettingsForm-Layout-Pass*
*Context gathered: 2026-08-11*
