---
phase: 02-foundations-gui-shell
fixed_at: 2026-07-24T00:00:00Z
review_path: .planning/phases/02-foundations-gui-shell/02-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 02: Code Review Fix Report

**Fixed at:** 2026-07-24
**Source review:** .planning/phases/02-foundations-gui-shell/02-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (fix_scope: critical_warning — CR-01, WR-01, WR-02, WR-03; IN-01 excluded per scope)
- Fixed: 4
- Skipped: 0

**Verification note:** No .NET SDK is available in this sandbox, so Tier 2 (compiler-based) verification could not be run for any fix. All fixes below were verified with Tier 1 (re-read modified file section, confirmed fix text present and surrounding code intact) and accepted per the Tier 3 fallback.

## Fixed Issues

### CR-01: Malformed settings.json crashes the app with no recovery path

**Files modified:** `src/RigToggle.Core/Persistence/JsonSettingsStore.cs`
**Commit:** d40e77e
**Applied fix:** Wrapped the `File.ReadAllText` + `JsonSerializer.Deserialize` call in `JsonSettingsStore.Load()` in a try/catch. `JsonException` (malformed/truncated JSON) and `IOException` (interrupted read, e.g. antivirus lock or 0-byte file mid-write) both degrade to a fresh `AppSettings()`, mirroring the existing "file missing" branch, rather than propagating the exception to `MainForm.OnLoad`/`BtnSettings_Click` where it would crash/terminate the app. Matches the review's suggested fix, extended to also catch `IOException` as the review's "ideally IOException" note recommended.

### WR-01: No settings-completeness guard before ToggleToRigMode

**Files modified:** `src/RigToggle.Core/ToggleService.cs`, `src/RigToggle.App/MainForm.cs`
**Commit:** 0903f03
**Applied fix:** Added a private `IsFullyConfigured(AppSettings)` check in `ToggleService` (mirrors the same four fields `SettingsForm.ValidateSettingsForm` already requires: `MonitorDevicePath`, `NormalAudioDeviceId`, `RigAudioDeviceId`, `CompanionAppPath`) and a public `IsSettingsConfigured()` wrapper. `ToggleToRigMode()` now throws `InvalidOperationException` *before* capturing state or calling `_snapshotStore.Save(...)` if settings are incomplete — this is the core-layer fix that actually prevents the garbage snapshot / false "Mode: Rig" bug described in the finding, and it holds even if a future caller bypasses the UI guard. `MainForm.BtnToggle_Click` additionally calls `_toggleService.IsSettingsConfigured()` before attempting the rig-mode path and shows a friendly "finish configuring Settings" message instead of proceeding, rather than relying solely on the generic catch-all error dialog.
**Verification status: fixed — requires human verification.** This finding required adding new control flow (a guard clause + a new public method + a new UI branch), not a mechanical patch. The existing `ToggleServiceTests.ConfiguredSettings` fixture has all four fields populated, so the added guard does not change behavior for the already-tested fully-configured path — confirmed by reading `src/RigToggle.Tests/ToggleServiceTests.cs`. No test exists yet for the incomplete-settings path (the review noted this gap); recommend the developer add one and manually confirm the `MessageBox` UX on Windows, since this sandbox cannot compile or run the WinForms app.

### WR-02: `Process` objects from `Process.GetProcessesByName` are never disposed

**Files modified:** `src/RigToggle.Windows/WindowsAppController.cs`
**Commit:** 0dac374
**Applied fix:** `IsRunning()` now captures the `Process[]` into a local, checks `.Length` inside a try block, and disposes every element in a `finally` block — applied exactly per the review's suggested fix.

### WR-03: `MMDevice` COM wrappers are never disposed in `WindowsAudioController`

**Files modified:** `src/RigToggle.Windows/WindowsAudioController.cs`
**Commit:** 29b4b9b
**Applied fix:** All three call sites now dispose their `MMDevice` instance: `GetPlaybackDevices()` wraps each loop iteration's `device` in a `using (device) { ... }` block; `CaptureState()` and `TryResolveDevice()` both declare their `MMDevice`/`MMDevice?` with a `using` declaration (`using MMDevice defaultDevice = ...` / `using MMDevice? device = ...`) so it is disposed when the method returns, including on the early-return-null path in `TryResolveDevice`. Applied per the review's suggested fix pattern, extended to the other two methods it named.

## Skipped Issues

None — all four in-scope findings (CR-01, WR-01, WR-02, WR-03) were fixed. IN-01 (`JsonSnapshotStore.Load()` unguarded deserialize) was excluded by `fix_scope: critical_warning` and left for a future `--all` pass or manual fix.

---

_Fixed: 2026-07-24_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
