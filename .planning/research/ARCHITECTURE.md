# Architecture Research: v1.1 Integration (Tray/Hotkey/CLI/Toast/Multi-Monitor)

**Domain:** Integrating automation + multi-monitor support into an existing shipped 4-project .NET/WinForms utility (Rig Toggle v1.0)
**Researched:** 2026-07-26
**Confidence:** HIGH (all findings verified directly against this repo's actual source files, not assumed from convention; a small number of Windows-mechanism claims are MEDIUM, sourced from WebSearch and flagged inline)

> Supersedes the previous version of this file (2026-07-24), which was written pre-implementation and described a hypothetical WPF/MVVM structure. The shipped app is WinForms, not WPF — this version replaces the generic pre-build guess with the actual v1.0 architecture (verified by reading the code) and focuses on the v1.1 integration question the current milestone requires.

## Current Architecture (v1.0, as-shipped)

```
┌───────────────────────────────────────────────────────────────────┐
│ RigToggle.App  (net10.0-windows, UseWindowsForms)                  │
│  Program.cs (composition root) → MainForm, SettingsForm,            │
│  MonitorConfirmDialog — never `new`s a concrete adapter (D-anti-2)  │
├───────────────────────────────────────────────────────────────────┤
│ RigToggle.Core  (net10.0, ZERO Windows API refs — enforced by csproj│
│  comment, must stay true)                                           │
│  ToggleService (orchestrates via 5 interfaces) + AppSettings/        │
│  MonitorState/AudioState/ToggleResult models + Json*Store            │
├───────────────────────────────────────────────────────────────────┤
│ RigToggle.Windows  (net10.0-windows, UseWindowsForms=true, but       │
│  currently uses NO WinForms controls — only P/Invoke + COM/CCD)      │
│  WindowsMonitorController / WindowsAudioController /                 │
│  WindowsAppController — implement Core's 3 mutation interfaces       │
└───────────────────────────────────────────────────────────────────┘
```

Key existing facts that determine where v1.1 pieces must go:
- `RigToggle.Core.csproj` has an explicit anti-regression comment: **zero Windows API references, ever.** Any new abstraction whose only implementation needs WinForms (`NotifyIcon`, message-loop `WndProc`) does **not** belong in Core the way `IMonitorController` does — Core can define the *contract*, but the natural implementation lives in App, not Windows.
- `RigToggle.Windows.csproj` already sets `UseWindowsForms=true` (inherited from the WindowsDisplayAPI dependency chain), but the project **contains zero WinForms controls today** — its P/Invoke (`NativeMethods.cs`) is scoped to `user32.dll` calls against *other processes'* windows (`EnumWindows`, `GetWindowThreadProcessId`), not its own message loop. This is a meaningful precedent: don't let `UseWindowsForms=true` being technically available tempt you into putting `NotifyIcon`/tray/hotkey code there — it breaks the established "Windows = Win32/COM adapters implementing Core contracts" vs "App = UI shell + composition" split.
- `ToggleService` (Core) has zero knowledge of *how* it was invoked (button, hotkey, CLI) or *whether* to notify — that decision currently lives entirely in `MainForm.BtnToggle_Click`. This is good: it means new trigger paths (hotkey, tray menu, CLI) can all call the *same* `ToggleService` methods without Core changing at all, IF the confirmation-dialog / notification / error-reporting logic that currently lives inline in `MainForm.BtnToggle_Click` gets extracted into a shared helper first (see Pattern 1 below) — otherwise it will be reimplemented three more times with subtly different behavior.
- Mode is derived from snapshot-file presence (`ToggleService.IsInRigMode()` → `ISnapshotStore.Exists()`), not an in-memory flag — this is important for CLI/tray/hotkey correctness: **every trigger path can independently and statelessly ask "which mode am I in right now" with no shared in-memory state**, which is exactly what a resident-process-plus-external-CLI-process design needs.

## Component Responsibilities Today

| Component | File | Responsibility |
|---|---|---|
| `ToggleService` | `RigToggle.Core/ToggleService.cs` | Orchestrates snapshot → mutate (rig mode, stop-on-first-failure) / restore (normal mode, isolate-and-continue) via the 5 interfaces below. Zero Windows API references. |
| `IMonitorController` / `WindowsMonitorController` | `Core/Abstractions/IMonitorController.cs`, `Windows/WindowsMonitorController.cs` | Enumerate, capture full-topology snapshot, disable one target monitor (CCD `ApplyPathInfos`), restore (in-process cache fast path + crash-recovery `ApplyTopology(Extend)` fallback) |
| `IAudioController` / `WindowsAudioController` | same pattern | Enumerate playback devices, capture/restore default device across all 3 audio roles via `IPolicyConfig` COM interop |
| `IAppController` / `WindowsAppController` | same pattern | Detect running, relaunch-or-focus (via `ShellExecute`, not window manipulation), minimize-if-visible |
| `ISettingsStore` / `JsonSettingsStore` | `Core/Persistence/JsonSettingsStore.cs` | Load/save `AppSettings` as JSON, degrade-to-fresh on corruption |
| `ISnapshotStore` / `JsonSnapshotStore` | same pattern | Load/save/clear `StateSnapshot`; **file presence is the mode flag** |
| `MainForm` | `App/MainForm.cs` | Mode indicator, Toggle button, confirmation dialog gate, error/partial-failure `MessageBox` reporting |
| `SettingsForm` | `App/SettingsForm.cs` | Single-select monitor `ComboBox`, two audio-role `ComboBox`es, app-path textbox+browse+drag-drop |
| `Program.cs` | `App/Program.cs` | Composition root — the only place real adapters/stores are constructed |

## Feature-by-Feature Integration

### 1. Tray residency + autostart + minimize-to-tray + context menu

**New components (all in `RigToggle.App`):**
- `TrayIcon` (or similar) — owns a `NotifyIcon`, its `ContextMenuStrip` (Switch to Rig/Normal Mode, Settings, Exit), double-click-to-restore. Constructed in `Program.cs`, given a reference to `MainForm` (to show/hide) and to the shared toggle-orchestration helper from Pattern 1.
- `WindowsAutostartService` in `RigToggle.Windows` implementing a new Core interface `IAutostartService` (`bool IsEnabled(); void Enable(); void Disable();`) — writes/removes a value under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` via `Microsoft.Win32.Registry`. **Do not use Task Scheduler** — Task Scheduler's main advantage is silent elevation, which this app deliberately never uses (no elevation manifest, per existing `Program.cs`/csproj comments); a plain per-user `Run` key or Startup-folder shortcut is the correct, simplest mechanism for a non-elevated app (verified via WebSearch: Task Scheduler is recommended specifically for elevated/admin scenarios, which do not apply here — MEDIUM confidence, community sources, but consistent with the existing "asInvoker only" constraint already enforced in this codebase).
- This follows the existing interface-in-Core/implementation-in-Windows pattern exactly — `IAutostartService` has no Windows API surface leaking into its signature (bool + two void methods), so Core stays clean.

**Modified:**
- `MainForm` — override `OnFormClosing` to cancel-and-hide (`e.Cancel = true; Hide();`) instead of exiting, when a new `AppSettings.MinimizeToTrayOnClose` flag is set; real exit only via the tray menu's "Exit" item (which must call `Application.Exit()` explicitly, bypassing the intercepted close).
- `AppSettings` — add `bool StartWithWindows` and `bool MinimizeToTrayOnClose` (both default `false` via normal C# bool default, so existing `settings.json` files deserialize these as `false` with **zero migration needed** — purely additive, non-breaking, unlike the monitor fields in Feature 5).
- `SettingsForm` — two new checkboxes wired to `IAutostartService`/the new settings fields.
- `Program.cs` — construct `WindowsAutostartService`, pass into `SettingsForm`'s factory and to the new `TrayIcon`.

**Not touched:** `ToggleService`, all five Core interfaces except the new `IAutostartService`, `WindowsMonitorController`/`WindowsAudioController`/`WindowsAppController`.

### 2. Global hotkey trigger

**New components (in `RigToggle.App`, NOT `RigToggle.Windows`):**
- A small `RegisterHotKey`/`UnregisterHotKey` P/Invoke pair (WM_HOTKEY = 0x0312) belongs in App because it must be called against `MainForm.Handle` and handled by overriding `MainForm.WndProc` — this is fundamentally tied to *this* window's message pump, unlike the existing `RigToggle.Windows/NativeMethods.cs`, which enumerates *other* processes' windows. Duplicate the small P/Invoke surface in App rather than trying to reuse `RigToggle.Windows.NativeMethods` (currently `internal` to that assembly and scoped to a different concern) — verified this is the standard idiom via WebSearch (override `WndProc`, intercept `WM_HOTKEY`, call `RegisterHotKey(this.Handle, id, modifiers, key)` — MEDIUM confidence, multiple independent community sources agree, no official Microsoft Learn page dedicated to this specific composition but the API itself (`user32.dll RegisterHotKey`) is decades-stable).
- Register in `MainForm.OnHandleCreated`/`OnLoad`, unregister in `OnHandleDestroyed`/`Dispose`.

**Modified:**
- `MainForm.WndProc` — new override, forwards `WM_HOTKEY` to the same shared toggle-orchestration helper the button click uses (Pattern 1) — must **not** duplicate `BtnToggle_Click`'s confirmation-dialog/checklist logic inline.
- `AppSettings` — add hotkey configuration (e.g. `int? HotkeyModifiers`, `int? HotkeyVirtualKey`, storing raw Win32 values is simplest and avoids inventing a serializable `Keys`-enum wrapper). Additive, nullable, non-breaking.
- `SettingsForm` — a hotkey-capture control (a textbox that records the next keydown + modifier state is the common WinForms pattern; no ready-made control exists in the BCL).

**Dependency:** A `RegisterHotKey` call is valid as long as `MainForm`'s `HWND` exists — it does **not** strictly require the window to be visible, only alive. But the hotkey is only *useful* as a background trigger if the app doesn't fully exit when the user closes the window — i.e., it depends on Feature 1's minimize-to-tray behavior existing first. Build hotkey registration to survive `Hide()` (it will, automatically, since the HWND is untouched by `Hide()`), but land Feature 1 before Feature 2 so this is testable end-to-end.

### 3. CLI trigger + single-instance signaling

This is the largest **new subsystem**, entirely in `RigToggle.App` (process-lifecycle/composition-root concern, not Core business logic and not a Windows API adapter):

- **Single-instance detection:** a named `Mutex` (e.g. `"Global\\RigToggle-SingleInstance"`) acquired at the very top of `Main()`, before `ApplicationConfiguration.Initialize()`. Verified via WebSearch as the standard .NET idiom (MEDIUM confidence, multiple independent sources: AutoIt Consulting, dotnet-guide.com, and others all describe the identical Mutex-then-pipe pattern).
- **IPC:** `System.IO.Pipes.NamedPipeServerStream`/`NamedPipeClientStream`. The resident instance (mutex-owner) runs a background listener thread/task hosting the server; any subsequent CLI-invoked process that finds the mutex already owned connects as a client, writes a short command string (`"--to-rig"` / `"--to-normal"` / `"--toggle"`), and exits immediately — it never constructs `ToggleService` or any adapter itself.
- **New `Program.cs` shape** (this is a real restructure, not additive, though it changes no public API since it's an entry point with no external callers):
  1. Parse `args`.
  2. Try to acquire the single-instance mutex.
  3. If **not acquired** (another instance is running): if `args` contains a recognized command, connect via the named pipe, send it, exit with no UI shown at all. If no recognized args, just exit (equivalent to today's implicit "already running" no-op) — or optionally bring the existing window to front via the pipe protocol too.
  4. If **acquired** (this is the first/only instance): construct the composition root exactly as today (settings store, adapters, `ToggleService`), **plus** start the named-pipe server, **plus** if `args` contains a recognized command, execute it immediately via the shared toggle-orchestration helper (Pattern 1) before/instead of showing `MainForm` (headless CLI launch — no window flashes open for a Stream-Deck-triggered call when nothing was running yet). If no `args`, proceed to `Application.Run(mainForm)` exactly as today.
- The pipe server's received-command handler must marshal back onto the UI thread (`MainForm.BeginInvoke`) before touching `ToggleService`/UI, since it runs on a background thread.

**Dependency — this is the most important cross-feature ordering constraint identified in this research:** a CLI trigger is only useful for its stated purpose (macro pad / Stream Deck triggering a *background* toggle) if there is normally a resident process to receive the pipe message. Without Feature 1's minimize-to-tray-on-close (keep process alive, don't exit), the "signal an already-running instance" path only works during the narrow window when the user happens to have the GUI open — which defeats the point of a hardware macro-pad trigger. **Land Feature 1 (tray residency, no-exit-on-close) before Feature 3's IPC signaling has real product value**, even though the Mutex/pipe mechanism itself has no code dependency on Feature 1.

**Not touched:** `ToggleService`, Core interfaces, Windows adapters — the CLI path reuses the exact same composition-root objects `Program.cs` already builds.

### 4. Toast/status notification

**Recommendation: do not introduce a Core `INotificationService` abstraction.** A toast/balloon notification is fundamentally tied to the `NotifyIcon` instance from Feature 1 (`NotifyIcon.ShowBalloonTip(...)` is an instance method — there is nothing to call it on until the tray icon exists), so its only real implementation lives in `RigToggle.App`, next to `TrayIcon`. Introducing a Core interface for something with exactly one call site and no branching logic to unit-test would be abstraction for its own sake — skip it (YAGNI), and just add a plain method on the App-layer `TrayIcon` class (e.g. `TrayIcon.ShowToggleNotification(ToggleResult result, string modeLabel)`), called from the shared orchestration helper (Pattern 1).

- Mechanism: `NotifyIcon.ShowBalloonTip(timeout, title, text, icon)`. Verified via WebSearch: on Windows 10/11 this reliably renders as a native toast/banner; the one caveat (MEDIUM confidence, Microsoft Q&A + `.NET` docs) is that on Windows 11 the balloon *displays* as a toast but does **not** persist into the Action Center history the way it does on Windows 10 — acceptable for this use case (a live confirmation the user glances at when triggering headlessly, not a durable log — `LOG-01` is explicitly deferred anyway).
- **Do not** reach for `Microsoft.Toolkit.Uwp.Notifications` / Windows App SDK toast APIs — verified via WebSearch that unpackaged (non-MSIX) apps need a manual AUMID + stub CLSID + shortcut setup and lose HTTP-image support, adding real integration surface for a capability `NotifyIcon.ShowBalloonTip` already covers with zero extra dependencies. Revisit only if a future need for persistent Action-Center history or rich toast buttons emerges.

**Trigger point:** the shared orchestration helper (Pattern 1) decides *when* to notify — per the requirement ("confirming a toggle when triggered without the GUI open"), notify when the trigger source is hotkey, tray-menu, or CLI/IPC; skip it (or make it redundant/optional) when the trigger is the button click inside a visible `MainForm`, since that path already shows a `MessageBox` on partial failure and the mode label updates in-window on success.

**Dependency:** hard-depends on Feature 1 (needs a live `NotifyIcon`). Sequence Feature 4 immediately alongside/after Feature 1, not before.

### 5. Multi-monitor enable/disable configuration

This is the deepest change — it touches the settings model, one Core interface signature, and the most CCD-sensitive code in the app (`WindowsMonitorController`, which is already the result of three rig-tested iterations per its own doc comments — treat it as **high-risk, deserving its own isolated rig-testing pass**, not a drive-by refactor alongside the other four features).

**Breaking model changes (Core):**

| Type | Today | v1.1 | Why |
|---|---|---|---|
| `AppSettings` | `MonitorDevicePath` (single `string?`), `MonitorFriendlyName` (single `string?`) | `MonitorsToDisable` (`List<string>`), `MonitorsToEnable` (`List<string>`) — friendly names can stay display-cache-only, resolved live at read time like today | Generalizes to independently-configurable disable/enable sets per PROJECT.md |
| `MonitorState` (record) | `TargetDevicePath` (single `string`) | `DisableTargetDevicePaths` (`IReadOnlyList<string>`) or similar | One primary target no longer models the domain; multiple monitors are being deliberately removed |
| `MonitorInfo` (record) | `(DevicePath, FriendlyName, IsPrimary)` | add `IsActive` (`bool`) | The enable-set picker in Settings must show **inactive** monitors (the rig monitor, normally OS-disabled to save power) — `GetActiveMonitors()` structurally cannot list something that isn't active |
| `IMonitorController` | `IReadOnlyList<MonitorInfo> GetActiveMonitors()` | add `IReadOnlyList<MonitorInfo> GetAllMonitors()` (active + inactive, via `WindowsDisplayAPI`'s `GetAllPaths()`, already used internally by `WindowsMonitorController.Restore()`'s fallback path) | Additive method — needed to populate the enable-set picker at all |
| `IMonitorController` | `void Disable(string monitorDevicePath)` | `void Disable(IReadOnlyList<string> monitorDevicePaths)` | **Breaking signature change.** Must NOT be implemented as N sequential single-monitor `Disable` calls in a loop from `ToggleService` — see Anti-Pattern below. |
| `IMonitorController` | *(none)* | `void Enable(IReadOnlyList<string> monitorDevicePaths)` | **New method**, the inverse operation: takes currently-**inactive** targets (found via `GetAllPaths()`, not `GetActivePaths()`) and adds them into the live topology — conceptually the same primitive the existing crash-recovery fallback in `Restore()` already uses (`ApplyTopology(Extend)` + reposition-from-live-objects), now promoted from "corner-case fallback" to "deliberate forward-mode feature." |
| `IMonitorController` | `void Restore(MonitorState previousState)` | unchanged signature | But its **internal logic** gains a new case: a currently-active path that is **not** present in `previousState.Paths` (i.e., a monitor from the enable-set that this session turned on) must be dropped again on restore — today's `Restore()` only ever adds paths back, it never has to remove an "extra" active one. |

**Why Enable-set restore is simpler than it first looks:** per PROJECT.md's phrasing ("mirrored on toggle-back"), the enable-set monitors don't need historical-state restore (they weren't active before rig mode — there is no prior state to remember), they just need to go back to disabled, deterministically. Since `CaptureState()` runs *before* any mutation (per `ToggleService`'s existing `D-08` guarantee), the pre-toggle snapshot already reflects the correct target end-state for toggle-back (enable-set monitors simply absent from it). This means `Restore()`'s new "extra active path not in snapshot → remove it" logic is really the *same* survivor-reconstruction primitive `Disable()` already implements, just invoked from the opposite direction. **Recommend refactoring `WindowsMonitorController` around one shared internal primitive — e.g. `ApplyExactActiveSet(IReadOnlyList<string> desiredActiveDevicePaths, ...)`** that both `Disable`/`Enable` (forward) and `Restore` (backward) call, rather than three independently-evolving methods reaching similar-but-not-identical conclusions about survivor sets, primary-repositioning, and verify-and-throw. This directly reduces the risk of the exact class of bug this file's own comments describe fighting three times already (`OutputTechnology` defaults, inactive-path mode-info unreliability, source-assignment).

**Modified (App):**
- `SettingsForm` — `cboMonitor` (single ComboBox) replaced by two multi-select lists (WinForms `CheckedListBox` is the natural fit for "check zero or more from a list," unlike a `ComboBox`) — one for "Monitors to Disable," one for "Monitors to Enable," the second populated from `GetAllMonitors()` filtered to (or at least prioritizing) inactive ones. `ValidateSettingsForm`'s save-gating logic needs updating for list-based selections.
- `MonitorConfirmDialog` — constructor changes from a single `string monitorFriendlyName` to something that can render a plural confirmation ("This will disable X, Y and enable Z. Continue?"). `MainForm.BtnToggle_Click`'s call site (currently `FirstOrDefault(m => m.DevicePath == settings.MonitorDevicePath)`) becomes a loop resolving friendly names for every device path in both sets.
- `ToggleService.ToggleToRigMode()` — replaces the single `_monitorController.Disable(settings.MonitorDevicePath!)` call with (recommended) one combined "Monitor" `TryExecuteStep` that internally calls `Disable(settings.MonitorsToDisable)` then `Enable(settings.MonitorsToEnable)` — keep this as **one** `ToggleStepResult` entry (not two), matching today's step granularity (Monitor/Audio/App) and avoiding inflating `MainForm.FormatChecklist`'s output shape; document that both sub-operations must succeed for the step to report `Succeeded`.
- `ToggleService.IsFullyConfigured` — updated null/empty checks for the new list-typed settings fields (open design question, not resolved by this research: should an empty disable-set be allowed now that the tool generalizes beyond "always disable exactly one monitor"? Flag for planning/requirements, don't hard-decide here).

**Settings migration (breaking-data, not breaking-code):** `settings.json` files written by v1.0 have `MonitorDevicePath`/`MonitorFriendlyName` populated and no `MonitorsToDisable`/`MonitorsToEnable` fields at all. Recommend a one-time migration inside `JsonSettingsStore.Load()` (the existing degrade-gracefully pattern already used for corrupt JSON is the right place): if the new list fields are absent/empty **and** the legacy `MonitorDevicePath` is present, seed `MonitorsToDisable = [MonitorDevicePath]` once. Keep the legacy fields in the model (harmless, `System.Text.Json` ignores extra/unused properties on read and simply stops writing meaningful data into them on next save) rather than hard-deleting them — this is a single personal user's settings file, so the migration only has to work once, but silently discarding a working v1.0 configuration on upgrade would be a bad experience for zero benefit.

**Not touched:** `IAudioController`, `IAppController`, `ISettingsStore`/`ISnapshotStore` interface shapes (only the models they carry change), `WindowsAudioController`, `WindowsAppController`.

## New Component Summary (by project)

| Project | New component | Purpose |
|---|---|---|
| RigToggle.Core | `IAutostartService` (interface only) | Contract for enable/disable/is-enabled autostart |
| RigToggle.Core | `MonitorInfo.IsActive`, `MonitorState.DisableTargetDevicePaths`, `AppSettings.MonitorsToDisable/MonitorsToEnable/StartWithWindows/MinimizeToTrayOnClose/Hotkey*` | Model changes for Features 2, 1, 5 |
| RigToggle.Windows | `WindowsAutostartService : IAutostartService` | Registry `Run` key read/write (no elevation) |
| RigToggle.Windows | `IMonitorController.Enable(...)`, `GetAllMonitors()`, internal `ApplyExactActiveSet` primitive | Multi-monitor CCD logic (Feature 5) |
| RigToggle.App | `TrayIcon` (NotifyIcon + context menu + `ShowToggleNotification`) | Features 1 and 4 |
| RigToggle.App | Hotkey P/Invoke + `MainForm.WndProc` override | Feature 2 |
| RigToggle.App | Single-instance `Mutex` + named-pipe server/client, restructured `Program.Main` | Feature 3 |
| RigToggle.App | Shared toggle-orchestration helper (Pattern 1) | Cross-cutting — used by button, hotkey, tray menu, CLI |
| RigToggle.App | Two-list `SettingsForm` monitor UI + plural `MonitorConfirmDialog` | Feature 5 |

## Architectural Pattern 1: Extract the Shared Toggle-Orchestration Helper

**What:** `MainForm.BtnToggle_Click` today inlines four responsibilities: (a) settings-configured guard, (b) DISPLAY-03 confirmation dialog + "don't ask again" persistence, (c) calling `ToggleService.ToggleToRigMode()/ToggleToNormalMode()`, (d) reporting the `ToggleResult` (MessageBox checklist) or a caught exception. Features 1-4 each add a **new trigger** for the exact same sequence (tray menu item, global hotkey, CLI/IPC command) but need to report outcomes differently (toast instead of/in addition to MessageBox) and skip re-showing the confirmation dialog when triggered non-interactively (a CLI/hotkey-triggered toggle can't wait on a modal dialog the user isn't watching for).

**When to use:** Before implementing Features 1-3, extract this into a single method — e.g. `ToggleOrchestrator.Execute(TriggerSource source)` — parameterized on trigger source (`Button`, `Hotkey`, `TrayMenu`, `Cli`) so it can decide once, in one place: whether to show the modal confirmation dialog (only for `Button`/`TrayMenu`, where a human is present to answer it — a CLI/hotkey trigger should either skip confirmation entirely and rely on the existing `SkipMonitorConfirmation` durable setting, or fail closed if unconfirmed) and whether to report via `MessageBox` vs `TrayIcon.ShowToggleNotification`.

**Trade-offs:** Adds one more class, but avoids four divergent copies of confirmation/error-handling logic (the exact kind of drift that produced this project's own H9 regression previously — see `.planning/debug/resolved/moza-foreground-focus.md`). Given four call sites are coming in this milestone, the extraction pays for itself immediately.

## Anti-Patterns to Avoid (specific to this codebase)

### Anti-Pattern 1: Implementing multi-monitor `Disable` as a loop of single-monitor `Disable` calls

**What people would do:** Keep `IMonitorController.Disable(string)`'s existing signature and have `ToggleService` call it once per device path in `MonitorsToDisable`.
**Why it's wrong:** `WindowsMonitorController.Disable`'s survivor-reconstruction and primary-repositioning math (shift every surviving path's coordinates so exactly one lands at `(0,0)`) is computed once per call from a fresh `GetActivePaths()` query, and its own verify-and-throw asserts `exactlyOnePrimary` after *every single call*. Looping would apply N separate native `SetDisplayConfig` calls instead of one atomic topology transition, multiplying the chance of a partial/inconsistent intermediate state, and would need the primary-promotion logic to reason correctly across sequential calls where the "current primary" keeps changing mid-loop.
**Instead:** Change the interface to `Disable(IReadOnlyList<string>)` and compute the full survivor set / single repositioning delta / single `ApplyPathInfos` call once, exactly like today's single-target version but generalized to an exclusion set.

### Anti-Pattern 2: Putting `NotifyIcon`/hotkey/tray code in `RigToggle.Windows` because `UseWindowsForms=true` is already set there

**What people would do:** Notice `RigToggle.Windows.csproj` already has `UseWindowsForms=true` and add the tray icon or hotkey `WndProc` logic there since "it's already Windows-flavored."
**Why it's wrong:** It breaks the established boundary (Windows = Win32/COM adapters *implementing Core interfaces*, stateless w.r.t. UI; App = the actual UI shell + composition root). `RigToggle.Windows` today has zero WinForms controls — `UseWindowsForms=true` is there only because `WindowsDisplayAPI`'s dependency chain needs it, not because the project owns any UI. A tray icon and a form's own message pump are UI-shell concerns that belong with `MainForm`/`SettingsForm` in `RigToggle.App`.
**Instead:** Put `TrayIcon`, hotkey P/Invoke + `WndProc` override, and the pipe server/client in `RigToggle.App`. Reserve `RigToggle.Windows` for genuinely reusable, Core-interface-shaped Windows API adapters (`IAutostartService`'s implementation, and the generalized `IMonitorController.Enable`).

### Anti-Pattern 3: Introducing a Core `INotificationService` abstraction for symmetry with the other four adapter interfaces

**What people would do:** Since Core defines `IMonitorController`/`IAudioController`/`IAppController`/`ISettingsStore`/`ISnapshotStore`, it might feel natural to add `INotificationService` too, "for consistency."
**Why it's wrong:** Those five interfaces exist because `ToggleService` (Core) needs to call them as part of its orchestration logic, and Core must stay Windows-API-free while still being testable against fakes (`RigToggle.Tests/Doubles/FakeControllers.cs`). Notification is different: it is a *caller-side* UI decision (should this particular trigger show feedback, and how) that `ToggleService` itself has no business making — and its only implementation is inseparable from a `NotifyIcon` instance that only exists in `RigToggle.App`. Adding the interface would be pure ceremony with a single real implementation and nothing to fake in a test.
**Instead:** A plain method on the App-layer `TrayIcon`/orchestration helper, no Core interface.

## Build Order (respecting the dependencies found above)

1. **Multi-monitor settings model + `WindowsMonitorController` generalization** (Feature 5) — land first, in isolation, with its own rig-testing pass. Rationale: it changes `AppSettings`, `MonitorState`, `MonitorInfo`, and `IMonitorController`'s signature — every other feature's new trigger paths (hotkey, tray menu, CLI) will call into `ToggleService`/the confirmation dialog, so it's cheaper to build those four call sites once against the *final* multi-monitor-aware shape than to build them against the old single-monitor shape and revisit all four later. This is also the highest-CCD-risk change (per `WindowsMonitorController`'s own history of three rig-tested iterations to get `Disable`/`Restore` right) and benefits from not being entangled with unrelated tray/hotkey/CLI work during debugging.
2. **Extract the shared toggle-orchestration helper (Pattern 1)** — refactor `MainForm.BtnToggle_Click` to call it, built against the now-final multi-monitor confirmation-dialog shape. No new user-facing behavior yet; this is the seam every subsequent trigger plugs into.
3. **Tray residency + autostart (Feature 1) + notification (Feature 4)** — these two ship together in practice: Feature 4 hard-depends on the `NotifyIcon` instance Feature 1 creates. This also establishes the "don't exit on close" behavior every remaining feature's headless/background usefulness depends on.
4. **Global hotkey (Feature 2)** — depends on Feature 3 (tray residency's keep-alive) for real background usefulness, and reuses the Pattern 1 helper.
5. **CLI trigger + single-instance IPC (Feature 3, listed last)** — depends on Feature 1's resident-process model for its "signal an already-running instance" path to have any point, and reuses the same Pattern 1 helper on the receiving end. Building it last also means the pipe-server's command handler is calling into an orchestration helper that has already been exercised by two other real trigger paths (tray menu, hotkey), reducing the chance the CLI path exposes an edge case (e.g. confirmation-dialog-on-a-headless-trigger) nobody thought through yet.

## Sources

- Direct source reads of this repository: `src/RigToggle.Core/Abstractions/*.cs`, `src/RigToggle.Core/Models/*.cs`, `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.Core/Persistence/JsonSettingsStore.cs`, `src/RigToggle.Windows/WindowsMonitorController.cs`, `src/RigToggle.Windows/WindowsAudioController.cs`, `src/RigToggle.Windows/WindowsAppController.cs`, `src/RigToggle.Windows/NativeMethods.cs`, `src/RigToggle.App/Program.cs`, `src/RigToggle.App/MainForm.cs`, `src/RigToggle.App/SettingsForm.cs`, `src/RigToggle.App/MonitorConfirmDialog.cs`, all four `.csproj` files, `src/RigToggle.Tests/Doubles/FakeControllers.cs` — HIGH confidence, all claims about current architecture verified against actual code, not assumed.
- `.planning/PROJECT.md` — v1.1 milestone scope, constraints (no elevation manifest), and the "mirrored on toggle-back" phrasing for multi-monitor enable/disable sets that shaped the Feature 5 restore-semantics analysis.
- WebSearch: unpackaged WinForms toast notifications (Microsoft.Toolkit.Uwp.Notifications AUMID/stub-CLSID requirements) — MEDIUM confidence, used to justify rejecting it in favor of `NotifyIcon.ShowBalloonTip`.
- WebSearch: `NotifyIcon.ShowBalloonTip` Windows 10/11 toast rendering behavior (renders as toast, doesn't persist to Action Center on Win11) — MEDIUM confidence, Microsoft Q&A + `learn.microsoft.com` API docs.
- WebSearch: single-instance `.NET` apps via named `Mutex` + `NamedPipeServerStream`/`NamedPipeClientStream` for CLI argument delegation — MEDIUM confidence, multiple independent community sources in agreement (AutoIt Consulting, dotnet-guide.com, CodeProject).
- WebSearch: `RegisterHotKey`/`WM_HOTKEY`/hidden-window `WndProc` pattern in WinForms — MEDIUM confidence, multiple independent community sources in agreement; underlying `user32.dll` API itself is long-stable and unchanged.
- WebSearch: Registry `Run` key vs Startup folder vs Task Scheduler for non-elevated vs elevated autostart — MEDIUM confidence, community sources; conclusion (avoid Task Scheduler here) is reinforced by this project's own existing, explicit "no elevation manifest" constraint (HIGH confidence, verified directly in `RigToggle.App.csproj`/`RigToggle.Windows.csproj` comments).

---
*Architecture research for: Rig Toggle v1.1 (tray residency, global hotkey, CLI trigger, toast notification, multi-monitor enable/disable)*
*Researched: 2026-07-26*
