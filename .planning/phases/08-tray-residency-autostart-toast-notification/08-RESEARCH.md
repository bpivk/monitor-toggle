# Phase 8: Tray Residency, Autostart & Toast Notification - Research

**Researched:** 2026-07-30
**Domain:** WinForms tray residency (`NotifyIcon`/`ContextMenuStrip`), HKCU Registry autostart, `NotifyIcon.ShowBalloonTip` toast notifications, WinForms message-loop startup semantics
**Confidence:** HIGH

> **RIG-TESTED CORRECTION (post-implementation, 08-04 checkpoint):** This document's central claim about `Application.Run(new ApplicationContext(mainForm))` — that passing `mainForm` into the `ApplicationContext` constructor suppresses `Show()` while still hosting it — was rig-tested on real Windows and found **FALSE**: the window still appeared under `--tray` despite this mechanism, contradicting every citation below (Pattern 4, the Anti-Patterns table, the flowchart, the condensed official-pattern excerpt). The actual working mechanism, confirmed live: give `ApplicationContext` **no** main form at all — `Application.Run(new ApplicationContext())` — and hold `mainForm` only as a local object reference, shown for the first time on demand via the tray icon's own handlers. `Application.Exit()` from the tray still terminates the loop correctly without any `ApplicationContext.MainForm` wiring. Every `ApplicationContext(mainForm)` reference below is superseded by this finding; kept in place as a record of what was tried and disproven, not as guidance. See `src/RigToggle.App/Program.cs`'s current implementation for the corrected code.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tray Icon Appearance & Click Behavior (TRAY-04, TRAY-05)**
- **D-01:** Two distinct tray icon states — a "normal" icon and a "rig" icon — swapped via `NotifyIcon.Icon` (not a badge/overlay drawn on one icon). The tooltip text also updates to match ("Rig Toggle — Normal Mode" / "Rig Toggle — Rig Mode"). Exact icon glyph/color design is a UI-SPEC concern (this phase carries `UI hint: yes` — a real UI-SPEC.md is expected here).
- **D-02:** Left-click on the tray icon restores and focuses the main window (TRAY-05, literal reading). Double-click is not specially handled — WinForms' `NotifyIcon` fires `Click` then `DoubleClick` on a double-click sequence, so the window simply gets a second harmless restore/focus call. No extra complexity needed.

**Minimize-to-Tray Scope (TRAY-01)**
- **D-03:** Only the window's Close (X button / Alt+F4 / taskbar-close) is intercepted and redirected to "hide to tray" (via `FormClosing` with `CloseReason` checked, `e.Cancel = true`, then `Hide()`). The native taskbar minimize button keeps standard OS minimize behavior — TRAY-01's literal wording is scoped to "closing the main window," not minimizing.

**Tray Context Menu (TRAY-03)**
- **D-04:** The toggle menu item's label is dynamic and mirrors `MainForm`'s existing `btnToggle.Text` wording exactly. "Settings" opens the existing modal `SettingsForm`. "Exit" performs a real `Application.Exit()` — the tray icon (`NotifyIcon`) MUST be explicitly disposed/hidden (`Visible = false`) before or during exit, since an undisposed `NotifyIcon` is a well-known WinForms bug that leaves a stale, unclickable ghost icon in the tray until the user hovers over it.

**Autostart (TRAY-02)**
- **D-05:** Autostart uses a plain `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` registry value (not Task Scheduler). Settings gets a new checkbox ("Start with Windows"), off by default, following the exact same UI pattern as the existing `chkEnableDebugLogging` checkbox in `SettingsForm.cs` (plain checkbox, no separate Save step beyond the existing Save button). The registry value's command string points at the self-contained exe's own path plus the `--tray` flag from D-06 below.

**Startup-Mode Flag for Autostart (TRAY-02, cross-cutting with tray residency)**
- **D-06:** `Program.cs`'s `Main` gains a `string[] args` parameter; when `args` contains `--tray`, the app starts with `MainForm` created but never shown. The Run registry value itself supplies `--tray` as part of its command string — no runtime "was I autostarted" detection heuristic needed.
  > **Research correction (see Common Pitfalls / Pattern 4):** the literal mechanism described in CONTEXT.md ("`Application.Run(mainForm)` still runs the message loop without an initial `Show()`") does not work as written — `Application.Run(Form)` is documented to unconditionally make the form visible. The *intent* (start hidden under `--tray`) is fully achievable, but requires wrapping `mainForm` in an `ApplicationContext` instead of passing it directly to `Application.Run`. This is a mechanism correction, not a scope change — D-06's outcome is unaffected.

**Toast Notification (NOTIF-01)**
- **D-07:** Uses `NotifyIcon.ShowBalloonTip`, not a packaged-app toast API.
- **D-08:** The toast fires on every toggle triggered via the tray context menu, unconditionally — it does NOT check whether the main window happens to be currently visible or hidden-to-tray. GUI-button-triggered toggles keep their existing `MessageBox` behavior unchanged.
- **D-09:** Toast content exactly mirrors `MainForm`'s existing `FormatChecklist` per-step outcome text plus the resulting mode, reusing the same formatting logic rather than inventing new wording.

### Claude's Discretion
- Whether the `NotifyIcon`/`ContextMenuStrip` component lives directly on `MainForm` (designer component, most idiomatic) or a separate class — `MainForm`-hosted is the natural default.
- Exact registry value name/format for the autostart entry and how the Settings checkbox reads current state (registry existence vs. a separate settings.json flag) — left to planner. **Research recommendation:** read registry existence directly as the source of truth (see Architecture Patterns Pattern 3) rather than a mirrored settings.json boolean, to avoid drift if the value is deleted outside the app (e.g. via Autoruns/Task Manager startup tab).
- Exact `FormatChecklist`-reuse mechanism (widen visibility vs. duplicate) — left to planner; reuse is strongly preferred. **Research recommendation:** move it out of `MainForm` entirely into `RigToggle.Core` (see Architecture Patterns Pattern 2) — it is a pure string-formatting function over an already-`Core` type (`ToggleResult`), so relocating it also makes it unit-testable for the first time, which reuse-in-place would not achieve.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. Hotkey trigger (Phase 9) and CLI trigger/single-instance IPC (Phase 10) are out of scope; this phase only needs to make the tray-menu trigger notification-worthy and reuse Phase 7's `ToggleOrchestrator` entry point those later phases will also call.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TRAY-01 | Closing the main window minimizes it to the tray instead of exiting | Architecture Patterns Pattern 1 (`FormClosing`/`CloseReason` gate); Common Pitfall 1 (`UserClosing` fires for both X-click and `this.Close()` — must not also fire for `ApplicationExitCall`) |
| TRAY-02 | User can enable "start with Windows" via a Settings checkbox (off by default) | Standard Stack (`Microsoft.Win32.Registry`); Architecture Patterns Pattern 3 (`IAutostartConfigurator` abstraction); Common Pitfall 5 (single-file exe path resolution) |
| TRAY-03 | Right-click tray icon shows context menu (Switch mode / Settings / Exit) | Standard Stack (`ContextMenuStrip`, confirmed the only viable option on net10.0-windows — legacy `ContextMenu` was removed from WinForms Core); Common Pitfall 2 (must use `MouseClick`, not `Click`, to avoid double-triggering restore on right-click) |
| TRAY-04 | Tray icon visually reflects current mode | Architecture Patterns Pattern 1 (`RefreshUi` extension); Common Pitfall 3 (`.ico` embedding vs. Bitmap-derived `Icon` GDI leak); Common Pitfall 6 (`Form.Load` never fires without `Show()`) |
| TRAY-05 | Left-clicking tray icon restores main window | Common Pitfall 2 (`MouseClick` + `MouseButtons.Left` check) |
| NOTIF-01 | Toast confirms toggle when triggered without GUI open, matching GUI's partial-failure detail | Architecture Patterns Pattern 2 (`FormatChecklist` relocation to `Core`); Common Pitfall 4 (255/63-char truncation limits) |
</phase_requirements>

## Summary

