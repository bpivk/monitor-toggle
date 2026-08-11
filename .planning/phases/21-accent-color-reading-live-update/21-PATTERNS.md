# Phase 21: Accent-Color Reading & Live Update - Pattern Map

**Mapped:** 2026-08-11
**Files analyzed:** 7 (all modifications, no new files this phase)
**Analogs found:** 7 / 7 — every target file already exists and IS its own analog (this phase extends existing patterns in place; the "analog" for each file is its own current `CurrentTheme`/`ThemeChanged` handling, which the new `AccentColor`/`AccentColorChanged` code mirrors 1:1)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `src/RigToggle.Core/Abstractions/IThemeProvider.cs` | interface/contract | event-driven | itself (`CurrentTheme`/`ThemeChanged` members already on this interface) | exact — additive members on the same interface |
| `src/RigToggle.Windows/WindowsThemeProvider.cs` | service (OS adapter) | event-driven + request-response (registry/P-Invoke read) | itself (`ReadThemeFromRegistry` + `OnUserPreferenceChanged` diff-lock-raise block) | exact — same class, same method extended, same lock-diff-raise shape reused for a second field |
| `src/RigToggle.Windows/NativeMethods.cs` | utility (P/Invoke declarations) | request-response | itself (`DwmSetWindowAttribute` dwmapi.dll declaration, lines 138-139) | exact — sibling `[DllImport("dwmapi.dll")]` declaration in the same class |
| `src/RigToggle.App/MainForm.cs` | controller/view (WinForms form) | event-driven (subscribe) + request-response (paint-time color read) | itself (`ThemeChanged` subscription line 118, `AccentColor` property line 184, `ApplyDashboardTheming()` line 1019, `DrawButtonFocusRing` call sites lines 1179/1242) | exact — same file, parallel subscription + property pass-through |
| `src/RigToggle.App/ThemeApplier.cs` | utility (static per-control theming helpers) | transform (color assignment) | itself (`ThemeMonitorTile` lines 187-203, `ThemeToggleSwitch` lines 218-263) | exact — same two methods, ternary literal replaced by a passed-in live value |
| `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` | test double | event-driven | itself (`CurrentTheme`/`ThemeChanged`/`RaiseThemeChanged` pattern) | exact — same file, parallel `AccentColor`/`AccentColorChanged`/`RaiseAccentColorChanged` triad |
| `src/RigToggle.Tests/ThemeProviderContractTests.cs` | test (contract test) | request-response (assertion) | itself (`RaiseThemeChanged_InvokesSubscriberExactlyOnce_WithUpdatedCurrentTheme`) | exact — same test class, parallel test method against the fake |

**No analog-search outside these files was needed or performed.** RESEARCH.md's "Existing Code State" table (confirmed by direct repo read this session, zero drift) already identifies every file this phase touches and exactly what changes in each — this phase is a pure extension of an already-shipped pipeline, not new architecture. `DwmTitleBar.cs` was read as a **negative pattern reference only** (see Anti-Pattern note below) — it is explicitly NOT to be copied for this phase's P/Invoke call.

## Pattern Assignments

### `src/RigToggle.Core/Abstractions/IThemeProvider.cs` (interface, event-driven)

**Analog:** itself — extend in place, do not replace `CurrentTheme`/`ThemeChanged`

**Current full file** (19 lines):
```csharp
using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

public interface IThemeProvider
{
    AppTheme CurrentTheme { get; }

    event EventHandler? ThemeChanged;
}
```

**Pattern to apply:** Add `Color AccentColor { get; }` + `event EventHandler? AccentColorChanged;` as siblings to the existing two members. Needs `using System.Drawing;` added to the import block. Update the XML doc comment to describe the new members' contract using the same style as the existing comment (source-of-truth registry path, live-flip semantics, thread-marshalling warning) — mirror the existing comment's structure rather than write a fresh one from scratch.

---

### `src/RigToggle.Windows/WindowsThemeProvider.cs` (service/OS adapter, event-driven)

**Analog:** itself — the existing `_themeLock`/`_currentTheme`/`OnUserPreferenceChanged` triad

**Imports pattern** (lines 1-4):
```csharp
using System.Diagnostics;
using Microsoft.Win32;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;
```
(Add `using System.Drawing;` for `Color`.)

