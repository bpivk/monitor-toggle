# Phase 22: SettingsForm Layout Pass - Research

**Researched:** 2026-08-12
**Domain:** WinForms `TableLayoutPanel`/`FlowLayoutPanel` layout migration (first use in this codebase), `DataGridView` sizing inside a table cell, `AutoScaleMode.Font` DPI behavior, `FormBorderStyle.Sizable` + content-driven sizing
**Confidence:** MEDIUM-HIGH (official Microsoft Learn docs cover the container mechanics with HIGH confidence; the specific DataGridView-inside-TableLayoutPanel-cell-at-125%/150%-scale interaction is, per this project's own PITFALLS.md Pitfall 9, only provable on real Windows hardware — this document treats it as unverified and produces a rig checklist rather than a claim of correctness)

## Summary

This phase migrates `SettingsForm.Designer.cs` (677 lines, zero `TableLayoutPanel`/`FlowLayoutPanel` usage today) from hardcoded `Panel` + `Location`/`Size` positioning to a `TableLayoutPanel`-based structure: an outer 2-row table (mode-columns row + shared-section row), with the mode-columns row itself split into a nested 2-column table (Normal | Rig), and the shared section built as either a single-column table or a `FlowLayoutPanel` (`FlowDirection.TopDown`, `WrapContents = false`) stacking the app-path row, hotkey row, checkboxes, and the Phase 23 reserved slot. This is a well-trodden WinForms pattern with strong official-docs backing for the container mechanics themselves (`AutoSizeMode.GrowAndShrink` + `Percent`/`AutoSize`/`Absolute` row-and-column styles, official DataGridView `AutoSizeColumnsMode.Fill` + `FillWeight` guidance). The two genuinely novel risk areas for *this specific codebase* — DataGridView `Dock=Fill` inside a nested TableLayoutPanel cell at non-100% Windows scale, and `Form.AutoSize=true` combined with `FormBorderStyle.Sizable` — have no first-party precedent anywhere else in this codebase (`MainForm`, `MonitorPanelForm` both use the old Panel pattern) and cannot be exercised in this Linux build environment at all.

One concrete, high-confidence finding worth flagging early: `MinimizeBox` should stay `false`, not become a planning toss-up. `SettingsForm` is shown via `ShowDialog()` with `ShowInTaskbar = false` (both unchanged this phase) — WinForms has a documented, by-design failure mode where minimizing a `ShowDialog()`-shown, non-taskbar window leaves the user with no way to restore it (some reports describe the dialog closing outright when minimized in this configuration). This is not a style preference; enabling `MinimizeBox` on this specific dialog is a functional bug waiting to happen, independent of whether `FormBorderStyle` is `Sizable` or `FixedDialog`.

Also confirmed by direct code read (not assumption): `ThemeApplier.cs` and `SettingsForm.cs`'s theming call sites (`OnThemeChanged`, `SettingsForm_Load`) reference specific control *instance fields* by name (`dgvMonitors`, `txtHotkey`, `btnBrowse`, `cboAudioNormal`, etc.) — never a recursive `Controls`-tree walk, never a container-type check. Reparenting these same field-referenced controls from `Panel` children to `TableLayoutPanel`/`FlowLayoutPanel` children changes nothing about whether theming reaches them; `Application.SetColorMode(SystemColorMode.System)` (called every theme flip) handles base `Panel`/`TableLayoutPanel`/`FlowLayoutPanel` background/foreground colors automatically the same way it already does for `Panel`. No panel-background theming exists in this codebase to preserve or port — panels are themed implicitly by `SetColorMode`, not by an explicit `ThemeApplier` call.

**Primary recommendation:** Use a `TableLayoutPanel` (not `FlowLayoutPanel`) as the outer container and for both the Normal/Rig column split, since column widths need controlled proportional sizing (Percent) that `FlowLayoutPanel` cannot express directly. Reserve `FlowLayoutPanel` for the shared section only, where controls genuinely stack top-to-bottom with no side-by-side sizing math needed. Set `Form.AutoSize = true` + `AutoSizeMode = GrowAndShrink`, outer `TableLayoutPanel.AutoSize = true` + `AutoSizeMode = GrowAndShrink`, keep every row/column that must flex `Percent` or `AutoSize` (never `Absolute` for anything containing the DataGridViews), and treat DataGridView-in-cell-at-DPI-scale and AutoSize-vs-user-resize-at-DPI-scale as two rig-verify-only claims — plan a dedicated non-100%-scale checkpoint modeled on Phase 21's precedent (`21-03-PLAN.md`), not a claim of "should work."

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Layout container structure (TableLayoutPanel/FlowLayoutPanel composition) | Presentation (WinForms Designer.cs) | — | Pure UI layout, no business logic; confined entirely to `SettingsForm.Designer.cs` per CONTEXT.md's canonical-refs framing |
| Control grouping/regrouping (mode columns, shared section) | Presentation (WinForms Designer.cs) | — | Visual regrouping only — no change to what any control binds to or does (`SettingsForm.cs` constructor/Save/Load logic untouched) |
| Theming of relocated controls | Presentation (`ThemeApplier.cs` + `SettingsForm.cs` call sites) | — | Existing per-instance theming pipeline is container-type-agnostic (verified by direct read); no new tier involvement needed |
| Form resize/AutoSize behavior | Presentation (WinForms Form/Designer.cs) | — | `FormBorderStyle`, `AutoSize`, `MinimizeBox` are all Form-level presentation properties; no data/business-logic layer touches window chrome |
| DPI/scale correctness | Presentation (WinForms rendering) + OS (Windows DWM/scale engine) | — | `AutoScaleMode.Font` scaling and DataGridView fill-column math are entirely presentation-tier, but their correctness is only observable through the OS's actual DPI-scale rendering pipeline — hence rig-only verification |

This phase touches exactly one tier (Presentation/WinForms Designer + code-behind). There is no API, database, or business-logic surface in scope — `SettingsForm.cs`'s data-binding, validation, and save/load logic are explicitly out of scope per CONTEXT.md.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SETTINGS-01 | SettingsForm has no overlapping or crowded controls at its default window size | Standard Stack / Architecture Patterns sections below specify the exact TableLayoutPanel/FlowLayoutPanel row-column structure and sizing rules (Percent/AutoSize/Absolute) that prevent overlap by construction; the DPI/Rig-Verification Checklist covers the one class of overlap (post-scale-change) that cannot be proven at design time |
| SETTINGS-02 | Related controls (each mode's monitor grid, audio device pickers, app path, hotkey capture) are visually grouped and consistently spaced | Architecture Patterns' nested-column structure directly implements D-01/D-02's mode-based grouping; "Don't Hand-Roll" and Code Examples sections show the Padding/Margin-based spacing mechanism that replaces today's hand-picked pixel offsets |
</phase_requirements>

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Replace today's category-based grouping (all monitor grids together, then a separate shared Audio Devices panel containing both Normal's and Rig's audio pickers, then App Path, etc.) with **mode-based grouping**: a **Normal column** (Normal's monitor grid + Normal's audio device picker) and a **Rig column** (Rig's monitor grid + Rig's audio device picker), side by side. This splits the current `pnlAudioDevices` panel's two pickers apart, moving each into its respective mode's column — an intentional regrouping, explicitly not the "re-tabbing into a wizard/multi-tab flow" anti-pattern FEATURES.md warns against (stays a single-page form).
- **D-02:** Everything not mode-specific — Target App path + Browse/Clear buttons, Hotkey capture box, debug-logging checkbox, the three tray/autostart checkboxes — goes in **one shared, full-width section below the two mode columns**, not sub-grouped into further boxes.
- **D-03:** Migrate the **entire form** (not just the new two-column area) from plain `Panel` + hardcoded `Location`/`Size` to `TableLayoutPanel`/`FlowLayoutPanel`. Explicitly chosen over keeping absolute positioning despite the flagged DPI risk (Pitfall 9) and despite every other form in this codebase using the plain-Panel pattern — user prioritized one coherent layout system over minimizing new-territory risk. Accept that this needs a dedicated multi-round rig-verification pass at non-100% Windows display scale before this phase can be considered done.
- **D-04:** Reserve a row/cell in the shared global section for Phase 23's future System/Light/Dark radio group — placed among the other global settings, not given a separate more-prominent spot. Phase 22 does NOT build the radio group itself (no new `AppSettings` fields, no new logic) — layout room only.
- **D-05:** The form sizes itself to whatever the new `TableLayoutPanel` content naturally needs — no fixed/target width or height to hit or verify against.
- **D-06:** Enable resizing: change `FormBorderStyle` from `FixedDialog` to `Sizable`. Keep `MaximizeBox = false` — draggable-edge resizing only, not full standard resizable-window chrome. Deliberate behavior change, not incidental.

### Claude's Discretion

- Exact `TableLayoutPanel`/`FlowLayoutPanel` row/column structure, cell sizing (`Percent`/`AutoSize`/`Absolute`), and how nested containers compose (e.g., whether each mode column is itself a nested `TableLayoutPanel` or a `FlowLayoutPanel`) — informed by D-01/D-03 but not pinned to an exact structure.
- Whether `MinimizeBox` stays `false` (matching today) or becomes `true` now that `FormBorderStyle` is changing to `Sizable` — default to leaving it `false` unless `Sizable` makes that visually inconsistent with typical Windows resizable-window chrome, in which case planning should pick whichever reads as more standard. **Research finding: keep `false` — see Pitfall 3 below, this is not a style call, it's a functional-bug avoidance.**
- Exact `TableLayoutPanel` cell width split between the Normal and Rig columns (50/50 vs. content-driven) — the two DataGridViews are structurally identical in content type so an even split is the natural default, not pinned. **Research finding: use `Percent = 50F` for both columns — see Standard Stack below.**
- How the existing THEME-05 "flat bordered Panel replacing GroupBox bevel" visual treatment carries into the new grouping — each new logical section (Normal column, Rig column, shared global section) most likely still wants this same flat-bordered-panel visual language, just relocated/resized, but exact panel boundaries are left to planning.
- Exact DPI/rig-verification checklist steps (125%/150% scale checks per Pitfall 9) — see "DPI / Rig-Verification Checklist" section below.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. Phase 23's System/Light/Dark radio group was discussed only as a layout-reservation question (D-04); building it is explicitly Phase 23's scope.
</user_constraints>

## Standard Stack

### Core

| Component | Namespace/Type | Purpose | Why Standard |
|-----------|-----------------|---------|---------------|
| `TableLayoutPanel` | `System.Windows.Forms.TableLayoutPanel` (BCL, no NuGet) | Outer form container, mode-column split, per-column internal layout | Only WinForms container that natively supports proportional (`Percent`) multi-column sizing with content-driven row heights; required for the side-by-side Normal/Rig columns D-01 specifies |
| `FlowLayoutPanel` | `System.Windows.Forms.FlowLayoutPanel` (BCL, no NuGet) | Shared/global section (app path row, hotkey row, checkboxes, Phase 23 reserved slot) | Simpler than a single-column `TableLayoutPanel` for a pure top-to-bottom stack with no proportional-width needs; `FlowDirection.TopDown` + `WrapContents = false` + `AutoSize = true` reproduces vertical-stack behavior with less row-management boilerplate |
| `Panel` (`BorderStyle.FixedSingle`) | `System.Windows.Forms.Panel` (BCL, no NuGet) | Section-container visual wrapper (THEME-05 flat-bordered-box pattern) inside each `TableLayoutPanel` cell | Existing codebase convention (Phase 12, THEME-05) — `GroupBox`'s native 3D bevel cannot be recolored by `ThemeApplier`, so this codebase already committed to `Panel`-as-GroupBox-substitute; reuse verbatim, just relocated |

No new package installs — `TableLayoutPanel`/`FlowLayoutPanel`/`Panel` are all part of the WinForms desktop SDK (`Microsoft.NET.Sdk` with `UseWindowsForms=true`, `net10.0-windows`, already the project's target per `RigToggle.App.csproj`). **Package Legitimacy Audit is not applicable to this phase** — no `npm`/`pip`/`cargo`/NuGet package is added.

### Supporting

| Item | Purpose | When to Use |
|------|---------|-------------|
| `DataGridViewAutoSizeColumnsMode.Fill` + `FillWeight` | Already in use on `colMonitorName`/`colMonitorNameNormal` (`AutoSizeMode = Fill`); unchanged this phase | Keep exactly as-is — the risk is the *container* (new TableLayoutPanel cell + Dock=Fill), not the grid's own column-fill math, which does not need touching (confirmed: official docs describe Fill mode as computed from the DataGridView's own client-area width, not from any awareness of its parent container type) |
| `TableLayoutPanel.SetColumnSpan` / `SetRowSpan` | For the shared section if built as a single-column `TableLayoutPanel` row spanning the outer table's 2 mode-columns | Only needed if the shared section is a row of the *outer* table rather than a separately-added, separately-Docked `FlowLayoutPanel` below the outer table — either composition is valid; see Architecture Patterns for the recommended one |
| `AutoScaleMode.Font` (unchanged) | Existing codebase-wide DPI mechanism, all three forms | This phase must work correctly under the existing mode — do not introduce `AutoScaleMode.Dpi` or an `ApplicationHighDpiMode` manifest change, that is out of this phase's scope and would be a much larger DPI-strategy change than a layout migration |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `TableLayoutPanel` for mode columns | `FlowLayoutPanel` with `FlowDirection.LeftToRight` for the two columns | `FlowLayoutPanel` cannot express "each column gets exactly 50% of available width" — it sizes each child to its natural/Anchor size and flows left-to-right; achieving an even, resize-responsive 50/50 split would require manual `Resize` event math, which is strictly worse than `TableLayoutPanel`'s built-in `Percent` column style. Not recommended for this phase. |
| Nested `TableLayoutPanel` per mode column | A single flat 2-column outer `TableLayoutPanel` with monitor grid + audio picker as separate rows directly in the outer table | Flattening loses the ability to give each column its own independent internal row heights/padding and makes the Panel-as-GroupBox wrapper harder to scope cleanly per column; nesting one `TableLayoutPanel` (or `Panel`) per column is the standard "compose small layout panels, don't build one giant grid" pattern documented by Microsoft's own walkthroughs. |
| `FlowLayoutPanel` for the shared section | A single-column `TableLayoutPanel` with one `AutoSize` row per control | Functionally equivalent for a pure vertical stack; `FlowLayoutPanel` requires less per-control row bookkeeping (no `RowStyles.Add` per item) for a list that may grow (Phase 23's reserved slot). Either is acceptable; `FlowLayoutPanel` is recommended for the shared section specifically because it is the simpler tool for a list that stacks and may append one more item next phase. |

**Installation:**
No installation required — verify current SDK only:
```bash
grep -n "TargetFramework\|UseWindowsForms" src/RigToggle.App/RigToggle.App.csproj
# net10.0-windows, UseWindowsForms=true — confirmed present, no change needed
```

## Package Legitimacy Audit

Not applicable — this phase installs no external package (npm/PyPI/NuGet/cargo). `TableLayoutPanel`, `FlowLayoutPanel`, and `Panel` all ship in the WinForms desktop SDK already referenced by `RigToggle.App.csproj` (`UseWindowsForms=true`, `net10.0-windows`). Skip the slopcheck/registry-verification gate for this phase; no `## Package Legitimacy Audit` table is produced because there is nothing to audit.

## Architecture Patterns

### System Architecture Diagram

```
SettingsForm (AutoSize=true, AutoSizeMode=GrowAndShrink, FormBorderStyle=Sizable, MaximizeBox=false, MinimizeBox=false)
│
└── tlpRoot : TableLayoutPanel  (Dock=None or Fill-in-Form; AutoSize=true, AutoSizeMode=GrowAndShrink)
    │  RowStyles: [0]=AutoSize (mode-columns row), [1]=AutoSize (shared section row), [2]=AutoSize (Save/Discard button row)
    │  ColumnStyles: [0]=Percent 100 (single outer column — the row-based structure carries the real layout)
    │
    ├── Row 0: tlpModeColumns : TableLayoutPanel  (Dock=Fill in its cell; AutoSize=true)
    │   │  ColumnStyles: [0]=Percent 50, [1]=Percent 50
    │   │  RowStyles: [0]=AutoSize
    │   │
    │   ├── Col 0: pnlMonitorNormal (existing Panel, BorderStyle.FixedSingle, THEME-05 wrapper)
    │   │   │       Dock=Fill in its TableLayoutPanel cell
    │   │   ├── lblMonitorNormalCaption ("Normal Mode")
    │   │   ├── lblMonitorNormalExplain
    │   │   ├── dgvMonitorsNormal (Dock=Fill inside pnlMonitorNormal, AutoSizeColumnsMode already set per-column)
    │   │   ├── lblMonitorNormalWarning
    │   │   ├── lblAudioNormalCaption ("Normal:")   ← MOVED from pnlAudioDevices (D-01 split)
    │   │   ├── cboAudioNormal                       ← MOVED from pnlAudioDevices (D-01 split)
    │   │   └── lblAudioNormalWarning                ← MOVED from pnlAudioDevices (D-01 split)
    │   │
    │   └── Col 1: pnlMonitor (existing Panel — "Rig Mode", mirror of Col 0)
    │       ├── lblMonitorCaption ("Rig Mode")
    │       ├── lblMonitorExplain
    │       ├── dgvMonitors (Dock=Fill inside pnlMonitor)
    │       ├── lblMonitorWarning
    │       ├── lblAudioRigCaption ("Rig:")          ← MOVED from pnlAudioDevices (D-01 split)
    │       ├── cboAudioRig                          ← MOVED from pnlAudioDevices (D-01 split)
    │       └── lblAudioRigWarning                   ← MOVED from pnlAudioDevices (D-01 split)
    │
    ├── Row 1: pnlSharedSection (Panel, THEME-05 wrapper, Dock=Fill)
    │   └── flpShared : FlowLayoutPanel (Dock=Fill; FlowDirection=TopDown; WrapContents=false; AutoSize=true, AutoSizeMode=GrowAndShrink)
    │       ├── pnlAppPath (existing Panel — Target App caption + txtAppPath/btnBrowse/btnClearAppPath/lblAppWarning, AllowDrop wiring UNCHANGED)
    │       ├── [hotkey row: lblHotkeyCaption + txtHotkey side by side — nested small TableLayoutPanel or Panel, per Code Examples]
    │       ├── lblHotkeyWarning
    │       ├── chkEnableDebugLogging
    │       ├── chkCloseMinimizesToTray
    │       ├── chkMinimizeToTray
    │       ├── chkStartWithWindows
    │       ├── lblAutostartWarning
    │       └── pnlThemeReserved  ← D-04: empty/reserved Panel or a labeled placeholder, Phase 23 fills this in
    │
    └── Row 2: pnlButtons (small TableLayoutPanel or FlowLayoutPanel, FlowDirection=RightToLeft or 2-column Percent table)
        ├── btnDiscardChanges
        └── btnSaveSettings
```

Data flow for the primary use case (user opens Settings, edits a value, saves): `SettingsForm_Load` populates controls exactly as today (grid population, audio picker population, app-path field, checkbox states) — **entirely unchanged by this phase**, since D-03 constrains the change to `Location`/`Size`/`Dock`/`Anchor`/container-type only. The only new data-flow-adjacent concern is that `PopulateAudioPickers()` and any code that currently assumes `cboAudioNormal`/`cboAudioRig` are siblings inside one `pnlAudioDevices` panel must be checked for container-relative logic (e.g., `.Parent` references) — see Common Pitfalls.

### Recommended Project Structure

No new files — this phase is Designer.cs-centric per CONTEXT.md's own framing (`SettingsForm.Designer.cs`, single file, ~677 lines today, will grow somewhat from the added `TableLayoutPanel`/`FlowLayoutPanel` declarations but should not need a code-behind split).

```
src/RigToggle.App/
├── SettingsForm.Designer.cs   # This phase's entire surface area — container-type/Location/Size/Dock/Anchor changes only
├── SettingsForm.cs            # Untouched except possibly minor .Parent-reference audits (see Pitfalls)
└── ThemeApplier.cs            # Untouched — container-agnostic by design, verified
```

### Pattern 1: Percent-split nested TableLayoutPanel for the two mode columns

**What:** A 2-column, 1-row `TableLayoutPanel` with both `ColumnStyles` set to `SizeType.Percent, 50F`, each cell hosting one mode's existing `Panel` wrapper (`pnlMonitorNormal`, `pnlMonitor`) with `Dock = DockStyle.Fill`.
**When to use:** Whenever two structurally-identical sections need an even, resize-responsive side-by-side split — exactly D-01's Normal/Rig requirement.
**Example:**
```csharp
// Source: pattern derived from official TableLayoutPanel.ColumnStyles docs
// (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.tablelayoutpanel.columnstyles)
this.tlpModeColumns = new TableLayoutPanel();
this.tlpModeColumns.ColumnCount = 2;
this.tlpModeColumns.RowCount = 1;
this.tlpModeColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
this.tlpModeColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
this.tlpModeColumns.RowStyles.Add(new RowStyle(SizeType.AutoSize));
this.tlpModeColumns.AutoSize = true;
this.tlpModeColumns.AutoSizeMode = AutoSizeMode.GrowAndShrink;
this.tlpModeColumns.Dock = DockStyle.Fill;

this.pnlMonitorNormal.Dock = DockStyle.Fill;
this.pnlMonitor.Dock = DockStyle.Fill;
this.tlpModeColumns.Controls.Add(this.pnlMonitorNormal, 0, 0);
this.tlpModeColumns.Controls.Add(this.pnlMonitor, 1, 0);
```

### Pattern 2: FlowLayoutPanel top-down stack for the shared section

**What:** `FlowLayoutPanel` with `FlowDirection.TopDown`, `WrapContents = false`, `AutoSize = true`, `AutoSizeMode = GrowAndShrink`, each shared-section item added as a full-width child with `Anchor = AnchorStyles.Left | AnchorStyles.Right` (not `Dock`, per the FlowLayoutPanel docking limitation below) so it stretches to the flow panel's implied-column width.
**When to use:** D-02's shared, non-mode-specific section — app path row, hotkey row, checkboxes, Phase 23's reserved slot — a pure vertical stack where each item's width should track the container, not a fixed pixel value.
**Example:**
```csharp
// Source: pattern derived from official FlowLayoutPanel docking/anchoring walkthrough
// (https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-anchor-and-dock-child-controls-in-a-flowlayoutpanel-control)
// FlowLayoutPanel does NOT support Dock on its children the way TableLayoutPanel does —
// "the FlowLayoutPanel calculates the width of an implied column from the widest child
// control, and other controls with Anchor set are stretched to fit this implied column."
// Use Anchor, not Dock, for full-width children inside a FlowLayoutPanel.
this.flpShared = new FlowLayoutPanel();
this.flpShared.FlowDirection = FlowDirection.TopDown;
this.flpShared.WrapContents = false;
this.flpShared.AutoSize = true;
this.flpShared.AutoSizeMode = AutoSizeMode.GrowAndShrink;
this.flpShared.Dock = DockStyle.Fill;

this.pnlAppPath.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
this.flpShared.Controls.Add(this.pnlAppPath);
// ...same Anchor pattern for the hotkey row container, each checkbox, and pnlThemeReserved
```

### Pattern 3: Form-level content-driven sizing (D-05)

**What:** `Form.AutoSize = true` + `Form.AutoSizeMode = AutoSizeMode.GrowAndShrink`, with the outer `TableLayoutPanel` also `AutoSize = true` / `GrowAndShrink`, and NO explicit `ClientSize` assignment in `InitializeComponent()` (remove the current `this.ClientSize = new Size(828, 768)` line entirely — letting AutoSize compute it).
**When to use:** Exactly D-05's requirement — content-driven sizing, no fixed target dimensions.
**Example:**
```csharp
// Source: pattern derived from official Form.AutoSize / Form.AutoSizeMode docs
// (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.autosizemode)
// "The form resizes according to the setting of AutoSizeMode. Set AutoSize=true and
// AutoSizeMode=GrowAndShrink. Default AutoSizeMode is GrowOnly — must be set explicitly
// to GrowAndShrink for a form whose content-driven size should also be able to shrink."
this.AutoSize = true;
this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
// Do NOT set this.ClientSize explicitly — content (tlpRoot) drives it.
this.Controls.Add(this.tlpRoot);
```
**Caveat (needs planning attention, not just a code pattern):** Microsoft's own docs state that "when using AutoSize, the MinimumSize and MaximumSize properties are respected, but the current value of the Size property is ignored." Combined with D-06's `FormBorderStyle.Sizable`, this means: AutoSize determines the form's *initial* size from content, but once the user drags an edge to resize, `AutoSize` does not fight the user back to the content-computed size on every resize — WinForms only re-runs AutoSize layout when a child control's own size/visibility changes (e.g., a warning label becoming visible), not on every user-driven resize event. This is the expected, desired behavior (user can freely resize larger or smaller after the initial content-driven size), but should be explicitly verified on the rig — see DPI/Rig-Verification Checklist.

### Anti-Patterns to Avoid

- **Setting `Dock = DockStyle.Fill` on a `FlowLayoutPanel` child expecting full-width behavior:** `FlowLayoutPanel` does not support meaningful `Dock` on children — use `Anchor = Left | Right` instead (confirmed via official docs, Pattern 2 above).
- **Mixing `Absolute` column/row sizes on any row/column that contains a `DataGridView` with `AutoSizeColumnsMode.Fill`:** An `Absolute` cell size defeats the purpose of `Percent`/`AutoSize` flexing and reintroduces exactly the fixed-pixel brittleness this migration is meant to remove — use `Percent` (for the 2 mode columns) or `AutoSize`/`Fill` for everything else.
- **Re-tabbing into a wizard/multi-tab flow:** Explicitly called out in CONTEXT.md as out of scope (FEATURES.md anti-pattern) — this stays a single-page form; do not reach for `TabControl` as a "cleaner" way to separate Normal/Rig.
- **Setting `MinimizeBox = true` because `FormBorderStyle` is now `Sizable`:** See Pitfall 3 below — a documented functional bug for `ShowDialog()` + `ShowInTaskbar=false` dialogs, not a style choice.
- **Combining overlapping shapes in `OnPaint` for any new visual flourish added during this migration:** Not directly this phase's concern (no new owner-drawn control), but if any custom drawing is added to a section-divider or the reserved Phase 23 slot, reuse the stroke-then-fill compositing fix from Phase 13 (`PITFALLS.md` Pitfall 2) rather than combining shapes into one `GraphicsPath`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| Even 50/50 column split that stays even on resize | Manual `Resize` event handler recomputing `Width / 2` for each panel | `TableLayoutPanel.ColumnStyles` with two `Percent, 50F` entries | Built-in, DPI-scaling-aware (participates in `AutoScaleMode.Font` the same as any other control bounds), zero custom event-wiring code to maintain |
| "Full width" children inside a vertical stack | Manual per-control `Width = flpShared.ClientSize.Width` assignment on every layout pass | `Anchor = AnchorStyles.Left \| AnchorStyles.Right` inside a `FlowLayoutPanel` | Documented, standard mechanism; avoids a `Layout`/`Resize` event handler that must run on every parent resize and risks lagging one frame behind |
| Content-driven form sizing | Manual `Form.Resize`/`Load` handler that measures children and sets `ClientSize` | `Form.AutoSize = true` + `AutoSizeMode = GrowAndShrink` | Built-in layout engine handles this correctly including DPI-scale-dependent measurements; a hand-rolled measurement pass would need to duplicate `AutoScaleMode.Font`'s own scaling math to get the same answer |

**Key insight:** Every genuine sizing/positioning need in this phase (even split, full-width stacking, content-driven form size) has a built-in `TableLayoutPanel`/`FlowLayoutPanel`/`Form.AutoSize` mechanism — the entire point of this migration (per D-03) is to replace hand-rolled pixel math with these mechanisms, so introducing a *new* hand-rolled measurement/resize handler anywhere in this phase would directly contradict the phase's own purpose.

## Common Pitfalls

### Pitfall 1: DataGridView `AutoSizeColumnsMode.Fill` inside a new `TableLayoutPanel`/`Panel` cell at non-100% Windows scale — UNVERIFIED in this environment

**What goes wrong:** `dgvMonitors`/`dgvMonitorsNormal` already use `AutoSizeColumnsMode.Fill` on their name column (`colMonitorName`/`colMonitorNameNormal`, `AutoSizeMode = Fill`) with two fixed-width (`66px`) checkbox columns. Fill-mode column width is computed from the grid's own client-area width at the moment layout runs (confirmed via official Microsoft Learn docs: "the width available for fill mode is determined by subtracting the widths of all other columns from the width of the client area of the control"). This computation happens correctly regardless of container type in principle, but this codebase's own PITFALLS.md (Pitfall 9, written directly from reading this exact `Designer.cs`) flags that no owner-drawn or auto-sizing container interaction has ever been exercised in this app before, under `AutoScaleMode.Font`, at a non-100% Windows display scale.

**Why it happens:** `AutoScaleMode.Font` rescales `Control.Bounds` on standard controls as the effective DPI/text-scale changes, but the *order* in which nested `TableLayoutPanel`/`Panel`/`DataGridView` layout passes run relative to that rescaling is not something this project has tested. A DataGridView computing its Fill-column widths against a stale (pre-rescale) client width, then not being told to recompute after the rescale settles, is a plausible (not confirmed) failure mode.

**How to avoid:** Keep the grids' own column configuration exactly as-is (already correct per official docs' Fill-mode + `FillWeight` guidance) — do not touch `colMonitorName`'s `AutoSizeMode` or add new `FillWeight` values. The only change is the *container*: `pnlMonitor`/`pnlMonitorNormal` (already `Panel`, unchanged type) becomes a `Dock=Fill` child of the new `tlpModeColumns` `TableLayoutPanel` cell, and `dgvMonitors`/`dgvMonitorsNormal` keep their existing `Dock=Fill`-inside-their-Panel relationship (already true today per the Designer.cs read — `dgvMonitors.Location`/`Size` are currently fixed pixels *inside* `pnlMonitor`, not `Dock=Fill`; this phase should consider switching these to `Dock=Fill` explicitly as part of removing hardcoded pixel math, which is a **new** behavior this phase introduces, not a preserved one — flag this explicitly for the rig check).

**Warning signs (rig-verify, not unit test):** At 125% and 150% Windows display scale, open Settings and check: are the "Off"/"On" checkbox columns still fully visible (not clipped) and still narrow/fixed-looking, is the "Monitor" name column still filling remaining space without overflowing the grid's visible bounds, and does the grid's own internal horizontal scrollbar appear (it should never appear — if it does, Fill-mode math broke at that scale).

**Phase to address:** This phase — must be an explicit rig checkpoint, not inferred from a 100%-scale design-time look, consistent with `PITFALLS.md` Pitfall 9's own stated verification requirement.

---

### Pitfall 2: `Form.AutoSize=true` computing an initial size that does not match `MinimumSize`/`MaximumSize` expectations, or fighting user resize

**What goes wrong:** Per official docs, "when using AutoSize, the MinimumSize and MaximumSize properties are respected, but the current value of the Size property is ignored." If this phase's `Designer.cs` migration leaves a stale `this.MinimumSize` or `this.MaximumSize` assignment from before the migration (none currently set, per direct read of the existing Designer.cs — only `ClientSize` is set today), or if a future edit adds one without checking AutoSize's interaction, the form could refuse to shrink below an unintended floor or grow past an unintended ceiling, silently overriding D-05's "content-driven, no fixed target" intent.

**Why it happens:** `AutoSize`'s docs explicitly call out that `MinimumSize`/`MaximumSize` are the two properties that still constrain it — an easy detail to miss since the whole point of enabling AutoSize is "stop thinking about explicit sizes."

**How to avoid:** Do not add `MinimumSize`/`MaximumSize` assignments to `SettingsForm` unless there's a specific, deliberate reason (e.g., preventing the two mode-grids from becoming unusably narrow on manual resize) — if one is added, document why, since it directly interacts with AutoSize's documented behavior.

**Warning signs:** Form opens at an unexpectedly large or small size that doesn't match its actual content; dragging an edge smaller than expected snaps back or refuses past a point with no visual explanation.

**Phase to address:** This phase, at design/build time (this one is checkable in the design surface / by inspecting the generated `Designer.cs`, not exclusively rig-only) — plus a rig confirmation that the resulting default size looks reasonable (D-05 has no fixed target, so "reasonable" is a judgment call, not a pixel match).

---

### Pitfall 3: `MinimizeBox = true` on a `ShowDialog()` + `ShowInTaskbar=false` window is a documented functional bug, not a style choice

**What goes wrong:** `SettingsForm` is opened via `ShowDialog()` (confirmed — `MainForm`'s `OpenSettingsDialog`-style call site, and `SettingsForm.cs`'s own `AcceptButton`/`CancelButton` wiring assumes modal dialog semantics) with `ShowInTaskbar = false` (unchanged this phase, confirmed in current `Designer.cs` line 583). If `MinimizeBox` is set to `true` (e.g., because `Sizable` "looks like it should have one"), minimizing the window removes it from view with no taskbar entry to restore it from — some documented reports describe `ShowDialog()` actually closing the dialog outright when minimized in this exact configuration (WinForms treats a minimized modal-with-no-taskbar-presence as unreachable and reclaims it). Either outcome (stuck-invisible or unexpectedly-closed) is a functional regression, not a cosmetic one.

**Why it happens:** `FormBorderStyle.Sizable` is the *default* style most Windows dialogs use, and most `Sizable` windows conventionally do have a minimize box — so "match the Sizable convention" is a reasonable-sounding but wrong instinct here specifically because of the `ShowInTaskbar=false` combination, which is easy to forget is still in effect after changing `FormBorderStyle`.

**How to avoid:** Keep `MinimizeBox = false` (matching today, per CONTEXT.md's default-unless-inconsistent framing). This is the recommended resolution for the "Claude's Discretion" MinimizeBox question — not a style call, a functional-correctness one.

**Warning signs (rig-verify, not unit test):** Open Settings, click the minimize button (if present) or press the minimize keyboard shortcut — confirm the window either has no minimize box at all (`MinimizeBox=false`, expected) or, if a planner chooses to override this finding and enables it anyway, confirm the window is actually restorable afterward without restarting the app.

**Phase to address:** This phase — low-cost to get right at design time (`MinimizeBox` stays `false`, zero new code), but worth an explicit rig confirmation if any future planner reconsiders this default.

---

### Pitfall 4: `.Parent`-relative code in `SettingsForm.cs` silently breaking after `pnlAudioDevices` is split apart (D-01)

**What goes wrong:** D-01 splits `pnlAudioDevices` (today's single panel holding both `cboAudioNormal` and `cboAudioRig`) so each combo moves into its own mode's column panel (`pnlMonitorNormal`/`pnlMonitor`). If any code in `SettingsForm.cs` (event handlers, particularly `AppPath_DragEnter`/`AppPath_DragDrop`, or the audio-picker population/validation logic) references a control's `.Parent`, walks up from a control to find a sibling, or assumes `cboAudioNormal.Parent == cboAudioRig.Parent`, that assumption breaks silently (no compile error — `.Parent` is always non-null once added to *any* container) once the two combos have different parents.

**Why it happens:** This is exactly the kind of container-relative logic that's easy to introduce without noticing, and D-03's "only `Location`/`Size`/`Dock`/`Anchor`/container-type changes, not what each control does or binds to" framing could lull a planner into treating this as a zero-logic-risk change when it is *usually* zero-logic-risk but not guaranteed to be.

**How to avoid:** Grep `SettingsForm.cs` for `.Parent`, `.Controls[`, and any use of `pnlAudioDevices` by name (confirmed via this research's own direct read: `pnlAudioDevices` is referenced only in `Designer.cs` today, not in `SettingsForm.cs`'s code-behind logic — so this specific risk is LOW-confidence-but-worth-a-final-grep, not a known landmine) before finalizing the split, as a cheap verification step.

**Warning signs:** Compile succeeds but a runtime `NullReferenceException` or a silently-wrong audio picker validation/warning-label placement appears only after the D-01 split, not before.

**Phase to address:** This phase, as a static grep check before/during implementation — cheap, does not need rig hardware.

---

### Pitfall 5: Tab order drifting from the current visual reading order after container-type migration

**What goes wrong:** WinForms `TabIndex` is normally auto-assigned in `Controls.Add()` call order per container, and nested containers each have their own local Tab sequence that composes into the overall Tab order based on container nesting order. Reparenting controls into new nested `TableLayoutPanel`/`FlowLayoutPanel` containers (as this phase does extensively) can reorder the effective Tab sequence even if no `TabIndex` is explicitly set, since the *order controls are added to their new parent* now differs from today's flat `Controls.Add()` sequence in `InitializeComponent()`.

**Why it happens:** Today's Tab order is implicitly defined by the single flat `this.Controls.Add(...)` sequence at the bottom of `InitializeComponent()` (lines 588-601). After migration, the equivalent sequence is spread across multiple nested `.Controls.Add()` calls (each mode-column panel, the shared FlowLayoutPanel, etc.) — the *visual* order (Normal column, then Rig column, then shared section, top to bottom) needs to be deliberately reproduced in the *add* order at each nesting level, or Tab will jump unpredictably (e.g., from the Normal grid straight to the Save button, skipping the Rig column).

**How to avoid:** When adding controls to each new container, add them in the same left-to-right, top-to-bottom order they'll visually appear, mirroring today's `InitializeComponent()` add-order intent. Since this project's own established pattern favors explicit correctness over implicit ordering, consider setting `TabIndex` explicitly on at least the top-level containers (`tlpModeColumns`, `pnlMonitorNormal`, `pnlMonitor`, `pnlSharedSection`) if the implicit order proves wrong on a manual Tab-through check.

**Warning signs:** Manual Tab-key walkthrough (buildable/testable conceptually, but only truly confirmable on Windows since this environment has no GUI) jumps in a visually illogical order.

**Phase to address:** This phase — flag as a manual (not necessarily rig-only, though easiest confirmed on the rig alongside the DPI checks) verification step.

## DPI / Rig-Verification Checklist

Modeled on the precedent in `21-CONTEXT.md`/`21-03-PLAN.md` (which built a registry/DWM verification checklist for that phase's own unverifiable-in-this-environment concern — accent-color byte order and live-notification reliability). This phase's equivalent unverifiable concern is **layout correctness at non-100% Windows display scale** (PITFALLS.md Pitfall 9). This build environment has no Windows GUI, no DWM, and cannot exercise `AutoScaleMode.Font` rescaling at all — every item below can only be confirmed on real Windows 11 hardware.

**Setup:** Publish the app (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`, matching prior phases' rig-publish workflow) and run it on the rig machine.

1. **100% scale baseline.** At default (100%) Windows display scale, open Settings. Confirm no overlapping or clipped controls anywhere (SETTINGS-01) and that the Normal/Rig columns read as evenly split and visually balanced (D-01/discretion item). Screenshot for later comparison.
2. **125% scale — grid columns.** Set Windows display scale to 125% (Settings > System > Display > Scale), relaunch the app (not just reflow the existing window — a fresh launch is needed to pick up the new DPI at process start). Open Settings. For both `dgvMonitorsNormal` and `dgvMonitors`: confirm the "Monitor" name column still fills available space without overflowing the grid bounds, confirm the "Off"/"On" checkbox columns are still fully visible (not clipped at the grid's right edge), and confirm no horizontal scrollbar appears on either grid.
3. **125% scale — overlap/crowding check (SETTINGS-01).** At 125%, re-check every section (Normal column, Rig column, shared section) for overlapping or crowded controls — text scaling at 125% can cause labels to need more width/height than at 100%; confirm no label text is clipped or overlapping its neighbor.
4. **125% scale — button text.** Confirm `btnBrowse` ("Browse…"), `btnClearAppPath` ("Clear"), `btnSaveSettings` ("Save Settings"), `btnDiscardChanges` ("Discard Changes") all show their full text without truncation or ellipsis at 125%.
5. **150% scale — repeat checks 2-4.** Same four checks at 150% scale — this is the higher-risk scale factor since layout slack that survives 125% may not survive 150%.
6. **150% scale — form-level AutoSize sanity.** Confirm the form's overall initial size (on fresh open, before any manual resize) still looks like a coherent, non-clipped window at 150% — not just individual controls, but the whole form's proportions.
7. **Manual resize interaction (D-06).** At 100% scale, drag the form's right and bottom edges smaller, then larger. Confirm: (a) the window can actually be resized (Sizable is in effect), (b) no maximize button is present (`MaximizeBox=false`), (c) resizing smaller doesn't visually break the layout (controls should either reflow via the Percent/AutoSize containers or, if a practical minimum is reached, stop shrinking further rather than clipping/overlapping), (d) resizing larger doesn't leave a large dead empty gap that looks unintentional.
8. **Manual resize at 125%/150%.** Repeat check 7's resize interaction at 125% and 150% scale — resize behavior interacting with DPI scaling is exactly the kind of compound risk that's easy to miss if only checked at one scale or the other independently.
9. **MinimizeBox absence confirmed.** At any scale, confirm there is no minimize button in the title bar (per Pitfall 3's recommendation to keep `MinimizeBox=false`). If a planner overrode this recommendation, instead confirm minimizing and restoring the window actually works without losing the window or closing the dialog.
10. **Tab order walkthrough.** With the mouse untouched, press Tab repeatedly from the first control and confirm the focus order visits Normal column controls, then Rig column controls (or an otherwise clearly intentional order), then the shared section, then Save/Discard — in a sequence that reads as deliberate, not scrambled (Pitfall 5).
11. **Theming still reaches every relocated control.** Flip Windows Light↔Dark while the Settings dialog is open (live, not via restart) and confirm every relocated control (both audio combos in their new column locations, both grids, all buttons) still re-themes correctly — this validates the "theming is container-agnostic" finding from this research is actually true in practice, not just in the source-reading analysis.
12. **Report PASS/FAIL per item**, following this project's established rig-verification discipline (Phase 21 precedent) — do not mark this phase done on an inferred "should work" basis for any of the above; DPI/display-scale behavior is exactly the category of claim this project's own history (Phase 12/13, `Application.SetColorMode`, `DWMWA_USE_IMMERSIVE_DARK_MODE`) has previously found to be wrong despite being training-data-plausible.

## Code Examples

### Existing dgvMonitors column configuration (unchanged this phase, confirmed correct per official docs)

```csharp
// Source: src/RigToggle.App/SettingsForm.Designer.cs (existing code, lines 157-184) —
// already follows the official Fill-mode pattern correctly; do not modify.
this.dgvMonitors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
    this.colMonitorName,
    this.colDisable,
    this.colEnable});

this.colMonitorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
this.colDisable.Width = 66;   // fixed-width, non-Fill column — correct per official docs' "fixed-width column" pattern
this.colEnable.Width = 66;
```

### Recommended: switching dgvMonitors from fixed Location/Size to Dock=Fill inside its Panel

```csharp
// NEW this phase — replaces the current hardcoded
// dgvMonitors.Location = new Point(12, 80); dgvMonitors.Size = new Size(372, 120);
// This is a genuinely new behavior (today's grid uses fixed pixel bounds even
// though it already sits inside a Panel) — flag explicitly for the rig checklist
// (Pitfall 1) since it changes how the grid's Fill-mode column math resolves its
// available width at each layout pass.
this.dgvMonitors.Dock = System.Windows.Forms.DockStyle.Fill;
// pnlMonitor's own Padding (not Location/Size math) now controls the grid's inset
// from the panel's border — e.g. this.pnlMonitor.Padding = new Padding(12, 60, 12, 12)
// to preserve room above the grid for lblMonitorCaption/lblMonitorExplain, which
// should switch from Location-based positioning to Dock=Top stacking above the grid.
```

## State of the Art

| Old Approach (this codebase, today) | New Approach (this phase) | When Changed | Impact |
|--------------------------------------|------------------------------|---------------|--------|
| `Panel` + hardcoded `Location`/`Size` for every control, `SuspendLayout`/`ResumeLayout` around bulk changes | `TableLayoutPanel`/`FlowLayoutPanel` with `Percent`/`AutoSize` styles, `Dock`/`Anchor` for children | This phase (D-03) | Eliminates hand-picked pixel offsets as the source of overlap/crowding bugs; first use of these container types anywhere in this codebase — no prior-art fallback if something goes wrong, must be rig-verified fresh |
| Fixed `ClientSize = new Size(828, 768)` | `Form.AutoSize = true` + `AutoSizeMode.GrowAndShrink`, no explicit `ClientSize` | This phase (D-05) | Form now sizes to content; a future control addition/removal (e.g., Phase 23's radio group) will change the form's default size automatically, rather than requiring a manual `ClientSize` recalculation the way today's fixed dialog would |
| `FormBorderStyle.FixedDialog`, `MaximizeBox=false`, `MinimizeBox=false` | `FormBorderStyle.Sizable`, `MaximizeBox=false`, `MinimizeBox=false` (unchanged) | This phase (D-06) | User can now resize the window by dragging edges; no maximize; minimize stays absent for the functional reason in Pitfall 3, not merely unchanged-by-default |

**Deprecated/outdated:** None — `TableLayoutPanel`/`FlowLayoutPanel` have been stable, unchanged WinForms APIs since .NET Framework 2.0; nothing about this migration involves a deprecated or superseded API. The only "new" surface is this codebase's own first use of them, not the APIs themselves being new or changed.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | Switching `dgvMonitors`/`dgvMonitorsNormal` from fixed `Location`/`Size` to `Dock=Fill` is necessary/desirable for this migration (rather than keeping their current fixed pixel bounds inside the new `Panel`-in-`TableLayoutPanel`-cell structure) | Code Examples, Pitfall 1 | If planning instead keeps the grids at fixed pixel size inside a now-flexibly-sized panel, the grid could end up with dead space around it or, at a narrow AutoSize-computed panel width, get visually cramped — this is a planning judgment call, not a settled fact; flagged here as the recommended default, not a locked decision |
| A2 | The shared section is best implemented as a `FlowLayoutPanel` rather than a single-column `TableLayoutPanel` with one `AutoSize` row per item | Standard Stack (Alternatives Considered), Architecture Patterns | If `FlowLayoutPanel`'s Anchor-not-Dock behavior (Pattern 2) proves harder to get right in practice than expected, a single-column `TableLayoutPanel` is an equally valid fallback with no loss of capability — this is a discretionary implementation choice `CONTEXT.md` explicitly left open, not a research-verified requirement |
| A3 | `MinimizeBox` should stay `false` due to the `ShowDialog()` + `ShowInTaskbar=false` restore-ability bug | Pitfall 3 | MEDIUM confidence (WebSearch-sourced community reports of the exact failure mode, not an official Microsoft Learn statement of this specific interaction) — cross-checked against the general, well-documented `ShowInTaskbar` semantics but the specific "minimizing closes/strands a `ShowDialog()` window" claim is not from a primary Microsoft source; if wrong, worst case is a planner unnecessarily avoids enabling `MinimizeBox`, which has zero functional cost since `MinimizeBox=false` matches the pre-existing behavior anyway |
| A4 | No `.Parent`-relative or container-structure-dependent logic exists in `SettingsForm.cs` beyond what a targeted grep can catch before the D-01 audio-picker split | Pitfall 4 | LOW risk — confirmed by direct read that `pnlAudioDevices` is referenced only in `Designer.cs` today, but the full 1176-line `SettingsForm.cs` was not read in its entirety during this research pass, only its first ~120 lines and targeted grep hits; a planner should re-grep before finalizing the split as a cheap final check |

## Open Questions

1. **Should `dgvMonitors`/`dgvMonitorsNormal` switch to `Dock=Fill`, or keep fixed pixel bounds inside their now-flexible parent panels?**
   - What we know: The grids' own Fill-mode *column* math is already correct and documented; the question is only about the grid *control's own* bounds within its parent.
   - What's unclear: Whether keeping fixed pixel bounds (simpler diff, less new-behavior risk) or switching to `Dock=Fill` (more consistent with "the whole point of this migration is removing hardcoded pixel math," per D-03's spirit) is the better call for this phase specifically.
   - Recommendation: Lean toward `Dock=Fill` for consistency with D-03's intent, but treat this as a planning decision to make explicitly (not silently), and make it the single most heavily rig-verified layout element (Pitfall 1) given it's the most novel new-behavior surface this phase introduces.

2. **Exact visual boundary of the "reserved" Phase 23 slot (D-04) — an empty `Panel` with a code comment, or a labeled placeholder control?**
   - What we know: D-04 requires the space to exist and be easy for Phase 23's planner to find; Phase 22 must not build the radio group itself.
   - What's unclear: Whether a bare reserved row (comment-only) is sufficient or whether a visible-but-inert placeholder (e.g., a disabled label reading "Theme: (Phase 23)") better serves SETTINGS-01's "no crowded controls" criterion by making the reserved space's purpose legible during this phase's own rig verification (so a reviewer doesn't mistake unexplained empty space for a layout bug).
   - Recommendation: A code comment plus a named-but-empty `TableLayoutPanel` row/cell (e.g., `rowThemeReserved`) is sufficient and lower-risk — a visible placeholder control risks being mistaken for a real, half-finished feature. Leave this to planning discretion, consistent with CONTEXT.md's own framing.

## Environment Availability

Skipped — this phase's only "external dependency" is the .NET 10 SDK / WinForms desktop workload, already confirmed present and in use by every prior phase in this codebase (`net10.0-windows`, `UseWindowsForms=true` in `RigToggle.App.csproj`). No new external tool, service, or package is introduced. The one genuinely unavailable "dependency" — a Windows GUI environment to visually verify DPI-scale rendering — is not a tool-availability gap but the exact reason the DPI/Rig-Verification Checklist section above exists as a human-executed checkpoint.

## Validation Architecture

Skipped — `.planning/config.json` sets `workflow.nyquist_validation: false` explicitly.

## Security Domain

Skipped — no `security_enforcement` key is present in `.planning/config.json`'s `features` object, but this phase has no applicable ASVS surface regardless: it is a pure client-side WinForms layout/presentation change with no authentication, session, access-control, input-validation-of-untrusted-data, or cryptography surface. No new data enters or leaves the process as a result of this phase; `SettingsForm.cs`'s existing validation logic (hotkey conflicts, app-path existence, audio-device-id resolution) is unchanged. No ASVS category applies.

## Sources

### Primary (HIGH confidence)
- `src/RigToggle.App/SettingsForm.Designer.cs` — read directly (2026-08-12), confirmed exact current structure, `AutoScaleMode.Font`, `FormBorderStyle.FixedDialog`, `ClientSize=828×768`, zero `TableLayoutPanel`/`FlowLayoutPanel` usage, existing `DataGridViewAutoSizeColumnMode.Fill` configuration
- `src/RigToggle.App/SettingsForm.cs` (partial read, ~120 of 1176 lines + targeted grep) — confirmed `ShowDialog()`-modal usage pattern, `IsDarkTheme`/`OnThemeChanged`/`SettingsForm_Load` theming call sites, no `.Parent`-relative logic found in the sections read
- `src/RigToggle.App/ThemeApplier.cs` — read directly in full, confirmed theming is per-instance-field-targeted, never a recursive `Controls`-tree walk, never container-type-aware — directly answers this research's question 5
- `.planning/research/PITFALLS.md` — read directly, Pitfall 9 is the authoritative prior statement of this phase's core DPI risk, grounded in the same `Designer.cs` this research also read
- `.planning/phases/21-accent-color-reading-live-update/21-03-PLAN.md` — read directly, source of the rig-verification-checklist structural precedent this research's own checklist follows
- https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-set-the-sizing-modes-of-the-windows-forms-datagridview-control — fetched directly, confirmed official `AutoSizeColumnsMode.Fill` + `FillWeight` + `MinimumWidth` guidance, confirmed Fill-mode width is computed from the grid's own client area
- https://github.com/dotnet/docs-desktop/blob/main/dotnet-desktop-guide/winforms/controls/autosize-behavior-in-the-tablelayoutpanel-control.md — fetched directly, confirmed `AutoSize`/`Percent`/`AutoSize`-style-row allocation order and the "no column/row clips its contents" guarantee when the container itself is AutoSize

### Secondary (MEDIUM confidence)
- WebSearch: "WinForms Form AutoSize true FormBorderStyle Sizable user resize conflict AutoSizeMode GrowAndShrink" — confirmed `AutoSizeMode.GrowAndShrink` requirement (default is `GrowOnly`) and the `MinimumSize`/`MaximumSize`-respected-but-`Size`-ignored behavior, cross-referenced against `learn.microsoft.com/.../system.windows.forms.form.autosizemode` result
- WebSearch: "WinForms dialog ShowInTaskbar false MinimizeBox true minimized window no way to restore" — source of Pitfall 3's `ShowDialog()`+`ShowInTaskbar=false`+`MinimizeBox=true` failure-mode claim; community-sourced, not an official Microsoft Learn statement of this specific combination, flagged MEDIUM and logged in Assumptions Log (A3)
- WebSearch: "WinForms TableLayoutPanel nested FlowLayoutPanel DataGridView Dock Fill AutoSize form content-driven sizing" — source of the `FlowLayoutPanel` Anchor-not-Dock guidance (Pattern 2), cross-referenced against the official how-to-anchor-and-dock-child-controls-in-a-flowlayoutpanel-control doc title found in the same search
- WebSearch: "TableLayoutPanel Percent AutoSize Absolute column style best practice two column layout WinForms" — source of the Absolute→AutoSize→Percent allocation-order summary, corroborated by the official `docs-desktop` autosize-behavior doc fetched directly above

### Tertiary (LOW confidence)
- None retained as unverified — every WebSearch finding used in this document was either corroborated by a directly-fetched official Microsoft Learn/docs-desktop source, or explicitly logged in the Assumptions Log with its confidence level stated (A3).

## Metadata

**Confidence breakdown:**
- Standard stack (TableLayoutPanel/FlowLayoutPanel container mechanics): HIGH — official Microsoft Learn/docs-desktop sources fetched directly, no ambiguity in the container APIs themselves
- Architecture (mode-column + shared-section composition): HIGH for the composition pattern itself (standard, documented WinForms idiom); MEDIUM for the exact Dock=Fill-on-grids decision (Open Question 1, left to planning)
- Pitfalls (DPI/scale-specific behavior): MEDIUM — grounded in this codebase's own direct-read Designer.cs facts and official docs for the *mechanisms* involved (Fill-mode math, AutoSize allocation order), but the actual *composed* behavior at 125%/150% scale is explicitly unverifiable in this environment and treated as LOW-confidence-until-rig-tested throughout, consistent with this project's own established discipline (Phase 12/13/21 precedent)

**Research date:** 2026-08-12
**Valid until:** 2026-09-11 (30 days — WinForms container APIs are stable/non-fast-moving; the only genuinely time-sensitive element, the rig-verification outcome, has no shelf life since it must be re-confirmed against this phase's actual final implementation regardless of research age)