This phase's actual research risk is not "which library" — every mechanism needed (`NotifyIcon`, `ContextMenuStrip`, `Microsoft.Win32.Registry`, `Application.Exit`) is BCL/WinForms-builtin with zero new NuGet dependencies. The risk is in **three specific, well-documented WinForms/Win32 gotchas that this codebase has not encountered before**, each of which would silently produce wrong behavior if implemented the way CONTEXT.md's decisions read literally:

1. **D-06's startup-hidden mechanism as literally described does not work.** `Application.Run(Form mainForm)` is documented by Microsoft to "begin running a standard application message loop on the current thread, **and make the specified form visible**" — internally it does exactly `applicationContext.MainForm = mainForm; applicationContext.MainForm.Show(); Application.Run(applicationContext)`. Passing an unshown `mainForm` to this overload and expecting it to stay hidden will not work — the framework shows it regardless of any `Visible = false` set beforehand. The correct mechanism for a genuinely-hidden startup is to construct `new ApplicationContext(mainForm)` (or `new ApplicationContext { MainForm = mainForm }`) and pass **that** to `Application.Run` instead — the `ApplicationContext` constructor only wires `mainForm`'s `Closed` event to end the message loop; it never calls `Show()`. This fully satisfies D-06's intent (message loop runs, form stays hidden, `Application.Exit()` still terminates everything correctly) — it is a mechanism substitution, not a scope change.

2. **`Form.Load` never fires unless the form is shown.** Today, `MainForm.RefreshUi()` (which will need to also set the tray icon/tooltip once this phase lands) runs from `OnLoad`. Under a `--tray` startup using the `ApplicationContext` fix above, `Show()` is never called, so `Load` never fires, so the tray icon would start in the wrong (default/uninitialized) state until the first toggle. The tray-icon-state initialization must be called explicitly and unconditionally at construction time (or right after, in `Program.cs`), not left to rely on `OnLoad`.

3. **`CloseReason.UserClosing` fires for both the X-button and `this.Close()` calls — but NOT for `Application.Exit()`.** This is good news for D-03/D-04: `CloseReason.ApplicationExitCall` is a distinct, official enum value raised specifically when `Application.Exit()` is invoked, so the tray Exit menu item can call `Application.Exit()` directly with zero extra flags — `FormClosing`'s existing `if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }` gate will correctly *not* intercept it, letting the real shutdown proceed. No `_isExiting` boolean or similar workaround is needed.

Beyond these three, the remaining research findings are smaller but still consequential: `ContextMenuStrip` is not just "recommended" over the legacy `ContextMenu` class — `ContextMenu`/`MenuItem` were never ported to .NET Core WinForms at all (removed since .NET Core 3.1/.NET 5), so `ContextMenuStrip` is the only real option on this project's `net10.0-windows` target. `NotifyIcon`'s `Click` event fires for **both** mouse buttons, not just left — D-02's "left-click restores" must be wired via `MouseClick` and an explicit `e.Button == MouseButtons.Left` check, or right-clicking to open the context menu will also restore the window as a side effect. `ShowBalloonTip`'s title/text are silently truncated at 63/255 characters respectively — usually harmless for this app's short checklist, but a long exception message in a `Failed` step's `Reason` could get cut off exactly where the diagnostic detail matters. `Microsoft.Win32.Registry` writes to `HKCU\...\Run` require no elevation (it's the current user's own hive). Getting this app's own exe path for the registry command string must use `Environment.ProcessPath` (.NET 6+), never `Assembly.Location`/`GetExecutingAssembly().Location`, which returns an empty string for code running from inside a `PublishSingleFile=true` bundle.

**Primary recommendation:** Host `NotifyIcon` + `ContextMenuStrip` directly on `MainForm` as designer-style fields constructed via the form's `IContainer components` (so `Form.Dispose(bool)`'s existing `components?.Dispose()` call cleans it up automatically as a defensive backstop). Gate `FormClosing` on `e.CloseReason == CloseReason.UserClosing` only. Relocate `FormatChecklist` (and a new one-line mode-title formatter) into `RigToggle.Core` so both the GUI `MessageBox` and the new toast reuse one tested implementation. Add `IAutostartConfigurator` to `RigToggle.Core.Abstractions` with a `WindowsAutostartConfigurator : IAutostartConfigurator` in `RigToggle.Windows` wrapping `Microsoft.Win32.Registry`, matching the existing `IMonitorController`/`IAudioController`/`IAppController` composition-root pattern. Fix D-06's startup path via `Application.Run(new ApplicationContext(mainForm))` gated on a tiny testable `StartupArgs.ShouldStartHidden(string[] args)` helper in `RigToggle.Core`.

## Architectural Responsibility Map

