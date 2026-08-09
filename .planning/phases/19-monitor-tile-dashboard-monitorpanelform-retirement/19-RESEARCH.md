# Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement - Research

**Researched:** 2026-08-09
**Domain:** WinForms owner-drawn UI (custom UserControl dashboard), Form-lifecycle absorption, GDI+ icon compositing — Rig Toggle v2.1
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tile Visual Design**
- **D-01:** Each tile's monitor icon reuses the existing monitor-silhouette motif from Phase 13's `RigToggle.IconGen` (the same glyph language already used for the normal-mode tray/exe icon) rather than a new, simpler glyph — keeps the icon vocabulary consistent across tray, exe, and now the tile dashboard.
- **D-02:** On/off status must be unmistakable at a glance — implemented as a **combined outline + color shift**: an OFF tile renders the monitor icon as a hollow/gray outline (no fill); an ON tile renders it solid-filled in an active color. Two independent visual signals (shape AND color), not a single dot/badge.
- **D-03:** The primary monitor's tile gets a small distinguishing badge/marker (exact glyph/placement left to planning — e.g. a small "P" corner badge).

**MainForm Sizing & Layout**
- **D-04:** MainForm auto-sizes based on the actual detected monitor count rather than staying a fixed size.
- **D-05:** Auto-sizing has a width cap: once enough tiles would make the window unreasonably wide (informally "~4-5 tiles"), additional tiles wrap onto a second row instead of the window continuing to widen indefinitely.
- **D-06:** The window resizes live when the tile row changes — both on hotplug (TILE-06) and on first-open.
- **D-07:** Tiles are a **fixed size** regardless of monitor count — they never shrink to cram more into one row. More monitors means more wrapping (per D-05), not smaller tiles.
- **D-08:** Tiles are ordered by monitor number (1, 2, 3…), not by attempting to infer physical desk position — simple, stable, matches how Windows itself numbers displays, and doesn't reorder on hotplug.

**Settings & Identify Placement**
- **D-09:** Vertical order, top to bottom: **tile row → shared Identify button → Rig/Normal toggle → (space) → Settings**, per MAIN-01/MAIN-02.
- **D-10:** Settings becomes a **small icon-only gear button** in a bottom corner — not a labeled text button, even a small one.
- **D-11:** Identify stays a **single shared button** (not a per-tile action) that numbers every screen at once when clicked, directly porting `MonitorPanelForm.BtnIdentify_Click`'s existing behavior (TILE-04) rather than adding N per-tile identify affordances.