**Field + property pattern to copy** (lines 26-38):
```csharp
private readonly object _themeLock = new();
private AppTheme _currentTheme;

public AppTheme CurrentTheme
{
    get { lock (_themeLock) { return _currentTheme; } }
    private set { _currentTheme = value; }
}

public event EventHandler? ThemeChanged;
```
Mirror this exactly for accent color: a dedicated `_accentLock` (RESEARCH.md Pattern 2 explicitly recommends a **separate** lock from `_themeLock`, not reuse — "avoids serializing accent reads behind theme reads or vice versa; mirrors this class's existing one-lock-per-piece-of-state discipline"), a `_accentColor` field, an `AccentColor` get-only property, and an `AccentColorChanged` event.

**Constructor pattern** (lines 40-45):
```csharp
public WindowsThemeProvider()
{
    CurrentTheme = ReadThemeFromRegistry();
    Log($"Constructed: initial theme resolved to {CurrentTheme}");
    SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
}
```
Extend to also call `_accentColor = ReadAccentColor();` and fold the accent value into the existing `Log(...)` line (do not add a second `Log` call) — RESEARCH.md's Pattern 2 example shows: `Log($"Constructed: initial theme resolved to {CurrentTheme}, accent resolved to {_accentColor}");`. Do **not** add a second `SystemEvents.UserPreferenceChanged +=` subscription (D-02, this is the phase's explicit anti-pattern to avoid).

**Core diff-lock-raise pattern to copy verbatim in shape** (lines 51-71):
```csharp
private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
{
    var resolved = ReadThemeFromRegistry();
    AppTheme previous;
    bool changed;
    lock (_themeLock)
    {
        changed = resolved != _currentTheme;
        previous = _currentTheme;
        if (changed)
        {
            _currentTheme = resolved;
        }
    }

    if (changed)
    {
        Log($"Theme flip detected: {previous} -> {resolved}");
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
```
Add a second, independent block of the identical shape inside this same method (not a new method, not a new subscription) reading `ReadAccentColor()`, diffing against `_accentColor` under `_accentLock`, and raising `AccentColorChanged` only on genuine change. `Color`'s value equality on the packed ARGB int makes `resolved != _accentColor` work correctly without a custom comparer — confirmed in RESEARCH.md Pattern 2.

**Safe-default read pattern to copy** (lines 73-85):
```csharp
private static AppTheme ReadThemeFromRegistry()
{
    try
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var raw = key?.GetValue(ValueName);
        return raw is int i && i == 0 ? AppTheme.Dark : AppTheme.Light;
    }
    catch
    {
        return AppTheme.Light;
    }
}
```
This exact "never throw from a Load-time read, default to a safe value" shape is the template for the two new private read methods (`ReadAccentColorFromRegistry()` returning `Color?`, `ReadAccentColorFromDwm()` returning `Color` with `SystemColors.Highlight` as the safe default) — see RESEARCH.md Pattern 1 (lines 150-198 of RESEARCH.md) for the fully-worked byte-extraction code for both, already validated against this file's conventions. `ReadAccentColor()` composes them: `ReadAccentColorFromRegistry() ?? ReadAccentColorFromDwm()` (D-01's registry-primary, DWM-fallback ordering).

**Dispose pattern (unchanged, no new subscription to unsubscribe)** (line 87):
```csharp
public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
```
No change needed here — extending the one existing handler means there is nothing new to unsubscribe.

**Logging pattern** (lines 92-102):
```csharp
private static void Log(string message)
{
    try
    {
        Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] WindowsThemeProvider: {message}");
    }
    catch
    {
        // Logging is diagnostic-only; never let it affect theme detection.
    }
}
```
Reuse as-is — the new accent-diff block's `Log($"Accent color flip detected: {previousAccent} -> {resolvedAccent}");` call routes through this same helper, no new logging infrastructure.

---

### `src/RigToggle.Windows/NativeMethods.cs` (utility, request-response P/Invoke)

**Analog:** itself — the existing `DwmSetWindowAttribute` declaration

**Sibling declaration pattern to copy** (lines 134-139):
```csharp
internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

[DllImport("dwmapi.dll")]
internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
```
Add the new declaration as a further sibling in this same `internal static class NativeMethods` block, same file, same dwmapi.dll grouping — per RESEARCH.md's exact prescribed addition:
```csharp
[DllImport("dwmapi.dll")]
internal static extern int DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);
```
Keep it `internal` (matches every other member in this class) — `WindowsThemeProvider` is in the same `RigToggle.Windows` assembly and can call it directly, no `InternalsVisibleTo` grant needed.

**File-level convention note:** this class's header comment (lines 6-17) documents the class's scope; extend that comment to mention the new dwmapi.dll accent-color read, following the same one-line-per-capability style already used for the hotkey and DWM chrome additions.

