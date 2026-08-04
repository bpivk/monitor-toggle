---
phase: 12-theme-infrastructure-live-theme-following
reviewed: 2026-08-03T07:16:06Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/MonitorConfirmDialog.cs
  - src/RigToggle.App/MonitorConfirmDialog.Designer.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.Core/Abstractions/IThemeProvider.cs
  - src/RigToggle.Core/Models/AppTheme.cs
  - src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
  - src/RigToggle.Tests/ThemeProviderContractTests.cs
  - src/RigToggle.Windows/DwmTitleBar.cs
  - src/RigToggle.Windows/NativeMethods.cs
  - src/RigToggle.Windows/WindowsThemeProvider.cs
findings:
  critical: 3
  warning: 2
  info: 1
  total: 6
status: issues_found
---

# Phase 12: Code Review Report

**Reviewed:** 2026-08-03T07:16:06Z
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Reviewed the theme-infrastructure phase (live light/dark theme-following: `IThemeProvider`/`WindowsThemeProvider`, `ThemeApplier`, `DwmTitleBar`/`NativeMethods` DWM P/Invoke, and the theming wiring added to `MainForm`, `MonitorConfirmDialog`, and `SettingsForm`). The plumbing (registry read, `SystemEvents.UserPreferenceChanged` diffing, marshal-then-try/catch event handlers, subscribe/unsubscribe lifecycle) is generally sound and well-documented. However, tracing the actual DWM/WinForms calls against the three rig-tester-reported symptoms surfaces three concrete, provable BLOCKER-level coverage gaps that fully explain all three reported symptoms — this is not "cosmetic edge cases," it is the core feature (dark title bar, themed buttons, themed audio pickers) simply never being wired up for those specific controls. Two further WARNING-level robustness gaps and one INFO-level duplication issue round out the report.

## Critical Issues

### CR-01: Title bar never actually gets the dark-mode DWM attribute set — explains rig finding #1

**File:** `src/RigToggle.Windows/DwmTitleBar.cs:23-30`, `src/RigToggle.Windows/NativeMethods.cs:128-135`

**Issue:** `DwmTitleBar.ApplyRoundedCornersAndMica` — the only DWM-attribute call site in the entire codebase (confirmed via `grep -rn DWMWA_USE_IMMERSIVE_DARK_MODE`) — sets exactly two attributes:
```csharp
int corner = DWMWCP_ROUND;
NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

int backdrop = DWMSBT_MAINWINDOW;
NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
```
`DWMWA_USE_IMMERSIVE_DARK_MODE` (attribute id `20`) — the specific DWM attribute that controls whether the **non-client title bar** is drawn dark — is declared as a constant (`NativeMethods.cs:133`) but is never passed to `DwmSetWindowAttribute` anywhere in the solution. The comment directly above the constant asserts responsibility lies elsewhere:
> "DWMWA_USE_IMMERSIVE_DARK_MODE is owned by Application.SetColorMode and is declared here for reference only -- never call it manually (Pitfall 1: double-set causes a title-bar color flash)."

This is an unverified assumption, and the rig-tester's finding #1 (title bar stays white/light in dark mode on both `MainForm` and `SettingsForm`) directly falsifies it: whatever `Application.SetColorMode(SystemColorMode.System)` does internally (`Program.cs:42`, `MainForm.cs:80`, `MonitorConfirmDialog.cs:68`, `SettingsForm.cs:130`), it is not producing a dark title bar on this build/runtime for any of the three forms — including `MainForm`, whose `SetColorMode` call in `Program.cs:42` runs before `ApplicationConfiguration.Initialize()` exactly per the documented guidance, and which still gets `ApplyDwmChrome()` (`MainForm.cs:99-109`) called with the correct Handle before first paint. Since `ApplyDwmChrome`/`ApplyRoundedCornersAndMica` is the one deterministic, always-called-with-a-real-Handle theming hook every form uses (constructor for the two dialogs, `InitializeTrayState()` for `MainForm`), and it never touches `DWMWA_USE_IMMERSIVE_DARK_MODE`, there is no code path in this codebase that can ever produce a dark title bar. This also means the codebase has zero test/verification coverage proving `SetColorMode` handles the non-client area — the one place that assumption could have been made correct (this P/Invoke call) explicitly opts out of it.

