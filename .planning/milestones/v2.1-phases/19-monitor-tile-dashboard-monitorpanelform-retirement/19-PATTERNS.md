# Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement - Pattern Map

**Mapped:** 2026-08-09
**Files analyzed:** 7 (2 new, 4 modified, 1 deleted-with-designer-pair)
**Analogs found:** 7 / 7 (this phase is explicitly a port-and-adapt exercise — every file has a direct source-of-truth analog per RESEARCH.md)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.App/Controls/MonitorTile.cs` (+ `.Designer.cs`) | component (owner-drawn `UserControl`) | event-driven (click/keyboard → event, no direct CRUD) | `src/RigToggle.App/MonitorIdentifyOverlay.cs` (closest *class shape* — sealed `Form`/`Control` composed in code, no Designer split) + `MonitorPanelForm.CreateStatusDot`/`DgvMonitorPanel_CellClick` (closest *behavior* — status rendering + click→action) | role-match (no existing `UserControl` anywhere in this codebase — confirmed via `find`; composite of two analogs) |
| `src/RigToggle.App/MonitorIconGeometry.cs` | utility (pure GDI+ geometry/paint helper) | transform (path-build + paint, no I/O) | `src/RigToggle.IconGen/IconGeometry.cs` | exact (explicit port target per RESEARCH.md Pattern 3 / Pitfall 7 — same fractional constants, same stroke-then-fill fix) |
| `src/RigToggle.App/MainForm.cs` / `.Designer.cs` (MODIFIED) | controller/orchestrator (Form, composition-root consumer) | request-response (UI event → controller mutation) + event-driven (hotplug subscription) | `src/RigToggle.App/MonitorPanelForm.cs` / `.Designer.cs` (mutation/lease/hotplug/Identify logic to absorb) | exact (this is a direct-port source, not an analog by similarity — RESEARCH.md names it as the literal code to port) |
| `src/RigToggle.App/ThemeApplier.cs` (MODIFIED — add tile theming) | utility (per-control theming pipeline) | transform (state → color assignment, no I/O) | itself — `ThemeButton`/`ThemeMonitorGrid` methods already in this file | exact (same file, same pattern, new method) |
| `src/RigToggle.App/Program.cs` (MODIFIED — drop factory wiring) | config (composition root) | request-response (constructs and wires dependencies once at startup) | itself — existing `MonitorPanelFormFactory` local function being removed | exact (deletion target, not new code) |
| `src/RigToggle.App/MonitorPanelForm.cs` / `.Designer.cs` (DELETED, TILE-07) | controller (Form) | request-response | N/A — deletion target | n/a |
| `src/RigToggle.App/MonitorConfirmDialog.cs`, `MonitorIdentifyOverlay.cs` (UNCHANGED — reused as-is) | dialog / overlay | request-response | itself | exact (no port needed, only caller/Owner changes) |

## Pattern Assignments

### `src/RigToggle.App/Controls/MonitorTile.cs` (+ `.Designer.cs`) — NEW

**Primary analog for structure:** `src/RigToggle.App/MonitorIdentifyOverlay.cs` (sealed class, all layout done in the constructor, no Designer.cs pair — since this codebase has zero existing `UserControl`s, follow the "no-Designer, code-only" convention `MonitorIdentifyOverlay` already establishes rather than inventing a Designer-file convention from scratch. If a `.Designer.cs` split is still desired for consistency with `MainForm`/`MonitorPanelForm`, model it after `MonitorPanelForm.Designer.cs`'s `InitializeComponent`/`Dispose(bool)` shape below.)

**Secondary analog for status-rendering + click behavior:** `src/RigToggle.App/MonitorPanelForm.cs`

**Status-dot precedent (lines 74-86 of `MonitorPanelForm.cs`, conceptual precedent for D-02's outline/fill, NOT literal code to copy — D-02 requires outline+fill on the icon itself, not a separate dot):**
```csharp
// 17-UI-SPEC.md Color: these two literals are the locked Status colors and are
// theme-independent -- deliberately takes no isDark parameter. A future editor
// should not add one; neither literal changes on a theme flip.
private static Bitmap CreateStatusDot(bool isActive)
{
    var bmp = new Bitmap(12, 12);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    Color dotColor = isActive ? Color.FromArgb(46, 204, 113) : Color.FromArgb(200, 60, 60);
    using var brush = new SolidBrush(dotColor);
    g.FillEllipse(brush, 0, 0, 12, 12);
    return bmp;
}
```
Key takeaway to carry into `MonitorTile`: status colors are **locked, theme-independent literals** (green `#2ECC71` / red `#C83C3C`) — do not thread `isDark` through the on/off color decision, only through any *chrome* the tile also paints (e.g. tile background/border, via `ThemeApplier`).

