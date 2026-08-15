---
phase: 22-settingsform-layout-pass
plan: 04
subsystem: ui
tags: [winforms, tablelayoutpanel, settingsform, designer, dotnet10, gap-closure]

# Dependency graph
requires:
  - phase: 22-settingsform-layout-pass (plan 01)
    provides: tlpRoot/tlpModeColumns mode-column scaffold
  - phase: 22-settingsform-layout-pass (plan 02)
    provides: pnlSharedSection/flpShared, flpButtons, Form.AutoSize/Sizable
  - phase: 22-settingsform-layout-pass (plan 03)
    provides: rig-hardware verification result — FAIL on Check 1 (grid/audio picker
      missing) and Check 3 (manual resize broken), plus two unconfirmed root-cause
      hypotheses this plan re-evaluates and repairs
provides:
  - "pnlMonitor/pnlMonitorNormal AutoSize=true (the suspected root cause of the
    Check 1 collapse — both were the only container Panels in the file that never
    measured their Dock=Fill child)"
  - "tlpModeColumns.MinimumSize(0, 280) — a documented, DPI-safe, mechanism-
    independent height floor that makes the Check 1 collapse structurally
    impossible regardless of which layout node actually caused it"
  - "Form.AutoSize=false plus a new SettingsForm.OnLoad override that computes the
    window's content-driven initial ClientSize from tlpRoot.PreferredSize (clamped
    to the screen's working area) and floors MinimumSize at that size — removes the
    mechanism the rig showed fighting a user's edge-drag (Check 3)"
affects: [22-05 (fresh rig-hardware verification pass — the only thing that can
  confirm either bug is actually fixed and close out SETTINGS-01/SETTINGS-02)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "MinimumSize floor as a mechanism-independent layout backstop: Control.GetPreferredSize
      clamps on the way up, Control.SetBoundsCore clamps on the way down, so a documented
      floor value holds regardless of which container in a nested AutoSize/Percent chain
      is the actual point of failure"
    - "Form.AutoSize replaced by an explicit one-shot OnLoad measurement
      (tlpRoot.PreferredSize -> Screen.WorkingArea clamp -> ClientSize -> MinimumSize =
      Size) when a Form needs both a content-driven initial size AND real Sizable
      resize behavior — AutoSize alone cannot deliver both simultaneously"

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs

key-decisions:
  - "Repaired the suspected root-cause node (pnlMonitor/pnlMonitorNormal AutoSize)
    AND added an independent MinimumSize floor on tlpModeColumns in the same task,
    rather than picking one — the floor holds even if the AutoSize repair turns out
    not to be the actual mechanism, per the plan's own root-cause-analysis hedge"
  - "Form.AutoSize disabled at the Form level only; tlpRoot and every container
    below it keep AutoSize/AutoSizeMode unchanged — the migration's container-driven
    sizing chain (D-03) is not touched, only the outermost node that was rewriting
    the window's bounds on every layout pass (including a user's edge-drag)"
  - "Hard constraint 3 deliberately exercised: SettingsForm.cs is no longer
    byte-identical to the phase base commit 0c1234f. Three prior plans (01, 02, 03)
    held this invariant; this plan breaks it because turning Form.AutoSize off (the
    only reliable fix for Bug A) removes the mechanism that produced D-05's
    content-driven size, and no Designer-only replacement exists that does not
    hardcode a dimension, which D-05 forbids. The added code is exactly one method
    (OnLoad) with no other change to any validation rule, save/load logic, theming
    call, or string in the file"
  - "requirements-completed is deliberately []. Neither SETTINGS-01 nor SETTINGS-02
    is verified complete by this plan — only Plan 05's rig checkpoint can establish
    that. This is the third time this instruction has been given explicitly after
    22-01-SUMMARY.md and 22-02-SUMMARY.md both wrote the requirement IDs prematurely
    (before rig verification, which then FAILED both)"

patterns-established: []

requirements-completed: []

# Metrics
duration: ~15min
completed: 2026-08-15
---

