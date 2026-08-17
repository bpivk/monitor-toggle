# Phase 20: Custom Toggle-Switch Control - Pattern Map

**Mapped:** 2026-08-10
**Files analyzed:** 4 (1 new, 3 modified)
**Analogs found:** 4 / 4

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.App/Controls/ToggleSwitch.cs` (new) | component (owner-draw `UserControl`) | event-driven (paint + click/keyboard → `ActionRequested` event) | `src/RigToggle.App/Controls/MonitorTile.cs` | exact (same dumb-presentational owner-draw UserControl shape, same author, same phase family) |
| `src/RigToggle.App/MainForm.cs` (modified: field/wiring removal + `RefreshUi()`/`LayoutDashboard()` edits + new `ToggleSwitch_ActionRequested` handler) | controller (WinForms code-behind, event wiring) | request-response (click → orchestrator call → UI refresh) | itself — `BtnToggle_Click`/`RefreshUi()`/`LayoutDashboard()`/`ApplyDashboardTheming()` (existing methods being edited in place) plus `OnTileAction`/`CreateTile()` (Phase 19 precedent for wiring a new owner-draw control's `ActionRequested` event) | exact (in-place edit of the exact methods that already own this logic) |
| `src/RigToggle.App/MainForm.Designer.cs` (modified: remove `lblMode`/`btnToggle` fields+wiring, add `toggleSwitch` field+`Controls.Add`) | config (WinForms designer-pattern field/init block) | request-response (declarative control tree, no runtime logic) | itself — the existing `btnToggle`/`lblMode` declaration block being replaced, `btnIdentify`/`tileStrip` declaration blocks as the pattern for adding a new field + `Controls.Add` line | exact |
| `src/RigToggle.App/ThemeApplier.cs` (modified: add `ThemeToggleSwitch` method) | utility (static per-control theme setter) | transform (bool `dark` in → control properties set out) | `ThemeApplier.ThemeMonitorTile` (same file, immediately preceding method) | exact (same file, same method shape, same AccentColor source, same "two call-site" contract) |

## Pattern Assignments

### `src/RigToggle.App/Controls/ToggleSwitch.cs` (new — component, event-driven)

**Analog:** `src/RigToggle.App/Controls/MonitorTile.cs` (full file read, 305 lines)

**Imports pattern** (MonitorTile.cs lines 1-9):
```csharp
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using RigToggle.Core.Models;

