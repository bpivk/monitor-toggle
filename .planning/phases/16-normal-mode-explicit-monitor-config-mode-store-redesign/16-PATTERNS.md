# Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign - Pattern Map

**Mapped:** 2026-08-04
**Files analyzed:** 17 (7 new, 10 modified)
**Analogs found:** 16 / 17

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|----------------|
| `src/RigToggle.Core/Models/ToggleMode.cs` (NEW) | model (enum) | transform | `src/RigToggle.Core/Models/AppTheme.cs` | exact |
| `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` (NEW) | model (record) | transform | `src/RigToggle.Core/Models/ToggleStepResult.cs` | exact |
| `src/RigToggle.Core/Abstractions/IModeStore.cs` (NEW) | abstraction (persistence contract) | file-I/O | `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` | exact |
| `src/RigToggle.Core/Abstractions/IToggleInProgressStore.cs` (NEW) | abstraction (persistence contract) | file-I/O | `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` | exact |
| `src/RigToggle.Core/Persistence/JsonModeStore.cs` (NEW) | service (persistence impl) | file-I/O | `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` | exact |
| `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs` (NEW) | service (persistence impl) | file-I/O | `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` | exact |
| `src/RigToggle.App/StartupRecoveryChecker.cs` (NEW) | utility (startup gate + dialogs) | request-response (blocking `MessageBox.Show`) | `src/RigToggle.App/Program.cs` (composition-root inline startup checks) | role-match |
| `src/RigToggle.Tests/Doubles/InMemoryStores.cs` — add `InMemoryModeStore`/`InMemoryToggleInProgressStore` (MODIFIED) | test-double | in-memory CRUD | `src/RigToggle.Tests/Doubles/InMemoryStores.cs`'s existing `InMemorySnapshotStore` | exact |
| `src/RigToggle.Core/ToggleService.cs` (MODIFIED — rewrite `ToggleToNormalMode`, `IsInRigMode`, remove `ISnapshotStore`) | service (orchestration) | transform / event-driven (multi-step pipeline) | Same file's own `ToggleToRigMode` Monitor step (self-analog) | exact |
| `src/RigToggle.Core/ToggleOrchestrator.cs` (MODIFIED — marker lifecycle) | service (guard/wrapper) | event-driven | Same file's own `RunGuarded` busy-flag `finally` discipline (self-analog) | exact |
| `src/RigToggle.Core/Models/AppSettings.cs` (MODIFIED — add 2 fields) | model (settings POCO) | CRUD (JSON round-trip) | Same file's own `MonitorsToDisable`/`MonitorsToEnable` fields (self-analog) | exact |
| `src/RigToggle.App/Program.cs` (MODIFIED — bootstrap, new stores, startup checks) | composition root / config | file-I/O + wiring | Same file (self-analog, extend existing construction block) | exact |
| `src/RigToggle.App/SettingsForm.Designer.cs` (MODIFIED — new grid + downstream reflow) | ui-designer (generated layout) | n/a (declarative layout) | Same file's own `pnlMonitor`/`dgvMonitors` block (self-analog) | exact |
| `src/RigToggle.App/SettingsForm.cs` (MODIFIED — Normal grid population/validation/save) | ui-component (code-behind) | event-driven (grid cell events) + CRUD (settings save) | Same file's own `PopulateMonitorGrid`/`GetGridSelection`/`ValidateSettingsForm` (self-analog) | exact |
| `src/RigToggle.App/MainForm.cs` (MODIFIED — mode-known guard, `RefreshUi`) | ui-component (code-behind) | event-driven (button/tray/hotkey handlers) | Same file's own `RefreshUi`/`BtnToggle_Click` (self-analog) | exact |
| `src/RigToggle.Windows/WindowsMonitorController.cs` (MODIFIED — generalize guard text) | adapter (Win32/CCD wrapper) | transform | Same file's own `DeactivateMonitors` guard (self-analog) | exact |
| `src/RigToggle.Tests/ToggleServiceTests.cs` (MODIFIED — `CreateService` rewiring) | test | CRUD (arrange/act/assert) | Same file's own `CreateService` factory (self-analog) | exact |

## Pattern Assignments

### `src/RigToggle.Core/Models/ToggleMode.cs` (model, transform)

**Analog:** `src/RigToggle.Core/Models/AppTheme.cs` (11 lines, full file)

**Core pattern** (lines 1-11):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Windows app-theme setting (light/dark) as reported by IThemeProvider.
/// </summary>
public enum AppTheme
{
    Light,
    Dark,
}
```

Apply directly — `ToggleMode` is a two-value enum with the identical shape:
```csharp
namespace RigToggle.Core.Models;

public enum ToggleMode
{
    Normal,
    Rig,
}
```

---

### `src/RigToggle.Core/Models/ToggleInProgressMarker.cs` (model, transform)

**Analog:** `src/RigToggle.Core/Models/ToggleStepResult.cs` (9 lines, full file) — this codebase's established one-line `sealed record` idiom for small immutable payload types.

**Core pattern** (lines 1-9):
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

Apply directly, per RESEARCH.md's own sketch:
```csharp
namespace RigToggle.Core.Models;

public sealed record ToggleInProgressMarker(ToggleMode TargetMode, DateTimeOffset StartedAtUtc);
```

**Naming caution (RESEARCH.md, explicit):** do not let this type or `IToggleInProgressStore` read as the same concept as the existing `src/RigToggle.Core/ToggleInProgressException.cs` (an unrelated in-memory reentrancy-guard exception, CORE-06). Add an explicit doc-comment distinguishing the two, mirroring `ToggleInProgressException.cs`'s own doc-comment style.

---

### `src/RigToggle.Core/Abstractions/IModeStore.cs` (abstraction, file-I/O)

**Analog:** `src/RigToggle.Core/Abstractions/ISnapshotStore.cs` (full file, 17 lines)

**Core pattern** (lines 1-16):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for StateSnapshot. Snapshot-file presence itself is the
/// mode indicator (D-14): Mode == RigMode iff Exists() is true. Implemented by
/// RigToggle.Core.Persistence.JsonSnapshotStore (plain net10.0, no Windows API refs).
/// </summary>
public interface ISnapshotStore
{
    bool Exists();
    void Save(StateSnapshot snapshot);
    StateSnapshot? Load();
    void Clear();
}
```