**Fix:** Explicitly set the attribute based on the live theme, sourced from `IThemeProvider.CurrentTheme` (which every caller of `ApplyRoundedCornersAndMica` already has in scope via `_themeProvider`):
```csharp
// DwmTitleBar.cs
public static void ApplyRoundedCornersAndMica(IntPtr handle, bool darkMode)
{
    int useDarkMode = darkMode ? 1 : 0;
    NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

    int corner = DWMWCP_ROUND;
    NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

    int backdrop = DWMSBT_MAINWINDOW;
    NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
}
```
and update every call site (`MainForm.cs:81,103`, `MonitorConfirmDialog.cs:51,69`, `SettingsForm.cs:83,131`) to pass `_themeProvider.CurrentTheme == AppTheme.Dark`. Promote `DWMWA_USE_IMMERSIVE_DARK_MODE` from `internal` visibility usage note to an actually-called constant.

---

### CR-02: `FlatStyle.System` buttons never receive dark-mode colors — explains rig finding #2

**File:** `src/RigToggle.App/MainForm.Designer.cs:81-85,94`, `src/RigToggle.App/MonitorConfirmDialog.Designer.cs:62-63,73`, `src/RigToggle.App/SettingsForm.Designer.cs:298-301,401,412`

**Issue:** Every themed button in the app (`btnToggle`, `btnSettings`, `btnContinue`, `btnCancel`, `btnBrowse`, `btnSaveSettings`, `btnDiscardChanges`) is set to `FlatStyle = FlatStyle.System`, with an explicit comment justifying the choice as a workaround for a different bug:
```csharp
// 12-02/THEME-05: FlatStyle.System (never .Flat, dotnet/winforms#13897) --
// renders a flat, theme-aware button using the OS's own visual-styles
// renderer instead of WinForms' 3D-bevel default.
this.btnToggle.FlatStyle = System.Windows.Forms.FlatStyle.System;
```
`FlatStyle.System` routes button painting through the native Windows visual-styles/theme renderer (`ButtonRenderer`/`VisualStyleRenderer` over `uxtheme.dll`), which is a *separate* rendering path from the WinForms-managed color pipeline that `Application.SetColorMode` drives for `FlatStyle.Standard`/`FlatStyle.Flat` buttons. `ThemeApplier.cs`'s own doc comment (lines 14-18) asserts "Button fill+text are already owned by SetColorMode" as the reason no button-theming method exists in `ThemeApplier` — this assumption is directly contradicted by the rig-tester's finding #2 (buttons stay white/light-themed in dark mode). Combined with CR-01 (the app never signals dark-mode preference to DWM/uxtheme via `DWMWA_USE_IMMERSIVE_DARK_MODE`, which is also what governs which visual-style theme variant `FlatStyle.System`'s native renderer picks up), these System-style buttons have no mechanism available to them that would ever turn them dark — `Application.SetColorMode` does not repaint them (wrong style for that pipeline) and nothing else does either.

**Fix:** Two options, either resolves the symptom:
1. Fix CR-01 first (the native dark-mode signal), which is a prerequisite for `FlatStyle.System`'s OS-visual-styles renderer to pick a dark variant at all, then re-verify on the rig — this may be sufficient since `FlatStyle.System` buttons legitimately follow OS dark theme once the *window* itself is dark-mode-flagged.
2. If (1) is insufficient (verify on rig), switch buttons to `FlatStyle.Flat` and add an explicit `ThemeApplier.ThemeButton(Button, bool dark)` helper (following the same pattern as `ApplyHotkeyIdleConfigured`) invoked from each form's `OnThemeChanged`/Load, working around dotnet/winforms#13897 by setting `FlatAppearance.BorderSize = 0` and explicit `BackColor`/`ForeColor` rather than relying on either automatic pipeline.

---

