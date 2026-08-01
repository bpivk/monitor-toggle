---
phase: 08-tray-residency-autostart-toast-notification
reviewed: 2026-07-31T00:00:00Z
depth: standard
files_reviewed: 12
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.App/RigToggle.App.csproj
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.Core/Abstractions/IAutostartConfigurator.cs
  - src/RigToggle.Core/StartupArgs.cs
  - src/RigToggle.Core/ToggleResultFormatter.cs
  - src/RigToggle.Tests/StartupArgsTests.cs
  - src/RigToggle.Tests/ToggleResultFormatterTests.cs
  - src/RigToggle.Windows/WindowsAutostartConfigurator.cs
findings:
  critical: 1
  warning: 5
  info: 2
  total: 8
status: issues_found
---

# Phase 8: Code Review Report

**Reviewed:** 2026-07-31T00:00:00Z
**Depth:** standard
**Files Reviewed:** 12
**Status:** issues_found

## Summary

Reviewed the tray-residency / autostart / toast-notification phase, with particular
attention to `Program.cs`'s `--tray` startup branch and `MainForm`'s tray lifecycle
(icon loading, `FormClosing` gating, `NotifyIcon` disposal) given the rig-validated
`ApplicationContext` fix (commit `91c11df`).

**On the specific fix under scrutiny:** `Application.Run(new ApplicationContext())`
with no `MainForm` reference is correct for this runtime — `NotifyIcon` owns its own
hidden native window and is driven by the thread's message pump regardless of whether
an `ApplicationContext` has a `MainForm` set, and `Application.Exit()` terminates every
message loop on the calling thread unconditionally, independent of
`ApplicationContext.MainForm`. `InitializeTrayState()` is called unconditionally before
either `Application.Run` branch, so `RefreshUi()`'s `_normalIcon`/`_rigIcon` null-guard
never actually engages under the current call order, and `NotifyIcon.Visible = true`
being set before `Icon` is assigned in the Designer is safe (`Icon`'s setter re-invokes
`UpdateIcon(visible)`, so the icon appears in the tray only once one of the two glyphs
is assigned). No regression was found in this specific mechanism.

Beyond that mechanism, this review found one crash-risk defect (an unhandled exception
inside error-recovery code meant to prevent exactly that), plus several warnings around
resource disposal, a documented-but-unfulfilled null-safety contract, and an
inconsistent null-check pattern in the new registry adapter.

## Critical Issues

### CR-01: Autostart save-failure recovery path can itself throw an unhandled exception

**File:** `src/RigToggle.App/SettingsForm.cs:579-586`
**Issue:** `BtnSaveSettings_Click`'s catch block exists specifically to degrade
gracefully when `_autostartConfigurator.Enable()`/`Disable()` fails (per the T-08-LIE
comment: "must never claim a success that did not happen"). But the recovery itself
calls `_autostartConfigurator.IsEnabled()` unguarded:

```csharp
catch (Exception ex)
{
    string message = $"Could not enable Start with Windows: {ex.Message}";
    lblAutostartWarning.Text = message;
    lblAutostartWarning.Visible = true;
    errAutostart.SetError(chkStartWithWindows, message);
    chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled(); // <-- unguarded
}
```

`WindowsAutostartConfigurator.IsEnabled()` performs its own registry read
(`Registry.CurrentUser.OpenSubKey`). If the same underlying condition that caused
`Enable()`/`Disable()` to fail (permissions, corrupted hive, GPO lockdown, etc.) also
affects reads — or if `IsEnabled()` fails for an unrelated reason — this second call
throws from inside the catch block with nothing above it in the call stack to catch it
(the default WinForms/`ApplicationConfiguration.Initialize()` template registers no
`Application.ThreadException` handler). The result is an unhandled exception that
crashes the whole app on Save, which is a strictly worse outcome than the "revert
checkbox + inline warning" the code is trying to guarantee.

