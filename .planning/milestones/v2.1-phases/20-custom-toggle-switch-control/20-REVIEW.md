---
phase: 20-custom-toggle-switch-control
reviewed: 2026-08-10T17:32:29Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - src/RigToggle.App/Controls/ToggleSwitch.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/MonitorConfirmDialog.Designer.cs
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.Core/ToggleInProgressException.cs
  - src/RigToggle.Tests/ToggleOrchestratorTests.cs
findings:
  critical: 1
  warning: 3
  info: 3
  total: 7
status: issues_found
---

# Phase 20: Code Review Report

**Reviewed:** 2026-08-10T17:32:29Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Reviewed the new owner-drawn `ToggleSwitch` control and its integration into `MainForm`
(replacing the old `btnToggle`/`lblMode` pair), plus the small supporting diffs in
`ThemeApplier.cs`, `MonitorConfirmDialog.Designer.cs`, `ToggleInProgressException.cs`, and
`ToggleOrchestratorTests.cs` (all comment/rename-only in this phase).

The rename/wiring itself is clean — `btnToggle`/`lblMode` are fully removed with no
dangling references, `ThemeApplier.ThemeToggleSwitch` is correctly reached from both
required call sites, and `ToggleInProgressException`'s exception-type contract is intact.

However, one genuine correctness bug (CR-01) survived from the pre-phase `BtnToggle_Click`
body into the renamed `ToggleSwitch_ActionRequested`: the Rig-mode confirmation dialog is
shown without acquiring the exclusive orchestrator lease that this same codebase already
uses, for the exact same class of dialog, in `OnTileAction`. A geometry bug in the new
`ToggleSwitch`'s focus ring (WR-01) and a keyboard-autorepeat issue (WR-02) round out the
control-level findings. A pattern of inconsistent settings-load error handling in
`MainForm.cs` (WR-03) is also flagged since the whole file is in scope.

## Critical Issues

### CR-01: Rig-mode confirmation dialog is shown without the exclusive-access lease, unlike the identical tile dialog

**File:** `src/RigToggle.App/MainForm.cs:469-482` (compare with `MainForm.cs:667-680` and `693-706`)

**Issue:** `OnTileAction` explicitly acquires `_orchestrator.BeginExclusiveMonitorAccess()`
via `TryAcquireMonitorAccess()` *before* calling `MonitorConfirmDialog.ShowDialog(this)`,
with a doc comment spelling out exactly why:

> "19-RESEARCH.md Pitfall 3: acquires the shared ToggleOrchestrator lease BEFORE
> MonitorConfirmDialog.ShowDialog() opens, because ShowDialog() runs a nested message loop
> that dispatches WM_HOTKEY -- without this, a hotkey-triggered toggle could start
> underneath a half-finished tile action."

`ToggleSwitch_ActionRequested` (the handler that fires when the user clicks the toggle
switch to go to Rig Mode) opens the *same* `MonitorConfirmDialog` type, via the same
`ShowDialog(this)` nested-message-loop mechanism, but does **not** acquire any lease first:

```csharp
using var confirmDialog = new MonitorConfirmDialog(disableNames, enableNames, _themeProvider);
if (confirmDialog.ShowDialog(this) != DialogResult.OK)
{
    return; // user cancelled — nothing mutated
}
...
result = _orchestrator.ToggleToRigMode();   // lease/busy flag only engages HERE
```

`ToggleOrchestrator`'s `_busy` guard (CORE-06) is only set for the duration of
`ToggleToRigMode()`/`ToggleToNormalMode()` themselves — not while this confirmation dialog
is open. While the dialog's nested message loop is pumping (i.e. before the user clicks
Continue), a concurrently fired global hotkey (TRIG-01) or tray "Switch mode" click
(TRAY-03) reaches `PerformBackgroundToggle()`, which calls `ToggleToRigMode()` /
`ToggleToNormalMode()` directly and **will succeed**, actually mutating monitor/audio
state. When the user then clicks "Continue" on the now-stale dialog, a second toggle fires
immediately on top of the state the background trigger just produced — the exact
"hotkey-triggered toggle starts underneath a half-finished action" race this codebase's own
`OnTileAction` fix was written to prevent, left unpatched on the primary toggle path.