namespace RigToggle.App.Controls
{
```
`ToggleSwitch.cs` needs the same set minus `System.Globalization`/`RigToggle.Core.Models` (no monitor-number formatting, no `MonitorInfo` dependency) — it takes zero domain-model input, only a `ToggleSwitchState` enum value via `SetState`.

**Constructor / control-style pattern** (MonitorTile.cs lines 52-73):
```csharp
public MonitorTile()
{
    SetStyle(
        ControlStyles.UserPaint |
        ControlStyles.AllPaintingInWmPaint |
        ControlStyles.OptimizedDoubleBuffer |
        ControlStyles.ResizeRedraw |
        ControlStyles.Selectable,
        true);

    // TILE-05 / Pitfall 4: a UserControl inherits none of Button's free
    // focus/keyboard affordances -- every one of them is opted into
    // explicitly here.
    TabStop = true;
    Cursor = Cursors.Hand;

    AutoScaleMode = AutoScaleMode.None;
    DoubleBuffered = true;
}
```
Copy verbatim into `ToggleSwitch`'s constructor (UI-SPEC.md's Design System row names this exact `SetStyle`/`TabStop`/`ProcessCmdKey` shape explicitly). `AutoScaleMode.None` matters here too — `MainForm.LayoutDashboard()` sizes the row explicitly via `Scaled()`, same reasoning as the tile comment.

**Dumb-presentational public surface** (MonitorTile.cs lines 75-151, `DevicePath`/`ActionRequested`/theme-color properties/`SetState`):
```csharp
public string? DevicePath { get; private set; }

public event EventHandler? ActionRequested;

[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
public Color AccentColor
{
    get => _accentColor;
    set { _accentColor = value; Invalidate(); }
}
// ... IconOffColor, HoverBackColor, FocusRingColor follow the identical get/set-then-Invalidate() shape

public void SetState(MonitorInfo monitor, int displayNumber)
{
    if (monitor is null) throw new ArgumentNullException(nameof(monitor));
    // ... assign fields ...
    AccessibleName = name;
    AccessibleRole = AccessibleRole.PushButton;
    Invalidate();
}
```
Map directly to the UI-SPEC.md Public Surface table:
- `ToggleSwitchState State { get; }` + `void SetState(ToggleSwitchState state)` — mirrors `SetState`'s "only way state is ever set, ends with `Invalidate()`" shape. No `ArgumentNullException` needed (enum, not a nullable reference type) but keep the "always ends in `Invalidate()`" discipline.
- `event EventHandler? ActionRequested` — identical signature, identical firing sites (see Click/ProcessCmdKey pattern below).
- Nine theme color properties (`OnColor`, `OffOutlineColor`, `OffHoverFillColor`, `OffPressFillColor`, `IndeterminateColor`, `ThumbColor`, `ThumbOutlineColor`, `LabelColor`, `FocusRingColor`) — each follows the exact `[DesignerSerializationVisibility(Hidden)]` + `get; set { field = value; Invalidate(); }` shape shown above, one block per property.
- `AccessibleName`/`AccessibleRole` set inside `SetState`, per UI-SPEC.md's Copywriting Contract (`"Rig Mode — On/Off/Unknown"`, `AccessibleRole.CheckButton`) — same call-site placement as `MonitorTile.SetState`'s `AccessibleName`/`AccessibleRole` assignment at the end of the state-setter, before `Invalidate()`.

**Click / keyboard pattern** (MonitorTile.cs lines 153-175):
```csharp
protected override void OnClick(EventArgs e)
{
    base.OnClick(e);
    Focus();
    ActionRequested?.Invoke(this, EventArgs.Empty);
}

protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    if (Focused && (keyData == Keys.Space || keyData == Keys.Return))
    {
        ActionRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }
    return base.ProcessCmdKey(ref msg, keyData);
}
```
Copy verbatim — UI-SPEC.md's `ActionRequested` row explicitly calls this "exact copy of `MonitorTile`'s override."

**Focus/hover state-tracking pattern** (MonitorTile.cs lines 177-204):
```csharp
protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }
protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }
```
Copy verbatim; `ToggleSwitch` additionally needs `_isPressed` tracked via `OnMouseDown`/`OnMouseUp` (see D-12/hover-press below) — no direct analog in `MonitorTile` (tiles have no press state), but `MainForm.BtnIdentify_MouseDown`/`_MouseUp` (lines 1108-1118) is the exact press-tracking shape to borrow instead (see Shared Patterns → Hover/Press Feedback below).

**OnPaint / stroke-then-fill / try-catch pattern** (MonitorTile.cs lines 206-273):
```csharp
protected override void OnPaint(PaintEventArgs e)
{
    try
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // ... geometry derived from ClientSize fractions, never bare pixel literals ...

        if (_isHovered)
        {
            using var hoverPath = BuildRoundedRect(new RectangleF(0f, 0f, w, h), cornerRadius);
            using var hoverBrush = new SolidBrush(HoverBackColor);
            g.FillPath(hoverBrush, hoverPath);
        }

        // ... icon/label draw calls ...

        if (Focused)
        {
            float penWidth = Math.Max(1f, w * FocusRingWidthFraction);
            var ringRect = new RectangleF(penWidth / 2f, penWidth / 2f, w - penWidth, h - penWidth);
            using var ringPath = BuildRoundedRect(ringRect, cornerRadius);
            using var ringPen = new Pen(FocusRingColor, penWidth);
            g.DrawPath(ringPen, ringPath);
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"MonitorTile.OnPaint failed: {ex}");
    }
}
```
`ToggleSwitch.OnPaint` follows the identical shape: early-return guard, `SmoothingMode.AntiAlias`, `using`-scoped `GraphicsPath`/brush/pen per shape, try/catch-wrapped with a `Trace.WriteLine($"ToggleSwitch.OnPaint failed: {ex}")` message. Per UI-SPEC.md's Geometry Contract, paint order is: **track** (stroke-only when Off, fill-only when On/Indeterminate — see the State→Visual Mapping table) drawn first via its own `GraphicsPath`/`FillPath`/`DrawPath` call, **then** the thumb via a **separate** `GraphicsPath`/`FillEllipse` (or `AddEllipse`+`FillPath`) call layered on top — mirrors the mandatory "track and thumb are separate `GraphicsPath` calls, stroke-then-fill, back-to-front" contract (Pitfall 2), and the "Rig Mode" label is drawn via `TextRenderer.DrawText` with `VerticalCenter | Left | NoPrefix` flags, matching `MonitorTile`'s own label draw call at lines 249-258 (`HorizontalCenter | Top | NoPrefix` there — same `TextRenderer.DrawText` API, different flag combination per the row's left-aligned-label layout).

**Rounded-rect path builder** (MonitorTile.cs lines 278-302, `BuildRoundedRect`):
```csharp
private static GraphicsPath BuildRoundedRect(RectangleF rect, float radius)
{
    var path = new GraphicsPath();
    float diameter = radius * 2f;
    if (diameter <= 0f) { path.AddRectangle(rect); return path; }
    var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
    path.StartFigure();
    path.AddArc(arc, 180, 90);
    arc.X = rect.Right - diameter;
    path.AddArc(arc, 270, 90);
    arc.Y = rect.Bottom - diameter;
    path.AddArc(arc, 0, 90);
    arc.X = rect.X;
    path.AddArc(arc, 90, 90);
    path.CloseFigure();
    return path;
}
```
UI-SPEC.md's Focus Ring row explicitly says to reuse this exact builder (or a local copy) for the track's fully-rounded 14px-radius pill shape (both the track's own outline/fill path AND its pill-shaped focus ring use this same helper — a radius of `height/2` makes the "rounded rect" fully round, i.e. a true pill/stadium shape, which is what the 52×28px / 14px-radius track needs). Copy this method into `ToggleSwitch` as a private static local (same-file duplication, matching the existing no-shared-geometry-helper-class convention in this codebase — `MonitorIconGeometry` is the one exception and is for icon glyphs, not generic rounded rects).

---

### `src/RigToggle.App/MainForm.cs` (modified — controller, request-response)

**Analog for the click-handler body relocation:** `BtnToggle_Click` (lines 386-530, full method read) is ported **verbatim** (WR-01 gate → DISPLAY-11 gate → confirm dialog → orchestrator call → `RefreshUi()` → partial-failure dialog → catch blocks) into a new `ToggleSwitch_ActionRequested(object? sender, EventArgs e)` handler with the exact same body — only the method name and its wiring site change (`btnToggle.Click += ...` becomes `toggleSwitch.ActionRequested += ...`). The three `MessageBox.Show` copy strings (DISPLAY-11 unknown-mode gate, WR-01 unconfigured-Settings gate, CORE-04 partial-failure checklist) and the `ToggleInProgressException`/generic `catch (Exception ex)` blocks are reused unmodified per UI-SPEC.md's Copywriting Contract ("Reused verbatim, unmodified").

**Analog for wiring a new owner-draw control's event, from Phase 19's `MonitorTile` precedent** (`CreateTile()`, lines 637-646):
```csharp
private MonitorTile CreateTile()
{
    var tile = new MonitorTile
    {
        Size = new Size(Scaled(TileWidthPx), Scaled(TileHeightPx)),
        Margin = new Padding(Scaled(TileMarginPx)),
    };
    tile.ActionRequested += (s, e) => OnTileAction((MonitorTile)s!);
    return tile;
}
```
For `ToggleSwitch`, since it is a single instance (not a per-monitor collection like tiles), the equivalent wiring is a one-line `toggleSwitch.ActionRequested += ToggleSwitch_ActionRequested;` in `MainForm.Designer.cs`'s init block (see Designer section below) rather than a factory method — matches how `btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);` is currently wired in the Designer (Designer.cs line 102).

**`RefreshUi()` edit** (lines 354-384, full method read) — current Unknown/Rig/Normal branches:
```csharp
private void RefreshUi()
{
    if (!_orchestrator.IsModeKnown())
    {
        lblMode.Text = "Mode: Unknown";
        btnToggle.Text = "Toggle";
        trayToggleMenuItem.Text = "Toggle";
        notifyIcon.Text = "Rig Toggle — Mode Unknown";
        return;
    }

    bool isInRigMode = _orchestrator.IsInRigMode();
    lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
    btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";

    if (_normalIcon is not null && _rigIcon is not null)
    {
        notifyIcon.Icon = isInRigMode ? _rigIcon : _normalIcon;
    }
    notifyIcon.Text = isInRigMode ? "Rig Toggle — Rig Mode" : "Rig Toggle — Normal Mode";
    trayToggleMenuItem.Text = btnToggle.Text; // D-04: one shared source of truth
}
```
Per D-06/D-07/D-08 and the "Integration Points" section of CONTEXT.md, this becomes:
- Unknown branch: delete `lblMode.Text = "Mode: Unknown";`, delete `btnToggle.Text = "Toggle";`, add `toggleSwitch.SetState(ToggleSwitchState.Indeterminate);`, compute `trayToggleMenuItem.Text = "Toggle";` directly (already is — unaffected), keep `notifyIcon.Text` line unchanged.
- Known branch: delete `lblMode.Text = ...;`, delete `btnToggle.Text = ...;`, add `toggleSwitch.SetState(isInRigMode ? ToggleSwitchState.On : ToggleSwitchState.Off);`, and since `trayToggleMenuItem.Text = btnToggle.Text;` can no longer read from the deleted button, compute it directly from `isInRigMode` per the Copywriting Contract's "Integration note": `trayToggleMenuItem.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";` (copy unchanged, only its source changes).

