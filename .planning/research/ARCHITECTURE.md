# Architecture Research

**Domain:** Single-user Windows desktop GUI utility (system-state toggle tool: display/audio/process orchestration)
**Researched:** 2026-07-24
**Confidence:** HIGH (general desktop app structure, Win32 interop patterns) / MEDIUM-LOW (true monitor "disconnect" capability — no officially documented public API confirms this; flagged below and in PITFALLS)

## Standard Architecture

For a tool of this shape — small, single-user, single-purpose, Windows-only, `.exe`-distributed — the right architecture is a thin layered desktop app, **not** a generic n-tier enterprise structure. The critical design move is isolating every piece of OS interop (P/Invoke, COM interop, `Process`/Win32 window calls) behind small interfaces, so the GUI and the orchestration logic never touch raw Windows APIs directly. This is what makes the app testable without a real rig, and what lets the riskiest component (monitor disable) be swapped or fixed without touching anything else.

### System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                              GUI Layer                              │
│  ┌─────────────────────┐        ┌─────────────────────────────┐     │
│  │  MainWindow/ViewModel│        │ SettingsView/ViewModel      │     │
│  │  - mode indicator    │        │ - monitor picker             │     │
│  │  - Toggle button     │        │ - audio device pair picker   │     │
│  └──────────┬───────────┘        │ - app path picker             │     │
│             │                    └───────────────┬───────────────┘    │
├─────────────┴────────────────────────────────────┴────────────────────┤
│                     Orchestration Layer (domain logic)               │
│  ┌───────────────────────────────────────────────────────────────┐   │
│  │  ToggleService                                                 │   │
│  │  - ToggleToRigMode() / ToggleToNormalMode()                    │   │
│  │  - reads Settings, drives Snapshot capture/restore,            │   │
│  │    sequences Monitor → Audio → App actions                     │   │
│  └───────┬───────────────┬───────────────┬───────────────┬────────┘   │
├──────────┴───────────────┴───────────────┴───────────────┴───────────┤
│                  Control Adapters (interfaces + impls)                │
│  ┌───────────┐   ┌───────────┐   ┌────────────────┐                  │
│  │IMonitor-  │   │IAudio-    │   │IAppController  │                  │
│  │Controller │   │Controller │   │(process+window)│                  │
│  │(CCD API / │   │(IPolicy-  │   │(Process class +│                  │
│  │ P/Invoke) │   │Config COM)│   │ Win32 user32)  │                  │
│  └───────────┘   └───────────┘   └────────────────┘                  │
├─────────────────────────────────────────────────────────────────────┤
│                        Persistence Layer                              │
│  ┌────────────────┐   ┌────────────────────┐                          │
│  │ SettingsStore  │   │ SnapshotStore       │                          │
│  │ (JSON, user    │   │ (JSON, "in-flight"  │                          │
│  │  config)       │   │  state, disk-backed)│                          │
│  └────────────────┘   └────────────────────┘                          │
│         %LocalAppData%\RigToggle\settings.json + state.json           │
└─────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| MainWindow / MainViewModel | Show current mode, expose single Toggle action, reflect success/failure | WPF Window + ViewModel (MVVM), bound `ICommand` for toggle |
| SettingsView / SettingsViewModel | Let user pick target monitor, audio device pair, app path; validate before save | WPF UserControl/Window, dialogs for device pickers populated from live enumeration |
| ToggleService (orchestration) | Owns the toggle state machine: snapshot → mutate → (later) restore → clear; sequences the three OS-control adapters in a fixed order; surfaces success/partial-failure to GUI | Plain C# class, no OS calls itself — only calls interfaces below |
| IMonitorController | Enumerate monitors, get "is X active", disable/enable a specific monitor, read/apply topology | P/Invoke wrapper around CCD (`QueryDisplayConfig`/`SetDisplayConfig`) or a shelled-out external tool as fallback |
| IAudioController | Enumerate playback devices, get/set default playback device | COM interop against undocumented `IPolicyConfig`, or a NuGet wrapper (e.g. AudioSwitcher.AudioApi) that already does this interop |
| IAppController | Detect if target app is running, launch it, focus its window, minimize its window | `System.Diagnostics.Process` + P/Invoke `user32.dll` (`FindWindow`/`ShowWindow`/`SetForegroundWindow`) |
| SettingsStore | Persist/load user-configurable settings (monitor ID, audio device IDs, app path) | JSON file via `System.Text.Json`, POCO settings model |
| SnapshotStore | Persist/load the "state before toggle" record; presence of this file **is** the "currently in rig mode" flag | JSON file, written synchronously before any mutation begins |

