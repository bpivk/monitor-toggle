# Phase 17: Manual Monitor Panel & Shared Safety Guard - Research

**Researched:** 2026-08-08
**Domain:** WinForms multi-monitor UI (live status panel, per-monitor CCD enable/disable, hotplug detection, multi-screen overlay) built on this project's existing WindowsDisplayAPI/CCD adapter
**Confidence:** HIGH

## Summary

Phase 17 is almost entirely a UI-tier addition on top of primitives Phase 16 already unified. The single most important research finding is that **DISPLAY-12's "shared codepath" requirement is already structurally satisfied** by `WindowsMonitorController.DeactivateMonitors()` — its zero-survivors guard (`"Cannot disable all configured monitors — at least one active display must remain."`, `src/RigToggle.Windows/WindowsMonitorController.cs:296-308`) is the one and only place that check exists, and both `ToggleService.ToggleToRigMode()` and `ToggleToNormalMode()` already call through it (confirmed in `16-03-SUMMARY.md`). The manual panel does **not** need a new guard, a new interface method, or a new abstraction — it only needs to become the third caller of the exact same `IMonitorController.DeactivateMonitors(IReadOnlySet<string>)` method, passing a one-element set for a single-monitor disable. If the panel instead built its own "is this the last monitor" check (e.g. by counting `IsActive` rows in the grid), that would violate the phase's explicit "not three separate checks" goal even if the outcome looked identical — this is the one architectural rule the plan must not violate.

The panel itself (PANEL-01/02/03/05) is new work with no direct precedent in this codebase, but every sub-problem has a close analog already solved here: `SettingsForm`'s `DataGridView`-based monitor grid (`dgvMonitors`/`GetAllMonitors()`/Tag-keyed `DevicePath` rows) is the direct structural template for PANEL-01/02; `WindowsThemeProvider`'s `SystemEvents.UserPreferenceChanged` + marshal-then-diff pattern is the direct template for PANEL-03's live hotplug detection, substituting `SystemEvents.DisplaySettingsChanged` (fires on `WM_DISPLAYCHANGE`, confirmed via Microsoft Learn); `MonitorConfirmDialog` + `AppSettings.SkipMonitorConfirmation` is the direct template for PANEL-04's confirmation gate; and `WindowsMonitorController.CaptureState()`'s existing `MonitorPathSnapshot` (`PositionX/Y`, `ResolutionWidth/Height`) is a ready-made, CCD-sourced position/size source for PANEL-05's Identify overlay windows — no new enumeration API, and no need to correlate against WinForms' separate GDI-based `Screen.AllScreens` identity space (which uses different device-name strings than CCD `DevicePath`).

**Primary recommendation:** Build the panel as a new non-modal `Form` (reachable from the tray context menu, mirroring the existing Settings/Toggle/Exit entries), backed directly by `IMonitorController` (not `ToggleService`/`ToggleOrchestrator` — PANEL-02 requires it be independent of mode semantics), reusing `GetAllMonitors()` for the row model, `ActivateMonitors`/`DeactivateMonitors` verbatim for row actions (this is what makes DISPLAY-12 free), `SystemEvents.DisplaySettingsChanged` for PANEL-03, and `CaptureState()`'s per-path `Position`/`Resolution` for PANEL-05's overlay placement.

## User Constraints

No `CONTEXT.md` exists yet for Phase 17 (`.planning/phases/17-manual-monitor-panel-shared-safety-guard/` contains only this research file) — `/gsd:discuss-phase` has not been run for this phase. There are no locked decisions, discretion notes, or deferred ideas to copy verbatim. The planner should treat every design choice below as a recommendation, not a locked decision, unless/until a discuss-phase session produces a CONTEXT.md.