Apply the identical shape, substituting the `TryLoad()` nullable-return convention (mirrors how `JsonSnapshotStore.Load()` already degrades corrupted JSON to `null` rather than throwing — see JsonModeStore pattern below):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

public interface IModeStore
{
    bool Exists();
    ToggleMode? TryLoad();
    void Save(ToggleMode mode);
}
```

---

### `src/RigToggle.Core/Abstractions/IToggleInProgressStore.cs` (abstraction, file-I/O)

**Analog:** same as above, `ISnapshotStore.cs`, but keep `Clear()` (mirrors the marker's own crash-detection lifecycle: written at toggle start, cleared in the orchestrator's `finally`, absent-on-next-launch = crash signal):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

public interface IToggleInProgressStore
{
    ToggleInProgressMarker? TryLoad();
    void Save(ToggleInProgressMarker marker);
    void Clear();
}
```

---

### `src/RigToggle.Core/Persistence/JsonModeStore.cs` (service/persistence, file-I/O)

**Analog:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` (full file, 67 lines)

**Imports pattern** (lines 1-5):
```csharp
using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;
```

**Core atomic-write pattern** (lines 15-37) — copy verbatim, this is the codebase's one and only file-persistence idiom (also used by `JsonSettingsStore.Save`):
```csharp
public sealed class JsonSnapshotStore : ISnapshotStore
{
    private readonly string _path;

    public JsonSnapshotStore(string path)
    {
        _path = path;
    }

    public bool Exists() => File.Exists(_path);

    public void Save(StateSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot));
        File.Move(tempPath, _path, overwrite: true);
    }
```

**Error-handling / degrade-to-null pattern** (lines 39-57) — the exact shape `TryLoad()` should follow, including the two `catch` clauses this codebase already treats as the standard corrupted-file posture:
```csharp
    public StateSnapshot? Load()
    {
        if (!Exists())
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StateSnapshot>(File.ReadAllText(_path));
        }
        catch (JsonException)
        {
            // A stale/old-shaped state.json (e.g. from before the AudioState reshape)
            // is treated as "no snapshot" = normal mode, rather than crashing on startup
            // (Open Question 1, T-03-01).
            return null;
        }
    }

    public void Clear()
    {
        if (Exists())
        {
            File.Delete(_path);
        }
    }
}
```

**Note:** `JsonModeStore.TryLoad()` must additionally catch `IOException` (not just `JsonException`) per `JsonSettingsStore.Load()`'s pattern (see next section) — an interrupted read (antivirus lock, mid-write 0-byte file) is a distinct-but-equally-real corruption cause RESEARCH.md's Code Examples section explicitly covers. Target path: `%LocalAppData%\RigToggle\mode.json`, constructed in `Program.cs` alongside the existing `Path.Combine(basePath, "state.json")`/`"settings.json"` calls.

---

### `src/RigToggle.Core/Persistence/JsonToggleInProgressStore.cs` (service/persistence, file-I/O)

**Analog:** same as above (`JsonSnapshotStore.cs`). Identical shape to `JsonModeStore`, plus the same `Clear()` method (already shown), serializing `ToggleInProgressMarker` instead of `ToggleMode`. Target path: `%LocalAppData%\RigToggle\toggle-in-progress.json`.

---

### `src/RigToggle.Core/ToggleService.cs` (service, transform/event-driven — MODIFIED)

**Analog:** the file's own existing `ToggleToRigMode` Monitor-step shape (lines 79-137) is the direct template for the rewritten `ToggleToNormalMode` (self-analog — RESEARCH.md Pattern 4 already specifies this mapping exactly).

**Constructor pattern to follow — remove `ISnapshotStore`, add `IModeStore`** (current shape, lines 27-45):
```csharp
private readonly ISettingsStore _settingsStore;
private readonly ISnapshotStore _snapshotStore;
private readonly IMonitorController _monitorController;
private readonly IAudioController _audioController;
private readonly IAppController _appController;

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
Apply: replace the `ISnapshotStore snapshotStore` parameter/field with `IModeStore modeStore`, keep the identical null-guard idiom for the new field.

**Rig-mode Monitor-step shape to mirror for the Normal-mode rewrite** (lines 87-137):
```csharp
var disableSet = (settings.MonitorsToDisable ?? new List<string>()).ToHashSet();
var enableSet = (settings.MonitorsToEnable ?? new List<string>()).ToHashSet();

// 06-RESEARCH.md Pitfall 2: ActivateMonitors MUST run BEFORE DeactivateMonitors. ...
if (!TryExecuteStep("Monitor", () =>
    {
        _monitorController.ActivateMonitors(enableSet);
        _monitorController.DeactivateMonitors(disableSet);
    }, steps))
{
    steps.Add(new ToggleStepResult("Audio", ToggleStepOutcome.NotAttempted, null));
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));

    // CR-01 recapture-and-compare guard — see below.
    try
    {
        if (MonitorStateUnchanged(monitorState, _monitorController.CaptureState()))
        {
            _snapshotStore.Clear();
        }
    }
    catch
    {
        // Re-capture failed — err toward keeping state.
    }

    return new ToggleResult(steps);
}
```
Apply: identical `ActivateMonitors(enableSet); DeactivateMonitors(disableSet);` call against `settings.NormalMonitorsToDisable`/`NormalMonitorsToEnable`, same `TryExecuteStep`/`TryExecuteOptionalStep` helpers (already static, reusable as-is, lines 184-226), same ordering constraint comment. The CR-01 `try { if (MonitorStateUnchanged(...)) { _snapshotStore.Clear(); } } catch { }` block must be extracted into a shared `ReconcileModeAfterMonitorFailure(MonitorState before)` helper (RESEARCH.md Pattern 2) and called from **both** `ToggleToRigMode`'s and the rewritten `ToggleToNormalMode`'s failure paths — this is a genuinely new generalization, not a straight copy, because today only `ToggleToRigMode` can hit this failure path.

**Structural-equality helper to reuse as-is** (lines 228-237):
```csharp
private static bool MonitorStateUnchanged(Models.MonitorState before, Models.MonitorState after) =>
    before.TargetDevicePath == after.TargetDevicePath && before.Paths.SequenceEqual(after.Paths);
```
No changes needed — reuse verbatim inside the new shared `ReconcileModeAfterMonitorFailure` helper.

