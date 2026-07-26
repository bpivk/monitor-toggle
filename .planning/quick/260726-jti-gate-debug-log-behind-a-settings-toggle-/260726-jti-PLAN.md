---
phase: quick-260726-jti
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/Program.cs
autonomous: true
requirements: []

must_haves:
  truths:
    - "By default (fresh settings, no toggle enabled) the app no longer writes %LOCALAPPDATA%\\RigToggle\\debug.log on startup"
    - "A user can enable debug logging via a checkbox in Settings, and after saving + restart the debug.log listener is wired again"
    - "MainForm no longer shows any 'Moza Companion: Running/Not running' status line"
    - "The solution builds with zero warnings/errors after both tasks (no unused field/param for IAppController in MainForm)"
  artifacts:
    - path: "src/RigToggle.Core/Models/AppSettings.cs"
      provides: "EnableDebugLogging bool property"
      contains: "EnableDebugLogging"
    - path: "src/RigToggle.App/SettingsForm.Designer.cs"
      provides: "chkEnableDebugLogging checkbox control"
      contains: "chkEnableDebugLogging"
    - path: "src/RigToggle.App/Program.cs"
      provides: "settings-gated TextWriterTraceListener wiring"
      contains: "EnableDebugLogging"
  key_links:
    - from: "src/RigToggle.App/SettingsForm.cs"
      to: "AppSettings.EnableDebugLogging"
      via: "load into chkEnableDebugLogging.Checked + save from it in BtnSaveSettings_Click"
      pattern: "chkEnableDebugLogging"
    - from: "src/RigToggle.App/Program.cs"
      to: "JsonSettingsStore.Load().EnableDebugLogging"
      via: "gate the trace-listener block behind the loaded flag"
      pattern: "EnableDebugLogging"
---

<objective>
Two independent, fully-specified cleanups now that the moza-foreground-focus
investigation is closed and rig-verified:

- Task A: Make debug.log opt-in — add an `EnableDebugLogging` setting and a Settings
  checkbox, and gate the existing `TextWriterTraceListener` wiring behind it (default off).
- Task B: Remove the now-useless "Moza Companion: Running/Not running" status line from
  MainForm, along with the field/constructor param for `IAppController` that becomes dead.

Purpose: Reduce disk churn (no unconditional log file) while keeping diagnostics one
checkbox away, and remove UI clutter that is no longer meaningful.
Output: Updated AppSettings, SettingsForm (+Designer), Program.cs, MainForm (+Designer).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<interfaces>
<!-- Extracted from the codebase — executor should use these directly, no exploration needed. -->

AppSettings (src/RigToggle.Core/Models/AppSettings.cs), current last property:
```csharp
    public string? CompanionAppPath { get; set; }
    public bool SkipMonitorConfirmation { get; set; }
```

Program.cs current shape (src/RigToggle.App/Program.cs):
- `basePath` = %LOCALAPPDATA%\RigToggle
- Lines ~42-54: try/catch block that does Directory.CreateDirectory + StreamWriter to
  debug.log + `Trace.Listeners.Add(new TextWriterTraceListener(traceWriter))`, catch swallows.
- Line 56: `var settingsStore = new JsonSettingsStore(Path.Combine(basePath, "settings.json"));`
- Line 72: `var mainForm = new MainForm(toggleService, appController, settingsStore, monitorController, SettingsFormFactory);`
- `appController` local (line 61) is ALSO passed to `ToggleService` (line 68) — must NOT be removed.

SettingsForm (src/RigToggle.App/SettingsForm.cs):
- `SettingsForm_Load` (line 46) loads `_settings = _settingsStore.Load();` then populates pickers.
- `BtnSaveSettings_Click` (line 294) constructs `new AppSettings { ... SkipMonitorConfirmation = ... }` at ~line 321-331 and calls `_settingsStore.Save(settingsToSave)`.

SettingsForm.Designer.cs layout facts:
- ClientSize = 420 x 380. grpAppPath at (12,244) size 396x76 (ends y=320).
  btnSaveSettings at (180,332), btnDiscardChanges at (298,332), size 110x32.