---

### `src/RigToggle.App/MainForm.cs` (controller/view, event-driven + paint-time read)

**Analog:** itself — the existing `ThemeChanged` subscription, `AccentColor` property, `ApplyDashboardTheming()` funnel, and `DrawButtonFocusRing` call sites

**Subscription pattern to extend** (lines 116-118):
```csharp
// 12-02/D-05: live theme-follow -- WindowsThemeProvider raises this whenever
// the OS AppsUseLightTheme value genuinely flips while this form is alive.
_themeProvider.ThemeChanged += OnThemeChanged;
```
Add directly below it: `_themeProvider.AccentColorChanged += OnThemeChanged;` — reuses the exact same handler (`OnThemeChanged`), no new method, per RESEARCH.md's explicit "no new call sites" guidance. Do not create a separate `OnAccentColorChanged` handler.

**Property pattern to replace (not restructure)** (lines 179-184):
```csharp
private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;

// Same source/values as ThemeApplier.ThemeMonitorTile's AccentColor -- kept
private Color AccentColor => IsDark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;
```
Replace only the `AccentColor` getter body with a pass-through: `private Color AccentColor => _themeProvider.AccentColor;`. `IsDark` is untouched — it is a separate concern (light/dark, not accent). Update the stale comment above `AccentColor` (currently references the placeholder literal) to describe the live pass-through instead.

**Repaint funnel — no changes needed, but confirm the shape** (lines 1019-1035):
```csharp
private void ApplyDashboardTheming()
{
    foreach (MonitorTile tile in _tiles)
    {
        ThemeApplier.ThemeMonitorTile(tile, IsDark);
    }

    ThemeApplier.ThemeButton(btnIdentify, IsDark);
    ThemeApplier.ThemeButton(btnSettings, IsDark);
    ThemeApplier.ThemeToggleSwitch(toggleSwitch, IsDark);
    btnSettings.Invalidate();

    lblNoMonitors.ForeColor = IsDark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
}
```
Both `ThemeApplier.ThemeMonitorTile(...)` and `ThemeApplier.ThemeToggleSwitch(...)` calls here need an added `AccentColor` argument once `ThemeApplier`'s signatures change (see below) — e.g. `ThemeApplier.ThemeMonitorTile(tile, IsDark, AccentColor)`. This is the only edit needed in this method — it is already the single funnel both `OnThemeChanged` and `InitializeTrayState()` go through (19-RESEARCH.md Pitfall 1's two-call-site rule), so no new call sites are required.

**Paint-time consumer pattern (unchanged call shape, values now live)** (lines 1179, 1242):
```csharp
using var ringPen = new Pen(AccentColor, penWidth);
...
DrawButtonFocusRing(e.Graphics, btnSettings.ClientRectangle, AccentColor);
```
No code change needed at these two call sites — they already read `AccentColor` (the property above), which now transparently returns the live value once the property body changes.

---

### `src/RigToggle.App/ThemeApplier.cs` (utility, transform)

**Analog:** itself — `ThemeMonitorTile` and `ThemeToggleSwitch`

**Current pattern (both methods currently self-derive the color from `dark`)** — `ThemeMonitorTile` (lines 187-203):
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

`ThemeToggleSwitch` (lines 218-263, accent-relevant lines only):
```csharp
public static void ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark)
{
    try
    {
        toggleSwitch.BackColor = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;

        // D-09: same literal as tile.AccentColor. Phase 21/THEME-07
        // replaces this pair with a live-read Windows accent color —
        // this is the single line that changes then.
        toggleSwitch.OnColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;

        // Same as tile.FocusRingColor.
        toggleSwitch.FocusRingColor = dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight;

        toggleSwitch.OffOutlineColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.GrayText;
        toggleSwitch.OffHoverFillColor = dark ? Color.FromArgb(80, 80, 86) : SystemColors.ControlLight;
        toggleSwitch.OffPressFillColor = dark ? Color.FromArgb(18, 18, 20) : SystemColors.ControlDarkDark;
        toggleSwitch.IndeterminateColor = dark ? Color.FromArgb(90, 90, 90) : Color.FromArgb(200, 200, 200);
        toggleSwitch.ThumbColor = Color.White;
        toggleSwitch.ThumbOutlineColor = dark ? Color.FromArgb(160, 160, 160) : SystemColors.ControlDark;
        toggleSwitch.LabelColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;

        toggleSwitch.Invalidate();
    }
    catch
    {
        // Cosmetic-only — leave the control unchanged on failure.
    }
}
```

