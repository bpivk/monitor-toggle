---
phase: quick-260728-rmp
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.App/SettingsForm.Designer.cs
autonomous: true
requirements: []
must_haves:
  truths:
    - "Monitor grid columns read 'Off (Rig)' and 'On (Rig)' instead of 'Disable'/'Enable'"
    - "Hovering either checkbox column header shows a tooltip explaining it only applies to the switch INTO Rig Mode"
    - "A permanent explanation label sits above the monitor grid clarifying that Normal Mode is always restored automatically"
    - "All controls below the monitor group shift down by exactly +58px so nothing overlaps"
  artifacts:
    - path: src/RigToggle.App/SettingsForm.Designer.cs
      provides: "Relabeled/retooltipped monitor grid, new lblMonitorExplain label, and shifted layout coordinates"
      contains: "lblMonitorExplain"
  key_links:
    - from: "grpMonitor.Controls"
      to: "lblMonitorExplain"
      via: "Controls.Add before dgvMonitors"
      pattern: "grpMonitor.Controls.Add\\(this.lblMonitorExplain\\)"
---

<objective>
Improve the Settings monitor grid's clarity. The grid's checkbox columns only ever describe the transition INTO Rig Mode, but the old "Disable"/"Enable" headers led the user to read them as a separate "Normal Mode" configuration. This plan relabels the columns, adds hover tooltips, adds a permanent explanation label, and reflows the form layout to make room.

Product decision (confirmed with user): underlying behavior is UNCHANGED. This is a labeling/copy/layout change ONLY in `SettingsForm.Designer.cs`. Do NOT touch `ToggleService`, `WindowsMonitorController`, or any Core/Windows logic. Do NOT touch `SettingsForm.cs` (it does not reference control positions/order in a way this change affects).

Purpose: Remove a persistent point of user confusion about what the monitor grid actually configures.
Output: Updated `src/RigToggle.App/SettingsForm.Designer.cs` with new copy and reflowed coordinates.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@src/RigToggle.App/SettingsForm.Designer.cs

<constraints>
- All target coordinate/text values below are pre-computed by the user's request. Use them EXACTLY as given. Do NOT recompute, round, or "improve" any number or string.
- Linux sandbox has no dotnet SDK; `net10.0-windows` cannot build here. Verify via grep/source assertions of exact values, per this project's established Phase 6 practice.
- Live visual confirmation on the Windows rig (no clipped text, no overlapping controls) is the required follow-up after this plan — note it in the SUMMARY.
</constraints>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Relabel columns, add tooltips, and add the permanent explanation label</name>
  <files>src/RigToggle.App/SettingsForm.Designer.cs</files>
  <action>
Make the copy and new-control changes inside `InitializeComponent()` and the designer fields section:

1. colDisable block (currently ~lines 112-117): change `HeaderText` from "Disable" to "Off (Rig)"; change `Width` from 60 to 66; add a new line `this.colDisable.ToolTipText = "Turns this monitor off when switching to Rig Mode. Restored automatically when switching back to Normal Mode.";`

2. colEnable block (currently ~lines 119-124): change `HeaderText` from "Enable" to "On (Rig)"; change `Width` from 60 to 66; add a new line `this.colEnable.ToolTipText = "Turns this monitor on when switching to Rig Mode (for a monitor normally kept off, e.g. to save power). Turned off again automatically when switching back to Normal Mode.";`
   (colMonitorName uses AutoSizeMode.Fill, so it absorbs the +12px automatically — do NOT change any other column width.)

3. Instantiate the new label alongside the other grpMonitor child instantiations (near line 38 where `this.lblMonitorWarning = new ...` is): add `this.lblMonitorExplain = new System.Windows.Forms.Label();`

4. Add a `// lblMonitorExplain` configuration block (place it near the lblMonitorWarning block, following the lblAudioNormalCaption declaration pattern for a permanent, always-visible label):
   - `this.lblMonitorExplain.Location = new System.Drawing.Point(12, 22);`
   - `this.lblMonitorExplain.Size = new System.Drawing.Size(372, 50);`
   - `this.lblMonitorExplain.AutoSize = false;`
   - `this.lblMonitorExplain.Text = "Only controls what changes when switching TO Rig Mode. Normal Mode is always restored exactly as it was before — nothing to set up separately.";`
   - `this.lblMonitorExplain.Name = "lblMonitorExplain";`
   Do NOT set Visible=false — this label is permanent, not a warning.

5. In the grpMonitor.Controls.Add sequence (currently lines 81-82), add `this.grpMonitor.Controls.Add(this.lblMonitorExplain);` BEFORE the existing `this.grpMonitor.Controls.Add(this.dgvMonitors);` line so tab/z-order is sane.

6. In the designer fields section at the bottom, add `private System.Windows.Forms.Label lblMonitorExplain;` next to the existing `private System.Windows.Forms.Label lblMonitorWarning;` declaration (line ~325).