**`OnThemeChanged`/`InitializeTrayState()` two-call-site edit** (lines 148-172 and 240-266) — both currently call `ThemeApplier.ThemeButton(btnToggle, IsDark);`; per Pitfall 1 this line is replaced in **both** locations with `ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark);` (see ThemeApplier section below). Do not add it to only one — this is the exact bug class this codebase shipped twice in Phase 12 per the file's own doc comments.

**`LayoutDashboard()` edit** (lines 864-937, full method read) — current `lblMode`/`btnToggle` block:
```csharp
lblMode.Location = new Point(margin, margin);
lblMode.Size = new Size(contentWidth, Scaled(ModeLabelHeightPx));

int stripTop = margin + Scaled(ModeLabelHeightPx) + Scaled(GapSmPx);
// ...
btnToggle.Size = new Size(contentWidth, Scaled(TogglePx));
btnToggle.Location = new Point(margin, btnIdentify.Bottom + Scaled(GapSmPx));

btnSettings.Location = new Point(margin + contentWidth - btnSettings.Width, btnToggle.Bottom + Scaled(GapLgPx));
```
Per UI-SPEC.md's Layout Integration section (D-06 resolved: collapse, don't preserve, the freed space) and Geometry Contract (row 288×32px, replacing `TogglePx`=40 with a new `ToggleRowHeightPx`=32 constant):
- Delete both `lblMode.Location`/`lblMode.Size` lines.
- Change `int stripTop = margin + Scaled(ModeLabelHeightPx) + Scaled(GapSmPx);` to `int stripTop = margin;` (flush with top margin).
- Replace `btnToggle.Size`/`btnToggle.Location` with `toggleSwitch.Size = new Size(contentWidth, Scaled(ToggleRowHeightPx)); toggleSwitch.Location = new Point(margin, btnIdentify.Bottom + Scaled(GapSmPx));` (identical gap-above formula, `btnToggle`→`toggleSwitch` rename, `TogglePx`→`ToggleRowHeightPx` rename per the new 32px value).
- Replace the two `btnToggle.Bottom` references (in the `btnSettings.Location` line) with `toggleSwitch.Bottom`.
- Delete the now-unused `ModeLabelHeightPx`/`TogglePx` constants (lines 74, 82) — replace with a single new `ToggleRowHeightPx = 32` constant.

