# Phase 4: Monitor Control (Production) - Research

**Researched:** 2026-07-24
**Domain:** Windows CCD (Connecting and Configuring Displays) API topology mutation — specifically, removing and restoring the GDI-primary display path, via `WindowsDisplayAPI` (falahati) 1.3.0.13, already in use in this codebase
**Confidence:** HIGH — the core blocking question (how to reposition the surviving monitor to (0,0) when removing the primary path) is resolved via direct source read of the exact library version already in this project, with a concrete public-API construction path confirmed. MEDIUM on the exact behavior of inactive-path mode-info on restore (Microsoft docs are explicit but this project's specific AMD/DisplayPort hardware combination has not been re-tested against the *new* repositioning code — only the spike's naive code was tested).

## Summary

The spike's blocker — `PathInfo.ApplyPathInfos()` throwing `PathChangeException` when removing the primary monitor's path — has a direct fix reachable entirely through `WindowsDisplayAPI`'s **public** API surface. No raw P/Invoke of `SetDisplayConfig`/`QueryDisplayConfig` is needed. `WindowsDisplayAPI.DisplayConfig.PathInfo` has a public constructor overload — `PathInfo(PathDisplaySource displaySource, Point position, Size resolution, DisplayConfigPixelFormat pixelFormat, PathTargetInfo[] pathTargetInfos)` — confirmed by direct source read of `PathInfo.cs` (the exact 1.3.0.13-era file, same version already referenced in this project's `.csproj`). `Position` itself has no public setter (confirmed), but you don't need one: construct a *brand-new* `PathInfo` object for the surviving display, reusing its existing `DisplaySource`, `Resolution`, `PixelFormat`, and `TargetsInfo` (all public getters, already used elsewhere in `WindowsMonitorController.GetActiveMonitors`), but supplying a new `Point(0, 0)` as the position. Pass this reconstructed array (survivor(s) repositioned, target's path excluded entirely) to `ApplyPathInfos(..., allowChanges: true)`. This is the same class of fix community multi-monitor tools use — shift the coordinate origin, don't just delete a path and hope Windows infers a new one.