The two relevant milestone-level constraints from `REQUIREMENTS.md`'s "Out of Scope" table still apply and are load-bearing for this phase:
- **"Drag-and-drop monitor arrangement / resolution / orientation editing in the manual panel"** — out of scope. The panel stays narrowly on/off, matching the app's existing disable/enable-only monitor model.
- **"Persisting manual-panel toggle actions into Rig-mode/Normal-mode config"** — out of scope. "The manual panel is explicitly independent, ad-hoc, on-demand — not a way to redefine what a mode means." This directly answers research question 6 below: the panel must never write to `AppSettings.MonitorsToDisable`/`NormalMonitorsToDisable`/etc., and must never call `IModeStore.Save(...)`.

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DISPLAY-12 | Safety guard enforced identically across Rig toggle, Normal toggle, and manual panel | Already satisfied structurally by `WindowsMonitorController.DeactivateMonitors()`'s zero-survivors guard, reused as-is from Phase 16 — see Summary and "Shared Safety Guard" pattern below. Panel must call `IMonitorController.DeactivateMonitors` directly, never re-implement the check. |
| PANEL-01 | One row/tile per monitor, live status via icon | `GetAllMonitors()` (already returns `IsActive`/`IsPrimary` per `MonitorInfo`) + `DataGridView`/`DataGridViewImageColumn` pattern, mirroring `SettingsForm.dgvMonitors`. See "Status Icon Rendering" pattern. |
| PANEL-02 | Enable/disable any monitor from panel, independent of Rig/Normal toggle, immediate effect | Panel calls `IMonitorController.ActivateMonitors`/`DeactivateMonitors` directly (bypassing `ToggleService`/`ToggleOrchestrator` entirely) — see "Independence from ToggleService" pattern and the concurrency Open Question. |
| PANEL-03 | Live update on connect/disconnect while panel open | `Microsoft.Win32.SystemEvents.DisplaySettingsChanged`, same idiom as `WindowsThemeProvider.OnUserPreferenceChanged` — see "Live Hotplug Detection" pattern and Pitfall 1. |
| PANEL-04 | Gated by same `SkipMonitorConfirmation` setting as Rig/Normal toggle | Reuse `AppSettings.SkipMonitorConfirmation` read/write exactly as `MainForm.BtnToggle_Click` does; reuse or adapt `MonitorConfirmDialog` for a single-monitor phrasing — see "Confirmation Gate Reuse" pattern. |
| PANEL-05 | Identify action overlays a number on each physical screen | Borderless topmost `Form` per monitor positioned from `CaptureState()`'s `MonitorPathSnapshot.PositionX/Y`/`ResolutionWidth/Height` (CCD-sourced, not `Screen.AllScreens`) — see "Identify Overlay" pattern and Pitfall 3 (DPI). |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Monitor enumeration + status (PANEL-01) | App tier (WinForms `MonitorPanelForm`) | Windows adapter (`WindowsMonitorController.GetAllMonitors`) | This is a single-user, single-process Windows desktop app with no server/client split — "App tier" here means the WinForms UI layer, "Windows adapter" the existing `RigToggle.Windows` CCD wrapper. Same split as every existing Settings/toggle feature. |
| Per-monitor enable/disable mutation (PANEL-02) | Windows adapter (`WindowsMonitorController.ActivateMonitors`/`DeactivateMonitors`) | App tier (panel button click handler) | The actual CCD mutation and its safety guard already live in the adapter; the panel only supplies the one-element device-path set and reacts to success/exception. |
| Zero-survivors safety guard (DISPLAY-12) | Windows adapter (`WindowsMonitorController.DeactivateMonitors`, existing code) | — | Already centralized; no new tier involvement needed. This is the phase's core "don't add a third check" constraint. |
| Hotplug live-update (PANEL-03) | App tier (new form subscribes to `SystemEvents.DisplaySettingsChanged`) | Windows adapter (re-query via `GetAllMonitors()` on event) | `SystemEvents` is a .NET BCL facility (`Microsoft.Win32`), not a CCD-specific API — the App tier owns the subscription/marshaling, the adapter still owns re-enumeration. |
| Confirmation gate (PANEL-04) | App tier (dialog + `AppSettings.SkipMonitorConfirmation` read/write) | Persistence (`ISettingsStore`) | Identical shape to the existing Rig/Normal confirmation flow in `MainForm.BtnToggle_Click`. |
| Identify overlay (PANEL-05) | App tier (new borderless per-monitor `Form`s) | Windows adapter (`CaptureState()` supplies position/size) | Overlay windows are pure WinForms UI; the adapter only supplies CCD-accurate coordinates so the overlay avoids the GDI/CCD identity-space mismatch pitfall (see Pitfall 2). |

## Standard Stack

### Core

No new packages are required for this phase. Every capability is covered by libraries already referenced in the solution:

| Library | Version | Purpose | Why Standard (for this phase) |
|---------|---------|---------|--------------------------------|
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 (already referenced, `RigToggle.Windows.csproj`) [VERIFIED: codebase] | `PathInfo`-based CCD enable/disable, already exposed via `IMonitorController.ActivateMonitors`/`DeactivateMonitors`/`GetAllMonitors`/`CaptureState` | This phase reuses these methods verbatim — no new WindowsDisplayAPI surface is needed. |
| `System.Windows.Forms` (`net10.0-windows`, in-box) | .NET 10 SDK | `DataGridView`/`DataGridViewImageColumn`, borderless `Form` for the Identify overlay, `NotifyIcon`/`ContextMenuStrip` for panel launch | Already the app's sole UI framework; no alternative under consideration. |
| `Microsoft.Win32.SystemEvents` (in `System` / part of the WinForms-enabled BCL, already used by `WindowsThemeProvider.cs`) | .NET 10 SDK, in-box | `SystemEvents.DisplaySettingsChanged` for PANEL-03 hotplug detection | Confirmed present in the codebase already (`using Microsoft.Win32;` in `WindowsThemeProvider.cs`) — zero new dependency, same event-subscription idiom this project already uses for live theme-follow. [VERIFIED: codebase + Microsoft Learn] |

### Supporting