**Pattern to apply:** the comment at line 225-227 is a pre-planted marker (written during Phase 20) explicitly identifying this as "the single line that changes then." Add an `accentColor` parameter to both method signatures — `ThemeMonitorTile(MonitorTile tile, bool dark, Color accentColor)` and `ThemeToggleSwitch(ToggleSwitch toggleSwitch, bool dark, Color accentColor)` — and replace exactly the four `dark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight` occurrences (tile's `AccentColor`/`FocusRingColor`, toggle's `OnColor`/`FocusRingColor`) with `accentColor`. Every other line in both methods (BackColor, ForeColor, IconOffColor, HoverBackColor, OffOutlineColor, hover/press fills, IndeterminateColor, ThumbColor, LabelColor) is untouched — those are explicitly gray-scale/theme-independent per the existing method doc comments ("AccentColor is deliberately confined to..."), do not widen scope beyond the four identified lines (D-04). Keep the try/catch cosmetic-fail-silent wrapper exactly as-is — this is the established convention for every method in this file, not something to change for this phase.

**Call-site signature propagation:** `MainForm.ApplyDashboardTheming()` (lines 1023, 1028) must pass `AccentColor` (the `MainForm` property) as the new third argument at both call sites — see MainForm section above.

---

### `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs` (test double, event-driven)

**Analog:** itself — full 25-line file, extend in place

**Current full file:**
```csharp
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

public sealed class FakeThemeProvider : IThemeProvider
{
    public AppTheme CurrentTheme { get; set; }

    public event EventHandler? ThemeChanged;

    public void RaiseThemeChanged(AppTheme newTheme)
    {
        CurrentTheme = newTheme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

**Pattern to apply:** add `using System.Drawing;`, then mirror the three-member triad exactly:
```csharp
public Color AccentColor { get; set; }

public event EventHandler? AccentColorChanged;

public void RaiseAccentColorChanged(Color newAccentColor)
{
    AccentColor = newAccentColor;
    AccentColorChanged?.Invoke(this, EventArgs.Empty);
}
```
Preserve the "assign then invoke" ordering `RaiseThemeChanged` already establishes (so subscribers observe the new value at invocation time) — this is the exact convention `RaiseAccentColorChanged` must copy, per the file's own doc comment ("mirrors WindowsThemeProvider's real behavior of assigning CurrentTheme before invoking ThemeChanged").

---

### `src/RigToggle.Tests/ThemeProviderContractTests.cs` (test, request-response assertion)

**Analog:** itself — `RaiseThemeChanged_InvokesSubscriberExactlyOnce_WithUpdatedCurrentTheme`

**Pattern to copy verbatim in shape** (lines 15-33):
```csharp
[Fact]
public void RaiseThemeChanged_InvokesSubscriberExactlyOnce_WithUpdatedCurrentTheme()
{
    var provider = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
    int invocationCount = 0;
    AppTheme? observedTheme = null;

    provider.ThemeChanged += (_, _) =>
    {
        invocationCount++;
        observedTheme = provider.CurrentTheme;
    };

    provider.RaiseThemeChanged(AppTheme.Dark);

    Assert.Equal(1, invocationCount);
    Assert.Equal(AppTheme.Dark, observedTheme);
    Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
}
```
Add a parallel `RaiseAccentColorChanged_InvokesSubscriberExactlyOnce_WithUpdatedAccentColor` test with the identical shape (arrange fake with an initial `AccentColor`, subscribe and record `invocationCount`/`observedAccent`, call `RaiseAccentColorChanged(someOtherColor)`, assert invocation count 1 + observed value + final property value). Use two visually/numerically distinct `Color.FromArgb(r,g,b)` values (e.g. a default and a saturated test color) so the assertion is unambiguous, mirroring how the existing test uses `AppTheme.Light`/`AppTheme.Dark` as clearly distinct states. Needs `using System.Drawing;` added to the test file's imports. The `AppTheme_HasExactlyLightAndDarkMembers` test is unrelated and needs no counterpart — `AccentColor` is a `Color`, not an enum with a fixed member set.

---

## Shared Patterns

### Lock-guarded diff-then-raise (the load-bearing pattern for this whole phase)
**Source:** `src/RigToggle.Windows/WindowsThemeProvider.cs` lines 26-38, 51-71
**Apply to:** `WindowsThemeProvider`'s new `AccentColor`/`AccentColorChanged` members — the entire phase's D-02 requirement is "reuse this pattern with a second, independent lock/field/diff, not a second subscription." Do not invent a different concurrency strategy.
```csharp
private readonly object _accentLock = new();
private Color _accentColor;