# Phase 22 Plan 04: Gap-Closure Fix for Rig-Hardware Check 1 (Missing Controls) and Check 3 (Broken Resize) Summary

**Source-level fix for both defects the Phase 22 rig-hardware checkpoint found: added AutoSize to the two mode-wrapper Panels plus a documented 280px MinimumSize floor on tlpModeColumns (Bug B — grid/audio picker not rendering), and replaced Form.AutoSize with an explicit content-driven OnLoad override (Bug A — manual resize broken). Neither bug is confirmed fixed by this plan; that is Plan 05's rig checkpoint to establish.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-08-15 (session start, after worktree base fast-forward to `de2faeb`)
- **Completed:** 2026-08-15T20:26:36Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- **Task 1 (Bug B):** Added `AutoSize = true` / `AutoSizeMode = GrowAndShrink` to both `pnlMonitor` and `pnlMonitorNormal` — the only two container `Panel`s in the file that never measured their `Dock=Fill` child, which the plan's root-cause analysis identified as the best-fitting explanation for the Check 1 collapse (caption/explain visible, everything from the grid row down clipped). Added a documented, DPI-safe `MinimumSize(0, 280)` floor on `tlpModeColumns` as a mechanism-independent backstop, since `MinimumSize` clamps `GetPreferredSize` on the way up and `SetBoundsCore` on the way down regardless of which container actually caused the collapse.
- **Task 2 (Bug A):** Changed `this.AutoSize = true;` to `this.AutoSize = false;` on `SettingsForm` and removed the Form-level `AutoSizeMode` line — the mechanism the rig's Check 3 showed rewriting the window's bounds on every layout pass, including a user's edge-drag (`WM_SIZING`/`WM_SIZE` triggers a layout pass; `AutoSize` on immediately overwrites the dragged bounds with the computed preferred size). Added a new `SettingsForm.OnLoad` override that calls `base.OnLoad(e)` first (so grids/pickers are populated and font scaling is applied), reflows `tlpRoot`, reads `tlpRoot.PreferredSize`, clamps it to `Screen.FromControl(this).WorkingArea`, applies it to `ClientSize`, then floors `MinimumSize` at the resulting `Size` — preserving D-05's content-driven initial size with no hardcoded dimension anywhere, while D-06 (`FormBorderStyle.Sizable`, `MaximizeBox=false`, `MinimizeBox=false`) stays fully intact.
- All 11 `SizeType.Percent, 100F` rows, both `Percent, 50F` columns, and all 18 `AutoSize` row styles preserved exactly — rig Check 3(d) (extra height lands on the grids, not a dead band) is not traded away.
- `Location`, `ClientSize`, and `SizeType.Absolute` counts all remain 0 in the Designer file; blast radius is exactly the two files named in `files_modified`.
- Build and the full 82-test suite held at their measured baselines throughout (`0 Error(s)`, `4 Warning(s)` — the four pre-existing `xUnit1031` warnings, unrelated to this plan; `82/82` tests passing).

## Task Commits

Each task was committed atomically:

1. **Task 1: Repair the mode-column sizing chain and floor it so it cannot collapse again (Bug B)** — `ba7b39c` (fix)
2. **Task 2: Turn off Form.AutoSize and replace it with an explicit content-driven size (Bug A)** — `b38d4ed` (fix)

## Files Created/Modified

- `src/RigToggle.App/SettingsForm.Designer.cs` — Added `AutoSize`/`AutoSizeMode` to `pnlMonitor`/`pnlMonitorNormal`; added a documented `tlpModeColumns.MinimumSize(0, 280)` floor; changed `SettingsForm.AutoSize` to `false` and removed the Form-level `AutoSizeMode` line; replaced the now-falsified comment above it with one recording the rig's Check 3 result and the remedy applied
- `src/RigToggle.App/SettingsForm.cs` — Added a single `protected override void OnLoad(System.EventArgs e)` method immediately after the constructor, computing and applying the window's content-driven initial size and resize floor now that `Form.AutoSize` no longer does so; nothing else in the file changed

## Verify Command Output (both tasks)

