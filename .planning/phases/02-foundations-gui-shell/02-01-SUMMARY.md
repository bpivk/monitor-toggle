---
phase: 02-foundations-gui-shell
plan: 01
subsystem: infra
tags: [dotnet10, winforms, csharp, windowsdisplayapi, naudio, solution-scaffold]

# Dependency graph
requires:
  - phase: 01-monitor-disable-spike
    provides: "Confirmed WindowsDisplayAPI PathInfo.GetActivePaths() enumeration runs non-elevated on this rig's hardware (spike/MonitorDetachSpike)"
provides:
  - "RigToggle.sln four-project solution (Core, Windows, App, Tests) with correct TFMs and references"
  - "RigToggle.Windows.csproj with WindowsDisplayAPI 1.3.0.13 + NAudio 2.3.0 package refs, isolated from Core"
  - "5 Core interfaces: IMonitorController, IAudioController, IAppController, ISettingsStore, ISnapshotStore"
  - "6 Core models: MonitorInfo, MonitorState, AudioDeviceInfo, AudioState, StateSnapshot, AppSettings"
affects: [02-02, 02-03, 02-04, 02-05]

# Tech tracking
tech-stack:
  added: ["WindowsDisplayAPI 1.3.0.13", "NAudio 2.3.0", ".NET 10 (net10.0 / net10.0-windows)", "WinForms", "xunit 2.9.2"]
  patterns: ["Layered architecture: App (GUI) -> Windows (adapters) -> Core (contracts, zero Windows API refs)", "Interface-first contract definition ahead of implementation"]

key-files:
  created:
    - RigToggle.sln
    - src/RigToggle.Core/RigToggle.Core.csproj
    - src/RigToggle.Windows/RigToggle.Windows.csproj
    - src/RigToggle.App/RigToggle.App.csproj
    - src/RigToggle.Tests/RigToggle.Tests.csproj
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.Core/Models/MonitorInfo.cs
    - src/RigToggle.Core/Models/MonitorState.cs
    - src/RigToggle.Core/Models/AudioDeviceInfo.cs
    - src/RigToggle.Core/Models/AudioState.cs
    - src/RigToggle.Core/Models/StateSnapshot.cs
    - src/RigToggle.Core/Abstractions/IMonitorController.cs
    - src/RigToggle.Core/Abstractions/IAudioController.cs
    - src/RigToggle.Core/Abstractions/IAppController.cs
    - src/RigToggle.Core/Abstractions/ISettingsStore.cs
    - src/RigToggle.Core/Abstractions/ISnapshotStore.cs
  modified: []

key-decisions:
  - "Human approved WindowsDisplayAPI 1.3.0.13 + NAudio 2.3.0 package legitimacy gate (Task 1) with explicit install-command confirmation before Task 2 began"
  - "RigToggle.App's WinForms template default Program.cs/Form1.*/Form1.resx left in place as scaffold-compile placeholders per plan instruction — Plan 05 replaces them with MainForm"

patterns-established:
  - "Real-read / fake-mutation adapter split (per 02-RESEARCH.md Pattern 1): interfaces expose both read and mutating methods on one contract; RigToggle.Windows adapters (built in later plans) implement reads for real starting Phase 2 and stub mutations as documented no-ops until Phases 3/4"
  - "RigToggle.Core is structurally Windows-API-free: no PackageReference to WindowsDisplayAPI/NAudio, no UseWindowsForms — enforced by grep and verified in this plan"

requirements-completed: [SETTINGS-01, SETTINGS-02, SETTINGS-03, SETTINGS-04]

# Metrics
duration: 10min
completed: 2026-07-24
---

# Phase 2 Plan 1: Solution Scaffold & Core Contracts Summary

**Four-project .NET 10 solution (Core/Windows/App/Tests) scaffolded with WindowsDisplayAPI 1.3.0.13 + NAudio 2.3.0 isolated to RigToggle.Windows, plus all 5 interfaces and 6 models Phase 2's downstream plans implement against.**

## Performance

- **Duration:** ~10 min (continuation run; Task 1 checkpoint was approved in a prior session)
- **Completed:** 2026-07-24T14:03:32Z
- **Tasks:** 2 of 3 (Task 1 checkpoint was approved by the user before this continuation run started)
- **Files modified:** 20 (9 in Task 2, 11 in Task 3)