## Recommended Project Structure

```
src/
├── RigToggle.App/                 # WPF executable project (entry point, composition root)
│   ├── App.xaml(.cs)              # Startup, DI container wiring (or manual composition — app is tiny)
│   ├── Views/
│   │   ├── MainWindow.xaml        # Toggle button + mode indicator
│   │   └── SettingsWindow.xaml    # Monitor/audio/app pickers
│   └── ViewModels/
│       ├── MainViewModel.cs
│       └── SettingsViewModel.cs
├── RigToggle.Core/                 # No WPF/UI references — pure logic, unit-testable
│   ├── ToggleService.cs           # Orchestration state machine
│   ├── Models/
│   │   ├── AppSettings.cs         # Monitor id, audio device ids, app path
│   │   └── StateSnapshot.cs       # Captured monitor+audio state
│   ├── Abstractions/
│   │   ├── IMonitorController.cs
│   │   ├── IAudioController.cs
│   │   ├── IAppController.cs
│   │   ├── ISettingsStore.cs
│   │   └── ISnapshotStore.cs
│   └── Persistence/
│       ├── JsonSettingsStore.cs
│       └── JsonSnapshotStore.cs
├── RigToggle.Windows/               # All real OS interop lives here, and only here
│   ├── Monitor/
│   │   └── CcdMonitorController.cs # P/Invoke CCD calls (or fallback shim)
│   ├── Audio/
│   │   └── PolicyConfigAudioController.cs
│   └── Process/
│       └── Win32AppController.cs
└── RigToggle.Tests/
    ├── ToggleServiceTests.cs      # Against fake controllers — no real OS needed
    └── Persistence tests
```

### Structure Rationale

- **RigToggle.Core has zero Windows API references.** This is the single most important boundary in this codebase: it means the orchestration logic (the actual "business rule" of snapshot-then-mutate-then-later-restore) can be fully unit tested with fakes, and it's where nearly all the actual bugs in sequencing/error-handling will be found and fixed cheaply.
- **RigToggle.Windows isolates all interop risk.** Every P/Invoke signature, every COM interop call, every undocumented API assumption lives in exactly one project. If the CCD-based monitor disable approach turns out not to achieve a true disconnect (a real risk — see below), only this project's `CcdMonitorController` needs to be replaced or reworked; `Core` and `App` are untouched.
- **App is a thin shell.** MVVM view/viewmodel split is enough structure for two windows — no need for a router, modules, or a full DI framework, though a lightweight container (or manual constructor injection) keeps the composition root (App.xaml.cs) as the one place that wires interfaces to real implementations, so swapping to fakes for manual testing is a one-line change.

## Architectural Patterns

### Pattern 1: Adapter/Facade over raw Win32 & COM interop

**What:** Every raw Windows API surface (CCD structs, `IPolicyConfig` COM interface, `user32.dll` P/Invoke) is wrapped by a small interface (`IMonitorController`, `IAudioController`, `IAppController`) exposing domain-shaped methods, not API-shaped ones.
**When to use:** Always, for this kind of app — the raw APIs are either undocumented, marshalled structs, or COM, all of which are painful to call directly from ViewModels and impossible to unit test without a real machine.
**Trade-offs:** Slightly more boilerplate (interface + impl per adapter) for a 1-developer/1-user app, but it's what makes the monitor-disable risk containable and the rest of the app testable.