None. No audio, JSON, or persistence changes are required by this phase's requirements (PANEL-04 only *reads* the existing `AppSettings.SkipMonitorConfirmation` field, added in an earlier phase).

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `DataGridView` + `DataGridViewImageColumn` for PANEL-01 | `ListView` (`View.Details` or `View.Tile`) + `ImageList` | `ListView` is a reasonable alternative and slightly lighter-weight for a pure list, but this project has zero `ListView` precedent and two independent `DataGridView` precedents (`dgvMonitors`, `dgvMonitorsNormal`) with already-solved patterns for Tag-keyed `DevicePath` rows, reentrancy-guarded checkbox columns, and `ThemeApplier.ThemeMonitorGrid` dark-mode theming. Reusing `DataGridView` costs nothing and inherits all of that; introducing `ListView` would mean re-solving theming and row-identity from scratch for no functional gain. |
| `SystemEvents.DisplaySettingsChanged` for PANEL-03 | Raw P/Invoke `WM_DISPLAYCHANGE` handling via a custom `NativeWindow`/`WndProc` override | `SystemEvents.DisplaySettingsChanged` is documented by Microsoft as firing on exactly this condition and is already the established in-project idiom (`WindowsThemeProvider` does the P/Invoke-free equivalent for `WM_SETTINGCHANGE`-driven theme flips). A raw `WndProc` override would require hosting a message-only window or overriding `MonitorPanelForm.WndProc`, which is strictly more code for no additional capability. |
| Runtime-drawn status dot `Bitmap` (Graphics.FillEllipse) for PANEL-01's icon | New embedded `.ico`/`.png` asset pair (green/red dot) | Either works. Runtime-drawn avoids adding new binary assets to the repo and can be made theme-aware (color choice can consult `IThemeProvider.CurrentTheme` the same way `ThemeApplier` already does for grid colors) without needing two icon variants per theme. A static asset pair is marginally simpler to reason about but doesn't get theme-awareness for free. Recommend runtime-drawn, but either is acceptable — this is Claude's-discretion-level, not load-bearing. |
| `CaptureState()`'s `MonitorPathSnapshot.PositionX/Y`/`ResolutionWidth/Height` for Identify overlay placement | `System.Windows.Forms.Screen.AllScreens[i].Bounds` | `Screen.AllScreens` uses GDI device names (e.g. `\\.\DISPLAY1`) as its identity, which do **not** match the CCD `DevicePath` strings (`\\?\DISPLAY#...#{GUID}`) this app uses everywhere else (`AppSettings.MonitorsToDisable`, `MonitorInfo.DevicePath`) — there is no built-in correlation between the two identity spaces, and this project's own code comments repeatedly warn against using `Screen`-based enumeration as an authority for CCD state (`WindowsMonitorController.cs`: "Never uses the WinForms screen-enumeration API as the oracle (D-04)"). `CaptureState()` already returns per-`DevicePath` `Position`/`Resolution` in the same coordinate space `PathInfo`/`ApplyPathInfos` uses elsewhere in this file, so reusing it sidesteps the correlation problem entirely and stays consistent with the existing much-reiterated "CCD data only, not `Screen`" convention. |

## Package Legitimacy Audit

**Not applicable this phase.** No new external packages are introduced — every capability (`DataGridView`, `SystemEvents`, borderless `Form`s, `WindowsDisplayAPI` CCD calls) is already present in the solution's dependency graph (`RigToggle.Windows.csproj`'s existing `WindowsDisplayAPI 1.3.0.13`/`NAudio 2.3.0` references, plus in-box `net10.0-windows` WinForms/BCL types). The planner does not need to gate anything behind a `checkpoint:human-verify` install step for this phase.

## Architecture Patterns

### System Architecture Diagram

```
                         ┌─────────────────────────────┐
                         │  Tray ContextMenuStrip        │
                         │  (existing: Toggle/Settings/  │
                         │   Exit — new: "Monitors...")  │
                         └──────────────┬────────────────┘
                                        │ click
                                        ▼
                         ┌─────────────────────────────┐
        SystemEvents.    │   MonitorPanelForm (new)     │◄──── CaptureState()
        DisplaySettings  │   - DataGridView (1 row/mon) │      (Position/Resolution
        Changed ────────►│   - status icon per row      │       for Identify overlay
        (marshaled via   │   - Enable/Disable per row    │       placement)
        InvokeRequired)  │   - Identify button           │
                         └──────┬─────────────┬───────────┘
                                │             │
                    GetAllMonitors()      ActivateMonitors(set)/
                    (row refresh)         DeactivateMonitors(set)
                                │             │
                                ▼             ▼
                         ┌─────────────────────────────┐
                         │  IMonitorController           │
                         │  (WindowsMonitorController)    │◄── zero-survivors guard
                         └──────────────┬────────────────┘     lives HERE — same
                                        │                       codepath both toggle
                          WindowsDisplayAPI PathInfo /          directions already use
                          SetDisplayConfig (CCD)                (DISPLAY-12 satisfied
                                                                  by reuse, not by a
                                                                  new check)

  Parallel, pre-existing path (unaffected by this phase):
  MainForm.BtnToggle_Click → ToggleOrchestrator → ToggleService.ToggleToRigMode/
  ToggleToNormalMode → same IMonitorController.ActivateMonitors/DeactivateMonitors
```

The key structural point the diagram is meant to convey: `MonitorPanelForm` and `MainForm`'s existing toggle path are two independent entry points that **converge on the exact same `IMonitorController` instance and the exact same `DeactivateMonitors` method** — that convergence is what makes DISPLAY-12 "one shared codepath" true without adding new code to enforce it.

### Recommended Project Structure

```
src/RigToggle.App/
├── MonitorPanelForm.cs            # new — panel window, grid population, event wiring
├── MonitorPanelForm.Designer.cs   # new — DataGridView + Identify button designer code
├── MonitorIdentifyOverlay.cs      # new — one borderless Form per monitor, auto-closing
├── MainForm.cs                    # modified — new tray menu item opens MonitorPanelForm
├── MainForm.Designer.cs           # modified — new ToolStripMenuItem
├── MonitorConfirmDialog.cs        # existing — reused as-is or lightly adapted for
│                                  #   single-monitor phrasing (PANEL-04)
├── SettingsForm.cs                # existing — structural template only, not modified
├── Program.cs                     # modified — construct/wire MonitorPanelForm factory
│                                  #   in the composition root (same pattern as
│                                  #   SettingsFormFactory)
src/RigToggle.Core/
├── (no new files expected — IMonitorController/ToggleService are reused unmodified)
src/RigToggle.Windows/
├── WindowsMonitorController.cs    # unmodified — zero-survivors guard already generic
                                    #   (mode-agnostic message since Phase 16)
```

No changes to `RigToggle.Core` or `RigToggle.Windows` are anticipated by this research — every requirement is satisfiable from the App tier alone, calling existing `IMonitorController` members. This is a smaller blast radius than Phase 16.

### Pattern 1: Shared Safety Guard Reuse (DISPLAY-12)