**Mode-write timing (NEW behavior, no direct analog — see RESEARCH.md Pattern 2):** unlike `_snapshotStore.Save()` at line 83 (pre-mutation), `_modeStore.Save(ToggleMode.Normal)`/`.Save(ToggleMode.Rig)` must be called **after** the Monitor step's outcome is confirmed successful, mirrored identically in both `ToggleToRigMode` and `ToggleToNormalMode`.

**`IsInRigMode()` replacement** (current, lines 452-456):
```csharp
/// <summary>
/// Current mode is derived from snapshot-file presence (D-14) — no separate
/// in-memory/persisted flag exists.
/// </summary>
public bool IsInRigMode() => _snapshotStore.Exists();
```
Apply: replace with a `ToggleMode? CurrentMode => _modeStore.TryLoad();` (or equivalent) plus an `IsModeKnown()` convenience pass-through, per RESEARCH.md Pattern 3's `ToggleOrchestrator` guard requirement. `IsInRigMode()` itself may be kept as a `CurrentMode == ToggleMode.Rig` convenience wrapper for minimal call-site churn in `MainForm.cs`, or replaced — Claude's Discretion per CONTEXT.md (mode-store abstraction shape).

**Class-level doc comment (lines 1-24) is stale and must be replaced, not edited piecemeal** — the current comment documents the retired D-14 snapshot-presence mode derivation and the retired D-02 "enable-set always re-disabled, never snapshot-restored" asymmetry (now moot: both directions use `NormalMonitorsToDisable`/`NormalMonitorsToEnable` symmetric config). Per RESEARCH.md Pattern 4's explicit call-out, this is a required edit, not incidental cleanup.

---

### `src/RigToggle.Core/ToggleOrchestrator.cs` (service, event-driven — MODIFIED)

**Analog:** the file's own `RunGuarded` method (self-analog, full file read, 79 lines) — the existing busy-flag `finally` discipline is the exact shape the marker lifecycle must extend.

**Core pattern to extend** (lines 56-77):
```csharp
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
        // Must run even when ToggleService throws (its own preflight
        // InvalidOperationExceptions, or anything unexpected) — otherwise a
        // single failed toggle would permanently wedge the app in "busy" and
        // every future request (including a well-formed one) would be
        // rejected forever.
        Volatile.Write(ref _busy, 0);
    }
}
```
Apply per RESEARCH.md's sketch: add `_markerStore.Save(new ToggleInProgressMarker(targetMode, DateTimeOffset.UtcNow));` as the first statement inside the `try`, and `_markerStore.Clear();` as the first statement inside `finally` (before `Volatile.Write`) — this deliberately does **not** clear on a real process kill/crash, which is exactly the condition DISPLAY-13 exists to detect at next launch. `RunGuarded` needs a `ToggleMode targetMode` parameter threaded from `ToggleToRigMode()`/`ToggleToNormalMode()`'s existing one-line call sites (lines 46, 48):
```csharp
public ToggleResult ToggleToRigMode() => RunGuarded(_toggleService.ToggleToRigMode);
public ToggleResult ToggleToNormalMode() => RunGuarded(_toggleService.ToggleToNormalMode);
```
becomes `RunGuarded(ToggleMode.Rig, _toggleService.ToggleToRigMode)` / `RunGuarded(ToggleMode.Normal, _toggleService.ToggleToNormalMode)`.

**Pass-through read pattern to extend** (lines 50-54):
```csharp
// D-04 pass-throughs — pure reads, no guard. Safe to call at any time, including
// while a toggle is in flight (mirrors how MainForm.RefreshUi() already calls
// IsInRigMode() immediately after every toggle today).
public bool IsInRigMode() => _toggleService.IsInRigMode();
public bool IsSettingsConfigured() => _toggleService.IsSettingsConfigured();
```
Apply: add an `IsModeKnown()` (or `CurrentMode` returning `ToggleMode?`) pass-through in the identical no-guard style, per RESEARCH.md Pattern 3.

---

### `src/RigToggle.Core/Models/AppSettings.cs` (model, CRUD — MODIFIED)

**Analog:** the file's own existing `MonitorsToDisable`/`MonitorsToEnable` fields (self-analog, full file read, 37 lines).