**Example:**
```csharp
public interface IMonitorController
{
    IReadOnlyList<MonitorInfo> GetActiveMonitors();
    MonitorState CaptureState(string monitorId);
    void Disable(string monitorId);
    void Restore(MonitorState previousState);
}
```

### Pattern 2: Snapshot-before-mutate state machine, snapshot persisted to disk

**What:** Before any destructive OS change is made, capture the "current" monitor+audio configuration into a `StateSnapshot` object and **write it to disk synchronously** before touching any real system setting. Only after the write succeeds does `ToggleService` proceed to call `Disable`/`SetDefaultDevice`/`Launch`.
**When to use:** Any toggle-and-restore tool where the "restore" step must reproduce an arbitrary prior state (not a fixed preset) and where the app might be killed or crash between capture and restore.
**Trade-offs:** A few extra milliseconds of disk I/O per toggle — irrelevant for a manually-triggered, once-in-a-while action. The alternative (in-memory-only snapshot) is strictly worse and is called out as an anti-pattern below.

**Example:**
```csharp
public async Task ToggleToRigMode()
{
    var settings = _settingsStore.Load();
    var snapshot = new StateSnapshot
    {
        Monitor = _monitorController.CaptureState(settings.MonitorId),
        Audio   = _audioController.CaptureState(settings.PlaybackDeviceId)
    };
    _snapshotStore.Save(snapshot);          // persisted BEFORE mutation
    _monitorController.Disable(settings.MonitorId);
    _audioController.SetDefault(settings.RigDeviceId);
    _appController.LaunchOrFocus(settings.AppPath);
}
```

### Pattern 3: Derive "current mode" from snapshot presence, not a separate flag

