---
phase: 08-tray-residency-autostart-toast-notification
plan: 03
subsystem: app-ui-settings
tags: [autostart, registry, winforms, composition-root, hidden-startup]

requires:
  - phase: 08-tray-residency-autostart-toast-notification (Plan 01)
    provides: StartupArgs.ShouldStartHidden, IAutostartConfigurator, WindowsAutostartConfigurator
  - phase: 08-tray-residency-autostart-toast-notification (Plan 02)
    provides: MainForm.InitializeTrayState(), tray residency wiring
provides:
  - "SettingsForm 'Start with Windows' checkbox backed by IAutostartConfigurator (registry as source of truth)"
  - "Program.cs composition-root wiring: args, autostart adapter injection, tray priming, --tray hidden-startup branch"
affects: [08-04]

tech-stack:
  added: []
  patterns:
    - "Dedicated inline-error control pair (lblAutostartWarning/errAutostart) per logical form section, rather than reusing a nearby unrelated section's warning label"
    - "Registry-as-source-of-truth checkbox: no AppSettings mirror field, Load reads IsEnabled() directly, Save calls Enable()/Disable() and reverts on failure"
    - "ApplicationContext(mainForm) instead of Application.Run(mainForm) for a true no-Show() hidden start, keeping the message loop (and Application.Exit() from the tray) alive"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "chkStartWithWindows reads/writes the registry only via IAutostartConfigurator — no AppSettings.StartWithWindows field was added, per RESEARCH Anti-Patterns (avoids a value that can drift from actual registry state)"
  - "Autostart write happens AFTER _settingsStore.Save() succeeds in BtnSaveSettings_Click, and on exception the checkbox reverts to _autostartConfigurator.IsEnabled() with an inline warning via a NEW dedicated errAutostart/lblAutostartWarning pair — never reusing errApp/lblAppWarning, which are Designer-bound to the unrelated App Path group box"
  - "mainForm.InitializeTrayState() is called unconditionally before either Application.Run branch, since Form.Load never fires when the form is hosted via ApplicationContext and never shown (Pitfall 6)"
  - "Hidden-start selection uses new ApplicationContext(mainForm) rather than constructing MainForm with Visible=false or WindowState=Minimized — ApplicationContext is the only one of the three that never calls Show() at all, avoiding a possible flash"

patterns-established:
  - "Any future Settings checkbox backed by external OS state (not app-owned JSON) should follow chkStartWithWindows's pattern: Load reads the live external state, Save applies the change with a dedicated inline-error revert path, no settings.json mirror"

requirements-completed: [TRAY-02]

duration: ~20min
completed: 2026-07-30
---

# Phase 08 Plan 03: Autostart Checkbox & Hidden-Startup Wiring Summary

Added a registry-backed "Start with Windows" checkbox to `SettingsForm` (off by default, reverts with an inline warning on a registry-write failure) and rewired `Program.cs`'s composition root to accept `args`, construct/inject `WindowsAutostartConfigurator`, prime the tray icon unconditionally before the message loop, and start hidden via `new ApplicationContext(mainForm)` when `--tray` is present — making the D-05 Run-key's `--tray` suffix actually produce a no-flash hidden autostart launch.

## Performance

- **Duration:** ~20 min
- **Started:** 2026-07-30 (worktree spawn, wave 3)
- **Completed:** 2026-07-30T07:32:41Z
- **Tasks:** 2 completed
- **Files modified:** 3 (SettingsForm.Designer.cs, SettingsForm.cs, Program.cs)

## Accomplishments

