# Phase 2: Foundations & GUI Shell - Research

**Researched:** 2026-07-24
**Domain:** WinForms (.NET 10) desktop GUI shell + settings persistence, wired to real hardware/process enumeration and fake mutation adapters
**Confidence:** HIGH

## Summary

This phase is almost entirely "assemble known, stable .NET/WinForms APIs correctly" — there is no undocumented-API risk here (that risk lives in Phases 3/4, already flagged in `PITFALLS.md`). The two libraries this phase newly wires into production code — `WindowsDisplayAPI` (monitor enumeration) and NAudio's `MMDeviceEnumerator` (audio endpoint enumeration) — are already proven to work on this exact rig hardware (`WindowsDisplayAPI.PathInfo.GetActivePaths()` is the literal call already exercised non-elevated in the Phase 1 spike, `spike/MonitorDetachSpike/Program.cs:55,74`). This phase reuses that exact call for the Settings monitor picker.

The one genuinely non-obvious design decision this research resolves: **which adapter methods count as "real" vs. "fake" under D-05/D-08.** D-05 says only *mutating* calls (`Disable`, `SetDefault`, `Launch`/focus/minimize) are faked — but `ToggleService`'s snapshot step (`CaptureState()`) is a **read**, not a mutation, so it should be wired to real enumeration too, not stubbed. This means Phase 2's `ToggleService` will write a snapshot file containing genuinely-current monitor/audio state (harmless, since nothing downstream acts on it yet) — a stronger, more realistic exercise of the full pipeline than a fully-fake snapshot would be, and it means Phase 4/5 only need to fill in `Disable`/`Restore`/`SetDefault` method bodies, not rewrite the read paths.