**What:** Don't maintain an independent `isInRigMode` boolean anywhere (in memory or in a settings file). Instead, `Mode == RigMode` **iff** a valid snapshot file exists on disk; `Mode == NormalMode` iff it doesn't. On app startup, `ToggleService` checks `_snapshotStore.Exists()` to decide which mode the GUI should show and which toggle direction is available.
**When to use:** Whenever "was the app in the middle of something" must survive an app restart/crash, and there's already a natural on-disk artifact (the snapshot) whose existence encodes that fact.
**Trade-offs:** Simpler and more crash-safe than tracking mode separately (no risk of flag/file getting out of sync with each other) — the risk is external drift (user manually changes audio/display settings while the app "thinks" it's in rig mode), which is a UX/pitfall concern, not an architectural one; note in GUI copy that "restore" reflects the last-known snapshot, not necessarily current live truth.

## Data Flow

### Toggle-to-rig-mode flow

```
[User clicks Toggle]
    ↓
MainViewModel.ToggleCommand
    ↓
ToggleService.ToggleToRigMode()
    ↓
SettingsStore.Load() ──► AppSettings (monitor id, audio ids, app path)
    ↓
MonitorController.CaptureState() + AudioController.CaptureState()
    ↓
SnapshotStore.Save(snapshot)   [disk write, must complete before next step]
    ↓
MonitorController.Disable(monitorId)
    ↓
AudioController.SetDefault(rigDeviceId)
    ↓
AppController.LaunchOrFocus(appPath)
    ↓
ToggleService returns success/failure per step
    ↓
MainViewModel updates mode indicator, shows any partial-failure warning
```

### Toggle-back flow

```
[User clicks Toggle again]
    ↓
ToggleService.ToggleToNormalMode()
    ↓
SnapshotStore.Load() ──► StateSnapshot (previous monitor+audio state)
    ↓
MonitorController.Restore(snapshot.Monitor)
    ↓
AudioController.Restore(snapshot.Audio)
    ↓
AppController.MinimizeIfRunning(appPath)
    ↓
SnapshotStore.Clear()   [only after restore steps succeed — mode flips back to Normal]
    ↓
MainViewModel updates mode indicator
```

### Settings flow (independent of toggle flow)

```
SettingsView ←→ SettingsViewModel ←→ SettingsStore (load/save JSON)
   ↑ (enumerate live options)
MonitorController.GetActiveMonitors()
AudioController.GetPlaybackDevices()
```
Settings and Toggle are decoupled: `ToggleService` only *reads* settings at toggle-time, it never writes them. Settings changes take effect on the *next* toggle, never mid-flight.

### Key Data Flows

1. **Snapshot-before-mutate:** The single most important flow in the app. `SnapshotStore.Save()` must complete and be flushed to disk before `MonitorController.Disable()` is called — this is what protects against "app crashed/closed mid-toggle" leaving the user's monitor/audio state unrecoverable.
2. **Mode derivation on startup:** On every app launch, `ToggleService` checks `SnapshotStore.Exists()` before the GUI renders its mode indicator, so a killed/closed process doesn't lose track of "we were mid-rig-mode."
3. **Settings as read-only input to Toggle:** Settings never flow the other direction — Toggle never mutates saved settings, only the snapshot.

## Scaling Considerations

This is a single-user, single-machine, manually-triggered utility — "scale" here means feature growth, not user load.

| Scenario | Architecture Adjustments |
|-------|--------------------------|
| v1 (this milestone): GUI-triggered toggle only | Current design as described is sufficient — no background service, no tray, no hotkey listener needed |
| + Global hotkey (deferred feature) | Add a hotkey-listener component that calls the same `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()` — no changes needed to Core or Windows layers, since orchestration is already UI-agnostic |
| + Tray residency / autostart | Add a tray-icon host around the same `App` composition root; `ToggleService` and adapters are already decoupled from the main window, so this is additive, not a rewrite |
| + Multiple named profiles (future) | `AppSettings` becomes a list of named profiles instead of a single set; `ToggleService` takes a profile id parameter — the snapshot/restore state machine itself doesn't change |

### Scaling Priorities

1. **First likely extension:** Hotkey trigger — architecturally trivial given the ViewModel-agnostic `ToggleService`, as long as GUI logic was never allowed to leak into `ToggleService` in v1.
2. **Second likely extension:** Tray/autostart — only affects the App shell/composition root, not Core or Windows layers, provided those weren't coupled to `MainWindow` lifetime.

## Anti-Patterns

### Anti-Pattern 1: In-memory-only snapshot

**What people do:** Keep the "state to restore" as a field on a ViewModel or service instance, relying on the process staying alive between toggle-to-rig and toggle-back.
**Why it's wrong:** If the app is closed, crashes, or the machine sleeps/restarts while in rig mode, the previous state is lost forever and the user is stuck with a disabled monitor and no memory of what it was before. This directly contradicts the project's own "exact restore" requirement.
**Do this instead:** Persist the snapshot to disk synchronously the moment it's captured, before any mutation happens (Pattern 2 above).

### Anti-Pattern 2: P/Invoke or COM calls scattered in ViewModels/code-behind

**What people do:** Call `SetDisplayConfig` or the `IPolicyConfig` COM interface directly from a button-click handler or ViewModel method because "it's just a small app."
**Why it's wrong:** Makes the riskiest, least-documented part of the app (monitor disable) untestable and tightly bound to the GUI framework; any future fix to the monitor-disable approach means hunting through UI code.
**Do this instead:** Route everything through the adapter interfaces (Pattern 1); keep all interop in the dedicated `RigToggle.Windows` project.

### Anti-Pattern 3: Treating "mode" as an independently-tracked flag

**What people do:** Store `IsRigMode: true/false` in the settings file alongside user preferences, updated on each toggle.
**Why it's wrong:** Two sources of truth (the flag and the snapshot's existence) can drift out of sync — e.g., app crashes after writing the flag but before finishing the snapshot write, or vice versa — leaving an inconsistent state that's hard to reason about on next launch.
**Do this instead:** Derive mode purely from snapshot presence (Pattern 3); one artifact, one truth.

### Anti-Pattern 4: Assuming the toggle sequence can't partially fail

