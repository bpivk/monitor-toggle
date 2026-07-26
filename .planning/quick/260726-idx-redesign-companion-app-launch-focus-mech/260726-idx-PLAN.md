---
phase: quick-260726-idx
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.Windows/WindowsAppController.cs
  - src/RigToggle.Windows/NativeMethods.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - .planning/debug/knowledge-base.md
  - .planning/STATE.md
autonomous: true
requirements: [APP-02]
must_haves:
  truths:
    - "Toggling to rig mode always relaunches the configured target app via ShellExecute (single-instance apps self-activate), never manipulating a window handle it does not own"
    - "The user can configure the launch target by dragging a .lnk or .exe onto the Settings app-path area, in addition to the existing Browse flow"
    - "Both .lnk and .exe launch targets are accepted, validated (File.Exists), and stored uniformly as the launch target path"
    - "MinimizeIfRunning (toggle-back) still minimizes the existing window via FindBestMainWindow + ShowWindow(SW_MINIMIZE), unchanged"
    - "Dead window-hunting/diagnostic code from the superseded already-running focus branch is removed; the solution compiles with no unused-symbol references"
  artifacts:
    - path: "src/RigToggle.Windows/WindowsAppController.cs"
      provides: "Unconditional relaunch LaunchOrFocus + unchanged IsRunning/MinimizeIfRunning/FindBestMainWindow"
      contains: "UseShellExecute"
    - path: "src/RigToggle.App/SettingsForm.cs"
      provides: "Drag-and-drop launch-target configuration + .lnk/.exe validation"
      contains: "DragDrop"
  key_links:
    - from: "src/RigToggle.Core/ToggleService.cs"
      to: "WindowsAppController.LaunchOrFocus"
      via: "app.LaunchOrFocus(settings.CompanionAppPath)"
      pattern: "LaunchOrFocus"
    - from: "src/RigToggle.App/SettingsForm.cs"
      to: "AppSettings.CompanionAppPath"
      via: "stored launch-target path (.lnk or .exe)"
      pattern: "CompanionAppPath"
---

<objective>
Redesign the "bring the companion app to the foreground when switching to rig mode" path to be app-agnostic and window-handle-free: always relaunch the configured target via `Process.Start` with `UseShellExecute = true` and let a well-behaved single-instance app self-activate (rig-verified by the user against Moza Companion). This supersedes the entire `FindBestMainWindow` + `SetForegroundWindow` + poll/fallback dance on the launch path, and — because RigToggle no longer touches a window it does not own — is believed to resolve the H9 "close button inert" limitation.

Purpose: Simpler, more reliable rig-mode activation that generalizes to any single-instance Windows app, plus a Settings UX (drag any shortcut/exe) that matches the generalized concept.

Output: Rewritten launch path, trimmed P/Invoke surface, drag-and-drop Settings configuration, and updated limitation docs.

**Scope note (do NOT expand):** `MinimizeIfRunning` (toggle-back) legitimately still needs real window control and stays exactly as-is. Per project conventions, add NO speculative/defensive fallback for apps that might not self-activate — the target is known and rig-tested.

**Known interaction (document, do NOT fix):** `IsRunning`/`MinimizeIfRunning` derive the process name from the configured path via `Path.GetFileNameWithoutExtension`. If the user configures a `.lnk` (rather than the target `.exe`), that derived name may not match the real process, so toggle-back minimize may no-op. This is out of scope for this change (MinimizeIfRunning is explicitly unchanged); it is captured in the Task 3 docs update, not patched here.

**Runtime constraint:** This sandbox has no Windows runtime — changes are written and (where the SDK is present) compiled, but cannot be run/rig-tested here. The user builds and rig-tests every round.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md
@.planning/debug/resolved/moza-foreground-focus.md
@.planning/debug/knowledge-base.md
@src/RigToggle.Windows/WindowsAppController.cs
@src/RigToggle.Windows/NativeMethods.cs
@src/RigToggle.App/SettingsForm.cs
@src/RigToggle.App/SettingsForm.Designer.cs

<interfaces>
<!-- Contracts the executor needs. Extracted from the codebase — no exploration required. -->

IAppController (src/RigToggle.Core/Abstractions/IAppController.cs) — signatures MUST stay unchanged:
  bool IsRunning(string companionAppPath);
  void LaunchOrFocus(string companionAppPath);   // behavior changes to unconditional relaunch
  void MinimizeIfRunning(string companionAppPath); // UNCHANGED

