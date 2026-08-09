---
phase: 19-monitor-tile-dashboard-monitorpanelform-retirement
plan: 01
subsystem: RigToggle.App (WinForms UI)
tags: [winforms, gdi+, owner-drawn-control, monitor-tile]
dependency-graph:
  requires: []
  provides:
    - MonitorIconGeometry (RigToggle.App namespace) - static GDI+ geometry helper
    - MonitorTile (RigToggle.App.Controls namespace) - presentational UserControl
  affects:
    - Plan 02 (hosts MonitorTile instances on MainForm, wires ThemeApplier)
    - Plan 03 (wires tile-click mutation logic in MainForm)
tech-stack:
  added: []
  patterns:
    - "Owner-drawn UserControl (this codebase's first) with SetStyle(UserPaint | AllPaintingInWmPaint | OptimizedDoubleBuffer | ResizeRedraw | Selectable)"
    - "Fractional OnPaint geometry (no pixel literals) for DPI-safety, matching IconGeometry.cs's existing idiom"
    - "Stroke-then-fill vs. fill-only compositing choice documented per D-02 (ON state needs no CR-01 seam workaround; OFF state's seams are intentional)"
key-files:
  created:
    - src/RigToggle.App/MonitorIconGeometry.cs
    - src/RigToggle.App/Controls/MonitorTile.cs
  modified: []
decisions:
  - "MonitorTile's four theme Color properties (AccentColor/IconOffColor/HoverBackColor/FocusRingColor) required [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] to satisfy the .NET 10 SDK's new WFO1000 analyzer (errors, not warnings, in this SDK) on public Color properties of a Control subclass with no designer-serialization configuration. Deviation not anticipated by the plan; fixed inline (Rule 1/3 - blocking build error), zero runtime behavior change since this control is never designer-hosted."
metrics:
  duration_minutes: 35
  completed: 2026-08-09
---

# Phase 19 Plan 01: Monitor-Tile Icon Geometry & Presentational Tile Control Summary

Ported the monitor-silhouette GDI+ geometry from `RigToggle.IconGen` into a new `MonitorIconGeometry` helper and built `MonitorTile`, this codebase's first owner-drawn `UserControl` — a presentational-only, keyboard-accessible tile that paints a monitor icon, on/off state, primary badge, and number entirely from fractional `ClientSize`-derived geometry.

## What Was Built

**`src/RigToggle.App/MonitorIconGeometry.cs`** (228 lines) — `internal static class MonitorIconGeometry`:
- `BuildMonitorPath(float x, float y, float w, float h)` — ported screen/neck/base silhouette, offsettable (unlike the IconGen port source, which only draws at the origin), built from the exact 13 fractional constants (`ScreenX`/`ScreenY`/`ScreenW`/`ScreenH`/`ScreenRadius`, `NeckX`/`NeckY`/`NeckW`/`NeckH`, `BaseX`/`BaseY`/`BaseW`/`BaseH`/`BaseRadius`) copied verbatim from `RigToggle.IconGen/IconGeometry.cs`.
- `DrawTileIcon(Graphics g, RectangleF bounds, bool isActive, Color activeColor, Color outlineColor)` — D-02's two-signal ON/OFF state: ON fills solid (`FillPath` only — a single fill cannot produce the CR-01 seam artifact, so no stroke-then-fill compositing is needed), OFF strokes a hollow outline only (`DrawPath` only, deliberately showing interior sub-shape seams as the intended wireframe look).
- `DrawPrimaryBadge(Graphics g, PointF center, float diameter)` — a filled 5-point star at the theme-independent locked literal `Color.FromArgb(245, 166, 35)` (D-03).
- `DrawGearIcon(Graphics g, RectangleF bounds, Color color)` — an 8-tooth ring gear built with `FillMode.Alternate` for the punched-out center hole, caller-supplied color (MAIN-02/D-10).