- `SettingsForm` now shows an off-by-default "Start with Windows" checkbox at (12, 532) that reads `IAutostartConfigurator.IsEnabled()` on Load and calls `Enable()`/`Disable()` on Save, with a brand-new dedicated `lblAutostartWarning`/`errAutostart` inline-error pair positioned directly below the checkbox (not the unrelated App Path section's `errApp`/`lblAppWarning`)
- A registry-write failure during Save reverts the checkbox to the actual `IsEnabled()` value and shows `"Could not enable Start with Windows: <reason>"` inline — the UI can never claim an autostart state that was not actually written (T-08-LIE)
- `Program.cs`'s `Main` now takes `string[] args`, constructs `WindowsAutostartConfigurator` alongside the other Windows adapters, injects it into the `SettingsFormFactory`, calls `mainForm.InitializeTrayState()` unconditionally before the run branch, and starts hidden via `new ApplicationContext(mainForm)` when `StartupArgs.ShouldStartHidden(args)` is true, otherwise `Application.Run(mainForm)` as before

## Task Commits

1. **Task 1: Add the "Start with Windows" checkbox and wire it to IAutostartConfigurator (SettingsForm)** - `b129bfa` (feat)
2. **Task 2: Composition-root wiring — args, autostart injection, tray priming, hidden-startup branch (Program.cs)** - `f8f4639` (feat)

## Files Created/Modified

- `src/RigToggle.App/SettingsForm.Designer.cs` - `chkStartWithWindows` CheckBox (12, 532)/(396, 24), new `lblAutostartWarning` Label (12, 556)/(396, 20, Visible=false) added directly to `this.Controls`, new dedicated `errAutostart` ErrorProvider with its own BeginInit/EndInit, buttons shifted to Y=588, ClientSize grown to (420, 636)
- `src/RigToggle.App/SettingsForm.cs` - `_autostartConfigurator` field + 4th ctor param with null-guard, Load reads `IsEnabled()` into the checkbox, Save applies `Enable()`/`Disable()` in a try/catch that reverts the checkbox and surfaces the dedicated inline warning on failure
- `src/RigToggle.App/Program.cs` - `Main(string[] args)`, `new WindowsAutostartConfigurator()` constructed in the composition root and injected into `SettingsFormFactory`, `mainForm.InitializeTrayState()` called unconditionally before the run branch, `if (StartupArgs.ShouldStartHidden(args))` branches between `new ApplicationContext(mainForm)` and `Application.Run(mainForm)`

## Decisions Made

See `key-decisions` in frontmatter for the full rationale set (registry-as-source-of-truth checkbox, dedicated inline-error pair, post-persist autostart apply-and-revert, `ApplicationContext` over other hidden-start mechanisms).

## Deviations from Plan

None — plan executed exactly as written. Both tasks matched the plan's `<action>` instructions precisely: the Designer control placement/sizes, the dedicated `errAutostart`/`lblAutostartWarning` pair (never reusing `errApp`/`lblAppWarning`), the no-`AppSettings`-mirror constraint, and the exact `Program.cs` wiring order (autostart construction → `SettingsFormFactory` injection → `InitializeTrayState()` → `ShouldStartHidden` branch).

**Total deviations:** 0
**Impact on plan:** None.

## Issues Encountered

None.

## Environment Constraint (matches Phase 6/7/08-01/08-02 precedent)

This executor sandbox has no `dotnet` SDK installed (confirmed via `which dotnet` and checking `/usr/share/dotnet` — not found). Verification was therefore done via grep-based source assertions plus a Python brace/paren balance check on all three modified files, instead of a live `dotnet build`/`dotnet test` run:

- `SettingsForm.Designer.cs`: `chkStartWithWindows` present 9×, `Location (12, 532)`/`Size (396, 24)` confirmed, `lblAutostartWarning.Location (12, 556)`/`Visible = false` confirmed, `btnSaveSettings`/`btnDiscardChanges` both at Y=588, `ClientSize (420, 636)` confirmed, `lblAutostartWarning` added via `this.Controls.Add` (0 matches for `grpAppPath.Controls.Add(this.lblAutostartWarning)` — confirmed NOT nested in the App Path group box), new `errAutostart` ErrorProvider declared with its own `BeginInit`/`EndInit` (6 total occurrences) — all PASSED
- `SettingsForm.cs`: `IAutostartConfigurator` appears in exactly 2 lines (field decl + ctor param), `_autostartConfigurator.IsEnabled/.Enable/.Disable` all present at the expected call sites, zero occurrences of an `AppSettings.StartWithWindows` field being introduced (the only match is a comment explaining its deliberate absence) — all PASSED
- `Program.cs`: `static void Main(string[] args)` confirmed, `new WindowsAutostartConfigurator()` present exactly once, `new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator)` confirmed as the sole `SettingsForm` construction site in `src/`, `mainForm.InitializeTrayState()` present before the run branch, `StartupArgs.ShouldStartHidden(args)` branches between `new ApplicationContext(mainForm)` and `Application.Run(mainForm)` — all PASSED
- Brace/paren balance check across all three modified files: equal counts in every file — PASSED

**Action required before Phase 8 is considered fully verified:** run `dotnet build`/`dotnet test` on a host with the .NET SDK (the Windows rig) to confirm the full solution actually compiles and the existing test suite stays green, per this plan's `<verification>` section — same standing blocker carried over from Phases 6/7/08-01/08-02. The hidden-start/`--tray`/`Application.Exit()` behavior itself (Assumption A2) is explicitly deferred to the Phase 8 rig checkpoint (Plan 08-04), not validated here.

## User Setup Required

None — no external service configuration required. (Registry write behavior and hidden-start/Exit interaction are explicitly deferred to the Phase 8 rig checkpoint per this plan's own `<verification>` section, not validated here.)

## Next Phase Readiness

- `SettingsForm.cs`/`SettingsForm.Designer.cs`/`Program.cs` are now stable for Plan 08-04's rig-validation checkpoint — no further edits to these three files are anticipated before that checkpoint.
- TRAY-02 is implementation-complete pending rig validation; REQUIREMENTS.md completion marking is deferred to the orchestrator after Plan 08-04's checkpoint passes, per this plan's explicit instruction.
- Blocker (carried over): a real `dotnet build`/`dotnet test` pass on Windows hardware, plus rig-validation of the `--tray` hidden-start/Exit flow and the registry checkbox's actual read/write behavior, are both still needed before Phase 8 is considered fully verified.

---
*Phase: 08-tray-residency-autostart-toast-notification*
*Completed: 2026-07-30*

## Self-Check: PASSED

- FOUND: `src/RigToggle.App/SettingsForm.Designer.cs`
- FOUND: `src/RigToggle.App/SettingsForm.cs`
- FOUND: `src/RigToggle.App/Program.cs`
- FOUND commit `b129bfa` (Task 1: autostart checkbox)
- FOUND commit `f8f4639` (Task 2: Program.cs composition-root wiring)
