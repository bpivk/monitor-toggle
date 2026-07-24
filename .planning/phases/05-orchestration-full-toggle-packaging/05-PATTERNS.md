# Phase 5: Orchestration, Full Toggle & Packaging - Pattern Map

**Mapped:** 2026-07-24
**Files analyzed:** 8
**Analogs found:** 8 / 8

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Core/Models/ToggleStepOutcome.cs` (new) | model (enum) | transform | `src/RigToggle.Core/Models/AudioRoleState.cs` | role-match (small POCO house style; no existing enum analog, so record style is the closest fit) |
| `src/RigToggle.Core/Models/ToggleStepResult.cs` (new) | model | transform | `src/RigToggle.Core/Models/AudioRoleState.cs` | exact (same shape: tiny `sealed record`, one-line XML-doc summary, nullable field) |
| `src/RigToggle.Core/Models/ToggleResult.cs` (new) | model | transform | `src/RigToggle.Core/Models/StateSnapshot.cs` | exact (wraps sub-records, same doc-comment convention, references decision IDs) |
| `src/RigToggle.Core/ToggleService.cs` (modified — return-type change) | service | request-response / CRUD (orchestration) | itself (existing methods `ToggleToRigMode`/`ToggleToNormalMode`) | exact (in-place modification, not new file — extend existing patterns) |
| `src/RigToggle.App/MainForm.cs` (modified — `BtnToggle_Click`) | controller (WinForms event handler) | request-response | itself (existing `BtnToggle_Click` catch block, existing `MessageBox.Show` calls) | exact (in-place modification) |
| `src/RigToggle.Tests/ToggleServiceTests.cs` (modified) | test | request-response | itself + `src/RigToggle.Tests/Doubles/FakeControllers.cs` | exact (existing test file + existing fakes, both reused as-is) |
| `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` (new) | config | batch (build/publish) | `src/RigToggle.App/RigToggle.App.csproj` | no analog (no `.pubxml` exists anywhere in repo) — use RESEARCH.md's cited MSBuild shape |
| `README.md` (new, repo root) | config/docs | N/A | none in repo | no analog — none exists; use RESEARCH.md's documented publish command |

## Pattern Assignments

### `src/RigToggle.Core/Models/ToggleStepOutcome.cs` (new model — enum)

**Analog:** `src/RigToggle.Core/Models/AudioRoleState.cs` (whole file, 8 lines) — establishes the house style for tiny `Models/` types: `namespace RigToggle.Core.Models;` file-scoped namespace, one-paragraph XML-doc summary explaining *what it's for* (not just field-by-field), no interfaces/inheritance.

**File header pattern** (`AudioRoleState.cs` lines 1-7):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device for a single Windows audio role (eConsole,
/// eMultimedia, or eCommunications), used to restore that role's default later.
/// </summary>
public sealed record AudioRoleState(string? DeviceId, string? DeviceName);
```

**Apply as:**
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Outcome of a single toggle step (Monitor / Audio / App). NotAttempted covers steps
/// skipped because an earlier step in a stop-on-first-failure sequence (ToggleToRigMode,
/// D-04) already failed.
/// </summary>
public enum ToggleStepOutcome
{
    Succeeded,
    Failed,
    NotAttempted,
}
```
No existing enum in the codebase to mirror exactly (`Models/` is all `sealed record`s) — RESEARCH.md's Code Examples section (lines 217-236) already verified this exact shape against the codebase this session; treat it as the analog for the *enum* specifically.

---

### `src/RigToggle.Core/Models/ToggleStepResult.cs` (new model — record)

**Analog:** `src/RigToggle.Core/Models/AudioRoleState.cs` (same file as above) — positional `sealed record` with a nullable field used only in one outcome case, exactly like `ToggleStepResult.Reason` (populated only when `Outcome == Failed`).

**Nullable-field precedent** — `AudioRoleState(string? DeviceId, string? DeviceName)`: both fields nullable because the role may have no captured default. Same pattern applies to `Reason` (nullable, populated only on `Failed`).

**Apply as:**
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// One toggle step's outcome — step name, result, and (if Failed) the reason. Reason is
/// null for Succeeded/NotAttempted; populated with the underlying exception's message for
/// Failed (same "surface the real error" posture as MainForm's existing exception-detail
/// MessageBox text, D-13/T-02-FAKEFAIL).
/// </summary>
public sealed record ToggleStepResult(string StepName, ToggleStepOutcome Outcome, string? Reason);
```