**Click → action pattern (lines 215-233, `DgvMonitorPanel_CellClick` — the direct precedent for `MonitorTile.OnClick`/`ActionRequested`):**
```csharp
private void DgvMonitorPanel_CellClick(object? sender, DataGridViewCellEventArgs e)
{
    if (e.RowIndex < 0 || e.ColumnIndex != colAction.Index) return;

    DataGridViewRow row = dgvMonitorPanel.Rows[e.RowIndex];
    if (row.Tag is not string devicePath) return;

    MonitorInfo? monitor = _allMonitors.FirstOrDefault(m => m.DevicePath == devicePath);
    if (monitor is null) return;

    if (monitor.IsActive)
    {
        DisableMonitor(devicePath, monitor.FriendlyName);
    }
    else
    {
        EnableMonitor(devicePath);
    }
}
```
`MonitorTile` must NOT reproduce the `if (monitor.IsActive) Disable else Enable` branch itself (Pattern 1, dumb-tile rule) — it only raises `ActionRequested`; `MainForm.OnTileAction` makes that branch decision, mirroring this method's shape but moved up a layer.

**Stable-identity precedent (line 138, `MonitorPanelForm.cs` — carry into `MonitorTile.DevicePath`):**
```csharp
// Stable-identity precedent (06-PATTERNS.md Shared Patterns, reused every grid
// in this app): key every row by DevicePath via Tag, NEVER by row index or
// display-name matching.
dgvMonitorPanel.Rows[rowIndex].Tag = monitor.DevicePath;
```

**Dispose/cleanup precedent (Designer.cs `Dispose(bool)`, for any per-tile-owned GDI resources — brushes/pens/paths cached as instance fields, per Threat Pattern in RESEARCH.md's Security Domain section):**
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing && (components != null))
    {
        components.Dispose();
    }
    if (disposing)
    {
        _dotActive?.Dispose();
        _dotInactive?.Dispose();
    }
    base.Dispose(disposing);
}
```

**RESEARCH.md's own `MonitorTile` skeleton (Pattern 1, this is the concrete target — treat as primary code to adapt, not just a description):**
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
```

---

### `src/RigToggle.App/MonitorIconGeometry.cs` — NEW

**Analog:** `src/RigToggle.IconGen/IconGeometry.cs` (exact port target, per RESEARCH.md Pitfall 7 — copy the fractional constants and `BuildMonitorPath` verbatim; `IconGen` itself is dev-time-only and cannot be referenced as a library)

**Constants to port (lines 21-36):**
```csharp
private const float ScreenX = 0.125f;
private const float ScreenY = 0.125f;
private const float ScreenW = 0.75f;
private const float ScreenH = 0.5f;
private const float ScreenRadius = 0.06f;

private const float NeckX = 0.4375f;
private const float NeckY = 0.625f;
private const float NeckW = 0.125f;
private const float NeckH = 0.125f;

private const float BaseX = 0.28f;
private const float BaseY = 0.75f;
private const float BaseW = 0.44f;
private const float BaseH = 0.125f;
private const float BaseRadius = 0.03f;
```

