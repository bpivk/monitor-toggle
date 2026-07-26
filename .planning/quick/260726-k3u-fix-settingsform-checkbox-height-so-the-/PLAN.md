---
phase: quick-260726-k3u
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
    - "chkEnableDebugLogging is tall enough (40px) that its two-line wrapped text is not visually clipped"
    - "btnSaveSettings and btnDiscardChanges shift down by the same 16px delta so nothing overlaps the taller checkbox"
    - "The form's ClientSize grows by the same 16px so the buttons keep their original bottom margin"
  artifacts:
    - path: "src/RigToggle.App/SettingsForm.Designer.cs"
      provides: "Corrected checkbox height and shifted downstream layout"
      contains: "new System.Drawing.Size(396, 40)"
---

<objective>
User rig-confirmed the "Enable debug logging" checkbox added in quick task 260726-jti gets its
text visually clipped after "(writes to" — the checkbox is 24px tall (single-line) but its text
wraps to two lines at 396px width, so the second line is cut off (height, not width, is the
constraint). Fix: grow the checkbox to 40px tall (a 16px delta) and shift the two buttons below
it, plus the form's ClientSize, down by the same 16px so nothing overlaps.

Output: Four numeric literal changes in SettingsForm.Designer.cs. No behavior/C# logic change.
</objective>

<context>
Current values in src/RigToggle.App/SettingsForm.Designer.cs:
- chkEnableDebugLogging.Size = new Size(396, 24)   -> new Size(396, 40)
- btnSaveSettings.Location   = new Point(180, 360) -> new Point(180, 376)
- btnDiscardChanges.Location = new Point(298, 360) -> new Point(298, 376)
- ClientSize                 = new Size(420, 408)  -> new Size(420, 424)
</context>

<tasks>

<task type="auto">
  <name>Task 1: Grow checkbox height and shift downstream layout by 16px</name>
  <files>src/RigToggle.App/SettingsForm.Designer.cs</files>
  <action>
Change exactly four numeric literals, nothing else:
1. `this.chkEnableDebugLogging.Size = new System.Drawing.Size(396, 24);` -> `new System.Drawing.Size(396, 40);`
2. `this.btnSaveSettings.Location = new System.Drawing.Point(180, 360);` -> `new System.Drawing.Point(180, 376);`
3. `this.btnDiscardChanges.Location = new System.Drawing.Point(298, 360);` -> `new System.Drawing.Point(298, 376);`
4. `this.ClientSize = new System.Drawing.Size(420, 408);` -> `new System.Drawing.Size(420, 424);`

Do not change Location/Text/Name/AutoSize on the checkbox, do not change button widths/heights/x-positions, do not touch any other control.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && grep -q "new System.Drawing.Size(396, 40)" src/RigToggle.App/SettingsForm.Designer.cs && grep -q "new System.Drawing.Point(180, 376)" src/RigToggle.App/SettingsForm.Designer.cs && grep -q "new System.Drawing.Point(298, 376)" src/RigToggle.App/SettingsForm.Designer.cs && grep -q "new System.Drawing.Size(420, 424)" src/RigToggle.App/SettingsForm.Designer.cs && echo PASS</automated>
  </verify>
  <done>chkEnableDebugLogging is 396x40; btnSaveSettings and btnDiscardChanges are at y=376; ClientSize is 420x424. No other control changed.</done>
</task>

</tasks>

<verification>
- All four grep checks in Task 1's verify block pass.
- No .NET SDK in this sandbox to actually render/build the form; the user will confirm visually on the rig that both lines of the checkbox text are now fully visible and the buttons don't overlap it.
</verification>

<success_criteria>
- The checkbox's wrapped two-line text is fully visible (no clipping).
- Save/Discard buttons and the form's bottom edge shift down by exactly 16px, preserving the original visual margins.
</success_criteria>

<output>
Create `.planning/quick/260726-k3u-fix-settingsform-checkbox-height-so-the-/260726-k3u-SUMMARY.md` when done.
</output>
