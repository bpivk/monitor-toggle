# Phase 6: Multi-Monitor Data Model & Controller Generalization - Pattern Map

**Mapped:** 2026-07-28
**Files analyzed:** 13 (all modified in place — this phase adds zero new files/folders per 06-RESEARCH.md's "Recommended Project Structure")
**Analogs found:** 13 / 13 (every file's primary analog is itself, since this phase generalizes 1→N on already-existing, already-N-generalized-in-part code; secondary analogs — the audio device-pair shape, Phase 4's verify-and-throw CCD discipline — are cited per file below)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Core/Models/AppSettings.cs` | model | CRUD (JSON persistence) | itself (existing singular fields) + `src/RigToggle.Core/Models/AudioState.cs`/`AudioRoleState.cs` (two-related-item shape precedent) | exact (self) |
| `src/RigToggle.Core/Models/MonitorState.cs` | model | CRUD (snapshot) | itself — already N-generalized (`Paths: IReadOnlyList<MonitorPathSnapshot>`) | exact (self) |
| `src/RigToggle.Core/Models/MonitorInfo.cs` | model | transform (enumeration DTO) | itself | exact (self) |
| `src/RigToggle.Core/Models/MonitorPathSnapshot.cs` | model | CRUD (snapshot) | itself — unchanged by this phase (D-02 means enable-set never extends this shape); read for context only | exact (self, no changes expected) |
| `src/RigToggle.Core/Abstractions/IMonitorController.cs` | interface (contract) | request-response | itself + `src/RigToggle.Core/Abstractions/IAudioController.cs` (sibling contract shape) | exact (self) |
| `src/RigToggle.Core/ToggleService.cs` | service (orchestrator) | event-driven (multi-step toggle sequence) | itself | exact (self) |
| `src/RigToggle.Windows/WindowsMonitorController.cs` | controller (Windows CCD adapter) | CRUD + batch (topology mutation) | itself — `Disable()` is the direct analog for `DeactivateMonitors`, `Restore()`'s Extend fallback is the direct analog for `ActivateMonitors` | exact (self) |
| `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` | persistence (file I/O) | file-I/O | itself | exact (self) |
| `src/RigToggle.App/SettingsForm.cs` + `.Designer.cs` | component (WinForms form) | request-response (form load/validate/save) | itself — `PopulateAudioCombo`/`ShowStaleWarning` are the reusable per-row analogs for the new grid | exact (self) |
| `src/RigToggle.App/MonitorConfirmDialog.cs` + `.Designer.cs` | component (WinForms dialog) | request-response | itself | exact (self) |
| `src/RigToggle.App/MainForm.cs` | component (WinForms form) | request-response | itself (confirmation call site, lines 80-105) | exact (self) |
| `src/RigToggle.Tests/*` (`JsonStoreTests.cs`, `ToggleServiceTests.cs`, `Doubles/FakeControllers.cs`) | test | CRUD / event-driven | itself | exact (self) |
| `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` | test | transform (pure-logic helpers only) | itself — same "no live CCD hardware in CI" constraint applies to any new pure-logic helper | exact (self) |

**No files in this phase lack an analog.** Every file being touched already exists and already contains the exact single-target version of the pattern that needs generalizing to N — this is why 06-RESEARCH.md rates data-model/UI/migration confidence as HIGH.

---

## Pattern Assignments

### `src/RigToggle.Core/Models/AppSettings.cs` (model, CRUD)

**Analog:** itself (current singular fields) + `AudioState`/`AudioRoleState` as the "two related, independently-tracked items" precedent already proven in this codebase.

**Current full file** (`src/RigToggle.Core/Models/AppSettings.cs` lines 1-20):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Persisted user settings (selected monitor, audio device pair, companion app path).
/// Serialized as-is to %LocalAppData%\RigToggle\settings.json via ISettingsStore.
/// All fields nullable: a null field means "never configured" (first run), not "stale"
/// (see 02-RESEARCH.md Pattern 2 / Pitfall 3 for the first-run-vs-stale UI distinction).
/// </summary>
public sealed class AppSettings
{
    public string? MonitorDevicePath { get; set; }
    public string? MonitorFriendlyName { get; set; }   // display-cache only, not used for matching
    public string? NormalAudioDeviceId { get; set; }
    public string? NormalAudioDeviceName { get; set; }
    public string? RigAudioDeviceId { get; set; }
    public string? RigAudioDeviceName { get; set; }
    public string? CompanionAppPath { get; set; }
    public bool SkipMonitorConfirmation { get; set; }
    public bool EnableDebugLogging { get; set; }
}
```

**Pattern to apply:** Keep `MonitorDevicePath`/`MonitorFriendlyName` as-is (legacy migration source, D-08 — do not delete), and add plural fields alongside them, e.g. `MonitorsToDisable: List<string>` / `MonitorsToEnable: List<string>` (CONTEXT.md leaves the exact wrapper-vs-`List<string>` choice to the planner). `System.Text.Json` defaults an absent/`null` `List<string>` property to `null` on deserialize of a legacy file with no plural fields present at all — this is exactly the signal `JsonSettingsStore.Load()`'s migration check uses (see below).

**Two-related-items precedent** (`src/RigToggle.Core/Models/AudioState.cs` lines 1-9, `AudioRoleState.cs` lines 1-7):
```csharp
// AudioState.cs
public sealed record AudioState(AudioRoleState Console, AudioRoleState Multimedia, AudioRoleState Communications);

// AudioRoleState.cs
public sealed record AudioRoleState(string? DeviceId, string? DeviceName);
```
This is the existing precedent for "more than one independently-tracked device/monitor identity living inside one settings/state shape" — `AppSettings` already carries `NormalAudioDeviceId`/`RigAudioDeviceId` as a role-pair sitting side by side, same idea as `MonitorsToDisable`/`MonitorsToEnable` sitting side by side. Do not follow `AudioState`'s record/immutable shape for `AppSettings` itself, though — `AppSettings` is a mutable settings POCO (existing convention, `{ get; set; }`), not a captured-state record; only borrow the "two named, independently-persisted sibling sets" idea, not the record type.

---

### `src/RigToggle.Core/Models/MonitorInfo.cs` (model, transform)

**Analog:** itself.

**Current full file** (lines 1-7):
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// A single enumerated display, as returned by IMonitorController.GetActiveMonitors().
/// DevicePath is the stable identifier persisted in AppSettings.MonitorDevicePath.
/// </summary>
public sealed record MonitorInfo(string DevicePath, string FriendlyName, bool IsPrimary);
```

**Pattern to apply:** Add `bool IsActive` per 06-RESEARCH.md Pattern 3 (needed to render the grid's "(currently OS-disabled)" suffix and to drive the D-05 validation predicate). Update the doc comment to also reference the new `GetAllMonitors()` method, not just `GetActiveMonitors()`.

---

### `src/RigToggle.Core/Abstractions/IMonitorController.cs` (interface, request-response)

**Analog:** itself.

**Current full file** (lines 1-17):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Monitor enumeration and CCD-level disable/restore contract. Implemented by
/// RigToggle.Windows.WindowsMonitorController. Read methods (GetActiveMonitors,
/// CaptureState) are real starting Phase 2; mutating methods (Disable, Restore)
/// are real starting Phase 4 (02-RESEARCH.md Pattern 1; 04-RESEARCH.md Patterns 1/2/3/4).
/// </summary>
public interface IMonitorController
{
    IReadOnlyList<MonitorInfo> GetActiveMonitors();
    MonitorState CaptureState();
    void Disable(string monitorDevicePath);
    void Restore(MonitorState previousState);
}
```

**Pattern to apply** (per 06-RESEARCH.md's Primary Recommendation): add `GetAllMonitors()`, replace `Disable(string)` with `DeactivateMonitors(IReadOnlySet<string>)`, add `ActivateMonitors(IReadOnlySet<string>)`, keep `Restore(MonitorState)` and `CaptureState()` unchanged in signature. Keep the XML-doc convention of naming which RESEARCH pattern each method implements — every existing method doc already does this; the new methods should cite `06-RESEARCH.md Pattern 2/3` the same way.

**Convention note:** every implementation of this interface across the codebase (`WindowsMonitorController` real, `FakeMonitorController` test double in `RigToggle.Tests/Doubles/FakeControllers.cs`) must be updated in lockstep — a signature change here breaks both call sites; see Shared Patterns below.

---

### `src/RigToggle.Windows/WindowsMonitorController.cs` (Windows adapter, CRUD+batch CCD mutation)

**Analog:** itself — `Disable()` is the direct 1→N analog for `DeactivateMonitors`, and `Restore()`'s Extend crash-recovery fallback is the direct analog for `ActivateMonitors`.

**`GetActiveMonitors()` — analog for the new `GetAllMonitors()`** (lines 38-56):
```csharp
public IReadOnlyList<MonitorInfo> GetActiveMonitors()
{
    PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);
    var result = new List<MonitorInfo>();

    foreach (PathInfo path in activePaths)
    {
        foreach (PathTargetInfo targetInfo in path.TargetsInfo)
        {
            PathDisplayTarget target = targetInfo.DisplayTarget;
            result.Add(new MonitorInfo(
                DevicePath: target.DevicePath,
                FriendlyName: target.FriendlyName ?? "(unknown display)",
                IsPrimary: path.IsGDIPrimary));
        }
    }

    return result;
}
```
`GetAllMonitors()` swaps `PathInfo.GetActivePaths(...)` for `PathInfo.GetAllPaths(...)`, filters targets to `t.DisplayTarget.IsAvailable` (inactive-path targets throw on unguarded property access — 06-RESEARCH.md Pattern 3/Pitfall inside Pitfall 1), and guards `IsGDIPrimary` behind `path.IsModeInformationAvailable` before reading it (throws `MissingModeException` otherwise on an inactive path). Also adds `IsActive: targetInfo.IsPathActive` to the constructed `MonitorInfo`.

**`Disable(string)` — direct analog for `DeactivateMonitors(IReadOnlySet<string>)`** (lines 99-177, full method — this is the method to generalize):
```csharp
public void Disable(string monitorDevicePath)
{
    PathInfo[] currentPaths = PathInfo.GetActivePaths(virtualModeAware: false);
    _originalPathsCache = currentPaths; // unchanged: cache BEFORE mutation

    PathInfo? targetPath = currentPaths.FirstOrDefault(p =>
        p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == monitorDevicePath));

    if (targetPath is null)
    {
        throw new InvalidOperationException(
            $"Configured monitor '{monitorDevicePath}' is not currently active.");
    }

    PathInfo[] survivors = currentPaths.Where(p => p != targetPath).ToArray();

    if (survivors.Length == 0)
    {
        throw new InvalidOperationException(
            $"Cannot disable '{monitorDevicePath}' — it is currently the only active " +
            "display. Connect and enable another display before switching to Rig Mode.");
    }

    PathInfo[] pathsToApply;
    if (targetPath.IsGDIPrimary)
    {
        // Shift ALL survivors by the same uniform delta (not just the promoted one) so
        // relative layout is preserved — Position has no public setter, so a fresh
        // PathInfo must be constructed per survivor.
        Point promoted = survivors[0].Position;
        var delta = new Point(-promoted.X, -promoted.Y);

        pathsToApply = survivors
            .Select(p => new PathInfo(
                p.DisplaySource,
                new Point(p.Position.X + delta.X, p.Position.Y + delta.Y),
                p.Resolution,
                p.PixelFormat,
                p.TargetsInfo))
            .ToArray();
    }
    else
    {
        pathsToApply = survivors;
    }

    PathInfo.ApplyPathInfos(pathsToApply, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false);

    // Verify-and-throw against a fresh re-query — never trust ApplyPathInfos's
    // non-throwing return alone as proof of success.
    PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);

    bool targetStillActive = verifyPaths
        .SelectMany(p => p.TargetsInfo)
        .Any(t => t.DisplayTarget.DevicePath == monitorDevicePath);

    bool exactlyOnePrimary = verifyPaths.Count(p => p.IsGDIPrimary) == 1;

    if (targetStillActive || !exactlyOnePrimary)
    {
        throw new InvalidOperationException(
            $"Monitor disable did not take effect as expected (targetStillActive={targetStillActive}, " +
            $"exactlyOnePrimary={exactlyOnePrimary}). No further automatic recovery is attempted (D-05).");
    }
}
```
**Generalization to `DeactivateMonitors(IReadOnlySet<string>)`:** replace the single `targetPath`/`FirstOrDefault` lookup with a `targets = currentPaths.Where(p => p.TargetsInfo.Any(t => monitorDevicePaths.Contains(t.DisplayTarget.DevicePath)))` filter (06-RESEARCH.md Pattern 1, already includes the exact adapted code); the "not currently active" guard generalizes from a null-check to an `Except(...)`-based missing-set check; `anyTargetWasPrimary = targets.Any(t => t.IsGDIPrimary)` replaces the single `targetPath.IsGDIPrimary` check; the verify-and-throw's `targetStillActive` becomes `!` `Intersect` is empty against the N-set. **Do not** add gap-closing/repositioning logic beyond the existing uniform-shift-on-primary-removal idiom (06-RESEARCH.md Pattern 1's closing note — D-01 explicitly scoped this out).

**`Restore()`'s Extend fallback — direct analog for `ActivateMonitors(IReadOnlySet<string>)`** (lines 263-282, the load-bearing reusable mechanism):
```csharp
// Step 2: PathInfo.ApplyTopology(Extend) — a single built-in CCD topology switch with NO
// manually-supplied path/mode structs at all, so none of the three failure modes above
// are even reachable. Brings the previously-disabled monitor back to SOME active state;
// exact position/arrangement don't matter yet — Step 3 corrects that.
try
{
    PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);
}
catch (Exception ex)
{
    throw new InvalidOperationException(
        $"Monitor restore failed while switching to Extend topology: {ex.Message}", ex);
}
```
`ActivateMonitors` reuses this exact call (`allowPersistence: false`) — do **not** manually reconstruct `PathTargetInfo`/mode info for the newly-activated target (06-RESEARCH.md Anti-Patterns; this exact codebase already tried that and hit three rig-tested validation failures per this file's own `Restore()` doc comment, lines 234-246). Full `ActivateMonitors` shape is in 06-RESEARCH.md Pattern 2 (Code Examples), built from this exact fallback plus a fresh availability guard (mirrors `Restore()`'s own Step 1, lines 250-261) and a verify-and-throw re-query (mirrors `Disable()`'s verify-and-throw idiom above).

**Ordering constraint (critical, non-obvious — 06-RESEARCH.md Pitfall 2):** `ActivateMonitors(enableSet)` must run **before** `DeactivateMonitors(disableSet)` on rig-mode entry, because `ApplyTopology(Extend)` restores the CCD persistence database's last-known extend layout — which still includes the disable-set monitors as active if `DeactivateMonitors`'s `saveToDatabase: false` call (already the existing behavior, line 158/206 area) already ran. On toggle-back, `DeactivateMonitors(enableSet)` (D-02) must run **after** `Restore(snapshot)`, for the same reason (`Restore()`'s own crash-recovery fallback also uses Extend internally).

**Verify-and-throw discipline (Phase 4 D-03/D-04, unchanged, must extend to N):** every mutating method in this file re-queries `PathInfo.GetActivePaths(...)` after `ApplyPathInfos`/`ApplyTopology` and throws `InvalidOperationException` on mismatch — never trusts a non-throwing return alone, never uses `Screen.AllScreens` as the oracle. The new bounding-box overlap check (06-RESEARCH.md Code Examples, `AnyOverlap`) slots into this same verify-and-throw pattern, reusing `System.Drawing.Rectangle` (already in scope via `WindowsDisplayAPI`'s `Point`/`Size` usage — no new `using`).

---

### `src/RigToggle.Core/ToggleService.cs` (service, event-driven orchestration)

**Analog:** itself.

**`IsFullyConfigured` — D-07 target** (lines 176-180):
```csharp
private static bool IsFullyConfigured(Models.AppSettings settings) =>
    !string.IsNullOrEmpty(settings.MonitorDevicePath)
    && !string.IsNullOrEmpty(settings.NormalAudioDeviceId)
    && !string.IsNullOrEmpty(settings.RigAudioDeviceId)
    && !string.IsNullOrEmpty(settings.CompanionAppPath);
```
**Pattern to apply:** replace the `MonitorDevicePath` non-empty check with `(settings.MonitorsToDisable?.Count > 0 || settings.MonitorsToEnable?.Count > 0)` (D-07's OR-not-AND generalization) — keep the three unchanged conjuncts (audio × 2, companion app) exactly as-is.

**Single `"Monitor"` `TryExecuteStep` call site** (line 82, inside `ToggleToRigMode`):
```csharp
if (!TryExecuteStep("Monitor", () => _monitorController.Disable(settings.MonitorDevicePath!), steps))
```
**Pattern to apply:** the lambda body becomes a two-call closure — `ActivateMonitors` then `DeactivateMonitors` (ordering per the Windows-adapter section above) — but the `TryExecuteStep("Monitor", ..., steps)` wrapper stays exactly one call, preserving the existing `ToggleResult` per-step (not per-sub-action) reporting granularity (Phase 5 precedent, `06-CONTEXT.md` Integration Points). `TryExecuteStep`'s try/catch/Trace-log/append-`ToggleStepResult` structure (lines 136-154) needs no changes — it already treats its `Action action` parameter opaquely.

**`ToggleToNormalMode`'s monitor restore block** (lines 246-296): unchanged `Restore(snapshot.Monitor)` call stays as the disable-set restore mechanism; per 06-RESEARCH.md's Architecture Diagram, a new `_monitorController.DeactivateMonitors(enableSet)` call is added **after** this block (D-02's unconditional enable-set teardown), reusing the exact same try/catch/`ToggleStepResult`-append shape already used for the monitor-restore try/catch (lines 248-263) — do not introduce a third bespoke error-handling shape.

**Set-equality helper precedent** — `MonitorStateUnchanged` (lines 164-165) is the existing example of "don't use `==`/reference equality on a non-record-equality member; use an explicit comparison helper instead":
```csharp
private static bool MonitorStateUnchanged(Models.MonitorState before, Models.MonitorState after) =>
    before.TargetDevicePath == after.TargetDevicePath && before.Paths.SequenceEqual(after.Paths);
```
This is the direct precedent for Pitfall 4's `HashSet<string>.SetEquals` fix needed in `SettingsForm.BtnSaveSettings_Click` (see below) — same "don't trust default equality on a collection-typed member" lesson, same file's own established convention.

---

### `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` (persistence, file-I/O)

**Analog:** itself.

**`Load()` — migration target, degrade-gracefully pattern to preserve** (lines 27-51):
```csharp
public AppSettings Load()
{
    if (!File.Exists(_path))
    {
        return new AppSettings();
    }

    try
    {
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
    }
    catch (JsonException)
    {
        return new AppSettings();
    }
    catch (IOException)
    {
        return new AppSettings();
    }
}
```
**Pattern to apply (D-08, per 06-RESEARCH.md Code Examples / Pitfall 6):** insert the migration step **inside** the `try` block, after the `JsonSerializer.Deserialize` line succeeds, before `return`:
```csharp
var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();

if (!string.IsNullOrEmpty(loaded.MonitorDevicePath)
    && (loaded.MonitorsToDisable is null || loaded.MonitorsToDisable.Count == 0))
{
    loaded.MonitorsToDisable = new List<string> { loaded.MonitorDevicePath };
}

return loaded;
```
Critical: this must live inside the existing `try`, not as a separate pass after `Load()` returns — a corrupted-but-partially-legible legacy file must still hit the existing `JsonException`/`IOException` degrade-to-fresh-`AppSettings()` path (Pitfall 6), and migration must not introduce a second, divergent failure mode. `Save()` (lines 53-64, atomic temp-file-then-`File.Move` write) needs no changes — it already serializes whatever shape `AppSettings` has.

---

### `src/RigToggle.App/SettingsForm.cs` + `.Designer.cs` (component, request-response form)

**Analog:** itself — `PopulateAudioCombo`/`ShowStaleWarning` are the direct per-row/per-field analogs the new grid's stale-handling should reuse (D-10 precedent, generalized).

**`PopulateMonitorPicker` — the exact method being replaced by the D-03 grid** (lines 59-117): shows the existing "unhook event handler around DataSource assignment," "empty-state disables the control," and "saved-but-not-found → `ShowStaleWarning`" idioms that the new `dgvMonitors` population logic must reproduce per-row instead of per-combo.

**`ShowStaleWarning` — reusable helper, generalize to accept a list per D-06's "no truncation" convention** (lines 203-209):
```csharp
private static void ShowStaleWarning(ErrorProvider errProvider, Control control, Label warningLabel, string noun)
{
    string message = $"Previously selected {noun} not found — please reselect.";
    errProvider.SetError(control, message);
    warningLabel.Text = message;
    warningLabel.Visible = true;
}
```
06-UI-SPEC.md's "Stale saved-monitor warning" copy (`"Previously configured monitor(s) not currently detected: \"X\", \"Y\" — settings preserved; reconnect the display to manage it here."`) is the generalized replacement text — this warning becomes **non-blocking** (does not call `errProvider.SetError`/disable Save), a deliberate divergence from this helper's current blocking behavior; document that divergence inline (06-UI-SPEC.md Grid Spec § Stale saved-monitor handling).

**`ValidateSettingsForm` — gate-priority pattern to extend** (lines 211-219):
```csharp
private void ValidateSettingsForm()
{
    bool monitorOk = cboMonitor.SelectedItem is PickerItem;
    bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
    bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
    bool appPathOk = IsValidLaunchTarget(txtAppPath.Text);

    btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;
}
```
Replace `monitorOk` with the two-gate D-05/D-07 predicate chain from 06-UI-SPEC.md's Validation contract (§ Grid Spec): DISPLAY-06 `WouldLeaveAtLeastOneMonitorActive(...)` gate first (06-RESEARCH.md Code Examples has the exact predicate), then the non-empty gate, in that priority order for which single `lblMonitorWarning` message wins — `audioNormalOk`/`audioRigOk`/`appPathOk` stay unchanged.

**`BtnSaveSettings_Click` — D-02 confirmation-skip-reset pattern, must generalize per Pitfall 4** (lines 308-311):
```csharp
// D-02: reset the durable confirmation-skip flag whenever the configured
// monitor changes, so a fresh named confirmation is forced for the new
// display; preserve the prior value when the monitor is unchanged.
bool monitorChanged = _settings.MonitorDevicePath != monitorItem.Id;
```
**Pattern to apply:** replace with `HashSet<string>.SetEquals` comparison against both new plural sets (order-independent — see `ToggleService.MonitorStateUnchanged` above for this codebase's existing "don't trust default/reference equality on a collection member" convention):
```csharp
bool monitorsChanged =
    !new HashSet<string>(_settings.MonitorsToDisable ?? new()).SetEquals(mergedDisableSet)
    || !new HashSet<string>(_settings.MonitorsToEnable ?? new()).SetEquals(mergedEnableSet);
```

**Constructor event-wiring pattern to extend** (lines 40-44):
```csharp
this.Load += SettingsForm_Load;
cboMonitor.SelectedIndexChanged += OnPickerChanged;
cboAudioNormal.SelectedIndexChanged += OnPickerChanged;
cboAudioRig.SelectedIndexChanged += OnPickerChanged;
```
`dgvMonitors.CurrentCellDirtyStateChanged` and `dgvMonitors.CellValueChanged` are added here per 06-UI-SPEC.md's exact D-04 wiring recipe (Pitfall 5) — `cboMonitor.SelectedIndexChanged -= OnPickerChanged` (line 81, the "unhook around population" idiom) has a direct grid analog: guard the `CellValueChanged` handler's programmatic-uncheck write with a reentrancy flag (per 06-UI-SPEC.md) rather than unhook/rehook, since the same handler both drives D-04's mutual exclusivity and calls `ValidateSettingsForm()`.

**Designer.cs pattern:** `SettingsForm.Designer.cs`'s per-control `Location`/`Size`/`Name` assignment block style (e.g. lines 73-96 for `grpMonitor`/`cboMonitor`/`lblMonitorWarning`) is the exact convention to follow for `dgvMonitors`'s declaration — 06-UI-SPEC.md's "Exact layout coordinates" table gives the precise `(x, y)`/`(w, h)` values already computed to preserve every existing gap.

---

### `src/RigToggle.App/MonitorConfirmDialog.cs` + `.Designer.cs` (component, request-response dialog)

**Analog:** itself.

**Current full `.cs` file** (lines 1-22):
```csharp
namespace RigToggle.App
{
    public partial class MonitorConfirmDialog : Form
    {
        public bool DontAskAgain => chkDontAskAgain.Checked;

        public MonitorConfirmDialog(string monitorFriendlyName)
        {
            InitializeComponent();

            lblMessage.Text = $"This will disable \"{monitorFriendlyName}\" (primary). Continue?";
            this.AcceptButton = btnContinue;
            this.CancelButton = btnCancel;
        }
    }
}
```
**Pattern to apply (D-06, exact code given in 06-CONTEXT.md/06-RESEARCH.md/06-UI-SPEC.md, all three agree verbatim):**
```csharp
private static string FormatNames(IReadOnlyList<string> names) =>
    string.Join(", ", names.Select(n => $"\"{n}\""));

var clauses = new List<string>();
if (disableNames.Count > 0) clauses.Add($"disable {FormatNames(disableNames)}");
if (enableNames.Count > 0) clauses.Add($"enable {FormatNames(enableNames)}");
lblMessage.Text = $"This will {string.Join(" and ", clauses)}. Continue?";
```
Constructor signature changes from `(string monitorFriendlyName)` to accepting both lists (e.g. `(IReadOnlyList<string> disableNames, IReadOnlyList<string> enableNames)`) — the "pure display data, no Core interface injected" doc-comment convention (line 4-7 of the current file, citing 04-RESEARCH.md Pattern 5) is preserved unchanged; the caller (`MainForm`) still resolves names before constructing.

**Designer.cs:** `lblMessage` grows from `(360, 48)` to `(360, 72)`; every control below shifts `+24px`; title `Text` changes from `"Disable Monitor?"` to `"Confirm Monitor Changes?"` — exact values in 06-UI-SPEC.md's Confirmation Dialog Spec table.

---

### `src/RigToggle.App/MainForm.cs` (component, request-response)

**Analog:** itself — confirmation call site.

**Current call site** (lines 80-102):
```csharp
var settings = _settingsStore.Load();
if (!settings.SkipMonitorConfirmation)
{
    var monitor = _monitorController.GetActiveMonitors()
        .FirstOrDefault(m => m.DevicePath == settings.MonitorDevicePath);
    string name = monitor?.FriendlyName ?? "the configured monitor";

    using var confirmDialog = new MonitorConfirmDialog(name);
    if (confirmDialog.ShowDialog(this) != DialogResult.OK)
    {
        return; // user cancelled — nothing mutated
    }

    if (confirmDialog.DontAskAgain)
    {
        settings.SkipMonitorConfirmation = true;
        _settingsStore.Save(settings);
    }
}
```
**Pattern to apply:** `_monitorController.GetActiveMonitors()` → `_monitorController.GetAllMonitors()` (critical — per 06-RESEARCH.md Pattern 3's closing note, an enable-set monitor is inactive by definition at confirm-time and cannot resolve via `GetActiveMonitors()`). Resolve both `disableNames`/`enableNames` lists from `settings.MonitorsToDisable`/`MonitorsToEnable` against the `GetAllMonitors()` result, falling back to the raw device path or a generic placeholder if a name can't resolve (same `?? "the configured monitor"` defensive idiom already used here). `MonitorConfirmDialog` constructor call updates to the two-list signature. The rest of the block (`ShowDialog`/`DontAskAgain`/`Save`) is unchanged.

---

### `src/RigToggle.Tests/Doubles/FakeControllers.cs` (test double)

**Analog:** itself — `FakeMonitorController`.

**Current shape** (lines 12-70): implements `IMonitorController` with a shared `_callLog`, deterministic fake data (`"\\\\?\\DISPLAY#FAKE"`), and constructor flags (`throwOnDisable`, `mutatesBeforeThrowingOnDisable`) to drive specific `ToggleServiceTests` scenarios. **Pattern to apply:** when `IMonitorController` gains `GetAllMonitors()`/`ActivateMonitors()`/`DeactivateMonitors()`, add matching fake methods following the exact same "append a label to `_callLog`, return deterministic fake data, optionally throw based on a constructor flag" convention — e.g. `_callLog.Add($"monitor.DeactivateMonitors:{string.Join(",", monitorDevicePaths)}")`. Do not add a mocking framework (explicit project convention, doc comment lines 6-10).

---

### `src/RigToggle.Tests/JsonStoreTests.cs` / `ToggleServiceTests.cs` (tests)

**Analog:** itself.

**`JsonStoreTests.cs` round-trip pattern** (lines 46-72, `SettingsStore_Save_ThenLoad_RoundTripsAllFields`) is the template for a new migration acceptance test: write a genuine v1.0-shape JSON literal directly to the temp path (bypassing `Save()`, which would always write the current shape) containing only `MonitorDevicePath`/`MonitorFriendlyName` and no plural fields, call `Load()`, and assert `MonitorsToDisable` contains exactly the legacy path while `MonitorsToEnable` is empty — 06-RESEARCH.md's Code Examples section already specifies this exact acceptance-test shape.

**`ToggleServiceTests.cs`'s `ConfiguredSettings`/`CreateService` fixture pattern** (lines 28-64) is the template for updating the "fully configured" fixture to also set `MonitorsToDisable`/`MonitorsToEnable`, and for adding a new D-07 test proving `IsSettingsConfigured()` returns `true` for an enable-only configuration (empty `MonitorsToDisable`, non-empty `MonitorsToEnable`).

---

## Shared Patterns

### Verify-and-throw after every mutating CCD call (Phase 4 D-03/D-04)
**Source:** `src/RigToggle.Windows/WindowsMonitorController.cs`, both `Disable()` (lines 160-176) and `Restore()` (lines 336-350, plus the fast-path equivalent lines 213-231).
**Apply to:** `ActivateMonitors`, `DeactivateMonitors` — every new/generalized mutating method in this file must re-query `PathInfo.GetActivePaths(...)` after mutation and throw `InvalidOperationException` with a descriptive message on any mismatch. Never use `Screen.AllScreens` as the oracle (D-04). Never attempt automatic rollback on verification failure (D-05) — the exception bubbles to `MainForm`'s existing catch block.
```csharp
PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);
bool targetStillActive = verifyPaths.SelectMany(p => p.TargetsInfo)
    .Any(t => t.DisplayTarget.DevicePath == monitorDevicePath);
bool exactlyOnePrimary = verifyPaths.Count(p => p.IsGDIPrimary) == 1;
if (targetStillActive || !exactlyOnePrimary)
{
    throw new InvalidOperationException(
        $"Monitor disable did not take effect as expected (...). No further automatic recovery is attempted (D-05).");
}
```

### Stable `DevicePath` as the only monitor identity key
**Source:** every model/controller file in this phase already keys exclusively on `DevicePath` (`MonitorInfo.DevicePath`, `MonitorPathSnapshot.DevicePath`, `AppSettings.MonitorDevicePath`).
**Apply to:** `MonitorsToDisable`/`MonitorsToEnable` (both `List<string>` of `DevicePath`), all lookup/matching logic in `WindowsMonitorController`, `SettingsForm`'s grid rows, `MonitorConfirmDialog`'s name resolution. Never key by index or grid row position (explicitly banned — `.planning/REQUIREMENTS.md` Out of Scope table, already burned once in v1.0).

### Degrade-gracefully, never throw, on settings read
**Source:** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs` `Load()` (lines 27-51) — catches `JsonException`/`IOException`, returns fresh `AppSettings()`.
**Apply to:** the D-08 migration step must live inside this existing try/catch, not add a second failure mode (Pitfall 6). `SettingsForm.PopulateMonitorPicker`'s existing `try { ... } catch (Exception) { monitors = Array.Empty<MonitorInfo>(); }` (lines 65-73) is the UI-layer sibling of this same discipline and should be reused verbatim for `GetAllMonitors()` calls.

### XML-doc "explain why, not what" convention, especially for intentional asymmetries
**Source:** `ToggleService.cs`'s class-level doc comment (lines 6-18) explicitly documents why `ToggleToRigMode` is stop-on-first-failure while `ToggleToNormalMode` is isolate-and-continue, warning future readers not to "fix" it into false symmetry.
**Apply to:** D-02's new asymmetry (enable-set always unconditionally re-disables on toggle-back vs. disable-set's snapshot-based restore) must get the same explicit "this is intentional, do not correct" doc-comment treatment in `WindowsMonitorController`/`ToggleService`, matching the existing pattern rather than leaving the asymmetry unexplained.

### Set-equality via `HashSet<T>.SetEquals`, not `!=`/reference comparison, on collection-typed settings fields
**Source:** `ToggleService.MonitorStateUnchanged` (lines 164-165) — already solves the identical problem for `MonitorState.Paths` (an `IReadOnlyList<T>` whose record-generated equality falls back to reference equality).
**Apply to:** `SettingsForm.BtnSaveSettings_Click`'s generalized `monitorsChanged` check (Pitfall 4) — use `HashSet<string>.SetEquals`, order-independent, exactly as this precedent already establishes for a different (but structurally identical) collection-equality problem in the same codebase.

### Unhook-event-handler-around-programmatic-write idiom
**Source:** `SettingsForm.PopulateMonitorPicker`/`PopulateAudioCombo` (lines 81, 116, 142, 176) — `combo.SelectedIndexChanged -= OnPickerChanged` before a `DataSource`/`SelectedItem` assignment, re-hooked after.
**Apply to:** the new `dgvMonitors.CellValueChanged` handler's own programmatic write (unchecking the sibling column per D-04) needs the equivalent protection — 06-UI-SPEC.md recommends a reentrancy flag instead of unhook/rehook here specifically because the same handler also drives `ValidateSettingsForm()`, but the underlying "don't let a programmatic write re-trigger the same handler" principle is identical to this existing convention.

---

## No Analog Found

None — every file this phase touches already exists in the codebase with a single-target/single-item version of the exact pattern being generalized to N. See File Classification table above; all 13 files/file-groups have an exact (self) analog.

## Metadata

**Analog search scope:** `src/RigToggle.Core/`, `src/RigToggle.Windows/`, `src/RigToggle.App/`, `src/RigToggle.Tests/`, `src/RigToggle.Windows.Tests/` (entire repository — small codebase, exhaustive read).
**Files scanned:** 30 (`.cs` files across all five projects, listed via `find`); 13 read in full or by targeted section for this pattern map (all files this phase's CONTEXT.md/RESEARCH.md name as in-scope, plus `AudioState.cs`/`AudioRoleState.cs`/`WindowsAudioController.cs`/`IAudioController.cs` as the two-related-items precedent, plus `ToggleServiceTests.cs`/`WindowsMonitorControllerTests.cs` for test-pattern precedent).
**Pattern extraction date:** 2026-07-28