**Fix:** Wrap the recovery read in its own try/catch (or reuse a
`TryIsEnabled` helper) so a second registry failure degrades the checkbox state instead
of crashing:
```csharp
catch (Exception ex)
{
    string message = $"Could not enable Start with Windows: {ex.Message}";
    lblAutostartWarning.Text = message;
    lblAutostartWarning.Visible = true;
    errAutostart.SetError(chkStartWithWindows, message);
    try
    {
        chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();
    }
    catch
    {
        // Best-effort UI sync only — leave the checkbox as the user left it
        // rather than crash on a second registry failure.
    }
}
```

## Warnings

### WR-01: `SettingsForm_Load` calls `IsEnabled()` with no error handling

**File:** `src/RigToggle.App/SettingsForm.cs:70`
**Issue:** `chkStartWithWindows.Checked = _autostartConfigurator.IsEnabled();` runs
unguarded in `SettingsForm_Load`, unlike the `Enable()`/`Disable()` calls in
`BtnSaveSettings_Click`, which are wrapped in try/catch. Any exception from this
registry read (permissions, hive corruption) will propagate out of the `Load` event and
crash the Settings dialog on open, rather than degrading (e.g. defaulting the checkbox
to unchecked with a warning).
**Fix:** Wrap the call in try/catch, defaulting to `false` and surfacing
`lblAutostartWarning` on failure, matching the degrade-gracefully pattern already used
for `PopulateMonitorGrid`/`PopulateAudioPickers` in the same method.

### WR-02: Tray-menu toggle can disable a monitor with zero user confirmation, ever

**File:** `src/RigToggle.App/MainForm.cs:332-368` (contrast with `BtnToggle_Click`,
lines 111-241, specifically the DISPLAY-07 confirm-dialog block at lines 144-184)
**Issue:** `TrayToggleMenuItem_Click` intentionally skips both the
`IsSettingsConfigured()` guard and the DISPLAY-07 "which monitors will be
disabled/enabled" confirmation dialog (documented in the method's own XML comment as a
deliberate design choice for a background trigger). That is defensible when the user
has already confirmed via the GUI button at least once — but nothing enforces that
ordering. A user can configure Settings entirely through the tray's own "Settings" menu
item and then click "Switch to Rig Mode" from the tray for the very first toggle ever,
in which case the monitor(s) named in `MonitorsToDisable`/`MonitorsToEnable` are
disabled/enabled with no confirmation shown at any point — the informed-consent safety
gate that exists specifically to prevent disabling the wrong monitor is fully bypassed
for this (plausible) tray-only usage pattern.
**Fix:** Either (a) require at least one GUI-confirmed toggle before enabling the tray
menu's toggle item, or (b) show a one-time confirmation via the tray's own balloon/mini
dialog the first time a tray-triggered toggle would mutate an unconfirmed monitor set,
or (c) explicitly document this as an accepted risk in product-facing docs (not just
code comments) since it affects the core "genuinely disable the right monitor" value
proposition.

### WR-03: `_normalIcon`/`_rigIcon` are never disposed

**File:** `src/RigToggle.App/MainForm.cs:29-30, 75-87`; `src/RigToggle.App/MainForm.Designer.cs:14-21`
**Issue:** `LoadTrayIconsIfNeeded()` loads two `System.Drawing.Icon` instances (each
wrapping a native GDI icon handle) and stores them in plain fields, not in `components`.
`MainForm.Designer.cs`'s `Dispose(bool disposing)` only disposes `components` — the two
`Icon` fields are never explicitly disposed anywhere in the class. They will eventually
be freed via `Icon`'s finalizer, but that defeats the deterministic-disposal contract
`IDisposable` exists for, and the code's own comment ("keeps the resulting Icon
instances for the lifetime of the form") implies ownership without following through
on cleanup.
**Fix:** Override `Dispose(bool disposing)` in `MainForm.cs` (not just rely on the
Designer partial) to dispose `_normalIcon`/`_rigIcon` when `disposing` is true:
```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        _normalIcon?.Dispose();
        _rigIcon?.Dispose();
    }
    base.Dispose(disposing);
}
```

### WR-04: `StartupArgs.ShouldStartHidden` does not fulfill its documented null-safety contract