### CR-03: Audio-device ComboBoxes are never touched by any theming pass — explains rig finding #3

**File:** `src/RigToggle.App/SettingsForm.Designer.cs:212-217,237-242`, `src/RigToggle.App/SettingsForm.cs:120-153,501-559`, `src/RigToggle.App/ThemeApplier.cs` (entire file)

**Issue:** `cboAudioNormal`/`cboAudioRig` are declared as plain `ComboBoxStyle.DropDownList` combo boxes with no `FlatStyle`, no explicit color assignment, and no `ThemeApplier` hook anywhere:
```csharp
// SettingsForm.Designer.cs:214-217
this.cboAudioNormal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
this.cboAudioNormal.Location = new System.Drawing.Point(64, 22);
this.cboAudioNormal.Size = new System.Drawing.Size(320, 23);
this.cboAudioNormal.Name = "cboAudioNormal";
```
Compare this to `dgvMonitors`, which gets an explicit `ThemeApplier.ThemeMonitorGrid(dgvMonitors, IsDarkTheme)` call both at `SettingsForm_Load` (`SettingsForm.cs:160`) and inside `OnThemeChanged` (`SettingsForm.cs:132`), and `txtHotkey`, which gets three dedicated `ThemeApplier.ApplyHotkey*` calls. `ThemeApplier.cs` contains exactly four public methods — `ThemeMonitorGrid` and three `txtHotkey` state helpers — and no `ComboBox` method exists at all. `SettingsForm.OnThemeChanged` (`SettingsForm.cs:120-153`) re-themes the grid and the hotkey textbox on every live flip but never re-touches `cboAudioNormal`/`cboAudioRig`, and `PopulateAudioCombo` (`SettingsForm.cs:519-559`), the method that builds/repopulates these combos, sets `DataSource`/`DisplayMember`/`ValueMember`/`Enabled` but never a color. `ThemeApplier.cs`'s doc comment explicitly lists `ComboBox` as one of the "base controls ... already owned by SetColorMode" (lines 14-18) that deliberately were not given an override — this is the same incorrect assumption as CR-02, and directly explains the rig-tester's finding #3: nothing in this codebase ever attempts to theme these two controls.

**Fix:** Add a `ThemeApplier.ThemeComboBox(ComboBox, bool dark)` method (`DropDownList` style comboboxes are a documented WinForms gap even where `SetColorMode` is otherwise effective — the edit-portion/list-portion often need explicit `BackColor`/`ForeColor`/`FlatStyle` to follow dark mode), call it from both `PopulateAudioCombo` and `OnThemeChanged` for `cboAudioNormal`/`cboAudioRig`, mirroring the existing `dgvMonitors` pattern:
```csharp
public static void ThemeComboBox(ComboBox combo, bool dark)
{
    try
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = dark ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
        combo.ForeColor = dark ? Color.FromArgb(240, 240, 240) : SystemColors.WindowText;
    }
    catch
    {
        // Cosmetic-only — leave the control unchanged on failure.
    }
}
```

## Warnings

### WR-01: `ThemeChanged` handler unsubscription for `MonitorConfirmDialog`/`SettingsForm` relies solely on `FormClosed`, not `Dispose(bool)`

**File:** `src/RigToggle.App/MonitorConfirmDialog.cs:44-45`, `src/RigToggle.App/SettingsForm.cs:74-75`, `src/RigToggle.App/MonitorConfirmDialog.Designer.cs:14-21`, `src/RigToggle.App/SettingsForm.Designer.cs:14-21`