### Claude's Discretion
- Exact tile dimensions (pixel size), spacing between tiles, and the specific width threshold (in tile-count or pixels) that triggers wrapping to a second row — should keep the window proportionate at 2 monitors (the user's actual rig) through at least 4-5.
- Exact visual treatment of the primary-monitor badge (D-03) — icon/glyph choice, corner placement, size — as long as it's clearly a marker distinct from the on/off outline+fill signal (D-02).
- Exact gear-icon geometry for the Settings button (D-10) — hand-drawn via GDI+ consistent with `RigToggle.IconGen` conventions, or a simpler inline `OnPaint` glyph.
- Whether the live window-resize (D-06) uses an animated/smooth transition or an instant resize — default to instant unless research surfaces a strong reason otherwise (it does not; see Pitfalls).
- Exact mechanism for porting `MonitorPanelForm`'s mutation logic, `BeginExclusiveMonitorAccess()` lease, hotplug subscription, and `MonitorConfirmDialog` gating into `MainForm` — architecturally scoped below (dumb `MonitorTile` UserControl raising events, `MainForm` remains sole `IMonitorController` caller).
- Whether wrapping to a second row also affects the shared Identify button/toggle/Settings vertical positions — natural consequence of D-04/D-06's auto-sizing, left to planning to lay out correctly.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. THEME-08 (toggle-switch visual redesign) is explicitly Phase 20's scope, not this phase's — Phase 19 only repositions the existing plain-button toggle per MAIN-01, it does not restyle it.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TILE-01 | One tile per detected monitor, icon+number, status via icon not text | `MonitorTile` UserControl pattern below; `GetAllMonitors()` data source confirmed (no built-in "monitor number" field — see Pitfall 6/Assumption A1); stroke-then-fill icon rendering (Code Examples) |
| TILE-02 | Clicking a tile toggles that monitor directly, taking effect immediately | Pattern 1 (dumb tile raises event, MainForm owns mutation) — direct port of `MonitorPanelForm.DgvMonitorPanel_CellClick` → `DisableMonitor`/`EnableMonitor` |
| TILE-03 | `SkipMonitorConfirmation` gate preserved | `MonitorConfirmDialog` reused unchanged, ported verbatim into `MainForm`'s tile-click handler (Code Examples) |
| TILE-04 | Identify action near the tiles overlays a number on each screen | `MonitorIdentifyOverlay`/`CaptureState()` reused unchanged, `Owner` retargeted from `MonitorPanelForm` to `MainForm`; numbering-order pitfall documented (Pitfall 6) |
| TILE-05 | Tab moves focus between tiles; Space/Enter toggles focused tile | `MonitorTile : UserControl` with `TabStop=true`, `OnKeyDown` handling (Code Examples) — `Button`'s free keyboard affordances are NOT inherited by a custom control (Pitfall 4) |
| TILE-06 | Tile row refreshes live on hotplug while MainForm is visible | New `SystemEvents.DisplaySettingsChanged` subscription on `MainForm` (does not currently exist there) — lifecycle pattern in Pitfall 2 |
| TILE-07 | `MonitorPanelForm` + both entry points removed | Build-order section (port-then-delete sequencing) |
| MAIN-01 | Toggle sits directly below tile row | Layout section (D-09 vertical order) |
| MAIN-02 | Settings relocated to secondary/de-emphasized position | Layout section (D-10 icon-only gear button) |
</phase_requirements>

## Summary

This phase is a **port-and-adapt exercise**, not new-territory engineering: every sub-behavior TILE-01 through TILE-07 requires has a directly portable existing implementation in `MonitorPanelForm.cs`, `MonitorConfirmDialog.cs`, and `MonitorIdentifyOverlay.cs`, all read directly from the current `src/` tree for this research. The only genuinely new code is (1) a `MonitorTile : UserControl` that owner-draws an icon+number+status and raises a click/keyboard event, and (2) the auto-sizing/wrapping tile-strip host inside `MainForm`. Zero new NuGet packages — this stays a pure WinForms/GDI+ extension of patterns already proven in this codebase (owner-drawn status dots in `MonitorPanelForm.CreateStatusDot`, stroke-then-fill `GraphicsPath` compositing in `RigToggle.IconGen/IconGeometry.cs`).

**Primary recommendation:** Build `MonitorTile` as a standalone, unwired `UserControl` first (icon rendering + keyboard focus only), wire it into `MainForm` read-only, then port `MonitorPanelForm`'s mutation/lease/hotplug/Identify logic verbatim before deleting `MonitorPanelForm` and its two entry points last — this is the exact "prove the replacement before deleting the original" sequencing both the milestone-level research and this phase's own architecture require.

Two findings from direct source inspection this session materially change what the planner must scope, beyond what the milestone-level research already flagged:

1. **`RigToggle.IconGen` is a dev-time-only console tool, never referenced by `RigToggle.App` at runtime** (confirmed by its own `Program.cs` doc comment: *"Never referenced by RigToggle.App -- this project is not part of the shipped self-contained publish"*). D-01 ("tiles reuse RigToggle.IconGen's monitor motif") therefore requires **porting the `BuildMonitorPath` geometry constants/logic into `RigToggle.App`** (e.g., a new internal helper class), not referencing `IconGen` as a library — `IconGen`'s output is static pre-rendered `.ico` bytes, but tiles need to render the same silhouette live, in multiple states (outline/filled, badged), which a pre-rendered bitmap cannot do.
2. **`MonitorInfo` has no "monitor number" field.** `GetAllMonitors()`/`GetActiveMonitors()` return `DevicePath`/`FriendlyName`/`IsPrimary`/`IsActive` only — the only place a "number" currently exists in this app is `MonitorPanelForm.BtnIdentify_Click`'s own local loop counter (`int number = 1`), assigned by iterating grid rows **in display order**, not any inherent monitor identity. D-08's "ordered by monitor number" therefore means "ordered by the app's own stable display/enumeration order," and the tile dashboard's numbering must reuse the exact same assignment scheme Identify already uses so the two stay consistent (see Pitfall 6).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Monitor tile rendering (icon/number/status/badge) | Client (WinForms `UserControl`, `RigToggle.App`) | — | Pure presentation; owns no controller reference (Anti-Pattern 1 below) |
| Monitor enumerate/mutate (Activate/DeactivateMonitors) | Client-hosted domain layer (`RigToggle.Windows.WindowsMonitorController` via `IMonitorController`) | — | Unchanged — CCD API adapter, sole owner of the DISPLAY-12 "at least one monitor" guard |
| Tile-click → mutation orchestration (lease, confirm dialog, mutate, refresh) | Client (`MainForm`, `RigToggle.App`) | Core (`ToggleOrchestrator.BeginExclusiveMonitorAccess()`) | `MainForm` is the sole caller of both the controller and the orchestrator lease — same pattern `MonitorPanelForm` already established |
| Hotplug detection | OS (`Microsoft.Win32.SystemEvents.DisplaySettingsChanged`) | Client (`MainForm` subscriber) | OS pushes the notification; `MainForm` is the (new) sole subscriber for this capability going forward |
| Window auto-sizing / tile-wrap layout | Client (WinForms layout engine — `FlowLayoutPanel`/`TableLayoutPanel` + `MainForm.AutoSize`) | — | Pure client-side layout math, no domain logic |
| Settings persistence (`SkipMonitorConfirmation`) | Client-hosted domain layer (`RigToggle.Core.AppSettings` via `ISettingsStore`) | — | Unchanged — same JSON file, same store |

This is a single-process, single-user desktop utility with no server/API/database tiers — the table above is intentionally narrow (WinForms client + one Core/domain layer + Windows OS APIs), matching the project's existing 4-project architecture (`RigToggle.App`/`RigToggle.Core`/`RigToggle.Windows`/`RigToggle.Tests`).

## Project Constraints (from CLAUDE.md)

- **Platform:** Windows only, standalone self-contained .NET 10 `.exe` — this phase adds zero new Windows APIs beyond what `MonitorPanelForm` already uses (CCD via `WindowsDisplayAPI`, already-proven).
- **No third-party UI component packages** — CLAUDE.md's "What NOT to Use" table explicitly rejects Krypton/MetroFramework/Guna/Infragistics/DevExpress/MaterialSkin-class dependencies for exactly this kind of custom control; `MonitorTile` must be hand-rolled `Control`/`UserControl` + GDI+, continuing the project's zero-new-dependency track record (three milestones running per the milestone-level STACK.md).
- **`PublishTrimmed` stays `false`** — unrelated to this phase's own code, but any new P/Invoke or reflection-adjacent code (none expected here) must not assume trimming is safe.
- **No elevation manifest** — this phase touches no elevation-sensitive surface; `MonitorTile`/`MainForm` changes are ordinary UI code.
- **`AutoScaleMode.Font`, no `ApplicationHighDpiMode` set in `RigToggle.App.csproj`** (confirmed by direct read this session — no `<ApplicationHighDpiMode>` property exists in the `.csproj`, and `MainForm.Designer.cs`/`MonitorPanelForm.Designer.cs` both set `AutoScaleMode.Font`) — this is a pre-existing gap the new owner-drawn tile geometry must design around (derive from `ClientSize`/`Font.Height`, never hardcode pixel literals), not something this phase is expected to fix project-wide.
- **GSD workflow enforcement**: this phase must be planned/executed via `/gsd:plan-phase` → `/gsd:execute-phase`, not direct repo edits.

## Standard Stack

### Core

No new libraries. This phase is 100% additive WinForms/GDI+ code inside the already-referenced projects.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Windows.Forms` (BCL, `net10.0-windows`) | Ships with .NET 10 SDK (already the project's TFM, confirmed in `RigToggle.App.csproj`) | `UserControl`, `FlowLayoutPanel`/`TableLayoutPanel`, `Control.OnPaint` | Already the project's only UI framework; no version change needed |
| `System.Drawing`/`System.Drawing.Drawing2D` (BCL) | Ships with .NET 10 SDK | `GraphicsPath`, `SolidBrush`, `Pen`, `SmoothingMode.AntiAlias` | Already used identically in `MonitorPanelForm.CreateStatusDot` and `RigToggle.IconGen/IconGeometry.cs` — this phase extends the exact same idiom, not a new one |

### Supporting

Not applicable — no supporting/optional libraries are needed for this phase's scope (THEME-07/accent color and THEME-08/toggle-switch styling are explicitly Phase 20/21 scope, not this phase's).

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-rolled `MonitorTile : UserControl` in a `FlowLayoutPanel` | `ListView` in `View.Tile` mode (built-in, zero extra code) | Rejected — `ListView` composes native Win32 common controls, which per .NET 10's dark-mode docs needs the manual `ControlStyles.ApplyThemingImplicitly` opt-in this codebase has never used/rig-tested, and its selection/`ItemActivate` model fits "click anywhere on the tile to toggle it" and Tab/Space/Enter (TILE-05) worse than a custom `Click`/`OnKeyDown` handler |
| Hand-rolled `MonitorTile` | Third-party tile/dashboard control suite (Krypton `TilePanel`, Infragistics, MetroFramework tiles) | Rejected per CLAUDE.md's explicit "What NOT to Use" — breaks the zero-dependency track record and introduces a second theming system to keep in sync with `ThemeApplier` |
| `FlowLayoutPanel` with `AutoSize`+`MaximumSize` cap for wrap-then-grow | Manual `TableLayoutPanel` with computed row/col count | Either works; `FlowLayoutPanel.WrapContents=true` + `AutoSize=true` + `MaximumSize.Width` is less code (no manual row/col math) and is the same technique the (never-built) v2.0-era manual-panel stack research already recommended for this exact tile-grid shape — see Code Examples |

**Installation:**
```bash
# No installation needed -- zero new NuGet packages this phase.
```

**Version verification:** N/A — no new package references. Confirmed via direct read of `RigToggle.App.csproj` (no `<PackageReference>` items exist in that file at all today; the two `<ProjectReference>`s to `RigToggle.Core`/`RigToggle.Windows` are unchanged by this phase).

## Package Legitimacy Audit

Not applicable — this phase installs zero external packages (confirmed: `RigToggle.App.csproj` has no `<PackageReference>` items, and this phase adds none). The Package Legitimacy Gate protocol is skipped per its own "Required whenever this phase installs external packages" scope condition.

**Packages removed due to slopcheck verdict:** none (N/A — no packages evaluated).
**Packages flagged as suspicious:** none (N/A).

## Architecture Patterns

### System Architecture Diagram

```
                     ┌─────────────────────────────────────────┐
                     │              MainForm (UI thread)          │
                     │                                             │
   User click/Tab ──▶│  MonitorTile[]  (FlowLayoutPanel host)     │
   +Space/Enter       │       │ ActionRequested event               │
                     │       ▼                                     │
                     │  OnTileAction(tile)                        │
                     │       │                                     │
                     │       ├─▶ ToggleOrchestrator                │
                     │       │     .BeginExclusiveMonitorAccess()  │◀── shared _busy flag
                     │       │        (lease, held across dialog)  │    (same as Rig/Normal
                     │       │                                     │     toggle button)
                     │       ├─▶ (if disabling) MonitorConfirmDialog│
                     │       │     .ShowDialog()  ── SkipMonitor    │
                     │       │        Confirmation gate (TILE-03)   │
                     │       │                                     │
                     │       ├─▶ IMonitorController                │
                     │       │     .ActivateMonitors /              │◀── DISPLAY-12 guard
                     │       │      DeactivateMonitors               │    lives ONLY here
                     │       │                                     │
                     │       └─▶ RefreshMonitorTiles()              │
                     │             (re-enumerate GetAllMonitors(),  │
                     │              update each tile's SetState)    │
                     │                                             │
   SystemEvents ────▶│  OnDisplaySettingsChanged (NEW subscription  │
   .DisplaySettings   │   on MainForm, app-lifetime, see Pitfall 2) │
   Changed (hotplug)  │       └─▶ RefreshMonitorTiles()             │
                     │                                             │
   Identify click ──▶│  BtnIdentify_Click (ported from              │
                     │   MonitorPanelForm, D-11 single shared btn) │
                     │       └─▶ CaptureState() + N x               │
                     │            MonitorIdentifyOverlay(Owner=this)│
                     └─────────────────────────────────────────────┘
```

### Recommended Project Structure

```
src/RigToggle.App/
├── MainForm.cs / .Designer.cs        # MODIFIED — absorbs tile strip, hotplug sub, lease,
│                                       #   confirm-dialog call, Identify; drops btnMonitors,
│                                       #   trayMonitorsMenuItem, MonitorPanelForm factory
├── Controls/
│   └── MonitorTile.cs / .Designer.cs # NEW — UserControl: DevicePath, SetState(MonitorInfo,
│                                       #   int number), event ActionRequested, TabStop,
│                                       #   OnKeyDown (Space/Enter)
├── MonitorIconGeometry.cs            # NEW — ported subset of RigToggle.IconGen's
│                                       #   BuildMonitorPath fractional geometry (D-01);
│                                       #   IconGen itself stays dev-time-only, unreferenced
├── MonitorConfirmDialog.cs           # unchanged — reused as-is (TILE-03)
├── MonitorIdentifyOverlay.cs         # unchanged internally — Owner retargeted to MainForm
├── ThemeApplier.cs                   # MODIFIED — + ThemeMonitorTile (or tile self-themes
│                                       #   via IsDark, see Pitfall 1)
├── Program.cs                        # MODIFIED — drop MonitorPanelFormFactory wiring
└── (MonitorPanelForm.cs/.Designer.cs DELETED — step 5 of build order, below)
```

### Structure Rationale

- `MonitorTile` lives under a new `Controls/` subfolder in `RigToggle.App` (not `RigToggle.Core`) — it is a WinForms `UserControl`, following the same placement rule every Form/dialog in this codebase already uses.
- The ported icon geometry gets its own file (`MonitorIconGeometry.cs`) rather than being inlined into `MonitorTile.cs` — keeps the pure-drawing-math concern separable and testable in isolation, mirroring how `RigToggle.IconGen/IconGeometry.cs` is itself a separate file from `IconWriter.cs`/`Program.cs`.
- Nothing moves into `RigToggle.Core`/`RigToggle.Windows` for this phase — every new type is WinForms-specific presentation code.

### Pattern 1: Dumb presentational tile, MainForm performs the mutation

**What:** `MonitorTile` never calls `IMonitorController` itself. It exposes `SetState(MonitorInfo monitor, int displayNumber)` and a single `event EventHandler? ActionRequested` fired on click or Space/Enter while focused. `MainForm` remains the only caller of `_monitorController.ActivateMonitors`/`DeactivateMonitors`, exactly as `MonitorPanelForm` was the only caller before it.

**When to use:** Any reusable child control representing domain state that must never become a second call site for the DISPLAY-12 safety guard.

**Example:**
```csharp
public partial class MonitorTile : UserControl
{
    public string? DevicePath { get; private set; }
    public event EventHandler? ActionRequested;

    public MonitorTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                  ControlStyles.Selectable, true);
        TabStop = true;
    }

    public void SetState(MonitorInfo monitor, int displayNumber)
    {
        DevicePath = monitor.DevicePath;
        _isActive = monitor.IsActive;
        _isPrimary = monitor.IsPrimary;
        _number = displayNumber;
        Invalidate();
    }

    protected override void OnClick(EventArgs e) => ActionRequested?.Invoke(this, EventArgs.Empty);

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (Focused && (keyData == Keys.Space || keyData == Keys.Return))
        {
            ActionRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

// MainForm:
tile.ActionRequested += (s, e) => OnTileAction((MonitorTile)s!);
private void OnTileAction(MonitorTile tile)
{
    var lease = TryAcquireMonitorAccess(); // ToggleOrchestrator.BeginExclusiveMonitorAccess()
    if (lease is null) return;
    using (lease)
    {
        // ... confirm dialog if disabling (ported verbatim from
        //     MonitorPanelForm.DisableMonitor, TILE-03), then:
        _monitorController.DeactivateMonitors(new HashSet<string> { tile.DevicePath! });
        // or ActivateMonitors -- same two calls MonitorPanelForm used, unchanged
    }
    RefreshMonitorTiles();
}
```

### Pattern 2: Auto-size-then-wrap tile strip (D-04/D-05/D-06/D-07)

**What:** Host tiles in a `FlowLayoutPanel` with `AutoSize = true`, `AutoSizeMode = AutoSizeMode.GrowAndShrink`, `WrapContents = true`, and a `MaximumSize` whose `Width` is the pixel-width cap (computed from `tileWidth * maxTilesPerRow + gap * (maxTilesPerRow - 1)`, `Height = 0` meaning unbounded). The panel then grows horizontally tile-by-tile up to the cap, and wraps to additional rows beyond it — `MainForm` itself must also be `AutoSize = true`/`GrowAndShrink` (or explicitly resize its `ClientSize` from the panel's `PreferredSize` on the panel's `Layout`/`SizeChanged` event) so the whole window grows/shrinks with the tile strip, satisfying D-04/D-06.

**When to use:** Any tile/card strip that should grow to fit N items up to a cap, then wrap, without manual row/column math.

**Example:**
```csharp
// Designer/InitializeComponent, values illustrative -- planner should tune per
// Claude's Discretion (tile size, spacing, wrap threshold):
tileStrip.AutoSize = true;
tileStrip.AutoSizeMode = AutoSizeMode.GrowAndShrink;
tileStrip.WrapContents = true;
tileStrip.FlowDirection = FlowDirection.LeftToRight;
const int TileWidth = 72, TileGap = 12, MaxTilesPerRow = 4;
tileStrip.MaximumSize = new Size(TileWidth * MaxTilesPerRow + TileGap * (MaxTilesPerRow - 1), 0);

// MainForm: propagate the panel's content size to the form itself so the WHOLE
// window (not just the panel) grows/shrinks -- FixedDialog border style does not
// block this; only user-resize is blocked, not programmatic/AutoSize resize.
this.AutoSize = true;
this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
```

### Pattern 3: Stroke-then-fill icon compositing (D-01, D-02, ported from Phase 13)

**What:** Port `RigToggle.IconGen/IconGeometry.cs`'s `BuildMonitorPath` fractional geometry (screen/neck/base rounded-rect + rectangle) into a new `RigToggle.App` helper. For the OFF state, stroke the path with a gray/muted `Pen` and no fill (hollow outline, D-02). For the ON state, fill the path with a solid active color (no separate outline needed for a single-shape fill — the seam-artifact bug only applies when *combining multiple overlapping shapes* into one path before stroking).

**Why this matters:** `IconGeometry.cs`'s own doc comments document the exact GDI+ pitfall (`DrawPath` on a combined multi-shape path strokes each sub-figure's boundary independently, producing seams at shape overlaps) and its proven fix (stroke first at the combined path, then fill on top to overpaint interior seams). A tile's monitor icon reuses the *same combined path* (screen ∪ neck ∪ base) — the exact scenario that bug applies to — so the fix must be reused, not re-discovered.

**Example (ported and adapted from `IconGeometry.BuildMonitorPath`/`DrawNormalIcon`):**
```csharp
// New file: RigToggle.App/MonitorIconGeometry.cs
internal static class MonitorIconGeometry
{
    // Same fractional constants as RigToggle.IconGen/IconGeometry.cs -- keep in sync
    // manually (no shared project reference exists; IconGen is dev-time-only).
    private const float ScreenX = 0.125f, ScreenY = 0.125f, ScreenW = 0.75f, ScreenH = 0.5f, ScreenRadius = 0.06f;
    private const float NeckX = 0.4375f, NeckY = 0.625f, NeckW = 0.125f, NeckH = 0.125f;
    private const float BaseX = 0.28f, BaseY = 0.75f, BaseW = 0.44f, BaseH = 0.125f, BaseRadius = 0.03f;

    public static GraphicsPath BuildMonitorPath(float w, float h) { /* identical logic */ }

    public static void DrawTileIcon(Graphics g, RectangleF bounds, bool isActive, Color activeColor, Color outlineColor)
    {
        using var path = BuildMonitorPath(bounds.Width, bounds.Height);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (isActive)
        {
            using var fill = new SolidBrush(activeColor);
            g.FillPath(fill, path); // single combined-shape fill -- no stroke, no seam risk
        }
        else
        {
            using var outline = new Pen(outlineColor, Math.Max(1.5f, bounds.Width / 24f));
            g.DrawPath(outline, path); // hollow outline only, D-02's OFF state
        }
    }
}
```

### Pattern 4: Lease + confirm-dialog structure ported verbatim (TILE-02/TILE-03)

**What:** `MonitorPanelForm.DisableMonitor`/`EnableMonitor`'s exact structure — acquire `_orchestrator.BeginExclusiveMonitorAccess()` **before** `MonitorConfirmDialog.ShowDialog()`, hold across the mutation via `using`, re-validate the device path against a possibly-refreshed monitor list after the dialog's nested message loop returns (`WR-03` in `MonitorPanelForm.cs`) — must be ported into `MainForm`'s tile-click handler with **zero simplification**, even though `MainForm` already has its own, differently-shaped `BtnToggle_Click` that does not explicitly acquire this lease (it relies on `ToggleOrchestrator`'s internal `_busy` guard instead). These are not redundant despite both gating the same `_busy` flag — see Pitfall 3.

**Example:** see `src/RigToggle.App/MonitorPanelForm.cs` lines 256-320 (`DisableMonitor`) — this is the literal source to port, not a pattern to reimplement from description.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| "At least one monitor must stay enabled" safety check | A tile-local or MainForm-local re-check before calling `DeactivateMonitors` | `WindowsMonitorController.DeactivateMonitors`'s existing internal guard (DISPLAY-12) | The guard already lives in exactly one place; a second check anywhere else is exactly the drift this project's DISPLAY-12 decision was designed to prevent |
| Hotkey-vs-confirm-dialog race prevention | A new/different concurrency primitive for tile actions | `ToggleOrchestrator.BeginExclusiveMonitorAccess()` (already exists, already proven, Phase 17) | Reinventing this for tiles risks a subtly different (and unverified) race-closure guarantee than the one already rig-tested |
| Monitor icon artwork | New hand-drawn glyph, or an image asset pipeline | Ported `IconGeometry.BuildMonitorPath` fractional geometry (D-01) | Consistency with the existing tray/exe icon vocabulary is an explicit locked decision, and the geometry+stroke-then-fill fix already exists and is proven |
| Multi-frame `.ico` byte-level writing | N/A for this phase | N/A — tiles paint live via `OnPaint`, they never need to become `.ico` files | `IconWriter.cs`'s hand-rolled ICONDIR/ICONDIRENTRY logic is scoped to `IconGen`'s static asset generation only, irrelevant to live-painted controls |
| Tile-row wrap-to-second-row math | Manual `if (tileCount > threshold) { row++; col = 0; }` bookkeeping | `FlowLayoutPanel.WrapContents=true` + `AutoSize`/`MaximumSize` (Pattern 2) | WinForms already solves this; manual layout math is more code and more DPI-fragile |

**Key insight:** Every mutation-adjacent behavior this phase needs (safety guard, race prevention, confirmation gate, hotplug detection, Identify overlay) already has a working, rig-verified implementation in `MonitorPanelForm.cs`/`MonitorConfirmDialog.cs`/`MonitorIdentifyOverlay.cs`. The discipline this phase requires is **porting without "simplifying,"** not designing new mechanisms.

## Common Pitfalls

### Pitfall 1: New `MonitorTile` control silently falls outside the theming pipeline

**What goes wrong:** `ThemeApplier`'s own doc comment states it is "deliberately NOT a recursive Controls-tree walk" — every themed control is an explicit call in exactly two places: `MainForm.OnThemeChanged` and `MainForm.InitializeTrayState()` (the `--tray`-safe startup path). A new `MonitorTile` control added to only one of these two call sites will render correctly at whichever theme Windows happened to be in at startup, then freeze in that mode forever while surrounding buttons keep flipping live.

**Why it happens:** This is the exact shape of bug this codebase already hit twice during Phase 12 (missed `dgvMonitors`, then a second gap-closure round for Button/ComboBox) — a manually maintained list is the failure mode, and a brand-new control type is the easiest thing to leave off it.

**How to avoid:** Add the tile-strip's re-theme call (either a loop calling a new `ThemeApplier.ThemeMonitorTile(tile, isDark)` per tile, or each `MonitorTile` self-reading `IsDark` at paint time — see Anti-Pattern 3 below for why paint-time reads are required either way) to **both** `OnThemeChanged` and `InitializeTrayState()`, not just one.

**Warning signs:** Set Windows to Light, launch, flip to Dark live (Settings > Personalization > Colors) while the app is running — watch the tiles specifically. Repeat starting the app with `--tray`.

**Phase to address:** This phase (the tile-grid build itself).

---

### Pitfall 2: Hotplug subscription lifecycle mismatch (`MainForm` is hidden-not-closed; `MonitorPanelForm` was closable-and-reopenable)

**What goes wrong:** `MonitorPanelForm` subscribes to `SystemEvents.DisplaySettingsChanged` in its constructor and explicitly unsubscribes in `FormClosed`, because it is non-modal and closable/reopenable. `MainForm` is an app-lifetime object — `Hide()`'d to tray, never actually closed until process exit (its `FormClosed` essentially never fires during normal tray-resident operation). Copying `MonitorPanelForm`'s subscribe/unsubscribe-on-FormClosed pattern verbatim onto `MainForm` produces either harmless dead code (if `FormClosed` never fires, the subscription just lives for the app's life — accidentally correct) or an active regression (if a developer "improves" it by gating subscribe/unsubscribe on `Hide()`/`Show()`, hotplug refresh silently stops while the app is tray-resident, which is most of its runtime).

**Why it happens:** The two Forms encode genuinely different, both-correct-for-their-own-lifecycle assumptions; copying one Form's cleanup pattern onto the other's lifecycle without re-deriving it from first principles produces a plausible-looking but wrong result.

**How to avoid:** Subscribe once in `MainForm`'s constructor and **never unsubscribe** until real process exit — matching `MainForm`'s own existing, un-unsubscribed `_themeProvider.ThemeChanged += OnThemeChanged` pattern (already in the constructor today), not `MonitorPanelForm`'s pattern. TILE-06 explicitly scopes "refreshes live while MainForm is visible... not required while hidden," but subscribing for app-lifetime and simply not caring whether the window happens to be visible is both simpler and satisfies the requirement as a strict superset — do not gate the subscription on visibility.

**Warning signs:** Hide MainForm to tray (don't close the app), unplug/replug a monitor, restore from tray — the tile grid should already reflect the change on restore if the subscription stayed live.

**Phase to address:** This phase.

---

### Pitfall 3: Lease "simplification" once tile-click and `BtnToggle_Click` share one class

**What goes wrong:** `MonitorPanelForm.DisableMonitor`/`EnableMonitor` explicitly acquire `BeginExclusiveMonitorAccess()` before `ShowDialog()` because `ShowDialog()`'s nested message loop can dispatch a concurrent `WM_HOTKEY`. `MainForm.BtnToggle_Click` does **not** itself acquire this lease explicitly — it relies on `ToggleOrchestrator`'s own internal `_busy` guard. Once tile-click mutation moves into `MainForm` — the same class that already owns `BtnToggle_Click` — it becomes tempting to "unify" the two and drop the explicit lease as apparent redundancy. It is not redundant: the lease's job is blocking a *concurrent* hotkey-triggered toggle from starting **while the confirm dialog's nested message pump is running**, which has nothing to do with which class hosts the code.

**How to avoid:** Port `DisableMonitor`/`EnableMonitor`'s lease-then-`using` structure into the tile-click handler exactly as written, with a comment cross-referencing this specific rationale, and leave `BtnToggle_Click` untouched.

**Warning signs (rig-verify, not unit test):** With a global hotkey configured, click a tile to disable a monitor; while `MonitorConfirmDialog` is open, press the hotkey — it must be rejected as "toggle in progress," not proceed underneath the open dialog.

**Phase to address:** This phase.

---

### Pitfall 4: Custom tile control loses `Button`'s free keyboard affordances (TILE-05)

**What goes wrong:** A plain `Control`/`UserControl` subclass gets no Tab-focus visual, no Space/Enter activation, and no `AcceptsTab`/`IsInputKey` handling for free — `Button` provided all of this automatically, and TILE-05 explicitly requires Tab-between-tiles + Space/Enter-to-toggle.

**How to avoid:** Explicitly set `TabStop = true`, draw a visible focus-cue in `OnPaint` (e.g. a focus ring when `Focused`), and override `ProcessCmdKey` (or `OnKeyDown` with `KeyEventArgs.Handled = true`) for `Keys.Space`/`Keys.Return` to fire the same `ActionRequested` event a click does (Pattern 1's example already includes this).

**Warning signs:** Tab through the tile row with the mouse untouched — focus must visibly move tile-to-tile in a stable order (D-08's monitor-number order), and Space/Enter on a focused tile must toggle it identically to a click.

**Phase to address:** This phase.

---

### Pitfall 5: DPI/`AutoScaleMode.Font` pixel-math breakage in the new tile `OnPaint`

**What goes wrong:** `RigToggle.App.csproj` sets no `ApplicationHighDpiMode` (confirmed by direct read this session), and `MainForm`/`MonitorPanelForm` both use `AutoScaleMode.Font` — this rescales standard control `Bounds`/`Font` but has zero visibility into hardcoded pixel literals inside a custom `OnPaint`. A tile's icon/number/badge geometry computed from fixed pixel constants will look correct only at the one scale factor it was designed at (typically 100%, since this project's build environment has no Windows GUI and cannot exercise display scaling at all).

**How to avoid:** Derive all `OnPaint` geometry from `ClientSize`/`Font.Height`/`DeviceDpi` at paint time (fractional coordinates like `IconGeometry.cs` already uses, e.g. `ScreenX = 0.125f * width`), never hardcoded pixel literals.

**Warning signs:** On real Windows 11 hardware, set display scale to 125%/150% and check tile icon/number/badge alignment — this cannot be verified in this project's build environment and must be a rig checkpoint.

**Phase to address:** This phase (tile `OnPaint` geometry) — flag as a rig-only verification step in the plan.

---

### Pitfall 6: "Monitor number" is not an existing data field — numbering must be derived consistently

**What goes wrong:** `MonitorInfo` (the record `GetAllMonitors()`/`GetActiveMonitors()` return) has exactly four fields: `DevicePath`, `FriendlyName`, `IsPrimary`, `IsActive` — there is no `Number`/`Ordinal` field anywhere in the domain model. The only place a "number" currently exists in this app is `MonitorPanelForm.BtnIdentify_Click`'s local `int number = 1` loop counter, incremented while iterating **grid rows in display order** (explicitly not `state.Paths` order, per that method's own comment: *"Iterate grid rows in display order, not state.Paths order, so the overlay numbers match what the user is looking at"*). If the tile dashboard assigns its own, independently-derived ordinal (e.g., re-sorting `GetAllMonitors()`'s raw return order, which may differ from display/insertion order), tile numbers and Identify's overlay numbers could disagree for the same physical monitor.

**Why it happens:** D-08 ("ordered by monitor number") reads as if monitor number is an intrinsic property being displayed, but it is actually an **assignment the app itself makes** at render time, currently only implemented once (in Identify) and never previously needed to be consistent with a second display surface.

**How to avoid:** Populate the tile strip and assign each tile's displayed number from the **same stable list/order** the ported Identify logic iterates (i.e., derive both from one canonical, sorted `GetAllMonitors()` result — e.g. sorted by `DevicePath` for stability across hotplug, or by whatever order the tile strip itself lays tiles out in), and make Identify's numbering consume that same order rather than re-deriving its own from grid rows (which no longer exist once `MonitorPanelForm`'s `DataGridView` is gone). This is a design decision the planner must make explicit, not an incidental detail.

**Warning signs:** Click Identify and compare each overlay's number against the tile in the same position — they must match 1:1, including after a hotplug event that changes monitor count.

**Phase to address:** This phase — should be an explicit task ("derive one canonical monitor ordering shared by tile numbering and Identify"), not left implicit.

---

### Pitfall 7: `RigToggle.IconGen`'s geometry is not importable — must be ported, and the two copies can drift

**What goes wrong:** `RigToggle.IconGen` is a separate, dev-time-only console project (`OutputType=Exe`) explicitly documented as never referenced by `RigToggle.App` at runtime. D-01 requires the tile icon to visually match the tray/exe icon's monitor motif, which means copying `IconGeometry.BuildMonitorPath`'s fractional constants (`ScreenX`, `ScreenY`, `NeckX`, etc.) into a new `RigToggle.App`-local file. Once copied, the two definitions have no shared source of truth — a future edit to one (e.g., during a Phase 13-style icon tweak) will not propagate to the other unless a maintainer remembers both copies exist.

**How to avoid:** Port the constants verbatim in this phase and add an explicit doc-comment cross-reference in both files ("keep in sync manually with X — no shared project reference exists") so a future editor knows the duplication is deliberate, not an oversight. Do not attempt to solve this by adding a project reference from `RigToggle.App` to `RigToggle.IconGen` — `IconGen` is a `WinExe`-adjacent console tool with its own `Main`, not designed as a class library, and pulling it into the shipped self-contained publish would violate its own documented "not part of the shipped publish" contract.

**Warning signs:** Side-by-side visual comparison of a tile's ON-state icon against the tray icon's monitor silhouette — should be recognizably the same shape family (not necessarily pixel-identical, since tiles also carry outline/fill state variations the static icon doesn't).

**Phase to address:** This phase.

## Code Examples

### Porting the confirm-dialog + lease structure into MainForm's tile handler

```csharp
// Source pattern: src/RigToggle.App/MonitorPanelForm.cs, DisableMonitor (lines 256-320)
// Port into MainForm with the tile's ActionRequested handler as the entry point.
private void OnTileAction(MonitorTile tile)
{
    if (tile.DevicePath is not string devicePath) return;

    MonitorInfo? monitor = _lastKnownMonitors.FirstOrDefault(m => m.DevicePath == devicePath);
    if (monitor is null) return;

    IDisposable? lease = TryAcquireMonitorAccessForTile(); // wraps BeginExclusiveMonitorAccess()
    if (lease is null) return;

    using (lease)
    {
        if (monitor.IsActive)
        {
            var settings = _settingsStore.Load();
            if (!settings.SkipMonitorConfirmation)
            {
                using var confirmDialog = new MonitorConfirmDialog(
                    new[] { monitor.FriendlyName }, Array.Empty<string>(), _themeProvider);
                if (confirmDialog.ShowDialog(this) != DialogResult.OK) return;
                if (confirmDialog.DontAskAgain)
                {
                    settings.SkipMonitorConfirmation = true;
                    _settingsStore.Save(settings);
                }

                // WR-03 equivalent: re-validate against a possibly-refreshed list --
                // ShowDialog()'s nested loop can dispatch a hotplug event mid-dialog.
                if (!_lastKnownMonitors.Any(m => m.DevicePath == devicePath))
                {
                    MessageBox.Show(this, "This monitor is no longer connected.",
                        "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshMonitorTiles();
                    return;
                }
            }

            try { _monitorController.DeactivateMonitors(new HashSet<string> { devicePath }); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        else
        {
            try { _monitorController.ActivateMonitors(new HashSet<string> { devicePath }); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message, "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }

    RefreshMonitorTiles();
}
```

### Ported Identify handler, retargeted Owner, canonical numbering (Pitfall 6)

```csharp
// Source: src/RigToggle.App/MonitorPanelForm.cs, BtnIdentify_Click (lines 348-404).
// Adapted: iterate the SAME canonical ordered list RefreshMonitorTiles() used to
// number the tiles (e.g. _lastKnownMonitors, sorted once at refresh time), not
// DataGridView rows (which no longer exist). Owner is now `this` (MainForm).
private void BtnIdentify_Click(object? sender, EventArgs e)
{
    MonitorState state;
    try { state = _monitorController.CaptureState(); }
    catch (Exception ex)
    {
        MessageBox.Show(this, $"{ex.GetType().Name}: {ex.Message}", "Rig Toggle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    var snapshotsByPath = state.Paths.GroupBy(s => s.DevicePath).ToDictionary(g => g.Key, g => g.First());

    int number = 1;
    foreach (MonitorInfo monitor in _lastKnownMonitors) // same canonical order as tiles
    {
        if (!snapshotsByPath.TryGetValue(monitor.DevicePath, out MonitorPathSnapshot? snapshot)) { number++; continue; }
        if (snapshot.ResolutionWidth <= 0 || snapshot.ResolutionHeight <= 0) { number++; continue; }

        try
        {
            var overlay = new MonitorIdentifyOverlay(snapshot, number) { Owner = this };
            overlay.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"BtnIdentify_Click: overlay for {monitor.DevicePath} failed: {ex}");
        }

        number++;
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Per-monitor status as a `DataGridView` row (icon column, name column, action-button column), in a separate `MonitorPanelForm` window | Per-monitor status as a clickable `MonitorTile` UserControl, inline on `MainForm` | This phase (v2.1, Phase 19) | Matches Windows' own Settings > Display numbered-tile convention (Microsoft's own multi-monitor UI) more closely than a grid did; removes a second window/entry-point users had to know about |
| Settings as a full-width, primary-weight button (`btnSettings`, 288x32, second control on the form) | Settings as a small icon-only gear button, bottom corner, de-emphasized | This phase | Matches iCUE/Razer Synapse-style dashboard convention where per-item status is primary and app-wide configuration is a secondary affordance |
| Fixed `ClientSize` (320x200) regardless of monitor count | Auto-sizing `MainForm` with a wrap-to-second-row cap | This phase | First auto-sizing/dynamic-layout Form in this codebase — all three existing Forms (`MainForm`, `SettingsForm`, `MonitorPanelForm`) currently use fixed `ClientSize` |

**Deprecated/outdated:** `MonitorPanelForm`'s `DataGridView`-based status/action grid is fully retired this phase (TILE-07) — do not extend or patch it during this phase; it is a deletion target, not a maintenance target.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | "Monitor number" should be derived from a stable, canonical enumeration order (e.g. sorted by `DevicePath`, or by tile insertion order) shared between the tile strip and the ported Identify handler, since no such field exists in `MonitorInfo` today | Pitfall 6 / Code Examples | If the planner instead lets tile numbering and Identify numbering diverge (two independently-derived orders), TILE-04's "briefly overlays a number on each physical screen" could show a number that doesn't match the tile the user just looked at — a direct, user-visible correctness bug, not just cosmetic drift |
| A2 | ~72px tile width, ~12px gap, 4-tile-per-row wrap threshold are reasonable starting values for D-05/D-07's discretionary sizing | Pattern 2 | LOW — explicitly flagged as "Claude's Discretion" in CONTEXT.md; wrong values are a quick visual-tuning fix, not an architecture problem, and the plan should treat these as adjustable starting points confirmed on the rig, not locked numbers |
| A3 | Instant (non-animated) window resize on tile-row changes is acceptable, matching CONTEXT.md's own stated default absent a stronger reason found in research | Locked Decisions (D-06 discretion note) | LOW — explicitly the CONTEXT.md default; no counter-evidence found this session |

**If this table is empty:** N/A — see entries above. A1 is the one assumption with real correctness risk if wrong; A2/A3 are low-risk, explicitly-discretionary tuning values.

## Open Questions

1. **Should the tile strip's canonical monitor order be `DevicePath`-sorted, or "first-seen" (insertion-order-stable across refreshes)?**
   - What we know: `GetAllMonitors()`'s returned order is active-monitors-first (from `GetActiveMonitors()`), then newly-discovered inactive targets appended — this order is not guaranteed stable/sorted by any human-meaningful key (not by `DevicePath` string, not by any positional/first-plugged concept).
   - What's unclear: whether "ordered by monitor number" (D-08) should mean "sorted by `DevicePath` for determinism" (simplest, fully stable) or "whatever order `GetAllMonitors()` happens to return, held stable within a session" (matches current Identify behavior most literally, but only "stable" until a hotplug event reshuffles the underlying list).
   - Recommendation: sort by `DevicePath` (a stable string) at the top of `RefreshMonitorTiles()` before assigning both tile position and tile number — this guarantees tiles do not silently reorder between hotplug refreshes purely because of noise in enumeration order, which the current `MonitorPanelForm` implementation does not explicitly guard against (it takes `GetAllMonitors()`'s raw order as row order every refresh). Flag this as a planning decision, not settled by this research.

2. **Does `MainForm.AutoSize = true` interact correctly with `FormBorderStyle.FixedDialog` and the tray-icon/hidden-start lifecycle (`InitializeTrayState()` runs before the form is ever shown)?**
   - What we know: `FixedDialog` blocks *user* resize, not programmatic/`AutoSize`-driven resize — this is documented WinForms behavior, not something this project has proven itself yet (no existing Form in this codebase uses `AutoSize = true`).
   - What's unclear: whether `AutoSize`'s layout pass behaves identically when the form's `Handle` exists but the form was never `Show()`n (the `--tray` startup path, where `InitializeTrayState()` — not `OnLoad`/`OnShown` — does the tile-population work) — this project's own `08-RESEARCH.md Pitfall 6` history shows `Load`/`Shown`-timing assumptions have bitten this codebase before under `--tray` specifically.
   - Recommendation: verify `AutoSize` resizing occurs correctly (window is the right size the first time it's actually `Show()`n after a `--tray` launch, not just after a subsequent tile-count change) as an explicit rig checkpoint in this phase's plan — do not assume it "just works" under `--tray` without checking, per this project's own established rig-verification discipline.

## Environment Availability

Skipped — this phase has no new external dependencies (no new NuGet packages, no new OS-level services/CLIs). All Windows APIs used (`WindowsDisplayAPI`/CCD via `IMonitorController`, `SystemEvents.DisplaySettingsChanged`) are already in use by `MonitorPanelForm` today and require no new environment verification.

## Security Domain

`security_enforcement` is not explicitly disabled in `.planning/config.json` (absent under `features`), so this section is included per protocol default. However, this phase's actual attack surface is minimal: a single-user, local-only, non-networked desktop utility with no authentication, no session management, no external input parsing beyond OS-provided monitor enumeration data (already-trusted CCD API output) and local settings JSON (already covered by existing `ISettingsStore` handling). No new ASVS categories are newly *applicable* as a result of this phase's changes.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Single local user, OS-level session already trusted; no new auth surface introduced |
| V3 Session Management | No | No sessions — desktop app, no new state introduced by this phase |
| V4 Access Control | No | No multi-user/permission model exists or is introduced |
| V5 Input Validation | Marginal | Tile click/keyboard events carry no external/untrusted input beyond what `MonitorPanelForm` already validated (device-path re-validation after the confirm dialog, Pitfall 3/Code Examples) — no new validation surface, existing pattern reused |
| V6 Cryptography | No | No new cryptographic surface introduced by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Stale device-path acted upon after a confirm-dialog nested message loop (a monitor unplugged mid-dialog) | Tampering (of in-memory state, not malicious) | Re-validate `devicePath` against a freshly-enumerated list before mutating (ported verbatim from `MonitorPanelForm`'s `WR-03` fix — see Pitfall 3/Code Examples) |
| GDI handle exhaustion from per-paint bitmap/brush/pen allocation in a long tray-resident session | Denial of Service (resource exhaustion, not attacker-driven) | Cache brushes/pens/paths as instance fields, allocate once per tile instance, not per `OnPaint` call (same discipline `MonitorPanelForm._dotActive`/`_dotInactive` already establishes) |

## Sources

### Primary (HIGH confidence — direct read of current repo source, this session)
- `/home/bpivk/moza/src/RigToggle.App/MainForm.cs`, `MainForm.Designer.cs` — current control layout, mode-refresh flow, theming call sites, tray/hotkey wiring, `MonitorPanelForm` entry points to be removed
- `/home/bpivk/moza/src/RigToggle.App/MonitorPanelForm.cs`, `MonitorPanelForm.Designer.cs` — full source of every behavior being ported (status dots, grid population, hotplug handler, lease usage, Identify)
- `/home/bpivk/moza/src/RigToggle.App/MonitorConfirmDialog.cs` — confirmation dialog contract, theming pattern, `DontAskAgain` flag
- `/home/bpivk/moza/src/RigToggle.App/MonitorIdentifyOverlay.cs` — overlay construction contract, `Owner` field, CCD-snapshot-only positioning
- `/home/bpivk/moza/src/RigToggle.App/ThemeApplier.cs` — confirmed "deliberately NOT a recursive Controls-tree walk" theming pipeline shape
- `/home/bpivk/moza/src/RigToggle.App/Program.cs` — composition root wiring, `MonitorPanelFormFactory` construction to be removed
- `/home/bpivk/moza/src/RigToggle.Core/ToggleOrchestrator.cs` — `BeginExclusiveMonitorAccess()` lease implementation and rationale doc comments
- `/home/bpivk/moza/src/RigToggle.Core/Abstractions/IMonitorController.cs`, `/home/bpivk/moza/src/RigToggle.Core/Models/MonitorInfo.cs` — confirmed no "monitor number" field exists (Pitfall 6/Assumption A1)
- `/home/bpivk/moza/src/RigToggle.Windows/WindowsMonitorController.cs` — confirmed `GetAllMonitors()`'s actual enumeration/merge order and `MonitorInfo` field set
- `/home/bpivk/moza/src/RigToggle.IconGen/IconGeometry.cs`, `IconWriter.cs`, `Program.cs` — confirmed the stroke-then-fill seam fix, the exact fractional geometry constants to port, and that `IconGen` is "never referenced by RigToggle.App" (Pitfall 7)
- `/home/bpivk/moza/src/RigToggle.App/RigToggle.App.csproj` — confirmed zero `<PackageReference>` items, no `ApplicationHighDpiMode` setting, `AutoScaleMode.Font` usage context
- `/home/bpivk/moza/src/RigToggle.App/SettingsForm.cs`, `SettingsForm.Designer.cs` — confirmed no `TableLayoutPanel`/`FlowLayoutPanel` currently exists anywhere in this codebase (out of scope for this phase but relevant to Pattern 2 being genuinely new territory)
- `/home/bpivk/moza/.planning/config.json` — confirmed `workflow.nyquist_validation: false` (Validation Architecture section correctly omitted from this document)

### Secondary (MEDIUM confidence — milestone-level research, read this session, re-verified against actual current source above)
- `/home/bpivk/moza/.planning/research/SUMMARY.md`, `ARCHITECTURE.md`, `PITFALLS.md`, `STACK.md`, `FEATURES.md` (2026-08-09, v2.1-scoped) — recommended build order, architectural responsibility split, milestone-wide pitfall catalog; this phase's research narrows and deepens the subset scoped to Phase 19 specifically (tile dashboard + panel retirement), verified against the actual current `MonitorPanelForm.cs`/`MainForm.cs` rather than restated from the milestone docs alone

### Tertiary (LOW confidence)
- None — every claim in this document is grounded in direct source reading or the already-cross-verified milestone-level research; no unverified WebSearch-only claims were needed for this phase's scope (accent-color/theme-override ambiguity, the milestone's one genuinely LOW-confidence area, is entirely out of scope for Phase 19).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, confirmed via direct `.csproj` read
- Architecture: HIGH — every pattern grounded in direct reading of the actual current `MonitorPanelForm.cs`/`MainForm.cs`/`ThemeApplier.cs`, not assumption
- Pitfalls: HIGH — six of seven pitfalls are directly sourced from this codebase's own documented history (Phase 12/13/17 doc comments) or this session's own direct-read findings (Pitfalls 6/7, new this session); DPI pitfall (5) is MEDIUM-HIGH since it cannot be verified in this build environment, consistent with the milestone-level research's own stated limitation

**Research date:** 2026-08-09
**Valid until:** 30 days (stable codebase, no external API/library churn risk for this phase's fully-in-house scope)