**Primary recommendation:** Implement one concrete adapter class per interface (`WindowsMonitorController`, `WindowsAudioController`, `WindowsAppController`) in a new `RigToggle.Windows` project, where every *read* method (`GetActiveMonitors`, `CaptureState`, `GetPlaybackDevices`, `IsRunning`) calls the real WindowsDisplayAPI/NAudio/`Process` APIs now, and every *mutating* method (`Disable`, `Restore`, `SetDefault`, `LaunchOrFocus`, `MinimizeIfRunning`) is a documented no-op stub in Phase 2. Phase 3/4/5 fill in only the stub bodies — no new adapter classes, no rename, no interface change.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** WinForms (per CLAUDE.md's primary recommendation) — not WPF. Two windows (Main, Settings), no XAML.
- **D-02:** System-default WinForms visual styling — no custom colors/fonts/theming.
- **D-03:** Settings window is a **modal dialog** launched from a button on Main — blocks Main until closed/saved. Not a separate non-modal window.
- **D-04:** Main window is **fixed-size**, not resizable.
- **D-05:** Monitor and audio device pickers in Settings show **real enumerated hardware** (via `WindowsDisplayAPI` for monitors, NAudio's `MMDeviceEnumerator` for audio endpoints, per STACK.md) — not placeholder/hardcoded data. Only the actual `Disable()` / `SetDefault()` mutation calls are faked (no-op) in this phase.
- **D-06:** Companion app path field uses a **file-browser dialog** (`OpenFileDialog` filtered to `.exe`), not a free-text field.
- **D-07:** "Is companion app already running" detection is **real now** (`Process.GetProcessesByName`, per CLAUDE.md — trivial, zero-risk, read-only). Only launch/focus/minimize actions stay faked until Phase 3.
- **D-08:** Phase 2 includes a **real `ToggleService` orchestration class** (per ARCHITECTURE.md's Pattern 2 — snapshot → mutate → restore sequencing), wired to the real enumeration adapters and fake mutation adapters. Phase 5 becomes an adapter swap, not new orchestration logic. `ToggleService` itself has zero Windows API references and is fully unit-testable now.
- **D-09:** Settings dialog is **one window with three labeled sections** (Monitor, Audio devices, App path) — no tabs. Content is small enough to not need tab structure.
- **D-10:** If a previously-saved monitor/audio device/app path is no longer found when Settings reopens (hardware changed), show the picker as **unselected with an inline warning** (e.g. "Previously selected device not found — please reselect"), rather than silently keeping a stale ID or showing a greyed-out stale entry.
- **D-11:** No manual "Refresh" button — Settings **re-enumerates every time it opens**. This is a rarely-opened one-time setup screen, not left open while plugging/unplugging hardware.
- **D-12:** Settings' Save button is **blocked/disabled until all three fields are validly selected** (monitor, both audio devices, app path) — no partial saves.
- **D-13:** Main window ships with its **full intended layout in Phase 2**, wired to real `ToggleService` + fake mutation adapters — not a placeholder mockup. Clicking Toggle actually runs the full snapshot → fake-mutate → flip-mode sequence end-to-end; it just has no real hardware effect yet. Avoids reworking the window in Phase 5.
- **D-14:** Current mode (Normal vs. Rig) is **derived from snapshot-file presence** on disk (per ARCHITECTURE.md's Pattern 3: `Mode == RigMode` iff a valid snapshot file exists), not a separate in-memory/persisted flag. This means startup-mode-detection (CORE-05, mapped to Phase 5) is effectively already exercised correctly in Phase 2 with the fake snapshot store.
- **D-15:** Main window shows a **small status line** for companion-app running state (e.g. "Moza Companion: Running" / "Not running"), reflecting the real detection from D-07 — not left as invisible internal state.

### Claude's Discretion

None — every discussed question reached an explicit user choice (all "Recommended" options were accepted as presented).

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. (Confirmation dialog before disabling the primary monitor is already correctly scoped to Phase 4 per ROADMAP.md DISPLAY-03 / Phase 4 Success Criterion #3 — not raised here to avoid scope creep.)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SETTINGS-01 | User can select which monitor is the "primary to disable" from a list of detected displays | `WindowsDisplayAPI.PathInfo.GetActivePaths()` enumeration pattern (Code Examples), `DevicePath` as the persisted stable identifier, ComboBox binding pattern with stale-detection (D-10) |
| SETTINGS-02 | User can select which audio devices form the toggle pair (normal device, rig device) from a list of detected audio endpoints | NAudio `MMDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)` pattern, `MMDevice.ID`/`FriendlyName`, same ComboBox binding + stale-detection pattern applied twice |
| SETTINGS-03 | User can specify the file path of the companion app to launch/focus/minimize | `OpenFileDialog` filtered to `*.exe` (D-06) pattern, path validation before persisting |
| SETTINGS-04 | Settings persist across app restarts | `System.Text.Json` POCO + atomic `File.Move(..., overwrite: true)` write pattern to `%LocalAppData%\RigToggle\settings.json` |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Main window UI (mode indicator, Toggle button, status line) | GUI Layer (`RigToggle.App`) | — | Pure presentation; no OS interop directly in code-behind (Anti-Pattern 2 in ARCHITECTURE.md) |
| Settings dialog UI (pickers, validation, Save/Discard) | GUI Layer (`RigToggle.App`) | — | Same — validation logic (D-12) lives in the form, but reads settings/enumeration through interfaces |
| Toggle orchestration (snapshot → mutate → restore sequencing) | Orchestration Layer (`RigToggle.Core.ToggleService`) | — | Zero Windows API references (D-08); this is the one class that must be unit-testable without a Windows machine |
| Monitor enumeration (read) | Control Adapter (`RigToggle.Windows.WindowsMonitorController`) | GUI (consumes via interface) | Real WindowsDisplayAPI call, but it's a read, not a mutation — safe to make real in Phase 2 per D-05's actual scope |
| Monitor disable/restore (mutation) | Control Adapter (`RigToggle.Windows.WindowsMonitorController`) | — | No-op stub in Phase 2; real CCD `SetDisplayConfig` topology-removal lands in Phase 4 |
| Audio endpoint enumeration (read) | Control Adapter (`RigToggle.Windows.WindowsAudioController`) | GUI (consumes via interface) | Real NAudio `MMDeviceEnumerator` call; read-only |
| Audio default-device switch (mutation) | Control Adapter (`RigToggle.Windows.WindowsAudioController`) | — | No-op stub in Phase 2; real `IPolicyConfig` COM interop lands in Phase 3 |
| Companion-app running detection (read) | Control Adapter (`RigToggle.Windows.WindowsAppController`) | — | Real `Process.GetProcessesByName` per D-07; read-only |
| Companion-app launch/focus/minimize (mutation) | Control Adapter (`RigToggle.Windows.WindowsAppController`) | — | No-op stub in Phase 2; real Win32 `user32.dll` calls land in Phase 3 |
| Settings persistence | Persistence Layer (`RigToggle.Core.Persistence.JsonSettingsStore`) | — | Plain JSON file I/O, no Windows API dependency, fully real in Phase 2 (SETTINGS-04) |
| Snapshot persistence / mode derivation | Persistence Layer (`RigToggle.Core.Persistence.JsonSnapshotStore`) | — | Plain JSON file I/O; content is real-shaped (from real `CaptureState()` reads) but never consumed by a real mutation yet |

## Standard Stack

### Core (already chosen in `.planning/research/STACK.md` — reiterated here with phase-specific verification)

| Library | Version | Purpose | Verification |
|---------|---------|---------|--------------|
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 | CCD monitor enumeration (`PathInfo.GetActivePaths()`) | `[VERIFIED: NuGet registry]` — confirmed `1.3.0.13` is still the latest version on nuget.org (`api.nuget.org/v3-flatcontainer/windowsdisplayapi/index.json` queried directly this session, no newer version exists). Source shape (`PathDisplayTarget.DevicePath`, `PathInfo.GetActivePaths()`/`ApplyPathInfos()`) confirmed by reading `PathDisplayTarget.cs`/`PathInfo.cs` from `github.com/falahati/WindowsDisplayAPI` directly this session. Additionally proven to run non-elevated on this exact rig's AMD/DisplayPort hardware by the Phase 1 spike (`spike/MonitorDetachSpike/Program.cs`). |
| NAudio | 2.3.0 | Audio render-endpoint enumeration (`MMDeviceEnumerator`) | `[VERIFIED: NuGet registry]` — confirmed via `api.nuget.org/v3-flatcontainer/naudio/index.json` this session: `2.3.0` is the latest **stable** release (a `3.0.0-preview.*` line exists but is pre-release and not recommended for this project). API shape (`EnumerateAudioEndPoints`, `GetDefaultAudioEndpoint`, `GetDevice`, `MMDevice.ID`/`FriendlyName`) confirmed by reading `src/NAudio.Wasapi/CoreAudioApi/MMDeviceEnumerator.cs` and `MMDevice.cs` from `github.com/naudio/NAudio` directly this session. |
| `System.Text.Json` | .NET 10 BCL | Settings + snapshot persistence | `[VERIFIED: official docs]` — `File.Move(string, string, bool overwrite)` confirmed present and current via `learn.microsoft.com/en-us/dotnet/api/system.io.file.move` (fetched this session; available since .NET Core 3.0, applies through net-10.0). |
| Win32 P/Invoke (`Process.GetProcessesByName`) | .NET 10 BCL | Companion-app running detection (D-07) | `[VERIFIED: official docs]` — BCL API, no P/Invoke needed for detection itself (only for focus/minimize, which is faked this phase). |

**No new packages are installed beyond what STACK.md already specified.** This phase is the first to actually `dotnet add package` them into a production (`RigToggle.Windows`) project rather than the throwaway spike console app, but the packages themselves were already vetted in `.planning/research/STACK.md`.

### Installation

```bash
dotnet new sln -n RigToggle
dotnet new classlib -n RigToggle.Core -f net10.0
dotnet new classlib -n RigToggle.Windows -f net10.0-windows
dotnet new winforms -n RigToggle.App -f net10.0-windows

dotnet add RigToggle.Windows package WindowsDisplayAPI --version 1.3.0.13
dotnet add RigToggle.Windows package NAudio --version 2.3.0

dotnet sln add RigToggle.Core RigToggle.Windows RigToggle.App
dotnet add RigToggle.App reference RigToggle.Core RigToggle.Windows
dotnet add RigToggle.Windows reference RigToggle.Core
```

`RigToggle.Windows` needs `<TargetFramework>net10.0-windows</TargetFramework>` (not plain `net10.0`) because it references `System.Windows.Forms.Screen` (used the same way the Phase 1 spike used it, as a second independent oracle) and the CCD/COM-dependent libraries. `RigToggle.Core` can stay plain `net10.0` since it has zero Windows API references (D-08) — this is enforced structurally, not just by convention: `RigToggle.Core.csproj` should not reference `WindowsDisplayAPI` or `NAudio` at all, and CI/manual review should treat any such reference appearing there as a regression.

## Package Legitimacy Audit

> `slopcheck` (installed and runnable this session — confirmed via `slopcheck --help`) does **not** support the NuGet ecosystem (`--ecosystem` choices are `pypi, npm, crates.io, go, rubygems, maven, packagist` only — confirmed via `slopcheck install --help`). Per the Package Legitimacy Gate's graceful-degradation clause, this is treated as "cannot run for this ecosystem" and packages are marked `[ASSUMED]` for the audit table below, even though registry existence, version currency, and full source-code shape were independently confirmed this session (see Standard Stack table above) — this is a stronger verification bar than a typical `[ASSUMED]` claim, but the protocol's ecosystem-scoped slopcheck gate specifically was not satisfied, so the planner should still gate installation behind a lightweight `checkpoint:human-verify` per the protocol, noting the strength of the existing verification in that checkpoint's description.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `WindowsDisplayAPI` | NuGet | latest version `1.3.0.13`; package itself has existed for years (multiple prior `1.0.x`/`1.1.x`/`1.2.x` releases visible in registry history) | Not queryable without NuGet Gallery web scrape (not attempted); no ecosystem-specific slopcheck signal available | `github.com/falahati/WindowsDisplayAPI` (confirmed reachable, source read directly this session) | N/A — ecosystem unsupported by slopcheck | `[ASSUMED]` per protocol — but already validated by direct source read + successful non-elevated execution on this exact rig hardware in Phase 1 (strongest possible real-world verification short of a published slopcheck verdict) |
| `NAudio` | NuGet | Long-running project; latest stable `2.3.0`, `3.0.0-preview.*` line also present (pre-release, not used) | Not queryable without NuGet Gallery web scrape (not attempted) | `github.com/naudio/NAudio` (confirmed reachable, source read directly this session) | N/A — ecosystem unsupported by slopcheck | `[ASSUMED]` per protocol — de facto standard .NET audio library (already noted in STACK.md), source-verified this session |

**Packages removed due to slopcheck `[SLOP]` verdict:** none (no NuGet ecosystem check was possible).
**Packages flagged as suspicious `[SUS]`:** none identified by manual review (no postinstall-script equivalent risk in NuGet packaging; both packages' source was read directly and contains only the expected P/Invoke/COM-interop and managed enumeration code, no network calls or unexpected file-system access).

**Recommendation for planner:** Insert a lightweight `checkpoint:human-verify` before the `dotnet add package` steps for `WindowsDisplayAPI`/`NAudio` per protocol, but this can reasonably be a fast rubber-stamp given: (1) both packages are unchanged from what STACK.md already specified and sourced at project-research time, (2) `WindowsDisplayAPI` was already installed and run successfully against real hardware in Phase 1, and (3) this research session re-confirmed both are still the current, non-yanked, latest-stable NuGet versions.

## Architecture Patterns

### System Architecture Diagram

```
┌───────────────────────────────────────────────────────────────────┐
│ RigToggle.App (WinForms, entry point)                              │
│                                                                     │
│  MainForm                          SettingsForm (modal, D-03)      │
│   - mode indicator (D-14)           - Monitor ComboBox (D-05,D-10) │
│   - Toggle button                   - Audio ComboBox x2 (D-05,D-10)│
│   - Settings button ──ShowDialog()─→ - App path TextBox+Browse(D-06)│
│   - companion status line (D-15)    - Save/Discard (D-12)          │
│        │ Toggle click                    │ Save click              │
│        ▼                                 ▼                         │
├───────────────────────────────────────────────────────────────────┤
│ RigToggle.Core (zero Windows API refs — D-08)                      │
│                                                                     │
│  ToggleService.ToggleToRigMode()/ToggleToNormalMode()               │
│    → ISettingsStore.Load()                                          │
│    → IMonitorController.CaptureState() + IAudioController.CaptureState()│
│    → ISnapshotStore.Save(snapshot)   [real write, before mutation] │
│    → IMonitorController.Disable() [fake]                           │
│    → IAudioController.SetDefault() [fake]                          │
│    → IAppController.LaunchOrFocus() [fake]                         │
│                                                                     │
│  JsonSettingsStore / JsonSnapshotStore (real file I/O, both modes) │
├───────────────────────────────────────────────────────────────────┤
│ RigToggle.Windows (all OS interop; enumeration real, mutation fake)│
│                                                                     │
│  WindowsMonitorController : IMonitorController                     │
│    GetActiveMonitors() → WindowsDisplayAPI.PathInfo.GetActivePaths()│
│    CaptureState()      → same real read                            │
│    Disable()/Restore() → no-op (Phase 4 fills these in)            │
│                                                                     │
│  WindowsAudioController : IAudioController                         │
│    GetPlaybackDevices() → NAudio MMDeviceEnumerator.EnumerateAudioEndPoints│
│    CaptureState()       → same real read                           │
│    SetDefault()         → no-op (Phase 3 fills this in)            │
│                                                                     │
│  WindowsAppController : IAppController                             │
│    IsRunning()          → Process.GetProcessesByName (real, D-07)  │
│    LaunchOrFocus()/MinimizeIfRunning() → no-op (Phase 3 fills in)  │
└───────────────────────────────────────────────────────────────────┘
                              │
                              ▼
              %LocalAppData%\RigToggle\settings.json
              %LocalAppData%\RigToggle\state.json  (presence = Rig mode, D-14)
```

### Recommended Project Structure

```
src/
├── RigToggle.App/
│   ├── Program.cs                 # [STAThread] Main, composition root (new up real adapters)
│   ├── MainForm.cs / .Designer.cs
│   └── SettingsForm.cs / .Designer.cs
├── RigToggle.Core/                # no WinForms/WindowsDisplayAPI/NAudio references
│   ├── ToggleService.cs
│   ├── Models/
│   │   ├── AppSettings.cs
│   │   └── StateSnapshot.cs
│   ├── Abstractions/
│   │   ├── IMonitorController.cs
│   │   ├── IAudioController.cs
│   │   ├── IAppController.cs
│   │   ├── ISettingsStore.cs
│   │   └── ISnapshotStore.cs
│   └── Persistence/
│       ├── JsonSettingsStore.cs
│       └── JsonSnapshotStore.cs
├── RigToggle.Windows/               # net10.0-windows; real enumeration + fake mutation THIS phase
│   ├── WindowsMonitorController.cs
│   ├── WindowsAudioController.cs
│   └── WindowsAppController.cs
└── RigToggle.Tests/
    └── ToggleServiceTests.cs        # against hand-written test doubles for the three interfaces
```

### Pattern 1: Real-read / fake-mutation split within a single adapter class

**What:** Rather than a separate `FakeMonitorController` and later a separate `RealMonitorController`, implement one `WindowsMonitorController : IMonitorController` now. Every read-only method calls the real API immediately; every mutating method is a clearly-commented no-op that Phase 4 fills in.
**When to use:** Whenever D-05-style "real enumeration, fake mutation" phasing applies — it's the correct read of D-05/D-08's actual boundary (mutation vs. read, not "this whole subsystem" vs. "that whole subsystem").
**Why this matters for planning:** it means Phase 3/4/5 tasks are "implement method body X in file Y that already exists and is already wired into DI/composition," not "create new class, delete old class, rewire composition root" — smaller, lower-risk diffs later.

```csharp
// Source: shape confirmed via github.com/falahati/WindowsDisplayAPI (PathInfo.cs, PathDisplayTarget.cs), read directly this session
public sealed class WindowsMonitorController : IMonitorController
{
    public IReadOnlyList<MonitorInfo> GetActiveMonitors()
    {
        var paths = WindowsDisplayAPI.DisplayConfig.PathInfo.GetActivePaths(virtualModeAware: false);
        var result = new List<MonitorInfo>();
        foreach (var path in paths)
        {
            foreach (var targetInfo in path.TargetsInfo)
            {
                var target = targetInfo.DisplayTarget;
                result.Add(new MonitorInfo(
                    DevicePath: target.DevicePath,
                    FriendlyName: target.FriendlyName ?? "(unknown display)",
                    IsPrimary: path.IsGDIPrimary));
            }
        }
        return result;
    }

    public MonitorState CaptureState(string monitorDevicePath)
        => new MonitorState(monitorDevicePath); // real read today; Phase 4 may enrich with full DISPLAYCONFIG_PATH_INFO/MODE_INFO arrays per PITFALLS.md Pitfall 7

    public void Disable(string monitorDevicePath)
    {
        // FAKE in Phase 2 — no-op. Real CCD topology-path-removal
        // (PathInfo.ApplyPathInfos with the target's path excluded) lands in Phase 4.
    }

    public void Restore(MonitorState previousState)
    {
        // FAKE in Phase 2 — no-op. Real ApplyPathInfos(originalActivePaths) lands in Phase 4.
    }
}
```

### Pattern 2: ComboBox bound to enumerated hardware with stable-ID value + stale-selection detection (D-10)

**What:** A small display/value wrapper record bound via `DisplayMember`/`ValueMember`, populated on every `Form.Load` (D-11), with explicit "saved ID not found among current items" handling.
**When to use:** All three Settings pickers (monitor, 2x audio) follow this identical shape.

```csharp
public sealed record PickerItem(string Id, string DisplayLabel);

private void PopulateMonitorPicker()
{
    var monitors = _monitorController.GetActiveMonitors(); // real, per D-05
    var items = monitors
        .Select(m => new PickerItem(m.DevicePath, m.IsPrimary ? $"{m.FriendlyName} (Primary)" : m.FriendlyName))
        .ToList();

    cboMonitor.SelectedIndexChanged -= OnPickerChanged; // suspend during binding to avoid a
    cboMonitor.DataSource = items;                       // spurious "changed" event firing mid-populate
    cboMonitor.DisplayMember = nameof(PickerItem.DisplayLabel);
    cboMonitor.ValueMember = nameof(PickerItem.Id);
    cboMonitor.SelectedIndex = -1;

    string? savedId = _settings.MonitorDevicePath;
    if (savedId is not null)
    {
        var match = items.FirstOrDefault(i => i.Id == savedId);
        if (match is not null)
        {
            cboMonitor.SelectedItem = match;
        }
        else
        {
            // D-10: previously configured, but not found now — unselected + inline warning.
            // Do NOT show this warning on first-ever run (savedId is null in that case, see below).
            errorProviderMonitor.SetError(cboMonitor, "Previously selected monitor not found — please reselect.");
            lblMonitorWarning.Visible = true;
        }
    }
    cboMonitor.SelectedIndexChanged += OnPickerChanged;
    ValidateSettingsForm(); // re-evaluate Save-button enabled state (D-12)
}
```

**Pitfall this avoids:** assigning `ComboBox.DataSource` can fire `SelectedIndexChanged` during the assignment itself (a well-known WinForms binding quirk) — unhooking the handler around the populate call prevents a spurious "changed" event from clearing `errorProvider` state or re-running validation with an incomplete list.

**First-run vs. stale distinction:** the D-10 warning copy ("Previously selected X not found — please reselect") is only correct when `_settings.MonitorDevicePath` (etc.) is **non-null** but doesn't match a current item. On a brand-new install with an empty/just-created settings file, all three saved-ID fields are `null` — that's "never configured," not "stale," and should show the picker unselected with **no** warning, not the D-10 message. This distinction is not explicit in CONTEXT.md/UI-SPEC.md and is worth the planner making an explicit task/acceptance-criterion out of ("first-run: no warnings shown; only Save-disabled state signals incompleteness").

### Pattern 3: Settings persistence — create-if-missing load, atomic write

```csharp
// Source: File.Move(string,string,bool) confirmed via learn.microsoft.com/en-us/dotnet/api/system.io.file.move (fetched this session)
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonSettingsStore(string path) => _path = path;

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings(); // all fields null -> "never configured"
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!); // create-if-missing (SETTINGS-04)
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
        File.Move(tempPath, _path, overwrite: true); // atomic rename on same volume, avoids partial-write corruption
    }
}
```

### Pattern 4: Snapshot-presence-as-mode (D-14), Phase-2-appropriate content

**What:** `state.json`'s mere existence means "currently in Rig mode." In Phase 2, its *content* is populated from the real `CaptureState()` reads (Pattern 1) even though nothing downstream mutates based on it yet — this proves the plumbing end-to-end and means Phase 5's `CORE-05` (startup mode detection) is already correctly exercised.

```csharp
public sealed record StateSnapshot(MonitorState Monitor, AudioState Audio);

public sealed class JsonSnapshotStore : ISnapshotStore
{
    private readonly string _path; // %LocalAppData%\RigToggle\state.json

    public bool Exists() => File.Exists(_path);

    public void Save(StateSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot));
        File.Move(tempPath, _path, overwrite: true); // must complete before any mutation call (ARCHITECTURE.md Pattern 2)
    }

    public StateSnapshot? Load() => Exists() ? JsonSerializer.Deserialize<StateSnapshot>(File.ReadAllText(_path)) : null;

    public void Clear() { if (Exists()) File.Delete(_path); }
}
```

**Recommended location:** `%LocalAppData%\RigToggle\` for **both** `settings.json` and `state.json`. `ARCHITECTURE.md` (this project's own architecture research) specifies `%LocalAppData%`; `STACK.md` says `%APPDATA%` (roaming) for `settings.json` only. **This is a real, unresolved discrepancy between the two upstream research documents** — see Open Questions below. This document's recommendation is `%LocalAppData%` for both files, matching `ARCHITECTURE.md`'s explicit single-machine rationale ("no need for roaming profile sync") and keeping both files under one directory for a simpler backup/inspection story.

### Pattern 5: Settings dialog Save-button gating (D-12) and close-box behavior

```csharp
private void ValidateSettingsForm()
{
    bool monitorOk = cboMonitor.SelectedItem is PickerItem;
    bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
    bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
    bool appPathOk = File.Exists(txtAppPath.Text) && Path.GetExtension(txtAppPath.Text).Equals(".exe", StringComparison.OrdinalIgnoreCase);

    btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;
}
```

Wire `ValidateSettingsForm()` from each ComboBox's `SelectedIndexChanged` and from the Browse button's completion handler. Set `this.CancelButton = btnDiscardChanges;` in the form constructor/designer so the system close box (X) and Esc key both produce `DialogResult.Cancel` automatically — matching the UI-SPEC's "Discard Changes / close box: `DialogResult.Cancel`" without extra `FormClosing` handler code. `btnSaveSettings`'s `DialogResult = DialogResult.OK` is set declaratively; WinForms will not invoke a disabled `AcceptButton` via Enter, so no extra guard is needed for D-12's disabled-Save state.

### Pattern 6: Companion-app running detection (D-07) — process-name derivation

```csharp
// Source: pattern confirmed in STACK.md "What NOT to Use" table (Process.GetProcessesByName over FindWindow)
public bool IsRunning(string companionAppPath)
{
    string processName = Path.GetFileNameWithoutExtension(companionAppPath); // GetProcessesByName excludes ".exe"
    return Process.GetProcessesByName(processName).Length > 0;
}
```

`Process.GetProcessesByName` takes the process name **without** the `.exe` extension — a common first-try mistake is passing the full file name or full path, which silently returns zero matches (no exception, easy to miss in testing since it "just shows Not Running" — flag as a specific test case: verify detection with the *actual* configured `.exe` path, not a hardcoded literal process name).

### Anti-Patterns to Avoid

- **Faking `CaptureState()` alongside `Disable()`/`Restore()`:** would under-exercise the real pipeline for no reason — `CaptureState` is a read, D-05 only requires mutation to be faked. Keep it real.
- **Caching a single `MMDeviceEnumerator`/`WindowsDisplayAPI` query across the app's lifetime:** re-enumerate fresh every time Settings opens (D-11) and dispose/let-go of NAudio COM objects each time rather than holding them across the session — this also sidesteps `PITFALLS.md` Pitfall 5's COM-leak-across-repeated-calls concern, which applies just as much to repeated Settings-reopens as to repeated toggles.
- **Showing the D-10 stale-warning on first-ever run:** a `null` saved ID means "never configured," not "previously selected item now missing" — don't show the D-10 copy in that case (see Pattern 2).
- **Putting P/Invoke/COM calls in `MainForm`/`SettingsForm` code-behind:** route everything through `IMonitorController`/`IAudioController`/`IAppController`, even the currently-real enumeration calls — keeps `RigToggle.App` swappable/testable and matches `ARCHITECTURE.md` Anti-Pattern 2.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Monitor enumeration | Custom P/Invoke of `QueryDisplayConfig`/`DisplayConfigGetDeviceInfo` | `WindowsDisplayAPI.PathInfo.GetActivePaths()` | Already the project's chosen wrapper (STACK.md); already proven working on this exact hardware (Phase 1 spike) |
| Audio endpoint enumeration | Custom COM interop against `IMMDeviceEnumerator` | NAudio `MMDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)` | Already the project's chosen wrapper; NAudio wraps the same public, documented Core Audio enumeration API — no reason to duplicate |
| Settings/snapshot persistence format | Windows Registry, `app.config`/`user.config` | `System.Text.Json` POCO to a plain file | Already decided in STACK.md — simpler, hand-editable, debuggable |
| Atomic file write | Manual `FileStream` + flush + rename dance | `File.Move(tempPath, finalPath, overwrite: true)` (BCL, .NET Core 3.0+) | One BCL call does exactly this; no need for a custom transactional-write helper |
| Combo-box "selected value" bookkeeping | Manual index/string tracking parallel to the ComboBox's own state | `ComboBox.DataSource` + `DisplayMember`/`ValueMember` + `SelectedItem` | Standard WinForms data-binding; avoids index-drift bugs when the enumerated list order isn't guaranteed stable between opens |

**Key insight:** every "hard part" in this specific phase (CCD enumeration, Core Audio enumeration) was already solved by the libraries STACK.md chose — Phase 2's actual net-new work is glue code (ComboBox binding, JSON persistence, validation wiring), all of which is extremely well-trodden, stable WinForms/.NET territory.

## Common Pitfalls

### Pitfall 1: `ComboBox.DataSource` assignment fires `SelectedIndexChanged` mid-populate

**What goes wrong:** Setting `DataSource` on a bound `ComboBox` can raise `SelectedIndexChanged` before the intended saved-selection or unselected state is applied, causing `ValidateSettingsForm()` (D-12) to run against a half-populated form and the D-10 stale-warning logic to fire prematurely or clear itself incorrectly.
**Why it happens:** WinForms data-binding triggers change notifications as soon as the list/selection index is touched internally, not only when the developer explicitly changes selection.
**How to avoid:** Unhook the `SelectedIndexChanged` handler before calling `DataSource = ...`/`SelectedIndex = ...` inside the populate method, then rehook it and call the validation method exactly once at the end (Pattern 2).
**Warning signs:** Save button briefly flickers enabled/disabled during Settings load; stale-device warning icon appears then disappears without user interaction.

### Pitfall 2: `MMDeviceEnumerator.GetDevice(savedId)` behavior on a missing ID is unconfirmed to throw vs. return null

**What goes wrong:** Code that assumes `GetDevice` returns `null` for an unresolvable ID (rather than throwing) will NullReferenceException instead of hitting the intended D-10 stale-picker path.
**Why it happens:** The underlying COM call (`IMMDeviceEnumerator::GetDevice`) returns an `HRESULT` error for a not-found ID; NAudo's managed wrapper's exact behavior (exception vs. null) was not independently confirmed by direct execution in this research session (no Windows machine available in this sandbox — same limitation noted throughout Phase 1).
**How to avoid:** Wrap `GetDevice(savedId)` in a `try/catch (Exception)` (broad catch is acceptable here specifically because the *only* correct fallback behavior — D-10's unselected+warning state — is identical regardless of the specific exception type), rather than relying on a null check alone. This should be called out as an explicit task/test case: "reopen Settings after a saved audio device ID no longer resolves; confirm no unhandled exception, confirm D-10 warning shown."
**Warning signs:** Settings dialog crashes or fails to open at all after the previously-selected audio device is unplugged/renamed — a much worse failure mode than the intended graceful D-10 warning.

### Pitfall 3: First-run (`null` saved ID) vs. stale (non-null, unresolvable ID) are different UI states

**What goes wrong:** Showing the D-10 "Previously selected X not found — please reselect" warning on the very first run (before the user has ever picked anything) is confusing and technically false — nothing was "previously selected."
**Why it happens:** A naive stale-check implementation (`if (selected item not in list) show warning`) doesn't distinguish "saved value is null" from "saved value is set but absent."
**How to avoid:** Gate the D-10 warning specifically on `savedId is not null && !items.Any(i => i.Id == savedId)` — see Pattern 2.
**Warning signs:** A fresh install (empty `%LocalAppData%\RigToggle\`) shows three stale-device warnings on first Settings open, which will look like a bug during manual verification.

### Pitfall 4: Snapshot/Settings directory location disagreement between upstream research docs

**What goes wrong:** `STACK.md` specifies `%APPDATA%\RigToggle\settings.json` (roaming); `ARCHITECTURE.md` specifies `%LocalAppData%\RigToggle\settings.json` + `state.json` (local). Building `JsonSettingsStore`/`JsonSnapshotStore` against two different base paths (one file in Roaming, one in Local) is an easy, confusing mistake if a planner copies one path literally from each source doc without noticing the conflict.
**Why it happens:** The two research documents were produced independently (`/gsd:new-project` stack research vs. architecture research) and never cross-checked this specific detail.
**How to avoid:** Pick one (`%LocalAppData%` recommended, see Pattern 4 and Open Questions) and use it for **both** files consistently.
**Warning signs:** `settings.json` and `state.json` end up in two different directories after implementation, making manual inspection/debugging confusing (and, in principle, meaning a Windows profile roaming/sync scenario — not applicable to this single-machine tool, but still a needless inconsistency).

### Pitfall 5: `Process.GetProcessesByName` needs the name **without** `.exe`

**What goes wrong:** Passing the full configured path (e.g. `C:\Program Files\Moza\MozaCompanion.exe`) or the name **with** the extension to `GetProcessesByName` silently returns zero matches — no exception, so it's easy to mistake for "the app really isn't running" during testing.
**Why it happens:** `Process.ProcessName` (and therefore what `GetProcessesByName` matches against) never includes the `.exe` suffix.
**How to avoid:** Always derive the lookup name via `Path.GetFileNameWithoutExtension(settings.CompanionAppPath)` (Pattern 6), never a hardcoded literal or the raw configured string.
**Warning signs:** Companion-app status line (D-15) always shows "Not running" even when the app is visibly open.

### Pitfall 6: Reflexively adding an app manifest / elevation to unblock unrelated dev friction

**What goes wrong:** Not directly a Phase 2 risk (no mutation happens yet) but worth flagging now since the GUI shell's `Program.cs`/manifest is set up in this phase and carries forward unchanged into Phases 3–5: adding `requireAdministrator` "to be safe" at this stage would silently set the precedent Phase 3/4 then has to fight (per `PITFALLS.md` Pitfall 2 — breaks `SetForegroundWindow` against the non-elevated Moza Companion process via UIPI).
**How to avoid:** Do not add an application manifest with `requestedExecutionLevel` at all in Phase 2 — leave the WinForms template's default (`asInvoker`, implicit, no manifest needed) untouched, matching the Phase 1 spike's approach (`spike/MonitorDetachSpike/MonitorDetachSpike.csproj` has no `ApplicationManifest`).
**Warning signs:** A `.manifest` file or `<ApplicationManifest>` MSBuild property appears in `RigToggle.App.csproj` during this phase's review — should not exist yet, and should not default to `requireAdministrator` even later.

## Code Examples

### Full ComboBox stale-detection + validation wiring (Monitor picker; audio pickers and app-path field follow the identical shape)

```csharp
// Source: pattern synthesized from WinForms ComboBox/ErrorProvider official API (learn.microsoft.com,
// fetched this session) + this project's D-05/D-10/D-12 decisions
private void SettingsForm_Load(object sender, EventArgs e)
{
    _settings = _settingsStore.Load();
    PopulateMonitorPicker();
    PopulateAudioPickers();
    PopulateAppPathField();
    ValidateSettingsForm();
}
```

### `AppSettings` model shape (persisted POCO)

```csharp
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `File.Copy` + `File.Delete` for a "safe" write | `File.Move(src, dest, overwrite: true)` | Available since .NET Core 3.0 (long-current by .NET 10) | Simpler, and the source explicitly notes the copy+delete approach "isn't atomic" — prefer the 3-arg `Move` overload for the settings/snapshot writers |
| `AudioSwitcher.AudioApi`/older NAudio majors for enumeration | NAudio 2.3.0's `MMDeviceEnumerator` (actively maintained, already the project's chosen library) | N/A — this project already made this call in STACK.md | No change needed this phase; confirmed still current |

**Deprecated/outdated:** None specific to this phase's actual scope — all deprecation-relevant findings (legacy `ChangeDisplaySettingsEx`, `AudioSwitcher.AudioApi` staleness) were already captured in `STACK.md`/`PITFALLS.md` and apply to Phase 3/4, not this phase's read-only enumeration + fake-mutation scope.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `WindowsDisplayAPI` and `NAudio` NuGet package identities/authorship are legitimate (not slopsquatted) | Package Legitimacy Audit | Low — both already vetted in `STACK.md` at project-research time via direct GitHub source read, and `WindowsDisplayAPI` was already installed and executed successfully against real hardware in Phase 1; slopcheck itself simply doesn't cover the NuGet ecosystem, this isn't a new/unverified claim |
| A2 | `MMDeviceEnumerator.GetDevice(id)` throws (rather than returning null) for an unresolvable ID | Common Pitfalls (Pitfall 2) | Medium — if it instead returns `null`, a `try/catch`-only implementation would still work (catch block simply never triggers) as long as a null-check is *also* present; recommend implementing both a null-check and a catch block defensively so either behavior is handled correctly without needing to re-verify on a Windows machine first |
| A3 | `%LocalAppData%\RigToggle\` is the correct base path for both `settings.json` and `state.json` (resolving the STACK.md/ARCHITECTURE.md discrepancy in favor of Local, not Roaming) | Architecture Patterns (Pattern 4), Common Pitfalls (Pitfall 4) | Low-Medium — purely a file-location choice with no functional impact on a single-machine tool either way; wrong choice just means an inconsistency with one of the two research docs, not a runtime bug |

## Open Questions

1. **Settings/snapshot base directory: `%LocalAppData%` vs `%APPDATA%`?**
   - What we know: `ARCHITECTURE.md` recommends `%LocalAppData%` for both files with an explicit single-machine rationale; `STACK.md` recommends `%APPDATA%` (roaming) for `settings.json` specifically, with no explicit rationale given for roaming over local.
   - What's unclear: which document the planner should treat as authoritative when they conflict — neither is marked as superseding the other, and this phase is the first one to actually write these files.
   - Recommendation: Use `%LocalAppData%\RigToggle\` for both `settings.json` and `state.json` (this document's Pattern 4). This is a low-stakes, easily-changed-later decision; flag to the user only if they have a specific reason to prefer roaming (e.g., planned future multi-machine sync, which is explicitly out of scope per `REQUIREMENTS.md`'s "Cloud sync / multi-device profile sync" exclusion — reinforcing that Local is the better-justified choice here).

2. **Does `GetDevice(id)` throw or return null for a missing endpoint ID, and what exception type?**
   - What we know: The COM-level call returns a failure `HRESULT` for a not-found ID; NAudio's managed wrapper's exact translation of that (exception type, or a null return) could not be confirmed by direct execution in this Linux-sandboxed research session.
   - What's unclear: the precise exception type (if any) to catch.
   - Recommendation: Implement defensively (both a preceding existence check where feasible and a broad `try/catch` around the resolve call — see Common Pitfalls Pitfall 2); treat "reopen Settings with a since-removed audio device" as an explicit manual test case once the app is buildable on the actual Windows rig, same pattern as Phase 1's human-verification loop.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Building/running `RigToggle.App`/`RigToggle.Core`/`RigToggle.Windows` | ✗ (this Linux research sandbox) | — | None needed — per `01-CONTEXT.md` D-01, all Windows-native build/run/verification for this project happens on the user's actual Windows rig, not in this sandbox. This is an accepted, already-established project constraint, not a new gap. |
| NuGet registry access (`api.nuget.org`) | `dotnet add package WindowsDisplayAPI`/`NAudio` | ✓ (reachable from this sandbox during research; confirmed via direct `curl` this session) | n/a | — |
| GitHub raw source access | Verifying `WindowsDisplayAPI`/NAudio source shape this session | ✓ | n/a | — |

**Missing dependencies with no fallback:**
- None specific to this phase beyond the already-accepted Linux/Windows execution-environment split carried forward from Phase 1.

**Missing dependencies with fallback:**
- .NET 10 SDK absence in this research sandbox — fallback is the established pattern from Phase 1: this agent produces code/docs, the user builds/runs/verifies on the Windows rig.

## Security Domain

> `security_enforcement` is absent from `.planning/config.json` (treated as enabled per protocol), but this phase has essentially no attack surface: single-user, local-only, no network, no auth, no multi-tenant data. Applicable categories below are accordingly thin.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Single-user local desktop tool, no login/session concept |
| V3 Session Management | No | N/A |
| V4 Access Control | No | N/A — no multi-user/role concept (explicitly out of scope per `REQUIREMENTS.md`) |
| V5 Input Validation | Yes (narrow) | Companion-app path (D-06): validate the selected path exists and ends in `.exe` before persisting (Pattern 5's `appPathOk` check) — matches `PITFALLS.md`'s Security Mistakes table entry on validating the configured path before `Process.Start()` (Phase 3 concern, but the *validation gate* belongs in this phase's Settings save logic) |
| V6 Cryptography | No | No secrets, credentials, or encrypted data anywhere in this phase's scope |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Corrupted/hand-edited `settings.json` pointing `CompanionAppPath` at an arbitrary executable | Tampering | Validate path existence + `.exe` extension at both Save-time (this phase, Pattern 5) and Load-time before any future `Process.Start()` call (Phase 3) — low severity given this is a single-user local config file, but a cheap, already-planned guard |
| Partial/corrupted `settings.json`/`state.json` from an interrupted write | Tampering / Denial of Service (self-inflicted) | Atomic `File.Move(temp, final, overwrite: true)` write pattern (Pattern 3/4) — already the core design of this phase's persistence layer, directly prevents this |

## Sources

### Primary (HIGH confidence)
- `github.com/falahati/WindowsDisplayAPI` — `PathInfo.cs`, `PathDisplayTarget.cs` read directly this session (WebFetch) — confirmed `GetActivePaths()`/`GetAllPaths()`/`ApplyPathInfos()` signatures and `DevicePath`/`FriendlyName`/`EDIDManufactureId`/`EDIDProductCode` fields
- `github.com/naudio/NAudio` — `src/NAudio.Wasapi/CoreAudioApi/MMDeviceEnumerator.cs`, `MMDevice.cs` read directly this session (WebFetch) — confirmed `EnumerateAudioEndPoints`, `GetDefaultAudioEndpoint`, `TryGetDefaultAudioEndpoint`, `GetDevice`, `MMDevice.ID`/`FriendlyName`/`DataFlow`/`State`
- `api.nuget.org/v3-flatcontainer/{windowsdisplayapi,naudio}/index.json` — queried directly this session (curl) — confirmed current latest versions match `STACK.md`'s pins
- `learn.microsoft.com/en-us/dotnet/api/system.io.file.move` — fetched this session — confirmed 3-arg overwrite overload, available since .NET Core 3.0
- `learn.microsoft.com/en-us/dotnet/api/system.windows.forms.combobox.dropdownstyle` — fetched this session — confirmed `ComboBoxStyle` enum property
- `learn.microsoft.com/en-us/dotnet/api/system.windows.forms.errorprovider.seterror` — fetched this session — confirmed `SetError(Control, string)` signature and empty-string-clears-icon behavior
- `spike/MonitorDetachSpike/Program.cs` (this repo, Phase 1 deliverable) — confirms `PathInfo.GetActivePaths()` runs non-elevated and correctly on the actual rig's AMD/DisplayPort hardware

### Secondary (MEDIUM confidence)
- `.planning/research/STACK.md`, `.planning/research/ARCHITECTURE.md`, `.planning/research/PITFALLS.md` (this project's own prior research) — reused directly per this phase's canonical-references list; the one identified conflict between STACK.md and ARCHITECTURE.md (settings directory location) is flagged explicitly above rather than silently resolved

### Tertiary (LOW confidence)
- `MMDeviceEnumerator.GetDevice(id)`'s exact throw-vs-null behavior on a missing ID — not independently confirmed by execution in this session (no Windows runtime available); flagged in Open Questions and Assumptions Log

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — both libraries' exact API shapes were read directly from source this session, and version currency was confirmed against the live NuGet registry
- Architecture: HIGH — this phase's architecture is a direct, low-ambiguity implementation of `ARCHITECTURE.md`'s already-established patterns; the one added clarification (real-read vs. fake-mutation split) is a straightforward reading of D-05's stated boundary, not new invention
- Pitfalls: MEDIUM-HIGH — WinForms-specific pitfalls (ComboBox binding, ErrorProvider, close-box DialogResult) are well-documented, stable APIs; the one genuinely unverified item (`GetDevice` exception behavior) is explicitly flagged as LOW confidence and given a defensive-coding recommendation that works regardless of the actual behavior

**Research date:** 2026-07-24
**Valid until:** 60 days (stable, mature WinForms/.NET BCL APIs and long-lived NuGet packages; low volatility risk for this specific research)