**Core pattern** (lines 19-36):
```csharp
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public List<string>? MonitorsToDisable { get; set; }
    public List<string>? MonitorsToEnable { get; set; }
    public string? NormalAudioDeviceId { get; set; }
    ...
```
Apply directly: add `public List<string>? NormalMonitorsToDisable { get; set; }` and `public List<string>? NormalMonitorsToEnable { get; set; }` as flat sibling properties (CONTEXT.md leaves flat-vs-nested to planning; RESEARCH.md's own Code Examples and every downstream reference use flat naming, so flat is the path of least resistance matching the existing `MonitorsToDisable`/`ToEnable` precedent exactly). **Do not** add any default-population/migration logic for these two fields inside `JsonSettingsStore.Load()` — per RESEARCH.md Pitfall 5, the correct diff to that file is empty; the two new `List<string>?` properties round-trip through `System.Text.Json` with zero code changes.

---

### `src/RigToggle.App/Program.cs` (composition root, file-I/O + wiring — MODIFIED)

**Analog:** the file's own existing store-construction block (self-analog, full file read, 169 lines).

**Construction pattern to extend** (lines 48-111):
```csharp
string basePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "RigToggle");

var settingsStore = new JsonSettingsStore(Path.Combine(basePath, "settings.json"));
...
var snapshotStore = new JsonSnapshotStore(Path.Combine(basePath, "state.json"));

var monitorController = new WindowsMonitorController();
var audioController = new WindowsAudioController();
var appController = new WindowsAppController();
var autostartConfigurator = new WindowsAutostartConfigurator();
...
var toggleService = new ToggleService(
    settingsStore,
    snapshotStore,
    monitorController,
    audioController,
    appController);

var toggleOrchestrator = new ToggleOrchestrator(toggleService);
```
Apply: add `var modeStore = new JsonModeStore(Path.Combine(basePath, "mode.json"));` and `var markerStore = new JsonToggleInProgressStore(Path.Combine(basePath, "toggle-in-progress.json"));` alongside `snapshotStore`'s construction; insert the one-time bootstrap (RESEARCH.md Pattern 1) immediately after:
```csharp
if (!modeStore.Exists())
{
    modeStore.Save(snapshotStore.Exists() ? ToggleMode.Rig : ToggleMode.Normal);
}
```
then run the two startup blocking-dialog checks (via the new `StartupRecoveryChecker`, see below) **before** constructing `ToggleService` — remove `snapshotStore` from the `new ToggleService(...)` call, add `modeStore`.

**Best-effort try/catch convention to reuse for any new best-effort startup code** (lines 56-65, 76-91) — this codebase's established "never let a startup side-effect block `Application.Run`" idiom:
```csharp
AppSettings settings;
try
{
    settings = settingsStore.Load();
}
catch
{
    settings = new AppSettings();
}
```
Note: the two new startup dialogs (D-02/D-03/D-06/D-07) are explicitly **not** best-effort/swallowed — they are the one deliberate blocking exception to this pattern, run synchronously before `Application.Run`, per RESEARCH.md Pattern 3.

**Ordering constraint already established for tray-safe startup calls** (lines 132-143) — both new dialog checks must run before this exact point, on both the visible and `--tray` paths:
```csharp
mainForm.InitializeTrayState();
mainForm.RegisterHotkeyAtStartup();
```

---

### `src/RigToggle.App/StartupRecoveryChecker.cs` (utility, request-response — NEW)

**No direct analog exists** — this is the first dedicated startup-check helper class in `RigToggle.App`; today all startup logic lives inline in `Program.cs`'s `Main` method. RESEARCH.md explicitly recommends extracting it "for testability" rather than inlining in `Program.cs`.

**Closest structural reference:** `Program.cs`'s own inline `if (settings.EnableDebugLogging) { try { ... } catch { } }` block shape (lines 76-91) for the "best-effort startup side-effect" pattern, and the two-dialog sketch already provided in RESEARCH.md's Code Examples section (reproduce here as the implementation starting point):
```csharp
ToggleMode? currentMode = modeStore.TryLoad();
if (currentMode is null)
{
    MessageBox.Show(
        null,
        "Rig Toggle can't tell whether you're currently in Rig Mode or Normal Mode — " +
        "the saved mode file is missing or unreadable. Please check your monitors and " +
        "audio device manually before using Toggle.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
}
else
{
    var marker = markerStore.TryLoad();
    if (marker is not null)
    {
        markerStore.Clear();
        MessageBox.Show(
            null,
            $"Rig Toggle didn't finish its last toggle to {marker.TargetMode} Mode cleanly " +
            "— it may have crashed or been closed mid-toggle. No automatic retry has been " +
            "attempted; please check your monitors and audio device manually before using " +
            "Toggle again.",
            "Rig Toggle",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
```
Exact copy text is locked in `16-UI-SPEC.md`'s Copywriting Contract ("Startup dialog 1"/"Startup dialog 2" tables) — use that wording verbatim, not this paraphrase. `owner: null` is required (may run before `MainForm` exists under `--tray`, matching `Program.cs`'s own `--tray`-safe timing discipline for `InitializeTrayState()`/`RegisterHotkeyAtStartup()`).

---

### `src/RigToggle.App/SettingsForm.Designer.cs` (ui-designer, declarative layout — MODIFIED)

**Analog:** the file's own existing `pnlMonitor`/`dgvMonitors`/`lblMonitorExplain`/`colDisable`/`colEnable` block (self-analog, lines 106-192 read directly) — D-05 requires the new grid to mirror this **exactly**.

**Panel + grid construction pattern to duplicate for `pnlMonitorNormal`** (lines 111-190):
```csharp
this.pnlMonitor.Location = new System.Drawing.Point(12, 12);
this.pnlMonitor.Size = new System.Drawing.Size(396, 234);
this.pnlMonitor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
this.pnlMonitor.Name = "pnlMonitor";
this.pnlMonitor.Controls.Add(this.lblMonitorCaption);
this.pnlMonitor.Controls.Add(this.lblMonitorExplain);
this.pnlMonitor.Controls.Add(this.dgvMonitors);
this.pnlMonitor.Controls.Add(this.lblMonitorWarning);

// lblMonitorCaption
this.lblMonitorCaption.Text = "Monitor";
this.lblMonitorCaption.Location = new System.Drawing.Point(9, 9);
this.lblMonitorCaption.AutoSize = true;
this.lblMonitorCaption.Name = "lblMonitorCaption";

// dgvMonitors
this.dgvMonitors.AllowUserToAddRows = false;
this.dgvMonitors.AllowUserToDeleteRows = false;
this.dgvMonitors.AllowUserToResizeRows = false;
this.dgvMonitors.AllowUserToResizeColumns = false;
this.dgvMonitors.RowHeadersVisible = false;
this.dgvMonitors.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
this.dgvMonitors.MultiSelect = false;
this.dgvMonitors.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
this.dgvMonitors.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
this.dgvMonitors.Location = new System.Drawing.Point(12, 80);
this.dgvMonitors.Size = new System.Drawing.Size(372, 120);
this.dgvMonitors.Name = "dgvMonitors";
this.dgvMonitors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
this.colMonitorName,
this.colDisable,
this.colEnable});

// colDisable
this.colDisable.HeaderText = "Off (Rig)";
this.colDisable.Name = "colDisable";
this.colDisable.Width = 66;
this.colDisable.ToolTipText = "Turns this monitor off when switching to Rig Mode. Restored automatically when switching back to Normal Mode.";

// colEnable
this.colEnable.HeaderText = "On (Rig)";
this.colEnable.Name = "colEnable";
this.colEnable.Width = 66;
this.colEnable.ToolTipText = "Turns this monitor on when switching to Rig Mode (for a monitor normally kept off, e.g. to save power). Turned off again automatically when switching back to Normal Mode.";

// lblMonitorExplain
this.lblMonitorExplain.Location = new System.Drawing.Point(12, 22);
this.lblMonitorExplain.Size = new System.Drawing.Size(372, 50);
this.lblMonitorExplain.AutoSize = false;
this.lblMonitorExplain.Text = "Only controls what changes when switching TO Rig Mode. Normal Mode is always restored exactly as it was before — nothing to set up separately.";
this.lblMonitorExplain.Name = "lblMonitorExplain";

// lblMonitorWarning
this.lblMonitorWarning.Location = new System.Drawing.Point(12, 206);
this.lblMonitorWarning.Size = new System.Drawing.Size(372, 20);
this.lblMonitorWarning.AutoSize = false;
this.lblMonitorWarning.Visible = false;
this.lblMonitorWarning.Name = "lblMonitorWarning";
```

