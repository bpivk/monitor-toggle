---
phase: 22-settingsform-layout-pass
reviewed: 2026-08-16T00:00:00Z
depth: standard
files_reviewed: 2
files_reviewed_list:
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
findings:
  critical: 2
  warning: 2
  info: 1
  total: 5
status: issues_found
fixed_inline:
  - CR-01
  - CR-02
---

**Post-review note (2026-08-16):** CR-01 and CR-02 were fixed inline during Phase 22's execute-phase code-review gate (both are narrow, low-risk changes confined to the `OnLoad` clamp added by 22-04) — see `SettingsForm.cs`'s `OnLoad` override. Build (0 errors) and the 82-test suite (82/82) were re-confirmed green after the fix. WR-01, WR-02, and IN-01 were left as recorded findings, not fixed, since they fall outside this phase's layout/resize scope.

# Phase 22: Code Review Report

**Reviewed:** 2026-08-16T00:00:00Z
**Depth:** standard
**Files Reviewed:** 2
**Status:** issues_found

## Summary

Reviewed `SettingsForm.cs` and `SettingsForm.Designer.cs` at standard depth, with focused attention on the new `OnLoad` override and the `AutoSize`/`MinimumSize` Designer changes from the 22-04/22-05 gap-closure plans, per the review brief.

The rig-hardware verification recorded in `22-05-SUMMARY.md` ("all 17 checks PASS") only exercises the code paths that were actually triggered during that session. Two of the checks most relevant here — Check 15 ("whole window fits screen on fresh open") and Check 16 (resize at 150%) — pass trivially whenever the dialog's preferred content size stays *under* the screen's working area, which is the common case on a normal-sized monitor. That leaves the `Math.Min(...)` clamp branch in `OnLoad` (the actual overshoot-prevention code, added specifically to satisfy threat T-22-16) effectively untested by the rig session: nothing in the recorded checks demonstrates the window actually overflowing content and then being successfully clamped. Reviewing that code path directly surfaces two real logic errors in the clamp (CR-01, CR-02) that would not show up unless the user's screen is small/low-DPI relative to the dialog's content, or the dialog is opened while the owning window sits on a different monitor than the one Windows currently considers "primary" — both plausible scenarios for this project's actual multi-monitor rig-desktop use case.

## Critical Issues

### CR-01: Working-area clamp ignores window chrome, so the outer window can still overshoot the screen it was clamped to

**File:** `src/RigToggle.App/SettingsForm.cs:159-167`
**Issue:**
```csharp
var workingArea = Screen.FromControl(this).WorkingArea;
var targetWidth = System.Math.Min(preferredSize.Width, workingArea.Width);
var targetHeight = System.Math.Min(preferredSize.Height, workingArea.Height);
this.ClientSize = new System.Drawing.Size(targetWidth, targetHeight);
```
`preferredSize` (from `tlpRoot.PreferredSize`) and `targetWidth`/`targetHeight` are **client-area** dimensions, but `workingArea.Width`/`workingArea.Height` are the screen's **outer** working-area dimensions. `Form.ClientSize` excludes the non-client chrome (title bar + resizable border), so `Form.Size` (the actual outer window footprint on screen) equals `ClientSize` plus that chrome — commonly ~16-24px of extra width and ~35-60px of extra height depending on DPI scale, more at 150%.

