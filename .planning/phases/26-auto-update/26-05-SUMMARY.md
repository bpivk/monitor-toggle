---
phase: 26-auto-update
plan: 05
subsystem: ui
tags: [dotnet, winforms, tray-menu, settings-form, self-update]

requires:
  - phase: 26-auto-update
    plan: 04
    provides: "UpdatePromptChoice (UpdateNow/Later/Skip) threaded through UpdateOrchestrator.CheckOnLaunchAsync, AppSettings.SkippedUpdateVersion + honourSkippedVersion, and ReleaseNotesFormatter/ReleaseNotesRenderer this plan's manual check reuses unchanged via the shared confirm/onApplyStarting callbacks"
provides:
  - "UpdateOrchestrator.CheckOnDemandAsync + UpdateCheckResult record: a shared CheckAsync body parameterised by honourSkippedVersion/reportFailures so the on-demand path always overrides a prior skip and always surfaces CheckFailed (with reason) instead of collapsing into NotAvailable"
  - "UpdateCheckOutcome.CheckFailed (renamed from plan 26-04's unused Failed placeholder, which was declared in advance for exactly this purpose)"
  - "MainForm.PerformManualUpdateCheckAsync/PerformManualUpdateCheck: the shared manual-check body reached from trayCheckUpdatesMenuItem and SettingsForm's btnCheckForUpdates, reporting Info/Warning-icon toasts per D-06/D-07"
  - "ShowUpdatePromptDialog/ShowUpdatingBalloon: extracted helpers shared by both RunAutomaticUpdateCheckAsync and PerformManualUpdateCheckAsync so the dialog-construction and Updating... balloon cannot drift between the two paths"
  - "trayCheckUpdatesMenuItem in the locked tray order (toggle, settings, check-updates, separator, exit) and SettingsForm's bottom-left btnCheckForUpdates, both wired to the identical shared check"
affects: [26-05-operator-rig-verification]

actuals:
  tokens: 10200
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "UpdateOrchestrator.CheckAsync (private): the fetch/compare/confirm/apply sequencer factored out of CheckOnLaunchAsync so CheckOnDemandAsync shares it byte-for-byte except two booleans (honourSkippedVersion, reportFailures) -- the two public entry points can never drift on the download/apply segment's exception-propagation contract (D-08)"
    - "MainForm.ShowUpdatePromptDialog/ShowUpdatingBalloon extraction mirrors the existing PerformBackgroundToggle DRY precedent (tray/hotkey toggle triggers) -- cited directly in the new methods' doc comments"
    - "SettingsForm's bottom button row converted from a single flpButtons child of tlpRoot into a two-column tlpButtonRow (AutoSize left cell for btnCheckForUpdates, Percent-100 right cell hosting the unchanged flpButtons) -- tlpRoot's row count and Percent-100 row-0 sizing untouched, only the bottom row's contents changed"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/UpdateOrchestrator.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/Program.cs
    - src/RigToggle.Tests/UpdateOrchestratorTests.cs

key-decisions:
  - "UpdateCheckOutcome.Failed (an unused placeholder plan 26-04 declared in advance, doc-commented as 'reserved for a future/manual-check caller') was renamed to CheckFailed rather than adding a second, overlapping enum member -- confirmed via grep that no code referenced Failed anywhere before the rename"
  - "CheckOnLaunchAsync's public signature and return type (Task<UpdateCheckOutcome>) were left completely unchanged rather than widening it to return UpdateCheckResult for symmetry (the plan explicitly allowed either) -- this avoided touching all 10 pre-existing CheckOnLaunchAsync unit tests for zero behavioral gain, since CheckOnLaunchAsync now simply extracts .Outcome from the shared CheckAsync body's result"
  - "UpdateCheckResult.RunningVersionText is formatted as Major.Minor only ($\"{_runningVersion.Major}.{_runningVersion.Minor}\"), matching this project's own v{Major}.{Minor} tag scheme (UpdateVersionComparer's documented convention) rather than the full four-component System.Version.ToString(), which would have rendered the D-06 toast as 'v2.1.0.0' instead of 'v2.1'"

requirements-completed: [UPDATE-06]

