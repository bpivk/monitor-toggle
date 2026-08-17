# Phase 22: SettingsForm Layout Pass - Pattern Map

**Mapped:** 2026-08-12
**Files analyzed:** 2 (1 primary modified file, decomposed into 5 logical sections; 1 secondary file with no expected code changes but a verified constraint)
**Analogs found:** 5 / 5 (self-analog for 4 sections; one cross-file analog for the new FlowLayoutPanel construct)

## Context Note

This phase has an unusual shape for pattern-mapping: there is exactly **one** physical file in scope for structural changes — `src/RigToggle.App/SettingsForm.Designer.cs` — being migrated in place from `Panel` + hardcoded `Location`/`Size` to `TableLayoutPanel`/`FlowLayoutPanel`. There is no sibling "SettingsForm2" to copy from. Per RESEARCH.md and CONTEXT.md, the best analogs are therefore:

1. **The file's own current content** — every existing block (`pnlMonitor`, `pnlMonitorNormal`, `pnlAudioDevices`, `pnlAppPath`, the checkbox/hotkey stack, the button row) is the ground truth for *what each control does and currently looks like*; the migration preserves this content and only changes container/positioning mechanics.
2. **`MainForm.Designer.cs`'s `tileStrip`** — the only pre-existing `FlowLayoutPanel` usage anywhere in this codebase, plus a directly-relevant documented caution about `AutoSize` being unproven on this runtime (see Shared Patterns below).
3. **`ThemeApplier.cs` + `SettingsForm.cs`'s theming call sites** — confirms the theming pipeline is container-type-agnostic, so no analog work is needed there, only a "don't break this" constraint.

