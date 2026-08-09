---
phase: 15-optional-app-audio-targets
reviewed: 2026-08-04T00:00:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - src/RigToggle.Core/Models/ToggleStepOutcome.cs
  - src/RigToggle.Core/Models/ToggleResult.cs
  - src/RigToggle.Core/ToggleResultFormatter.cs
  - src/RigToggle.Core/Abstractions/IAudioController.cs
  - src/RigToggle.Tests/Doubles/FakeControllers.cs
  - src/RigToggle.Core/ToggleService.cs
  - src/RigToggle.Tests/ToggleServiceTests.cs
  - src/RigToggle.App/SettingsForm.Designer.cs
  - src/RigToggle.App/SettingsForm.cs
  - src/RigToggle.App/MainForm.cs
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
status: issues_found
---

# Phase 15: Code Review Report

**Reviewed:** 2026-08-04
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

I traced `ToggleService.ToggleToRigMode`/`ToggleToNormalMode` step-by-step against the D-03/D-04/D-05 invariants the phase's own SUMMARYs claim to satisfy (Skipped vs. NotAttempted never conflated, result always has exactly 3 steps, stop-on-first-failure preserved for Rig mode, isolate-and-continue preserved for Normal mode, `IsFullyConfigured` relaxed to monitor-only), cross-checked every `TryExecuteOptionalStep`/`TryExecuteStep` branch against `ToggleServiceTests.cs`'s assertions, and walked the `SettingsForm.cs` Clear-button/sentinel/validation wiring end to end (Load → Clear/Browse/DragDrop → Validate → Save). I did not find a genuine correctness bug or security issue in the phase's core behavior change — the Skipped/Failed bifurcation, the 3-steps-always invariant, and the Normal-mode `SetDefault`-instead-of-`Restore` rewrite all hold up under trace, including the less obvious paths (Monitor-fails-before-any-mutation snapshot-clear, Monitor-fails-after-mutation snapshot-keep, Audio-fails-blocks-App-but-Skipped-doesn't).

What this review did find is real: this phase's own Normal-mode rewrite left `IAudioController.Restore` — and the audio-restore-throws test scaffolding — as dead code that nothing in production calls anymore, and it introduced a new null-vs-empty-string semantic split between the UI layer (treats a hypothetical `""` as "configured but broken," blocking Save) and the Core layer (treats `""` identically to `null`, i.e. Skipped/non-blocking). Neither is reachable through normal UI interaction today, but both are latent maintenance traps a future edit could easily wake up. The rest are minor copy/dead-code nits.

## Warnings

### WR-01: `IAudioController.Restore` is now dead code in the production path, but still live/dangerous surface area

**File:** `src/RigToggle.Core/Abstractions/IAudioController.cs:16`, `src/RigToggle.Core/ToggleService.cs` (no call site remains), `src/RigToggle.Tests/Doubles/FakeControllers.cs:120-131`

**Issue:** Plan 02 rewired `ToggleToNormalMode`'s Audio step from `_audioController.Restore(snapshot.Audio)` to `_audioController.SetDefault(settings.NormalAudioDeviceId)`. I confirmed via `grep -rn "\.Restore(" src/RigToggle.App src/RigToggle.Windows src/RigToggle.Core` that `IAudioController.Restore` now has **zero production call sites** anywhere in the solution — it is only referenced by `FakeAudioController.Restore` and by two now-dead assertions (`ToggleServiceTests.cs:137,483`) that merely check it is *not* called. `snapshot.Audio` (the `AudioState` captured at rig-toggle time) is likewise captured and persisted but never read again. The SUMMARY for 15-02 explicitly acknowledges this as "Phase 18 cleanup scope, left untouched here," so it's a known deferral, not an oversight — but flagging it here because: (1) the interface still forces every current and future `IAudioController` implementer to carry a method with real audio-switching side effects that is unreachable from the app's actual toggle flow, and (2) `WindowsAudioController.Restore` (not reviewed in this phase, since that file was untouched) could silently rot — a bug introduced there today would not be caught by any exercised code path, since the only remaining caller is a fake in the test suite that no test drives to `true` anymore (see IN-01 below).

**Fix:** Track this explicitly for the Phase 18 cleanup pass referenced in the SUMMARY — remove `Restore`/`AudioState` capture-for-audio entirely (or, if `WindowsAudioController.Restore` needs to survive as a lower-level primitive `SetDefault` can call into, stop exposing it on `IAudioController` so the orchestration layer can't accidentally depend on it again). Until then, consider a code comment on the interface member itself (not just in `ToggleService.cs`'s remarks) noting it is unreachable from `ToggleService`, so a future reader doesn't assume it's part of the live restore path.

### WR-02: Null vs. empty-string "unset" semantics diverge between `SettingsForm` and `ToggleService`

**File:** `src/RigToggle.App/SettingsForm.cs:690` (`appPathOk = _pendingAppPath is null || IsValidLaunchTarget(_pendingAppPath)`), `src/RigToggle.App/SettingsForm.cs:582-593` (`savedId is not null` audio match), vs. `src/RigToggle.Core/ToggleService.cs:219` (`string.IsNullOrEmpty(configuredValue)`)