- No CheckBox control exists yet anywhere in the project.

MainForm.Designer.cs: `lblCompanionStatus` (Label) declared line 34/102, configured lines
67-74, added via `this.Controls.Add(this.lblCompanionStatus)` line 92.

MainForm.cs: `_appController` field line 19, ctor param line 26, assigned line 32; its ONLY
usage is `_appController.IsRunning(...)` in `RefreshUi()` lines 58-59.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task A: Gate debug.log behind an EnableDebugLogging Settings toggle</name>
  <files>src/RigToggle.Core/Models/AppSettings.cs, src/RigToggle.App/SettingsForm.Designer.cs, src/RigToggle.App/SettingsForm.cs, src/RigToggle.App/Program.cs</files>
  <action>
1. In AppSettings.cs, add `public bool EnableDebugLogging { get; set; }` immediately after
   the existing `SkipMonitorConfirmation` auto-property. Same plain-auto-property style;
   defaults to false. Do not add a doc comment (matches surrounding properties).

2. In SettingsForm.Designer.cs, add the first CheckBox in the project — a bare checkbox
   (no new GroupBox) placed below grpAppPath. Concretely:
   - Declare field: `private System.Windows.Forms.CheckBox chkEnableDebugLogging;` in the
     field-declaration region near the bottom.
   - Instantiate in InitializeComponent: `this.chkEnableDebugLogging = new System.Windows.Forms.CheckBox();`
   - Configure it: Text = "Enable debug logging (writes to %LOCALAPPDATA%\\RigToggle\\debug.log)",
     Location = new Point(12, 326), Size = new Size(396, 24), AutoSize = false, Name = "chkEnableDebugLogging".
   - To make room, move the two buttons down: btnSaveSettings.Location -> (180, 360),
     btnDiscardChanges.Location -> (298, 360); and grow the form:
     ClientSize = new Size(420, 408).
   - Add it to the form: `this.Controls.Add(this.chkEnableDebugLogging);` (add-order does
     not matter functionally; place it near the other top-level Controls.Add calls).

3. In SettingsForm.cs SettingsForm_Load, after `_settings = _settingsStore.Load();`, add
   `chkEnableDebugLogging.Checked = _settings.EnableDebugLogging;` (load the persisted value
   into the UI, mirroring how the other fields hydrate from `_settings`).

4. In SettingsForm.cs BtnSaveSettings_Click, in the `new AppSettings { ... }` initializer
   (the same object where `SkipMonitorConfirmation = ...` appears), add a line
   `EnableDebugLogging = chkEnableDebugLogging.Checked,`.

5. In Program.cs, reorder so settings load BEFORE the trace-listener wiring. Move the
   `var settingsStore = new JsonSettingsStore(...)` construction above the try/catch trace
   block. Load settings once (e.g. `var settings = settingsStore.Load();`) inside its own
   best-effort try/catch that defaults to a value meaning "logging off" if Load throws
   (per the fail-toward-off requirement). Then wrap the EXISTING try/catch
   TextWriterTraceListener block in `if (settings.EnableDebugLogging) { ... }`. Keep the
   inner try/catch exactly as-is (logging setup must never throw/block startup). Do NOT
   construct settingsStore twice — the single instance is reused for ToggleService/factories
   below. Do NOT remove or alter any Trace.WriteLine call sites in other files.
  </action>
  <verify>
    <automated>dotnet build src/RigToggle.App/RigToggle.App.csproj -clp:ErrorsOnly 2>&1 | tail -5; grep -q "EnableDebugLogging" src/RigToggle.Core/Models/AppSettings.cs && grep -q "chkEnableDebugLogging" src/RigToggle.App/SettingsForm.Designer.cs && grep -q "if (settings.EnableDebugLogging)" src/RigToggle.App/Program.cs && echo GATE_OK</automated>
  </verify>
  <done>AppSettings has EnableDebugLogging; SettingsForm shows a checkbox that loads/saves it; Program.cs only wires the TextWriterTraceListener when the loaded setting is true, defaulting to off if settings load fails; solution builds clean.</done>