**Fix:** Acquire the same lease before showing the dialog, mirroring `OnTileAction`:

```csharp
using var settings = ...; // existing settings/name resolution
IDisposable? lease = TryAcquireMonitorAccess();
if (lease is null) return;

using (lease)
{
    using var confirmDialog = new MonitorConfirmDialog(disableNames, enableNames, _themeProvider);
    if (confirmDialog.ShowDialog(this) != DialogResult.OK) return;
    if (confirmDialog.DontAskAgain) { ... }
    result = _orchestrator.ToggleToRigMode();
}
```

## Warnings

### WR-01: ToggleSwitch focus ring is clipped by ~half its own pen width against the control's right edge

**File:** `src/RigToggle.App/Controls/ToggleSwitch.cs:338-360, 449-460`

**Issue:** `ringMargin` (reserved space between the track's right edge and
`ClientSize.Width`) is computed as `h * FocusRingWidthFraction / 2f` — i.e. *half* the focus
ring's pen width. That margin is exactly enough to contain the Off-state outline pen (which
is drawn directly on `trackPath` and only needs one half-pen-width of outward bleed), but
the focus ring is built from a *second*, independently outward-inflated rectangle
(`ringRect`, inflated by `penWidth / 2` beyond the track) which is then itself stroked with
a pen of the *same* `penWidth`. Working through the algebra: the track's right edge sits at
`w - ringMargin` where `ringMargin = penWidth / 2`; the ring path's right edge is
`track.Right + penWidth/2`; the final drawn ink right edge (path edge + another
`penWidth/2` from the stroke itself) lands at `track.Right + penWidth`, which equals
`w + penWidth/2` — i.e. the ring's outer edge is clipped by `ringMargin` pixels past the
control's `ClientSize.Width` on every Tab-focus. This directly contradicts the "2026-08-10
rig fix round 1 (check 11)" comment's stated purpose (reserving room specifically to stop
this clipping).

**Fix:** Reserve a full `penWidth` of margin (not `penWidth / 2`) on the right side, e.g.
change `ringMargin` to `h * FocusRingWidthFraction` (drop the `/ 2f`) in both `OnPaint` and
`GetPreferredSize`, or equivalently halve the ring's own outward `ringRect` inflation so
only one pen-width of outward bleed is ever produced instead of two.

### WR-02: Space/Enter re-fire ActionRequested on every OS key-autorepeat while held

**File:** `src/RigToggle.App/Controls/ToggleSwitch.cs:239-250`

**Issue:** `ProcessCmdKey` fires `ActionRequested` on every `WM_KEYDOWN` for `Keys.Space`/
`Keys.Return` while the control is focused. Windows sends repeated `WM_KEYDOWN` messages
(OS key-autorepeat) for as long as a key is held, and `ProcessCmdKey` is invoked for each
one — unlike a native `Button`, which fires its click on key-*up* specifically to avoid
firing multiple times from a single held keypress. Holding Space/Enter on the switch will
therefore fire `ActionRequested` repeatedly; the first call performs the toggle, and every
subsequent repeat while the orchestrator is still busy throws `ToggleInProgressException`,
which `MainForm.ToggleSwitch_ActionRequested` surfaces as a `MessageBox.Show(...,
MessageBoxIcon.Information)` — i.e. holding the key down can stack up multiple modal
dialogs the user has to dismiss.

**Fix:** Track whether the key is already down (e.g. an `_spaceOrEnterDown` flag set on the
first `ProcessCmdKey` hit for that key and cleared on `OnKeyUp`), or move the trigger to
`OnKeyUp` instead of `ProcessCmdKey`'s key-down routing, matching `Button`'s own convention.

### WR-03: `_settingsStore.Load()` is unguarded at two call sites, inconsistent with this file's own defensive pattern

**File:** `src/RigToggle.App/MainForm.cs:712` (`OnTileAction`) and `src/RigToggle.App/MainForm.cs:1467` (`TryRegisterConfiguredHotkey`, called unguarded from `OpenSettingsDialog` at line 559)