**`src/RigToggle.App/Controls/MonitorTile.cs`** (296 lines) — `public sealed class MonitorTile : UserControl`:
- `public string? DevicePath { get; private set; }` — sole identity, set only via `SetState`.
- `public event EventHandler? ActionRequested;` — raised from exactly two places: `OnClick` (after `Focus()`) and `ProcessCmdKey` on `Keys.Space`/`Keys.Return` while focused.
- `public void SetState(MonitorInfo monitor, int displayNumber)` — the only way tile state is set; builds the locked `AccessibleName` format (`Monitor {n}: {FriendlyName}, Primary — On/Off`).
- Theme properties `AccentColor`/`IconOffColor`/`HoverBackColor`/`FocusRingColor`, each `Invalidate()`-ing on set, each marked `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]`.
- `OnPaint` (wrapped in try/catch, traces `MonitorTile.OnPaint failed: {ex}` on any exception) draws hover background, the monitor icon via `MonitorIconGeometry.DrawTileIcon`, the primary badge via `MonitorIconGeometry.DrawPrimaryBadge` when primary, the number label via `TextRenderer.DrawText`, and a focus ring when `Focused` — every dimension derived from `ClientSize` via named `*Fraction` constants, zero pixel literals.
- Never references `IMonitorController`, `ToggleOrchestrator`, `ISettingsStore`, `MonitorConfirmDialog`, or `MonitorIdentifyOverlay` in code (only in the class doc comment, verified by grep audit).

Nothing hosts either file yet — `MainForm` is untouched. Plan 02 wires these into the dashboard.

## Verification

- `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` — 0 Warning(s), 0 Error(s).
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` — `Passed: 81, Failed: 0, Total: 81` (matches pre-phase baseline, no regression).
- `git diff --stat` from the pre-plan commit to HEAD shows exactly two new files (`MonitorIconGeometry.cs`, `MonitorTile.cs`), zero modifications to any existing file.
- Every acceptance-criteria grep from both tasks (constant presence, entry-point signatures, `FillPath`/`DrawPath` counts, badge literal, no `IconGen` project reference, no `PackageReference`, no `new Bitmap`, `TabStop`/`ControlStyles.Selectable`/`ProcessCmdKey`/`Keys.Space`/`Keys.Return` presence, exactly 2 `ActionRequested?.Invoke` call sites, presentational-only audit = 0 violations, DPI fraction-literal audit = 0 stray pixel literals) ran and passed.
- Visual correctness (icon proportions, badge placement, focus ring, DPI behavior) is not provable in this Linux build environment — deferred to Plan 05's rig checkpoint, per the plan's own verification note.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] WFO1000 build errors on MonitorTile's theme Color properties**
- **Found during:** Task 2, first build attempt
- **Issue:** The .NET 10 SDK's WinForms analyzer (`WFO1000`) reported a build **error** (not warning) for each of the four public `Color` properties (`AccentColor`, `IconOffColor`, `HoverBackColor`, `FocusRingColor`) on `MonitorTile : UserControl`, because none configured designer-serialization behavior. Not anticipated by the plan (no prior owner-drawn `UserControl` exists in this codebase to have hit this before).
- **Fix:** Added `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]` to each property — the standard fix per the analyzer's own linked guidance (`aka.ms/winforms-warnings/wfo1000`), correct here since `MonitorTile` is a code-only control never dropped on a designer surface. Zero runtime behavior change.
- **Files modified:** `src/RigToggle.App/Controls/MonitorTile.cs`
- **Commit:** `5ddc05f` (included in the same commit that introduced the properties — caught before the first commit was made, not a follow-up fix)

## Known Stubs

None — both files are additive, unwired building blocks by design (per the plan's objective); nothing renders empty/placeholder data since nothing is hosted yet.

## Threat Flags

None. Both new files match the plan's own `<threat_model>` register (T-19-01/02/03, T-19-SC) exactly — no new surface introduced beyond what that register already covers.