**Apply per `16-UI-SPEC.md`'s exact coordinate table** (already locked, not Claude's Discretion):
- `pnlMonitorNormal`: `Location(12,258)`, `Size(396,234)`, `BorderStyle.FixedSingle`
- `lblMonitorNormalCaption`: Text `"Normal Mode"`, `Location(9,9)`, `AutoSize=true`
- `lblMonitorNormalExplain`: `Location(12,22)`, `Size(372,50)`, `AutoSize=false`, text per UI-SPEC Copywriting Contract
- `dgvMonitorsNormal`: `Location(12,80)`, `Size(372,120)`, identical grid-config properties to `dgvMonitors`
- `colMonitorNameNormal`/`colDisableNormal`/`colEnableNormal`: headers `"Monitor"`/`"Off (Normal)"`/`"On (Normal)"`, same `Width=66` for the two checkbox columns
- `lblMonitorNormalWarning`: `Location(12,206)`, `Size(372,20)`, `Visible=false` initially

**Downstream reflow (mechanical, non-negotiable per RESEARCH.md Pitfall 4 / UI-SPEC's full table):** every control at or below `pnlAudioDevices` (currently `Location.Y = 258`) shifts `Location.Y` down by **+246px** (new panel height 234 + one 12px inter-panel gap), `Location.X` unchanged. `SettingsForm.ClientSize` grows from `(420, 768)` to `(420, 1014)`. UI-SPEC's own table lists the exact old→new Y for every affected control (`pnlAudioDevices`, `pnlAppPath`, `chkEnableDebugLogging`, `lblHotkeyCaption`, `txtHotkey`, `lblHotkeyWarning`, `chkCloseMinimizesToTray`, `chkMinimizeToTray`, `chkStartWithWindows`, `lblAutostartWarning`, `btnSaveSettings`, `btnDiscardChanges`) — apply verbatim, do not recompute.

**Stale-prose fixes required in this same file (RESEARCH.md Pitfall 3, locked in UI-SPEC Copywriting Contract):**
- `lblMonitorExplain.Text` (line 181): drop the now-false `"Normal Mode is always restored exactly as it was before — nothing to set up separately."` clause → `"Only controls what changes when switching TO Rig Mode. A monitor not listed here is left untouched."`
- `colDisable.ToolTipText` (line 165): drop `"...Restored automatically when switching back to Normal Mode."` → `"Turns this monitor off when switching to Rig Mode."`
- `colEnable.ToolTipText` (line 173): drop `"...Turned off again automatically when switching back to Normal Mode."` → `"Turns this monitor on when switching to Rig Mode (for a monitor normally kept off, e.g. to save power)."`

---

### `src/RigToggle.App/SettingsForm.cs` (ui-component, event-driven + CRUD — MODIFIED)

**Analog:** the file's own existing `PopulateMonitorGrid`/`OnMonitorCellValueChanged`/`GetGridSelection`/`ValidateSettingsForm`/save-merge block (self-analog, lines 357-528, 665-905 read directly). RESEARCH.md's Recommended Project Structure explicitly calls for **duplicating**, not sharing, this logic for the Normal grid ("per existing single-grid precedent style") — there is no generalized/parameterized version to extend.

**`PopulateMonitorGrid` pattern to duplicate as `PopulateMonitorGridNormal`** (lines 357-428):
```csharp
private void PopulateMonitorGrid()
{
    errMonitor.SetError(dgvMonitors, string.Empty);
    lblMonitorWarning.Visible = false;

    try
    {
        _allMonitors = _monitorController.GetAllMonitors();
    }
    catch (Exception)
    {
        _allMonitors = Array.Empty<MonitorInfo>();
    }

    dgvMonitors.CellValueChanged -= OnMonitorCellValueChanged;
    dgvMonitors.Rows.Clear();

    if (_allMonitors.Count == 0)
    {
        dgvMonitors.Enabled = false;
        lblMonitorWarning.Text = "No displays detected.";
        lblMonitorWarning.Visible = true;
        dgvMonitors.CellValueChanged += OnMonitorCellValueChanged;
        return;
    }

    dgvMonitors.Enabled = true;

    var disableSet = new HashSet<string>(_settings.MonitorsToDisable ?? new List<string>());
    var enableSet = new HashSet<string>(_settings.MonitorsToEnable ?? new List<string>());

    foreach (MonitorInfo monitor in _allMonitors)
    {
        string suffix = monitor.IsPrimary
            ? " (Primary)"
            : !monitor.IsActive
                ? " (currently OS-disabled)"
                : string.Empty;

        int rowIndex = dgvMonitors.Rows.Add(
            monitor.FriendlyName + suffix,
            disableSet.Contains(monitor.DevicePath),
            enableSet.Contains(monitor.DevicePath));

        dgvMonitors.Rows[rowIndex].Tag = monitor.DevicePath;
    }

    dgvMonitors.CellValueChanged += OnMonitorCellValueChanged;
}
```
Apply for `PopulateMonitorGridNormal()`: reuse the **same** `_allMonitors` list already populated by `PopulateMonitorGrid()` (RESEARCH.md/UI-SPEC both note "no second `GetAllMonitors()` call needed" — call `PopulateMonitorGrid()` first, then `PopulateMonitorGridNormal()` reads `_allMonitors` directly, only re-reading `_settings.NormalMonitorsToDisable`/`NormalMonitorsToEnable` instead and writing to `dgvMonitorsNormal`). Duplicate the empty-state and row-population logic verbatim against the new grid/columns/warning label.

**Reentrancy-guarded sibling-uncheck pattern to duplicate** (lines 443-475, `OnMonitorCellValueChanged`) — needs its own `_updatingMonitorGridNormalProgrammatically` boolean field (mirrors `_updatingMonitorGridProgrammatically`) and its own `OnMonitorNormalCellValueChanged` handler wired to `dgvMonitorsNormal.CellValueChanged`:
```csharp
private void OnMonitorCellValueChanged(object? sender, DataGridViewCellEventArgs e)
{
    if (e.RowIndex < 0)
    {
        return; // column-header pseudo-event guard
    }

    if (!_updatingMonitorGridProgrammatically
        && (e.ColumnIndex == colDisable.Index || e.ColumnIndex == colEnable.Index))
    {
        DataGridViewRow row = dgvMonitors.Rows[e.RowIndex];
        bool newValue = row.Cells[e.ColumnIndex].Value is true;

        if (newValue)
        {
            int siblingIndex = e.ColumnIndex == colDisable.Index ? colEnable.Index : colDisable.Index;

            _updatingMonitorGridProgrammatically = true;
            try
            {
                row.Cells[siblingIndex].Value = false;
            }
            finally
            {
                _updatingMonitorGridProgrammatically = false;
            }
        }
    }

    ValidateSettingsForm();
}
```

**`GetGridSelection` pattern to duplicate as `GetGridSelectionNormal`** (lines 479-503) — same Tag-keyed-by-DevicePath convention, never row index:
```csharp
private (HashSet<string> Disable, HashSet<string> Enable) GetGridSelection()
{
    var disable = new HashSet<string>();
    var enable = new HashSet<string>();

    foreach (DataGridViewRow row in dgvMonitors.Rows)
    {
        if (row.Tag is not string devicePath)
        {
            continue;
        }

        if (row.Cells[colDisable.Index].Value is true)
        {
            disable.Add(devicePath);
        }

        if (row.Cells[colEnable.Index].Value is true)
        {
            enable.Add(devicePath);
        }
    }

    return (disable, enable);
}
```

**`ValidateSettingsForm` gating pattern** (lines 682-740) — the safety guard (`WouldLeaveAtLeastOneMonitorActive`) is Rig-specific per RESEARCH.md's Anti-Pattern list ("no second Settings-time blocking cross-check for Normal") — the Normal grid's own validation should require only "at least one action configured" (matching D-01's "untouched if unmentioned" semantics, which makes an all-empty Normal grid valid, unlike Rig's grid where `WouldLeaveAtLeastOneMonitorActive` is mandatory):
```csharp
private void ValidateSettingsForm()
{
    bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
    bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
    bool appPathOk = _pendingAppPath is null || IsValidLaunchTarget(_pendingAppPath);
    bool monitorOk;

    if (!dgvMonitors.Enabled)
    {
        monitorOk = false;
    }
    else
    {
        var (disableSelected, enableSelected) = GetGridSelection();

        if (!WouldLeaveAtLeastOneMonitorActive(_allMonitors, disableSelected, enableSelected))
        {
            lblMonitorWarning.Text = "This configuration would leave no monitor active. At least one monitor must stay enabled after switching to Rig Mode.";
            lblMonitorWarning.Visible = true;
            errMonitor.SetError(dgvMonitors, lblMonitorWarning.Text);
            monitorOk = false;
        }
        else if (disableSelected.Count == 0 && enableSelected.Count == 0)
        {
            lblMonitorWarning.Text = "Select at least one monitor to disable or enable.";
            lblMonitorWarning.Visible = true;
            errMonitor.SetError(dgvMonitors, lblMonitorWarning.Text);
            monitorOk = false;
        }
        else
        {
            errMonitor.SetError(dgvMonitors, string.Empty);
            ...
            monitorOk = true;
        }
    }

    btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;
}
```
Apply: fold a Normal-grid-derived boolean into the final `btnSaveSettings.Enabled` expression. Per D-01 ("a monitor not listed in either set is left untouched" is the documented default, not an error state), the Normal grid should NOT block Save when both sets are empty — this is a genuine behavioral divergence from the Rig grid's `disableSelected.Count == 0 && enableSelected.Count == 0` blocking gate, since an all-empty Normal-mode config is valid (RESEARCH.md's own Assumptions Log A4 confirms: toggling to Normal with both fields null should silently no-op the Monitor step, not be an error state).