Do NOT reflow any Y coordinates in this task — that is Task 2.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && grep -q 'this.colDisable.HeaderText = "Off (Rig)";' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.colEnable.HeaderText = "On (Rig)";' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.colDisable.Width = 66;' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.colEnable.Width = 66;' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.colDisable.ToolTipText = "Turns this monitor off when switching to Rig Mode. Restored automatically when switching back to Normal Mode.";' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.colEnable.ToolTipText = "Turns this monitor on when switching to Rig Mode (for a monitor normally kept off, e.g. to save power). Turned off again automatically when switching back to Normal Mode.";' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.lblMonitorExplain.Text = "Only controls what changes when switching TO Rig Mode. Normal Mode is always restored exactly as it was before — nothing to set up separately.";' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'private System.Windows.Forms.Label lblMonitorExplain;' src/RigToggle.App/SettingsForm.Designer.cs && grep -Pzoq 'grpMonitor.Controls.Add\(this.lblMonitorExplain\);\s*\n\s*this.grpMonitor.Controls.Add\(this.dgvMonitors\);' src/RigToggle.App/SettingsForm.Designer.cs && echo PASS</automated>
  </verify>
  <done>Both columns are renamed to "Off (Rig)"/"On (Rig)", widened to 66, and carry the exact tooltip strings. lblMonitorExplain is instantiated, configured at (12,22)/(372,50) with the exact permanent text, added to grpMonitor.Controls before dgvMonitors, and declared as a field. No warning-style Visible=false on the new label. No Y coordinates reflowed yet.</done>
</task>

<task type="auto">
  <name>Task 2: Reflow layout — shift grid, grow group, and move all controls below by +58px</name>
  <files>src/RigToggle.App/SettingsForm.Designer.cs</files>
  <action>
Apply the pre-computed coordinate changes. Every shift below grpMonitor is exactly +58px (delta = 234 - 176 = 58); use the identical +58 for all of them.

1. dgvMonitors.Location: `(12, 22)` -> `(12, 80)` (Size unchanged, 372x120).
2. lblMonitorWarning.Location: `(12, 148)` -> `(12, 206)` (Size unchanged, 372x20).
3. grpMonitor.Size: `(396, 176)` -> `(396, 234)`.
4. grpAudioDevices.Location: `(12, 200)` -> `(12, 258)`.
5. grpAppPath.Location: `(12, 344)` -> `(12, 402)`.
6. chkEnableDebugLogging.Location: `(12, 426)` -> `(12, 484)`.
7. btnSaveSettings.Location: `(180, 476)` -> `(180, 534)`.
8. btnDiscardChanges.Location: `(298, 476)` -> `(298, 534)`.
9. SettingsForm.ClientSize: `(420, 524)` -> `(420, 582)`.

Do NOT change any X coordinate or any Size (except grpMonitor.Size and ClientSize.Height as listed). Only the Y values (and the two explicitly-listed Size/ClientSize heights) change.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && grep -q 'this.dgvMonitors.Location = new System.Drawing.Point(12, 80);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.lblMonitorWarning.Location = new System.Drawing.Point(12, 206);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.grpMonitor.Size = new System.Drawing.Size(396, 234);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.grpAudioDevices.Location = new System.Drawing.Point(12, 258);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.grpAppPath.Location = new System.Drawing.Point(12, 402);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.chkEnableDebugLogging.Location = new System.Drawing.Point(12, 484);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.btnSaveSettings.Location = new System.Drawing.Point(180, 534);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.btnDiscardChanges.Location = new System.Drawing.Point(298, 534);' src/RigToggle.App/SettingsForm.Designer.cs && grep -q 'this.ClientSize = new System.Drawing.Size(420, 582);' src/RigToggle.App/SettingsForm.Designer.cs && echo PASS</automated>
  </verify>
  <done>dgvMonitors sits at (12,80), lblMonitorWarning at (12,206), grpMonitor grown to (396,234), and grpAudioDevices/grpAppPath/chkEnableDebugLogging/btnSaveSettings/btnDiscardChanges all shifted +58px in Y, with ClientSize at (420,582). No X coordinate or non-listed Size changed. The old pre-shift coordinate values no longer appear for these controls.</done>
</task>

</tasks>

<verification>
- No changes outside `src/RigToggle.App/SettingsForm.Designer.cs`: `git diff --name-only` lists only that file.
- No touch to Core/Windows/service logic or `SettingsForm.cs`.
- All grep assertions in both tasks PASS.
- The build cannot run in this Linux sandbox (no dotnet SDK / net10.0-windows). Live visual confirmation on the Windows rig — no clipped text in the wider columns, the explanation label fully readable, no overlapping controls after the +58px reflow — is the required follow-up. Record this explicitly in the SUMMARY.
</verification>

<success_criteria>
- Column headers read "Off (Rig)" and "On (Rig)", both 66px wide, each with the exact tooltip string.
- Permanent lblMonitorExplain label exists above the grid with the exact text, added to grpMonitor.Controls before dgvMonitors, and declared as a designer field.
- Grid and all controls below grpMonitor reflowed by the exact deltas (grid to y=80, group height 234, everything below +58, ClientSize height 582).
- Behavior unchanged; only Designer.cs edited.
</success_criteria>

<output>
Create `.planning/quick/260728-rmp-improve-settings-monitor-grid-clarity-re/260728-rmp-SUMMARY.md` when done. Include the rig-side live-visual-confirmation follow-up note.
</output>