Below, the single physical file is decomposed into 5 logical sections (each destined for its own `TableLayoutPanel`/`FlowLayoutPanel` construct per RESEARCH.md's Architecture Patterns) so the planner can assign each section's concrete before/after excerpt independently.

## File Classification

| Logical Section (all within `SettingsForm.Designer.cs`) | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| Outer form container (`tlpRoot`, `Form.AutoSize`, `FormBorderStyle`) | config (Designer layout root) | request-response (dialog open/close, no data flow change) | `SettingsForm.Designer.cs` lines 574-586 (current `SettingsForm` property block) | exact (self, mechanics changing) |
| Mode columns (`tlpModeColumns` + `pnlMonitorNormal`/`pnlMonitor`, with `cboAudioNormal`/`cboAudioRig` moved in) | component (composite section container) | CRUD (grid/combo population + edit, unchanged) | `SettingsForm.Designer.cs` lines 118-360 (`pnlMonitor`, `pnlMonitorNormal`, `pnlAudioDevices` blocks — the exact controls being split/regrouped) | exact (self, same controls, new grouping) |
| Shared global section (`pnlSharedSection`/`flpShared`: app path, hotkey, debug checkbox, tray/autostart checkboxes, Phase 23 reserved slot) | component (composite section container) | CRUD (form field bind/save, unchanged) | `SettingsForm.Designer.cs` lines 362-530 (`pnlAppPath` block + checkbox/hotkey stack) | exact (self, same controls, new container type) |
| Shared section's *container type* specifically (`FlowLayoutPanel`, `TopDown`, `Anchor`-not-`Dock` children) | component | request-response (pure layout, no data flow) | `MainForm.Designer.cs` lines 60-147 (`tileStrip` — only existing `FlowLayoutPanel` in the codebase) | role-match (same container type, different content — monitor tiles vs. settings rows) |
| Button row (`btnSaveSettings`/`btnDiscardChanges`) | component | request-response (Save/Cancel dialog result, unchanged) | `SettingsForm.Designer.cs` lines 533-556 (current button block) | exact (self, same controls, new positioning) |
| `SettingsForm.cs` (code-behind) | controller (Load/Save/theming orchestration) | CRUD + event-driven (unchanged) | itself — **no code changes expected**, verified below | n/a (verification target, not a migration target) |

## Pattern Assignments

### Outer form container (`tlpRoot`, `Form.AutoSize`, `FormBorderStyle`)

**Analog:** `src/RigToggle.App/SettingsForm.Designer.cs` lines 574-616 (current `SettingsForm` property block + `Controls.Add` sequence)

**Current pattern to replace** (lines 574-601):
```csharp
this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
this.ClientSize = new System.Drawing.Size(828, 768);
this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
this.MaximizeBox = false;
this.MinimizeBox = false;
this.ShowInTaskbar = false;
this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
this.Text = "Rig Toggle — Settings";
this.Name = "SettingsForm";

this.Controls.Add(this.pnlMonitor);
this.Controls.Add(this.pnlMonitorNormal);
this.Controls.Add(this.pnlAudioDevices);
this.Controls.Add(this.pnlAppPath);
this.Controls.Add(this.chkEnableDebugLogging);
this.Controls.Add(this.lblHotkeyCaption);
this.Controls.Add(this.txtHotkey);
this.Controls.Add(this.lblHotkeyWarning);
this.Controls.Add(this.chkCloseMinimizesToTray);
this.Controls.Add(this.chkMinimizeToTray);
this.Controls.Add(this.chkStartWithWindows);
this.Controls.Add(this.lblAutostartWarning);
this.Controls.Add(this.btnSaveSettings);
this.Controls.Add(this.btnDiscardChanges);
```

**Fields to KEEP unchanged** (per D-05/D-06, verbatim from current file):
- `AutoScaleDimensions = new SizeF(7F, 15F)` and `AutoScaleMode = AutoScaleMode.Font` — untouched, this is the existing codebase-wide DPI mechanism (RESEARCH.md: "must work correctly under the existing mode — do not introduce `AutoScaleMode.Dpi`")
- `ShowInTaskbar = false` — untouched, and load-bearing for Pitfall 3 (`MinimizeBox` must stay `false` because of this)
- `StartPosition = CenterParent`, `Text`, `Name` — untouched

**Fields to CHANGE** (per RESEARCH.md Pattern 3 / D-05 / D-06):
```csharp
// REMOVE entirely: this.ClientSize = new System.Drawing.Size(828, 768);
// ADD:
this.AutoSize = true;
this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;   // was FixedDialog
this.MaximizeBox = false;   // unchanged
this.MinimizeBox = false;   // unchanged — Pitfall 3, NOT a style default, functional requirement
this.Controls.Add(this.tlpRoot);   // replaces the 14-line flat Controls.Add(...) sequence above
```

**Do-not-hand-roll constraint** (RESEARCH.md "Don't Hand-Roll"): do not add a `Form.Resize`/`Load` handler that measures children and sets `ClientSize` manually — `AutoSize=true` + `AutoSizeMode.GrowAndShrink` is the built-in mechanism and is the entire point of D-05.

**Caveat to carry into the plan** (RESEARCH.md Pitfall 2, Pattern 3): do not add `MinimumSize`/`MaximumSize` unless there's a specific documented reason — none exist today (verified: only `ClientSize` was set in the current file), and adding one silently overrides D-05's content-driven intent.

---

### Mode columns (`tlpModeColumns` + `pnlMonitorNormal`/`pnlMonitor`, audio combos moved in)

**Analog:** `src/RigToggle.App/SettingsForm.Designer.cs` lines 118-360 (existing `pnlMonitor`, `pnlMonitorNormal`, `pnlAudioDevices` blocks)

**THEME-05 flat-bordered-Panel-as-GroupBox pattern to preserve** (lines 118-125, applies identically to both mode panels):
```csharp
//
// pnlMonitor (THEME-05: flat bordered Panel replacing the grpMonitor GroupBox
// bevel -- GroupBox has no flat variant, SetColorMode cannot recolor its 3D
// border. Same Location/Size as the original GroupBox, zero layout drift.)
//
this.pnlMonitor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
this.pnlMonitor.Name = "pnlMonitor";
this.pnlMonitor.Controls.Add(this.lblMonitorCaption);
this.pnlMonitor.Controls.Add(this.lblMonitorExplain);
this.pnlMonitor.Controls.Add(this.dgvMonitors);
this.pnlMonitor.Controls.Add(this.lblMonitorWarning);
```
**Keep `BorderStyle.FixedSingle`** on every relocated section-container Panel (`pnlMonitorNormal`, `pnlMonitor`, and the new `pnlSharedSection`) — this is the established THEME-05 visual language (Phase 12), not something this phase should change; only `Location`/`Size`→`Dock`/`Anchor` changes.

**Grid column configuration to preserve verbatim (do not touch)** (lines 157-184, and mirrored 241-268 for the Normal grid):
```csharp
this.dgvMonitors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
this.colMonitorName,
this.colDisable,
this.colEnable});

this.colMonitorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
this.colDisable.Width = 66;   // fixed-width, non-Fill
this.colEnable.Width = 66;
```
RESEARCH.md Pitfall 1 is explicit: do not modify `AutoSizeMode`/`FillWeight` on these columns — only the *container* (grid's own `Location`/`Size` → `Dock=Fill`, per RESEARCH.md's Code Examples "Recommended" pattern) changes.

**Grid's own bounds — NEW behavior this phase introduces** (currently fixed pixels inside its Panel, lines 154-155):
```csharp
// CURRENT (fixed pixel bounds inside pnlMonitor):
this.dgvMonitors.Location = new System.Drawing.Point(12, 80);
this.dgvMonitors.Size = new System.Drawing.Size(372, 120);

// RECOMMENDED NEW (RESEARCH.md Code Examples, Open Question 1 resolution):
this.dgvMonitors.Dock = System.Windows.Forms.DockStyle.Fill;
// pnlMonitor.Padding now reserves room above the grid for the caption/explain
// labels, which switch from Location-based positioning to Dock=Top stacking:
// this.pnlMonitor.Padding = new Padding(12, 60, 12, 12);  (illustrative — exact
// inset should preserve today's visual spacing, not be recomputed from scratch)
```
Flag this specific change as the single most heavily rig-verified element (RESEARCH.md Pitfall 1) — it is a genuinely new behavior, not a preserved one.

**D-01 audio-picker split — exact controls moving, with their current pixel layout for reference** (lines 289-360, `pnlAudioDevices` block being split apart):
```csharp
// Normal's combo (currently a child of pnlAudioDevices, lines 313-335) —
// MOVES into pnlMonitorNormal:
this.lblAudioNormalCaption.Text = "Normal:";
this.cboAudioNormal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.cboAudioNormal.Name = "cboAudioNormal";
this.lblAudioNormalWarning.Visible = false;   // shown conditionally by SettingsForm.cs

// Rig's combo (currently a child of pnlAudioDevices, lines 338-360) —
// MOVES into pnlMonitor:
this.lblAudioRigCaption.Text = "Rig:";
this.cboAudioRig.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.cboAudioRig.Name = "cboAudioRig";
this.lblAudioRigWarning.Visible = false;
```
**`pnlAudioDevices` itself and `lblAudioDevicesCaption` are removed** — D-01 dissolves this panel entirely; its two children go to their respective mode panels, its "Audio Devices" caption label has no replacement (each mode panel's existing caption — "Rig Mode"/"Normal Mode" — already labels the section it now also contains audio for).

**Nested Percent-split container** (RESEARCH.md Pattern 1, no direct in-codebase analog — first use of `TableLayoutPanel` anywhere in this project, use exactly as researched):
```csharp
// Source: RESEARCH.md Pattern 1, derived from official TableLayoutPanel.ColumnStyles docs
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
this.tlpModeColumns.Controls.Add(this.pnlMonitorNormal, 0, 0);   // Normal = left, per D-01 user framing
this.tlpModeColumns.Controls.Add(this.pnlMonitor, 1, 0);         // Rig = right
```
Note user's own framing (CONTEXT.md Specific Ideas): "One side is for normal mode... second for rig mode" — Normal reads left-to-right first, so put Normal in column 0.

---

### Shared global section (`pnlSharedSection`/`flpShared`)

**Analog (content/controls):** `src/RigToggle.App/SettingsForm.Designer.cs` lines 362-530 (`pnlAppPath` block + checkbox/hotkey stack)
**Analog (container type):** `src/RigToggle.App/MainForm.Designer.cs` lines 60-147 (`tileStrip` FlowLayoutPanel — see next section)

**`pnlAppPath` critical wiring to preserve verbatim** (lines 362-379 — flagged in existing code comments as load-bearing, T-12-07):
```csharp
//
// pnlAppPath (THEME-05: flat bordered Panel replacing the grpAppPath GroupBox
// bevel. Same Location/Size as the original GroupBox. CRITICAL: AllowDrop and
// the AppPath_DragEnter/AppPath_DragDrop wiring move here from the old
// GroupBox verbatim -- must not be dropped (T-12-07).)
//
this.pnlAppPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
this.pnlAppPath.AllowDrop = true;
this.pnlAppPath.Controls.Add(this.lblAppPathCaption);
this.pnlAppPath.Controls.Add(this.txtAppPath);
this.pnlAppPath.Controls.Add(this.btnBrowse);
this.pnlAppPath.Controls.Add(this.btnClearAppPath);
this.pnlAppPath.Controls.Add(this.lblAppWarning);
this.pnlAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
this.pnlAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);
```
This drag-drop wiring must survive the container-type migration unmodified — moving `pnlAppPath` from a fixed `Location` child of the form into a `FlowLayoutPanel` child does not change its own internal `AllowDrop`/`DragEnter`/`DragDrop` wiring, only its parent and `Anchor`.

**Checkbox/hotkey stack to preserve (content), currently positioned by absolute Y-stacking** (lines 453-530):
```csharp
this.chkEnableDebugLogging.Text = "Enable debug logging (writes to %LOCALAPPDATA%\\RigToggle\\debug.log)";
this.lblHotkeyCaption.Text = "Hotkey:";
this.txtHotkey.ReadOnly = true;             // D-01/UI-SPEC load-bearing, keep verbatim
this.txtHotkey.TabStop = false;             // load-bearing, keep verbatim
this.txtHotkey.Cursor = System.Windows.Forms.Cursors.Hand;   // load-bearing, keep verbatim
this.chkCloseMinimizesToTray.Text = "Closing the window (X) minimizes to tray";
this.chkMinimizeToTray.Text = "Minimizing the window also sends it to tray";
this.chkStartWithWindows.Text = "Start with Windows";
```
The hotkey row (`lblHotkeyCaption` + `txtHotkey` side by side) needs its own small nested layout (a 2-column `TableLayoutPanel` or a `Panel`) inside the `FlowLayoutPanel` stack, per RESEARCH.md Architecture Patterns line 139 — this is the one sub-item in the shared section that isn't a pure single-control row.

**D-04 reserved Phase 23 slot** — add as the last (or a clearly-marked) item in the `flpShared` stack, per RESEARCH.md Open Question 2 recommendation: a named-but-empty row/panel with a code comment (not a visible placeholder control), e.g. `pnlThemeReserved` — a bare `Panel` with `Size = new Size(1, 1)` or similar, and a comment pointing to Phase 23.

---

### Shared section's container type (`FlowLayoutPanel`, `TopDown`, `Anchor`-not-`Dock`)

**Analog:** `src/RigToggle.App/MainForm.Designer.cs` lines 60-147 (`tileStrip`)

**What to copy — FlowLayoutPanel property set** (lines 138-146, adapted for `TopDown` instead of `tileStrip`'s `LeftToRight`):
```csharp
// tileStrip (existing, LeftToRight) — the shape to imitate, not the values:
this.tileStrip.Name = "tileStrip";
this.tileStrip.AutoSize = false;   // NOTE: tileStrip deliberately does NOT use AutoSize (see caution below)
this.tileStrip.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
this.tileStrip.WrapContents = true;
this.tileStrip.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
this.tileStrip.Padding = new System.Windows.Forms.Padding(0);
this.tileStrip.Margin = new System.Windows.Forms.Padding(0);
```
**Adapted for `flpShared`** (per RESEARCH.md Pattern 2 — this phase DOES use AutoSize, deliberately diverging from `tileStrip`'s choice, see caution immediately below):
```csharp
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

**IMPORTANT divergence to flag explicitly in the plan:** `MainForm.Designer.cs` lines 126-135 contain a direct code comment explaining that `tileStrip.AutoSize` was deliberately left `false` because *"no form in this codebase has ever used AutoSize and its layout-pass timing... is unproven on this runtime."* This phase's `flpShared` (and `tlpRoot`, and `tlpModeColumns`) is the **first place in the codebase that actually turns `AutoSize=true` on, on a `FlowLayoutPanel`/`TableLayoutPanel`/`Form`** — `MainForm` explicitly avoided this. This directly corroborates RESEARCH.md's own framing (D-03 was chosen "despite... this build environment has no Windows GUI" and needs "a dedicated multi-round rig-verification pass") — it is not a new independent risk, but it is now confirmed from a second, independent source (a prior developer's own in-repo comment) rather than only from this phase's own research. Include this as supporting evidence in the plan's rig-verification section.

---

### Button row (`btnSaveSettings`/`btnDiscardChanges`)

**Analog:** `src/RigToggle.App/SettingsForm.Designer.cs` lines 533-556 (current button block)

**Content to preserve verbatim (only positioning changes)**:
```csharp
this.btnSaveSettings.Text = "Save Settings";
this.btnSaveSettings.DialogResult = System.Windows.Forms.DialogResult.OK;
this.btnSaveSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
this.btnSaveSettings.Click += new System.EventHandler(this.BtnSaveSettings_Click);

this.btnDiscardChanges.Text = "Discard Changes";
this.btnDiscardChanges.DialogResult = System.Windows.Forms.DialogResult.Cancel;
this.btnDiscardChanges.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
```
Per RESEARCH.md Architecture Patterns, wrap this pair in either a small `TableLayoutPanel` (2-column Percent) or a `FlowLayoutPanel` with `FlowDirection.RightToLeft` as `tlpRoot`'s Row 2 — a minor, low-risk container, no dedicated deep-dive needed beyond consistency with the rest of the migration (`Percent`/`AutoSize`, never `Absolute`).

## Shared Patterns

### THEME-05 flat-bordered-Panel-as-GroupBox (applies to every section container: `pnlMonitorNormal`, `pnlMonitor`, `pnlSharedSection`)
**Source:** `src/RigToggle.App/SettingsForm.Designer.cs` lines 118-125 (comment + `BorderStyle.FixedSingle` pattern), established by Phase 12 (THEME-05)
**Apply to:** Every new/relocated section-container `Panel` in this migration
```csharp
this.pnlX.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
```
GroupBox is never used — `ThemeApplier`/`SetColorMode` cannot recolor its native 3D bevel border.

### Theming pipeline is container-agnostic — no code changes needed
**Source:** `src/RigToggle.App/ThemeApplier.cs` (read in full) + `src/RigToggle.App/SettingsForm.cs` grep (verified: `OnThemeChanged` at lines 139-176, `SettingsForm_Load` at lines 185-198)
**Apply to:** All relocated controls — no action needed, just a verification note for the plan
Every `ThemeApplier.*` call in `SettingsForm.cs` targets a specific instance field by name (`ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme)`, `ThemeApplier.ThemeButton(btnBrowse, IsDarkTheme)`, `ThemeApplier.ThemeComboBox(cboAudioNormal, IsDarkTheme)`, etc.) — never a recursive `Controls`-tree walk, never container-type-aware. Reparenting these fields into new `TableLayoutPanel`/`FlowLayoutPanel` containers changes nothing about whether theming reaches them. **No changes to `ThemeApplier.cs` or `SettingsForm.cs`'s theming call sites are needed or expected this phase.**

### No `.Parent`-relative logic exists in `SettingsForm.cs` — D-01 split is safe
**Source:** `grep -n "\.Parent\b" src/RigToggle.App/SettingsForm.cs` — zero matches (verified directly, this pattern-mapping pass, not just RESEARCH.md's earlier partial-file claim)
**Apply to:** The D-01 audio-picker split (`cboAudioNormal`/`cboAudioRig` moving to different parents)
Confirms RESEARCH.md Pitfall 4 / Assumptions Log A4's flagged risk does not materialize — no code in `SettingsForm.cs` assumes `cboAudioNormal.Parent == cboAudioRig.Parent` or otherwise walks up from these controls. `pnlAudioDevices` is referenced only in `Designer.cs`, never in the code-behind. **`SettingsForm.cs` needs zero code changes for the D-01 split itself** — only `SettingsForm.Designer.cs`.

### `SuspendLayout`/`ResumeLayout` and `BeginInit`/`EndInit` bracketing (unchanged mechanic, more containers now)
**Source:** `src/RigToggle.App/SettingsForm.Designer.cs` lines 103-116 (existing `BeginInit`/`SuspendLayout` block) and lines 603-615 (existing `EndInit`/`ResumeLayout` block)
**Apply to:** Every new container added this phase (`tlpRoot`, `tlpModeColumns`, `flpShared`, the button-row container) needs its own `SuspendLayout()`/`ResumeLayout(false)` pair added to these existing brackets, following the same call-one-per-container-then-batch-resume pattern already used for `pnlMonitor`/`pnlMonitorNormal`/`pnlAudioDevices`/`pnlAppPath`.
```csharp
// Existing pattern (extend, don't replace):
this.pnlMonitor.SuspendLayout();
this.pnlMonitorNormal.SuspendLayout();
this.pnlAudioDevices.SuspendLayout();   // REMOVE this line — pnlAudioDevices no longer exists post-D-01
this.pnlAppPath.SuspendLayout();
this.SuspendLayout();
// ADD: this.tlpRoot.SuspendLayout(); this.tlpModeColumns.SuspendLayout(); this.flpShared.SuspendLayout(); etc.
```

### `AutoSize=true` is genuinely new territory in this codebase — corroborating evidence
**Source:** `src/RigToggle.App/MainForm.Designer.cs` lines 126-135 (in-repo comment)
**Apply to:** `tlpRoot`, `tlpModeColumns`, `flpShared`, `Form.AutoSize` — every AutoSize-bearing construct this phase introduces
A prior developer explicitly chose `AutoSize=false` for `MainForm`'s `tileStrip` specifically because AutoSize's layout-pass timing was unproven on this runtime, especially under the `--tray` hidden-start path. This phase makes the opposite, deliberate choice (D-03/D-05) — the plan should carry this corroborating in-repo caution into its rig-verification checklist framing (RESEARCH.md already independently derived the same need; this is a second source, not a new finding).

## No Analog Found

None — every logical section has at least a role-match or exact self-analog. The only genuinely novel constructs (`TableLayoutPanel` anywhere, `FlowLayoutPanel` with `TopDown`+`AutoSize=true`, `Form.AutoSize=true`) have no in-codebase precedent at all (first use), so their patterns come directly from RESEARCH.md's Architecture Patterns / Code Examples sections (official Microsoft docs-derived, not a codebase analog) — flagged inline above at each occurrence rather than listed separately here, since RESEARCH.md already provides concrete, ready-to-use code for them.

## Metadata

**Analog search scope:** `src/RigToggle.App/` (all `.cs` and `.Designer.cs` files); `src/RigToggle.Windows/`, `src/RigToggle.Core/` excluded (RESEARCH.md confirms this phase touches exactly one tier — Presentation/WinForms Designer + code-behind — no business-logic or data-layer surface)
**Files scanned:** `SettingsForm.Designer.cs` (677 lines, read in full), `SettingsForm.cs` (grepped for `.Parent`, `pnlAudioDevices`, `OnThemeChanged`, `SettingsForm_Load`, `ThemeApplier.` call sites), `ThemeApplier.cs` (302 lines, read in full), `MainForm.Designer.cs` (grepped for `TableLayoutPanel`/`FlowLayoutPanel`/`Panel`, then read lines 40-184 in full for the `tileStrip` FlowLayoutPanel block)
**Pattern extraction date:** 2026-08-12