For monitor **state persistence** across app restarts (required by CORE-05 — mode must be correctly detected even after a crash while in rig mode, which implies the snapshot must survive a process restart, not just live in memory for one run): `PathInfo` objects themselves cannot be round-tripped through JSON (confirmed by the spike's own code comment: "PathInfo does not deserialize cleanly from its ToString()"). The correct pattern is to persist only **primitive fields** (device path, position X/Y, resolution W/H, pixel format, rotation, scaling, output technology, frequency, scan-line ordering) captured from the live `PathInfo`/`PathTargetInfo` objects at snapshot time, and at restore-time **re-resolve identity objects (`PathDisplaySource`, `PathDisplayTarget`, `PathDisplayAdapter`) live** via a fresh `PathInfo.GetAllPaths()` query matched by the stable `DevicePath` string — never persist or reconstruct the adapter's `LUID` directly from old data, since LUIDs are only valid within a Windows session and can go stale across a driver reload/reboot that might occur while a snapshot file sits on disk. `PathInfo.GetAllPaths()` is confirmed (via official Microsoft `QueryDisplayConfig` documentation) to return inactive-but-connected paths after all active ones, which is exactly what's needed to re-find a CCD-disabled monitor's path at restore time.

**Primary recommendation:** Implement the primary-monitor repositioning fix by constructing new `PathInfo` objects (via the public constructor) with translated positions — shifting the *entire remaining topology* by a delta so the promoted survivor lands at exactly `(0,0)`, not just the survivor in isolation. Change `IMonitorController.CaptureState` to take no monitor-specific parameter (mirroring `IAudioController.CaptureState()`'s Phase-3 precedent) and capture the **full active-path topology** (not just the target monitor), because restoring "exact prior configuration" (DISPLAY-02) requires reverting every path Windows may have repositioned when the primary was removed, not just the one that was disabled. Enrich `MonitorState` into a primitive, JSON-serializable per-path array (position, resolution, pixel format, rotation, scaling, frequency, scan-line ordering, output technology, device path, friendly name, is-primary) rather than trying to preserve live `WindowsDisplayAPI` objects. Verify every mutation via a fresh `PathInfo.GetActivePaths()` re-query (never `Screen.AllScreens`, per spike Finding 2), throwing on mismatch, matching Phase 3's `ApplyAndVerify` precedent exactly.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Display topology mutation (remove/restore primary path) | OS Display Subsystem / GPU driver (WDDM, `SetDisplayConfig` via `WindowsDisplayAPI`) | `RigToggle.Windows.WindowsMonitorController` (constructs the requested topology) | The app decides *what* topology to request (which path to drop, where to reposition survivors); the driver honors or rejects it. Same tier split as Phase 1. |
| Repositioning math (computing the (0,0)-shift delta) | `RigToggle.Windows.WindowsMonitorController` (in-process C#) | — | Pure coordinate arithmetic on data already enumerated; no OS call needed to compute it, only to apply it |
| Verification that mutation took effect | `RigToggle.Windows.WindowsMonitorController` (re-query via `PathInfo.GetActivePaths()`) | OS Display Subsystem (source of truth) | Direct precedent: Phase 3's `WindowsAudioController.ApplyAndVerify` (D-03/D-04 in 03-CONTEXT.md); extended here per this phase's D-03 |
| Snapshot persistence across process restarts | `RigToggle.Core.Persistence.JsonSnapshotStore` (existing, unchanged) | `RigToggle.Core.Models.MonitorState` (new primitive shape) | CORE-05 requires mode detection to survive a crash — this is only possible if `MonitorState` is plain-data JSON, not live COM/CCD handles |
| Confirmation UX before disabling | `RigToggle.App.MainForm` (or a new small dialog Form) | `RigToggle.Core.Models.AppSettings` (persisted "don't ask again" flag) | GUI-tier concern; the "don't ask again" state is user preference data, not display state |

## Project Constraints (from CLAUDE.md)

- **Platform**: Windows only — no cross-platform concerns for this phase.
- **Monitor control**: "Must achieve true OS-level display disable/enable... not merely a monitor power signal" — this phase's entire purpose; the repositioning fix below stays within the same CCD `SetDisplayConfig` mechanism already validated, not a fallback to power-signal APIs.
- **Distribution**: standalone self-contained .exe — no impact on this phase's code (no new NuGet dependency is needed; the fix uses `WindowsDisplayAPI`'s existing public surface).
- **GSD Workflow Enforcement**: file-changing work must route through `/gsd-execute-phase` — applies to the planner/executor, not to this research document.
- No constraint conflicts with CONTEXT.md's decisions.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Confirmation Dialog (DISPLAY-03)**
- D-01: The confirmation dialog is shown once, not on every toggle — with a "don't ask again" checkbox that persists to settings (`AppSettings`/`settings.json`), not a per-toggle MessageBox.
- D-02: The remembered "don't ask again" preference resets automatically if the configured monitor (device path) changes in Settings — the user gets exactly one fresh confirmation naming the newly-configured monitor, never a stale consent silently carried over to a different display.

**Verification Strictness (mirrors Phase 3's audio verify-and-throw pattern)**
- D-03: After `Disable`/`Restore` applies a topology change, re-query the actual resulting display state via `WindowsDisplayAPI` (`PathInfo.GetActivePaths()`) and confirm it matches the expected topology (monitor genuinely gone / genuinely restored). Throw a clear error if it doesn't match — do not trust `ApplyPathInfos()`'s non-throwing return alone as proof of success.
- D-04: `Screen.AllScreens` is NOT the verification oracle for this check — `WindowsDisplayAPI`'s own re-query (`PathInfo.GetActivePaths()`) is authoritative (per spike Finding 2's documented staleness/caching gotcha).

**Failure Path**
- D-05: When Disable/Restore's verification throws, let the exception bubble up through the existing `MainForm` exception handling — same pattern as Phase 3's audio verify-and-throw. No automatic rollback/re-apply attempt on failure. Comprehensive step-by-step failure reporting and recovery is explicitly Phase 5 (CORE-04) scope.

### Claude's Discretion
- The exact mechanism for repositioning the remaining display to (0,0) before removing the primary monitor's path — resolved by this research (see Summary / Pattern 1 below): a new-`PathInfo`-construction approach via `WindowsDisplayAPI`'s public constructor, not raw P/Invoke.
- `MonitorState`'s exact snapshot shape — resolved by this research (see Standard Stack / Code Examples): a primitive, JSON-serializable per-path array, following the spike's "keep the full topology, reapply wholesale" pattern, but adapted for cross-process-restart persistence (which the spike itself never needed to handle).

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. Automatic rollback-on-failure and comprehensive step-by-step failure reporting are correctly deferred to Phase 5 (CORE-04).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DISPLAY-01 | Disable configured primary monitor at true CCD level | Pattern 1 (repositioning-aware `ApplyPathInfos` call) + Pattern 3 (verify-and-throw) — resolves the spike's `PathChangeException` blocker |
| DISPLAY-02 | Restore to exact prior configuration (position, primary designation, orientation) | Pattern 2 (full-topology capture) + Pattern 4 (live-identity-reconstruction restore) — resolves cross-restart persistence of `PathInfo`-derived state |
| DISPLAY-03 | Confirmation dialog before disabling, naming the monitor | Pattern 5 (custom WinForms dialog + persisted "don't ask again" flag with monitor-change reset) |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|---------------|
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 [VERIFIED: already referenced in this repo's `.csproj`; source re-confirmed this session via direct GitHub raw-file read] | CCD topology query/mutation | Same library already in use for `GetActiveMonitors`/spike; no new dependency needed — the primary-repositioning fix is reachable through its existing public constructors |

No new NuGet packages are required for this phase — `WindowsDisplayAPI` is already installed (`RigToggle.Windows.csproj`), and `System.Text.Json` (BCL) already handles `MonitorState` serialization via the existing `JsonSnapshotStore`.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Constructing new `PathInfo` objects via the public constructor to reposition survivors | Raw P/Invoke of `SetDisplayConfig`/`QueryDisplayConfig`/`DisplayConfigGetDeviceInfo` reconstructing `DISPLAYCONFIG_SOURCE_MODE.position` directly (the fallback documented in project STACK.md) | Not needed — direct source read of `PathInfo.cs` (this exact 1.3.0.13-era code) confirms a public constructor overload accepts `(PathDisplaySource, Point position, Size resolution, DisplayConfigPixelFormat, PathTargetInfo[])`, which is sufficient to build a repositioned path without touching native structs. Raw P/Invoke would only be needed if this constructor didn't exist or didn't flow correctly into `ApplyPathInfos`'s internal `GetDisplayConfigPathInfos` (verified it does — `GetDisplayConfigSourceMode()` reads `_position`/`_resolution`/`_pixelFormat`, all of which the public constructor sets). Keep raw P/Invoke as a documented fallback only if empirical testing on the rig shows this constructor path fails in a way `ValidatePathInfos`/`ApplyPathInfos` can't explain. |
| Persisting only primitive fields + live identity re-resolution at restore | Persisting `AdapterId`/`SourceId`/`TargetId` (LUID-based) directly in `MonitorState` and reconstructing `PathDisplaySource`/`PathDisplayTarget` from those stored values without re-querying | LUIDs (`WindowsDisplayAPI.Native.Structures.LUID`, a 64-bit locally-unique identifier) are only guaranteed valid for the current Windows session — after a driver reload, GPU reset, or reboot, a stored LUID may not resolve to the same physical adapter/source, silently corrupting a restore. Always re-deriving identity live via `GetAllPaths()` matched on the target's stable `DevicePath` string (already used elsewhere in this app for settings-matching) avoids this failure mode entirely, at the cost of one extra enumeration call at restore time — negligible for a manual, infrequent toggle action. |

## Package Legitimacy Audit

No new packages installed this phase — `WindowsDisplayAPI` was already vetted and approved in Phase 1's research (Package Legitimacy Audit: `WindowsDisplayAPI`, NuGet, ~6 years old, 152.5K downloads, LGPL-3.0, `github.com/falahati/WindowsDisplayAPI`, used by Lenovo Legion Toolkit and WinDynamicDesktop — "Approved (manual verification)"). No re-audit needed since the version is unchanged.

## Architecture Patterns

### System Architecture Diagram

```
                    ┌───────────────────────────────────────────┐
                    │   MainForm.BtnToggle_Click                  │
                    │   (rig-mode direction only)                 │
                    └───────────────────┬───────────────────────┘
                                        │ settings.SkipMonitorConfirmation == false?
                                        ▼
                    ┌───────────────────────────────────────────┐
                    │  Confirmation Dialog (new, Pattern 5)       │
                    │  "This will disable <FriendlyName>          │
                    │   (primary). Continue?" + checkbox           │
                    │  → on OK: persist SkipMonitorConfirmation    │
                    │    if checked                                 │
                    └───────────────────┬───────────────────────┘
                                        │ confirmed
                                        ▼
                    ┌───────────────────────────────────────────┐
                    │  ToggleService.ToggleToRigMode()             │
                    │   var monitorState =                          │
                    │     _monitorController.CaptureState();  ◄──── Pattern 2: full topology,
                    │   _snapshotStore.Save(...)  (BEFORE mutate)   no param needed
                    │   _monitorController.Disable(devicePath) ◄─── Pattern 1: repositioning-
                    └───────────────────┬───────────────────────┘   aware ApplyPathInfos
                                        ▼
                    ┌───────────────────────────────────────────┐
                    │  WindowsMonitorController.Disable            │
                    │   1. currentPaths = GetActivePaths()          │
                    │   2. targetPath = match by DevicePath          │
                    │   3. survivors = currentPaths minus target     │
                    │   4. if targetPath.IsGDIPrimary:                │
                    │        delta = (0,0) - chosenSurvivor.Position  │
                    │        rebuild survivors with shifted Position │
                    │        (new PathInfo(...) per survivor)          │
                    │   5. ApplyPathInfos(rebuiltSurvivors,             │
                    │        allowChanges: true)                        │
                    │   6. VERIFY: GetActivePaths() re-query            │
                    │        — target absent, exactly one Position     │
                    │        (0,0) exists — else throw (D-03)           │
                    └───────────────────┬───────────────────────┘
                                        │
                    OS Display Subsystem (CCD / WDDM / GPU driver)
                                        │
                                        ▼ (toggle back)
                    ┌───────────────────────────────────────────┐
                    │  WindowsMonitorController.Restore(state)      │
                    │   1. livePaths = GetAllPaths() (incl. inactive)│
                    │   2. for each snapshot entry: match live         │
                    │      PathDisplaySource/PathTargetInfo by         │
                    │      DevicePath (fresh identity, never stale     │
                    │      stored LUIDs)                                │
                    │   3. reconstruct PathInfo per entry using stored │
                    │      Position/Resolution/PixelFormat +            │
                    │      reconstructed PathTargetInfo (stored         │
                    │      Rotation/Scaling/Frequency/ScanLineOrdering) │
                    │   4. ApplyPathInfos(reconstructed, allowChanges)  │
                    │   5. VERIFY: GetActivePaths() re-query — target  │
                    │      present, position/primary match original    │
                    │      — else throw (D-03)                          │
                    └───────────────────────────────────────────┘
```

### Recommended Project Structure

No new files strictly required — all changes land in existing files:
```
src/RigToggle.Core/
├── Abstractions/IMonitorController.cs   # CaptureState() signature change (no param)
├── Models/MonitorState.cs                # reshaped: per-path primitive array
├── Models/MonitorPathSnapshot.cs         # NEW — one record per captured path
src/RigToggle.Windows/
└── WindowsMonitorController.cs           # Disable/Restore/CaptureState real implementations
src/RigToggle.App/
├── MainForm.cs                           # confirmation-dialog call before ToggleToRigMode
├── MonitorConfirmDialog.cs               # NEW — small custom Form (Pattern 5)
└── MonitorConfirmDialog.Designer.cs      # NEW — matches existing *.Designer.cs convention
```

### Pattern 1: Repositioning-aware primary-path removal

**What:** When the path being removed is the current GDI primary (`Position == (0,0)`), don't just drop it from the array — reconstruct every surviving path with a coordinate shift so exactly one survivor lands at `(0,0)`.
**When to use:** Always check `targetPath.IsGDIPrimary` before calling `ApplyPathInfos`; branch only when true (non-primary removal already works per the spike, no reconstruction needed there).
**Example:**
```csharp
// Source: WindowsDisplayAPI 1.3.0.13 PathInfo.cs, direct source read this session
// (github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathInfo.cs)
using System.Drawing;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

PathInfo[] currentPaths = PathInfo.GetActivePaths(virtualModeAware: false);

PathInfo? targetPath = currentPaths.FirstOrDefault(p =>
    p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == monitorDevicePath));

if (targetPath is null)
{
    throw new InvalidOperationException($"Configured monitor '{monitorDevicePath}' is not currently active.");
}

var survivors = currentPaths.Where(p => p != targetPath).ToArray();

PathInfo[] pathsToApply;
if (targetPath.IsGDIPrimary && survivors.Length > 0)
{
    // Promote the first survivor to (0,0); shift ALL survivors by the same delta
    // so their RELATIVE layout (left/right/above/below) is preserved — this is the
    // fix the spike's naive approach was missing (PathInfo.Position has no setter,
    // so a fresh PathInfo must be constructed instead).
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
```
**Note on the "which survivor becomes primary" choice:** For this project's 2-monitor rig, `survivors.Length == 1` after removing the primary, so the choice is forced. The `survivors[0]` fallback documented above generalizes reasonably to N>2 monitors (arbitrary-but-deterministic), which is safe since this project has no requirement to preserve a *specific* new-primary choice beyond "some valid primary exists after disable" — DISPLAY-01's success criterion is about the *target* monitor being gone, not about which remaining monitor becomes primary.

### Pattern 2: Full-topology capture (not just the target monitor)

**What:** `CaptureState` must snapshot **every** currently active path, not just the one about to be disabled — because Pattern 1's repositioning shifts *all* surviving paths' coordinates, and exact restore (DISPLAY-02) must undo that shift for every display, not just reactivate the disabled one.
**When to use:** Change `IMonitorController.CaptureState(string monitorDevicePath)` to `IMonitorController.CaptureState()` — no parameter — mirroring `IAudioController.CaptureState()`'s existing no-param signature from Phase 3. `ToggleService.ToggleToRigMode()`'s call site (`_monitorController.CaptureState(settings.MonitorDevicePath!)`) changes to `_monitorController.CaptureState()`.
**Example:**
```csharp
// New MonitorState shape: primitive, JSON-serializable, one entry per path that was
// active at capture time (not just the target). Enums (DisplayConfigPixelFormat,
// DisplayConfigRotation, DisplayConfigScaling, DisplayConfigVideoOutputTechnology,
// DisplayConfigScanLineOrdering) are plain C# enums — serialize cleanly with
// System.Text.Json (default: as their underlying numeric value).
public sealed record MonitorPathSnapshot(
    string DevicePath,
    string FriendlyName,
    int PositionX,
    int PositionY,
    int ResolutionWidth,
    int ResolutionHeight,
    DisplayConfigPixelFormat PixelFormat,
    DisplayConfigRotation Rotation,
    DisplayConfigScaling Scaling,
    DisplayConfigVideoOutputTechnology OutputTechnology,
    ulong FrequencyInMillihertz,
    DisplayConfigScanLineOrdering ScanLineOrdering,
    bool IsPrimary);

public sealed record MonitorState(IReadOnlyList<MonitorPathSnapshot> Paths, string TargetDevicePath);
// TargetDevicePath: which entry is "the configured monitor to disable" — carried through
// so Restore/Disable both know which path to act on without a second settings read.
```
```csharp
// WindowsMonitorController.CaptureState() — captures the FULL active topology
public MonitorState CaptureState(string monitorDevicePath)
{
    PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);

    var snapshots = activePaths
        .SelectMany(p => p.TargetsInfo.Select(t => new MonitorPathSnapshot(
            t.DisplayTarget.DevicePath,
            t.DisplayTarget.FriendlyName ?? "(unknown display)",
            p.Position.X, p.Position.Y,
            p.Resolution.Width, p.Resolution.Height,
            p.PixelFormat,
            t.Rotation, t.Scaling, t.OutputTechnology,
            t.FrequencyInMillihertz, t.ScanLineOrdering,
            p.IsGDIPrimary)))
        .ToList();

    return new MonitorState(snapshots, monitorDevicePath);
}
```

### Pattern 3: Verify-and-throw after Disable (D-03/D-04)

**What:** After `ApplyPathInfos`, re-query `PathInfo.GetActivePaths()` fresh and confirm (a) the target device path is genuinely absent, and (b) exactly one remaining path is at `(0,0)` (a valid primary exists) — never trust the non-throwing return of `ApplyPathInfos` alone, and never use `Screen.AllScreens` (spike Finding 2: it caches and lags behind a real successful change).
**When to use:** Immediately after every `ApplyPathInfos` call in both `Disable` and `Restore`.
**Example:**
```csharp
// Mirrors WindowsAudioController.ApplyAndVerify (Phase 3, D-03/D-04 precedent)
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
```

### Pattern 4: Restore via live-identity re-resolution (not stored LUIDs)

**What:** At restore time, never reconstruct `PathDisplaySource`/`PathDisplayTarget` from LUID values persisted in yesterday's JSON — always re-derive them from a **fresh** `PathInfo.GetAllPaths()` call, matched against the snapshot's stored `DevicePath` strings. `GetAllPaths()` (→ `QDC_ALL_PATHS`) is documented by Microsoft to return "all the inactive paths after the active ones" — so the just-disabled monitor's path is still discoverable here even though it's not in `GetActivePaths()`.
**When to use:** `WindowsMonitorController.Restore(MonitorState previousState)`.
**Example:**
```csharp
// Source: WindowsDisplayAPI PathInfo.cs / PathTargetInfo.cs public constructors
// (direct source read this session) + learn.microsoft.com/windows/win32/api/winuser/
// nf-winuser-querydisplayconfig ("If QDC_ALL_PATHS is set... QueryDisplayConfig returns
// all the inactive paths after the active paths" — confirms the disabled target is
// still enumerable here for re-identification).
public void Restore(MonitorState previousState)
{
    PathInfo[] liveAllPaths = PathInfo.GetAllPaths(virtualModeAware: false);

    var rebuilt = new List<PathInfo>();
    foreach (var snap in previousState.Paths)
    {
        PathInfo? liveMatch = liveAllPaths.FirstOrDefault(p =>
            p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == snap.DevicePath));

        if (liveMatch is null)
        {
            // Hardware changed since capture (monitor unplugged) — cannot restore this
            // entry exactly. Per D-03/D-05, fail loudly rather than silently skip.
            throw new InvalidOperationException(
                $"Cannot restore '{snap.FriendlyName}' ({snap.DevicePath}) — no longer detected.");
        }

        // Reconstruct target info fully from STORED values, not from liveMatch's own
        // TargetsInfo — for an inactive path, Microsoft's docs state mode/signal info
        // "is not available... set to default values", so trusting live signal data
        // here would silently apply wrong rotation/scaling/frequency.
        var liveTarget = liveMatch.TargetsInfo.First(t => t.DisplayTarget.DevicePath == snap.DevicePath);
        var reconstructedTarget = new PathTargetInfo(
            liveTarget.DisplayTarget,          // fresh identity (adapter/target id), live-resolved
            snap.FrequencyInMillihertz,
            snap.ScanLineOrdering,
            snap.Rotation,
            snap.Scaling);

        rebuilt.Add(new PathInfo(
            liveMatch.DisplaySource,           // fresh identity, live-resolved
            new Point(snap.PositionX, snap.PositionY),
            new Size(snap.ResolutionWidth, snap.ResolutionHeight),
            snap.PixelFormat,
            new[] { reconstructedTarget }));
    }

    PathInfo.ApplyPathInfos(rebuilt.ToArray(), allowChanges: true);

    // Verify (D-03): target now active again, and its position/primary flag match.
    PathInfo[] verifyPaths = PathInfo.GetActivePaths(virtualModeAware: false);
    var restoredTarget = verifyPaths.FirstOrDefault(p =>
        p.TargetsInfo.Any(t => t.DisplayTarget.DevicePath == previousState.TargetDevicePath));
    var expectedSnap = previousState.Paths.First(s => s.DevicePath == previousState.TargetDevicePath);

    if (restoredTarget is null ||
        restoredTarget.Position.X != expectedSnap.PositionX ||
        restoredTarget.Position.Y != expectedSnap.PositionY ||
        restoredTarget.IsGDIPrimary != expectedSnap.IsPrimary)
    {
        throw new InvalidOperationException(
            "Monitor restore did not reproduce the exact prior configuration.");
    }
}
```

### Pattern 5: "Don't ask again" confirmation dialog (DISPLAY-03)

**What:** WinForms' built-in `MessageBox` has no checkbox option — a small custom `Form` (matching the project's existing `SettingsForm`/`*.Designer.cs` convention) with a `Label` + `CheckBox` + OK/Cancel buttons is the standard, dependency-free WinForms pattern for a "don't show this again" prompt. (A `TaskDialog`/`TASKDIALOGCONFIG` via `comctl32.dll` P/Invoke is the only alternative and adds native interop complexity for no benefit here — not recommended for a single-checkbox dialog.)
**When to use:** `MainForm.BtnToggle_Click`, only on the rig-mode direction, only when `settings.SkipMonitorConfirmation != true`.
**Example:**
```csharp
// New small Form, same pattern as existing SettingsForm (constructor injection avoided
// here since it needs no Core interface — just display data passed via constructor args).
public partial class MonitorConfirmDialog : Form
{
    public bool DontAskAgain => chkDontAskAgain.Checked;

    public MonitorConfirmDialog(string monitorFriendlyName)
    {
        InitializeComponent();
        lblMessage.Text = $"This will disable \"{monitorFriendlyName}\" (primary). Continue?";
        AcceptButton = btnContinue;
        CancelButton = btnCancel;
    }
}
```
```csharp
// MainForm.BtnToggle_Click, rig-mode branch, before _toggleService.ToggleToRigMode():
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
```csharp
// SettingsForm.BtnSaveSettings_Click — D-02: reset the flag when the monitor changes.
// Compare the PREVIOUSLY loaded _settings.MonitorDevicePath (captured in Load) against
// the newly selected monitorItem.Id before building settingsToSave:
bool monitorChanged = _settings.MonitorDevicePath != monitorItem.Id;
var settingsToSave = new AppSettings
{
    MonitorDevicePath = monitorItem.Id,
    // ... other fields unchanged ...
    SkipMonitorConfirmation = monitorChanged ? false : _settings.SkipMonitorConfirmation,
};
```

### Anti-Patterns to Avoid

- **Reusing the spike's `RunDisable`/`VerifyOnce` code verbatim:** it has no primary-repositioning logic and no cross-restart-safe snapshot shape — explicitly flagged as insufficient in the spike record itself.
- **Trying to set `PathInfo.Position` directly:** it has no public setter (confirmed by source read) — always construct a *new* `PathInfo` instance instead.
- **Persisting live `WindowsDisplayAPI` objects (or their LUIDs) directly into JSON and trusting them unchanged at restore time:** LUIDs are session-scoped; always re-resolve identity live via `GetAllPaths()` matched by `DevicePath`.
- **Trusting an inactive path's own `TargetsInfo` mode/signal data at restore time:** Microsoft's docs state inactive-path target info is "set to default values" — always reconstruct `PathTargetInfo` from the *stored* snapshot values (frequency, scan-line ordering, rotation, scaling), using only the *identity* (`DisplayTarget`) from the live inactive-path match.
- **Using a `MessageBox` for the "don't ask again" prompt:** no native checkbox support — use a small custom `Form` instead (Pattern 5).
- **Gating confirmation display on anything other than the persisted `SkipMonitorConfirmation` flag plus a monitor-change reset:** per D-01/D-02, this must be a durable settings flag, not a per-session or in-memory-only flag.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| CCD path/mode struct marshalling | Raw P/Invoke `DISPLAYCONFIG_PATH_INFO`/`DISPLAYCONFIG_MODE_INFO` definitions | `WindowsDisplayAPI`'s `PathInfo` public constructor (Pattern 1) | Confirmed sufficient for the repositioning fix via direct source read — no need to drop to the documented P/Invoke fallback |
| "Don't ask again" dialog with a checkbox | Native `TASKDIALOGCONFIG` P/Invoke | A small custom WinForms `Form` (Pattern 5) | Zero interop complexity, matches the project's existing `SettingsForm` convention exactly |
| Device identity resolution after a topology change | Manually tracking/caching adapter LUIDs across calls | Live `GetAllPaths()`/`GetActivePaths()` re-query matched by `DevicePath` (Pattern 4) | LUIDs are session-scoped and can go stale; the library already exposes a stable identifier (`DevicePath`) that survives topology changes |

**Key insight:** Every piece of this phase's core risk (primary-monitor repositioning) is resolvable through `WindowsDisplayAPI`'s existing public surface — the spike's blocker was a *usage* gap (not calling the right constructor), not a library *capability* gap. No new dependency, no raw P/Invoke, no elevation change from Phase 1's non-elevated baseline.

## Common Pitfalls

### Pitfall 1: Forgetting to shift ALL survivors, not just the promoted one
**What goes wrong:** If only the promoted survivor's position is set to `(0,0)` while other survivors (in a 3+ monitor setup) keep their original absolute coordinates, the desktop layout becomes internally inconsistent (gaps, overlaps) relative to the new origin.
**Why it happens:** It's tempting to special-case only the single path that needs the origin fix and leave everything else untouched.
**How to avoid:** Compute one delta (based on the promoted survivor's original position) and apply it uniformly to every survivor's position, not just the promoted one (Pattern 1).
**Warning signs:** Correct on this project's 2-monitor rig (only one survivor exists, so the bug is invisible) but would manifest immediately if the rig ever gains a third display.

### Pitfall 2: Restoring using an inactive path's own live mode/signal data
**What goes wrong:** Microsoft's `QueryDisplayConfig` documentation states inactive paths return target info "set to default values" with mode indexes "marked as invalid" — trusting a live inactive-path's `Rotation`/`Scaling`/`FrequencyInMillihertz` at restore time could silently apply wrong values instead of throwing.
**Why it happens:** It seems natural to just reuse whatever the live re-query returns for the matched path, since it's "the same monitor."
**How to avoid:** Only use the live re-query for *identity* objects (`DisplaySource`, `DisplayTarget`) — reconstruct all *mode/signal* values from the values captured in the JSON snapshot at Disable-time (Pattern 4).
**Warning signs:** Restored monitor comes back at the wrong refresh rate, rotation, or scaling despite the correct position/primary designation.

### Pitfall 3: Treating `MonitorState.CaptureState`'s target-only version as sufficient
**What goes wrong:** If `CaptureState` only captures the one monitor being disabled (Phase 2's current stub shape), `Restore` has no way to undo the coordinate shift Pattern 1 applies to the *other* monitor(s) — DISPLAY-02's "position... restored to exact prior configuration" would silently fail for the survivor.
**Why it happens:** The interface's current Phase-2 shape (`CaptureState(string monitorDevicePath)`) makes it easy to keep capturing just the one path.
**How to avoid:** Change the interface to capture the full active topology (Pattern 2), matching `IAudioController.CaptureState()`'s existing no-param, capture-everything precedent from Phase 3.
**Warning signs:** After restore, the previously-secondary monitor's position looks correct in isolation but the primary designation or an inspection with a third-party tool (e.g. Windows Display Settings) shows something subtly different from before the toggle.

### Pitfall 4: Old/stale `state.json` from before this phase's `MonitorState` reshape
**What goes wrong:** A `state.json` left over from Phase 2/3's testing (old `MonitorState(string MonitorDevicePath)` shape) will fail to deserialize into the new record shape.
**Why it happens:** `JsonSnapshotStore.Load()` already handles `JsonException` by returning `null` (treating a stale/old-shaped file as "no snapshot" — see its existing doc comment), which is the correct behavior and requires no change — but it's worth confirming this phase's new `MonitorState`/`MonitorPathSnapshot` shape doesn't accidentally deserialize *successfully* into garbage data (e.g. if field names happen to overlap). Records with renamed properties will fail cleanly via `JsonException`, which is the safe outcome already handled.
**How to avoid:** No new code needed — verify existing `JsonStoreTests.cs` still covers this "old shape → treated as no snapshot" behavior after `MonitorState`'s reshape, and add a dedicated test case for the new field set if the existing generic test doesn't already parametrize the shape.
**Warning signs:** App reports "Rig mode" on startup with garbage monitor coordinates instead of correctly falling back to "no snapshot."

### Pitfall 5: Assuming `PathInfo.GetAllPaths()` always finds the disabled monitor
**What goes wrong:** If the DisplayPort cable is physically unplugged (or the monitor powered fully off, not just CCD-disabled) between Disable and Restore, `GetAllPaths()` won't include it, and Pattern 4's live-match will come back null.
**Why it happens:** The whole restore design assumes the physical connection persists across the toggle cycle, which is true for this project's fixed rig setup but not universally guaranteed.
**How to avoid:** Pattern 4 already throws a clear `InvalidOperationException` naming the missing monitor rather than silently skipping it — this is the correct behavior per D-03/D-05 (fail loudly, no silent partial success), not a bug to "fix" with a fallback.
**Warning signs:** N/A — this is an expected, already-handled failure mode, not a hidden pitfall; documented here so the planner doesn't try to add unnecessary defensive plumbing beyond the throw.

## Code Examples

See Patterns 1–5 above — all code examples are sourced from direct reads of `WindowsDisplayAPI` 1.3.0.13's actual source (`PathInfo.cs`, `PathTargetInfo.cs`, `PathDisplaySource.cs`, `PathDisplayTarget.cs`, `PathDisplayAdapter.cs`) fetched this session from `github.com/falahati/WindowsDisplayAPI` (master branch, same tagged version already referenced in this project), plus Microsoft's official `QueryDisplayConfig` documentation for the `QDC_ALL_PATHS` inactive-path behavior.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|-----------------|--------|
| Spike's naive `reducedPaths = activePaths.Where(idx != targetIndex)` then `ApplyPathInfos` | Reconstructed `PathInfo[]` with shifted positions (Pattern 1) | This phase | Resolves the `PathChangeException` the spike hit twice on primary-removal; no library/API version change involved, purely a usage-pattern fix |
| In-memory-only snapshot (spike's `snapshot.json` was a human-readable audit trail; the *actual* restore reused in-memory `PathInfo[]` from the same process run) | Primitive, JSON-serializable `MonitorState`/`MonitorPathSnapshot` reconstructed via live identity re-resolution at restore time (Pattern 2 + 4) | This phase | Required because CORE-05 (mode survives a crash/restart) means the snapshot must be durable across process boundaries — the spike never needed this since it ran restore in the same process invocation |

**Deprecated/outdated:** None — the underlying CCD API surface is unchanged since Phase 1's research; this phase only changes how the existing library is *called*.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Constructing a new `PathInfo` via the public constructor (reusing `DisplaySource`/`Resolution`/`PixelFormat`/`TargetsInfo` from a live-enumerated survivor, with a translated `Position`) will pass `ApplyPathInfos`'s internal `ValidatePathInfos`/`SetDisplayConfig(SDC_VALIDATE)` check where the spike's naive path-removal failed | Pattern 1, Summary | If wrong, the same `PathChangeException` recurs even with repositioning — the next fallback is raw P/Invoke of `SetDisplayConfig` with hand-built `DISPLAYCONFIG_PATH_INFO`/`DISPLAYCONFIG_MODE_INFO` structs (documented in project STACK.md's Alternatives table) reconstructing `sourceInfo.modeInfoIdx`-referenced `DISPLAYCONFIG_SOURCE_MODE.position` directly. This assumption is based on source-code analysis (confirmed the constructor correctly threads `_position`/`_resolution`/`_pixelFormat` into `GetDisplayConfigSourceMode()`, which is exactly what `ApplyPathInfos` reads) but has **not** been empirically re-tested against the rig's actual AMD Radeon/DisplayPort hardware with this specific new code — only the spike's simpler naive removal was empirically tested. **This is the single highest-priority thing for the planner to schedule an early, isolated verification task for** (e.g. a small throwaway console re-test using this exact repositioning pattern, before building it into the full `WindowsMonitorController`), mirroring how Phase 1 de-risked the base mechanism. |
| A2 | Microsoft's documented `QDC_ALL_PATHS` behavior ("returns all the inactive paths after the active ones") means a CCD-topology-removed (not physically unplugged) DisplayPort monitor remains discoverable via `PathInfo.GetAllPaths()` for restore-time identity re-resolution | Pattern 4 | If wrong (e.g. if this rig's AMD driver drops the path from `GetAllPaths()` entirely once removed, not just from `GetActivePaths()`), Restore's live-match would always fail, and the design would need to fall back to storing raw LUID/SourceId/TargetId values (with the staleness risk noted in Alternatives Considered) or querying `PathDisplayTarget.GetDisplayTargets()`/`PathDisplaySource.GetDisplaySources()` as an alternate discovery path. This is a documented Microsoft behavior (HIGH confidence for the *general* Win32 API contract) but not yet empirically confirmed on this project's specific AMD/DisplayPort hardware post-CCD-removal — flag for early verification alongside A1. |
| A3 | A small custom WinForms `Form` (Pattern 5) is sufficient for the "don't ask again" confirmation UX, and no `TaskDialog`/native-checkbox API is needed | Pattern 5 | Low risk — this is a standard, widely-used WinForms pattern with no ambiguity; if wrong, the only cost is slightly more code for a `TASKDIALOGCONFIG` P/Invoke wrapper, not a correctness issue. |

## Open Questions

1. **Does the repositioning-aware `ApplyPathInfos` call (Pattern 1) actually succeed on this rig's AMD Radeon/DisplayPort hardware for the primary-removal case?**
   - What we know: source-code analysis confirms the public `PathInfo` constructor correctly threads position/resolution/pixel-format into the same internal path used by `ApplyPathInfos`'s validation and apply logic; the only thing the spike's naive code was missing (per Finding 3's root-cause) was exactly this repositioning step.
   - What's unclear: whether AMD's specific WDDM driver on this rig accepts this reconstructed topology, or has some other validation quirk not covered by the general CCD documentation.
   - Recommendation: the planner should schedule this as an isolated, early verification step (extend the existing spike tool or add a throwaway test harness) before wiring the full `Disable`/`Restore`/confirmation-dialog flow — same "de-risk first" philosophy Phase 1 already established for this project. This is not fully researchable further from a Linux sandbox; it requires the user's rig.

2. **What is the correct behavior when `GetAllPaths()` returns MULTIPLE candidate matches for a target `DevicePath` (e.g. after a driver re-enumeration assigns a new `TargetId` to the same physical monitor)?**
   - What we know: `DevicePath` (the `\\?\DISPLAY#...` string) is described by `WindowsDisplayAPI`/Microsoft as the stable per-monitor identifier, and is already used elsewhere in this app (Settings picker matching) as the stable ID.
   - What's unclear: whether a driver reload mid-session could ever produce two live paths reporting the identical `DevicePath` simultaneously (unlikely, but not explicitly ruled out by any source found this session).
   - Recommendation: use `.First()`/`.FirstOrDefault()` (not `.Single()`) in the matching logic to avoid an unhandled `InvalidOperationException` from LINQ itself if this edge case ever occurs — already reflected in Pattern 4's example code.

## Environment Availability

Not applicable — this phase adds no new external dependency (no new NuGet package, no new CLI tool, no new service). `WindowsDisplayAPI` 1.3.0.13 is already installed and available per Phase 1/2's environment setup; no change here.

## Security Domain

> `security_enforcement` is absent from `.planning/config.json`'s `features` block — treated as enabled per policy, consistent with Phase 1's research. This remains a single-user, no-network, local-desktop-API phase; most ASVS categories are not applicable.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No | Single local interactive user, no auth surface |
| V3 Session Management | No | No network sessions |
| V4 Access Control | No | No multi-user/role concept |
| V5 Input Validation | Yes (minimal) | `settings.MonitorDevicePath` is read from a previously-validated Settings selection (already constrained to enumerated hardware by `SettingsForm`'s picker, per Phase 2/3 precedent) — `Disable`/`Restore` should still defensively handle a "not found" match (Pattern 3/4's throw) rather than indexing into an empty/mismatched array unguarded |
| V6 Cryptography | No | No secrets; `state.json`'s monitor-topology data is not sensitive |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|------------------------|
| Elevation of privilege via reflexive `requireAdministrator` | Elevation of Privilege | Not applicable to this phase's mechanism — the repositioning fix stays entirely within the already-non-elevated `ApplyPathInfos` path validated in Phase 1; no elevation change needed |
| Trusting a mutating COM/CCD call's non-throwing return as proof of success | Tampering (of application state, not data) | D-03/D-04's verify-and-throw pattern (Pattern 3), directly inherited from Phase 3's `ApplyAndVerify` precedent |
| Stale/corrupted `state.json` causing incorrect mode detection or a bad restore | Tampering / Denial of Service (app stuck reporting wrong mode) | Existing `JsonSnapshotStore.Load()`'s `JsonException` → `null` fallback (Pitfall 4) already covers deserialization failure; Pattern 4's throw-on-missing-target covers the "hardware changed" case |

## Sources

### Primary (HIGH confidence)
- `github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathInfo.cs` — direct source read this session, confirmed the public constructor overload `PathInfo(PathDisplaySource, Point, Size, DisplayConfigPixelFormat, PathTargetInfo[])`, confirmed `Position` has no public setter, confirmed `ApplyPathInfos`'s internal flow (`ValidatePathInfos` → `GetDisplayConfigPathInfos` → `SetDisplayConfig`) reads position/resolution/pixel-format from exactly the fields this constructor sets
- `github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathTargetInfo.cs` — direct source read this session, confirmed the public constructor `PathTargetInfo(PathDisplayTarget, ulong frequencyInMillihertz, DisplayConfigScanLineOrdering, DisplayConfigRotation, DisplayConfigScaling, bool)` needed for Pattern 4's restore reconstruction
- `github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathDisplaySource.cs`, `PathDisplayTarget.cs`, `PathDisplayAdapter.cs` — direct source reads this session, confirmed identity object shapes (`AdapterId`/LUID, `SourceId`, `TargetId`, `DevicePath`) and that `LUID` is a session-scoped 64-bit value (`LowPart`/`HighPart`), motivating Pattern 4's live-re-resolution design over persisted-LUID reconstruction
- `learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig` — official Microsoft reference, fetched directly this session, confirmed `QDC_ALL_PATHS` returns all inactive paths after active ones, and confirmed inactive-path target/mode info is "set to default values" with mode indexes "marked as invalid" (source for Pitfall 2)
- `spike/RESULTS-TEMPLATE.md`, `spike/MonitorDetachSpike/Program.cs`, `spike/FALLBACK.md` — this project's own empirical spike record; source of the exact `PathChangeException` root cause this phase resolves
- `.planning/phases/01-monitor-disable-feasibility-spike/01-RESEARCH.md` — Pitfall B / Assumption A2, which correctly predicted the spike's actual Finding 3
- `.planning/phases/03-app-audio-control/03-CONTEXT.md` and `src/RigToggle.Windows/WindowsAudioController.cs` — direct precedent for the verify-and-throw pattern (`ApplyAndVerify`) this phase extends to monitor control

### Secondary (MEDIUM confidence)
- None beyond the above — this phase's research relied entirely on direct source/doc verification rather than community/forum sources, given the core question was resolvable against the exact library version and official Microsoft API docs.

### Tertiary (LOW confidence)
- None.

## Metadata

**Confidence breakdown:**
- Standard stack (no new dependency, same `WindowsDisplayAPI` 1.3.0.13): HIGH — unchanged from Phase 1's already-verified audit
- Architecture / repositioning mechanism (Pattern 1): HIGH on API-surface correctness (direct source read confirms the constructor threads through correctly), MEDIUM on empirical rig behavior (not yet re-tested with this exact new code — see Assumption A1 / Open Question 1)
- Restore mechanism (Pattern 4): HIGH on Microsoft's documented `QDC_ALL_PATHS` inactive-path behavior, MEDIUM on this rig's specific AMD driver conforming to that general documentation (see Assumption A2 / Open Question 2)
- Confirmation dialog (Pattern 5): HIGH — standard, unambiguous WinForms pattern

**Research date:** 2026-07-24
**Valid until:** ~30 days (stable Win32/CCD API surface and unchanged library version; the only fast-moving risk is empirical hardware behavior, which no further research can resolve — only the rig test can)