**What:** The panel's per-monitor "Disable" action calls `_monitorController.DeactivateMonitors(new HashSet<string> { devicePath })` — the exact same method both `ToggleService.ToggleToRigMode()` and `ToggleToNormalMode()` already call (`src/RigToggle.Core/ToggleService.cs:104`, `:356`). No new guard code is written for this phase.
**When to use:** Every panel-initiated disable action, with no exceptions.
**Example:**
```csharp
// Source: src/RigToggle.Windows/WindowsMonitorController.cs:268-366 (existing, unmodified)
// The guard that makes DISPLAY-12 "free":
if (survivors.Length == 0)
{
    throw new InvalidOperationException(
        "Cannot disable all configured monitors — at least one active display must remain.");
}

// New panel code (App tier) — same call shape as ToggleService's Monitor step,
// but for exactly one monitor and with no Audio/App steps around it:
private void DisableMonitor(string devicePath)
{
    try
    {
        _monitorController.DeactivateMonitors(new HashSet<string> { devicePath });
        RefreshGrid();
    }
    catch (InvalidOperationException ex)
    {
        MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
```
**Do not** write a pre-check like `if (allMonitors.Count(m => m.IsActive) <= 1) { ... }` in the panel before calling `DeactivateMonitors` — that would be a second implementation of the same rule (the exact anti-pattern DISPLAY-12 exists to prevent), and could drift from the adapter's own check (e.g. the adapter's check is topology-aware — it recomputes `survivors` from the live CCD state at mutation time, not from a UI-cached `IsActive` snapshot that could be stale by the time the click is processed).

### Pattern 2: Independence from ToggleService/ToggleOrchestrator (PANEL-02)

**What:** The panel calls `IMonitorController` directly, never `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()` and never `ToggleOrchestrator`.
**When to use:** All panel monitor actions.
**Why:** `ToggleService`'s two public toggle methods are 3-step sequences (Monitor + Audio + App) tied to `IModeStore` mode-flag writes and `AppSettings.MonitorsToDisable`/`NormalMonitorsToDisable` — calling through them for a single ad-hoc monitor flip would incorrectly attempt an audio switch and app launch/minimize, and would incorrectly write a `ToggleMode` the user never asked for. REQUIREMENTS.md's Out of Scope table confirms this is intentional: "Persisting manual-panel toggle actions into Rig-mode/Normal-mode config" is explicitly excluded. `ToggleResult`/`ToggleStepResult`/`ToggleResultFormatter` are also scoped to the 3-step model and should not be reused for panel actions — a panel action is a single try/catch around one `IMonitorController` call, not a checklist.

### Pattern 3: Status Icon Rendering (PANEL-01)