**`BtnSaveSettings_Click` merge pattern** (lines 833-905) — the stale-entry-preserving merge logic must be duplicated for the Normal sets:
```csharp
var enumeratedPaths = new HashSet<string>(_allMonitors.Select(m => m.DevicePath));
IEnumerable<string> staleDisable = (_settings.MonitorsToDisable ?? new List<string>())
    .Where(p => !enumeratedPaths.Contains(p));
IEnumerable<string> staleEnable = (_settings.MonitorsToEnable ?? new List<string>())
    .Where(p => !enumeratedPaths.Contains(p));

var mergedDisable = new HashSet<string>(staleDisable);
mergedDisable.UnionWith(disableSelected);

var mergedEnable = new HashSet<string>(staleEnable);
mergedEnable.UnionWith(enableSelected);
...
var settingsToSave = new AppSettings
{
    MonitorDevicePath = _settings.MonitorDevicePath,
    MonitorFriendlyName = _settings.MonitorFriendlyName,
    MonitorsToDisable = mergedDisable.ToList(),
    MonitorsToEnable = mergedEnable.ToList(),
    ...
};
```
Apply: same stale-preserving merge against `GetGridSelectionNormal()`'s output, writing `NormalMonitorsToDisable = mergedDisableNormal.ToList()` / `NormalMonitorsToEnable = mergedEnableNormal.ToList()` into the same `settingsToSave` object.