coverage:
  - id: D1
    description: "A 'Check for Updates' item exists in the tray context menu, between Settings and the separator, and triggers the same check as the automatic on-launch path"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "grep -q trayCheckUpdatesMenuItem MainForm.Designer.cs; AddRange order confirmed via awk (traySettingsMenuItem, trayCheckUpdatesMenuItem, traySeparator); TrayCheckUpdatesMenuItem_Click dispatches to PerformManualUpdateCheck"
        status: pass
    human_judgment: true
    rationale: "Static wiring (menu order, event handler dispatch, string literals) is grep-verified, but the actual right-click tray menu rendering, item positioning, and click behavior can only be confirmed on a real WinForms message loop on Windows hardware -- deferred to Task 3's operator rig checkpoint (this build sandbox is Linux)."
  - id: D2
    description: "A 'Check for Updates' button exists in Settings and triggers the identical shared check -- one implementation, two entry points"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "grep -q btnCheckForUpdates/BtnCheckForUpdates_Click; flpButtons.Controls.Add count unchanged at 2; btnCheckForUpdates confirmed absent from flpButtons' own Controls.Add block; Program.cs threads mainForm.PerformManualUpdateCheck through SettingsFormFactory"
        status: pass
    human_judgment: true
    rationale: "Constructor wiring and control-tree placement are grep/static-verified, but the actual bottom-left layout, theming, and non-disturbance of the right-aligned Discard/Save pair require a live rendered Settings dialog on Windows hardware -- deferred to Task 3."
  - id: D3
    description: "A manual check that finds the app already current reports so with an Info-icon tray balloon naming the running version"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnDemandAsync_FeedReturnsOlderOrEqualTag_ReturnsNotAvailable_DistinctFromCheckFailed proves the orchestrator-side NotAvailable branch; grep confirms the verbatim \"You're already on the latest version\" string and ToolTipIcon.Info pairing in MainForm.cs"
        status: pass
    human_judgment: true
    rationale: "The orchestrator's outcome and the exact toast string/icon pairing are unit- and grep-verified, but the actual rendered NotifyIcon balloon tip requires a live Windows tray on real hardware -- deferred to Task 3."
  - id: D4
    description: "A manual check that itself fails shows a Warning-icon tray balloon naming the reason, visibly different from the already-up-to-date report"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAndCheckOnDemand_SameThrowingFeed_YieldDistinctOutcomes -- the paired D-07 distinctness guard: the same throwing feed yields NotAvailable on the launch path and CheckFailed (carrying the exception message) on the on-demand path"
        status: pass
    human_judgment: true
    rationale: "The distinct-outcome contract is unit-tested at the orchestrator boundary; the actual Warning-icon balloon rendering (visibly distinct from the Info-icon up-to-date toast) requires real Windows hardware -- deferred to Task 3."
  - id: D5
    description: "A manual check ignores a previously skipped version -- an explicit request overrides a prior skip"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnDemandAsync_ReleaseMatchesPersistedSkip_ConfirmIsInvokedAnyway -- proves confirm IS invoked even when the release's tag exactly matches AppSettings.SkippedUpdateVersion, because CheckOnDemandAsync always passes honourSkippedVersion: false"
        status: pass
    human_judgment: false
  - id: D6
    description: "The manual check is independent of the automatic on-launch check -- runnable at any time, any number of times, without a relaunch"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "PerformManualUpdateCheckAsync is a standalone public Task-returning method with no dependency on RunAutomaticUpdateCheckAsync's own state (releaseForFailureMessage); PerformManualUpdateCheck's fire-and-forget wrapper can be invoked repeatedly from either entry point"
        status: pass
    human_judgment: false
  - id: D7
    description: "Rig verification of the whole Phase 26 auto-update feature on real Windows hardware (Task 3)"
    verification: []
    human_judgment: true
    rationale: "Task 3 is a blocking-human checkpoint requiring a real installed exe on real Windows hardware -- a genuine reboot, a real GitHub release tag/workflow run, SmartScreen absence, and a deliberately-interrupted update. This build sandbox is Linux; none of the 15 numbered rig steps can be produced or fabricated here. NOT PERFORMED this session -- returned as a checkpoint per the plan's own gate="blocking" designation."

duration: ~25min (Tasks 1-2 only; Task 3 not started)
completed: 2026-08-22
status: halted
---

# Phase 26 Plan 05: On-Demand "Check for Updates" (Tray + Settings) Summary

**UpdateOrchestrator.CheckOnDemandAsync shares CheckOnLaunchAsync's fetch/compare/confirm/apply sequencer via a new private CheckAsync body, always overriding a prior skip and always surfacing a distinct CheckFailed outcome; MainForm.PerformManualUpdateCheck is the one shared body reached from a new tray menu item and a new Settings button, reporting honest Info/Warning-icon toasts.**

## Performance

- **Duration:** ~25 min (Tasks 1-2; Task 3 is a blocking-human rig checkpoint, not started)
- **Started:** 2026-08-22 (base commit `69ea391`)
- **Completed (Tasks 1-2):** 2026-08-22
- **Tasks:** 2 of 3 (Task 3 halted at checkpoint)
- **Files modified:** 7

## Accomplishments