**What people do:** Fire off monitor-disable, audio-switch, and app-launch as an all-or-nothing block with no per-step error handling, assuming Windows APIs "just work."
**Why it's wrong:** Any of the three OS operations can fail independently (audio device unplugged, monitor ID changed after a driver update, target app path no longer exists) — a naive implementation leaves the system in a half-toggled state with no signal to the user about what succeeded.
**Do this instead:** `ToggleService` should treat each step's outcome independently, continue where safe, and report a clear "partial success" state back to the GUI rather than silently swallowing exceptions or crashing mid-sequence.

## Integration Points

### Windows Subsystems (in place of "External Services")

| Subsystem | Integration Pattern | Notes |
|---------|---------------------|-------|
| CCD Display API (`QueryDisplayConfig`/`SetDisplayConfig`) | P/Invoke, struct marshalling | No officially documented public flag exists for a true "disconnect this display" equivalent to the Settings UI action (confirmed via Microsoft Q&A as of current research) — the commonly cited working approach (used by tools like NirSoft MultiMonitorTool) manipulates the active-path array passed to `SetDisplayConfig` to exclude the target monitor. This needs a standalone feasibility spike before committing to it as the implementation; a fallback (shelling out to an existing tool, or a devcon-style device-disable) should be considered if the CCD path-exclusion approach doesn't yield a true disconnect. |
| Core Audio / default device switching | COM interop against the undocumented `IPolicyConfig`/`IPolicyConfigVista` interface, OR a maintained wrapper library (e.g. AudioSwitcher.AudioApi on NuGet) | This interface is undocumented but has been stable across Windows versions for years and is the mechanism virtually every audio-switching tool (SoundVolumeView, EarTrumpet, etc.) relies on; using a maintained library avoids re-deriving the COM GUIDs by hand. |
| Win32 window/process APIs (`FindWindow`, `ShowWindow`, `SetForegroundWindow`, `Process` class) | Direct P/Invoke + `System.Diagnostics.Process` | Well documented, low risk. Note: Windows restricts an app from forcibly stealing foreground focus from the user's active window in some cases — `SetForegroundWindow` may only flash the taskbar instead of truly focusing; acceptable fallback behavior, not a blocker. |
| Filesystem (`%LocalAppData%`) | Plain file I/O, JSON serialization | Use `LocalApplicationData`, not roaming — this is a single-machine tool, no need for roaming profile sync. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| GUI ↔ ToggleService | Direct method call (sync or async `Task`) | No event bus needed at this scale; a simple async command binding from the ViewModel is sufficient. |
| ToggleService ↔ Control Adapters | Interface calls (`IMonitorController`, `IAudioController`, `IAppController`) | ToggleService only ever depends on interfaces, never concrete Windows-layer classes — enables fakes for testing and swappable implementations for the monitor-disable risk. |
| ToggleService ↔ Persistence | Interface calls (`ISettingsStore`, `ISnapshotStore`) | Both are simple load/save; no need for a generic repository abstraction beyond these two concrete stores. |
| Settings ↔ Toggle | One-directional (Settings → Toggle reads only) | Prevents the toggle flow from ever mutating user-configured settings. |

## Suggested Build Order

Two different orderings matter here and they answer different questions — which to build for clean dependency structure, and which to *validate* first because everything else is riskless in comparison.

**1. Data contracts + persistence first (no OS dependency at all).**
Define `AppSettings` and `StateSnapshot` models and their JSON stores (`SettingsStore`, `SnapshotStore`) before anything else. These have zero Windows API dependency, are trivial to unit test, and every other component's shape (what a "monitor state" or "audio state" snapshot even contains) flows from these models. Get this right first since the adapters below will be built to fill in these data shapes.

**2. GUI shell against fake/stub controllers.**
Build `MainWindow`/`SettingsWindow` and their ViewModels wired to hand-written fake implementations of `IMonitorController`/`IAudioController`/`IAppController` (in-memory, no real OS calls). This validates the full UX (settings round-trip, toggle button, mode indicator, snapshot-derived mode-on-startup logic) with zero risk and no rig hardware needed to iterate quickly. This is explicitly answerable "yes" to the downstream question: the GUI can and should be built and fully validated before any real monitor/audio control code exists.