This app has no web/server tiers; "tiers" are its own established layered architecture (interface-per-concern in `RigToggle.Core.Abstractions`, Windows-specific implementations in `RigToggle.Windows`, composition root in `RigToggle.App/Program.cs`, per `02-RESEARCH.md`).

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Tray icon lifecycle (`NotifyIcon`, `ContextMenuStrip`, click routing) | UI layer (`MainForm`) | — | Pure WinForms component wiring; no existing precedent for a separate "tray controller" class in this codebase, and D-01/D-02 decisions already assume `MainForm`-hosted |
| Toggle-result / mode-name formatting for display (`FormatChecklist`, toast title) | Domain layer (`RigToggle.Core`) — **moved this phase** | UI layer (both `MainForm`'s `MessageBox` and the new toast consume it) | Currently mis-placed as a `MainForm` private static; it has zero WinForms dependency (`ToggleResult` is already a `Core` type) — relocating unlocks unit testing and eliminates the D-09 duplication risk |
| Toggle orchestration entry point | Domain layer (`RigToggle.Core.ToggleOrchestrator`, Phase 7, unchanged) | — | The tray menu's toggle handler is the second-ever caller after `MainForm.BtnToggle_Click` — validates Phase 7's extraction |
| Autostart registration (HKCU Run key read/write) | Adapter/infrastructure layer (**new** `RigToggle.Windows.WindowsAutostartConfigurator`) | UI layer (`SettingsForm`'s checkbox calls the interface, never touches `Microsoft.Win32.Registry` directly) | Matches the existing "UI never instantiates a concrete Windows adapter" anti-pattern guard (`02-RESEARCH.md`); also gives Phase 9/10 a reusable abstraction if either ever needs to query/report autostart state |
| Startup-mode decision (`--tray` flag → hidden vs. shown) | Composition root (`Program.cs`) delegating to a pure helper in `RigToggle.Core` | — | `Program.cs` is already the sole place concrete wiring happens; extracting the arg-parsing predicate into `Core` makes it unit-testable even though `Program.cs` itself has no test project |
| Toast display (`ShowBalloonTip` call itself) | UI layer (`MainForm`, wherever the tray menu's toggle handler lives) | — | Requires a live `NotifyIcon` instance; cannot be tested without WinForms, consistent with this codebase's existing "MainForm/SettingsForm are UI-wiring, verified by rig/manual testing, not unit tests" convention |

## Standard Stack

### Core
No new libraries — this phase uses only BCL/WinForms components already available via `net10.0-windows` (already referenced by `RigToggle.App.csproj`) and `Microsoft.Win32.Registry`, part of the BCL on `net10.0` since .NET Core (no separate NuGet package required on modern .NET — the standalone `Microsoft.Win32.Registry` NuGet package is only needed for .NET Standard 2.0 targets, not applicable here).

| API | Namespace | Purpose | Why Standard |
|-----|-----------|---------|---------------|
| `System.Windows.Forms.NotifyIcon` | `System.Windows.Forms` | Tray icon, tooltip, balloon notifications | The only tray-icon component in WinForms; explicitly named in `CLAUDE.md`'s stack recommendation ("WinForms' built-in `NotifyIcon` component covers this natively — no extra library needed") |
| `System.Windows.Forms.ContextMenuStrip` | `System.Windows.Forms` | Right-click tray menu | `[VERIFIED via WebSearch, cross-checked against github.com/dotnet/docs #15813]`: the legacy `System.Windows.Forms.ContextMenu`/`MenuItem` classes were **not ported** to .NET Core WinForms (removed as of .NET Core 3.1/.NET 5) — `ContextMenuStrip`/`ToolStripMenuItem` are the only classes that exist on this project's `net10.0-windows` target, not merely "the modern recommendation" |
| `Microsoft.Win32.Registry` / `RegistryKey` | `Microsoft.Win32` | Read/write the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` autostart value | BCL on Windows; `HKEY_CURRENT_USER` writes never require elevation since it is the invoking user's own hive — matches the app's existing non-elevated execution model (`CLAUDE.md`, "Deliberately no elevation manifest") `[CITED: learn.microsoft.com/dotnet/api/microsoft.win32.registry]` |
| `System.Windows.Forms.ApplicationContext` | `System.Windows.Forms` | Correct mechanism for a hidden-at-startup message loop (D-06 fix) | `Application.Run(Form)`'s own documentation states it "makes the specified form visible" — `ApplicationContext`'s `Form`-argument constructor only wires the `Closed` event, it does not call `Show()`, making it the documented way to run a message loop without an initial visible form `[CITED: learn.microsoft.com/dotnet/api/system.windows.forms.application.run, learn.microsoft.com/dotnet/api/system.windows.forms.applicationcontext]` |
| `Environment.ProcessPath` | `System` | Resolve this app's own exe path for the Registry Run command string | `.NET 6+` API designed exactly for this; `Assembly.Location`/`GetExecutingAssembly().Location` returns an empty string for assemblies bundled inside a `PublishSingleFile=true` host (this project's actual publish mode, per `RigToggle.App.csproj`/`win-x64.pubxml`) `[CITED: learn.microsoft.com/dotnet/core/deploying/single-file/warnings/il3000]` |
| `System.Windows.Forms.CloseReason` (enum) | `System.Windows.Forms` | Distinguish X-button/`Close()` (`UserClosing`) from `Application.Exit()` (`ApplicationExitCall`) in `FormClosing` | Official, documented enum member specifically for this distinction — no custom flag needed `[CITED: learn.microsoft.com/dotnet/api/system.windows.forms.closereason]` |

### Supporting
None needed beyond what's already referenced. `System.Drawing.Icon` (already transitively available via `UseWindowsForms=true`) is required for `NotifyIcon.Icon` — see Common Pitfall 3 for the correct way to source two icon instances (normal/rig).

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `NotifyIcon.ShowBalloonTip` (D-07, locked) | Windows App SDK / MSIX toast (`Microsoft.Windows.SDK.NET` toast APIs) | Explicitly out of scope per `REQUIREMENTS.md`'s Out-of-Scope table — requires AUMID/shortcut registration that conflicts with the standalone self-contained-exe distribution model. Not reconsidered here; D-07 is correct. |
| `Microsoft.Win32.Registry` HKCU Run key (D-05, locked) | Windows Task Scheduler (`schtasks`/`TaskScheduler` NuGet) | Explicitly out of scope per `REQUIREMENTS.md`'s Out-of-Scope table (elevation/UIPI risk, per the v1.0 H9 debug session). Not reconsidered here; D-05 is correct. |
| `ApplicationContext(mainForm)` for hidden startup | `mainForm.Visible = false` set before `Application.Run(mainForm)` | Does not work — `Application.Run(Form)` unconditionally shows the form per its own documentation ("makes the specified form visible"), regardless of any property set beforehand. Confirmed against official docs, not just inferred. |
| `ApplicationContext(mainForm)` for hidden startup | `Application.Run()` (no-arg overload) + manual `ExitThread()`/`Application.Exit()` wiring | Functionally possible (Microsoft's own "Starting an application without showing a form" article describes this as the *less* elegant of two options) but fragile: "a missed call to `Application.Exit()` means your application will keep running even though there is no UI present." `ApplicationContext(mainForm)` gets the same hidden-start behavior for free while still tying `mainForm`'s eventual `Closed` event to thread exit, which is more consistent with this app's existing lifetime model. |
| `MouseClick` + `e.Button == MouseButtons.Left` (D-02 implementation) | `NotifyIcon.Click` (generic) | `Click` fires for **both** left and right button per community-verified WinForms behavior — wiring "restore window" to `Click` would also restore the window on every right-click that opens the context menu, a visible, confusing bug. `MouseClick` with an explicit button check is the documented-correct pattern. |

**Installation:** None — `NotifyIcon`, `ContextMenuStrip`, `ApplicationContext`, `CloseReason` are already available via `RigToggle.App.csproj`'s existing `UseWindowsForms=true`; `Microsoft.Win32.Registry` and `Environment.ProcessPath` are BCL, already implicitly available on `net10.0`/`net10.0-windows`.

## Package Legitimacy Audit

**Not applicable.** This phase introduces zero new external packages (no NuGet, no npm, no pip). Every mechanism is either WinForms-builtin (already referenced via `UseWindowsForms=true`) or BCL (`Microsoft.Win32.Registry`, `System.Diagnostics.Process`, `Environment.ProcessPath`). Skip the Package Legitimacy Gate protocol entirely for this phase's plan.

## Architecture Patterns

### System Architecture Diagram

```
                    ┌───────────────────────────────────────────────┐
                    │              Program.cs (composition root)     │
                    │  args contains "--tray"?                        │
                    │    NO  → Application.Run(mainForm)               │
                    │           (shows normally, existing behavior)   │
                    │    YES → Application.Run(new ApplicationContext │
                    │           (mainForm))  ← D-06 FIX (see Pitfall) │
                    │  Explicitly calls mainForm.InitializeTrayState() │
                    │  BEFORE Run() either way — Load never fires      │
                    │  when the hidden path is taken (Pitfall 6)       │
                    └───────────────────┬───────────────────────────┘
                                        │ constructs / owns
                                        ▼
                    ┌───────────────────────────────────────────────┐
                    │                    MainForm                     │
                    │  ┌───────────────────────────────────────────┐ │
                    │  │ NotifyIcon (components-owned, D-04)         │ │
                    │  │  .Icon        ← swapped per mode (D-01)     │ │
                    │  │  .Text        ← tooltip per mode (D-01)     │ │
                    │  │  .ContextMenuStrip  ← Switch/Settings/Exit  │ │
                    │  │  .MouseClick  ← Left → Restore/Focus (D-02) │ │
                    │  │  .ShowBalloonTip(...)  ← tray-triggered      │ │
                    │  │      toggle only (D-08), unconditional       │ │
                    │  └───────────────────────────────────────────┘ │
                    │  FormClosing:                                    │
                    │    CloseReason == UserClosing                    │
                    │      → e.Cancel = true; Hide();  (D-03)          │
                    │    else (ApplicationExitCall, WindowsShutDown,   │
                    │      TaskManagerClosing, ...) → let it proceed   │
                    │  Tray "Switch to Rig/Normal Mode" click:          │
                    │    → same call pattern as BtnToggle_Click,        │
                    │      but result → ToggleResultFormatter (Core)    │
                    │      → ShowBalloonTip instead of MessageBox        │
                    │  Tray "Exit" click:                                │
                    │    → notifyIcon.Visible = false; Application.Exit()│
                    └───────────────────┬───────────────────────────┘
                                        │ calls (unchanged from Phase 7)
                                        ▼
                    ┌───────────────────────────────────────────────┐
                    │        ToggleOrchestrator (Phase 7, unchanged)  │
                    └───────────────────┬───────────────────────────┘
                                        ▼
                    ┌───────────────────────────────────────────────┐
                    │  IMonitorController / IAudioController /         │
                    │  IAppController (unchanged)                       │
                    │  IAutostartConfigurator (NEW, this phase)          │
                    │    → WindowsAutostartConfigurator (RigToggle.     │
                    │      Windows) wraps Microsoft.Win32.Registry        │
                    │      HKCU\...\Run, called from SettingsForm's        │
                    │      new "Start with Windows" checkbox Save path     │
                    └───────────────────────────────────────────────┘

                    ┌───────────────────────────────────────────────┐
                    │        RigToggle.Core (moved this phase)        │
                    │  ToggleResultFormatter.FormatChecklist(result)   │
                    │    ← relocated from MainForm private static      │
                    │  ToggleResultFormatter.FormatModeTitle(isRig)     │
                    │  StartupArgs.ShouldStartHidden(string[] args)      │
                    │    ← tiny pure helper, unit-testable even though   │
                    │      Program.cs itself has no test project          │
                    └───────────────────────────────────────────────┘
```

Reading the diagram: `Program.cs` decides the startup path (hidden vs. shown) using the `ApplicationContext` fix, and explicitly primes the tray icon's initial state because `Load` cannot be relied on in the hidden path. `MainForm` owns all tray UI and gates its own `FormClosing` on the specific `CloseReason` value, so `Application.Exit()` from the tray's own "Exit" item is never accidentally intercepted by the same guard that redirects the X button. Everything below `MainForm` (orchestrator, adapters) is unchanged from Phase 7 except for the one new `IAutostartConfigurator` adapter, which follows the exact same interface-in-Core/implementation-in-Windows split as the three existing controllers.

### Recommended Project Structure
```
src/RigToggle.Core/
├── ToggleOrchestrator.cs              # UNCHANGED (Phase 7)
├── ToggleResultFormatter.cs           # NEW — relocated FormatChecklist + new FormatModeTitle
├── StartupArgs.cs                     # NEW — tiny testable "--tray" predicate
├── Abstractions/
│   ├── IMonitorController.cs          # UNCHANGED
│   ├── IAudioController.cs            # UNCHANGED
│   ├── IAppController.cs              # UNCHANGED
│   └── IAutostartConfigurator.cs      # NEW — IsEnabled()/Enable()/Disable()
└── Models/                            # UNCHANGED (ToggleResult, etc.)

src/RigToggle.Windows/
├── WindowsMonitorController.cs        # UNCHANGED
├── WindowsAudioController.cs          # UNCHANGED
├── WindowsAppController.cs            # UNCHANGED
└── WindowsAutostartConfigurator.cs    # NEW — Microsoft.Win32.Registry HKCU\...\Run wrapper

src/RigToggle.App/
├── MainForm.cs                        # MODIFIED — NotifyIcon/ContextMenuStrip, FormClosing gate,
│                                       #   tray toggle handler, Exit handler
├── MainForm.Designer.cs               # MODIFIED — NotifyIcon/ContextMenuStrip declared via components
├── SettingsForm.cs                    # MODIFIED — "Start with Windows" checkbox wired to
│                                       #   IAutostartConfigurator (mirrors chkEnableDebugLogging)
├── SettingsForm.Designer.cs           # MODIFIED — new checkbox control
├── Program.cs                         # MODIFIED — args parameter, ApplicationContext startup path,
│                                       #   constructs WindowsAutostartConfigurator
└── Resources/                         # NEW (or embedded resource folder per UI-SPEC)
    ├── normal.ico                     # NEW — UI-SPEC asset
    └── rig.ico                        # NEW — UI-SPEC asset

src/RigToggle.Tests/
├── ToggleOrchestratorTests.cs         # UNCHANGED
├── ToggleResultFormatterTests.cs      # NEW — pure formatting logic, fully unit-testable
└── StartupArgsTests.cs                # NEW — "--tray" parsing, fully unit-testable

src/RigToggle.Windows.Tests/
└── WindowsAutostartConfiguratorTests.cs  # NEW, optional — see Testability discussion below;
                                           #   requires a real HKCU write, so likely rig-verified
                                           #   rather than CI-run (see Open Questions)
```

### Pattern 1: `FormClosing` gated on `CloseReason`, not a custom flag (D-03/D-04)
**What:** Cancel and hide only for `CloseReason.UserClosing`; let every other reason (`ApplicationExitCall`, `WindowsShutDown`, `TaskManagerClosing`, etc.) proceed untouched.
**When to use:** Any WinForms "minimize to tray on X" implementation — this is the standard, documented pattern, not an app-specific workaround.
**Example:**
```csharp
// Source: pattern confirmed against learn.microsoft.com/dotnet/api/system.windows.forms.closereason
private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
{
    if (e.CloseReason == CloseReason.UserClosing)
    {
        // TRAY-01/D-03: X button, Alt+F4, or a plain this.Close() call — redirect to tray.
        // Deliberately does NOT check WindowsShutDown/TaskManagerClosing/ApplicationExitCall —
        // those must be allowed to proceed, or the OS/Task Manager/our own Exit menu item
        // would be unable to actually close the app.
        e.Cancel = true;
        Hide();
        return;
    }

    // ApplicationExitCall (our own tray "Exit" menu item calling Application.Exit()),
    // WindowsShutDown, TaskManagerClosing, etc. — let the close proceed normally.
    notifyIcon.Visible = false; // D-04: belt-and-suspenders ghost-icon prevention
}
```
Because `CloseReason.ApplicationExitCall` is raised specifically for `Application.Exit()` calls, the tray "Exit" menu item needs **no special-case flag** — it just calls `Application.Exit()` directly and this same handler correctly lets it through.

### Pattern 2: Relocate `FormatChecklist` to `RigToggle.Core` for real reuse (D-09, Claude's Discretion)
**What:** Move the existing private static `MainForm.FormatChecklist(ToggleResult)` into a new `RigToggle.Core.ToggleResultFormatter` class, and add a companion `FormatModeTitle(bool isInRigMode)` for the toast's title ("Switched to Rig Mode"/"Switched to Normal Mode").
**When to use:** Any time the same `ToggleResult` needs to be rendered in more than one UI surface (today: GUI `MessageBox` + tray `ShowBalloonTip`; tomorrow: Phase 9/10's hotkey/CLI toasts will need the exact same formatting).
**Example:**
```csharp
// Source: relocated verbatim from src/RigToggle.App/MainForm.cs lines 191-202,
// widened from `private static` (MainForm-only) to `public static` (Core, shared)
namespace RigToggle.Core;

public static class ToggleResultFormatter
{
    public static string FormatChecklist(ToggleResult result) =>
        string.Join(
            Environment.NewLine,
            result.Steps.Select(step => step.Outcome switch
            {
                ToggleStepOutcome.Succeeded => $"{step.StepName}: OK",
                ToggleStepOutcome.Failed => $"{step.StepName}: FAILED ({step.Reason})",
                ToggleStepOutcome.NotAttempted => $"{step.StepName}: not attempted",
                _ => $"{step.StepName}: unknown",
            }));

    // NOTIF-01/D-09: shared mode-title wording for the toast, matching the GUI's own
    // btnToggle.Text / lblMode.Text phrasing convention.
    public static string FormatModeTitle(bool isInRigMode) =>
        isInRigMode ? "Switched to Rig Mode" : "Switched to Normal Mode";
}
```
`MainForm.BtnToggle_Click`'s existing `MessageBox.Show($"...\n\n{FormatChecklist(result)}", ...)` call becomes `ToggleResultFormatter.FormatChecklist(result)`; the new tray toggle handler calls the same method for its `ShowBalloonTip` text. This is now directly unit-testable in `RigToggle.Tests` with zero WinForms dependency — the first test coverage this formatting logic has ever had.

### Pattern 3: Autostart as a Core interface + Windows adapter, matching existing convention
**What:** `IAutostartConfigurator` in `RigToggle.Core.Abstractions` (three members: `bool IsEnabled()`, `void Enable()`, `void Disable()`), implemented by `WindowsAutostartConfigurator` in `RigToggle.Windows` using `Microsoft.Win32.Registry.CurrentUser`.
**When to use:** Matches the codebase's established "UI never instantiates a concrete Windows adapter directly" rule (`02-RESEARCH.md` Anti-Pattern 2) already followed by `IMonitorController`/`IAudioController`/`IAppController`. `SettingsForm` depends on the interface only, constructed and injected from `Program.cs`'s composition root exactly like the other three controllers.
**Example:**
```csharp
// Source: matches existing WindowsAppController.cs's "adapter behind an interface" shape
namespace RigToggle.Windows;

public sealed class WindowsAutostartConfigurator : IAutostartConfigurator
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RigToggle";

    public bool IsEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable()
    {
        // Environment.ProcessPath (NOT Assembly.Location — see Common Pitfall 5) is the
        // correct way to resolve this app's own exe path when running from inside a
        // PublishSingleFile=true bundle (RigToggle.App.csproj/win-x64.pubxml).
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("Could not resolve the running executable's path.");
        }

        // Re-write unconditionally every time Enable() is called (even if already enabled) —
        // self-heals a stale path left over from a prior install/relocation, matching this
        // codebase's existing "settings save always writes the full current state" convention
        // (see SettingsForm.BtnSaveSettings_Click).
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{exePath}\" --tray");
    }

    public void Disable()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```
`SettingsForm_Load` calls `_autostartConfigurator.IsEnabled()` to set the checkbox's initial state (registry is the source of truth — no mirrored `AppSettings.StartWithWindows` boolean needed, avoiding drift if the value is deleted externally, e.g. via Windows' own Startup Apps settings page or Autoruns). `BtnSaveSettings_Click` calls `Enable()`/`Disable()` based on the checkbox's final state, exactly mirroring `chkEnableDebugLogging`'s existing "no separate Save step beyond the existing Save button" pattern from D-05.

### Pattern 4: Correct hidden-startup mechanism for `--tray` (D-06, corrected)
**What:** Replace `Application.Run(mainForm)` with `Application.Run(new ApplicationContext(mainForm))` for the `--tray` path only; keep the existing direct call for normal startup.
**When to use:** Exactly D-06's scenario — starting the WinForms message loop without popping up the main window.
**Example:**
```csharp
// Source: pattern confirmed against learn.microsoft.com/dotnet/api/system.windows.forms.application.run
// ("Run(Form) ... makes the specified form visible") and
// learn.microsoft.com/dotnet/api/system.windows.forms.applicationcontext
// ("By default, the ApplicationContext listens to the Closed event on the application's
// main Form, then exits the thread's message loop" — no Show() call implied or performed).
[STAThread]
static void Main(string[] args)
{
    ApplicationConfiguration.Initialize();

    // ... existing composition root wiring (settingsStore, toggleOrchestrator, etc.) ...

    bool startHidden = StartupArgs.ShouldStartHidden(args); // Core, unit-testable

    var mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, autostartConfigurator);

    // Pitfall 6: Form.Load never fires unless Show() is called — the tray icon's initial
    // state must be primed explicitly, regardless of which path below is taken.
    mainForm.InitializeTrayState();

    if (startHidden)
    {
        // D-06 fix: Application.Run(Form) unconditionally shows the form (see Pattern
        // header). ApplicationContext's Form-argument constructor does NOT call Show() —
        // it only arranges for the form's eventual Closed event to end the message loop.
        Application.Run(new ApplicationContext(mainForm));
    }
    else
    {
        Application.Run(mainForm); // existing behavior, unchanged — shows normally
    }
}
```
```csharp
// Source: RigToggle.Core — new, tiny, pure, unit-testable helper
namespace RigToggle.Core;

