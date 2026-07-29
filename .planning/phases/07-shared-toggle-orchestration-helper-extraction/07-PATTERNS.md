# Phase 7: Shared Toggle-Orchestration Helper Extraction - Pattern Map

**Mapped:** 2026-07-29
**Files analyzed:** 6 (2 new, 3 modified, 1 modified-test-double)
**Analogs found:** 6 / 6 (all in-repo — this codebase's own prior-phase code is the closest and only relevant analog; no external pattern needed for `Interlocked.CompareExchange`, which RESEARCH.md already fully specifies)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|-----------------|---------------|
| `src/RigToggle.Core/ToggleOrchestrator.cs` | service (thin wrapper/decorator) | request-response (guarded pass-through) | `src/RigToggle.Core/ToggleService.cs` | exact (same layer, same public-surface shape, wraps it directly) |
| `src/RigToggle.Core/ToggleInProgressException.cs` | model (exception type) | request-response (preflight signal) | `ToggleService.cs` inline `InvalidOperationException` throws (no separate exception file exists yet — first extracted exception type in the codebase) | role-match (pattern exists inline, not yet as its own file) |
| `src/RigToggle.App/MainForm.cs` (modified) | controller/UI event handler | request-response | itself, pre-refactor (`BtnToggle_Click`, lines 54-161) | exact (in-place refactor, not a new file) |
| `src/RigToggle.App/Program.cs` (modified) | config (composition root) | CRUD (object construction/wiring) | itself, pre-refactor (lines 82-91) | exact (in-place refactor, not a new file) |
| `src/RigToggle.Tests/ToggleOrchestratorTests.cs` | test | request-response / event-driven (concurrency test) | `src/RigToggle.Tests/ToggleServiceTests.cs` | exact (same test project, same xUnit + hand-written-fake convention) |
| `src/RigToggle.Tests/Doubles/BlockingMonitorController.cs` (new, optional) | test double | event-driven (blocking synchronization) | `src/RigToggle.Tests/Doubles/FakeControllers.cs` (`FakeMonitorController`) | role-match (same interface, adds blocking behavior FakeMonitorController doesn't have) |

## Pattern Assignments

### `src/RigToggle.Core/ToggleOrchestrator.cs` (service, request-response guarded pass-through)

**Analog:** `src/RigToggle.Core/ToggleService.cs`

**Namespace/imports pattern** (`ToggleService.cs` lines 1-4):
```csharp
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core;
```
`ToggleOrchestrator` lives in the same `RigToggle.Core` namespace/project (it wraps `ToggleService`, which is also here) and will additionally need `using System.Threading;` for `Interlocked`.

**Constructor null-guard pattern** (`ToggleService.cs` lines 33-45):
```csharp
public ToggleService(
    ISettingsStore settingsStore,
    ISnapshotStore snapshotStore,
    IMonitorController monitorController,
    IAudioController audioController,
    IAppController appController)
{
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
    _appController = appController ?? throw new ArgumentNullException(nameof(appController));
}
```
Copy this exact `?? throw new ArgumentNullException(nameof(...))` idiom for `ToggleOrchestrator`'s single-dependency constructor (`ToggleService toggleService`).

**Sealed class + XML-doc-rationale convention** (`ToggleService.cs` lines 6-25, class-level doc):
The class doc explains *why* the asymmetry/design choice exists, not just what the class does, and explicitly warns future editors not to "fix" an intentional-looking irregularity. `ToggleOrchestrator`'s class doc must do the same for D-01/D-02 (why busy-flag not lock/queue, why one shared flag not per-direction) — RESEARCH.md's Pattern 2 code example already drafts this doc; use it verbatim as the starting point, since it already matches this file's established rationale-comment convention.

**Core guarded-pass-through pattern** — copy directly from RESEARCH.md Pattern 2 (fully drafted, verified-idiom code, cited against `learn.microsoft.com/dotnet/api/system.threading.interlocked.compareexchange`):
```csharp
public sealed class ToggleOrchestrator
{
    private readonly ToggleService _toggleService;
    private int _busy; // 0 = idle, 1 = a toggle is in flight

    public ToggleOrchestrator(ToggleService toggleService)
    {
        _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
    }

    public ToggleResult ToggleToRigMode() => RunGuarded(_toggleService.ToggleToRigMode);

    public ToggleResult ToggleToNormalMode() => RunGuarded(_toggleService.ToggleToNormalMode);

    public bool IsInRigMode() => _toggleService.IsInRigMode();
    public bool IsSettingsConfigured() => _toggleService.IsSettingsConfigured();

    private ToggleResult RunGuarded(Func<ToggleResult> pipeline)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new ToggleInProgressException(
                "A toggle is already in progress. Wait for it to finish, then try again.");
        }

        try
        {
            return pipeline();
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }
}
```
Message-string style ("A toggle is already in progress. Wait for it to finish, then try again.") matches the direct, user-facing tone of `ToggleService.cs`'s own preflight messages (see Error Handling excerpt below) — plain sentence, no jargon, actionable next step.

**Error handling / preflight-exception pattern to match** (`ToggleService.cs` lines 60-68):
```csharp
if (!IsFullyConfigured(settings))
{
    // Guard against WR-01: without this check, an unconfigured (null-field)
    // AppSettings would still make it through to _snapshotStore.Save() below,
    // durably persisting a garbage snapshot and flipping IsInRigMode() to true
    // (D-14) even though nothing was actually captured or changed.
    throw new InvalidOperationException(
        "Rig Toggle settings are not fully configured. Open Settings and choose at least one monitor to disable or enable, both audio devices, and the companion app path before switching to Rig Mode.");
}
```
`ToggleInProgressException` is this same shape — a plain, direct `InvalidOperationException`-family throw with a clear rationale comment above it — just extracted into its own dedicated exception type instead of an inline `throw new InvalidOperationException(...)`.

---

### `src/RigToggle.Core/ToggleInProgressException.cs` (model, exception type)

**Analog:** No standalone exception file exists yet in this codebase (`grep -rn "class.*Exception"` across `src/` returns zero matches) — the analog is the *inline* preflight-exception precedent inside `ToggleService.cs` (see excerpt above), now being extracted into its own file for the first time.

**Pattern to follow** — subclass `InvalidOperationException`, not bare `Exception` (D-05 requirement; matches `MainForm.BtnToggle_Click`'s existing `catch (Exception ex)` which already handles any `InvalidOperationException` today):
```csharp
namespace RigToggle.Core;

/// <summary>
/// Thrown by ToggleOrchestrator when a toggle is requested while another is already
/// in flight (CORE-06). Subclasses InvalidOperationException (not a bare Exception)
/// so it is caught by MainForm.BtnToggle_Click's existing `catch (Exception ex)` block
/// with zero UI changes — the same way ToggleService's own preflight guards
/// (unconfigured settings, missing companion app path) are already surfaced today.
/// </summary>
public sealed class ToggleInProgressException : InvalidOperationException
{
    public ToggleInProgressException(string message) : base(message) { }
}
```
`sealed class`, one-line constructor delegating to `base(message)` — matches this codebase's `sealed class ToggleService`/`sealed class ToggleOrchestrator` convention (every top-level Core type in this project is `sealed` — see `ToggleService.cs` line 25 and every `Fake*Controller` in `FakeControllers.cs`).

---

### `src/RigToggle.App/MainForm.cs` (modified — field/constructor/call-site refactor only)

**Analog:** itself, pre-refactor.

**Constructor injection pattern to preserve** (`MainForm.cs` lines 18-35):
```csharp
private readonly ToggleService _toggleService;
private readonly ISettingsStore _settingsStore;
private readonly IMonitorController _monitorController;
private readonly Func<SettingsForm> _settingsFormFactory;

public MainForm(
    ToggleService toggleService,
    ISettingsStore settingsStore,
    IMonitorController monitorController,
    Func<SettingsForm> settingsFormFactory)
{
    _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    _settingsFormFactory = settingsFormFactory ?? throw new ArgumentNullException(nameof(settingsFormFactory));

    InitializeComponent();
}
```
The diff is: `ToggleService toggleService` param/field → `ToggleOrchestrator toggleOrchestrator` (or similar), same `?? throw new ArgumentNullException` guard shape unchanged. `_settingsStore`/`_monitorController`/`_settingsFormFactory` fields and their guards are untouched.

**Exact call sites to redirect** (`MainForm.cs`):
- Line 49: `bool isInRigMode = _toggleService.IsInRigMode();` → `_orchestrator.IsInRigMode()`
- Line 60: `if (_toggleService.IsInRigMode())` → `_orchestrator.IsInRigMode()`
- Line 62: `result = _toggleService.ToggleToNormalMode();` → `_orchestrator.ToggleToNormalMode()`
- Line 66: `if (!_toggleService.IsSettingsConfigured())` → `_orchestrator.IsSettingsConfigured()`
- Line 124: `result = _toggleService.ToggleToRigMode();` → `_orchestrator.ToggleToRigMode()`

**Existing catch-and-display path — do not modify** (`MainForm.cs` lines 142-160):
```csharp
catch (Exception ex)
{
    // Basic guard only (D-13/T-02-FAKEFAIL) — this catch is the fallback for the
    // exception-based preflight/corrupted-snapshot guards (unconfigured settings,
    // missing companion app path, corrupted monitor snapshot), which are NOT part
    // of the ToggleResult contract (see Plan 01). ...
    MessageBox.Show(
        this,
        $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
}
```
This block already surfaces `ToggleInProgressException` correctly with zero changes (D-05's entire point) — the executor should confirm this by test, not by adding a new `catch (ToggleInProgressException)` branch (that would be scope creep beyond D-05's design).

---

### `src/RigToggle.App/Program.cs` (modified — composition-root wiring only)

**Analog:** itself, pre-refactor (`Program.cs` lines 82-91).

**Current wiring to extend, not replace:**
```csharp
var toggleService = new ToggleService(
    settingsStore,
    snapshotStore,
    monitorController,
    audioController,
    appController);

SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore);

var mainForm = new MainForm(toggleService, settingsStore, monitorController, SettingsFormFactory);

Application.Run(mainForm);
```
**New wiring** (per RESEARCH.md Pattern 1) — insert one line between `toggleService` construction and `mainForm` construction, then change `mainForm`'s first argument:
```csharp
var toggleService = new ToggleService(
    settingsStore,
    snapshotStore,
    monitorController,
    audioController,
    appController);

var toggleOrchestrator = new ToggleOrchestrator(toggleService);

SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore);

var mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory);

Application.Run(mainForm);
```
This is the entire `Program.cs` diff — no other lines in this file change (settings loading, trace-listener wiring, and adapter construction above line 82 are all untouched).

---

### `src/RigToggle.Tests/ToggleOrchestratorTests.cs` (test, request-response + concurrency)

**Analog:** `src/RigToggle.Tests/ToggleServiceTests.cs`

**Test class shape / xUnit + IDisposable convention** (`ToggleServiceTests.cs` lines 1-45):
```csharp
using RigToggle.Core;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;

public class ToggleServiceTests : IDisposable
{
    private readonly string ExistingCompanionAppPath = Path.GetTempFileName();
    private readonly AppSettings ConfiguredSettings;

    public ToggleServiceTests()
    {
        ConfiguredSettings = new AppSettings { /* ... */ };
    }

    public void Dispose() => File.Delete(ExistingCompanionAppPath);

    private (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
        AppSettings? settings = null, /* ...flags... */)
    {
        var callLog = new List<string>();
        var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
        var snapshotStore = new InMemorySnapshotStore(callLog);
        var monitorController = new FakeMonitorController(callLog, /* ... */);
        var audioController = new FakeAudioController(callLog, /* ... */);
        var appController = new FakeAppController(callLog, /* ... */);

        var service = new ToggleService(settingsStore, snapshotStore, monitorController, audioController, appController);
        return (service, callLog, snapshotStore);
    }

    [Fact]
    public void ToggleToRigMode_SavesSnapshotBeforeAnyMutationCall()
    {
        var (service, callLog, _) = CreateService();
        service.ToggleToRigMode();
        // ... Assert.True / Assert.Equal ...
    }
}
```
`ToggleOrchestratorTests` should follow this exact shape: a `CreateOrchestrator(...)` helper analogous to `CreateService(...)` that builds a real `ToggleService` wired to the existing fakes, wraps it in `ToggleOrchestrator`, and returns `(orchestrator, callLog, ...)`. No mocking framework — plain xUnit `[Fact]` + hand-written fakes, matching this file's own precedent.

**Deterministic concurrency test pattern (no analog exists yet in this codebase — first concurrency test)** — copy the shape from RESEARCH.md Pitfall 2's recommendation directly, since it is the only correct pattern and is fully specified there:
```csharp
[Fact]
public void ToggleToRigMode_RejectsSecondCallWhileFirstInFlight()
{
    var enteredGuardedRegion = new ManualResetEventSlim(false);
    var releaseFirstCall = new ManualResetEventSlim(false);
    var (orchestrator, _, _) = CreateOrchestrator(
        monitorController: new BlockingMonitorController(enteredGuardedRegion, releaseFirstCall));

    var firstCallTask = Task.Run(() => orchestrator.ToggleToRigMode());
    enteredGuardedRegion.Wait(); // deterministic — no Thread.Sleep, no timing guess

    Assert.Throws<ToggleInProgressException>(() => orchestrator.ToggleToRigMode());

    releaseFirstCall.Set();
    var firstResult = firstCallTask.GetAwaiter().GetResult();
    Assert.True(firstResult.Success);
}
```
Also add a companion test proving the flag is released after a `ToggleService` preflight exception (Pitfall 3's warning-sign scenario): call `ToggleToRigMode()` once with unconfigured settings (expect `InvalidOperationException`, not `ToggleInProgressException`), then call it again with configured settings and assert it succeeds — proving the `finally` reset actually ran and the orchestrator isn't permanently wedged.

---

### `src/RigToggle.Tests/Doubles/BlockingMonitorController.cs` (test double, optional new file)

**Analog:** `src/RigToggle.Tests/Doubles/FakeControllers.cs` → `FakeMonitorController` (same `IMonitorController` interface, same recording-fake convention, same file-header doc style).

**Pattern to follow** (interface + constructor-flag shape, from `FakeControllers.cs` lines 12-27):
```csharp
public sealed class FakeMonitorController : IMonitorController
{
    private readonly List<string> _callLog;
    private readonly bool _throwOnDisable;
    private readonly bool _mutatesBeforeThrowingOnDisable;
    private bool _disableWasCalled;

    public FakeMonitorController(
        List<string> callLog,
        bool throwOnDisable = false,
        bool mutatesBeforeThrowingOnDisable = false)
    {
        _callLog = callLog;
        _throwOnDisable = throwOnDisable;
        _mutatesBeforeThrowingOnDisable = mutatesBeforeThrowingOnDisable;
    }
    // ... GetActiveMonitors / GetAllMonitors / CaptureState / ActivateMonitors /
    //     DeactivateMonitors / Restore, each appending a label to _callLog ...
}
```
`BlockingMonitorController` implements the same `IMonitorController` interface (all 6 members required — `GetActiveMonitors`, `GetAllMonitors`, `CaptureState`, `ActivateMonitors`, `DeactivateMonitors`, `Restore`); only `DeactivateMonitors` needs real (blocking) behavior — signal `enteredGuardedRegion.Set()` then `releaseFirstCall.Wait()` before returning. The other 5 members can return the same minimal fixture values `FakeMonitorController` already uses (e.g. the same single-`MonitorPathSnapshot` `MonitorState` literal at lines 54-56) — no need to invent new fixture data. Per RESEARCH.md's recommendation and Open Question #2, keep this as its own small file rather than bloating `FakeMonitorController`'s already multi-flag constructor.

---

## Shared Patterns

### Sealed classes everywhere in Core/App
**Source:** `ToggleService.cs` line 25 (`public sealed class ToggleService`), every class in `FakeControllers.cs`
**Apply to:** `ToggleOrchestrator`, `ToggleInProgressException`, `BlockingMonitorController` — all new types in this phase should be `sealed` unless there's a specific reason not to (none exists here).

### `?? throw new ArgumentNullException(nameof(...))` constructor guard
**Source:** `ToggleService.cs` lines 40-44, `MainForm.cs` lines 29-32
**Apply to:** `ToggleOrchestrator`'s constructor (`toggleService` parameter).

### XML-doc rationale comments explaining *why*, not *what*
**Source:** `ToggleService.cs` class-level doc (lines 6-24) and inline comments throughout (e.g. lines 62-65, 91-98, 111-122)
**Apply to:** `ToggleOrchestrator`'s class doc (why busy-flag not lock/queue — D-01; why one shared flag not per-direction — D-02) and `ToggleInProgressException`'s doc (why it subclasses `InvalidOperationException` — D-05). RESEARCH.md Patterns 2 and 3 already draft these comments in this exact style — copy them directly rather than rewriting.

### Exception-based preflight signaling, outside the `ToggleResult` step-checklist contract
**Source:** `ToggleService.cs` lines 60-68 and 76-77 (`InvalidOperationException` for unconfigured settings / missing companion app path)
**Apply to:** `ToggleInProgressException` — same "throw before any step runs" shape, same `InvalidOperationException` family, same plain user-facing message style, caught by the same unmodified `MainForm.BtnToggle_Click` catch block (lines 142-160).

### Composition-root-only object construction (no service locator, no static state)
**Source:** `Program.cs` lines 82-91 (`ToggleService`/`MainForm` constructed once, wired by hand)
**Apply to:** `ToggleOrchestrator` construction — one line inserted into the existing wiring block, `MainForm` receives it via constructor injection exactly as it receives `ToggleService` today.

### Hand-written recording/blocking fakes, no mocking framework
**Source:** `FakeControllers.cs` (entire file), `InMemoryStores.cs`
**Apply to:** `ToggleOrchestratorTests.cs` and `BlockingMonitorController.cs` — continue using plain `List<string>` call logs and constructor-flag-driven behavior variations; do not introduce Moq/NSubstitute/etc.

## No Analog Found

None. Every file in this phase has either a direct in-place analog (itself, pre-refactor — `MainForm.cs`, `Program.cs`) or a same-layer/same-convention analog to model the new file on (`ToggleService.cs` for `ToggleOrchestrator.cs`; `FakeControllers.cs`/`ToggleServiceTests.cs` for the new test files). The one genuinely novel element — a standalone exception type and a `Interlocked.CompareExchange`-based guard — has zero prior in-repo occurrence (`grep -rn "class.*Exception"` and `grep -rn "Interlocked\|volatile\|lock ("` across `src/` both return zero matches), but RESEARCH.md's Patterns 2/3 already supply fully-specified, codebase-convention-consistent code for both, so no gap exists for the planner.

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.App/`, `src/RigToggle.Tests/` (all `.cs` files read directly; `src/RigToggle.Windows/` and `src/RigToggle.Windows.Tests/` excluded — out of scope per CONTEXT.md, no Windows-adapter files touched by this phase)
**Files scanned:** `ToggleService.cs`, `MainForm.cs`, `Program.cs`, `FakeControllers.cs`, `ToggleServiceTests.cs` (first 100 lines), plus repo-wide `grep` for existing `Exception`/`Interlocked`/`lock` usage (confirmed zero — matches RESEARCH.md's own finding)
**Pattern extraction date:** 2026-07-29