When the clamp branch actually binds (i.e., `preferredSize.Width/Height > workingArea.Width/Height` — exactly the "150% display scale" scenario this code's own comments say it exists to handle), setting `ClientSize` to the *full* working-area dimension makes the resulting outer `Size` **larger** than the working area by the chrome amount. The window can still be partially off-screen (its bottom edge pushed under/past the taskbar, or its right edge past the screen edge) — the exact failure this gap-closure plan (T-22-16, Rig Check 15) was written to eliminate.

This is not exercised by the recorded rig verification: Check 15 passed because on that hardware/content combination the clamp branch never bound (`preferredSize` stayed under `workingArea`), so the bug is latent, not disproven.

**Fix:** Subtract the window's non-client size from the working area before comparing/assigning, e.g.:
```csharp
var workingArea = Screen.FromControl(this).WorkingArea;
var chrome = this.Size - this.ClientSize; // non-client width/height at current size
var maxClientWidth = System.Math.Max(0, workingArea.Width - chrome.Width);
var maxClientHeight = System.Math.Max(0, workingArea.Height - chrome.Height);
var targetWidth = System.Math.Min(preferredSize.Width, maxClientWidth);
var targetHeight = System.Math.Min(preferredSize.Height, maxClientHeight);
this.ClientSize = new System.Drawing.Size(targetWidth, targetHeight);
```
(Note: `this.Size - this.ClientSize` before the resize gives an approximation of chrome size that is stable across resizes for a given border style; alternatively query `SystemInformation.FrameBorderSize`/`CaptionHeight`, scaled for DPI.)

### CR-02: `Screen.FromControl(this)` in `OnLoad` measures the wrong monitor on multi-monitor setups

**File:** `src/RigToggle.App/SettingsForm.cs:164`
**Issue:** The method's own comment (lines 138-142) correctly documents that `FormStartPosition.CenterParent` positioning is applied *after* `OnLoad` returns — i.e., at the point `Screen.FromControl(this)` is called, `this.Location` has not yet been moved to sit over the owner. For a form with no explicit `Location` set in the Designer, that means `Screen.FromControl(this)` resolves against whatever monitor contains the form's pre-positioning default bounds — in practice, the primary display at virtual-desktop coordinate (0,0) — **not** the monitor the dialog will actually be centered on and shown on.

For this specific project (a sim-racing rig utility whose entire purpose is switching between two monitors — a desktop monitor and a rig monitor, plausibly at different resolutions/DPI), this means: if `MainForm`/the tray icon is on a non-primary monitor when Settings is opened, the working-area clamp in `OnLoad` is computed against the *primary* monitor's dimensions, then the window is centered onto the *actual* (possibly larger, possibly smaller, possibly different-DPI) monitor — the clamp bound has no relationship to the screen the window ends up on.

**Fix:** Derive the target screen from the owner/parent instead of `this`:
```csharp
var workingArea = (this.Owner is not null
    ? Screen.FromControl(this.Owner)
    : Screen.FromPoint(System.Windows.Forms.Cursor.Position)).WorkingArea;
```
or defer the clamp to `OnShown` (after `CenterParent` has run and `this.Location` reflects the real target screen) — noting the method's own comment about why `OnShown` was rejected for the *general* sizing pass (it would visibly show the stale-then-resized window); the clamp specifically, as opposed to the base size computation, could still be safely re-validated/adjusted in `OnShown` without reintroducing that visible-resize problem, since it only ever shrinks.

## Warnings

### WR-01: Working-area clamp and descendant `MinimumSize` floors are not reconciled

**File:** `src/RigToggle.App/SettingsForm.cs:159-167`, `src/RigToggle.App/SettingsForm.Designer.cs:340-361`
**Issue:** `tlpModeColumns.MinimumSize` is hardcoded to `(0, 280)` and `dgvMonitors`/`dgvMonitorsNormal` each carry a `(0, 120)` floor, but `OnLoad`'s clamp can set `ClientSize` to any value down to `Screen.WorkingArea`'s dimensions with no lower bound check against these floors. On an unusually small or low-resolution working area (e.g., a small external monitor, a VM display, or a heavily-scaled small laptop panel), the clamp could produce a `ClientSize` shorter than the combined `280 + shared-section + button-row` minimum content height, which then fights the descendant `MinimumSize` constraints during layout — producing clipped/overlapping content despite the window nominally "fitting" the screen. Neither guarantee (fits the screen / never collapses below the floor) is preserved simultaneously in that case.
**Fix:** Clamp the *lower* bound too — e.g. `Math.Max(tlpModeColumns' minimum content height + shared section + buttons, Math.Min(preferredSize.Height, maxClientHeight))` — or explicitly decide (and document) which guarantee wins when they conflict, rather than leaving the outcome to whichever constraint happens to apply last in the layout pass.

### WR-02: `_settingsStore.Save()` call has no error handling, unlike every other side-effecting call in the same method

**File:** `src/RigToggle.App/SettingsForm.cs:1165`
**Issue:** `BtnSaveSettings_Click` wraps the autostart registry write (lines 1178-1214) and the hotkey registration (lines 1226-1236) in defensive try/catch blocks with inline warnings, explicitly reasoning that a failure "must never crash Settings." The actual settings persistence call itself, `_settingsStore.Save(settingsToSave);` (line 1165), has no such guard. A failure here (disk full, `settings.json` locked by another process/AV scanner, permission denied on `%APPDATA%`) throws unhandled out of a button-click event handler — inconsistent with the surrounding code's own stated robustness bar, and worse, since it's also the operation everything downstream depends on (tray visibility, autostart, hotkey re-registration all run only if `Save` doesn't throw first).
**Fix:**
```csharp
try
{
    _settingsStore.Save(settingsToSave);
}
catch (Exception ex)
{
    // surface via an existing warning label (or a new one) instead of crashing the dialog
    MessageBox.Show(this, $"Could not save settings: {ex.Message}", "Rig Toggle",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
    return;
}
```

## Info

### IN-01: Inconsistent namespace qualification in the new `OnLoad` override

**File:** `src/RigToggle.App/SettingsForm.cs:148, 157, 165-167`
**Issue:** `OnLoad(System.EventArgs e)` and its body (`System.Math.Min`, `System.Drawing.Size`) fully qualify types that are used unqualified everywhere else in this hand-written file (e.g. `SettingsForm_Load(object? sender, EventArgs e)` at line 248, `MouseEventArgs`, `KeyEventArgs` throughout). This is harmless (the project's implicit-usings setup makes both forms resolve identically) but is an inconsistent style within a single file that otherwise reads as deliberately curated.
**Fix:** Drop the `System.` prefixes to match the rest of the file's convention (`EventArgs e`, `Math.Min(...)`, `new Size(...)`), or, if the qualification was intentional to avoid ambiguity with some other in-scope type, add a short comment explaining why.

---

_Reviewed: 2026-08-16T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