---

### `src/RigToggle.App/MainForm.Designer.cs` (modified — config, request-response)

**Analog for removal:** the existing `lblMode` block (lines 78-86) and `btnToggle` block (lines 88-105) — both are deleted entirely, including their `Controls.Add` lines (286, 290) and field declarations (298-299).

**Analog for the new field + init block, from `btnIdentify`'s code-only-control wiring shape** (lines 177-195, adapted — note `btnIdentify` is a stock `Button`, not a code-only custom control; the closer shape for *instantiating a custom code-only control* is how `tileStrip` is declared as a field then configured, lines 137-158, combined with how `MonitorTile` instances get their events wired in `MainForm.cs`'s `CreateTile()`):
```csharp
// btnIdentify (Button-based event-wiring pattern to mirror for the new field)
this.btnIdentify = new System.Windows.Forms.Button();
// ...
this.btnIdentify.Name = "btnIdentify";
this.btnIdentify.Text = "Identify";
this.btnIdentify.Size = new System.Drawing.Size(100, 32);
this.btnIdentify.Location = new System.Drawing.Point(16, 148);
this.btnIdentify.Click += new System.EventHandler(this.BtnIdentify_Click);
// ... Paint/MouseEnter/MouseLeave/MouseDown/MouseUp/Enter/Leave wiring
```
The new Designer block is:
```csharp
this.toggleSwitch = new RigToggle.App.Controls.ToggleSwitch();
this.toggleSwitch.Name = "toggleSwitch";
this.toggleSwitch.Location = new System.Drawing.Point(16, 148); // LayoutDashboard() overwrites this at runtime, same as every other control here
this.toggleSwitch.Size = new System.Drawing.Size(288, 32);
this.toggleSwitch.ActionRequested += new System.EventHandler(this.ToggleSwitch_ActionRequested);
```
placed in the same declare-then-configure-then-wire-events ordering as every other control in this file, added to `this.Controls.Add(this.toggleSwitch);` at the same position `this.Controls.Add(this.btnToggle);` (line 290) currently occupies (preserves tab order: tile row → Identify → toggle → Settings, per D-09/UI-SPEC.md's phase framing), and declared as a private field `private RigToggle.App.Controls.ToggleSwitch toggleSwitch;` at the same place `private System.Windows.Forms.Button btnToggle;` (line 299) currently sits.

---

### `src/RigToggle.App/ThemeApplier.cs` (modified — utility, transform)

**Analog:** `ThemeMonitorTile` (lines 169-203, full method read, immediately precedes where the new method is inserted):
```csharp
public static void ThemeMonitorTile(MonitorTile tile, bool dark)
{
    try
    {
        tile.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
        tile.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
        tile.AccentColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
        tile.FocusRingColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
        tile.IconOffColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
        tile.HoverBackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.ControlLight;
        tile.Invalidate();
    }
    catch
    {
        // Cosmetic-only — leave the control unchanged on failure.
    }
}
```
`ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark)` follows the identical try/`Invalidate()`/empty-catch shape, one property assignment per line, sourcing every literal from UI-SPEC.md's Color section (all already-established literals in this codebase — zero new colors except the two Indeterminate-state literals, which are new but isolated per D-07):
```csharp
public static void ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark)
{
    try
    {
        toggleSwitch.OnColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight; // same as tile.AccentColor
        toggleSwitch.OffOutlineColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText; // same as tile.IconOffColor
        toggleSwitch.OffHoverFillColor = dark ? Color.FromArgb(80, 80, 86) : SystemColors.ControlLight; // same as ThemeButton's MouseOverBackColor
        toggleSwitch.OffPressFillColor = dark ? Color.FromArgb(18, 18, 20) : SystemColors.ControlDarkDark; // same as ThemeButton's MouseDownBackColor
        toggleSwitch.IndeterminateColor = dark ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200); // new, isolated literal (D-07)
        toggleSwitch.ThumbColor = Color.White; // theme-independent (D-11)
        toggleSwitch.ThumbOutlineColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.ControlDark;
        toggleSwitch.LabelColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
        toggleSwitch.FocusRingColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight; // same as tile.FocusRingColor
        toggleSwitch.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control; // same as tile.BackColor, Pitfall 3 Mica-safe background
        toggleSwitch.Invalidate();
    }
    catch
    {
        // Cosmetic-only — leave the control unchanged on failure.
    }
}
```
Note: hover/press variants of `OnColor`/`IndeterminateColor` (`ControlPaint.Light(OnColor, 0.2f)` / `ControlPaint.Dark(OnColor, 0.2f)` per UI-SPEC.md) are **computed at paint time inside `ToggleSwitch.OnPaint`**, not stored as separate `ThemeApplier`-set properties — there is no existing precedent for a "computed hover variant" theme property in this codebase (`MonitorTile.HoverBackColor` is itself the literal, not a computed one), so this is the one place `ToggleSwitch` deviates slightly from the tile's exact property list, using in-box `ControlPaint.Light`/`ControlPaint.Dark` directly in `OnPaint` against the already-set `OnColor`/`IndeterminateColor` fields — matches UI-SPEC.md's explicit formula table.

## Shared Patterns

### Owner-draw try/catch + Trace.WriteLine discipline
**Source:** `MonitorTile.OnPaint` (lines 206-273), `MainForm.LayoutDashboard`/`BtnToggle_Paint`/`BtnIdentify_Paint`/`BtnSettings_Paint` (all wrap their bodies in `try { ... } catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"{Class}.{Method} failed: {ex}"); }`)
**Apply to:** `ToggleSwitch.OnPaint`, and any new/edited paint-adjacent code in `MainForm.cs` — a painting/layout failure must never crash the toggle flow (T-12-02's "cosmetic-only" rule extends to this whole family of methods).
```csharp
try
{
    // paint or layout body
}
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"ToggleSwitch.OnPaint failed: {ex}");
}
```

### Hover/Press manual fill (bypassing FlatAppearance)
**Source:** `MainForm.ManualButtonFill` (lines 1003-1008) + `BtnIdentify_MouseEnter`/`_MouseLeave`/`_MouseDown`/`_MouseUp` (lines 1095-1118)
```csharp
private void BtnIdentify_MouseDown(object? sender, MouseEventArgs e)
{
    _identifyPressed = true;
    btnIdentify.Invalidate();
}

private void BtnIdentify_MouseUp(object? sender, MouseEventArgs e)
{
    _identifyPressed = false;
    btnIdentify.Invalidate();
}
```
**Apply to:** `ToggleSwitch`'s own hover/press tracking (D-12 — "reuse the established pattern's mechanism, adapt the specific shift for the switch's track/thumb shape"). `ToggleSwitch` needs its own `_isHovered`/`_isPressed` private fields (mirroring `MonitorTile._isHovered` for hover, plus a new `_isPressed` field for press since `MonitorTile` has no press state) with `OnMouseEnter`/`OnMouseLeave` (from `MonitorTile`) and `OnMouseDown`/`OnMouseUp` overrides (from `MainForm`'s Identify/Settings button pattern, adapted from event handlers to protected overrides since `ToggleSwitch` is the control itself, not a wired-up stock `Button`) — each setting the flag and calling `Invalidate()`.

### Focus ring (accent-colored, drawn only when Focused)
**Source:** `MainForm.DrawButtonFocusRing` (lines 1022-1030, plain rectangle — **do not reuse this method directly**, UI-SPEC.md explicitly calls it "wrong shape for a pill") and `MonitorTile.OnPaint`'s own rounded focus-ring block (lines 260-267, the correct rounded-rect shape to follow):
```csharp
if (Focused)
{
    float penWidth = Math.Max(1f, w * FocusRingWidthFraction);
    var ringRect = new RectangleF(penWidth / 2f, penWidth / 2f, w - penWidth, h - penWidth);
    using var ringPath = BuildRoundedRect(ringRect, cornerRadius);
    using var ringPen = new Pen(FocusRingColor, penWidth);
    g.DrawPath(ringPen, ringPath);
}
```
**Apply to:** `ToggleSwitch.OnPaint`'s focus ring, drawn around the **track only** (not the whole row) per UI-SPEC.md, using the track's own 14px radius passed into the local `BuildRoundedRect` copy — same `if (Focused)` gate, same 2px accent pen.

### Theming two-call-site rule (Pitfall 1)
**Source:** `MainForm.OnThemeChanged` (lines 148-172) and `MainForm.InitializeTrayState()` (lines 240-266) both currently call `ThemeApplier.ThemeButton(btnToggle, IsDark);` and `ApplyDashboardTheming()`.
**Apply to:** Every new `ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark);` call must be added to **both** locations, never just one — this is the single most important cross-cutting rule for this phase (explicitly called out three times across CONTEXT.md/UI-SPEC.md/PITFALLS.md as a bug this codebase has shipped twice before).

### AccentColor placeholder (D-09)
**Source:** `MainForm.AccentColor` property (line 182) and `ThemeApplier.ThemeMonitorTile`'s `tile.AccentColor` line (line 193) — both `Color.FromArgb(0, 90, 158)` dark / `SystemColors.Highlight` light.
**Apply to:** `ToggleSwitch.OnColor` (via `ThemeApplier.ThemeToggleSwitch`) and, if `MainForm.cs` needs the accent value directly anywhere in the ported `ToggleSwitch_ActionRequested` body (it does not — the confirm dialog and message boxes carry no color), otherwise no direct `MainForm.AccentColor` reference is needed by the new code since theming flows through `ThemeApplier` exclusively.

## No Analog Found

None — every file this phase touches has a direct, exact-match precedent already in the codebase (Phase 19's `MonitorTile`/`ThemeMonitorTile`/tile-wiring pattern was purpose-built as this exact template, per `ARCHITECTURE.md`'s Recommended Build Order step 7: "swap it in for `btnToggle` last... same isolation principle as step 1" referring to `MonitorTile`).

## Metadata

**Analog search scope:** `src/RigToggle.App/`, `src/RigToggle.App/Controls/`, `.planning/research/ARCHITECTURE.md`, `.planning/research/PITFALLS.md`
**Files scanned:** `MonitorTile.cs` (full), `MainForm.cs` (targeted: header/constants lines 1-90, theming/lifecycle lines 130-400, click handler lines 386-530, layout lines 856-1000, paint/focus-ring lines 1010-1160), `MainForm.Designer.cs` (grep for `btnToggle`/`lblMode`/`tileStrip`/`btnIdentify`), `ThemeApplier.cs` (full)
**Pattern extraction date:** 2026-08-10
