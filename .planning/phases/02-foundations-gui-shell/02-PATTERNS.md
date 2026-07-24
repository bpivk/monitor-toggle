# Phase 2: Foundations & GUI Shell - Pattern Map

**Mapped:** 2026-07-24
**Files analyzed:** 19
**Analogs found:** 2 / 19 (codebase); remaining 17 have no in-repo analog (greenfield project) — RESEARCH.md's Code Examples/Architecture Patterns sections serve as the reference implementation instead, per its own "Code Examples" already being sourced from verified upstream library shapes.

## Context

This is confirmed greenfield: no `src/` directory exists anywhere in the repo (`find` from repo root shows only `.planning/`, `spike/`, `CLAUDE.md`, `.claude/`). The **only** pre-existing, reusable production-adjacent code is `spike/MonitorDetachSpike/` (Phase 1 deliverable — a throwaway console spike, not part of `src/`, but its enumeration logic and `.csproj` shape are directly reusable per `02-CONTEXT.md`'s "Reusable Assets" note).

Because there is no established in-repo convention to mine for the other 17 files, this PATTERNS.md does two things instead of the usual "analog file + excerpt" mapping:
1. For the 2 files with a real analog (monitor enumeration logic, WinForms/net10.0-windows `.csproj` shape), gives concrete excerpts + line numbers from `spike/MonitorDetachSpike/`.
2. For all other files, points the planner directly at the specific `02-RESEARCH.md` section/line range that already contains a concrete, source-verified code example for that exact file — since RESEARCH.md's authors independently read the actual library source (`WindowsDisplayAPI`, `NAudio`) and official BCL/WinForms docs this session, its excerpts are the strongest available "pattern to copy from" in the absence of prior project code.

## File Classification

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|-----------------|---------------|
| `RigToggle.sln` | config | — | none (new) | no-analog |
| `src/RigToggle.App/RigToggle.App.csproj` | config | — | `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` | role-match (partial — WinForms template differs from console+UseWindowsForms) |
| `src/RigToggle.Core/RigToggle.Core.csproj` | config | — | `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` | partial (plain classlib, no WinForms/PackageReference) |
| `src/RigToggle.Windows/RigToggle.Windows.csproj` | config | — | `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` | exact (same TFM `net10.0-windows`, same `WindowsDisplayAPI` package ref pattern, adds NAudio) |
| `src/RigToggle.Tests/RigToggle.Tests.csproj` | config | — | none (new) | no-analog |
| `src/RigToggle.App/Program.cs` | provider (composition root) | request-response (app bootstrap) | none (new) | no-analog |
| `src/RigToggle.App/MainForm.cs` / `.Designer.cs` | component | request-response | none (new) | no-analog |
| `src/RigToggle.App/SettingsForm.cs` / `.Designer.cs` | component | request-response | none (new) | no-analog |
| `src/RigToggle.Core/ToggleService.cs` | service | event-driven (orchestration sequencing) | none (new) | no-analog |
| `src/RigToggle.Core/Models/AppSettings.cs` | model | CRUD (persisted POCO) | none (new) | no-analog |
| `src/RigToggle.Core/Models/StateSnapshot.cs` | model | CRUD (persisted POCO) | none (new) | no-analog |
| `src/RigToggle.Core/Abstractions/IMonitorController.cs` | model (interface) | request-response | `spike/MonitorDetachSpike/Program.cs` (method shapes to abstract, not an interface itself) | role-match (source of method signatures) |
| `src/RigToggle.Core/Abstractions/IAudioController.cs` | model (interface) | request-response | none (new) | no-analog |
| `src/RigToggle.Core/Abstractions/IAppController.cs` | model (interface) | request-response | none (new) | no-analog |
| `src/RigToggle.Core/Abstractions/ISettingsStore.cs` | model (interface) | CRUD | none (new) | no-analog |
| `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` | model (interface) | CRUD | none (new) | no-analog |
| `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` | service | file-I/O, CRUD | none (new) | no-analog |
| `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` | service | file-I/O, CRUD | none (new) | no-analog |
| `src/RigToggle.Windows/WindowsMonitorController.cs` | service (adapter) | request-response (real read) / no-op (fake mutation) | `spike/MonitorDetachSpike/Program.cs` `RunList()` (lines 53-70) | exact (same enumeration call, same library, same hardware already proven) |
| `src/RigToggle.Windows/WindowsAudioController.cs` | service (adapter) | request-response (real read) / no-op (fake mutation) | none in-repo — `02-RESEARCH.md` NAudio source citations | no-analog (repo); strong external analog |
| `src/RigToggle.Windows/WindowsAppController.cs` | service (adapter) | request-response (real read) / no-op (fake mutation) | none (new) | no-analog |
| `src/RigToggle.Tests/ToggleServiceTests.cs` | test | request-response | none (new) | no-analog |

## Pattern Assignments

### `src/RigToggle.Windows/WindowsMonitorController.cs` (service/adapter, request-response read + no-op mutation)

**Analog:** `spike/MonitorDetachSpike/Program.cs` (this repo, Phase 1 deliverable) — the ONLY real in-repo code that exercises the production dependency this file needs.

**Imports pattern** (lines 15-19):
```csharp
using System.Text.Json;
using System.Threading;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;
using System.Windows.Forms;
```
Note: `WindowsDisplayAPI.DisplayConfig` (not just `WindowsDisplayAPI`) is required for `PathTargetInfo`/`PathDisplayTarget` — easy to miss if copying only the top-level namespace.

**Core enumeration pattern — directly reusable for `GetActiveMonitors()`** (lines 53-70):
```csharp
static void RunList()
{
    PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);
    for (int i = 0; i < activePaths.Length; i++)
    {
        PathInfo path = activePaths[i];
        foreach (PathTargetInfo targetInfo in path.TargetsInfo)
        {
            PathDisplayTarget target = targetInfo.DisplayTarget;
            string friendlyName = target.FriendlyName ?? "(unavailable)";
            Console.WriteLine(
                $"[{i}] Target={friendlyName} " +
                $"DevicePath={target.DevicePath} " +
                $"IsGDIPrimary={path.IsGDIPrimary} " +
                $"OutputTechnology={targetInfo.OutputTechnology}");
        }
    }
}
```
This is proven (Phase 1 verification report) to run **non-elevated** and correctly enumerate on the actual rig's AMD/DisplayPort hardware. For `WindowsMonitorController.GetActiveMonitors()`, replace the `Console.WriteLine` loop body with `result.Add(new MonitorInfo(target.DevicePath, friendlyName, path.IsGDIPrimary))` — see `02-RESEARCH.md` lines 205-223 ("Pattern 1: Real-read / fake-mutation split") for the exact adapted shape already written against this project's `IMonitorController`/`MonitorInfo` types.

**What NOT to reuse from this analog:** `RunDisable()` (lines 72-125) and `VerifyOnce()` (lines 127-153) — the disable/restore/verify logic is Phase 4 scope (`ApplyPathInfos` topology mutation), and per `02-CONTEXT.md` it has a known primary-monitor-repositioning gap (see `spike/RESULTS-TEMPLATE.md` Finding 3). `WindowsMonitorController.Disable()`/`Restore()` must stay a documented no-op stub in this phase (`02-RESEARCH.md` lines 228-238).

**Snapshot format note (do NOT copy this part):** the spike's snapshot write (lines 90-98) stores a human-readable `ToString()` audit trail only, explicitly because "`PathInfo` does not deserialize cleanly from its `ToString()`." This project's real `JsonSnapshotStore` (Phase 2, `02-RESEARCH.md` Pattern 4, lines 319-340) instead serializes the project's own `StateSnapshot`/`MonitorState`/`AudioState` records — plain POCOs, not `PathInfo` objects — so this specific gotcha does not carry over, but it's worth knowing why the spike's approach differs.

---

### `src/RigToggle.Windows/RigToggle.Windows.csproj` (config)

**Analog:** `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` (full file, 22 lines) — exact structural match: this is the only other `.csproj` in the repo that already targets `net10.0-windows` and references `WindowsDisplayAPI`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Intentionally no elevation manifest element of any kind: this keeps the tool asInvoker -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WindowsDisplayAPI" Version="1.3.0.13" />
  </ItemGroup>

</Project>
```

**Differences to apply for `RigToggle.Windows.csproj`:**
- Remove `<OutputType>Exe</OutputType>` — this is a class library, not an executable (per `02-RESEARCH.md`'s `dotnet new classlib -n RigToggle.Windows -f net10.0-windows`).
- Add `<PackageReference Include="NAudio" Version="2.3.0" />` alongside `WindowsDisplayAPI`.
- Add `<ProjectReference Include="..\RigToggle.Core\RigToggle.Core.csproj" />` (per `02-RESEARCH.md`'s Installation section, line 98: `dotnet add RigToggle.Windows reference RigToggle.Core`).
- Keep `<UseWindowsForms>true</UseWindowsForms>` — required here too, per `02-RESEARCH.md` line 101, because this project references `System.Windows.Forms.Screen` the same way the spike does (as a second independent oracle) plus the CCD/COM-dependent libraries.
- **Critically preserve the no-manifest convention** (the trailing comment in the analog) — carries forward directly into this phase per `02-RESEARCH.md` Pitfall 6 (lines 429-434): do not add `requestedExecutionLevel`/`requireAdministrator` anywhere in `src/`, including this file and `RigToggle.App.csproj`.

**For `RigToggle.Core.csproj`:** same base shape minus `UseWindowsForms`, minus any `PackageReference` to `WindowsDisplayAPI`/`NAudio` — `02-RESEARCH.md` line 101 explicitly calls out that `RigToggle.Core.csproj` must **not** reference either package (structural enforcement of D-08's "zero Windows API references" rule); a `WindowsDisplayAPI`/`NAudio` reference appearing there during review is a regression signal.

---

### `src/RigToggle.Core/Abstractions/IMonitorController.cs` (interface)

**Analog:** method signatures implied by `spike/MonitorDetachSpike/Program.cs`'s `RunList()`/`VerifyOnce()` shape, already abstracted into interface form in `02-RESEARCH.md` Pattern 1 (lines 205-238) as the concrete `WindowsMonitorController : IMonitorController` implementation. The interface itself (`GetActiveMonitors()`, `CaptureState(string)`, `Disable(string)`, `Restore(MonitorState)`) should be extracted as the signatures used by that class — no separate interface-only analog exists in-repo, so define the interface directly from the adapter shape RESEARCH.md already committed to.

---

## No Analog Found

The following files have no existing in-repo code to copy from (confirmed greenfield — no `src/` tree exists). For each, `02-RESEARCH.md` already contains a concrete, ready-to-copy code example (cited with section/line numbers) written specifically for this project's interfaces and decisions — the planner should treat these as the pattern source in place of a codebase analog.

| File | Role | Data Flow | Reference in 02-RESEARCH.md |
|------|------|-----------|------------------------------|
| `RigToggle.sln`, `RigToggle.Tests.csproj` | config | — | "Installation" section, lines 87-99 (`dotnet new sln`, `dotnet new classlib`/`winforms`, `dotnet sln add`, `dotnet add ... reference`) |
| `src/RigToggle.App/Program.cs` | provider (composition root) | request-response | "Recommended Project Structure" lines 170-195 (comment: "`[STAThread]` Main, composition root (new up real adapters)"); wire real `WindowsMonitorController`/`WindowsAudioController`/`WindowsAppController` + `JsonSettingsStore`/`JsonSnapshotStore` into `ToggleService`, then `MainForm` |
| `src/RigToggle.App/MainForm.cs` | component | request-response | System Architecture Diagram lines 122-131 (mode indicator D-14, Toggle button, Settings button `ShowDialog()`, companion status line D-15); Pattern 4 lines 315-340 for mode-derivation via `ISnapshotStore.Exists()` |
| `src/RigToggle.App/SettingsForm.cs` | component | request-response | Pattern 2 lines 241-281 (ComboBox stale-detection, full excerpt) + Pattern 5 lines 344-358 (Save-button gating, `CancelButton`/`DialogResult` wiring) + Pitfalls 1-3 (lines 394-420, ComboBox binding quirk, first-run vs stale distinction) |
| `src/RigToggle.Core/ToggleService.cs` | service | event-driven (sequencing) | System Architecture Diagram lines 133-143 (exact call sequence: `ISettingsStore.Load()` → `CaptureState()` x2 → `ISnapshotStore.Save()` → `Disable()`/`SetDefault()`/`LaunchOrFocus()`) — zero Windows API references per D-08 |
| `src/RigToggle.Core/Models/AppSettings.cs` | model | CRUD | Code Examples, lines 454-465 (full POCO shape) |
| `src/RigToggle.Core/Models/StateSnapshot.cs` | model | CRUD | Pattern 4, line 320 (`public sealed record StateSnapshot(MonitorState Monitor, AudioState Audio);`) |
| `src/RigToggle.Core/Abstractions/IAudioController.cs`, `IAppController.cs`, `ISettingsStore.cs`, `ISnapshotStore.cs` | model (interface) | request-response / CRUD | System Architecture Diagram method lists per adapter (lines 146-159); `ISnapshotStore` methods concretely shown in Pattern 4's `JsonSnapshotStore` implementation (`Exists()`, `Save()`, `Load()`, `Clear()`, lines 322-339) |
| `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` | service (file-I/O) | CRUD | Pattern 3, lines 289-313 (full class, `Load()`/`Save()` with atomic `File.Move` write) |
| `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` | service (file-I/O) | CRUD | Pattern 4, lines 319-340 (full class) |
| `src/RigToggle.Windows/WindowsAudioController.cs` | service (adapter) | request-response (real read) / no-op (fake mutation) | Standard Stack table lines 79 (NAudio API shapes: `EnumerateAudioEndPoints`, `MMDevice.ID`/`FriendlyName`) + Pitfall 2 lines 401-406 (defensive `GetDevice` null/throw handling) — no full class body given, model it directly on Pattern 1's `WindowsMonitorController` shape (lines 205-238), substituting NAudio calls for the read methods |
| `src/RigToggle.Windows/WindowsAppController.cs` | service (adapter) | request-response (real read) / no-op (fake mutation) | Pattern 6, lines 360-368 (full `IsRunning` method) + Pitfall 5 lines 422-427 (`Path.GetFileNameWithoutExtension` requirement) |
| `src/RigToggle.Tests/ToggleServiceTests.cs` | test | request-response | Recommended Project Structure line 194 ("against hand-written test doubles for the three interfaces") — no example test body given; write hand-rolled fakes implementing `IMonitorController`/`IAudioController`/`IAppController`/`ISettingsStore`/`ISnapshotStore` directly (not a mocking framework, consistent with the project's overall "no unnecessary dependency" posture from CLAUDE.md/STACK.md) |

## Shared Patterns

### ComboBox stale-selection + validation wiring (applies to all 3 Settings pickers: Monitor, Audio-Normal, Audio-Rig)
**Source:** `02-RESEARCH.md` Pattern 2, lines 241-281 (full excerpt) + Pitfall 1, lines 394-399 + Pitfall 3, lines 408-413.
**Apply to:** `SettingsForm.cs` — all three picker population methods (`PopulateMonitorPicker`, and the audio/app-path equivalents) follow the identical shape: unhook `SelectedIndexChanged` before `DataSource` assignment, distinguish `savedId == null` (first-run, no warning) from `savedId != null && not found` (D-10 stale warning), rehook, call `ValidateSettingsForm()` exactly once at the end.

### Settings/Snapshot atomic JSON persistence
**Source:** `02-RESEARCH.md` Pattern 3 (lines 289-313) and Pattern 4 (lines 319-340).
**Apply to:** `JsonSettingsStore.cs`, `JsonSnapshotStore.cs` — both use `File.Move(tempPath, path, overwrite: true)` for atomic writes and `Directory.CreateDirectory` for create-if-missing; both target `%LocalAppData%\RigToggle\` (resolves the STACK.md-vs-ARCHITECTURE.md directory discrepancy — see RESEARCH.md Open Questions #1, lines 486-489).

### Real-read / fake-mutation adapter split
**Source:** `02-RESEARCH.md` Pattern 1, lines 197-238.
**Apply to:** all three `RigToggle.Windows` controller classes (`WindowsMonitorController`, `WindowsAudioController`, `WindowsAppController`) — one class per interface now, not separate Fake/Real classes; every read method (`GetActiveMonitors`, `CaptureState`, `GetPlaybackDevices`, `IsRunning`) is real starting this phase; every mutating method (`Disable`, `Restore`, `SetDefault`, `LaunchOrFocus`, `MinimizeIfRunning`) is a clearly-commented no-op stub that Phases 3/4/5 fill in without renaming or restructuring.

### No elevation manifest
**Source:** `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` line 15 (comment) + `02-RESEARCH.md` Pitfall 6, lines 429-434.
**Apply to:** `RigToggle.App.csproj`, `RigToggle.Windows.csproj` — do not add `requestedExecutionLevel`/`requireAdministrator` or any `.manifest` file. WinForms template default (`asInvoker`, implicit) is correct and must remain untouched into Phases 3-5 (elevation would break `SetForegroundWindow` against the non-elevated Moza Companion process via UIPI, per PITFALLS.md Pitfall 2).

### Process-name-without-extension lookup
**Source:** `02-RESEARCH.md` Pattern 6, lines 360-368 + Pitfall 5, lines 422-427.
**Apply to:** `WindowsAppController.IsRunning()` — always `Path.GetFileNameWithoutExtension(companionAppPath)` before `Process.GetProcessesByName`, never the raw path or a hardcoded literal.

## Metadata

**Analog search scope:** entire repo (`find` from `/home/bpivk/moza`, excluding `.git`); confirmed no `src/` directory exists.
**Files scanned:** `spike/MonitorDetachSpike/Program.cs` (162 lines, read in full, single pass — no re-reads), `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` (22 lines, read in full).
**Pattern extraction date:** 2026-07-24