**File:** `src/RigToggle.Core/StartupArgs.cs:21-22`
**Issue:** The type's XML doc states: "`ShouldStartHidden` must never throw on
null/empty/garbage args (Security Domain V5)". The implementation is:
```csharp
public static bool ShouldStartHidden(string[] args) =>
    args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);
```
`Enumerable.Contains<T>(IEnumerable<T>, T, IEqualityComparer<T>)` throws
`ArgumentNullException` when its `source` argument is null. If `args` is ever null
(defensive callers, reflection-based invocation, or a future refactor of `Main` that
forwards a nullable array), this throws — directly contradicting the documented
contract for a method whose whole purpose is to run safely "before any UI exists" on
every autostart-launched process. `StartupArgsTests.cs` also has no test case for a
null array, so this gap is untested as well as unguarded.
**Fix:**
```csharp
public static bool ShouldStartHidden(string[]? args) =>
    args is not null && args.Contains(TrayFlag, StringComparer.OrdinalIgnoreCase);
```
and add a `[InlineData(null, false)]` case to `StartupArgsTests`.

### WR-05: `WindowsAutostartConfigurator.Enable()` dereferences a nullable registry key without a null check

**File:** `src/RigToggle.Windows/WindowsAutostartConfigurator.cs:42-44` (contrast with
`Disable()`, lines 49-50)
**Issue:**
```csharp
using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
key.SetValue(ValueName, $"\"{exePath}\" --tray");
```
`RegistryKey.CreateSubKey(string, bool)` is documented/annotated as returning `null` if
the operation fails without throwing. `key.SetValue(...)` dereferences it unconditionally.
Two lines away, `Disable()` correctly guards the analogous case with `key?.DeleteValue(...)`.
While a `CreateSubKey(writable: true)` call against the always-writable HKCU hive is
unlikely to return null in practice, the inconsistency with the adjacent method (which
handles exactly this case) is a real defensive-coding gap, and under the project's
`<Nullable>enable</Nullable>` setting this is a genuine nullable-dereference warning
site.
**Fix:**
```csharp
using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
    ?? throw new InvalidOperationException("Could not create or open the Run registry key.");
key.SetValue(ValueName, $"\"{exePath}\" --tray");
```

## Info

### IN-01: Embedded icon resources loaded via null-forgiving operator with no diagnostic on failure

**File:** `src/RigToggle.App/MainForm.cs:82-86`
**Issue:**
```csharp
using var normalStream = assembly.GetManifestResourceStream("normal.ico");
using var rigStream = assembly.GetManifestResourceStream("rig.ico");
_normalIcon = new System.Drawing.Icon(normalStream!);
_rigIcon = new System.Drawing.Icon(rigStream!);
```
`GetManifestResourceStream` returns `null` if the logical resource name isn't found
(e.g. a future csproj edit renames/removes a `LogicalName`). The `!` suppresses the
nullable warning but does not prevent the resulting `ArgumentException`/`NullReferenceException`
from the `Icon` constructor, and this runs at startup, before any window exists, with no
`Application.ThreadException` handler configured — the app will terminate with an
unhelpful low-level exception rather than a clear "embedded tray icon resource missing"
diagnostic.
**Fix:** Null-check explicitly and throw a descriptive exception:
```csharp
_normalIcon = new System.Drawing.Icon(normalStream ?? throw new InvalidOperationException("Embedded resource 'normal.ico' not found."));
```

### IN-02: Duplicated Settings-launch logic between GUI button and tray menu

**File:** `src/RigToggle.App/MainForm.cs:243-251` (`BtnSettings_Click`) and
`src/RigToggle.App/MainForm.cs:297-302` (`TraySettingsMenuItem_Click`)
**Issue:** Both handlers have identical bodies (construct via `_settingsFormFactory()`,
`ShowDialog(this)`, `RefreshUi()`). Minor duplication; a future change to one path (e.g.
adding a busy-guard) is easy to forget to mirror in the other.
**Fix:** Extract a shared private method, e.g. `private void OpenSettingsDialog() { using var f = _settingsFormFactory(); f.ShowDialog(this); RefreshUi(); }`, called from both event handlers.

---

_Reviewed: 2026-07-31T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