**Task 1 count invariants (verified live in this session, after Task 1's edit):**

| Check | Expected | Actual |
|---|---|---|
| `this.pnlMonitor.AutoSize = true;` | 1 | 1 |
| `this.pnlMonitor.AutoSizeMode = ...GrowAndShrink;` | 1 | 1 |
| `this.pnlMonitorNormal.AutoSize = true;` | 1 | 1 |
| `this.pnlMonitorNormal.AutoSizeMode = ...GrowAndShrink;` | 1 | 1 |
| `this.tlpModeColumns.MinimumSize = new System.Drawing.Size(0, 280);` | 1 | 1 |
| `SizeType.Percent, 100F` (non-comment) | 11 | 11 |
| `SizeType.Percent, 50F` (non-comment) | 2 | 2 |
| `RowStyle(...SizeType.AutoSize)` (non-comment) | 18 | 18 |
| `.Location = new System.Drawing.Point(` | 0 | 0 |
| `ClientSize` | 0 | 0 |
| `SizeType.Absolute` | 0 | 0 |
| `MinimumSize` (non-comment) | 16 | 16 |

`git diff --stat src/` after Task 1: exactly one file, `src/RigToggle.App/SettingsForm.Designer.cs`.

**Task 2 count invariants (verified live in this session, after Task 2's edit):**

| Check | Expected | Actual |
|---|---|---|
| `this.AutoSize = false;` | 1 | 1 |
| Form-level `this.AutoSizeMode = ...GrowAndShrink;` (non-comment) | 0 | 0 |
| `this.tlpRoot.AutoSize = true;` | 1 | 1 |
| `this.tlpRoot.AutoSizeMode = ...GrowAndShrink;` | 1 | 1 |
| `FormBorderStyle.Sizable` | 1 | 1 |
| `this.MaximizeBox = false;` | 1 | 1 |
| `this.MinimizeBox = false;` | 1 | 1 |
| `ClientSize` (Designer file) | 0 | 0 |
| `protected override void OnLoad` (SettingsForm.cs) | 1 | 1 |
| `base.OnLoad(e);` | 1 | 1 |
| `tlpRoot.PerformLayout();` | 1 | 1 |
| `tlpRoot.PreferredSize` | 2 | 2 |
| `WorkingArea` | 1 | 1 |
| `this.ClientSize = ` | 1 | 1 |
| `this.MinimumSize = this.Size;` | 1 | 1 |
| `git diff --name-only src/` (Tasks 1+2 combined) | 2 files | 2 files |
| `git diff --name-only -- '*.csproj' '*.sln' ThemeApplier.cs` | empty | empty |
| `grep -c 'protected override' SettingsForm.cs` | 1 | 1 |

**Build** (`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true --no-incremental`, final state after both tasks):
```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```
All 4 warnings are the pre-existing `xUnit1031` warnings in `ToggleOrchestratorTests.cs` — matches the phase baseline exactly.

**Test** (`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`, final state after both tasks):
```
Passed!  - Failed: 0, Passed: 82, Skipped: 0, Total: 82, Duration: 88 ms
```

**Blast radius** (`git diff --name-only de2faeb -- src/`, base commit at plan start): exactly `src/RigToggle.App/SettingsForm.Designer.cs` and `src/RigToggle.App/SettingsForm.cs`.

## Invariant Counts Before and After

| Invariant | Before this plan | After this plan |
|---|---|---|
| `SizeType.Percent, 100F` | 11 | 11 (unchanged) |
| `SizeType.Percent, 50F` | 2 | 2 (unchanged) |
| `RowStyle(...SizeType.AutoSize)` | 18 | 18 (unchanged) |
| `.Location = new System.Drawing.Point(` | 0 | 0 (unchanged) |
| `ClientSize` | 0 | 0 (unchanged) |
| `SizeType.Absolute` | 0 | 0 (unchanged) |
| `MinimumSize` (non-comment) | 15 | 16 (+1: `tlpModeColumns`) |
| Form-level `AutoSize` value | `true` | `false` |
| Form-level `AutoSizeMode` line | present | removed |
| `protected override` in `SettingsForm.cs` | 0 | 1 (new `OnLoad`) |

## Hard Constraint 3 — Exercised, With Reason

`SettingsForm.cs` is no longer byte-identical to the phase base commit `0c1234f`. Plans 01, 02, and 03 each held this invariant and stated it explicitly in their own Summaries (`_SettingsForm.cs and ThemeApplier.cs remain byte-identical to the phase baseline commit_`). This plan deliberately breaks it: turning `Form.AutoSize` off is the only reliable fix for Bug A (the rig's Check 3 — resize preview flickers, window never actually resizes), and disabling `AutoSize` removes the mechanism that was producing D-05's content-driven initial window size. No Designer-file-only replacement exists that doesn't hardcode a `ClientSize`, which D-05 explicitly forbids and which the 22-01/22-02 `TableLayoutPanel` migration deliberately deleted. The single new `OnLoad` override in `SettingsForm.cs` is the only way to preserve D-05 without that hardcoded dimension. `git diff src/RigToggle.App/SettingsForm.cs` confirms the change is exactly one new method — no edit inside `SettingsForm_Load`, no changed string, no changed validation or theming call.

## Re-Evaluation of 22-03-SUMMARY.md's Hypothesis 2 Against Official Documentation

`22-03-SUMMARY.md`'s Hypothesis 2 proposed that a `SizeType.Percent, 100F` row nested inside an `AutoSize=true` `TableLayoutPanel` collapses to zero height, because a `Percent` row needs a known total container height to distribute against, and that dependency is circular when the container's own height is itself derived from its children.

This plan's `<root_cause_analysis>` re-evaluated that hypothesis against Microsoft's own `autosize-behavior-in-the-tablelayoutpanel-control` documentation (already cited in `22-RESEARCH.md`'s sources) and found it **contradicted**: the documentation states that when a `TableLayoutPanel` itself is `AutoSize`, `Percent` rows "acquire an automatic sizing aspect," and the control "expands the column or row to create adequate free space, so that no column or row with `SizeType.Percent` styling clips its contents." A collapse to zero height is precisely what that sentence rules out. Separately, `22-VERIFICATION.md` had already flagged the hypothesis as insufficient on its own for a second reason: `cboAudioNormal`/`cboAudioRig` sit in an `AutoSize` row (row 4), not the `Percent` row (row 2) — so `Percent`-row collapse alone cannot explain the audio picker's absence either.

The better-fitting explanation this plan applied instead (see the plan's `<root_cause_analysis>` for the full reasoning): `pnlMonitor`/`pnlMonitorNormal` were the only two container `Panel`s in the file that never set `AutoSize`, and their sole child being `Dock=Fill` contributes nothing to a `DefaultLayout` container's own measured extent — so without `AutoSize` each wrapper reported the default `Panel` size (200×100) up to its `tlpModeColumns` cell instead of its measured content. That matches the reported symptom precisely (caption + explain visible, roughly the first ~76px of content surviving, everything below clipped) and explains the audio-picker absence too, since it sits well below that clipping point regardless of its own row's `SizeType`.

Because this explanation is well-supported but not provable without a Windows layout engine, Task 1 applied it as the primary repair **and** added the `MinimumSize(0, 280)` floor as an independent backstop that holds even if the wrapper-`Panel` explanation turns out to be wrong.

## Recorded Fallback (Not Implemented Here)

If Plan 05's rig pass shows the grids still absent *with* the floor in place, the plan's `<root_cause_analysis>` records the next fallback for the following planner: eliminate the intermediate wrapper `Panel`s (`pnlMonitor`/`pnlMonitorNormal`) entirely, hosting `tlpNormalColumn`/`tlpRigColumn` directly in the `tlpModeColumns` cells with `BorderStyle.FixedSingle` and `Padding=12` carried onto the tables themselves (`TableLayoutPanel` is itself a `Panel` subclass and would render an identical THEME-05 flat border). This was deliberately **not** implemented in this plan.

## Decisions Made

See `key-decisions` in the frontmatter above for the full list. Summarized: (1) applied both the suspected-root-cause repair and an independent floor for Bug B rather than choosing one; (2) disabled `Form.AutoSize` at the Form level only, leaving the container `AutoSize` chain intact; (3) deliberately exercised hard constraint 3 on `SettingsForm.cs`, documented above; (4) `requirements-completed` is `[]` per this plan's explicit `<output>` instruction.

## Deviations from Plan

None — plan executed exactly as written for both tasks. One self-correction during Task 2: the first draft of the new `OnLoad` doc comment in `SettingsForm.Designer.cs` used the word "ClientSize" in prose, which the verify command's raw `grep -c 'ClientSize'` check (intentionally counting the whole file, comments included) would have failed. Caught and fixed before running the verify command for real — rephrased to "the window's client area" with no functional or behavioral change. Not logged as a Rule 1-4 deviation since no incorrect code was ever committed; this was corrected during in-progress editing, not after a task was marked done.

## Issues Encountered

None. No auth gates, no blocking issues, no architectural questions arose during execution. No package installs were needed (this plan adds zero external dependencies, consistent with the threat model's `T-22-SC` disposition).

## Self-Check

- `src/RigToggle.App/SettingsForm.Designer.cs` — modified, exists — FOUND
- `src/RigToggle.App/SettingsForm.cs` — modified, exists — FOUND
- Commit `ba7b39c` (Task 1) — `git log --oneline --all | grep ba7b39c` — FOUND
- Commit `b38d4ed` (Task 2) — `git log --oneline --all | grep b38d4ed` — FOUND
- All grep/build/test commands quoted above in "Verify Command Output" and "Invariant Counts" were executed directly against the live repository state in this session; results are transcribed verbatim, not estimated

## User Setup Required

None for this plan — no external service configuration required, no rig hardware step in this plan's own scope (Task 1 and Task 2 are both fully automated `type="auto"` tasks with no checkpoint). **Plan 05 will require the user to re-publish and re-run the rig binary on real Windows hardware** to confirm whether Bug A and Bug B are actually fixed — that is explicitly out of scope for this plan per hard constraint 5 ("Do not claim either bug is fixed").

## No Behavioral Claim — Explicit Statement

Per this plan's hard constraint 5 and the threat model's `T-22-19` mitigation: **neither Bug A (broken manual resize) nor Bug B (missing grid/audio picker) is confirmed fixed by this plan.** Nothing in this Linux sandbox environment can render WinForms, run DWM, or exercise `AutoScaleMode.Font` scaling. Everything documented above is source-level, build-level, and test-level evidence only — that the fix is present, internally coherent, does not regress any of the phase's structural invariants (D-01 through D-06), and does not break the build or the 82-test suite. The behavioral claim that either bug is actually resolved belongs exclusively to Plan 05's rig-hardware checkpoint.

## Next Phase Readiness

- Both source-level fixes are committed, green on build (`0 Error(s)`, `4 Warning(s)`) and test (`82/82`), and scoped to exactly the two files this plan was authorized to touch.
- Every locked decision from `22-CONTEXT.md` survives: D-01 (mode grouping), D-02 (shared section), D-03 (`TableLayoutPanel` throughout), D-04 (`pnlThemeReserved` intact), D-05 (content-driven, no fixed target — now delivered via `OnLoad` rather than `Form.AutoSize`), D-06 (`Sizable`, no maximize, no minimize).
- Plan 05 can proceed directly to publishing the rig binary and re-running the 17-check rig-hardware verification from `22-03-PLAN.md`, focusing first on Check 1 and Check 3 (the two that failed) before continuing through the DPI-scale checks that were blocked by them.
- No blockers. `requirements-completed: []` is intentional — SETTINGS-01 and SETTINGS-02 remain rig-pending until Plan 05 reports a result.

---
*Phase: 22-settingsform-layout-pass*
*Completed: 2026-08-15*

## Self-Check: PASSED

- `.planning/phases/22-settingsform-layout-pass/22-04-SUMMARY.md` — FOUND
- Commit `ba7b39c` — FOUND in `git log --oneline --all`
- Commit `b38d4ed` — FOUND in `git log --oneline --all`