**Theming call sites (mandatory, exact per `16-UI-SPEC.md` Interaction Contract)** — `dgvMonitorsNormal` MUST be threaded through `ThemeApplier.ThemeMonitorGrid` at the identical two call sites the Rig grid already uses:
```csharp
// SettingsForm_Load, line 182
ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme);
// OnThemeChanged, line 144
ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme);
```
Add `ThemeApplier.ThemeMonitorGrid(dgvMonitorsNormal, IsDarkTheme);` immediately after each existing call — omitting either produces an unthemed grid silently reverting to light-mode colors under dark mode (UI-SPEC's explicit warning).

---

### `src/RigToggle.App/MainForm.cs` (ui-component, event-driven — MODIFIED)

**Analog:** the file's own existing `RefreshUi`/`BtnToggle_Click`/`TrayToggleMenuItem_Click`/`HandleHotkeyToggle` (self-analog, full file read, 741 lines).

**`RefreshUi` pattern to rewrite** (lines 255-271):
```csharp
private void RefreshUi()
{
    bool isInRigMode = _orchestrator.IsInRigMode();
    lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
    btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";

    if (_normalIcon is not null && _rigIcon is not null)
    {
        notifyIcon.Icon = isInRigMode ? _rigIcon : _normalIcon;
    }
    notifyIcon.Text = isInRigMode ? "Rig Toggle — Rig Mode" : "Rig Toggle — Normal Mode";
    trayToggleMenuItem.Text = btnToggle.Text;
}
```
Apply: `_orchestrator.IsInRigMode()` must now read from `IModeStore` via `ToggleOrchestrator` (either kept as a `CurrentMode == ToggleMode.Rig` convenience wrapper, or replaced with a tri-state-aware read — see `ToggleService.cs`'s own pattern entry above). Add an unknown-mode branch (per UI-SPEC's "Toggle-trigger guard when mode is unknown" table) — when `IsModeKnown()` is false, `lblMode.Text` should read something like `"Mode: Unknown"` rather than defaulting to either label.

**Toggle-trigger guard pattern to add — three call sites, each already has an established chrome convention to extend:**

1. `BtnToggle_Click` (lines 273-403) — GUI trigger, uses `MessageBox.Show(this, ...)`. Existing WR-01 guard shape to mirror (lines 285-297):
```csharp
if (!_orchestrator.IsSettingsConfigured())
{
    MessageBox.Show(
        this,
        "Please choose at least one monitor to disable or enable in Settings before switching to Rig Mode.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
    return;
}
```
Apply an identical `if (!_orchestrator.IsModeKnown()) { MessageBox.Show(this, "...", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }` guard **before** the `IsInRigMode()` branch decision itself (that branch requires a known mode). Exact copy is locked in UI-SPEC's Copywriting Contract ("Toggle-trigger guard when mode is unknown" table).

2. `TrayToggleMenuItem_Click` (lines 574-610) and `HandleHotkeyToggle` (lines 621-657) — both use `notifyIcon.ShowBalloonTip`, never `MessageBox.Show` (D-08 no-chrome guarantee, explicitly documented in both methods' doc comments). Existing balloon-tip pattern to mirror (lines 584-592):
```csharp
catch (ToggleInProgressException ex)
{
    notifyIcon.ShowBalloonTip(
        3000,
        "Rig Toggle",
        ToggleResultFormatter.TruncateForBalloon(ex.Message),
        ToolTipIcon.Warning);
    return;
}
```
Apply an identical `if (!_orchestrator.IsModeKnown()) { notifyIcon.ShowBalloonTip(3000, "Rig Toggle", "...", ToolTipIcon.Warning); return; }` guard at the top of both handlers, before the `IsInRigMode()` branch decision. Exact copy locked in UI-SPEC's Copywriting Contract.

---

### `src/RigToggle.Windows/WindowsMonitorController.cs` (adapter, transform — MODIFIED)

**Analog:** the file's own existing `DeactivateMonitors` zero-survivors guard (self-analog, lines 268-309 read directly).

**Guard pattern whose text must generalize** (lines 295-309):
```csharp
PathInfo[] survivors = currentPaths.Where(p => !targets.Contains(p)).ToArray();

if (survivors.Length == 0)
{
    // ApplyPathInfos would call ValidatePathInfos on an empty array and throw a
    // generic PathChangeException("Invalid paths information.") with no
    // indication of the actual cause (validation fails before any native
    // mutation — this does NOT blank the screen, but the error is useless).
    // Reachable in production: every currently-active display is configured
    // in the disable-set, or a laptop with only its built-in display, when
    // Switch to Rig Mode is pressed.
    throw new InvalidOperationException(
        "Cannot disable all configured monitors — at least one active display must " +
        "remain. Connect and enable another display before switching to Rig Mode.");
}
```
Apply: the exception message must drop the Rig-specific `"...before switching to Rig Mode."` trailer, per UI-SPEC's Copywriting Contract table, since this same method is now reachable from `ToggleToNormalMode`'s rewritten Monitor step too (this phase is the first to add a second caller). New text: `"Cannot disable all configured monitors — at least one active display must remain."` (drop the mode-specific instruction entirely rather than trying to name both modes).

---

### `src/RigToggle.Tests/Doubles/InMemoryStores.cs` (test-double, in-memory CRUD — MODIFIED)

**Analog:** the file's own existing `InMemorySnapshotStore` (full file read, 53 lines).

**Core pattern to duplicate for `InMemoryModeStore`/`InMemoryToggleInProgressStore`** (lines 6-33):
```csharp
public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly List<string> _callLog;
    private StateSnapshot? _snapshot;

    public InMemorySnapshotStore(List<string> callLog) => _callLog = callLog;

    public bool Exists() => _snapshot is not null;

    public void Save(StateSnapshot snapshot)
    {
        _snapshot = snapshot;
        _callLog.Add("snapshot.Save");
    }

    public StateSnapshot? Load() => _snapshot;

    public void Clear()
    {
        _snapshot = null;
        _callLog.Add("snapshot.Clear");
    }
}
```
Apply verbatim shape for both new doubles: `InMemoryModeStore : IModeStore` (fields: `List<string> _callLog`, `ToggleMode? _mode`; log `"mode.Save"` on `Save`), `InMemoryToggleInProgressStore : IToggleInProgressStore` (fields: `List<string> _callLog`, `ToggleInProgressMarker? _marker`; log `"marker.Save"`/`"marker.Clear"`). This keeps the same shared-call-log assertion style (`callLog` list ordering) the existing `ToggleServiceTests`/`ToggleOrchestratorTests` already rely on to assert step ordering (e.g. "Clear is the last snapshot-store interaction").

---

### `src/RigToggle.Tests/ToggleServiceTests.cs` (test, CRUD — MODIFIED)

**Analog:** the file's own `CreateService` factory (self-analog, lines 47-57+ read directly) — every one of the file's 23 `[Fact]` test methods calls through this one factory, so this is the single mechanical choke point for the constructor-signature change.

**Current factory shape** (lines 47-57):
```csharp
private (ToggleService Service, List<string> CallLog, InMemorySnapshotStore SnapshotStore) CreateService(
    AppSettings? settings = null,
    bool audioThrowsOnRestore = false,
    bool monitorThrowsOnDisable = false,
    bool monitorMutatesBeforeThrowingOnDisable = false,
    bool appThrowsOnMinimize = false,
    bool audioDeviceMissing = false)
{
    var callLog = new List<string>();
    var settingsStore = new InMemorySettingsStore(settings ?? ConfiguredSettings);
    var snapshotStore = new InMemorySnapshotStore(callLog);
    var monitorController = new FakeMonitorController(
        callLog,
        throwOnDisable: monitorThrowsOnDisable,
```
Apply: replace `var snapshotStore = new InMemorySnapshotStore(callLog);` with `var modeStore = new InMemoryModeStore(callLog);` (and, if DISPLAY-13-adjacent orchestrator-level tests are in scope for this file rather than `ToggleOrchestratorTests.cs`, an `InMemoryToggleInProgressStore` too), update the tuple return type and the `new ToggleService(...)` construction call accordingly, and update every test method's destructuring (`var (service, callLog, _) = CreateService();`) only where the discarded/named third tuple element type changes. This is "mechanical but wide-blast-radius" per RESEARCH.md — size it as its own explicit task, not incidental churn. Tests asserting `SnapshotStore`-specific state (e.g. `ToggleToRigMode_SavesSnapshotBeforeAnyMutationCall`, `ToggleToNormalMode_SetsIsInRigModeFalse_AndClearIsLastSnapshotInteraction`, `ToggleToRigMode_KeepsSnapshot_WhenDisableThrowsAfterPartiallyMutating`) need their assertions rewritten against the new `modeStore`/call-log semantics (mode written on success, not before mutation; CR-01's shared-helper leaves the mode flag untouched rather than clearing a snapshot).

## Shared Patterns

### Atomic JSON file persistence (temp-file + `File.Move(overwrite:true)`)
**Source:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` lines 26-37, `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` lines 76-87
**Apply to:** `JsonModeStore.Save`, `JsonToggleInProgressStore.Save`
```csharp
var directory = Path.GetDirectoryName(_path);
if (!string.IsNullOrEmpty(directory))
{
    Directory.CreateDirectory(directory);
}

var tempPath = _path + ".tmp";
File.WriteAllText(tempPath, JsonSerializer.Serialize(value));
File.Move(tempPath, _path, overwrite: true);
```

### Corrupted-JSON degrade-to-null/fresh-default (never throw on load)
**Source:** `src/RigToggle.Core/Persistence/JsonSnapshotStore.cs` lines 39-57 (`catch (JsonException)`), `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` lines 62-73 (`catch (JsonException)` + `catch (IOException)`)
**Apply to:** `JsonModeStore.TryLoad`, `JsonToggleInProgressStore.TryLoad` — catch both `JsonException` and `IOException`, return `null` (never throw), matching `JsonSettingsStore`'s more complete two-exception coverage rather than `JsonSnapshotStore`'s single-exception coverage.

### Best-effort startup side-effect (never block `Application.Run`)
**Source:** `src/RigToggle.App/Program.cs` lines 56-65, 76-91
**Apply to:** any new non-dialog startup code in `Program.cs`/`StartupRecoveryChecker.cs` that is not itself one of the two deliberate blocking dialogs
```csharp
try
{
    // side effect
}
catch
{
    // degrade silently — never let a non-critical startup step block Application.Run
}
```

### Fail-fast, surface-via-MessageBox for anything toggle-relevant
**Source:** `src/RigToggle.Core/ToggleService.cs` lines 63-71 (unconfigured-settings preflight throw), lines 312-316 (corrupted-snapshot throw); `src/RigToggle.App/MainForm.cs` lines 290-296 (WR-01 config guard MessageBox)
**Apply to:** the D-06/D-07 mode-corruption dialog and D-02/D-03 crash-recovery dialog — one clear problem statement, one clear next-step instruction, `MessageBoxIcon.Warning`/`.Information`, never silent-log-only. Exact copy is locked in `16-UI-SPEC.md`'s Copywriting Contract, not to be improvised.

### Structural (order-independent) set comparison instead of `List<T>`/record equality
**Source:** `src/RigToggle.Core/ToggleService.cs` lines 228-237 (`MonitorStateUnchanged`, works around `IReadOnlyList<T>` reference-equality trap); `src/RigToggle.App/SettingsForm.cs` lines 875-877 (`HashSet<string>.SetEquals` for `monitorsChanged` detection)
**Apply to:** the shared `ReconcileModeAfterMonitorFailure` helper (reuses `MonitorStateUnchanged` as-is) and any Settings-save-time "did the Normal set change" comparison (mirror the `SetEquals` idiom, not raw `List<string>` equality).

### Reentrancy guard around programmatic grid writes (`_updatingXxxProgrammatically` flag)
**Source:** `src/RigToggle.App/SettingsForm.cs` lines 54, 450-471 (`_updatingMonitorGridProgrammatically`)
**Apply to:** the new `dgvMonitorsNormal`'s own `OnMonitorNormalCellValueChanged` handler — needs its own independent `_updatingMonitorGridNormalProgrammatically` field, not the shared Rig-grid flag (the two grids are edited/committed independently).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `src/RigToggle.App/StartupRecoveryChecker.cs` | utility (startup gate + dialogs) | request-response | First dedicated startup-check helper class in `RigToggle.App` — no prior extraction of startup logic out of `Program.cs`'s `Main` exists to model against. Use the inline `Program.cs` best-effort-try/catch idiom for structure and the RESEARCH.md/UI-SPEC-provided dialog sketches (reproduced above) for content; this is new-shape, low-risk (pure `MessageBox.Show` + two `TryLoad()` calls), not a novel mechanism. |

## Metadata

**Analog search scope:** `src/RigToggle.Core/` (Abstractions, Models, Persistence, top-level), `src/RigToggle.App/` (Program.cs, MainForm.cs, SettingsForm.cs, SettingsForm.Designer.cs), `src/RigToggle.Windows/WindowsMonitorController.cs`, `src/RigToggle.Tests/` (ToggleServiceTests.cs, ToggleOrchestratorTests.cs, JsonStoreTests.cs, Doubles/InMemoryStores.cs)
**Files scanned (read in full or targeted range):** `ToggleService.cs` (full, 457 lines), `ToggleOrchestrator.cs` (full, 79 lines), `ToggleInProgressException.cs` (full), `ISnapshotStore.cs` (full), `JsonSnapshotStore.cs` (full), `JsonSettingsStore.cs` (full), `AppSettings.cs` (full), `IMonitorController.cs` (full), `ToggleStepResult.cs` (full), `AppTheme.cs` (full), `MonitorState.cs` (full), `Program.cs` (full, 169 lines), `MainForm.cs` (full, 741 lines), `SettingsForm.cs` (lines 1-230, 350-530, 680-940), `SettingsForm.Designer.cs` (lines 1-270), `WindowsMonitorController.cs` (lines 268-312, targeted grep), `InMemoryStores.cs` (full), `ToggleServiceTests.cs` (lines 1-60 + grep of all 23 test-method signatures), `JsonStoreTests.cs` (lines 1-80)
**Pattern extraction date:** 2026-08-04