**3. Process/window control (real implementation) — lowest-risk real OS integration.**
Swap in the real `IAppController` next. `Process`/`user32.dll` APIs are well documented and low-risk; this is the best first real-OS-interop component to validate the "swap fake for real, wire into ToggleService" pattern before tackling the two riskier adapters.

**4. Audio control (real implementation) — moderate risk.**
Swap in the real `IAudioController`. The API surface (`IPolicyConfig` COM interop, or a wrapper library) is undocumented but well-trodden by existing tools; expect some COM interop friction but a known-working path exists.

**5. Monitor control (real implementation) — highest risk, and the one to de-risk earliest, not last.**
This is the component the other four's "clean build order" doesn't reflect: it is the project's entire core value proposition (per PROJECT.md), and it's the one with no officially documented API guarantee. **Recommendation for roadmap:** run a standalone throwaway spike/prototype of the CCD path-exclusion approach (or the devcon/external-tool fallback) as early as possible — ideally before or in parallel with step 2 — purely to answer the go/no-go question "can this app actually make Windows treat the primary monitor as absent, not just powered-off." If the spike fails, the whole project's premise needs re-evaluation before more work is sunk into GUI/settings/other adapters. Once feasibility is confirmed, the production `CcdMonitorController` implementation slots into the architecture exactly like the other adapters (step 3/4 pattern), last in the "clean" build order but validated first in the "risk" order.

**6. Orchestration wiring (`ToggleService`) — assembled last, from parts already validated.**
Once all three adapters have real implementations, wire them into `ToggleService`'s snapshot → mutate → restore sequence, including the partial-failure handling called out in Anti-Pattern 4. Because `ToggleService` was already exercised against fakes in step 2, this step is mostly "swap fakes for reals" rather than writing new orchestration logic from scratch.

## Sources

- Microsoft Learn — CCD APIs overview: https://learn.microsoft.com/en-us/windows-hardware/drivers/display/connecting-and-configuring-displays (MEDIUM — describes documented capability, does not confirm a "disable/disconnect" flag)
- Microsoft Q&A — "Is there a supported way to script / automate 'Disconnect this display'": https://learn.microsoft.com/en-us/answers/questions/5662114/windows-11-is-there-a-supported-way-to-script-auto (MEDIUM — community/MS confirmation that no documented public flag exists for this exact action)
- Microsoft Learn — SetDisplayConfig summary and scenarios: https://learn.microsoft.com/en-us/windows-hardware/drivers/display/setdisplayconfig-summary-and-scenarios (HIGH — official API reference)
- NirSoft MultiMonitorTool (reference implementation of the target behavior, exact internals not publicly documented): https://www.nirsoft.net/utils/multi_monitor_tool.html (LOW-MEDIUM — functional description only, internals unconfirmed)
- AudioSwitcher.AudioApi source (reference for default-device-switching COM interop pattern): https://github.com/xenolightning/AudioSwitcher (MEDIUM — widely used community library)
- Microsoft docs-desktop — WinForms/WPF application settings architecture: https://github.com/dotnet/docs-desktop/blob/main/dotnet-desktop-guide/framework/winforms/advanced/application-settings-architecture.md (HIGH — official)
- General MVVM structure guidance (community, cross-verified across multiple sources): https://blog.rsuter.com/recommendations-best-practices-implementing-mvvm-xaml-net-applications/, https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/february/patterns-wpf-apps-with-the-model-view-viewmodel-design-pattern (MEDIUM-HIGH)
- Win32 window control APIs (`SetForegroundWindow`, `ShowWindow`, single-instance mutex pattern) — cross-verified across multiple community sources and Microsoft Learn (HIGH for API existence/behavior, MEDIUM for community usage patterns)

---
*Architecture research for: Windows desktop GUI utility (monitor/audio/process toggle tool)*
*Researched: 2026-07-24*
