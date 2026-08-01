---
phase: 11-configurable-tray-close-minimize-behavior-user-selectable-op
reviewed: 2026-08-01T00:00:00Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - src/RigToggle.Core/Models/AppSettings.cs
  - src/RigToggle.Tests/JsonStoreTests.cs
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/Program.cs
findings:
  critical: 1
  warning: 2
  info: 2
  total: 5
status: issues_found
resolution: "CR-01, WR-01, and IN-01 fixed manually in commit e317323 (see below). WR-02 and IN-02 left as-is per reviewer's own recommendation (not required / confirmed correct as implemented)."
---

**Post-review resolution (2026-08-01):** CR-01 (tray-icon lockout), WR-01 (unguarded settings.json reads in FormClosing/Resize), and IN-01 (doc-comment restore-order mismatch) were fixed in commit `e317323`. WR-02 (redundant disk reads on Resize) and IN-02 (no error path on new checkboxes) were left as-is — the reviewer itself marked WR-02 "not required for this phase" and IN-02 "confirmed correct as implemented."

# Phase 11: Code Review Report

**Reviewed:** 2026-08-01
**Depth:** standard (static source read — no `dotnet` SDK available in this environment; all findings derived from direct reading of the seven changed files plus `JsonSettingsStore.cs` for cross-reference)
**Files Reviewed:** 7
**Status:** issues_found

## Summary

The Phase 11 implementation (`AppSettings.CloseMinimizesToTray`/`MinimizeToTray`, the settings-driven `MainForm_FormClosing`/`MainForm_Resize` handlers, the derived `ApplyTrayVisibility()` rule, and the two new `SettingsForm` checkboxes with live-apply wiring) is clean, well-commented, and faithfully implements the locked decisions D-01 through D-11. The shared `SendToTray()` helper correctly prevents the Close and Minimize paths from duplicating `Hide()`, the persistence layer round-trips both new fields correctly (verified against `JsonSettingsStore.cs` and the new `JsonStoreTests` cases), and the Designer layout changes are internally consistent (no control overlap, correct 64px shift cascade).

However, tracing the interaction between D-08's derived tray-icon-visibility rule and D-09's "tray icon reachable from the tray context menu itself" produces a genuine lockout scenario the human-verify checkpoint (11-04) did not test: a user can turn off both tray-hiding preferences from within Settings *while the window is currently hidden to tray*, which removes the tray icon without ever showing the window again — leaving the app with no tray icon and no taskbar entry, recoverable only via Task Manager. This is the headline finding below. Two further Warnings cover a new unguarded-exception surface introduced by adding `_settingsStore.Load()` calls to `FormClosing`/`Resize`, plus a stale-registration robustness point in `SettingsForm`'s constructor wiring is not an issue — no such gap exists there. Two Info items are minor documentation/robustness notes.

## Critical Issues

### CR-01: Turning off both tray preferences while the window is hidden makes the app unreachable via any UI surface

**File:** `src/RigToggle.App/MainForm.cs:111-115` (`ApplyTrayVisibility`), reached via `src/RigToggle.App/SettingsForm.cs:745` (`_applyTrayVisibility()` call in `BtnSaveSettings_Click`), reachable from `src/RigToggle.App/MainForm.cs:312-319` (`OpenSettingsDialog`, invoked by `TraySettingsMenuItem_Click`)

**Issue:** D-09 explicitly keeps the Settings dialog reachable via the tray context menu's own "Settings" item even while the main window is hidden — that's the whole point of `TraySettingsMenuItem_Click` → `OpenSettingsDialog()`, which calls `_settingsFormFactory()` and `ShowDialog(this)` without ever calling `Show()`/`WindowState = Normal` on the (possibly-hidden) `MainForm`.