**Issue:** `ToggleService.TryExecuteOptionalStep` (and the inline Normal-mode Audio check) treats an empty string `""` identically to `null` — both produce a `Skipped` step, per `string.IsNullOrEmpty`. `SettingsForm`'s equivalent gates use a strict `is null`/`is not null` check instead: if `AppSettings.CompanionAppPath` (or `*AudioDeviceId`) were ever persisted as `""` rather than `null` — e.g. via a future settings-migration bug, a hand-edited `settings.json`, or a different serializer default — `RenderAppPathDisplay`/`PopulateAudioCombo` would treat it as "configured but broken" (shows a "please reselect" stale warning and **blocks Save**), while `ToggleService` would silently treat the exact same value as "deliberately unset" and skip the step without blocking the toggle. Today this is unreachable through normal UI flow (Clear/sentinel-selection always write `null`, never `""`, and `AppSettings`'s properties default to `null`), so it's not user-facing yet, but it is a genuine cross-layer contract mismatch introduced by this phase's optionality relaxation — before Phase 15, both layers required non-empty values uniformly, so this divergence didn't exist.

**Fix:** Normalize on one convention. Either have `SettingsForm` treat `string.IsNullOrEmpty(_pendingAppPath)`/`string.IsNullOrEmpty(savedId)` as "unset" everywhere it currently checks `is null`/`is not null` (matching `ToggleService`'s contract), or have `ToggleService.TryExecuteOptionalStep` switch to a strict `is null` check and have `ISettingsStore`/`AppSettings` guarantee empty strings are normalized to `null` on load/save. Cheapest fix: swap `SettingsForm`'s three `is null`/`is not null` checks to `string.IsNullOrEmpty`/`!string.IsNullOrEmpty` so both layers agree.

## Info

### IN-01: `audioThrowsOnRestore` test knob is now dead code

**File:** `src/RigToggle.Tests/ToggleServiceTests.cs:49,62`

**Issue:** `CreateService`'s `audioThrowsOnRestore` parameter is wired straight through to `FakeAudioController(..., throwOnRestore: audioThrowsOnRestore, ...)`, but no test in the file passes `audioThrowsOnRestore: true` anymore (`grep -n "audioThrowsOnRestore" ToggleServiceTests.cs` only shows the declaration and the pass-through). This is leftover plumbing from before Plan 02 deleted the two "audio restore throws" tests and re-homed their coverage onto the `audioDeviceMissing`-driven test — the parameter itself should have been deleted in the same pass.

**Fix:** Remove the `audioThrowsOnRestore` parameter from `CreateService` and the corresponding `throwOnRestore`/`_throwOnRestore` plumbing from `FakeAudioController` (or fold it into the WR-01 cleanup, since `Restore` itself is dead in production).

### IN-02: Inconsistent capitalization in `FormatChecklist`'s Skipped line

**File:** `src/RigToggle.Core/ToggleResultFormatter.cs:33`

**Issue:** The new `Skipped` arm renders `"{name}: Skipped (not configured)"` (capital "Skipped"), while the sibling `NotAttempted` arm renders `"{name}: not attempted"` (lowercase). Both are user-facing strings shown in the same MessageBox/toast checklist, so the capitalization mismatch is visible in the same UI surface side by side.

**Fix:** Lowercase to `"{name}: skipped (not configured)"` for visual consistency with `"not attempted"`, or capitalize both — either is fine, just make them match.

### IN-03: Selecting the "(None…)" sentinel persists its display label as the device *name*, not a null/empty name

**File:** `src/RigToggle.App/SettingsForm.cs:888,890`

**Issue:** `BtnSaveSettings_Click` unconditionally sets `NormalAudioDeviceName = audioNormalItem.DisplayLabel` / `RigAudioDeviceName = audioRigItem.DisplayLabel`. When the sentinel is selected, `DisplayLabel` is the literal string `"(None — don't switch audio)"`, so that sentence gets persisted verbatim into `AppSettings.NormalAudioDeviceName`/`RigAudioDeviceName` even though the corresponding `*AudioDeviceId` is `null`. The 15-03 SUMMARY already flags this as a known, accepted cosmetic limitation (the Name fields are display-only, never read by `ToggleService`'s resolution logic) — surfacing it here for completeness since a future consumer of `settings.json` (a diagnostics dump, a support script, a future migration) reading `RigAudioDeviceName` would see a UI sentence instead of a device name or `null`/empty, which is misleading outside the context of this specific dialog.

**Fix:** When `audioNormalItem.Id is null` (sentinel selected), persist `NormalAudioDeviceName = null` (or `string.Empty`) instead of the sentinel's display text; same for the Rig pair.

### IN-04: Defensive `items.Count == 0` branch in `PopulateAudioCombo` is unreachable dead code

**File:** `src/RigToggle.App/SettingsForm.cs:564-571`

**Issue:** Since `PopulateAudioPickers` now unconditionally prepends the `"(None…)"` sentinel before calling `PopulateAudioCombo`, the `items` list passed in can never be empty, so the `if (items.Count == 0) { ... "No audio devices detected." ... }` branch can never execute from the codebase's only call site. The code's own comment already acknowledges this ("no longer reachable in practice — left in place defensively"), so this is a self-flagged dead branch rather than a hidden one.

**Fix:** No action required if the defensive branch is intentionally kept for future callers; otherwise remove it and the now-unused `combo.Items.Add("No audio devices detected.")` string during the same Phase 18 cleanup pass.

---

_Reviewed: 2026-08-04_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