---

### `src/RigToggle.Core/Models/ToggleResult.cs` (new model — wrapping record)

**Analog:** `src/RigToggle.Core/Models/StateSnapshot.cs` (whole file, 9 lines) — a record that wraps two other model records and documents *why* it exists / what invariant it carries, referencing the decision ID by name (`D-14`) the same way `ToggleResult` should reference `D-03`.

**Wrapping-record pattern** (`StateSnapshot.cs` lines 1-9):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Combined monitor + audio state captured immediately before a toggle mutation,
/// persisted via ISnapshotStore so toggle-back can restore the exact prior configuration.
/// Snapshot-file presence itself is what determines current mode (D-14): Mode == RigMode
/// iff ISnapshotStore.Exists() is true.
/// </summary>
public sealed record StateSnapshot(MonitorState Monitor, AudioState Audio);
```

**Apply as** (computed property style follows no existing precedent in `Models/` — all current records are pure data — but is a minimal, idiomatic C# record addition; RESEARCH.md Code Examples lines 228-236 already verified this against the codebase):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Full outcome of a ToggleToRigMode/ToggleToNormalMode call — ordered per-step results,
/// consumed identically by MainForm regardless of toggle direction (D-03). Scoped strictly
/// to the 3 mutation steps (Monitor/Audio/App); preflight guards (unconfigured settings,
/// missing companion app path) remain exception-based and are NOT represented here.
/// </summary>
public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    public bool Success => Steps.All(s => s.Outcome == ToggleStepOutcome.Succeeded);
}
```

---

### `src/RigToggle.Core/ToggleService.cs` (modified — `ToggleToRigMode`/`ToggleToNormalMode` return types)

**Analog:** itself — this is an in-place modification, not a new file. The existing class-level and method-level XML-doc rationale-comment style (extensive "why", references to decision IDs by name) is the pattern to continue, not replace.

**Class-level doc-comment style to preserve/extend** (lines 5-12):
```csharp
/// <summary>
/// Orchestrates the snapshot-before-mutate toggle sequence (D-08/ARCHITECTURE.md Pattern 2)
/// entirely through the ISettingsStore/ISnapshotStore/IMonitorController/IAudioController/
/// IAppController interfaces — zero Windows API references live here. Current mode is
/// derived from snapshot-file presence (D-14), not a separate flag. Partial-failure
/// handling (CORE-04) is explicitly out of scope for this phase; the sequence below is
/// linear and unconditional.
/// </summary>
```
The final sentence ("Partial-failure handling (CORE-04) is explicitly out of scope for this phase") is now **stale** and must be updated/replaced to describe the new stop-on-first-failure (rig mode) vs. isolate-and-continue (normal mode) asymmetry — this is exactly the D-05 requirement to document the asymmetry inline so it isn't later "fixed" into false symmetry.

**Preflight-guard exception pattern to KEEP UNCHANGED** (lines 46-64) — per RESEARCH.md Open Question 1 / Pitfall 4, these two guards stay exception-based, NOT folded into `ToggleResult`:
```csharp
if (!IsFullyConfigured(settings))
{
    throw new InvalidOperationException(
        "Rig Toggle settings are not fully configured. Open Settings and choose a monitor, both audio devices, and the companion app path before switching to Rig Mode.");
}

if (!File.Exists(settings.CompanionAppPath))
{
    throw new InvalidOperationException(
        $"The companion app could not be found at '{settings.CompanionAppPath}'. Open Settings and reselect the companion app path before switching to Rig Mode.");
}
```

**Linear mutation sequence to wrap with stop-on-first-failure result tracking** (lines 66-75, `ToggleToRigMode`):
```csharp
var monitorState = _monitorController.CaptureState();
var audioState = _audioController.CaptureState();

_snapshotStore.Save(new Models.StateSnapshot(monitorState, audioState));

_monitorController.Disable(settings.MonitorDevicePath!);
_audioController.SetDefault(settings.RigAudioDeviceId!);
_appController.LaunchOrFocus(settings.CompanionAppPath!);
```
Note: snapshot capture/save (CORE-03) happens BEFORE the three mutation steps and is NOT itself one of the three `ToggleStepResult` entries (Monitor/Audio/App) per the checklist wording in D-01 ("Monitor: ... / Audio: ... / App: ...") — only the three mutation calls become steps.