## Accomplishments
- Scaffolded `RigToggle.sln` with four projects (`RigToggle.Core`, `RigToggle.Windows`, `RigToggle.App`, `RigToggle.Tests`), correct TFMs (`net10.0` for Core/Tests, `net10.0-windows` for Windows/App), and correct `ProjectReference` wiring (App → Core + Windows; Windows → Core; Tests → Core only).
- Isolated `WindowsDisplayAPI` (1.3.0.13) and `NAudio` (2.3.0) package references to `RigToggle.Windows` only — structurally verified `RigToggle.Core.csproj` contains zero references to either package and no `UseWindowsForms`.
- Confirmed no elevation manifest (`requestedExecutionLevel`/`ApplicationManifest`) exists anywhere under `src/`, preserving the `asInvoker` default per 02-RESEARCH.md Pitfall 6.
- Defined all 5 Core interfaces (`IMonitorController`, `IAudioController`, `IAppController`, `ISettingsStore`, `ISnapshotStore`) exactly per the plan's `<interfaces>` contract block, and all 6 models (`MonitorInfo`, `MonitorState`, `AudioDeviceInfo`, `AudioState`, `StateSnapshot`, `AppSettings`) with the field shapes cited from 02-RESEARCH.md.

## Task Commits

Each task was committed atomically:

1. **Task 1: Package legitimacy gate** — checkpoint approved by the user in a prior session (no commit; gate-only task, no files produced)
2. **Task 2: Scaffold solution, four projects, references, and packages** — `db45a0e` (feat)
3. **Task 3: Define Core contracts — models and interfaces** — `703aaf0` (feat)

_Note: this SUMMARY's own metadata commit follows separately per the worktree execution protocol._

## Files Created/Modified
- `RigToggle.sln` - Solution file listing all four projects with build configurations
- `src/RigToggle.Core/RigToggle.Core.csproj` - Plain net10.0 classlib, zero Windows API refs (D-08)
- `src/RigToggle.Windows/RigToggle.Windows.csproj` - net10.0-windows, WindowsDisplayAPI + NAudio package refs, references Core
- `src/RigToggle.App/RigToggle.App.csproj` - net10.0-windows WinForms exe, references Core + Windows
- `src/RigToggle.Tests/RigToggle.Tests.csproj` - net10.0 xunit test project, references Core only
- `src/RigToggle.App/Program.cs`, `Form1.cs`, `Form1.Designer.cs`, `Form1.resx` - WinForms template defaults, left in place so App compiles during Wave 2; Plan 05 replaces with MainForm
- `src/RigToggle.Core/Models/AppSettings.cs` - Mutable POCO, 7 nullable string properties (MonitorDevicePath, MonitorFriendlyName, NormalAudioDeviceId, NormalAudioDeviceName, RigAudioDeviceId, RigAudioDeviceName, CompanionAppPath)
- `src/RigToggle.Core/Models/MonitorInfo.cs` - `sealed record MonitorInfo(string DevicePath, string FriendlyName, bool IsPrimary)`
- `src/RigToggle.Core/Models/MonitorState.cs` - `sealed record MonitorState(string MonitorDevicePath)`, Phase-2-minimal capture
- `src/RigToggle.Core/Models/AudioDeviceInfo.cs` - `sealed record AudioDeviceInfo(string Id, string FriendlyName)`
- `src/RigToggle.Core/Models/AudioState.cs` - `sealed record AudioState(string? DefaultDeviceId)`
- `src/RigToggle.Core/Models/StateSnapshot.cs` - `sealed record StateSnapshot(MonitorState Monitor, AudioState Audio)`
- `src/RigToggle.Core/Abstractions/IMonitorController.cs` - `GetActiveMonitors`, `CaptureState`, `Disable`, `Restore`
- `src/RigToggle.Core/Abstractions/IAudioController.cs` - `GetPlaybackDevices`, `CaptureState`, `SetDefault`, `Restore`
- `src/RigToggle.Core/Abstractions/IAppController.cs` - `IsRunning`, `LaunchOrFocus`, `MinimizeIfRunning`
- `src/RigToggle.Core/Abstractions/ISettingsStore.cs` - `Load`, `Save`
- `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` - `Exists`, `Save`, `Load`, `Clear`