**`BuildMonitorPath` to port (lines 186-199 — note the original signature takes `int size`; the tile version needs a `RectangleF`/`w,h` overload since tiles are not square, per RESEARCH.md Pattern 3's `DrawTileIcon(Graphics g, RectangleF bounds, ...)` signature):**
```csharp
private static GraphicsPath BuildMonitorPath(int size)
{
    float w = size, h = size;
    var path = new GraphicsPath();

    // Screen: top-left (0.125W, 0.125H), size 0.75W x 0.5H, corner radius 0.06W.
    AddRoundedRect(path, ScreenX * w, ScreenY * h, ScreenW * w, ScreenH * h, ScreenRadius * w);
    // Neck: top-left (0.4375W, 0.625H), size 0.125W x 0.125H, sharp corners.
    path.AddRectangle(new RectangleF(NeckX * w, NeckY * h, NeckW * w, NeckH * h));
    // Base: top-left (0.28W, 0.75H), size 0.44W x 0.125H, corner radius 0.03W.
    AddRoundedRect(path, BaseX * w, BaseY * h, BaseW * w, BaseH * h, BaseRadius * w);

    return path;
}

// Helper this depends on -- also port verbatim:
private static void AddRoundedRect(GraphicsPath path, float x, float y, float width, float height, float radius)
{
    float diameter = radius * 2;
    if (diameter <= 0f)
    {
        path.AddRectangle(new RectangleF(x, y, width, height));
        return;
    }
    var arc = new RectangleF(x, y, diameter, diameter);
    path.StartFigure();
    path.AddArc(arc, 180, 90);   // Top-left
    arc.X = x + width - diameter;
    path.AddArc(arc, 270, 90);   // Top-right
    arc.Y = y + height - diameter;
    path.AddArc(arc, 0, 90);     // Bottom-right
    arc.X = x;
    path.AddArc(arc, 90, 90);    // Bottom-left
    path.CloseFigure();
}
```

**Stroke-then-fill compositing fix — critical, do not simplify (lines 66-95, doc comment + `DrawNormalIcon` body — this is the exact seam-artifact avoidance RESEARCH.md Pitfall 2/Pattern 3 requires for the ON-state fill; for the OFF-state hollow outline, RESEARCH.md's own `DrawTileIcon` already gives the simpler single-shape-stroke-only variant, reproduced below):**
```csharp
// CR-01 fix: a Pen stroke on the combined path strokes every sub-figure's own
// boundary independently -- it does NOT compute a merged union contour -- so
// stroking after filling leaves visible seam lines wherever two sub-shapes touch
// or overlap (screen<->neck, neck<->base). Draw the outline FIRST, at DOUBLE
// width, then fill ON TOP: the anti-aliased fill exactly covers the union
// region, overpainting the inner half of every stroke (including interior
// seams), leaving only the true outer contour visible.
using var path = BuildMonitorPath(size);
using var outline = new Pen(Color.Black, 2f * OutlineWidth(size));
g.DrawPath(outline, path);
using var fill = new SolidBrush(Color.White);
g.FillPath(fill, path);
```

**Target signature for the tile-specific entry point (RESEARCH.md Pattern 3, this is the new API shape — not a literal port, adapt from the above):**
```csharp
internal static class MonitorIconGeometry
{
    public static GraphicsPath BuildMonitorPath(float w, float h) { /* ported logic, RectangleF-based */ }

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

---

### `src/RigToggle.App/MainForm.cs` / `.Designer.cs` (MODIFIED — absorbs `MonitorPanelForm`'s logic)

**Analog:** `src/RigToggle.App/MonitorPanelForm.cs` (the literal source being ported in — this is a merge, not a from-scratch design)

**Constructor DI pattern to extend (lines 56-76 of `MainForm.cs` — add hotplug subscription here, mirroring `MonitorPanelForm`'s constructor-time subscribe at lines 55-61, but WITHOUT the FormClosed-unsubscribe, per Pitfall 2):**
```csharp
public MainForm(
    ToggleOrchestrator orchestrator,
    ISettingsStore settingsStore,
    IMonitorController monitorController,
    Func<SettingsForm> settingsFormFactory,
    IThemeProvider themeProvider)                 // Func<MonitorPanelForm> factory param REMOVED
{
    _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
    _settingsFormFactory = settingsFormFactory ?? throw new ArgumentNullException(nameof(settingsFormFactory));
    _themeProvider = themeProvider ?? throw new ArgumentNullException(nameof(themeProvider));

    InitializeComponent();

    _themeProvider.ThemeChanged += OnThemeChanged;

    // NEW (TILE-06): subscribe once, app-lifetime, NEVER unsubscribed until
    // process exit -- MainForm is hidden-not-closed during tray-resident
    // operation (Pitfall 2), unlike MonitorPanelForm's closable/reopenable
    // subscribe-in-ctor/unsubscribe-on-FormClosed pattern. Do not gate on
    // Hide()/Show().
    Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
}
```

**Hotplug handler to port verbatim, adapted from `PopulateMonitorGrid()` re-population target to `RefreshMonitorTiles()` (lines 148-181 of `MonitorPanelForm.cs`):**
```csharp
private void OnDisplaySettingsChanged(object? sender, EventArgs e)
{
    // Disposed-race guard: SystemEvents can fire on a background thread. Even
    // though MainForm is not closable/reopenable like MonitorPanelForm was, the
    // IsDisposed check is still correct defensive practice at real process exit.
    if (IsDisposed) return;

    if (InvokeRequired)
    {
        try
        {
            BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e)));
        }
        catch (ObjectDisposedException)
        {
        }
        return;
    }

    try
    {
        RefreshMonitorTiles();
    }
    catch
    {
        // A hotplug notification must never crash the form.
    }
}
```

**Lease + confirm-dialog structure to port verbatim into the tile-click handler (this is the literal code from `DisableMonitor`, lines 256-320 of `MonitorPanelForm.cs`, already adapted in RESEARCH.md's own Code Examples section — use that adapted version as the direct implementation target, not this raw form, since it already accounts for `MainForm`'s field names):**
```csharp
// See RESEARCH.md "Code Examples > Porting the confirm-dialog + lease structure
// into MainForm's tile handler" for the exact adapted version to implement --
// it is the literal DisableMonitor/EnableMonitor structure moved into
// OnTileAction(MonitorTile), unabbreviated, per Pitfall 3 (do not "unify" this
// with BtnToggle_Click's differently-shaped implicit-_busy-guard pattern).
```

**Identify handler to port, Owner retargeted (lines 348-404 of `MonitorPanelForm.cs`, adapted version already in RESEARCH.md's Code Examples — iterate the SAME canonical `_lastKnownMonitors` order tiles use, per Pitfall 6, not `DataGridView` rows which no longer exist):**
```csharp
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