**Existing isolate-and-continue pattern to preserve exactly, only changing reporting mechanism** (lines 122-180, `ToggleToNormalMode`) — this is the most load-bearing excerpt in the phase; the control flow (monitor try/catch not swallowed and re-thrown after audio attempt, audio try/catch swallowed with `Trace.WriteLine`, conditional skip of `MinimizeIfRunning`/`Clear` on monitor failure) must be preserved verbatim, only the "record outcome" and "throw" mechanics change to append to a `List<ToggleStepResult>` and return `ToggleResult` instead of throwing:
```csharp
Exception? monitorFailure = null;
try
{
    _monitorController.Restore(snapshot.Monitor);
}
catch (Exception ex)
{
    monitorFailure = ex;
}

try
{
    _audioController.Restore(snapshot.Audio);
}
catch (Exception ex)
{
    // Intentionally swallowed (gap-closure 03-04): see class-level remarks.
    System.Diagnostics.Trace.WriteLine($"Audio restore failed, continuing: {ex}");
}

if (monitorFailure is not null)
{
    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(monitorFailure).Throw();
}
```
The `ExceptionDispatchInfo.Capture(...).Throw()` re-throw at the end must become "record Monitor as Failed with `monitorFailure.Message`, App step becomes NotAttempted, return `ToggleResult`" — no throw, since D-02 replaces the void/throw contract for both directions.

**Corrupted-snapshot exception (lines 129-137) — same "keep exception-based" reasoning as preflight guards** (this is not one of the 3 mutation steps, it fires before any step runs):
```csharp
if (snapshot is null)
{
    if (wasInRigMode)
    {
        throw new InvalidOperationException(
            "The saved rig-mode state file exists but could not be read (corrupted). " +
            "Your monitor and audio device were NOT restored automatically. Fix or " +
            "delete the corrupted state file before retrying.");
    }
}
```

---

### `src/RigToggle.App/MainForm.cs` (modified — `BtnToggle_Click`)

**Analog:** itself — existing catch block and `MessageBox.Show` call conventions.

**Existing MessageBox tone/format to match for the new checklist** (lines 117-131):
```csharp
catch (Exception ex)
{
    // Basic guard only (D-13/T-02-FAKEFAIL) — full per-step CORE-04 partial-failure
    // reporting is out of scope until Phase 5. Exception detail is included
    // (not just a generic message) because this is a single-user diagnostic
    // tool, not a hardened multi-user app — surfacing the real error is more
    // useful than hiding it, especially for CCD-mutation failures that are
    // otherwise unreproducible without rig hardware.
    MessageBox.Show(
        this,
        $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
}
```
Conventions to carry into the new checklist MessageBox:
- `this` as owner, `"Rig Toggle"` as title (used consistently across every `MessageBox.Show` call in this file — see also lines 79-85 for the "Please finish configuring..." informational dialog).
- Icon choice signals severity: `MessageBoxIcon.Warning` for failure, `MessageBoxIcon.Information` for benign/informational (line 79-85 uses Information for the pre-check redirect). Use `Warning` when `ToggleResult.Success == false`; consider `MessageBoxIcon.Information` (or no dialog at all, just `RefreshUi()`) when `Success == true`, matching the current behavior where a fully-successful toggle shows no dialog at all today.
- Exception detail is surfaced verbatim, not hidden — same posture applies to `ToggleStepResult.Reason` text in the new checklist.

**The two-branch call sites that will now receive a `ToggleResult` instead of nothing** (lines 66-113):
```csharp
try
{
    if (_toggleService.IsInRigMode())
    {
        _toggleService.ToggleToNormalMode();
    }
    else
    {
        // ...preflight/confirm-dialog guards unchanged (still throw/return early)...
        _toggleService.ToggleToRigMode();
    }

    RefreshUi();
}
```
Both calls must now capture the return value (`var result = _toggleService.ToggleToNormalMode();` / `var result = _toggleService.ToggleToRigMode();`) and branch on `result.Success` to either call `RefreshUi()` silently (success) or build+show the checklist (failure), while `RefreshUi()` should still run in both cases (state may have partially changed even on failure — e.g. monitor disabled successfully but audio failed).

**Informational MessageBox pattern for checklist formatting reference** (lines 79-85):
```csharp
MessageBox.Show(
    this,
    "Please finish configuring Settings (monitor, both audio devices, and the companion app) before switching to Rig Mode.",
    "Rig Toggle",
    MessageBoxButtons.OK,
    MessageBoxIcon.Information);
```