public static class StartupArgs
{
    private const string TrayFlag = "--tray";

    public static bool ShouldStartHidden(string[] args) =>
        args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);
}
```

### Anti-Patterns to Avoid
- **Wiring `NotifyIcon.Click` for the "left-click restores" behavior (D-02):** `Click` fires for both left and right mouse buttons — using it directly makes right-clicking to open the context menu *also* restore the window as an unwanted side effect. Use `MouseClick` and check `e.Button == MouseButtons.Left`.
- **Trusting `Application.Run(mainForm)` to respect a pre-set `Visible = false` (D-06):** It does not — the framework calls `Show()` internally regardless. Use `ApplicationContext(mainForm)` instead for the hidden-startup path (Pattern 4).
- **Relying on `MainForm.OnLoad`/`Form_Load` to initialize the tray icon's mode-correct state:** `Load` never fires unless the form is actually shown at least once — under `--tray` startup it may never fire for the entire session. Initialize tray state explicitly and unconditionally (constructor, or immediately after construction in `Program.cs`).
- **A custom `_isExiting` boolean to distinguish the tray "Exit" click from the X button (D-03/D-04):** Unnecessary — `CloseReason.ApplicationExitCall` already exists specifically for this and is set automatically whenever `Application.Exit()` is called, regardless of call site.
- **Loading a `Bitmap` and converting it to an `Icon` via `Icon.FromHandle(bitmap.GetHicon())` without disposing the raw HICON:** `Icon.FromHandle` does not take ownership of the handle it wraps, so `Icon.Dispose()` alone will not call `DestroyIcon` on it — each call leaks a GDI handle, and Windows processes have a hard 10,000-GDI-object ceiling. If a genuine `.ico` file/embedded resource is used instead (the recommended approach per D-01/UI-SPEC), this pitfall does not arise at all.
- **A mirrored `AppSettings.StartWithWindows` boolean as the checkbox's source of truth:** Can drift from the actual registry state (e.g. user disables it via Windows' own Startup Apps settings page, or a stale value survives an app uninstall/reinstall at a different path). Read `IAutostartConfigurator.IsEnabled()` directly on `SettingsForm_Load` instead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tray icon / balloon notification | A custom Win32 `Shell_NotifyIcon` P/Invoke wrapper | `System.Windows.Forms.NotifyIcon` | Already does exactly this, including the native message-window creation this codebase's own `WindowsAppController.cs` comments confirm exists ("`System.Windows.Forms.NotifyIcon` creates its own top-level, owner-less, permanently-invisible native window purely to receive `Shell_NotifyIcon` callback messages") — reimplementing it would duplicate BCL code with more surface area for bugs |
| Right-click context menu | A hand-rolled popup `Form` positioned at the cursor | `System.Windows.Forms.ContextMenuStrip` assigned to `NotifyIcon.ContextMenuStrip` | Standard, and the *only* option — `ContextMenu`/`MenuItem` (the older alternative) do not exist on `net10.0-windows` at all |
| Distinguishing X-button-close from programmatic/Exit-menu close | A custom `_isExiting`/`_allowClose` boolean flag | `FormClosingEventArgs.CloseReason` (`UserClosing` vs. `ApplicationExitCall`) | The BCL already models exactly this distinction as a first-class enum; a hand-rolled flag would just reimplement (with more room for bugs, e.g. forgetting to reset it) what the framework already tracks correctly |
| Autostart registration | A hand-written `.lnk` shortcut writer into the Startup folder | `Microsoft.Win32.Registry` HKCU `...\Run` value | D-05 already locks this choice; `Run` is a single `SetValue` call vs. COM shortcut (`IShellLink`) interop for the Startup-folder alternative — strictly simpler for the same effect |
| Resolving this app's own exe path for the Run value | `Process.GetCurrentProcess().MainModule?.FileName` (works, but heavier — spins up a full `Process` object for something the BCL already exposes as a plain string) | `Environment.ProcessPath` | Purpose-built for exactly this (.NET 6+), avoids `Assembly.Location`'s single-file-publish empty-string trap entirely, and needs no `Process` object lifecycle management |

**Key insight:** Every mechanism this phase needs already exists in WinForms/BCL form fit for purpose — the entire engineering risk is *correct usage* (right event, right enum member, right API overload), not missing tooling. All five "Don't Hand-Roll" rows above are really "don't reach for a lower-level primitive when a purpose-built one already exists one layer up."

## Runtime State Inventory

Not applicable — this phase adds new capability (tray residency, autostart, toast) rather than renaming/refactoring/migrating existing state. No existing stored data, live service config, OS-registered state, secrets, or build artifacts reference concepts this phase changes the name/shape of. (The new HKCU `Run` value and the new `%LOCALAPPDATA%\RigToggle\settings.json` checkbox field are themselves the state being *introduced*, not migrated.)

## Common Pitfalls

### Pitfall 1: `CloseReason.UserClosing` fires for both the X button AND a plain `this.Close()` call
**What goes wrong:** A naive assumption that `FormClosing` can distinguish "user clicked X" from "code called `Close()`" using `CloseReason` alone is wrong for that specific pair — both report `UserClosing`. This does not affect this phase's design (nothing in this phase ever calls `mainForm.Close()` programmatically — the tray Exit path uses `Application.Exit()`, which correctly reports the distinct `ApplicationExitCall`), but it is a documented WinForms limitation worth knowing so a future editor doesn't try to add a programmatic "soft close" that expects to be distinguishable from the X button via `CloseReason` alone.
**Why it happens:** `Close()` and the X button both funnel through the same underlying Win32 `WM_CLOSE`/`SC_CLOSE` path, so the BCL cannot tell them apart.
**How to avoid:** For this phase, no action needed — `Application.Exit()` (used by the tray Exit item) is a genuinely distinct `CloseReason.ApplicationExitCall`, which is all that's required here. If a future phase needs to distinguish `Close()` from the X button specifically, an explicit flag would be needed then — not now.
**Warning signs:** A future bug report where a programmatic `mainForm.Close()` call gets redirected to "hide to tray" instead of actually closing — that would indicate someone tried to close the form via `Close()` rather than `Application.Exit()`.

### Pitfall 2: `NotifyIcon.Click` fires for both mouse buttons, silently double-triggering
**What goes wrong:** Wiring `notifyIcon.Click += (s, e) => RestoreAndFocus();` makes the window restore on *every* click, including the right-click that's supposed to only open the context menu.
**Why it happens:** WinForms' `NotifyIcon.Click` event is button-agnostic — it doesn't expose which button was pressed at all (that's `EventArgs`, not `MouseEventArgs`).
**How to avoid:** Use `NotifyIcon.MouseClick` (which provides `MouseEventArgs` with a `.Button` property) and check `e.Button == MouseButtons.Left` before restoring.
**Warning signs:** Right-clicking the tray icon visibly restores/flashes the main window in addition to showing the context menu.

### Pitfall 3: `NotifyIcon.Icon` requires a real `System.Drawing.Icon`, and converting a `Bitmap` to one leaks GDI handles
**What goes wrong:** `NotifyIcon.Icon` is typed `Icon`, not `Image`/`Bitmap`. A common shortcut — `Icon.FromHandle(myBitmap.GetHicon())` — produces a working `Icon` at first, but each call leaks the underlying native `HICON` because `Icon.FromHandle`'s result does not own the handle it wraps, so disposing the `Icon` never calls `DestroyIcon` on it. Doing this repeatedly (e.g. redrawing an icon on every mode switch instead of using two pre-made icons) will eventually exhaust the process's ~10,000-object GDI handle ceiling.
**Why it happens:** `.NET`'s `Icon`/`Bitmap` GDI wrapper classes distinguish "owns the handle" from "wraps an externally-owned handle," and `GetHicon()`/`FromHandle()` is exactly the boundary where that distinction gets lost if not handled explicitly.
**How to avoid:** Per D-01, use two genuine, pre-made `.ico` files (the UI-SPEC's actual deliverable for this phase) embedded as `EmbeddedResource` items in `RigToggle.App.csproj` and loaded once at startup via `new Icon(assembly.GetManifestResourceStream(resourceName))` — no bitmap-to-icon conversion, no leak, and no loose files sitting outside the single-file publish output. If a future phase ever needs a programmatically-drawn icon instead, the correct pattern is `IntPtr hIcon = bitmap.GetHicon(); var icon = (Icon)Icon.FromHandle(hIcon).Clone(); DestroyIcon(hIcon);` (P/Invoke `user32.dll DestroyIcon`) — clone before destroying the source handle.
**Warning signs:** `System.ComponentModel.Win32Exception`/GDI-related crashes after prolonged runtime, or Task Manager showing steadily climbing "GDI objects" count for the process.

### Pitfall 4: `ShowBalloonTip`'s title/text are silently truncated (63 / 255 characters)
**What goes wrong:** `NotifyIcon.BalloonTipTitle` truncates past 63 characters and `BalloonTipText` truncates past 255 — with no exception, no visible ellipsis marker, just silently cut-off text.
**Why it happens:** These map to the underlying Win32 `Shell_NotifyIcon` structure's fixed-size character buffers (`szInfoTitle[64]`, `szInfo[256]`), a legacy Win32 API constraint that `NotifyIcon` does not attempt to work around.
**How to avoid:** For this app's actual content, the title ("Switched to Rig Mode"/"Switched to Normal Mode", ~21 chars) is nowhere near the 63-char limit. The body (`ToggleResultFormatter.FormatChecklist`, three short lines like "Monitor: OK") is normally well under 255 chars too — but a `Failed` step's `Reason` field is populated from `ex.Message` (per `ToggleStepResult`'s own doc comment), and some Windows API exception messages (e.g. a verbose CCD/COM HRESULT description) can run well past 255 characters on their own. Unlike the GUI's `MessageBox` (D-09's comparison point), which has no such limit, the toast body could silently truncate exactly the diagnostic detail a partial-failure toast exists to convey. Consider truncating/summarizing long `Reason` strings defensively before passing them to `ShowBalloonTip` (e.g. `reason[..Math.Min(reason.Length, N)]` with an explicit "…" marker) — left to the planner as an explicit design decision, not silently inherited from the MessageBox path.
**Warning signs:** A rig test where a deliberately-failed step (e.g. yank the rig monitor mid-toggle) produces a toast whose failure reason looks cut off mid-sentence, compared against the same failure's full `MessageBox` text.

### Pitfall 5: `Assembly.Location` returns an empty string inside a `PublishSingleFile=true` bundle
**What goes wrong:** The commonly-reached-for `Assembly.GetExecutingAssembly().Location` (or `typeof(Program).Assembly.Location`) returns `""` for any assembly loaded from inside a single-file bundle — which is exactly this app's actual publish mode (`RigToggle.App.csproj`'s `win-x64.pubxml`, `PublishSingleFile=true`). Writing that empty string into the Registry `Run` value would silently produce a broken autostart entry (`"" --tray`, which Windows would fail to launch at boot with no visible error until the user notices the app never started).
**Why it happens:** In single-file publish mode, bundled assemblies are loaded from memory, not extracted to a real file with a real path on disk — there is no meaningful "assembly location" for `Assembly.Location` to return.
**How to avoid:** Use `Environment.ProcessPath` (available since .NET 6) instead — it resolves the actual running host executable's path regardless of single-file bundling. This is the specific, purpose-built replacement Microsoft's own compatibility documentation recommends for this exact single-file scenario.
**Warning signs:** The autostart checkbox appears to save successfully, but the app never actually launches at the next Windows login — inspecting the `Run` value's data with `regedit` or Autoruns would show an empty or malformed command string.

### Pitfall 6: `Form.Load` never fires unless the form is actually shown at least once
**What goes wrong:** `MainForm`'s existing `RefreshUi()` call (today wired to `OnLoad`) is the only place mode-derived UI state gets initialized. Once this phase adds "set the tray icon/tooltip to match current mode" to that same refresh logic, a `--tray` autostart launch (which, per the D-06 fix, never calls `Show()`) would leave the tray icon in whatever default/uninitialized state it was constructed with until the very first toggle — a visibly wrong icon for however long the user leaves the app tray-resident before their first toggle.
**Why it happens:** WinForms' `Load` event is tied to the form's window handle being created as part of becoming visible (`Show()`/`ShowDialog()`), not to object construction — a `Form` that is constructed but never shown never receives `Load` at all in that session.
**How to avoid:** Ensure the tray-icon-state initialization path is called explicitly and unconditionally — either directly from `MainForm`'s constructor (after `InitializeComponent()`), or as an explicit `mainForm.InitializeTrayState()` call from `Program.cs` immediately after construction, before either `Application.Run` branch (see Pattern 4's code example). Do not rely on it happening "eventually" via `OnLoad` for the hidden-startup path.
**Warning signs:** Starting the app via the Registry `Run` value (or manually testing with `RigToggle.exe --tray`) shows a default/wrong-mode tray icon immediately after boot, which only corrects itself after the first manual toggle from the context menu.

## Code Examples

Verified patterns from official sources, adapted to this codebase's existing conventions:

### Existing `MainForm.Designer.cs` `components` field — currently dead code, becomes load-bearing this phase
```csharp
// Source: src/RigToggle.App/MainForm.Designer.cs lines 6-21 (existing code)
private System.ComponentModel.IContainer components = null;