**Theme-flip handler to extend (lines 85-106 of `MainForm.cs` — add tile-strip re-theme call here, per Pitfall 1, matching the existing per-button call shape):**
```csharp
private void OnThemeChanged(object? sender, EventArgs e)
{
    if (InvokeRequired)
    {
        BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
        return;
    }

    try
    {
        System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
        DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark);
        ThemeApplier.ThemeButton(btnToggle, IsDark);
        ThemeApplier.ThemeButton(btnSettings, IsDark);   // now gear-icon button
        // NEW: theme every tile + Identify button here too (Pitfall 1) --
        // e.g. foreach (MonitorTile tile in tileStrip.Controls) ThemeApplier.ThemeMonitorTile(tile, IsDark);
        ThemeApplier.ThemeButton(btnIdentify, IsDark);
        Refresh();
    }
    catch
    {
        // Cosmetic-only -- a theming failure must never crash the toggle flow.
    }
}
```

**`InitializeTrayState()` to extend identically (lines 169-185 — Pitfall 1 requires BOTH call sites updated, never just one):**
```csharp
public void InitializeTrayState()
{
    LoadTrayIconsIfNeeded();
    RefreshUi();
    ApplyTrayVisibility();
    ApplyDwmChrome();

    ThemeApplier.ThemeButton(btnToggle, IsDark);
    ThemeApplier.ThemeButton(btnSettings, IsDark);
    // NEW: same tile/Identify theming call as OnThemeChanged above -- both
    // call sites must stay in lockstep (Pitfall 1).
    ThemeApplier.ThemeButton(btnIdentify, IsDark);

    // NEW (TILE-01/D-04/D-06): populate + size the tile strip here too, since
    // this runs unconditionally on both startup paths (including --tray, where
    // OnLoad never fires) -- mirrors the --tray-safe timing rationale already
    // documented for ApplyDwmChrome/button theming above.
    RefreshMonitorTiles();
}
```

**Entry points to DELETE (TILE-07 — `BtnMonitors_Click`, `TrayMonitorsMenuItem_Click`, `OpenMonitorPanel()`, lines 475-514, plus the `_monitorPanelForm`/`_monitorPanelFormFactory` fields at lines 34-42):** delete verbatim, no replacement — the tile row IS the new entry point, it has no separate "open" action.