---

### `src/RigToggle.Tests/ToggleServiceTests.cs` (modified — assertions on new contract)

**Analog:** itself + `src/RigToggle.Tests/Doubles/FakeControllers.cs` (both reused unmodified as fixtures).

**Existing fixture/helper pattern to keep** (lines 32-45):
```csharp
private static (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
    AppSettings? settings = null,
    bool audioThrowsOnRestore = false)
{
    var callLog = new List<string>();
    var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
    var snapshotStore = new InMemorySnapshotStore(callLog);
    var monitorController = new FakeMonitorController(callLog);
    var audioController = new FakeAudioController(callLog, throwOnRestore: audioThrowsOnRestore);
    var appController = new FakeAppController(callLog);

    var service = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
    return (service, callLog, snapshotStore);
}
```
No changes needed to fixtures/fakes themselves — `FakeAudioController(throwOnRestore: true)` (see `FakeControllers.cs` lines 43-89) already provides a controlled-throw double usable to assert the new `ToggleStepResult` reason text (`"Fake audio restore failure (simulated stale/missing device)."`) flows through into `ToggleResult`.

**Assertion pattern that must change from throw-based to result-based** — e.g. `ToggleToRigMode_Throws_WhenCompanionAppPathDoesNotExist` (lines 129-148) exercises a **preflight guard**, which per this phase's design stays exception-based (Common Pitfalls #4 / Open Question 1) — this specific test's `Assert.Throws<InvalidOperationException>` should NOT change. Contrast with a genuinely new test needed: `ToggleToRigMode_ReturnsFailedStep_WhenMonitorDisableThrows` (no existing fake currently supports monitor-disable throwing — `FakeMonitorController.Disable` at `FakeControllers.cs` lines 32-35 is unconditional; a `throwOnDisable` parameter analogous to `FakeAudioController`'s `throwOnRestore` will need to be added there, following the exact same constructor-flag + conditional-throw pattern already used at `FakeControllers.cs` lines 77-88).

**Existing call-log-based assertion style to keep for ordering checks** (lines 47-61) — this pattern (assert index-of-string-prefix ordering in a shared `callLog`) remains valid and orthogonal to the `ToggleResult` return-value assertions; both can coexist in the same test.

---

### `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` (new — no analog)

No `.pubxml` or `Properties/PublishProfiles/` directory exists anywhere in the repo (confirmed via directory listing of `src/RigToggle.App/`) — this is genuinely greenfield. Use RESEARCH.md's verified MSBuild shape directly (cited from official Microsoft Learn docs, fetched this session):

```xml
<!-- src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml -->
<Project>
  <PropertyGroup>
    <PublishDir>bin\publish\win-x64\</PublishDir>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
</Project>
```

**Existing `.csproj` PropertyGroup style to extend** (`RigToggle.App.csproj`, full file, 18 lines):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Intentionally no elevation manifest element of any kind: this keeps the tool asInvoker
         (02-RESEARCH.md Pitfall 6) — do not add an elevated execution level or admin requirement. -->
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\RigToggle.Core\RigToggle.Core.csproj" />
    <ProjectReference Include="..\RigToggle.Windows\RigToggle.Windows.csproj" />
  </ItemGroup>

</Project>
```
Comment convention to follow: inline XML comments explaining *why* a property is set/omitted (see the "Intentionally no elevation manifest" comment) — apply the same style when adding `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` to this file per RESEARCH.md Pitfall 1 (the CLI does not honor `RuntimeIdentifier` from `.pubxml` alone), e.g.:
```xml
<!-- RuntimeIdentifier lives here (not only in the .pubxml) because `dotnet publish` CLI
     does not honor RuntimeIdentifier set only inside a .pubxml file — see 05-RESEARCH.md
     Pitfall 1. This project only ever targets win-x64 (D-09), so this is unconditioned. -->
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

---

### `README.md` (new, repo root — no analog)

No README exists anywhere in the repo (`find` confirmed). Use RESEARCH.md's documented publish command as the core content (Code Examples, lines 267-272):
```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```
Belt-and-suspenders fallback form to include per Pitfall 1 (also from RESEARCH.md):
```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64
```

---

## Shared Patterns

### XML-doc rationale comments (project-wide convention)
**Source:** `src/RigToggle.Core/ToggleService.cs` (class-level and per-method doc comments throughout), `src/RigToggle.Core/Models/StateSnapshot.cs`, `src/RigToggle.Core/Models/MonitorPathSnapshot.cs`
**Apply to:** All new/modified files this phase, especially `ToggleService.cs`'s updated class doc (must replace the now-stale "Partial-failure handling (CORE-04) is explicitly out of scope" sentence) and the new asymmetry-documentation requirement (D-05).
**Pattern:** Every class/method-level doc comment explains *why*, references decision IDs by name (e.g. "D-08/CORE-03", "D-14", "gap-closure 03-04"), and calls out non-obvious invariants explicitly rather than leaving them implicit in code.

### Plain `sealed record` POCOs, no inheritance/generics
**Source:** `src/RigToggle.Core/Models/AudioRoleState.cs`, `src/RigToggle.Core/Models/MonitorPathSnapshot.cs`, `src/RigToggle.Core/Models/StateSnapshot.cs`, `src/RigToggle.Core/Models/AudioState.cs`
**Apply to:** `ToggleStepResult.cs`, `ToggleResult.cs` (both new)
**Pattern:** `public sealed record TypeName(params...)` — positional record syntax, `namespace RigToggle.Core.Models;` file-scoped, one XML-doc `<summary>` per type, nullable reference fields (`string?`) marked explicitly where the value may be legitimately absent.

### MessageBox.Show conventions (owner/title/icon)
**Source:** `src/RigToggle.App/MainForm.cs` lines 79-85, 117-131 (and `SettingsForm.cs` likely follows the same pattern, not re-read this session as `MainForm.cs` alone was sufficient)
**Apply to:** The new checklist-rendering code in `BtnToggle_Click`
**Pattern:** `MessageBox.Show(this, <message>, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.{Warning|Information})` — owner always `this`, title always the literal `"Rig Toggle"`, icon chosen by severity.

### No mocking framework — hand-written recording fakes
**Source:** `src/RigToggle.Tests/Doubles/FakeControllers.cs` (whole file)
**Apply to:** Any new test double needed for `ToggleServiceTests.cs` updates (e.g. a `throwOnDisable` flag on `FakeMonitorController`, mirroring `FakeAudioController`'s existing `throwOnRestore` flag at lines 47-57, 81-88)
**Pattern:** Constructor-injected `bool throwOnX` flags default to `false`; when `true`, the relevant method appends its call-log entry first, then throws `InvalidOperationException` with a `"Fake ... failure (simulated ...)."`-worded message.

### `dotnet publish` self-contained/single-file/untrimmed (CLAUDE.md-mandated)
**Source:** `CLAUDE.md` (Packaging section) + `05-RESEARCH.md` (Standard Stack, Common Pitfalls 1-3)
**Apply to:** `RigToggle.App.csproj` + new `win-x64.pubxml` + `README.md`
**Pattern:** `SelfContained=true`, `PublishSingleFile=true`, `PublishTrimmed=false` (explicit, not relying on the default), `IncludeNativeLibrariesForSelfExtract=true`, `RuntimeIdentifier=win-x64` placed in the `.csproj` itself (not only the `.pubxml`, per Pitfall 1).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` | config | batch | No `.pubxml`/`Properties/` publish-profile precedent exists anywhere in this repo — greenfield MSBuild config. Use RESEARCH.md's Code Examples section (officially-sourced shape) directly. |
| `README.md` | docs | N/A | No README exists in the repo root or anywhere in the repo. Use RESEARCH.md's documented publish command directly; no in-repo prose-doc style to mirror (closest stylistic precedent is CLAUDE.md's own table-heavy Markdown, but a README for a one-command publish instruction doesn't need that structure). |

## Metadata

**Analog search scope:** `src/RigToggle.Core/` (Models/, ToggleService.cs), `src/RigToggle.App/` (MainForm.cs, RigToggle.App.csproj), `src/RigToggle.Tests/` (ToggleServiceTests.cs, Doubles/FakeControllers.cs), repo root (README search)
**Files scanned:** 13 (all `.cs` files under `src/RigToggle.Core/Models/` and `ToggleService.cs`; `MainForm.cs`; `RigToggle.App.csproj`; `ToggleServiceTests.cs`; `FakeControllers.cs`; directory listings for `Properties/PublishProfiles/` and repo-root README)
**Pattern extraction date:** 2026-07-24