**What:** A `DataGridView` (matching `SettingsForm.dgvMonitors`'s conventions: one row per `MonitorInfo` from `GetAllMonitors()`, `Tag = monitor.DevicePath` for stable identity) with a `DataGridViewImageColumn` showing a small runtime-drawn status glyph (filled circle, green = active/on, gray or red = OS-disabled/off), plus text columns for friendly name and Primary/OS-disabled suffix (reusing the exact suffix logic already in `SettingsForm.PopulateMonitorGrid`), plus either a single toggle button column or twin Enable/Disable button columns per row.
**When to use:** PANEL-01's row/tile requirement.
**Example:**
```csharp
// Source: pattern derived from SettingsForm.cs:405-423 (existing row-population idiom)
// and System.Drawing Graphics API (no new dependency)
private static Bitmap CreateStatusDot(bool isActive, bool isDark)
{
    var bmp = new Bitmap(12, 12);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    Color dotColor = isActive ? Color.FromArgb(46, 204, 113) : Color.FromArgb(200, 60, 60);
    using var brush = new SolidBrush(dotColor);
    g.FillEllipse(brush, 0, 0, 11, 11);
    return bmp;
}

private void PopulateMonitorPanelGrid()
{
    IReadOnlyList<MonitorInfo> monitors = _monitorController.GetAllMonitors();
    dgvMonitorPanel.Rows.Clear();
    foreach (MonitorInfo m in monitors)
    {
        int rowIndex = dgvMonitorPanel.Rows.Add(CreateStatusDot(m.IsActive, IsDark), m.FriendlyName);
        dgvMonitorPanel.Rows[rowIndex].Tag = m.DevicePath;
    }
}
```
Note: `DataGridViewImageColumn` requires a `Bitmap`/`Image`, not an `Icon` — the small embedded tray `.ico` files (`normal.ico`/`rig.ico`) represent app *mode*, not per-monitor status, and should not be reused here; drawing a fresh dot bitmap is simpler and theme-aware.

### Pattern 4: Live Hotplug Detection (PANEL-03)

**What:** Subscribe to `Microsoft.Win32.SystemEvents.DisplaySettingsChanged` in the panel form's constructor (or an `OnLoad`), unsubscribe in `FormClosed`/`Dispose`, marshal to the UI thread exactly like `WindowsThemeProvider`/`MainForm.OnThemeChanged` already do, then re-run `PopulateMonitorPanelGrid()`.
**When to use:** Whenever `MonitorPanelForm` is open.
**Example:**
```csharp
// Source: pattern mirrors src/RigToggle.Windows/WindowsThemeProvider.cs:44,51-71
// and src/RigToggle.App/MainForm.cs:72-92 (marshal-then-try/catch idiom)
public MonitorPanelForm(IMonitorController monitorController, /* ... */)
{
    InitializeComponent();
    Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    this.FormClosed += (_, _) =>
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
}

private void OnDisplaySettingsChanged(object? sender, EventArgs e)
{
    if (InvokeRequired) { BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e))); return; }
    try { PopulateMonitorPanelGrid(); }
    catch { /* best-effort refresh — never crash on a hotplug notification */ }
}
```
This event also fires for the panel's **own** `ActivateMonitors`/`DeactivateMonitors` calls (any CCD topology change triggers `WM_DISPLAYCHANGE`, not just physical hotplug) — the explicit post-action `RefreshGrid()` call in Pattern 1's example and this event handler will both fire for a panel-initiated change. This is harmless (idempotent re-render) but worth a comment in the plan so a future reader doesn't "fix" it as a duplicate-refresh bug.

### Pattern 5: Confirmation Gate Reuse (PANEL-04)

**What:** Before a panel disable action mutates state, check `_settingsStore.Load().SkipMonitorConfirmation`; if false, show a confirmation dialog naming the single monitor being disabled (adapt `MonitorConfirmDialog`, which already accepts `disableNames`/`enableNames` lists and already has a "don't ask again" checkbox wired to write `SkipMonitorConfirmation = true` back through `ISettingsStore`), matching `MainForm.BtnToggle_Click`'s existing block (`src/RigToggle.App/MainForm.cs:337-377`) almost verbatim, but for exactly one monitor instead of a full configured set.
**When to use:** Panel disable actions only — PANEL-04 does not require gating Enable actions (enabling a monitor can never trigger the zero-survivors failure mode, matching why `MonitorConfirmDialog`'s existing call site already treats disable as the safety-relevant direction).
**Example:**
```csharp
// Source: pattern mirrors src/RigToggle.App/MainForm.cs:337-377
var settings = _settingsStore.Load();
if (!settings.SkipMonitorConfirmation)
{
    using var confirmDialog = new MonitorConfirmDialog(
        disableNames: new[] { monitorFriendlyName },
        enableNames: Array.Empty<string>(),
        _themeProvider);
    if (confirmDialog.ShowDialog(this) != DialogResult.OK) return;
    if (confirmDialog.DontAskAgain)
    {
        settings.SkipMonitorConfirmation = true;
        _settingsStore.Save(settings);
    }
}
```

### Pattern 6: Identify Overlay (PANEL-05)

**What:** For each monitor to identify, construct one borderless, topmost, non-taskbar `Form` sized/positioned from CCD-sourced coordinates, showing a large number, auto-closing after a few seconds via `System.Windows.Forms.Timer`.
**When to use:** PANEL-05's Identify action, scoped to **active** monitors only — an OS-disabled monitor has no active desktop surface to draw on (Windows has detached it from the composition pipeline), so `CaptureState()` (which only enumerates active `PathInfo`s, per its own doc comment) naturally cannot supply coordinates for a disabled monitor. The panel should either disable/hide the Identify affordance for OS-disabled rows or simply skip them silently when building the overlay set.
**Example:**
```csharp
// Source: pattern combines src/RigToggle.Windows/WindowsMonitorController.cs's
// CaptureState() (existing, unmodified) with a new borderless-Form idiom (no
// existing precedent in this codebase for this specific shape)
private void ShowIdentifyOverlays()
{
    MonitorState state = _monitorController.CaptureState(); // active paths only
    int number = 1;
    foreach (MonitorPathSnapshot snap in state.Paths)
    {
        var overlay = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(snap.PositionX, snap.PositionY),
            Size = new Size(snap.ResolutionWidth, snap.ResolutionHeight),
            TopMost = true,
            ShowInTaskbar = false,
            BackColor = Color.Black,
        };
        var lbl = new Label
        {
            Text = number.ToString(),
            Font = new Font("Segoe UI", 120, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        overlay.Controls.Add(lbl);
        var timer = new System.Windows.Forms.Timer { Interval = 2500 };
        timer.Tick += (_, _) => { timer.Stop(); overlay.Close(); };
        overlay.Shown += (_, _) => timer.Start();
        overlay.Show();
        number++;
    }
}
```
The "number" assigned to each monitor should derive from a stable, reproducible ordering (e.g. the same row order already shown in the panel grid) so the overlay's numbering matches what the user just saw in the list — do not re-sort `state.Paths` independently of the grid's own row order.

### Anti-Patterns to Avoid

- **Re-implementing the zero-survivors check in the panel:** covered exhaustively above (Pattern 1) — this is the one rule this entire phase exists to enforce.
- **Routing panel actions through `ToggleService`/`ToggleOrchestrator`:** would incorrectly touch audio/app steps and the mode flag; PANEL-02 and the REQUIREMENTS.md Out-of-Scope table both forbid this.
- **Using `Screen.AllScreens` to position the Identify overlay:** wrong identity space relative to the CCD `DevicePath`s this app uses everywhere else, and contradicts this codebase's own repeated "never use the WinForms screen-enumeration API as an oracle" doctrine (originally stated for CCD-mutation verification, but the identity-space mismatch applies equally to positioning).
- **Persisting a panel-initiated monitor change into `AppSettings.MonitorsToDisable`/`NormalMonitorsToDisable`:** explicitly out of scope per REQUIREMENTS.md.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| "Is this the last enabled monitor?" check | A new pre-check in the panel counting active monitors | `IMonitorController.DeactivateMonitors`'s existing zero-survivors guard | This is DISPLAY-12's entire point — a second implementation of this rule is the specific failure mode the requirement exists to prevent, and would be strictly worse than the existing guard (UI-cached `IsActive` state can go stale between grid population and click; the adapter's guard recomputes from live CCD state at mutation time). |
| Hotplug/display-change detection | A custom `NativeWindow`/`WndProc` override listening for raw `WM_DISPLAYCHANGE` | `Microsoft.Win32.SystemEvents.DisplaySettingsChanged` | Already the exact BCL facility for this, already proven in this codebase for an analogous live-system-change scenario (`WindowsThemeProvider`), zero new P/Invoke surface. |
| Monitor-to-screen-coordinate mapping for the Identify overlay | A custom correlation layer matching `Screen.AllScreens[i].DeviceName` strings to CCD `DevicePath` strings | `WindowsMonitorController.CaptureState()`'s existing `MonitorPathSnapshot.PositionX/Y`/`ResolutionWidth/Height` | The correlation problem (two disjoint identity spaces, GDI vs. CCD) is exactly the kind of "deceptively complex, someone already solved it" trap this section exists to flag — `CaptureState()` already returns the right data in the right coordinate space for the app's own `DevicePath` identity, no mapping needed. |

**Key insight:** Every "don't hand-roll" item above is really the same lesson restated three ways: this codebase already has a single canonical source of truth for monitor identity/position/active-state (`WindowsDisplayAPI`'s CCD data, wrapped by `IMonitorController`), and every temptation this phase introduces (re-deriving safety logic, re-deriving hotplug detection, re-deriving position data from a second, GDI-based API) is a shortcut back toward a second, competing source of truth that Phase 4/6's hard-won rig lessons (documented at length in `WindowsMonitorController.cs`'s own comments) already warn against.

## Common Pitfalls

### Pitfall 1: Self-triggered `DisplaySettingsChanged` refresh loop
**What goes wrong:** The panel's own `ActivateMonitors`/`DeactivateMonitors` calls change the CCD topology, which itself fires `WM_DISPLAYCHANGE`/`SystemEvents.DisplaySettingsChanged` — so a panel-initiated action produces a UI refresh from *two* sources (the explicit post-action `RefreshGrid()` call, and the event handler's own `PopulateMonitorPanelGrid()`).
**Why it happens:** `SystemEvents.DisplaySettingsChanged` does not distinguish "changed by this process" from "changed externally" — it is a systemwide broadcast.
**How to avoid:** Treat the double-refresh as harmless (both calls converge on the same live-queried `GetAllMonitors()` result, so the second is a no-op re-render) rather than adding suppression/debounce logic — debounce logic would add real complexity to defend against a cosmetic non-issue. Document it inline so a future editor doesn't "fix" it into a bug.
**Warning signs:** If a future editor adds actual state mutation (not just re-render) inside the refresh path, the double-fire could become a real problem — keep the refresh handler side-effect-free beyond re-populating the grid.

### Pitfall 2: `Screen.AllScreens` identity mismatch
**What goes wrong:** Using `System.Windows.Forms.Screen.AllScreens` to position the Identify overlay (or to try to answer "which monitor is this panel row") silently breaks correlation, because `Screen.DeviceName` (GDI, e.g. `\\.\DISPLAY1`) and `MonitorInfo.DevicePath`/`MonitorPathSnapshot.DevicePath` (CCD target device path, e.g. `\\?\DISPLAY#...#{GUID}`) are different identity spaces with no built-in mapping.
**Why it happens:** Both APIs describe "the same" physical monitors, so it's tempting to assume they share an identity key; they don't.
**How to avoid:** Use `CaptureState()`'s `MonitorPathSnapshot` (CCD-sourced) for all position/size needs, as in Pattern 6 above. Never introduce `Screen.AllScreens` into this codebase's monitor-identity logic.
**Warning signs:** Any code that tries to `.FirstOrDefault(s => s.DeviceName == devicePath)` against `Screen.AllScreens` — this comparison will never match.

### Pitfall 3: Multi-monitor DPI scaling and overlay placement
**What goes wrong:** On a mixed-DPI multi-monitor setup, `Form.Location`/`Size` set from raw CCD pixel coordinates can be mispositioned or mis-sized if the app's DPI-awareness mode doesn't match what the coordinates assume — a well-documented general WinForms multi-monitor pitfall, not specific to this codebase.
**Why it happens:** `RigToggle.App.csproj` has no explicit `<ApplicationHighDpiMode>` setting or app manifest (confirmed via grep — none found), so it runs under whatever the .NET 10 WinForms SDK default is; CCD `PathInfo.Position`/`Resolution` values are always in raw physical pixels regardless of DPI mode.
**How to avoid:** This is exactly the kind of issue this project's own established convention (extensive rig-hardware verification for every CCD-adjacent feature, per Phase 1/4/6/16's spike-and-rig-retest pattern) exists to catch — flag the Identify overlay's on-screen accuracy (correct monitor, correct full coverage, no offset) as a required rig-verification checkpoint in the plan, not something to resolve by inspection alone.
**Warning signs:** An overlay that appears offset, clipped, or on the wrong monitor specifically when the rig's monitors have different scaling factors.

### Pitfall 4: Concurrency between the panel and the Rig/Normal toggle path
**What goes wrong:** `WindowsMonitorController` is a stateful singleton (holds `_originalPathsCache`, constructed once in `Program.cs`'s composition root and shared by every caller). If a panel action and a Rig/Normal toggle (triggered via hotkey or tray menu, which do not require `MainForm`'s window to be enabled/focused, so they can technically fire while a non-modal panel is open) run close together, both mutate the same live CCD topology and the same in-process cache field, with no serialization between them today — `ToggleOrchestrator`'s existing busy-guard (`CORE-06`) only protects `ToggleService.ToggleToRigMode`/`ToggleToNormalMode` against each other; it has no visibility into panel-initiated `IMonitorController` calls, since the panel is deliberately designed (Pattern 2) to bypass `ToggleOrchestrator` entirely.
**Why it happens:** This app is single-threaded (WinForms UI thread only), so true simultaneous execution is not possible, but non-modal windows and background triggers (global hotkey `WM_HOTKEY`, tray menu clicks) mean two *sequential-but-close* mutation calls from different entry points are possible without any deliberate double-click.
**How to avoid:** Flagged as an Open Question below rather than asserted as a required fix — the planner should decide whether the panel needs to share (or duplicate, in a lighter-weight form) `ToggleOrchestrator`'s `Interlocked.CompareExchange` busy-guard so a panel action and a Rig/Normal toggle can never race against the same stateful `WindowsMonitorController` instance.
**Warning signs:** A panel action's `DeactivateMonitors` throwing an unexpected/generic CCD validation error (rather than the clean zero-survivors message) shortly after a hotkey/tray toggle, or vice versa.

### Pitfall 5: `DataGridViewCheckBoxColumn`/button-column dirty-state commit timing
**What goes wrong:** If the panel reuses a checkbox- or button-column-based grid interaction model, a `DataGridViewCheckBoxCell`'s value doesn't commit until the cell loses focus, so a click-driven action can silently act on the *previous* cell value unless the dirty state is force-committed on the same click.
**Why it happens:** This is a `DataGridView` framework quirk, already discovered and worked around in this exact codebase (`SettingsForm.DgvMonitors_CurrentCellDirtyStateChanged`, `src/RigToggle.App/SettingsForm.cs:494-500`).
**How to avoid:** If the panel's row actions are `DataGridViewButtonColumn` clicks (recommended — a button click commits immediately, unlike a checkbox toggle) rather than checkboxes, this pitfall does not apply at all; if the panel instead models Enable/Disable as a checkbox column (mirroring `SettingsForm`'s Disable/Enable columns), reuse the exact `CurrentCellDirtyStateChanged` → `CommitEdit(DataGridViewDataErrorContexts.Commit)` pattern already proven there.
**Warning signs:** A row's Enable/Disable action appearing to require two clicks to take effect.

## Code Examples

See the six numbered patterns above (Architecture Patterns section) — each includes a source-cited code example rather than duplicating them here.

## State of the Art

Not applicable in the traditional "library ecosystem evolved" sense — this phase's domain is entirely internal-codebase reuse (Phase 16's unified controller-call shape) plus long-stable .NET BCL/WinForms facilities (`SystemEvents`, `DataGridView`, borderless `Form`s), none of which have had breaking or deprecating changes relevant to this project's `net10.0-windows` target.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The panel should be a non-modal `Form` (not `ShowDialog()`), reachable from the tray menu, to satisfy PANEL-03's "live update while panel is open" without blocking the rest of the app. | Architecture Patterns / Recommended Project Structure | LOW-MEDIUM — if the planner/user prefers a modal panel instead, PANEL-03 can still technically work (WinForms' nested modal message loop still pumps thread-wide `SystemEvents` notifications), but the concurrency question in Pitfall 4/Open Questions becomes more contained (a modal panel blocks the mouse/keyboard on `MainForm`, though not `WM_HOTKEY` delivery — see Pitfall 4). This is a UX/architecture choice, not a technical requirement, and should likely be confirmed via `/gsd:discuss-phase` before planning locks it in. |
| A2 | A runtime-drawn status dot (`Graphics.FillEllipse`) is preferable to a new embedded `.ico`/`.png` asset pair for PANEL-01's icon. | Standard Stack / Alternatives Considered, Pattern 3 | LOW — purely a style choice; either approach satisfies PANEL-01's "icon, not just text" requirement equally. |
| A3 | The panel should NOT share `ToggleOrchestrator`'s exact busy-guard type, but the planner should decide whether some serialization is needed between panel actions and Rig/Normal toggles. | Common Pitfalls (Pitfall 4), Open Questions | MEDIUM — if no serialization is added and the concurrency scenario is more reachable in practice than this research estimates (e.g. if the rig's real usage pattern involves leaving the panel open while also using the hotkey), a real race against `WindowsMonitorController`'s stateful `_originalPathsCache` could produce a confusing CCD validation failure. Recommend the planner explicitly resolve this via a plan-level decision (add a lightweight shared guard, or accept the risk with rig verification) rather than leaving it implicit. |

## Open Questions

1. **Should panel monitor actions be serialized against `ToggleOrchestrator`'s busy state?**
   - What we know: `ToggleOrchestrator` has an `Interlocked.CompareExchange`-based busy-guard protecting `ToggleService.ToggleToRigMode`/`ToggleToNormalMode` against each other, but the panel is architecturally required (PANEL-02, Pattern 2) to bypass `ToggleOrchestrator` and call `IMonitorController` directly. `WindowsMonitorController` is a shared, stateful singleton.
   - What's unclear: How reachable the race actually is in real single-user rig usage (requires a non-modal panel open + a near-simultaneous hotkey/tray toggle), and whether the cost of adding a second guard is worth it for a low-probability race in a single-user tool.
   - Recommendation: Surface this as a discuss-phase or plan-time decision. A cheap mitigation: expose a `ToggleOrchestrator.IsBusy` read-only pass-through and have the panel refuse a mutation (with a clear message) while a toggle is in flight — this reuses the existing flag without requiring the panel to route through `ToggleService`.

2. **Exact panel entry point: tray-only, or also reachable from `MainForm`?**
   - What we know: The existing tray context menu (`trayToggleMenuItem`/`traySettingsMenuItem`/`trayExitMenuItem`) is the natural place for a new "Monitors..." entry, matching the app's tray-resident design (Phase 8). `MainForm` itself currently only has a Toggle button and a Settings button.
   - What's unclear: Whether the user also wants a `MainForm` button (like `btnSettings`) for the panel, or tray-menu-only access is sufficient.
   - Recommendation: Add to the tray menu at minimum (consistent, always-available entry point regardless of whether `MainForm` is visible); adding a `MainForm` button too is a low-cost additive option the planner can decide on. This should ideally be confirmed via discuss-phase given the phase's `**UI hint**: yes` marker in ROADMAP.md.

3. **Does Identify's "number" need to be user-configurable/persistent, or is a fresh 1..N assignment on each Identify click sufficient?**
   - What we know: PANEL-05 only requires "briefly overlays a number on each physical screen" — no requirement ties the number to a specific persisted monitor identity across sessions.
   - What's unclear: Nothing blocking — this is confirmed sufficient as a fresh, session-local, grid-row-order-derived assignment (Pattern 6).
   - Recommendation: No persistence needed; assign numbers from the panel's current row display order each time Identify is invoked.

## Environment Availability

Not applicable — this phase introduces no new external tool, service, or runtime dependency. Everything required (`net10.0-windows` WinForms SDK, the already-referenced `WindowsDisplayAPI` NuGet package, in-box `Microsoft.Win32.SystemEvents`) is already present and verified working in the existing solution (`dotnet build RigToggle.sln` succeeds per `16-04-SUMMARY.md`).

## Security Domain

`security_enforcement` is not explicitly disabled in `.planning/config.json`, so this section is included per protocol, but almost all ASVS categories are not meaningfully applicable to this phase: Rig Toggle is a single-user, local-only, non-networked Windows desktop utility with no authentication, session, or remote-access surface, and this phase adds no new attack surface beyond what Phase 4/6 already introduced (CCD mutation via a non-elevated, single local user's own account).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Single local user, no auth boundary — out of scope for this entire project (established since v1.0). |
| V3 Session Management | No | No sessions — desktop app. |
| V4 Access Control | No | No multi-user/role concept. |
| V5 Input Validation | Marginally | The only "input" this phase introduces is which `DevicePath` the user clicks in the panel grid — already constrained to values `GetAllMonitors()` itself enumerated (Tag-keyed rows, same pattern as `SettingsForm`), so there is no free-text or externally-supplied input to validate. No new validation logic is needed beyond what `WindowsMonitorController.DeactivateMonitors`'s own "not currently active"/"not detected" guards already provide. |
| V6 Cryptography | No | No secrets/crypto involved in this phase. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Malformed/spoofed `DevicePath` reaching `DeactivateMonitors` (e.g. if a future change let the panel accept a device path from an untrusted source) | Tampering | Not a realistic concern for this phase — panel rows are always populated directly from `GetAllMonitors()`'s own live enumeration (Tag-keyed by the same `DevicePath` string it just returned), never from user-typed or externally-supplied text. No new mitigation needed beyond continuing this pattern. |

## Sources

### Primary (HIGH confidence)
- `src/RigToggle.Core/ToggleService.cs` (read directly) — confirmed both `ToggleToRigMode`/`ToggleToNormalMode` call `DeactivateMonitors` and the shared `ReconcileModeAfterMonitorFailure` CR-01 helper.
- `src/RigToggle.Windows/WindowsMonitorController.cs` (read directly) — confirmed exact zero-survivors guard location/text, `CaptureState()`'s `MonitorPathSnapshot` shape, and the extensive documented Screen-API-avoidance doctrine (D-04).
- `src/RigToggle.Windows/WindowsThemeProvider.cs` (read directly) — confirmed the `SystemEvents`-subscription + marshal-then-diff idiom already established in this codebase.
- `src/RigToggle.App/MainForm.cs`, `SettingsForm.cs`, `MonitorConfirmDialog.cs`, `Program.cs` (read directly) — confirmed existing tray-menu structure, `DataGridView` row-population/Tag-identity pattern, `SkipMonitorConfirmation` confirmation-gate flow, and composition-root wiring conventions.
- `.planning/phases/16-normal-mode-explicit-monitor-config-mode-store-redesign/16-03-SUMMARY.md`, `16-04-SUMMARY.md` (read directly) — confirmed Phase 16 already generalized the zero-survivors guard message to be mode-agnostic and reachable from both directions, and confirmed the `StartupRecoveryChecker`/composition-root wiring pattern.
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md`, `.planning/STATE.md` (read directly) — confirmed requirement text, phase goal/success criteria, and the "manual panel is independent/ad-hoc" out-of-scope framing.

### Secondary (MEDIUM confidence)
- [SystemEvents.DisplaySettingsChanged Event (Microsoft.Win32) | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=windowsdesktop-8.0) — confirmed the event fires on display-configuration changes including connect/disconnect, corroborating the codebase's existing analogous use of `SystemEvents.UserPreferenceChanged`.

### Tertiary (LOW confidence)
None — every claim in this document is either sourced directly from the codebase or corroborated by official Microsoft documentation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, every capability traced to already-referenced/in-box APIs.
- Architecture: HIGH — DISPLAY-12's resolution is a direct, verified code-reading finding (not an inference); PANEL-01/02/03/04/05 patterns are all direct analogs of existing, working code in this same codebase.
- Pitfalls: MEDIUM-HIGH — Pitfalls 1/2/5 are HIGH confidence (directly sourced from existing code/comments); Pitfall 3 (DPI) and Pitfall 4 (concurrency) are informed-but-untested judgment calls flagged explicitly as Open Questions/Assumptions rather than asserted as settled facts, since this app has no existing multi-monitor-DPI or panel-vs-toggle-concurrency precedent to point to.

**Research date:** 2026-08-08
**Valid until:** 30 days (stable, internal-codebase-driven domain; no fast-moving external dependency risk)
