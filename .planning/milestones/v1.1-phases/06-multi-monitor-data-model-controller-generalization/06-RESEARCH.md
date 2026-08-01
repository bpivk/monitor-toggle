# Phase 6: Multi-Monitor Data Model & Controller Generalization - Research

**Researched:** 2026-07-28
**Domain:** Windows CCD (Connecting and Configuring Displays) multi-target topology mutation via `WindowsDisplayAPI`; WinForms multi-select settings UI; JSON settings schema migration
**Confidence:** MEDIUM-HIGH (data model/UI/migration: HIGH, verified against live source code; CCD combined-topology sequencing: MEDIUM — reasoned from official Microsoft docs + this repo's own rig-tested precedent code, but not itself rig-tested; final go/no-go per ROADMAP.md remains the mandatory rig-validation checkpoint)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** A monitor in the enable-set that's currently OS-disabled gets activated via auto-extend placement at its native/preferred resolution — the same "let CCD's Extend-topology mechanism decide placement" approach `WindowsMonitorController.Restore`'s crash-recovery fallback already uses (`PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, ...)`). No manual position/resolution configuration UI.
- **D-02:** On toggle-back, every enable-set monitor is unconditionally returned to OS-disabled — not routed through the general "restore whatever the snapshot says" mechanism. Deliberately asymmetric from the disable-set's snapshot-based restore.
- **D-03:** Settings' monitor section becomes a `DataGridView`-style grid: one row per enumerated monitor (friendly name), with two checkbox columns, "Disable" and "Enable" — replacing the single `cboMonitor` dropdown.
- **D-04:** A monitor cannot be checked in both columns simultaneously — enforced live in the UI (checking one column for a row automatically prevents/unchecks the other for that same row), not just caught at Save time.
- **D-05:** DISPLAY-06's "don't allow disabling every monitor" check counts enable-set monitors as "staying active" — the real check is "will at least one monitor be active once the rig-mode topology is fully applied" (disable-set removed, enable-set added), not just "is every currently-active monitor in the disable-set."
- **D-06:** The confirmation dialog (DISPLAY-07) always spells out every affected monitor's full friendly name in a comma-separated list — no truncation/"and N more" logic. E.g. `This will disable "Dell U2720Q", "LG UltraGear" and enable "Rig Monitor". Continue?`
- **D-07:** `IsFullyConfigured` (`ToggleService.cs`) no longer requires a non-empty disable-set specifically — it requires disable-set **OR** enable-set to be non-empty, plus both audio devices and the companion app path as before.
- **D-08:** The v1.0 → v1.1 settings migration (`AppSettings.MonitorDevicePath` → the new `MonitorsToDisable` set) is fully silent — no dialog, no toast, no one-time banner.

### Claude's Discretion

- Exact migration mechanism (in `JsonSettingsStore.Load()` itself, vs. a separate migration step in the composition root).
- Exact `DataGridView` column/control configuration (checkbox column types, row height, sizing) to achieve D-03/D-04.
- Whether `MonitorState`/`AppSettings` represent the enable-set as a `List<string>` of device paths directly or a small wrapper type.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. No manual position/arrangement UI was pursued past D-01's explicit "auto-extend is good enough" decision.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DISPLAY-04 | User can configure a set of monitors to disable when entering rig mode (not limited to one) | Data model section (`MonitorsToDisable: List<string>`), generalized `DeactivateMonitors` controller method, DataGridView grid pattern |
| DISPLAY-05 | User can configure a set of monitors to enable when entering rig mode (e.g. a rig monitor normally kept OS-disabled) | **Critical gap found:** existing `GetActiveMonitors()` cannot enumerate OS-disabled monitors — new `GetAllMonitors()` method required (see Pitfall 1). `ActivateMonitors` controller method using `ApplyTopology(Extend)`, sequencing analysis answering the load-bearing question |
| DISPLAY-06 | Settings prevents saving a configuration that would disable every monitor | D-05 validation predicate (Code Examples), reusing live `GetAllMonitors()`/currently-active data already on the form |
| DISPLAY-07 | The pre-disable confirmation dialog names every monitor being disabled and enabled | `MonitorConfirmDialog` multi-name formatting pattern, friendly-name resolution against the new all-monitors enumeration (not just active) |
| DISPLAY-08 | A user upgrading from v1.0 keeps their previously-configured single monitor working automatically | Migration pattern in `JsonSettingsStore.Load()`, acceptance-test shape against a genuine v1.0 JSON fixture |
</phase_requirements>

## Summary

This phase generalizes a proven, twice rig-validated single-monitor CCD disable/restore mechanism (`WindowsMonitorController` from Phase 4/v1.0) to two independently-configurable N-monitor sets, without introducing any new third-party dependency — `WindowsDisplayAPI` 1.3.0.13 already exposes everything needed (`PathInfo.GetAllPaths()`, `ApplyPathInfos()`, `ApplyTopology(Extend)`). The data-model and UI work (plural settings fields, `DataGridView` grid, migration, validation, confirmation wording) is low-risk and can be planned with HIGH confidence directly from the existing, already-generalized-for-N-paths code in `WindowsMonitorController.CaptureState`/`Restore`.

The one genuinely load-bearing unknown — combining disable-set removal and enable-set activation "in one operation" (ROADMAP.md's completion-gate wording) — resolves to a **sequenced, two-native-call strategy inside a single new `IMonitorController` method**, not a single raw `SetDisplayConfig`/`ApplyPathInfos` call that both adds and removes targets at once. Microsoft's own documentation for `SDC_TOPOLOGY_EXTEND` ("the caller requests the **last extended configuration from the persistence database**") combined with this codebase's existing `Disable()` call using `saveToDatabase: false` produces a critical ordering constraint: **`ApplyTopology(Extend)` must run before disable-set removal, never after** — otherwise Extend would restore the DB's last-known extend layout, which still includes the disable-set monitors as active (since disabling them was never persisted to the database), silently undoing the disable. This is a new, non-obvious finding not present in the existing Phase 4 research and must be designed in explicitly, not left as an implementation detail.

A second load-bearing gap this research surfaces: **`IMonitorController.GetActiveMonitors()` cannot see OS-disabled monitors at all** (it wraps `PathInfo.GetActivePaths()`). DISPLAY-05's entire premise — picking a monitor "normally kept OS-disabled" from a list — is unsatisfiable with the current interface. A new `GetAllMonitors()`-style method wrapping `PathInfo.GetAllPaths()` (filtered to `IsAvailable` targets) is required, and both `SettingsForm`'s new grid and `MainForm`'s confirmation-dialog name resolution must use it instead of (or alongside) `GetActiveMonitors()`.

**Primary recommendation:** Replace `IMonitorController.Disable(string)`/`Restore(MonitorState)` with three focused methods — `GetAllMonitors()` (enumeration, active+inactive), `ActivateMonitors(IReadOnlySet<string>)` (wraps `ApplyTopology(Extend)`, used only for the enable-set), and a generalized `DeactivateMonitors(IReadOnlySet<string>)` (the existing `Disable()` survivor-repositioning logic, extended to remove N targets and reused for both the disable-set on rig-mode entry *and* the enable-set teardown on toggle-back per D-02). `ToggleService.ToggleToRigMode` calls `ActivateMonitors` then `DeactivateMonitors` inside one `TryExecuteStep("Monitor", ...)` closure; `ToggleToNormalMode` calls `Restore` then `DeactivateMonitors` on the enable-set. Keep `Restore(MonitorState)` for the disable-set/survivor restore path, generalized to verify N target device paths instead of one.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Monitor enumeration (active + inactive) | Windows adapter (`RigToggle.Windows`) | Core (`IMonitorController` contract) | Only the CCD wrapper can see OS-disabled-but-connected targets; Core stays a pure interface, no `WindowsDisplayAPI` reference (existing constraint, unchanged) |
| Disable-set/enable-set persistence | Core (`AppSettings`, `JsonSettingsStore`) | — | Plain JSON file, no external service; matches existing `%LocalAppData%\RigToggle\settings.json` pattern |
| Combined topology mutation (activate enable-set, remove disable-set) | Windows adapter (`WindowsMonitorController`) | Core (`IMonitorController` contract, orchestration in `ToggleService`) | Native CCD calls must live in the Windows adapter (existing constraint); sequencing/ordering logic is itself part of the adapter, not `ToggleService`, since it depends on CCD-specific behavior (Extend's DB-restore semantics) that Core must never know about |
| Settings validation (DISPLAY-06) | App UI (`SettingsForm`) | Core (defensive re-check possible in `ToggleService`) | Primary check belongs at Save-time in the UI (D-05's exact wording is a UI-facing rule); a defensive re-check in the controller before mutation is good practice but not required by CONTEXT.md |
| Confirmation dialog wording (DISPLAY-07) | App UI (`MainForm` + `MonitorConfirmDialog`) | — | Pure presentation; no Core interface needed (matches existing 04-RESEARCH.md Pattern 5 — `MonitorConfirmDialog` takes display strings only, never a controller reference) |
| Settings migration (DISPLAY-08) | Core (`JsonSettingsStore.Load()` or composition root) | — | Pure data transformation on the already-existing degrade-gracefully `Load()` path; no Windows API involvement |

## Standard Stack

### Core
No new third-party dependency is required for this phase. Everything needed already exists in the currently-referenced packages.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 `[VERIFIED: nuget registry — confirmed current/latest via api.nuget.org/v3-flatcontainer/windowsdisplayapi/index.json, matches CLAUDE.md and the existing `RigToggle.Windows.csproj` reference]` | `PathInfo.GetAllPaths()` (new capability this phase needs), `ApplyPathInfos()`, `ApplyTopology(Extend)` — all already in use by the existing `WindowsMonitorController` | Same library already proven twice on this rig (Phase 1 spike, Plan 01 re-test); no reason to add a second display library |
| `System.Windows.Forms.DataGridView` | Included in .NET 10 WinForms SDK `[VERIFIED: BCL — part of `UseWindowsForms=true`, no NuGet reference needed]` | D-03's multi-select grid | Built-in; no charting/grid third-party package needed for a 2-3 row personal-rig monitor list |
| `System.Text.Json` | Included in .NET 10 BCL | Migration deserialization of legacy `MonitorDevicePath` field alongside new plural fields | Already the project's settings-persistence mechanism (`JsonSettingsStore`) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `DataGridView` two-checkbox-column grid (D-03, locked) | `CheckedListBox` ×2 (one for disable, one for enable) | Rejected by the user's own decision (D-03) — grid ties both sets visually to the same monitor list, which two independent list boxes don't |
| Sequenced `ActivateMonitors` → `DeactivateMonitors` (this research's recommendation) | Single mixed `ApplyPathInfos` call with some `PathInfo` entries carrying full mode info and others with `ModeInfoIdx = Invalid` (theoretically valid per MS docs) | Theoretically supported by `SDC_USE_SUPPLIED_DISPLAY_CONFIG`'s documented "invalid mode index lets best-mode-logic fill it in" behavior, but this exact pattern (manually constructing `PathTargetInfo` for a previously-inactive target) is the one this codebase already tried and abandoned after **three separate rig-tested validation failures** (see `WindowsMonitorController.Restore`'s own doc comment, lines 234-246) — not worth re-attempting without new evidence it would behave differently this time |

**Installation:** No `npm install`/`dotnet add package` changes required — both `WindowsDisplayAPI` and `NAudio` are already referenced at the versions needed.

**Version verification:** `WindowsDisplayAPI` confirmed 1.3.0.13 is the newest published version via direct NuGet flat-container query (`curl https://api.nuget.org/v3-flatcontainer/windowsdisplayapi/index.json`) — no newer release exists to consider upgrading to.

## Package Legitimacy Audit

Not applicable — this phase introduces **zero new packages**. No `slopcheck`/registry verification needed; both dependencies in play (`WindowsDisplayAPI` 1.3.0.13, `NAudio` 2.3.0, neither newly added) were already vetted and pinned in prior phases (see CLAUDE.md's Sources section and `04-RESEARCH.md`).

## Architecture Patterns

### System Architecture Diagram

```
SettingsForm (grid, D-03/D-04)
   │ Save
   ▼
JsonSettingsStore.Save(AppSettings{ MonitorsToDisable, MonitorsToEnable, ... })
   │
   │ (next toggle)
   ▼
ToggleService.ToggleToRigMode()
   │
   ├─► IMonitorController.CaptureState()          (unchanged — captures NORMAL-mode
   │                                                topology; disable-set active,
   │                                                enable-set NOT present as active
   │                                                paths by definition)
   ├─► SnapshotStore.Save(...)                     (before any mutation, unchanged)
   │
   └─► "Monitor" step (ONE TryExecuteStep, TWO native calls inside):
         │
         ├─► IMonitorController.ActivateMonitors(enableSet)
         │      │  skip entirely if every enableSet member already active
         │      ▼
         │   PathInfo.ApplyTopology(Extend, allowPersistence:false)
         │      │  restores LAST DB-SAVED extend layout — must run BEFORE
         │      │  disable-set removal (see Pitfall 2)
         │      ▼
         │   re-query GetActivePaths(); verify-and-throw each enableSet
         │   device path is now active
         │
         └─► IMonitorController.DeactivateMonitors(disableSet)
                │  generalized Disable() — reuses live active PathInfo/
                │  TargetsInfo wholesale, removes disableSet targets,
                │  uniform-shifts survivors iff a removed target was
                │  GDI-primary (existing Pattern 1, N-generalized)
                ▼
             PathInfo.ApplyPathInfos(survivors, saveToDatabase:false)
                │
                ▼
             re-query; verify-and-throw: no disableSet path active,
             exactly one GDI primary, no survivor bounding-box overlap

ToggleService.ToggleToNormalMode()
   │
   ├─► IMonitorController.Restore(snapshot.Monitor)     (existing two-path
   │                                                      mechanism, N-generalized
   │                                                      verify-and-throw)
   └─► IMonitorController.DeactivateMonitors(enableSet)   (D-02: unconditional
                                                            re-disable, same
                                                            primitive reused)
```

### Recommended Project Structure

No new files/folders — this phase modifies existing files in place:

```
src/RigToggle.Core/
├── Models/AppSettings.cs          # + MonitorsToDisable, MonitorsToEnable (List<string>)
├── Models/MonitorInfo.cs          # + IsActive bool (needed to render grid state)
├── Abstractions/IMonitorController.cs  # GetActiveMonitors -> GetAllMonitors;
│                                        # Disable/Restore -> ActivateMonitors/
│                                        # DeactivateMonitors/Restore
├── Persistence/JsonSettingsStore.cs    # + silent migration in Load()
└── ToggleService.cs                # IsFullyConfigured (D-07), new Monitor step shape

src/RigToggle.Windows/
└── WindowsMonitorController.cs     # GetAllMonitors, ActivateMonitors, DeactivateMonitors
                                     # (generalized Disable), Restore (N-generalized verify)

src/RigToggle.App/
├── SettingsForm.cs / .Designer.cs  # cboMonitor -> DataGridView grid (D-03/D-04)
├── MainForm.cs                     # confirmation dialog call site: resolve names via
│                                    # GetAllMonitors, pass both sets
└── MonitorConfirmDialog.cs         # multi-name formatting (D-06)
```

### Pattern 1: Generalizing `Disable()` into `DeactivateMonitors(IReadOnlySet<string>)`

**What:** The existing `Disable(string monitorDevicePath)` already contains all the logic needed for N-target removal — it just needs its single-path lookup/comparison generalized to a set.
**When to use:** Both the rig-mode-entry disable-set removal AND the toggle-back enable-set teardown (D-02) — one primitive, two call sites.
**Example (adapted from existing `WindowsMonitorController.Disable`, `src/RigToggle.Windows/WindowsMonitorController.cs` lines 99-177):**
```csharp
// Source: this repo, src/RigToggle.Windows/WindowsMonitorController.cs (read directly)
public void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths)
{
    if (monitorDevicePaths.Count == 0) return; // no-op, e.g. enable-only config on toggle-back

    PathInfo[] currentPaths = PathInfo.GetActivePaths(virtualModeAware: false);
    _originalPathsCache = currentPaths; // unchanged: cache BEFORE mutation

    PathInfo[] targets = currentPaths
        .Where(p => p.TargetsInfo.Any(t => monitorDevicePaths.Contains(t.DisplayTarget.DevicePath)))
        .ToArray();

    // Generalized "not currently active" guard — was previously a single not-found check.
    var missing = monitorDevicePaths.Except(
        currentPaths.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath));
    if (missing.Any())
        throw new InvalidOperationException($"Configured monitor(s) not currently active: {string.Join(", ", missing)}");

    PathInfo[] survivors = currentPaths.Where(p => !targets.Contains(p)).ToArray();

    if (survivors.Length == 0)
        throw new InvalidOperationException("Cannot disable all configured monitors — at least one active display must remain.");

    // Unchanged uniform-shift idiom — reposition ALL survivors iff ANY removed target
    // was GDI-primary, promoting the first survivor to (0,0).
    bool anyTargetWasPrimary = targets.Any(t => t.IsGDIPrimary);
    PathInfo[] pathsToApply = anyTargetWasPrimary
        ? UniformShiftToOrigin(survivors)   // same delta-shift construction as today
        : survivors;

    PathInfo.ApplyPathInfos(pathsToApply, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false);

    // Verify-and-throw, generalized: none of the N device paths still active, exactly
    // one GDI primary, AND (new) no bounding-box overlap among survivors.
}
```
**Note:** Existing v1.0 `Disable()` never attempted to "close the gap" left by removing a non-primary monitor from a multi-monitor layout — it only ever shifts everything when the removed target *was* primary. This generalizes cleanly and should NOT be extended into gap-closing logic for the multi-target case — that's new scope D-01 explicitly didn't ask for ("Windows' own default placement is good enough").

### Pattern 2: `ActivateMonitors` via `ApplyTopology(Extend)` — the load-bearing sequencing answer

**What:** Answers the research prompt's central question directly.
**Sub-question 1 — can `ApplyPathInfos()` do combined add+remove in one call?** Theoretically yes per Microsoft's docs (a `DISPLAYCONFIG_PATH_MODE_IDX_INVALID` mode index lets best-mode-logic fill in a path's mode), but this codebase already tried the closest equivalent (manually constructing target/mode info for a previously-inactive path) and hit three separate rig-tested validation failures (see `Restore()`'s own doc comment). **Recommendation: do not attempt this.** Use the two-step `ApplyTopology(Extend)` + `ApplyPathInfos` sequence instead — already proven, and it's what D-01 explicitly names as the mechanism to reuse.
**Sub-question 2 — what does a freshly-activated path need?** Nothing manually constructed. `ApplyTopology(Extend)` takes **zero path/mode arguments at all** (`SetDisplayConfig(0, null, 0, null, ...)`) — the OS does 100% of the mode/position selection using the CCD **persistence database's last-known extend layout** for currently-available targets `[CITED: learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig — "SDC_TOPOLOGY_EXTEND: The caller requests the last extended configuration from the persistence database"]`. This matches — and explains *why* — the existing `Restore()` crash-recovery fallback already avoids manually building `PathTargetInfo`/mode structs for the reactivated target.
**Sub-question 3 (ordering) — the actual load-bearing pitfall:** See Pitfall 2 below. Extend must run **before**, never after, disable-set removal.
**Example:**
```csharp
// Source: this repo (existing Restore() crash-recovery fallback, lines 263-282) +
// learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig
public void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths)
{
    if (monitorDevicePaths.Count == 0) return;

    PathInfo[] currentActive = PathInfo.GetActivePaths(virtualModeAware: false);
    var currentlyActivePaths = currentActive
        .SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath).ToHashSet();

    // Optimization + pitfall avoidance (see Pitfall 3): skip Extend entirely if every
    // target is already active — Extend recomputes the WHOLE topology from the DB
    // record and can incidentally reposition unrelated already-correct displays.
    if (monitorDevicePaths.All(currentlyActivePaths.Contains)) return;

    // Early availability guard (mirrors Restore()'s Step 1) — confusing generic error
    // otherwise if a configured enable-set monitor is physically unplugged.
    PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
    var missing = monitorDevicePaths.Where(dp => !allPaths
        .Any(p => p.TargetsInfo.Any(t => t.DisplayTarget.IsAvailable && t.DisplayTarget.DevicePath == dp)));
    if (missing.Any())
        throw new InvalidOperationException($"Cannot enable monitor(s) — not detected: {string.Join(", ", missing)}");

    PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence: false);

    // Verify-and-throw (D-03/D-04 discipline, unchanged): re-query, confirm every
    // enable-set device path is now active.
    PathInfo[] postExtend = PathInfo.GetActivePaths(virtualModeAware: false);
    var stillInactive = monitorDevicePaths.Except(
        postExtend.SelectMany(p => p.TargetsInfo).Select(t => t.DisplayTarget.DevicePath));
    if (stillInactive.Any())
        throw new InvalidOperationException(
            $"Monitor enable did not take effect: {string.Join(", ", stillInactive)}. No further automatic recovery is attempted (D-05, 04-CONTEXT.md).");
}
```

### Pattern 3: `GetAllMonitors()` — the enumeration gap

**What:** `IMonitorController.GetActiveMonitors()` wraps `PathInfo.GetActivePaths()` and structurally cannot return an OS-disabled monitor. DISPLAY-05 requires picking exactly that kind of monitor from a list. This is a new capability, not a rename.
**Pitfall inside the pitfall:** `PathDisplayTarget.FriendlyName`/`DevicePath` getters throw `TargetNotAvailableException` when `!IsAvailable` — must filter `IsAvailable` first (same filter `PathDisplayTarget.GetDisplayTargets()` already applies internally). `PathInfo.IsGDIPrimary` reads `Position`, which throws `MissingModeException` when `!IsModeInformationAvailable` (true for inactive paths) — must guard with `path.IsModeInformationAvailable && path.IsGDIPrimary`, never call `.IsGDIPrimary` unconditionally on an inactive path.
**Example:**
```csharp
// Source: this repo (adapted from existing GetActiveMonitors, generalized to
// PathInfo.GetAllPaths()) + WindowsDisplayAPI/DisplayConfig/PathDisplayTarget.cs
// (IsAvailable-gated property getters, confirmed by direct source read)
public IReadOnlyList<MonitorInfo> GetAllMonitors()
{
    PathInfo[] allPaths = PathInfo.GetAllPaths(virtualModeAware: false);
    var result = new List<MonitorInfo>();

    foreach (PathInfo path in allPaths)
    {
        bool isPrimary = path.IsModeInformationAvailable && path.IsGDIPrimary; // guard!
        foreach (PathTargetInfo targetInfo in path.TargetsInfo.Where(t => t.DisplayTarget.IsAvailable))
        {
            PathDisplayTarget target = targetInfo.DisplayTarget;
            result.Add(new MonitorInfo(
                DevicePath: target.DevicePath,
                FriendlyName: target.FriendlyName ?? "(unknown display)",
                IsPrimary: isPrimary,
                IsActive: targetInfo.IsPathActive));
        }
    }

    return result;
}
```
`MainForm`'s existing confirmation-dialog friendly-name lookup (`_monitorController.GetActiveMonitors().FirstOrDefault(...)`) must switch to this method too — otherwise an enable-set monitor's name can never resolve (it's inactive at confirm-time by definition), and DISPLAY-07's "names every monitor" requirement silently degrades to "the configured monitor" for every enable-set entry.

### Anti-Patterns to Avoid

- **Manually reconstructing `PathTargetInfo`/mode info for a previously-inactive target:** already tried and abandoned in this exact codebase (three rig-tested failures). Reuse `ApplyTopology(Extend)` + live re-query instead (Pattern 2).
- **Calling `GetActiveMonitors()` (active-only) anywhere the enable-set needs to be displayed, selected, or name-resolved:** structurally cannot see the monitors DISPLAY-05 is about. Always use the new `GetAllMonitors()` for anything touching the enable-set.
- **Treating "one operation" (ROADMAP.md's completion-gate wording) as a literal single native `SetDisplayConfig` call:** CONTEXT.md's own D-01 already commits to reusing the Extend-based mechanism, which is inherently multi-call. "One operation" is best read as *one logical `IMonitorController` method invocation / one `ToggleResult` "Monitor" step*, not one syscall — consistent with Integration Points already documented in 06-CONTEXT.md ("the disable+enable combination happens inside one controller call").

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Activating a previously-OS-disabled monitor at a sane position/resolution | Manual `PathTargetInfo`/mode-info construction, manual source assignment | `PathInfo.ApplyTopology(DisplayConfigTopologyId.Extend, allowPersistence:false)` | Already proven in this codebase; manual construction already failed three times on real rig hardware for the equivalent single-monitor case |
| Detecting overlapping monitor rectangles for the verify-and-throw step | Nothing external needed — this is genuinely simple enough to hand-roll (axis-aligned rectangle intersection, ~10 lines) | Plain `Rectangle.IntersectsWith` (`System.Drawing.Rectangle`, already referenced via `WindowsDisplayAPI`'s `Point`/`Size` usage) | Trivial geometry; no library needed, and `Rectangle` is already in scope via `System.Drawing` |
| DataGridView checkbox column "commit immediately on click" behavior | A custom control or manual mouse-hit-testing | `CurrentCellDirtyStateChanged` + `dgv.CommitEdit(DataGridViewDataErrorContexts.Commit)`, then handle `CellValueChanged` | Documented, standard WinForms idiom for this exact problem — see Pitfall 5 |

**Key insight:** every genuinely hard problem in this phase (CCD activation of an inactive target) already has a proven, in-repo, rig-validated solution one level up in the same file. The work here is generalization (1→N) and sequencing (order of two already-proven calls), not new native-API discovery.

## Common Pitfalls

### Pitfall 1: `GetActiveMonitors()` cannot enumerate OS-disabled monitors — DISPLAY-05's UI is unsatisfiable without a new method
**What goes wrong:** If `SettingsForm`'s new grid is populated from the existing `GetActiveMonitors()`, a rig monitor "normally kept OS-disabled to save power" (D-05's own example) never appears as a selectable row.
**Why it happens:** `GetActiveMonitors()` wraps `PathInfo.GetActivePaths()`, which by definition excludes inactive paths.
**How to avoid:** Add `GetAllMonitors()` (wraps `PathInfo.GetAllPaths()`, filtered to `IsAvailable`) and use it for the Settings grid and for `MainForm`'s confirmation-dialog name resolution. Keep `GetActiveMonitors()` for anywhere only currently-active monitors are relevant (e.g. `CaptureState`'s existing usage, which is correct as-is).
**Warning signs:** A rig monitor that's currently powered off/OS-disabled simply doesn't show up in the new grid at all — silent, not an error, easy to miss in code review.

### Pitfall 2: Calling `ApplyTopology(Extend)` *after* disable-set removal re-activates the monitors you just disabled
**What goes wrong:** `SDC_TOPOLOGY_EXTEND` "requests the last extended configuration **from the persistence database**" `[CITED: learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig]`. The existing `Disable()`/generalized `DeactivateMonitors()` calls `ApplyPathInfos(..., saveToDatabase: false)` — the disable is deliberately **not** persisted to that database. If `ActivateMonitors` (Extend) runs *after* `DeactivateMonitors`, Extend restores the DB's last-known layout, which still includes the just-disabled monitor(s) as active — silently undoing the disable.
**Why it happens:** Extend is a whole-topology restore from a persisted record, not an incremental "turn on just this one path" operation — this is easy to misread as the latter given `ApplyTopology`'s minimal (zero-argument) call signature.
**How to avoid:** Always sequence `ActivateMonitors(enableSet)` **before** `DeactivateMonitors(disableSet)` on rig-mode entry. On toggle-back, `DeactivateMonitors(enableSet)` (D-02's unconditional teardown) should run **after** `Restore(snapshot)` for the same reason — Restore's own crash-recovery fallback also uses Extend internally, so any manual enable-set teardown must come after it, not before.
**Warning signs:** Rig-testing the "combined disable+enable topology" gate (ROADMAP.md's own completion criterion) shows the "disabled" monitor still lit up after the operation completes.

### Pitfall 3: `ApplyTopology(Extend)` can reposition monitors that were never part of either set
**What goes wrong:** Extend recomputes the *entire* topology from the DB record, not just the newly-added target — a "stay as-is" third monitor (in neither the disable-set nor enable-set) can end up repositioned as a side effect.
**Why it happens:** Same root cause as Pitfall 2 — Extend has no concept of "only touch this one path."
**How to avoid:** Skip the `ActivateMonitors` call entirely when every enable-set member is already active (cheap live pre-check) to avoid unnecessary Extend calls. When Extend genuinely must run, treat any resulting repositioning of untouched monitors as an accepted, pre-existing tradeoff (this phase is entering rig mode, not restoring exact prior state, so there is no "exact position" contract for untouched monitors to violate) — but this is exactly the kind of thing the ROADMAP.md rig-validation gate ("no position overlap") exists to catch empirically.
**Warning signs:** After a combined operation, a monitor that wasn't configured in either set has moved or changed primary status unexpectedly.

### Pitfall 4: Set-equality confirmation-skip reset (04-CONTEXT.md D-02 precedent) needs generalizing, not just copy-pasting
**What goes wrong:** `SettingsForm.BtnSaveSettings_Click` currently does `bool monitorChanged = _settings.MonitorDevicePath != monitorItem.Id;` (single string comparison) to decide whether to reset `SkipMonitorConfirmation`. A naive port to the plural fields (e.g. comparing `List<string>` reference or using `!=`) will not detect reordering-only or genuinely-equal-but-different-instance changes correctly.
**Why it happens:** `List<string>` has no value equality by default; `!=` compares references.
**How to avoid:** Use `HashSet<string>.SetEquals` (order-independent, matches "is this the same configured set" semantics) for both `MonitorsToDisable` and `MonitorsToEnable` when deciding whether to reset the skip-confirmation flag.
**Warning signs:** Reordering the same two monitors in the grid (unlikely with checkboxes, but reachable if the underlying list serialization order differs) incorrectly resets — or a real change incorrectly fails to reset — the "don't ask again" flag.

### Pitfall 5: DataGridView checkbox columns don't commit on single click by default
**What goes wrong:** A `DataGridViewCheckBoxColumn` cell's `Value` doesn't update until the cell loses focus (user clicks elsewhere) — so D-04's "checking one column automatically unchecks the sibling column in the same row" handler, if wired to `CellValueChanged`, won't fire on the first click.
**Why it happens:** Standard, well-documented WinForms `DataGridView` behavior — checkbox edits are deferred until commit.
**How to avoid:** Handle `CurrentCellDirtyStateChanged`, and inside it call `dgv.CommitEdit(DataGridViewDataErrorContexts.Commit)` to force an immediate commit, which then fires `CellValueChanged` on the same click.
**Warning signs:** In manual testing, checking "Disable" for a row doesn't immediately uncheck "Enable" for that row — it only updates after clicking a different cell.

### Pitfall 6: Migration silence (D-08) must not accidentally also silently drop the legacy fields' data on a corrupted/partial read
**What goes wrong:** `JsonSettingsStore.Load()` already degrades to a fresh `AppSettings()` on `JsonException`/`IOException` (existing SETTINGS-04 behavior). If the migration logic is added as a *separate* pass that assumes `Load()` always succeeds, a genuinely-corrupted v1.0 file could throw before migration ever runs, losing the "keeps working automatically" guarantee for the (admittedly rare) case of a corrupted-but-partially-legible legacy file.
**Why it happens:** Migration and corruption-handling are two independent concerns touching the same method; easy to reason about them separately and miss the interaction.
**How to avoid:** Keep the migration step *inside* the existing try/catch degrade-gracefully block in `Load()` (per D-08's Claude's-Discretion note, this is the natural home) — migrate as one of the last steps before returning, after successful deserialization, so it inherits the existing corruption handling for free rather than needing its own.
**Warning signs:** A hand-edited or slightly-malformed genuine v1.0 `settings.json` that used to load fine (degrading some fields to null) now fails differently after migration logic is added.

## Code Examples

### D-05 validation predicate (DISPLAY-06)
```csharp
// Source: this repo, synthesized from 06-CONTEXT.md D-05's exact wording — "will at
// least one monitor be active once the rig-mode topology is fully applied"
private bool WouldLeaveAtLeastOneMonitorActive(
    IReadOnlyList<MonitorInfo> allMonitors, // from GetAllMonitors()
    HashSet<string> monitorsToDisable,
    HashSet<string> monitorsToEnable)
{
    bool anySurvivingActiveMonitor = allMonitors
        .Any(m => m.IsActive && !monitorsToDisable.Contains(m.DevicePath));

    return anySurvivingActiveMonitor || monitorsToEnable.Count > 0;
}
```

### Migration (D-08), inside `JsonSettingsStore.Load()`
```csharp
// Source: this repo, src/RigToggle.Core/Persistence/JsonSettingsStore.cs (read
// directly) — migration inserted after successful deserialization, before return.
var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();

// D-08: silent migration — a genuine v1.0-era file has MonitorDevicePath set and
// MonitorsToDisable null/empty (the new field didn't exist yet, so it deserializes
// to its default). No dialog, no toast — just map it forward.
if (!string.IsNullOrEmpty(loaded.MonitorDevicePath)
    && (loaded.MonitorsToDisable is null || loaded.MonitorsToDisable.Count == 0))
{
    loaded.MonitorsToDisable = new List<string> { loaded.MonitorDevicePath };
}

return loaded;
```
Acceptance-test shape (extends the existing `JsonStoreTests.cs` pattern in `src/RigToggle.Tests/`): write a genuine v1.0-shape JSON literal (only the legacy singular fields present, no plural fields at all) to a temp file, `Load()` it, assert `MonitorsToDisable` contains exactly the legacy path and `MonitorsToEnable` is empty.

### Confirmation dialog multi-name formatting (D-06)
```csharp
// Source: this repo, src/RigToggle.App/MonitorConfirmDialog.cs (existing single-name
// version read directly) — generalized per D-06's exact example format.
private static string FormatNames(IReadOnlyList<string> names) =>
    string.Join(", ", names.Select(n => $"\"{n}\""));

// D-06 asymmetric wording: only mention "disable"/"enable" clauses that are non-empty.
var clauses = new List<string>();
if (disableNames.Count > 0) clauses.Add($"disable {FormatNames(disableNames)}");
if (enableNames.Count > 0) clauses.Add($"enable {FormatNames(enableNames)}");
lblMessage.Text = $"This will {string.Join(" and ", clauses)}. Continue?";
```

### Bounding-box overlap check (verify-and-throw generalization)
```csharp
// Source: this repo, synthesized — System.Drawing.Rectangle already in scope via
// WindowsDisplayAPI's own Point/Size usage in PathInfo.
private static bool AnyOverlap(IReadOnlyList<PathInfo> activePaths)
{
    var rects = activePaths
        .Where(p => p.IsModeInformationAvailable)
        .Select(p => new Rectangle(p.Position, p.Resolution))
        .ToList();

    for (int i = 0; i < rects.Count; i++)
        for (int j = i + 1; j < rects.Count; j++)
            if (rects[i].IntersectsWith(rects[j]))
                return true;

    return false;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Single `MonitorDevicePath` field, single `Disable(string)`/`Restore(MonitorState)` pair | Plural `MonitorsToDisable`/`MonitorsToEnable` sets, `ActivateMonitors`/`DeactivateMonitors`/`Restore` triad | This phase (v1.1 Phase 6) | Every later trigger path (tray, hotkey, CLI — Phases 8-10) inherits the new shapes; this is why ROADMAP.md sequences this phase first in the v1.1 milestone |

**Deprecated/outdated:** None — `WindowsDisplayAPI` 1.3.0.13 remains the current release; no CCD API changes between Windows versions affect this phase's mechanism.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ApplyTopology(Extend)`'s "last extended configuration from the persistence database" will, in practice on this rig, include a never-before-manually-extended enable-set monitor via Windows' documented "best topology/mode logic" fallback (used "if that topology had never been set before") | Pattern 2 / Pitfall 2 | If the fallback doesn't behave as documented on this specific AMD/DisplayPort rig hardware, a brand-new enable-set monitor might not activate on the first try — must be covered by the mandatory rig-validation checkpoint (ROADMAP.md gate (a)/(b)) before this phase is considered complete |
| A2 | The bounding-box overlap check (Code Examples) is a sufficient automated proxy for the ROADMAP.md gate's "no position overlap" requirement | Common Pitfalls / Code Examples | The rig-validation checkpoint remains the authoritative go/no-go gate regardless — this automated check is a defensive addition, not a replacement for the human-verified rig test |
| A3 | Skipping `ActivateMonitors`'s `ApplyTopology(Extend)` call entirely when every enable-set member is already active is safe and doesn't skip some other needed refresh | Pattern 2 (optimization) | Low risk — this mirrors the existing codebase's own "don't call CCD mutation APIs unless something needs to change" discipline (e.g. `Disable()`'s pre-mutation guards), but should be confirmed during rig testing that a no-op path truly produces no observable topology change |

**If this table is empty:** N/A — see above.

## Open Questions (RESOLVED)

1. **Should `IMonitorController.Restore(MonitorState)` also perform its own defensive overlap/all-active-check, or is that only needed on the forward (`ActivateMonitors`/`DeactivateMonitors`) path?**
   - What we know: `Restore()` already has its own verify-and-throw (position/primary match against the snapshot), which structurally can't produce an overlap if the snapshot itself never had one (since restore reproduces exact prior positions).
   - What's unclear: whether `DeactivateMonitors(enableSet)` running *after* `Restore()` (D-02's teardown) could theoretically introduce a fresh overlap if repositioning is needed (e.g. an enable-set monitor became GDI-primary during rig mode and needs to be un-primaried on the way out).
   - Recommendation: reuse the same generalized `DeactivateMonitors` verify-and-throw (which already includes the overlap check) for this call site — no separate logic needed, just confirm the planner wires `DeactivateMonitors` into both call sites, not a divergent teardown-only variant.
   - **RESOLVED:** Implemented in `06-03-PLAN.md` Task 2 — `DeactivateMonitors`'s verify-and-throw (including the overlap check) is reused at both call sites, per gsd-plan-checker's verification pass.

2. **Should the Settings grid show a per-row "currently OS-disabled" indicator or hint text, even though CONTEXT.md doesn't mandate a specific UI treatment?**
   - What we know: `MonitorInfo.IsActive` (new field, Pattern 3) makes this information available.
   - What's unclear: CONTEXT.md's Claude's-Discretion section leaves exact grid presentation to the planner/executor.
   - Recommendation: surface `IsActive` in the row (e.g. `"LG UltraGear (currently OS-disabled)"` in the friendly-name column) — cheap, directly answers "why would I check Enable for this row," and reuses the existing `D-10`-style informational-label convention already established in `SettingsForm`.
   - **RESOLVED:** Locked in `06-UI-SPEC.md` (approved by gsd-ui-checker) and implemented in `06-04-PLAN.md` — the recommended row-suffix treatment was adopted.

3. **How should a stale saved device path (in either set, no longer enumerated by `GetAllMonitors()`) be surfaced, given the grid can only show rows for currently-enumerated monitors?**
   - What we know: the existing single-`ComboBox` pattern (D-10, Phase 2) shows "previously selected monitor not found" via an inline warning label when the saved ID doesn't match any enumerated item. A grid has no analogous "unselected but remembered" row state — a monitor either has a row (and can be checked) or doesn't exist in the grid at all.
   - What's unclear: whether to (a) silently drop the stale device path from the set on next Save (simplest, but silently discards a user's prior configuration for a monitor that's merely disconnected, e.g. rig PC powered off), or (b) show a shared warning label listing all currently-unmatched saved device paths (consistent with the existing no-truncation, full-name-listing convention from D-06).
   - Recommendation: (b) — generalize the existing `ShowStaleWarning` helper to accept a list and reuse D-06's "always list every name, no truncation" convention for consistency; do not silently drop saved-but-disconnected monitors from the persisted set (a temporarily-unplugged rig monitor should not lose its configuration).
   - **RESOLVED:** Locked in `06-UI-SPEC.md` (approved, non-blocking on Save per its documented rationale) and implemented in `06-04-PLAN.md` — option (b) was adopted.

## Environment Availability

Skipped for the CCD-mutation portions of this phase — no automated test harness in this repository can exercise real `PathInfo.ApplyTopology`/`ApplyPathInfos` calls (confirmed by `WindowsMonitorControllerTests.cs`'s own doc comment: "Restore()/Disable() themselves are NOT unit-tested here... they remain verified only via live rig testing"). This is unchanged from Phase 4 and is why ROADMAP.md mandates the dedicated rig-validation checkpoint as this phase's completion gate rather than treating it as follow-up hardening. The development/research environment for this session is Linux and cannot build or run `net10.0-windows` targets at all — all CCD-specific findings in this document are derived from direct source-code reading (`WindowsDisplayAPI` cloned locally) and official Microsoft documentation, not from execution.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Windows 10/11 + WDDM driver | All CCD mutation calls (`ActivateMonitors`, `DeactivateMonitors`, `Restore`) | ✗ (this research session, Linux) | — | None — rig-validation checkpoint is mandatory before this phase can be marked complete, per ROADMAP.md |
| `dotnet` SDK (net10.0-windows target) | Building/testing `RigToggle.Windows`/`RigToggle.Windows.Tests` | ✗ (this research session) | — | Non-CCD unit tests (data model, migration, validation predicate, confirmation-dialog formatting) can and should be covered by `RigToggle.Tests` (cross-platform, net10.0), which does not require Windows |

**Missing dependencies with no fallback:**
- Windows/WDDM for CCD mutation testing — this is expected and matches the existing Phase 4 precedent; not a gap introduced by this research.

## Security Domain

This phase has no network-facing surface, no authentication/authorization concerns, and no new external input beyond what already exists (a local, hand-editable `settings.json`). ASVS categories mapped for completeness per the project's proportional-security posture (single-user personal desktop tool, no elevation, per CLAUDE.md's explicit "keeps the tool asInvoker" constraint):

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Single-user local desktop tool, no accounts |
| V3 Session Management | No | N/A |
| V4 Access Control | No | N/A |
| V5 Input Validation | Yes (lightweight) | `System.Text.Json` deserialization of a hand-editable local file; already degrades gracefully on malformed JSON (`JsonSettingsStore.Load()`, existing SETTINGS-04 behavior) — migration logic must preserve this, not bypass it (Pitfall 6) |
| V6 Cryptography | No | No secrets/credentials involved in this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Hand-edited `settings.json` containing a malformed or oversized `MonitorsToDisable`/`MonitorsToEnable` array | Tampering (self-tampering only — local single-user file, not attacker-controlled in any realistic threat model for this tool) | Existing `JsonException`/`IOException` degrade-to-fresh-settings behavior already covers this; no new validation needed beyond what deserialization already enforces |

## Sources

### Primary (HIGH confidence)
- This repository, read directly: `src/RigToggle.Windows/WindowsMonitorController.cs`, `src/RigToggle.Core/Abstractions/IMonitorController.cs`, `src/RigToggle.Core/Models/{AppSettings,MonitorState,MonitorInfo,MonitorPathSnapshot}.cs`, `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.Core/Persistence/JsonSettingsStore.cs`, `src/RigToggle.App/{SettingsForm.cs,SettingsForm.Designer.cs,MainForm.cs,MonitorConfirmDialog.cs}`, `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs`, `src/RigToggle.Tests/{JsonStoreTests.cs,Doubles/FakeControllers.cs}`
- `WindowsDisplayAPI` source, cloned locally at `/tmp/wdapi/WindowsDisplayAPI` (commit `ecb08f8`, matches published 1.3.0.13 per NuGet), read directly: `DisplayConfig/PathInfo.cs`, `DisplayConfig/PathTargetInfo.cs`, `DisplayConfig/PathDisplayTarget.cs`, `DisplayConfig/PathDisplaySource.cs`, `Native/DisplayConfig/SetDisplayConfigFlags.cs`, `Native/DisplayConfig/DisplayConfigTopologyId.cs`
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig — fetched directly; authoritative source for `SDC_TOPOLOGY_EXTEND` = "last extended configuration from the persistence database" (the load-bearing finding behind Pitfall 2), `SDC_USE_SUPPLIED_DISPLAY_CONFIG`/`DISPLAYCONFIG_PATH_MODE_IDX_INVALID` mode-index semantics
- `api.nuget.org/v3-flatcontainer/windowsdisplayapi/index.json` — queried directly; confirms 1.3.0.13 is the latest published version, no upgrade to evaluate
- `.planning/phases/06-multi-monitor-data-model-controller-generalization/06-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md`, `.planning/ROADMAP.md` (Phase 6 section), `.planning/milestones/v1.0-phases/04-monitor-control-production/04-CONTEXT.md` — all read directly

### Secondary (MEDIUM confidence)
- WebSearch cross-referencing DisplayLink forum / OSR developer community threads on `ApplyTopology(Extend)` behavior — used only to corroborate (not as primary source for) the official MS docs finding; the MS docs page itself is treated as primary

### Tertiary (LOW confidence)
- None used as a basis for any claim in this document — all CCD-specific claims trace to either direct source-code reads or the official Microsoft `SetDisplayConfig` documentation page.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, existing versions verified against live NuGet registry
- Architecture (data model, UI, migration): HIGH — directly derived from reading the exact files this phase modifies; `CaptureState`/`Restore` are already N-generalized in the existing code, only `Disable`'s single-target lookup needs generalizing
- Architecture (combined CCD sequencing — Pitfall 2/Pattern 2): MEDIUM — reasoned rigorously from official Microsoft documentation plus this repo's own rig-tested precedent, but not itself executed on real hardware in this research session (Linux environment, no CCD API access). This is exactly the class of finding the phase's mandatory rig-validation checkpoint (ROADMAP.md) exists to confirm or refute before the phase can be marked complete.
- Pitfalls: HIGH for data-model/UI pitfalls (directly observable in existing code), MEDIUM for the two CCD-specific pitfalls (documented Windows behavior, not yet rig-confirmed for this exact combined-operation scenario)

**Research date:** 2026-07-28
**Valid until:** 2026-08-27 (30 days — stable API surface, no fast-moving dependencies; re-verify NuGet versions if planning is delayed past this window)