ToggleService call sites (do NOT change ToggleService):
  ToggleToRigMode:    File.Exists(settings.CompanionAppPath) preflight, then app.LaunchOrFocus(settings.CompanionAppPath!)
  ToggleToNormalMode: app.MinimizeIfRunning(settings.CompanionAppPath)
  (File.Exists works for both .lnk and .exe, so the preflight needs no change.)

Symbols that must SURVIVE the cleanup (still used by MinimizeIfRunning):
  IsRunning, FindBestMainWindow, MinimizeIfRunning, Log
  NativeMethods still needed by the survivors: EnumWindows, EnumWindowsProc, GetWindowThreadProcessId,
    GetWindow (+ GW_OWNER), GetWindowTextLength, GetWindowText, GetWindowPlacement (+ WindowPlacement/Rect/Point),
    ShowWindow (+ SW_MINIMIZE)

Symbols that become DEAD once LaunchOrFocus stops touching windows (remove them):
  WindowsAppController: FocusWindow, LaunchFreshAndFocus, IsSystemCloseGrayed,
    LaunchPollTimeout, LaunchPollInterval, AlreadyRunningWindowPollTimeout, and the H10 poll/fallback logic.
  NativeMethods (remove only those with NO remaining caller after FocusWindow is gone; verify by build):
    SetForegroundWindow, SetWindowPos (+ HWND_TOPMOST/HWND_NOTOPMOST, SWP_NOMOVE/NOSIZE/NOACTIVATE),
    GetForegroundWindow, IsWindowVisible, IsIconic, IsWindowEnabled, GetClassName, GetWindowRect,
    GetSystemMenu, GetMenuState (+ SC_CLOSE, MF_BYCOMMAND, MF_GRAYED, MF_DISABLED, MENU_ITEM_NOT_FOUND),
    SW_SHOW, SW_RESTORE.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Replace already-running focus dance with unconditional ShellExecute relaunch + remove dead window-hunting code</name>
  <files>src/RigToggle.Windows/WindowsAppController.cs, src/RigToggle.Windows/NativeMethods.cs</files>
  <action>