protected override void Dispose(bool disposing)
{
    if (disposing && (components != null))
    {
        components.Dispose();
    }
    base.Dispose(disposing);
}
```
Today `components` is declared but never instantiated (`= null`), so this `Dispose(bool)` block's `components.Dispose()` call is unreachable dead code. This phase should instantiate it (`this.components = new System.ComponentModel.Container();`) and construct `NotifyIcon` with it (`this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);`), which makes this existing, already-correct disposal path a genuine defensive backstop against the ghost-tray-icon bug (D-04) — on top of (not instead of) the explicit `notifyIcon.Visible = false` set in the `FormClosing`/Exit-menu handler.

### Official `ApplicationContext`-for-hidden-form pattern (D-06 fix, condensed from Microsoft's own historical guidance)
```csharp
// Source: learn.microsoft.com/en-us/archive/blogs/jfoscoding/starting-an-application-without-showing-a-form
// (still-accurate description of Application.Run(Form)'s internal wrapping, corroborated by
// the current official Application.Run(Form) and ApplicationContext docs)
//
// What Application.Run(mainForm) does internally (why it can't stay hidden):
//   Form1 form1 = new Form1();
//   ApplicationContext applicationContext = new ApplicationContext();
//   applicationContext.MainForm = form1;
//   applicationContext.MainForm.Show();      // <-- this is the unconditional Show() call
//   Application.Run(applicationContext);
//
// What to do instead for a genuinely-hidden start:
var context = new ApplicationContext(mainForm); // does NOT call Show()
Application.Run(context); // message loop runs; ends when mainForm's Closed event fires
```

### Existing `WindowsAppController.cs` NotifyIcon-native-window awareness (directly relevant precedent already in this codebase)
```
// Source: src/RigToggle.Windows/WindowsAppController.cs, FindBestMainWindow's doc comment
// (1) System.Windows.Forms.NotifyIcon creates its own top-level, owner-less,
//     permanently-invisible native window purely to receive Shell_NotifyIcon
//     callback messages -- it satisfies the same owner==0 filter as a real main
//     form but has no caption and a zero/near-zero rect.
```
This codebase already discovered (during Phase 3's window-focus debugging) that `NotifyIcon` creates its own hidden top-level window. That existing knowledge is directly relevant here: it confirms `NotifyIcon`'s message-pump plumbing works independently of `MainForm`'s own window/handle lifecycle, which is *why* the tray icon can be shown and receive clicks even while `MainForm` itself stays `Visible = false` under `--tray` startup — the two windows are independent as far as the OS shell is concerned.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `System.Windows.Forms.ContextMenu`/`MenuItem` | `System.Windows.Forms.ContextMenuStrip`/`ToolStripMenuItem` | .NET Core 3.1 / .NET 5 (the legacy classes were not ported to WinForms Core at all) | Not a style preference for this project — `ContextMenu` does not exist on `net10.0-windows`, so `ContextMenuStrip` is the only option, period |
| `NotifyIcon.ShowBalloonTip`'s `timeout` parameter actually controlling display duration | Timeout parameter is effectively ignored/deprecated; display duration now follows system accessibility settings | Windows 7+ (long-standing, not new to Windows 11) | Passing a specific timeout value to `ShowBalloonTip` (the overload that accepts one) has no reliable effect — don't design any behavior around a specific toast duration |
| Balloon tips as always-visible until dismissed (older Windows versions) | Windows 11 renders `ShowBalloonTip` as a transient toast/banner; the message does not persist in the Notification Center once its timeout elapses (unlike Windows 10, where it did) | Windows 11 | If a user glances away right when a tray-triggered toggle toast appears, they may miss it entirely on Windows 11 with no way to review it afterward from the Notification Center — worth noting as a UX limitation inherent to `ShowBalloonTip` (out of scope to fix in this phase; `NOTIF-01`'s own out-of-scope table already rules out the packaged-toast alternative that would fix this) |

**Deprecated/outdated:** `System.Windows.Forms.ContextMenu`/`MenuItem` (removed from WinForms Core entirely, not merely deprecated-but-present). `Icon.FromHandle`-from-`Bitmap` as a general pattern for `NotifyIcon.Icon` (works but leaks unless manually paired with `DestroyIcon` — avoidable entirely by using real `.ico` assets, D-01's actual plan).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The UI-SPEC step for this phase will produce two real `.ico` files (or equivalent embeddable icon assets) rather than opting for a programmatically-drawn icon. This research recommends the `.ico`-embedded-resource approach specifically because it sidesteps the GDI-leak pitfall (Pitfall 3) entirely, but the actual UI-SPEC.md (not yet written) could in principle choose a runtime-drawn approach instead. | Common Pitfalls (Pitfall 3), Architecture Patterns (Recommended Project Structure) | Low — if the UI-SPEC does choose a programmatic/drawn icon instead of static `.ico` files, this research's fallback pattern (`GetHicon()` + clone + `DestroyIcon`) is already documented as the correct-but-more-complex alternative; no design rethink needed, just a different (already-specified) implementation path |
| A2 | `ApplicationContext(mainForm)`'s `Closed`-event wiring will correctly end the process when `Application.Exit()` is called from the tray Exit menu item, even though `mainForm` was never `Show()`n and therefore its own `Closed` event semantics under "never shown, then `Application.Exit()` called" are inferred from documented `ApplicationContext`/`Application.Exit()` behavior rather than directly tested against this exact never-shown scenario in this session (no Windows execution environment was available to this research agent to verify empirically). | Architecture Patterns Pattern 4 | Medium — if `Application.Exit()` somehow doesn't cleanly terminate a message loop started via `ApplicationContext(neverShownForm)`, the practical symptom would be the process failing to exit when "Exit" is clicked from the tray while autostarted hidden; this is a plausible, testable-on-first-rig-run risk, not a purely theoretical one, and the planner should treat "Exit from tray while started via `--tray`, never having shown the window" as its own explicit rig-test scenario (see Open Questions) |

## Open Questions (RESOLVED)

1. **RESOLVED — Does `Application.Exit()` cleanly terminate the message loop when the main form was started via `ApplicationContext` and never shown?**
   - What we know: `Application.Exit()` is documented to close all forms and message loops on the calling thread, regardless of how the loop was started (`Run()`, `Run(Form)`, or `Run(ApplicationContext)`) — this is a stronger, more explicit termination than relying on `ApplicationContext`'s own `Closed`-event wiring.
   - What's unclear: Whether any WinForms edge case exists where a `Form` that was constructed but never made visible (no window handle ever created via `Show()`) behaves differently when `Application.Exit()` attempts to close it as part of its "close all open forms" step, versus a form that was shown then hidden.
   - Recommendation: This is very likely a non-issue (`Application.Exit()`'s documented behavior does not condition on form visibility), but the planner should flag "start via `--tray`, then click tray Exit without ever having shown the main window" as an explicit manual/rig-test scenario in this phase's plan, since it's the one combination this research could not verify against a live Windows environment in this session.
   - Resolution: Adopted — 08-04-PLAN.md's rig-validation checkpoint includes this exact scenario ("Assumption A2": start via `--tray`, click tray Exit without ever showing the main window) as a go/no-go gate item.

2. **RESOLVED — Should `WindowsAutostartConfigurator` get a dedicated `RigToggle.Windows.Tests` test, given it requires a real registry write?**
   - What we know: `RigToggle.Windows.Tests` already exists as a separate `net10.0-windows`-targeted test project (per `RigToggle.Windows.Tests.csproj`), distinct from the cross-platform `RigToggle.Tests`. A registry-write test would need to run under this project (Windows-only), and would mutate the real `HKCU\...\Run` key — even scoped to a distinctly-named test value, this is a side-effecting test against real user-hive state, unlike this codebase's existing hand-written-fake convention for `IMonitorController`/`IAudioController`/`IAppController`.
   - What's unclear: Whether this project's existing CI/test-running setup (if any exists beyond local `dotnet test`) runs on a real Windows machine where such a test would be safe to execute, or whether it would need to be excluded/marked manual-only.
   - Recommendation: Left to the planner. A pragmatic middle ground: unit-test the *interface contract* is trivially satisfiable by keeping `WindowsAutostartConfigurator` a thin, obviously-correct wrapper (as shown in Pattern 3) and relying on rig-testing for its actual registry behavior, while `ToggleResultFormatter` and `StartupArgs` (Pattern 2 and Pattern 4's helpers) get real, fast, side-effect-free unit tests in the existing cross-platform `RigToggle.Tests` project.
   - Resolution: Adopted the pragmatic middle ground — 08-01-PLAN.md gives `ToggleResultFormatter`/`StartupArgs` real unit tests in `RigToggle.Tests`, and deliberately omits a registry-mutating test for `WindowsAutostartConfigurator`, relying on the 08-04 rig checkpoint instead.

## Environment Availability

Skipped — this phase has no new external tool/service/runtime dependencies beyond the .NET 10 SDK and Windows registry access already established as working by every prior phase in this repository. `Microsoft.Win32.Registry` is part of the BCL on the already-verified `net10.0-windows` target; no new install step is introduced.

## Validation Architecture

Skipped — `.planning/config.json` sets `workflow.nyquist_validation: false` explicitly.

## Security Domain

`security_enforcement` is absent from `.planning/config.json` (the `workflow` block has no such key), so per the instructions this section is included, though this phase — like Phase 7 before it — has almost no ASVS-relevant surface: a purely local, single-user, no-network, no-auth desktop utility.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V1 Architecture / Business Logic | Marginally | The `IAutostartConfigurator` abstraction (Pattern 3) is itself a small integrity control — it ensures autostart state is read from a single source of truth (the registry) rather than a settings.json value that could drift, avoiding a "silently wrong toggle state" class of bug, not a network-facing vulnerability |
| V2 Authentication | No | Single local user, no authentication surface |
| V3 Session Management | No | No sessions — stateless-per-invocation desktop utility |
| V4 Access Control | No | No multi-user/permission model. Writing to `HKCU\...\Run` deliberately stays within the current user's own hive and requires no elevation, preserving the app's existing non-elevated ("asInvoker") execution level per `CLAUDE.md`'s explicit constraint — this phase must not add any elevation manifest or admin-required registry path (e.g. `HKLM\...\Run` would require elevation and is explicitly the wrong hive for this design) |
| V5 Input Validation | Marginally | The only new "external input" this phase introduces is the `args` array passed to `Main` — `StartupArgs.ShouldStartHidden` must not crash on `null`/empty/malformed `args` (e.g. launching the exe with unexpected extra arguments should not throw, just fall through to normal — visible — startup) |
| V6 Cryptography | No | Not touched by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| Writing an autostart entry to the wrong registry hive (`HKLM` instead of `HKCU`) would silently require/attempt elevation, breaking the app's documented non-elevated execution model and reintroducing the exact UIPI cross-process-focus class of bug the v1.0 H9 debug session already worked around | Elevation of Privilege (self-inflicted, not attacker-driven, but the same bug class) | Always target `Microsoft.Win32.Registry.CurrentUser`, never `LocalMachine`, for this feature — already locked by D-05/`REQUIREMENTS.md`'s Out-of-Scope table; this phase's implementation must not deviate |
| A malformed/unexpected `args` array (e.g. the exe launched with garbage arguments by something other than the app's own Run-key command string) crashing `Main` before the composition root finishes wiring | Denial of Service (self-inflicted — the process fails to start at all, which is especially bad for an autostart entry that silently fails every login) | `StartupArgs.ShouldStartHidden` should be a simple, defensive `Contains` check with no indexing/parsing that could throw on empty or single-element arrays |

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.run — confirms `Run(Form)` "makes the specified form visible" and describes its internal wrapping into `ApplicationContext` + `Show()`
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.applicationcontext — confirms `ApplicationContext`'s `Form`-argument constructor only wires the `Closed` event to `ExitThreadCore`, does not call `Show()`
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.closereason — official enum reference; confirms all 7 members including `ApplicationExitCall` ("The Exit() method of the Application class was invoked") and `UserClosing`'s dual meaning (X-button and programmatic `Close()`)
- https://learn.microsoft.com/en-us/archive/blogs/jfoscoding/starting-an-application-without-showing-a-form — Microsoft's own historical (but still technically accurate, corroborated by the two current API docs above) explanation of `Application.Run(Form)`'s internal `Show()` call and the `ApplicationContext` workaround
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/warnings/il3000 — confirms `Assembly.Location` returns empty string for single-file-bundled assemblies; `Environment.ProcessPath` as the .NET 6+ replacement
- https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registry — confirms `HKEY_CURRENT_USER` access requires no elevation
- Direct reading of `src/RigToggle.App/MainForm.cs`, `MainForm.Designer.cs`, `SettingsForm.cs`, `Program.cs`, `src/RigToggle.Core/ToggleOrchestrator.cs`, `src/RigToggle.Core/Models/ToggleResult.cs`/`ToggleStepResult.cs`, `src/RigToggle.Windows/WindowsAppController.cs`, `src/RigToggle.Tests/ToggleOrchestratorTests.cs`, all `.csproj` files — this phase's actual integration surface and existing conventions, read in full
- `grep`/`find` across `src/` confirming zero existing `.ico` files, no `Properties/Resources.resx`, no `InternalsVisibleTo` from `RigToggle.App`, and no existing `RigToggle.App`-targeting test project — establishes the actual current state this phase builds on, not an assumption

### Secondary (MEDIUM confidence)
- GitHub `dotnet/winforms` issue #6996 ("NotifyIcon is not deleted (stays in tray) when application closes") — community-documented, longstanding, unresolved-at-the-framework-level bug corroborating the need for explicit `Visible = false` + `Dispose()` before exit (D-04's existing rationale); no maintainer-confirmed root cause exists, only the community workaround, which matches D-04's own approach
- GitHub `dotnet/docs` issue #15813 ("[WinForms] deprecated controls removed") and community WebSearch corroboration — confirms `ContextMenu`/`MenuItem` removal from WinForms Core (.NET Core 3.1+); cross-checked against multiple independent sources (Microsoft.Learn `ContextMenu` docs' own "provided for binary compatibility... not intended to be used directly" remark, GitHub issue discussion)
- Multiple independent WebSearch results (Tek-Tips, CodeProject, MSDN social forums) all independently confirming `NotifyIcon.Click`'s button-agnostic behavior and the `MouseClick`/`MouseButtons.Left` fix — consistent across sources, no contradiction found
- WebSearch results on `ShowBalloonTip`'s 63/255-character truncation limits (Stephen Sulzberger's blog, MSDN social forums, cross-referenced against the official `NotifyIcon.Text` max-length compatibility note for the *related-but-distinct* tooltip-text limit) — consistent across sources; underlying Win32 `NOTIFYICONDATA` struct field sizes are stable, undocumented-by-Microsoft-directly but empirically well-established

### Tertiary (LOW confidence)
None — every claim above was either verified against official Microsoft Learn documentation directly, or corroborated across multiple independent community sources with no contradictions found. The one genuinely unverifiable claim (Assumption A2, `Application.Exit()` behavior against a never-shown `ApplicationContext`-hosted form) is explicitly flagged in the Assumptions Log and Open Questions rather than stated as fact.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every component (`NotifyIcon`, `ContextMenuStrip`, `Microsoft.Win32.Registry`, `ApplicationContext`, `CloseReason`, `Environment.ProcessPath`) is BCL/WinForms-builtin, confirmed against official Microsoft Learn API docs; zero new external dependencies to evaluate
- Architecture: HIGH — directly derived from reading this codebase's actual existing `MainForm.cs`/`SettingsForm.cs`/`Program.cs`/`ToggleOrchestrator.cs`/`WindowsAppController.cs` and CONTEXT.md's locked D-01 through D-09 decisions; the one substantive correction (D-06's mechanism) is backed by direct official-documentation verification, not speculation
- Pitfalls: HIGH — all six pitfalls are well-established, mechanically verifiable WinForms/Win32 behaviors (confirmed against official docs and/or multiple independent, mutually-corroborating community sources), not speculative edge cases; two (Pitfall 1 on `CloseReason`, the `ApplicationExitCall`/D-06 correction) were specifically re-derived from official docs rather than assumed from training-data memory, since this exact interaction had non-obvious failure-mode potential

**Research date:** 2026-07-30
**Valid until:** No expiry concern for the core mechanisms (`NotifyIcon`/`ContextMenuStrip`/`Registry`/`ApplicationContext` have been API-stable since .NET Framework and are not under active change in modern .NET). Re-research only if: (a) this project's `net10.0-windows` target is ever bumped in a way that changes WinForms' tray/menu API surface (no such change is currently planned or foreseeable), or (b) CONTEXT.md's locked D-01 through D-09 decisions are revisited, particularly D-06 given this research's mechanism correction.