**Issue:** Both transient dialogs subscribe to the app-lifetime `WindowsThemeProvider.ThemeChanged` event in their constructors and unsubscribe via a `FormClosed` lambda:
```csharp
_themeProvider.ThemeChanged += OnThemeChanged;
this.FormClosed += (_, _) => _themeProvider.ThemeChanged -= OnThemeChanged;
```
This is correct for the normal `ShowDialog()`-then-close path, but unlike `MainForm` (whose `Dispose(bool)` override at `MainForm.Designer.cs:24-33` explicitly does `_themeProvider.ThemeChanged -= OnThemeChanged;` as a deterministic backstop), neither `MonitorConfirmDialog.Designer.cs` nor `SettingsForm.Designer.cs`'s `Dispose(bool)` override unsubscribes. If either dialog is disposed via its `using var ... = new ...(...)` wrapper (`MainForm.cs:322`, `Program.cs:128`'s factory) without `FormClosed` ever having fired — e.g. an exception thrown between construction and `ShowDialog()` returning, or a future caller pattern that constructs-and-discards without ever calling `ShowDialog` — the subscription leaks: the disposed form instance stays referenced forever by the singleton `WindowsThemeProvider.ThemeChanged` event, and the next real theme flip invokes `OnThemeChanged` on a disposed control (caught silently by its own try/catch, so no crash, but a permanent per-instance memory leak that accumulates across repeated Settings-open/Confirm-dialog cycles if this edge case is ever hit).

**Fix:** Add the same deterministic backstop `MainForm` already has to both dialogs' `Dispose(bool)` overrides:
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        _themeProvider.ThemeChanged -= OnThemeChanged;
        if (components != null) { components.Dispose(); }
    }
    base.Dispose(disposing);
}
```
(Note: `_themeProvider` and `OnThemeChanged` live in the non-Designer partial class file, so this requires either moving the override there or exposing an internal unsubscribe hook — same pattern already established for `MainForm`.)

### WR-02: `WindowsThemeProvider.CurrentTheme` is mutated without synchronization while documented as cross-thread-readable

**File:** `src/RigToggle.Windows/WindowsThemeProvider.cs:26,41-51`

**Issue:** The class's own doc comment (lines 16-19) states `ThemeChanged` "may fire off the UI thread" and callers must marshal. `CurrentTheme` is a plain auto-property (`{ get; private set; }`) written from `OnUserPreferenceChanged` (potentially a non-UI thread per that same comment) and read from arbitrary threads via the public getter (e.g., `SettingsForm.IsDarkTheme` on the UI thread, concurrently with a theme flip in flight). There is no `lock`/`volatile`/`Interlocked` around the read-compare-write in `OnUserPreferenceChanged` (`CurrentTheme = resolved;` at line 47) or around the public getter. In practice a torn read is not possible (the backing field is a 4-byte enum), but there is no memory-barrier guarantee that a UI-thread read immediately after `ThemeChanged` is marshaled back via `BeginInvoke` is guaranteed to observe the just-written value on all executing architectures without a barrier — and two rapid successive `UserPreferenceChanged` events (plausible, since the handler is intentionally unfiltered by category per the A1 comment) could race the diff-and-assign in `OnUserPreferenceChanged` itself, producing an extra/missed `ThemeChanged` invocation.

**Fix:** Not urgent given .NET's practical guarantees for aligned 32-bit field access, but for correctness under the class's own stated cross-thread contract, wrap the read-compare-write with a simple `lock`:
```csharp
private readonly object _lock = new();
private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
{
    var resolved = ReadThemeFromRegistry();
    lock (_lock)
    {
        if (resolved == CurrentTheme) return;
        CurrentTheme = resolved;
    }
    ThemeChanged?.Invoke(this, EventArgs.Empty);
}
```

## Info

### IN-01: Dark-mode accent color duplicated as a magic literal in `ThemeApplier`

**File:** `src/RigToggle.App/ThemeApplier.cs:37,94`

**Issue:** `Color.FromArgb(0, 90, 158)` (the dark-mode selection/accent blue) is hardcoded identically in both `ThemeMonitorGrid` (grid selection background) and `ApplyHotkeyRecording` (recording-state background), with no shared constant. If the accent color is ever revisited, it's easy to update one call site and miss the other.

**Fix:** Extract to a single `private const` (or `static readonly Color`) at the top of `ThemeApplier`, e.g. `private static readonly Color DarkAccent = Color.FromArgb(0, 90, 158);`, and reference it from both methods.

---

_Reviewed: 2026-08-03T07:16:06Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