- `UpdateOrchestrator.CheckOnDemandAsync` (Core, new) and the private `CheckAsync` body it shares with `CheckOnLaunchAsync`: identical fetch/compare/confirm/apply sequencing, differing only in `honourSkippedVersion` (on-demand always `false` — an explicit request overrides a prior skip) and `reportFailures` (on-demand `true` — a caught fetch/compare exception becomes the distinct `UpdateCheckOutcome.CheckFailed` carrying the exception message, instead of collapsing into `NotAvailable`).
- New `UpdateCheckResult` record (`Outcome`, `RunningVersionText`, `FailureReason`) returned by `CheckOnDemandAsync`; `UpdateCheckOutcome`'s pre-existing unused `Failed` placeholder (declared by plan 26-04 "for exactly this purpose") renamed to `CheckFailed` rather than adding an overlapping second member.
- `MainForm.PerformManualUpdateCheckAsync`/`PerformManualUpdateCheck`: the one shared manual-check body, reporting an Info-icon "You're already on the latest version (v{X.Y})" toast on `NotAvailable`, a Warning-icon "Couldn't check for updates — {reason}. Try again from the tray menu or Settings." toast on `CheckFailed` (or any escaping exception), exiting on `Applying`, and staying silent on `Declined`/`Skipped`.
- `ShowUpdatePromptDialog`/`ShowUpdatingBalloon` extracted from `RunAutomaticUpdateCheckAsync` so both the automatic and manual paths construct the dialog and the "Downloading and installing update…" balloon identically — the same DRY precedent `PerformBackgroundToggle` already established for the tray/hotkey toggle triggers.
- New `trayCheckUpdatesMenuItem` inserted into the locked tray order (toggle → settings → check-updates → separator → exit) and a new bottom-left `btnCheckForUpdates` on `SettingsForm` (via a new two-column `tlpButtonRow` replacing `flpButtons`' direct placement in `tlpRoot`'s bottom row, without touching `tlpRoot`'s row count or Percent-100 row-0 sizing) — both wired to the identical `PerformManualUpdateCheck` callback, threaded through `Program.cs`'s `SettingsFormFactory` the same way `TryRegisterConfiguredHotkey`/`ApplyTrayVisibility` already are.
- 3 new `UpdateOrchestratorTests`, including the paired launch-vs-on-demand distinctness test that is the automated guard for D-07 (same throwing feed yields `NotAvailable` on the launch path, `CheckFailed` on the on-demand path). 206/206 tests pass; solution and `RigToggle.Windows.Tests` both build with 0 errors/0 new warnings (the same 6 pre-existing, unrelated `xUnit1031` warnings documented in every prior plan's summary since Phase 25 remain, and only surface on a genuinely clean/forced rebuild — see `deferred-items.md`).

## Task Commits

1. **Task 1: On-demand check with honest reporting, reached from the tray menu** - `c86f50b` (feat)
2. **Task 2: The same check, reachable from Settings** - `2e55d01` (feat)

**Task 3 (checkpoint:human-verify, gate="blocking"): NOT performed.** This plan is marked `autonomous: false` specifically because Task 3 requires real Windows rig hardware (a published exe, a real GitHub release tag/workflow run, a real reboot, SmartScreen behavior, a deliberately-interrupted update) that cannot be produced or fabricated in this Linux build sandbox. See "CHECKPOINT REACHED" returned to the orchestrator for the full 15-step verification checklist awaiting the operator.

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP writes are owned by the wave orchestrator in worktree mode).

## Files Created/Modified

- `src/RigToggle.Core/UpdateOrchestrator.cs` - `CheckOnDemandAsync`, private `CheckAsync` shared body, `UpdateCheckResult` record, `UpdateCheckOutcome.CheckFailed` (renamed from `Failed`)
- `src/RigToggle.App/MainForm.cs` - `PerformManualUpdateCheckAsync`/`PerformManualUpdateCheck`, `TrayCheckUpdatesMenuItem_Click`, `ShowUpdatePromptDialog`/`ShowUpdatingBalloon` extraction, `RunAutomaticUpdateCheckAsync` refactored to use the extracted helpers
- `src/RigToggle.App/MainForm.Designer.cs` - `trayCheckUpdatesMenuItem` field/construction/wiring, new `AddRange` order
- `src/RigToggle.App/SettingsForm.cs` - `performManualUpdateCheck` constructor param + field + guard, `BtnCheckForUpdates_Click`, `ThemeButton(btnCheckForUpdates, ...)` at Load and live theme-change
- `src/RigToggle.App/SettingsForm.Designer.cs` - `btnCheckForUpdates` field/construction, new `tlpButtonRow` two-column container replacing `flpButtons`' direct `tlpRoot` placement
- `src/RigToggle.App/Program.cs` - `mainForm.PerformManualUpdateCheck` threaded through `SettingsFormFactory`
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` - 3 new tests: skip-override, NotAvailable-vs-CheckFailed distinctness, and the paired launch-vs-on-demand distinctness guard

## Decisions Made

See `key-decisions` in the frontmatter above for the full list, most notably:
- `UpdateCheckOutcome.Failed` (an unused placeholder plan 26-04 declared in advance) was renamed to `CheckFailed` in place, rather than adding a second overlapping enum member — grep-confirmed no code referenced `Failed` anywhere before the rename.
- `CheckOnLaunchAsync`'s public signature/return type were left completely unchanged (it now just extracts `.Outcome` from the shared `CheckAsync` result) rather than widening it to `UpdateCheckResult` for symmetry — the plan explicitly allowed either, and this avoided churning all 10 pre-existing `CheckOnLaunchAsync` tests for no behavioral gain.
- `UpdateCheckResult.RunningVersionText` is Major.Minor only (`"2.1"`, not `"2.1.0.0"`), matching this project's own tag-comparison convention (`UpdateVersionComparer`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug/dead-code] Renamed the unused `UpdateCheckOutcome.Failed` placeholder to `CheckFailed` instead of adding a second member**
- **Found during:** Task 1
- **Issue:** Plan 26-04 had already declared an enum member named `Failed`, doc-commented as "reserved for a future/manual-check caller that wants to represent 'the check itself failed'" — i.e., precisely the member this plan's `<action>` asked to add under the name `CheckFailed`. Adding a second, differently-named member with the identical purpose would have left one of the two permanently orphaned/dead.
- **Fix:** Renamed the existing `Failed` member to `CheckFailed` in place (verified via grep that no code anywhere referenced `Failed` before the rename) and updated its doc comment to describe the now-real usage (`CheckOnDemandAsync`'s fetch/compare segment).
- **Files modified:** `src/RigToggle.Core/UpdateOrchestrator.cs`
- **Verification:** `grep -q 'CheckFailed' src/RigToggle.Core/UpdateOrchestrator.cs` passes (plan's own acceptance criterion); all 206 tests pass.
- **Committed in:** `c86f50b` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 bug/dead-code avoidance)
**Impact on plan:** No scope creep — the fix produces exactly the `CheckFailed`-named member the plan's acceptance criteria grep for, without leaving a duplicate, unused enum member behind.

## Issues Encountered

- **This build sandbox is Linux, not Windows** (same constraint every prior plan in this phase has documented). `dotnet build RigToggle.sln -c Release` and `dotnet test src/RigToggle.Tests` both ran and passed directly in this session (206/206 tests green, including all 3 new tests from this plan's Task 1). `dotnet build src/RigToggle.Windows.Tests` also succeeded (0 errors, 0 warnings). What could **not** be exercised: any part of Task 3's rig checklist — the real tray context menu render/click, the Settings dialog's real bottom-left button layout/theming, and every one of the 15 numbered rig-verification steps covering the entire Phase 26 feature (swap, autostart survival, auto-rollback, interrupted-update recovery, manual check on real hardware, skip override, release-notes overflow).
- **A full clean rebuild of `RigToggle.Tests` re-surfaces 6 pre-existing `xUnit1031` warnings** (documented since Phase 25/26-04, in `SingleInstanceGuardTests.cs` and `ToggleOrchestratorTests.cs`, both files this plan never touches). An incremental rebuild after this plan's own changes reports `0 Warning(s)` because dotnet's up-to-date check skips re-running `CoreCompile` on files that didn't change; a forced `--no-incremental`-equivalent rebuild reports the same 6 warnings every prior plan in this phase already flagged as pre-existing and out of this plan's scope. This plan's own new/modified files introduce 0 new warnings either way.

## User Setup Required

None - no external service configuration required for Tasks 1-2. Task 3 requires the operator to publish a real build, tag/push a real GitHub release, and run the 15-step checklist on real Windows hardware — see the checkpoint returned to the orchestrator.

## Next Phase Readiness

Phase 26's code is now feature-complete: version stamping, on-launch check, themed three-choice prompt with formatted release notes, checksum-verified download, rename-in-place self-replacement with relaunch, retained-backup auto-rollback, and now a manual "Check for Updates" from both the tray menu and Settings — all unit-tested at the Core boundary and building clean. Nothing further can be built without the rig; the only remaining work in this phase is Task 3's 15-step operator verification, which is this plan's (and the whole phase's) last blocking checkpoint. No code-level blockers.

**Blocker/concern carried forward:** Task 3 (rig verification of the ENTIRE Phase 26 feature, not just this plan's own two tasks) is unperformed — genuinely blocked on real Windows hardware, not deferred by choice. This is a `gate="blocking"` checkpoint per the plan itself; it must not be auto-approved or waived by any automated process. Returned to the orchestrator as a checkpoint.

---
*Phase: 26-auto-update*
*Completed (Tasks 1-2 only): 2026-08-22*
