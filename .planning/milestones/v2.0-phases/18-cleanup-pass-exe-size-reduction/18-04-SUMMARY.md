---
phase: 18-cleanup-pass-exe-size-reduction
plan: 04
subsystem: core-app-cleanup
tags: [cleanup, code-review-followup, dead-code, diagnostics]
dependency-graph:
  requires: []
  provides:
    - "PopulateAudioCombo single-body implementation"
    - "sentinel-aware audio device name persistence"
    - "consistent FormatChecklist Skipped/NotAttempted wording"
    - "ReconcileModeAfterMonitorFailure per-case diagnostics"
  affects:
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.Core/ToggleResultFormatter.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/ToggleResultFormatterTests.cs
tech-stack:
  added: []
  patterns:
    - "fully-qualified System.Diagnostics.Trace.WriteLine diagnostic idiom, reused rather than reinvented"
key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.Core/ToggleResultFormatter.cs
    - src/RigToggle.Core/ToggleService.cs
    - src/RigToggle.Tests/ToggleResultFormatterTests.cs
decisions: []
metrics:
  duration_minutes: 25
  completed: 2026-08-09
---

# Phase 18 Plan 04: Cleanup Pass — Deferred Review Findings Summary

Closed four previously-deferred code-review findings (15-REVIEW IN-02/IN-03/IN-04, 16-REVIEW WR-03) with the reviewer's own proposed fixes and zero user-facing behavior change, adding one net-new regression test for previously-uncovered checklist wording.

## What Was Built

**Task 1 — `SettingsForm.cs` (IN-04, IN-03):**
- Collapsed `PopulateAudioCombo`'s unreachable `if (items.Count == 0) { ... }` branch (and its `"No audio devices detected."` string) into a single unconditional method body. The branch was provably dead: `PopulateAudioPickers` always prepends the `(None…)` sentinel before calling this method, so `items` is never empty. All live behavior (theming, stale-device warning, `CurrencyManager` guard, sentinel auto-selection) was preserved verbatim, just un-nested.
- `BtnSaveSettings_Click` now persists `null` for `NormalAudioDeviceName`/`RigAudioDeviceName` when the `(None…)` sentinel is selected, instead of the sentinel's `DisplayLabel` UI sentence (`"(None — don't switch audio)"`). Both fields are write-only in production (grep-confirmed zero production reads), so nothing rendered to the user changes — only what a hand-inspection of `settings.json` would show.

**Task 2 — `ToggleResultFormatter.cs` / `ToggleResultFormatterTests.cs` / `ToggleService.cs` (IN-02, WR-03):**
- `FormatChecklist`'s `Skipped` arm now renders `skipped (not configured)` (lowercase leading letter) instead of `Skipped (not configured)`, matching the sibling `NotAttempted` arm's lowercase `not attempted` — both strings appear in the same MessageBox/toast checklist.
- Added `FormatChecklist_SkippedStep_RendersLowercaseSkipped`, asserting the exact string `Audio: skipped (not configured)`. This arm had zero prior coverage.
- `ReconcileModeAfterMonitorFailure`'s three previously-indistinguishable no-op branches (unchanged, partial-mutation, recapture-failure) each now emit a distinct `System.Diagnostics.Trace.WriteLine` diagnostic, reusing the file's existing fully-qualified idiom (matching the pre-existing call sites). Behavior is unchanged: the method still never calls `_modeStore.Save(...)`, still swallows the recapture exception, and still returns normally on every path.

## Findings Resolved

| Finding | Source | Resolution |
|---------|--------|------------|
| IN-04 | `15-REVIEW.md` | Unreachable `items.Count == 0` branch removed from `PopulateAudioCombo`; single unconditional body remains |
| IN-03 | `15-REVIEW.md` | Sentinel selection now persists `null` for `*AudioDeviceName`, not the sentinel's `DisplayLabel` |
| IN-02 | `15-REVIEW.md` | `FormatChecklist`'s Skipped arm lowercased to match `NotAttempted`'s convention; new regression test added |
| WR-03 | `16-REVIEW.md` | `ReconcileModeAfterMonitorFailure`'s three branches now each trace a distinct diagnostic message; mode-flag behavior unchanged |

**Deliberately not touched (confirmed):**
- `17-REVIEW.md` WR-02 — locked Phase 17 planning decision, out of this plan's scope, no edits made to any file it references.
- `16-REVIEW.md` WR-04 (unguarded `_modeStore.Save`) — already fixed in Phase 16 (`TrySaveMode` helper, `ToggleService.cs`); not re-opened.
- `src/RigToggle.Tests/ToggleServiceTests.cs`, `src/RigToggle.Tests/Doubles/**` — owned by plan 18-05, no edits made.
- No `.csproj`/`.pubxml` files touched (plan 18-03's scope).
- No snapshot/`Restore` code touched (plans 18-01/18-02's scope).

## Test Totals

- Before this plan (measured baseline in PLAN.md): `Total: 85`
- After Task 2's new test: `Total: 86` (85 + 1, exactly as required)
- `Failed: 0` in both the full suite and the filtered `FormatChecklist_SkippedStep_RendersLowercaseSkipped` run

## Verification

- `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` → 0 Error(s), 0 Warning(s) (final state; the pre-existing 4 `xUnit1031` warnings in `ToggleOrchestratorTests.cs` were only transiently visible mid-edit and are outside this plan's `files_modified` scope — confirmed absent from the final build)
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` → `Passed: 86, Failed: 0, Skipped: 0`
- `grep -c "No audio devices detected\|items.Count == 0" src/RigToggle.App/SettingsForm.cs` → `0`
- `grep -c "Skipped (not configured)" src/RigToggle.Core/ToggleResultFormatter.cs` → `0`
- `sed -n '/private void ReconcileModeAfterMonitorFailure/,/^    }/p' src/RigToggle.Core/ToggleService.cs | grep -c "System.Diagnostics.Trace.WriteLine"` → `3`
- `git diff --stat` against the plan's base commit touches exactly the four files listed in `files_modified` — no accidental scope creep into `ToggleServiceTests.cs`, `Doubles/**`, or any `.csproj`/`.pubxml`

## Deviations from Plan

None — plan executed exactly as written. One minor self-correction during Task 2: an initial doc-comment wording accidentally contained the literal string `System.Diagnostics.Trace.WriteLine`, which inflated the acceptance-criteria grep count from 8 to 9; reworded the comment (no code change) before committing so the count matched exactly.

## Threat Flags

None — this plan's threat model (T-18-04-01 through T-18-04-04, T-18-04-SC) was already assessed in the plan itself; no new network endpoints, auth paths, file access patterns, or schema changes were introduced beyond what the plan's own threat register covers.

## Self-Check: PASSED

- FOUND: src/RigToggle.App/SettingsForm.cs
- FOUND: src/RigToggle.Core/ToggleResultFormatter.cs
- FOUND: src/RigToggle.Core/ToggleService.cs
- FOUND: src/RigToggle.Tests/ToggleResultFormatterTests.cs
- FOUND commit: 64b1c17
- FOUND commit: 2ee7c4e