public Color AccentColor
{
    get { lock (_accentLock) { return _accentColor; } }
}

public event EventHandler? AccentColorChanged;
```
(Note: unlike `CurrentTheme`, `AccentColor`'s setter is fully private/internal to the diff block rather than exposing a `private set` — RESEARCH.md's Pattern 2 example shows the field assigned directly inside the lock block in `OnUserPreferenceChanged`, not through a property setter. Either shape is acceptable as long as all writes stay lock-guarded; match whichever is cleaner against the final `CurrentTheme` implementation the planner chooses.)

### "Never throw from a Load-time/OS read, default to a safe value"
**Source:** `src/RigToggle.Windows/WindowsThemeProvider.cs` lines 73-85 (`ReadThemeFromRegistry`)
**Apply to:** the two new private methods `ReadAccentColorFromRegistry()` (returns `Color?`, `null` on any failure) and `ReadAccentColorFromDwm()` (returns `Color`, `SystemColors.Highlight` on any failure) — both wrap their body in try/catch with no rethrow, exactly like the existing method.

### Cosmetic-only fail-silent wrapper
**Source:** every method in `src/RigToggle.App/ThemeApplier.cs` (e.g. lines 189-202)
**Apply to:** `ThemeMonitorTile`/`ThemeToggleSwitch`'s existing try/catch bodies are untouched by this phase's edit — the new `accentColor` parameter is just one more value assigned inside the same already-wrapped try block. Do not add a second try/catch around only the new lines.

### Two-call-site theming funnel (19-RESEARCH.md Pitfall 1's rule)
**Source:** `src/RigToggle.App/MainForm.cs` `ApplyDashboardTheming()` (lines 1019-1035), called from both `OnThemeChanged` (line 167) and `InitializeTrayState()` (line 267)
**Apply to:** no new call sites needed this phase — `AccentColorChanged` is wired to the same `OnThemeChanged` handler, which already calls `ApplyDashboardTheming()`. This is a constraint to verify, not a pattern to newly implement: confirm the planner's plan does NOT add a separate accent-only repaint path that bypasses this funnel.

### Public façade for cross-assembly P/Invoke access — explicitly NOT needed this phase
**Source:** `src/RigToggle.Windows/DwmTitleBar.cs` (full file) — the existing façade pattern for `DwmSetWindowAttribute`
**Do NOT apply to:** `DwmGetColorizationColor`. RESEARCH.md's Anti-Patterns section explicitly flags this: `DwmTitleBar` exists only because `RigToggle.App` (a different assembly) needs to call an `internal NativeMethods` member. `WindowsThemeProvider` is already inside `RigToggle.Windows`, the same assembly `NativeMethods` lives in, so it must call `NativeMethods.DwmGetColorizationColor` directly — adding a public façade class here would be unrequested extra surface area. Included here specifically so the planner does not mistakenly generalize `DwmTitleBar` as "the" pattern for any new dwmapi.dll call.

## No Analog Found

None. Every file this phase touches already exists with a directly-extensible sibling pattern in the same file (confirmed by RESEARCH.md's direct-read "Existing Code State" table, zero drift). No new files are created this phase — RESEARCH.md's "Recommended Project Structure" section states this explicitly: "No new files. All changes are additive edits to the five files already named in `21-CONTEXT.md`'s canonical refs... plus the two test files."

## Metadata

**Analog search scope:** `src/RigToggle.Core/Abstractions/`, `src/RigToggle.Windows/`, `src/RigToggle.App/`, `src/RigToggle.App/Controls/`, `src/RigToggle.Tests/`, `src/RigToggle.Tests/Doubles/` — all files directly named in `21-CONTEXT.md` canonical refs and RESEARCH.md's Existing Code State table were read in full or via targeted offset/limit reads this session; `MonitorTile.cs`/`ToggleSwitch.cs` (pure color-sink controls, confirmed via RESEARCH.md as needing zero change) and `MonitorConfirmDialog.cs`/`SettingsForm.cs` (confirmed out of scope per D-04) were not re-read since RESEARCH.md already verified their current state with zero drift and no changes are needed in either this phase.
**Files scanned:** 7 target files (all read this session) + 1 negative-reference file (`DwmTitleBar.cs`, read to document why its pattern must NOT be copied)
**Pattern extraction date:** 2026-08-11