Walk the sequence:
1. User has `MinimizeToTray` (or `CloseMinimizesToTray`) enabled — tray icon visible, main window currently `Hide()`'d (via either `MainForm_FormClosing` or `MainForm_Resize`, both routing through `SendToTray()`).
2. User right-clicks the tray icon → Settings (`TraySettingsMenuItem_Click`, reachable precisely because the icon is visible while the window stays hidden — this is D-09's intended UX).
3. In the dialog, the user unchecks **both** `chkCloseMinimizesToTray` and `chkMinimizeToTray` and clicks Save.
4. `BtnSaveSettings_Click` persists the settings, then calls `_applyTrayVisibility()` (`SettingsForm.cs:745`), which invokes `MainForm.ApplyTrayVisibility()` (`MainForm.cs:111-115`):
   ```csharp
   public void ApplyTrayVisibility()
   {
       var settings = _settingsStore.Load();
       notifyIcon.Visible = settings.CloseMinimizesToTray || settings.MinimizeToTray;
   }
   ```
   Both flags are now `false`, so `notifyIcon.Visible` is set to `false` **immediately**, while `MainForm` is still `Hide()`'d (`Visible == false`, no taskbar entry since `Hide()` removes it).
5. The Settings dialog closes (`DialogResult.OK`). `OpenSettingsDialog()` (`MainForm.cs:312-319`) resumes after `ShowDialog` returns and only calls `TryRegisterConfiguredHotkey()` + `RefreshUi()` — neither of which shows the window.

Result: no tray icon (gone), no taskbar entry (window still `Hidden`), no visible UI surface of any kind. The only two theoretical recovery paths — the global hotkey (`HandleHotkeyToggle`, only performs a rig/normal toggle, never restores window visibility) and the CLI trigger (Phase 10, also toggle-only per phase boundary) — do not restore the window either. The only recovery is killing the process via Task Manager and relaunching. For a single-user personal utility whose entire interaction model is "click things," this is a severe regression: the app can render itself completely inaccessible through completely ordinary use of the very feature this phase ships. The 11-04 human-verify checklist (fresh-upgrade default, close-to-tray, minimize-to-tray, live icon appear/disappear, tray-menu regression) never exercises "open Settings from the tray while hidden, then disable both prefs," so this was not caught.

**Fix:** Make `ApplyTrayVisibility()` itself lockout-safe — if the derived visibility is about to go to `false` while the window is currently not visible, force the window back into view first:

```csharp
public void ApplyTrayVisibility()
{
    var settings = _settingsStore.Load();
    bool shouldBeVisible = settings.CloseMinimizesToTray || settings.MinimizeToTray;

    // D-09 keeps Settings reachable from the tray while the window is hidden. If
    // both prefs are being turned off right now, losing the tray icon while the
    // window is still Hidden would leave zero UI surface reachable (no taskbar
    // entry either) — force the window back into view so the user is never
    // locked out of their own app.
    if (!shouldBeVisible && !Visible)
    {
        Show();
        WindowState = FormWindowState.Normal;
    }

    notifyIcon.Visible = shouldBeVisible;
}
```

This keeps the fix centralized in the one method both the startup path (`InitializeTrayState`) and the Settings-Save path already call, so no other call site needs to change.

## Warnings

### WR-01: New unguarded `_settingsStore.Load()` calls in `FormClosing`/`Resize` are a new unhandled-exception surface on Close/Minimize

**File:** `src/RigToggle.App/MainForm.cs:350` (`MainForm_FormClosing`), `src/RigToggle.App/MainForm.cs:381` (`MainForm_Resize`)

**Issue:** Before Phase 11, `MainForm_FormClosing` never touched the settings store (Close-to-tray was unconditional) and there was no `Resize` handler at all. Phase 11 adds `_settingsStore.Load()` calls to both, with no `try`/`catch` around either call. `JsonSettingsStore.Load()` (`src/RigToggle.Core/Persistence/JsonSettingsStore.cs:34-73`) only catches `JsonException` and `IOException` — it does **not** catch `UnauthorizedAccessException` (permission-denied on `%LocalAppData%\RigToggle\settings.json`, e.g. a locked-down AV product or a corrupted ACL), which is a `SystemException`, not an `IOException`. If that throws while the user is simply trying to close or minimize the window, the exception propagates out of a WinForms event handler for the two most routine actions in the app — at best surfacing an unhandled-exception dialog, at worst preventing the close/minimize from completing at all. Other new-in-recent-phases callers of `_settingsStore.Load()` (e.g. `TryRegisterConfiguredHotkey`) share this same unguarded pattern, but those are lower-frequency, best-effort paths — Close and Minimize are core, everyday interactions and deserve the same defensive posture `Program.cs:49-57` already uses for its own startup `Load()`.

**Fix:** Wrap both calls and degrade to a safe default (treat as "never configured" — X exits, minimize does standard OS behavior — exactly what happens on first run today):

```csharp
private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
{
    AppSettings settings;
    try
    {
        settings = _settingsStore.Load();
    }
    catch
    {
        settings = new AppSettings(); // degrade to "never configured" defaults rather than block Close
    }
    // ... unchanged from here
}
```
Apply the same pattern in `MainForm_Resize`.

### WR-02: `ApplyTrayVisibility()`/`MainForm_Resize` re-read settings.json from disk on every event instead of sharing one load

**File:** `src/RigToggle.App/MainForm.cs:381` (`MainForm_Resize`), `src/RigToggle.App/MainForm.cs:113` (`ApplyTrayVisibility`)

**Issue:** `MainForm_Resize` fires for every `SizeChanged`, not just the transition into `Minimized` — WinForms can raise multiple `Resize` events during a single minimize animation/OS transition — and each invocation performs a fresh synchronous `File.ReadAllText` via `_settingsStore.Load()` on the UI thread. This is not flagged as a performance defect (out of v1 scope per review policy) but is worth noting as a robustness smell: it multiplies the exposure window for WR-01's unguarded-exception risk, and ties routine window-resize handling to disk I/O for no benefit (the two booleans rarely change and are already re-read at Settings-Save time via `_applyTrayVisibility()`).

**Fix:** Not required for this phase, but consider caching the two flags on `MainForm` (refreshed only in `ApplyTrayVisibility()`) rather than loading from disk inside `MainForm_Resize` itself, e.g.:
```csharp
private bool _minimizeToTray;
// set in ApplyTrayVisibility(): _minimizeToTray = settings.MinimizeToTray;
// MainForm_Resize reads the cached field instead of calling _settingsStore.Load() itself
```

## Info

### IN-01: `MainForm_Resize` doc comment states a restore order that doesn't match `NotifyIcon_MouseClick`'s actual code

**File:** `src/RigToggle.App/MainForm.cs:368-378` (doc comment above `MainForm_Resize`), vs. `src/RigToggle.App/MainForm.cs:395-403` (`NotifyIcon_MouseClick`)

**Issue:** The XML doc above `MainForm_Resize` reads: *"Restore happens through the existing NotifyIcon_MouseClick left-click path (WindowState = Normal; Show(); Activate())"* — but the actual code in `NotifyIcon_MouseClick` is:
```csharp
Show();
WindowState = FormWindowState.Normal;
Activate();
```
i.e. `Show()` runs before `WindowState = Normal`, the reverse of what the comment describes. Harmless in practice (this method predates Phase 11 and wasn't modified by it), but since Phase 11 is the first time a `Hide()` can occur while `WindowState == Minimized` (previously `Hide()` only ever ran while `WindowState == Normal`, from the Close path), the exact restore order is now more load-bearing than before and worth getting the comment right for the next reader.

**Fix:** Either correct the comment to match the code (`Show(); WindowState = Normal; Activate();`) or reorder the code to match the comment — either is fine, just make them consistent.

### IN-02: New checkboxes have no dedicated `errCloseMinimizesToTray`/`errMinimizeToTray` warning path — confirmed intentional, documented here for traceability

**File:** `src/RigToggle.App/SettingsForm.cs:91-92` (Load), `src/RigToggle.App/SettingsForm.cs:726-727` (Save)

**Issue:** Not a defect — flagged only for completeness since every other checkbox/field in `SettingsForm` has some inline-warning affordance (`errAutostart`/`lblAutostartWarning`, `errHotkey`/`lblHotkeyWarning`, etc.) and a reviewer scanning for "missing error handling" would otherwise flag this. Per `11-03-SUMMARY.md`'s own stated decision, these two fields have no runtime failure path (plain in-memory booleans, no registry/COM/file call), so `chkEnableDebugLogging`'s no-error-path pattern is the correct template to follow, not `chkStartWithWindows`'s. Confirmed correct as implemented.

---

_Reviewed: 2026-08-01_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