Rewrite `LaunchOrFocus(string companionAppPath)` to do nothing but relaunch the target unconditionally — no IsRunning check, no window enumeration, no focus calls. Use `Process.Start(new ProcessStartInfo { FileName = companionAppPath, UseShellExecute = true })`. UseShellExecute=true is REQUIRED so `.lnk` shortcuts resolve like a real double-click (carrying the shortcut's working dir/args) and so a single-instance app's own activation logic runs. Keep the existing error surface pattern: if `Process.Start` returns null, throw `InvalidOperationException($"Failed to start '{companionAppPath}'.")` (matches the existing LaunchFreshAndFocus contract; ToggleService already wraps this step). Emit one `Log(...)` line noting a relaunch was requested for the path (relaunch logging is still generically useful per the task); let real `Process.Start` exceptions propagate (ToggleService's step wrapper handles them) — do NOT add a try/catch that swallows them.

Delete now-dead members: `FocusWindow`, `LaunchFreshAndFocus`, `IsSystemCloseGrayed`, and the `LaunchPollTimeout`/`LaunchPollInterval`/`AlreadyRunningWindowPollTimeout` fields and the entire H10 bounded-poll/fresh-launch-fallback block that lived in the old already-running branch.

Trim `FindBestMainWindow`'s diagnostic-only per-candidate logging (the `enabled=`/`iconic=`/`class=`/`normalRect=` candidate and result Log lines added for the H9 investigation) — that instrumentation existed to chase the now-superseded window-manipulation approach. KEEP the actual selection logic (owner-less filter, caption presence, GetWindowPlacement rcNormalPosition largest-area scoring). `FindBestMainWindow` MUST remain because `MinimizeIfRunning` still calls it. Keep `IsRunning`, `MinimizeIfRunning`, and `Log` exactly as-is behaviorally.

Rewrite the `WindowsAppController` class-level XML doc to describe the new reality: LaunchOrFocus unconditionally relaunches via ShellExecute (single-instance self-activation); only MinimizeIfRunning does real window control via FindBestMainWindow. Remove the stale MainWindowHandle/H10/H9 narrative from that doc comment. Follow project comment conventions: explain non-obvious WHY only (e.g. why UseShellExecute=true; why relaunch instead of focus), no narration of obvious code.

In `NativeMethods.cs`, remove every P/Invoke declaration and constant that has NO remaining caller after `FocusWindow`/`IsSystemCloseGrayed` are gone (see the DEAD list in `<interfaces>`). Do NOT remove anything still referenced by `FindBestMainWindow`/`MinimizeIfRunning` (see the SURVIVE list). Verify by compiling — the build's unused-symbol/reference errors are the ground truth; if the SDK is unavailable in-sandbox, cross-check each removed symbol against a full-repo grep to confirm zero remaining references before deleting. Also prune the now-orphaned diagnostic remarks blocks attached to the removed declarations.

Do NOT modify `Program.cs` — the debug.log TextWriterTraceListener wiring persists still-useful Trace output and is not dead. Do NOT change `IAppController`, `ToggleService`, or `AppSettings`.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && (dotnet build src/RigToggle.Windows/RigToggle.Windows.csproj -c Debug 2>&1 | tail -20 || echo "SDK-UNAVAILABLE: fall back to grep-verify each removed symbol has 0 references"); grep -rn "UseShellExecute" src/RigToggle.Windows/WindowsAppController.cs; grep -c "FocusWindow\|IsSystemCloseGrayed\|LaunchFreshAndFocus" src/RigToggle.Windows/WindowsAppController.cs</automated>
  </verify>
  <done>LaunchOrFocus is a single unconditional ShellExecute relaunch; FocusWindow/LaunchFreshAndFocus/IsSystemCloseGrayed and their dead NativeMethods P/Invokes are gone; FindBestMainWindow + MinimizeIfRunning + IsRunning survive and still compile; project builds (or every removed symbol confirmed zero-reference via grep when no SDK).</done>
</task>

<task type="auto">
  <name>Task 2: Generalize Settings to a "target app" with drag-and-drop (.lnk or .exe) alongside Browse</name>
  <files>src/RigToggle.App/SettingsForm.cs, src/RigToggle.App/SettingsForm.Designer.cs</files>
  <action>
Relax launch-target validation and add drag-and-drop as an alternative to Browse. Do NOT rename the persisted `AppSettings.CompanionAppPath` field (avoid churn across ToggleService/tests) — only the UX/labels generalize.

In `SettingsForm.cs`: replace `IsValidExePath` with a target-app validator (e.g. rename to `IsValidLaunchTarget`) that accepts a path where `File.Exists(path)` is true AND the extension is `.exe` OR `.lnk` (OrdinalIgnoreCase). Update all call sites (`ValidateSettingsForm`, `PopulateAppPathField`, `BtnSaveSettings_Click`). Generalize the stale-warning noun and first-run placeholder text from "application"/"No file selected" wording toward a generic "target app"/"app shortcut or .exe" phrasing (keep it short, match existing UI tone).

Add drag-and-drop on the app-path group box (`grpAppPath`) so a dragged shortcut/exe icon configures the target: in the Designer set `grpAppPath.AllowDrop = true` and wire `DragEnter` + `DragDrop` handlers (also allow drop on `txtAppPath` for a larger hit area if trivial). In `DragEnter`: accept only `DataFormats.FileDrop` with exactly one file whose extension is `.exe` or `.lnk`, set `e.Effect = DragDropEffects.Copy`, otherwise `DragDropEffects.None`. In `DragDrop`: read the dropped path, clear the error/warning (mirror `BtnBrowse_Click`), set `txtAppPath.Text`, then `ValidateSettingsForm()`. Store whatever is dropped verbatim as the launch-target path — no .lnk resolution (ShellExecute handles both at launch time).

Update the OpenFileDialog: in the Designer set `dlgOpenExe.Filter = "App shortcuts and executables (*.lnk;*.exe)|*.lnk;*.exe"` and generalize its `Title` to a target-app phrasing. Update the section label/heading wording on `grpAppPath` from a companion-app-specific label toward "Target App" if a companion-specific string is present.

Follow existing SettingsForm conventions (declarative wiring, ErrorProvider usage, no premature abstraction, no defensive handling beyond the existing patterns). Do NOT touch monitor/audio sections.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza && (dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug 2>&1 | tail -20 || echo "SDK-UNAVAILABLE"); grep -n "DragDrop\|DragEnter\|AllowDrop" src/RigToggle.App/SettingsForm.cs src/RigToggle.App/SettingsForm.Designer.cs; grep -n "\.lnk" src/RigToggle.App/SettingsForm.cs src/RigToggle.App/SettingsForm.Designer.cs</automated>
  </verify>
  <done>Validation accepts existing (File.Exists) .lnk or .exe; a single dropped .lnk/.exe onto the app-path area sets and validates the launch target; Browse dialog filter includes .lnk; labels generalized to "target app"; persisted field name unchanged; App project compiles (or SDK-unavailable noted with grep confirmation of the wiring).</done>
</task>

<task type="auto">
  <name>Task 3: Update knowledge-base and STATE.md — H9 believed resolved (pending rig verification)</name>
  <files>.planning/debug/knowledge-base.md, .planning/STATE.md</files>
  <action>
Update the two limitation records to reflect the redesign. Frame H9 as BELIEVED RESOLVED, not just accepted — because the new launch path never manipulates the target app's window (it relaunches and lets single-instance activation run), the trigger for the inert-close-button symptom no longer exists. Explicitly flag as PENDING RIG VERIFICATION (this sandbox cannot run/test; the user rig-tests).

In `.planning/STATE.md` "Known Limitations": revise the Moza close-button entry to state the launch-to-rig-mode path was redesigned to relaunch-based (ShellExecute) activation that never touches Moza's window, so the H9 close-button limitation is believed resolved pending rig verification; keep the historical pointer to `.planning/debug/resolved/moza-foreground-focus.md`. Also add a one-line note about the documented `.lnk`-vs-process-name interaction for `MinimizeIfRunning`/`IsRunning` (toggle-back minimize may no-op if a `.lnk` rather than the target `.exe` is configured, since process-name matching derives from the path) so it is not silently lost.

In `.planning/debug/knowledge-base.md`: update the `moza-foreground-focus` entry's "Known limitation" line to note the follow-up redesign (app-agnostic relaunch-based activation) that is believed to remove the H9 trigger, pending rig verification, and cross-reference this quick task's directory (`.planning/quick/260726-idx-redesign-companion-app-launch-focus-mech`).

Keep edits tight and factual; do not rewrite unrelated sections.
  </action>
  <verify>
    <automated>grep -in "believed resolved\|relaunch\|shellexecute\|pending rig" .planning/STATE.md .planning/debug/knowledge-base.md</automated>
  </verify>
  <done>Both docs record H9 as believed-resolved-pending-rig-verification via the relaunch redesign; STATE.md notes the .lnk/process-name minimize interaction; historical investigation pointers preserved.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| user → Settings (Browse/drag-drop) → Process.Start | User selects/drops a launch-target path from their own machine; RigToggle later ShellExecutes it |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-idx-01 | Elevation of Privilege | Process.Start(UseShellExecute) on configured path | accept | Path is user-chosen on a single-user personal machine (no untrusted input source); app stays asInvoker (Program.cs adds no elevation manifest), so a launched target inherits no elevated rights from RigToggle. |
| T-idx-02 | Tampering | Dropped file in DragDrop | mitigate | Accept only a single FileDrop item with a .exe/.lnk extension that File.Exists; anything else sets DragDropEffects.None and is not stored. |
</threat_model>

<verification>
- LaunchOrFocus contains exactly one relaunch call (UseShellExecute=true) and no window-enumeration/focus calls.
- Removed symbols (FocusWindow, LaunchFreshAndFocus, IsSystemCloseGrayed, dead NativeMethods P/Invokes) have zero remaining references repo-wide.
- FindBestMainWindow + MinimizeIfRunning + IsRunning unchanged behaviorally; MinimizeIfRunning still resolves and minimizes via FindBestMainWindow.
- Settings accepts and validates .lnk or .exe via both Browse and drag-drop; persisted CompanionAppPath field name unchanged.
- Both touched projects build (or, absent an SDK in-sandbox, each removed symbol confirmed unreferenced via grep and the new code is syntactically consistent).
- STATE.md + knowledge-base.md record H9 as believed-resolved pending rig verification.
</verification>

<success_criteria>
- Rig-mode activation is a single unconditional ShellExecute relaunch that never manipulates the target app's window.
- Any single-instance app configurable via drag-and-drop of a .lnk/.exe shortcut, in addition to Browse.
- No dead code from the superseded window-focus approach remains; MinimizeIfRunning path intact.
- Docs updated; changes ready for the user to build and rig-test (no runtime verification possible in-sandbox).
</success_criteria>

<output>
Create `.planning/quick/260726-idx-redesign-companion-app-launch-focus-mech/260726-idx-SUMMARY.md` when done.
</output>