**Designer.cs changes (MainForm.Designer.cs):**
- Remove `btnMonitors` (lines 55, 104-116, 207, 217) and `trayMonitorsMenuItem` (lines 60, 135-139, 170, 222) entirely.
- Existing `ClientSize = new Size(320, 200)` (line 194) becomes dynamic per D-04 — do not hardcode a new fixed value; this line is superseded by `MainForm.AutoSize = true` set either in `InitializeComponent` or at runtime (RESEARCH.md Pattern 2).
- `btnSettings` (lines 94-101) needs its `Size`/`Location`/`Text` changed to the small icon-only gear per D-10 — reuse the existing `Click += BtnSettings_Click` wiring unchanged, only the visual properties and position move (per D-09's bottom-corner placement).

---

### `src/RigToggle.App/ThemeApplier.cs` (MODIFIED)

**Analog:** itself — `ThemeButton` (lines 124-139), the most directly-transferable existing method for a new `ThemeMonitorTile`/tile-strip theming method, per Pitfall 1's explicit requirement.

**Pattern to replicate for the new tile theming method:**
```csharp
public static void ThemeButton(Button button, bool dark)
{
    try
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
        button.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
        button.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(62, 62, 66) : SystemColors.ControlLight;
        button.FlatAppearance.MouseDownBackColor = dark ? Color.FromArgb(28, 28, 30) : SystemColors.ControlDark;
    }
    catch
    {
        // Cosmetic-only — leave the control unchanged on failure.
    }
}
```
New method should follow the identical shape: `public static void ThemeMonitorTile(MonitorTile tile, bool dark)`, wrapped in the same try/catch-swallow (cosmetic-only, must never throw), setting only the tile's *chrome* (background/border/number-label color) — never the on/off status color, which per the `CreateStatusDot` doc comment is a locked, theme-independent literal.

Every method in this file already follows: `try { ...palette assignment... } catch { /* cosmetic-only */ }` — this is the mandatory shape for any new method added here, not optional boilerplate.

---

### `src/RigToggle.App/Program.cs` (MODIFIED — remove `MonitorPanelForm` wiring)

**Analog:** itself — the exact lines to delete are already isolated:
```csharp
// DELETE (line 160):
MonitorPanelForm MonitorPanelFormFactory() => new MonitorPanelForm(monitorController, settingsStore, themeProvider, toggleOrchestrator);

// MODIFY (line 162) -- drop the MonitorPanelFormFactory argument:
mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, themeProvider);
```
No new pattern needed here — `toggleOrchestrator`, `monitorController`, `settingsStore`, `themeProvider` are already threaded into `MainForm`'s constructor call for other reasons and require no new composition-root wiring; only the deletion above and the constructor-arity change apply.

---

## Shared Patterns

### Disposed-race / cross-thread event marshalling
**Source:** `MonitorPanelForm.OnDisplaySettingsChanged` (lines 148-181) and `MonitorPanelForm.OnThemeChanged` (lines 183-213), also `MainForm.OnThemeChanged` (lines 85-106)
**Apply to:** `MainForm.OnDisplaySettingsChanged` (new), any future `MonitorTile` event handler that touches UI state from a non-UI-thread-guaranteed event
```csharp
if (IsDisposed) return;
if (InvokeRequired)
{
    try { BeginInvoke(new Action(() => Handler(sender, e))); }
    catch (ObjectDisposedException) { }
    return;
}
try { /* actual work */ }
catch { /* cosmetic/best-effort: must never crash the form */ }
```

### Lease-before-dialog, `using`-scoped mutation
**Source:** `MonitorPanelForm.DisableMonitor`/`EnableMonitor` (lines 256-346), `ToggleOrchestrator.BeginExclusiveMonitorAccess()` (`ToggleOrchestrator.cs` lines 88-97)
**Apply to:** `MainForm.OnTileAction` (new tile-click handler)
```csharp
IDisposable? lease = TryAcquireMonitorAccess(); // wraps _orchestrator.BeginExclusiveMonitorAccess(), catches ToggleInProgressException
if (lease is null) return;
using (lease)
{
    // ... confirm dialog if disabling, re-validate device path, then mutate ...
}
RefreshMonitorTiles();
```
**Never simplify away** — see Pitfall 3: this lease is not redundant with `BtnToggle_Click`'s implicit `_busy` guard even once both live in the same class.

### Stable device-path identity, never row-index/name matching
**Source:** `MonitorPanelForm.cs` line 138 comment + `DgvMonitorPanel_CellClick` line 220
**Apply to:** `MonitorTile.DevicePath` property, `MainForm`'s tile-to-monitor lookup in `RefreshMonitorTiles()`/`OnTileAction`
```csharp
// Key every tile by DevicePath, NEVER by array index or display-name matching.
```

### Canonical monitor ordering shared between tile numbering and Identify (Pitfall 6 / Assumption A1)
**Source:** RESEARCH.md Pitfall 6, Open Question 1 (no existing code owns this yet — it is new logic this phase must introduce, sort by `DevicePath` at the top of `RefreshMonitorTiles()`)
**Apply to:** `MainForm.RefreshMonitorTiles()` (assigns both tile position and tile number) and the ported `BtnIdentify_Click` (must consume the exact same ordered list, e.g. a `_lastKnownMonitors` field populated by `RefreshMonitorTiles()`)
```csharp
// Sort once, share everywhere -- both tile numbering and Identify's overlay
// numbering must derive from this single ordered list, or the two can disagree
// for the same physical monitor after a hotplug reshuffle (Pitfall 6).
_lastKnownMonitors = _monitorController.GetAllMonitors()
    .OrderBy(m => m.DevicePath, StringComparer.Ordinal)
    .ToList();
```

### GDI resource lifecycle — allocate once, dispose deterministically
**Source:** `MonitorPanelForm` constructor comment (lines 42-48) + `MonitorPanelForm.Designer.cs` `Dispose(bool)` (lines 21-40)
**Apply to:** `MonitorTile` (any cached `Pen`/`Brush`/`GraphicsPath` fields), `MonitorIconGeometry` (prefer `using` per-call since paths are built fresh per paint, not cached — no long-lived Bitmap allocation needed since tiles paint live via `OnPaint`, unlike the old grid's pre-rendered status-dot Bitmaps)
```csharp
// Built ONCE and shared/cached where the same visual is reused across many
// paints/refreshes -- allocating fresh GDI objects per-paint in a long
// tray-resident session leaks/exhausts handles (Security Domain threat table).
```

### Exception-swallow-with-Trace convention for defensive/cosmetic code paths
**Source:** repeated throughout `MonitorPanelForm.cs` (`PopulateMonitorGrid` lines 97-104, `BtnIdentify_Click` lines 397-400) and `ThemeApplier.cs` (every method)
**Apply to:** `MonitorTile` paint-time exceptions must never propagate (WinForms `OnPaint` exceptions can be fatal), `RefreshMonitorTiles()` enumeration failures
```csharp
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"MethodName: description of what failed: {ex}");
    // degrade to a safe empty/default state, never crash the caller
}
```

## No Analog Found

None — every file this phase touches has a direct source-of-truth analog, either because it is an explicit port target (`MonitorIconGeometry.cs` ← `IconGeometry.cs`, `MainForm`'s new mutation logic ← `MonitorPanelForm`'s existing logic) or a same-file extension (`ThemeApplier.cs`, `Program.cs`). The one structurally novel element — `MonitorTile` as this codebase's first-ever `UserControl` — still has two directly relevant behavioral analogs (`MonitorPanelForm`'s status-dot rendering and cell-click dispatch) even though no prior `UserControl` *class shape* exists to copy; RESEARCH.md's own Pattern 1 code block is the closest thing to a structural analog and should be treated as the primary implementation skeleton.

## Metadata

**Analog search scope:** `src/RigToggle.App/`, `src/RigToggle.Core/`, `src/RigToggle.Windows/`, `src/RigToggle.IconGen/` (confirmed via `find`/`ls` that no `Controls/`, `UserControl`, or `FlowLayoutPanel`/`TableLayoutPanel` precedent exists anywhere in the solution — this phase is the first to introduce all three)
**Files scanned:** `MainForm.cs`/`.Designer.cs`, `MonitorPanelForm.cs`/`.Designer.cs`, `MonitorConfirmDialog.cs`, `MonitorIdentifyOverlay.cs`, `ThemeApplier.cs`, `Program.cs`, `ToggleOrchestrator.cs`, `IMonitorController.cs`, `MonitorInfo.cs`, `IconGeometry.cs` (10 files read directly this session; `WindowsMonitorController.cs`, `IconWriter.cs`, `IconGen/Program.cs` referenced via RESEARCH.md's own prior direct reads, not re-read here — no new information needed beyond what RESEARCH.md already extracted from them)
**Pattern extraction date:** 2026-08-09