</task>

<task type="auto">
  <name>Task B: Remove Moza Companion status line and dead IAppController from MainForm</name>
  <files>src/RigToggle.App/MainForm.cs, src/RigToggle.App/MainForm.Designer.cs, src/RigToggle.App/Program.cs</files>
  <action>
Do this AFTER Task A's Program.cs edit (both edits touch Program.cs in non-overlapping
regions — Task A the top trace block, Task B the MainForm construction line).

1. In MainForm.cs RefreshUi(), remove the `companionRunning` computation and the
   `lblCompanionStatus.Text = ...` assignment (the block using `_settingsStore.Load()` +
   `_appController.IsRunning(...)`). Keep the mode/button lines. Note: `_settingsStore` is
   still used elsewhere in the file (BtnToggle_Click) — do NOT remove it. Only
   `_appController` becomes dead. Update the RefreshUi() doc-comment to drop the mention of
   the companion-app status line. Also update the class-level doc comment that references
   "the companion-app status line (D-15)".

2. Confirm `_appController` has zero remaining references in MainForm.cs
   (`grep -n _appController src/RigToggle.App/MainForm.cs` should return nothing after step 1),
   then remove: the `private readonly IAppController _appController;` field (line 19), the
   `IAppController appController,` constructor parameter (line 26), and the
   `_appController = appController ?? throw new ArgumentNullException(nameof(appController));`
   assignment (line 32). Leave the `using RigToggle.Core.Abstractions;` import — other
   injected types (ISettingsStore, IMonitorController) come from it.

3. In MainForm.Designer.cs, remove `lblCompanionStatus` entirely: the instantiation line
   (`this.lblCompanionStatus = new ...` line 34), the configuration block (lines 67-74),
   the `this.Controls.Add(this.lblCompanionStatus);` line (92), and the field declaration
   (line 102). The MainForm ClientSize (320x200) can stay as-is.

4. In Program.cs, update the `new MainForm(...)` call to drop the `appController` argument:
   `var mainForm = new MainForm(toggleService, settingsStore, monitorController, SettingsFormFactory);`.
   The `appController` LOCAL must remain — it is still passed to the ToggleService ctor.

Do NOT touch IAppController, WindowsAppController, ToggleService, or any other consumer.
MainForm.resx does not exist — no resx cleanup.
  </action>
  <verify>
    <automated>dotnet build src/RigToggle.App/RigToggle.App.csproj -clp:ErrorsOnly 2>&1 | tail -5; ! grep -q "lblCompanionStatus" src/RigToggle.App/MainForm.Designer.cs && ! grep -q "_appController" src/RigToggle.App/MainForm.cs && grep -q "new MainForm(toggleService, settingsStore" src/RigToggle.App/Program.cs && echo CLEANUP_OK</automated>
  </verify>
  <done>MainForm no longer references lblCompanionStatus or _appController; MainForm ctor no longer takes IAppController; Program.cs call site updated; appController local still flows to ToggleService; solution builds clean.</done>
</task>

</tasks>

<verification>
- `dotnet build` of the solution (or RigToggle.App project) succeeds with no errors and no
  new warnings (no unused field/parameter warnings for the removed IAppController).
- Fresh run with default settings does NOT create/append debug.log; enabling the checkbox,
  saving, and restarting DOES wire the listener again.
- MainForm renders without the companion status line and with no leftover dead space
  control.
</verification>

<success_criteria>
- EnableDebugLogging property exists and is honored as an off-by-default gate on the trace
  listener in Program.cs.
- SettingsForm checkbox round-trips the value through load and save.
- lblCompanionStatus and MainForm's IAppController field/param/arg are fully removed with a
  clean build.
</success_criteria>

<output>
Create `.planning/quick/260726-jti-gate-debug-log-behind-a-settings-toggle-/260726-jti-SUMMARY.md` when done.
</output>