## Decisions Made
- User approved the Task 1 package legitimacy gate for WindowsDisplayAPI 1.3.0.13 and NAudio 2.3.0 by confirming the exact `dotnet add package` commands in a prior session; this continuation proceeded directly to Task 2 with that approval in hand.
- Left the WinForms template's default `Program.cs`/`Form1.cs`/`Form1.Designer.cs`/`Form1.resx` in `RigToggle.App` unchanged (per plan instruction) so the App project compiles during Wave 2 before Plan 05 replaces them with the real `MainForm`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Reworded elevation-manifest exclusion comments to avoid tripping the acceptance-criteria grep**
- **Found during:** Task 2 (Scaffold solution, four projects, references, and packages)
- **Issue:** The plan's own acceptance criterion `grep -rl "requestedExecutionLevel\|ApplicationManifest" src/` (expected: no results) is a literal substring match. My initial csproj comments explaining "do not add requestedExecutionLevel/requireAdministrator" contained the literal string `requestedExecutionLevel`, which the grep would flag as a false-positive manifest reference even though no actual manifest element existed.
- **Fix:** Reworded the explanatory comments in `RigToggle.Windows.csproj` and `RigToggle.App.csproj` to convey the same intent ("do not add an elevated execution level or admin requirement") without using the literal flagged strings.
- **Files modified:** `src/RigToggle.Windows/RigToggle.Windows.csproj`, `src/RigToggle.App/RigToggle.App.csproj`
- **Verification:** Re-ran `grep -rl "requestedExecutionLevel\|ApplicationManifest" src/` — returns no results, satisfying the acceptance criterion.
- **Committed in:** `db45a0e` (part of Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — acceptance-criteria grep false positive from explanatory comment text)
**Impact on plan:** Cosmetic-only fix to comment wording; no change to actual project configuration or behavior. No scope creep.

## Issues Encountered
None beyond the deviation noted above.

## User Setup Required

**This Linux sandbox has no .NET SDK.** All source/project files were written directly (not via `dotnet new`/`dotnet add`) to match the exact output shape those commands would have produced, per RESEARCH.md/PATTERNS.md guidance and the execution boundary carried from Phase 1.

The following verification steps require the user's Windows rig and were **not** run in this sandbox:
- `dotnet build RigToggle.sln` — must restore `WindowsDisplayAPI`/`NAudio` packages and build all four (currently-empty-of-logic) projects with 0 errors.
- `dotnet build src/RigToggle.Core/RigToggle.Core.csproj` — contracts-only build; confirms the 5 interfaces + 6 models compile cleanly with no Windows API references.

Structural/textual verification performed in this sandbox instead (all passed):
- `RigToggle.sln` lists exactly 4 `Project(...)` entries.
- `RigToggle.Core.csproj` contains 0 matches for `WindowsDisplayAPI|NAudio` and 0 matches for `UseWindowsForms`.
- `RigToggle.Windows.csproj` targets `net10.0-windows`, contains both required `PackageReference`s, and a `ProjectReference` to Core.
- `RigToggle.App.csproj` targets `net10.0-windows` with `ProjectReference`s to both Core and Windows.
- `grep -rl "requestedExecutionLevel\|ApplicationManifest" src/` returns no results.
- `grep -c "interface I" src/RigToggle.Core/Abstractions/*.cs` totals 5.
- `IMonitorController.cs` declares all four required methods; `ISnapshotStore.cs` declares all four required methods.
- `AppSettings.cs` contains all seven required `string?` properties.
- `StateSnapshot.cs` declares the required record shape.

No USER-SETUP.md generated — no external service configuration required, only the above build/verify commands the user runs on the rig.

## Next Phase Readiness
- All 5 interfaces and 6 models are defined and ready for Plans 02-02 through 02-05 to implement against (persistence layer, Windows adapters, ToggleService, GUI forms).
- The solution skeleton is ready to accept implementation code in subsequent Wave-2 plans; the App project's placeholder `Form1` keeps the solution buildable in the interim.
- Actual `dotnet build` verification remains deferred to the user's Windows rig — flag this in the next plan's checkpoint/verification step if not already covered.

---
*Phase: 02-foundations-gui-shell*
*Completed: 2026-07-24*