**Issue:** `MainForm.cs` wraps `_settingsStore.Load()` in `try/catch` at four separate call
sites specifically because a `settings.json` read failure is treated as an expected,
recoverable condition (`ApplyTrayVisibility`, `MainForm_FormClosing`, `MainForm_Resize`, and
the outer try/catch around `ToggleSwitch_ActionRequested`'s own `Load()` call). Two other
call sites do not follow this pattern:

- `OnTileAction` (line 712) calls `var settings = _settingsStore.Load();` directly inside
  the `using (lease)` block with no surrounding `try/catch` — an I/O hiccup here (e.g. an
  AV lock or a concurrent Settings-Save) propagates out of a `MonitorTile.ActionRequested`
  event handler unhandled.
- `OpenSettingsDialog` calls `TryRegisterConfiguredHotkey()` (line 559) after every
  Settings-dialog close, and that method's own `_settingsStore.Load()` (line 1467) has no
  `try/catch` around it either — unlike its only other caller, `RegisterHotkeyAtStartup()`,
  which wraps the whole call in `try/catch (Exception ex)`. Since `OpenSettingsDialog` is
  invoked from both the Settings gear button and the tray Settings menu item, an unguarded
  throw here surfaces on a very common, everyday interaction.

**Fix:** Wrap both call sites the same way the other four already are, e.g.:

```csharp
AppSettings settings;
try { settings = _settingsStore.Load(); }
catch { settings = new AppSettings(); }
```

and consider wrapping `OpenSettingsDialog`'s `TryRegisterConfiguredHotkey()` call in a
try/catch that traces and continues, matching `RegisterHotkeyAtStartup`'s own handling.

## Info

### IN-01: Inline magic-number fractions in `MainForm.cs` paint helpers

**File:** `src/RigToggle.App/MainForm.cs:1055-1058, 1112, 1128-1132, 1185`

**Issue:** `DrawButtonFocusRing`, `BtnIdentify_Paint`, and `BtnSettings_Paint` all use
inline literal fractions (`4f / 32f`, `2f / 32f`, `0.2f`) rather than named constants, in
contrast to `ToggleSwitch.cs`'s explicit convention (called out in its own doc comment) of
naming every fractional geometry value as a `private const float ...Fraction` to avoid bare
multi-digit literals inside paint code.

**Fix:** Not required to change, but consider hoisting these into named constants
(`IdentifyCornerRadiusFraction`, `FocusRingWidthFraction`, `GearGlyphInsetFraction`) for
consistency with the new control's own stated convention.

### IN-02: No overlap guard between `btnIdentify` and `toggleSwitch` on the shared action row

**File:** `src/RigToggle.App/MainForm.cs:931-943`

**Issue:** `btnIdentify` is placed at a fixed left position (`Scaled(IdentifyWidthPx)` wide)
and `toggleSwitch` is right-aligned using its own `GetPreferredSize`-measured width
(driven by `TextRenderer.MeasureText("Rig Mode", Font)`). There is no `Math.Max`/clamp
ensuring a minimum gap (or that they don't overlap) between `btnIdentify.Right` and
`toggleSwitch.Left`. At unusually large system font scaling (e.g. Windows large-text
accessibility settings), the two could visually collide since one width scales through
`Scaled()` (a form-font-height ratio) and the other scales through `Font`-based text
measurement — they are not guaranteed to grow in lockstep.

**Fix:** Low priority given normal DPI/font ranges; if this ever needs to be hardened,
clamp `toggleRowWidth`'s effective left edge to `btnIdentify.Right + Scaled(GapMdPx)` at
minimum.

### IN-03: Redundant double-buffering configuration in `ToggleSwitch` constructor

**File:** `src/RigToggle.App/Controls/ToggleSwitch.cs:100-129`

**Issue:** The constructor both `SetStyle(ControlStyles.OptimizedDoubleBuffer | ..., true)`
and separately sets `DoubleBuffered = true;`. `Control.DoubleBuffered`'s setter internally
sets the same `OptimizedDoubleBuffer`/`AllPaintingInWmPaint`/`UserPaint` style bits already
set explicitly above it — the two statements are redundant with each other (harmless, but
dead/duplicated configuration).

**Fix:** Drop one of the two; keeping the explicit `SetStyle` call alone (as
`MonitorTile.cs` presumably does, per the file's own stated "matching MonitorTile's field
discipline" convention) is sufficient.

---

_Reviewed: 2026-08-10T17:32:29Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
