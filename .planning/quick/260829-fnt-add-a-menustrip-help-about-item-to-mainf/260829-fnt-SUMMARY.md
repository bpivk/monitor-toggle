---
phase: quick-260829-fnt
plan: 01
subsystem: ui
tags: [winforms, menustrip, theming, update-check]

requires:
  - phase: 26-auto-update
    provides: UpdateOrchestrator.CheckOnDemandAsync / PerformManualUpdateCheck, the shared
      manual update-check body this plan's About dialog button reuses unchanged
provides:
  - "UpdateOrchestrator.FormatDisplayVersion(Version) / RunningVersionText -- the single
    three-component Major.Minor.Patch display-string implementation"
  - "A Help > About menu on MainForm opening a themed AboutForm (name, version, Check for
    Updates, Close)"
  - "ThemeApplier.ThemeMenuStrip(MenuStrip, bool) -- targeted MenuStrip theming helper"
affects: [ui, auto-update]

actuals:
  tokens: 6900
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Version display text has exactly one implementation (UpdateOrchestrator.FormatDisplayVersion);
      every display surface (balloon, About dialog) reads from it or from RunningVersionText"
    - "New transient themed dialogs (AboutForm) follow UpdatePromptDialog's exact structural
      template: ctor theming block, ThemeChanged subscribe + FormClosed unsubscribe + Dispose
      backstop, IsDark property, marshalled OnThemeChanged"
    - "New ThemeApplier helpers target one caller-supplied control instance (never a recursive
      Controls-tree walk), per the class's existing convention"

key-files:
  created:
    - src/RigToggle.App/AboutForm.cs
    - src/RigToggle.App/AboutForm.Designer.cs
  modified:
    - src/RigToggle.Core/UpdateOrchestrator.cs
    - src/RigToggle.Tests/UpdateOrchestratorTests.cs
    - src/RigToggle.App/ThemeApplier.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs

key-decisions:
  - "CheckAsync's inline runningVersionText interpolation was replaced with a read of the new
    RunningVersionText property (pure extraction, byte-identical output) rather than calling
    FormatDisplayVersion directly, per the plan's explicit instruction"
  - "ShowAboutDialog resolves version text via _updateOrchestrator?.RunningVersionText, falling
    back to UpdateOrchestrator.FormatDisplayVersion(GetEntryAssembly...Version ?? new Version(0,0))
    for the null-orchestrator test-harness case, mirroring Program.cs's own resolution exactly"
  - "menuStrip is appended to Controls LAST (after the five existing Controls.Add calls) so the
    D-09 tile/Identify/toggle/Settings tab order stays byte-for-byte unchanged"

requirements-completed: [UPDATE-06]

coverage:
  - id: D1
    description: "UpdateOrchestrator.FormatDisplayVersion produces three-component Major.Minor.Patch
      text (Build normalized, Revision dropped, null guarded), and RunningVersionText reads from it"
    requirement: UPDATE-06
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#FormatDisplayVersion_NormalizesToThreeComponents"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#FormatDisplayVersion_FourComponentVersion_DropsRevision"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#FormatDisplayVersion_NullVersion_ThrowsArgumentNullException"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#RunningVersionText_ReflectsFormatDisplayVersionOfConstructorVersion"
        status: pass
    human_judgment: false
  - id: D2
    description: "MainForm carries a Help menu with a single About item opening a themed AboutForm
      (name, version, Check for Updates wired to MainForm.PerformManualUpdateCheck, Close); menu
      bar and dialog theme correctly and do not overlap existing controls, on the normal and
      --tray startup paths, at 100/125/150% scaling"
    requirement: UPDATE-06
    verification: []
    human_judgment: true
    rationale: "Requires a native Windows build (net10.0-windows WinForms) launched and visually
      inspected on real rig hardware -- cannot be run or rendered on this Linux dev host. This is
      Task 3's blocking checkpoint, not yet performed. See 'Pending Human Verification' below."

duration: ~35min (Tasks 1-2 only; Task 3 not run)
completed: 2026-08-29
status: incomplete
superseded_by: quick-260829-ga9
superseded_note: "Task 3's rig-verification checkpoint was never answered -- instead of
  verifying, the user requested two follow-up changes to this same feature (remove the
  tray/Settings Check-for-Updates entries, fix the About dialog's silent-feedback bug).
  Those changes and their own rig-verification checkpoint are tracked in
  quick-260829-ga9. This task's own deliverables (MenuStrip, AboutForm, shared version
  formatter) are still live in master -- only the checkpoint response is superseded,
  not the code."
---

# Phase quick-260829-fnt Plan 01: MenuStrip Help > About Summary

**Extracted a single UpdateOrchestrator.FormatDisplayVersion formatter and added a themed
Help > About menu/dialog to MainForm, reusing the existing manual update-check path
unchanged; rig verification (Task 3) is still pending.**

## Performance

- **Duration:** ~35 min (Tasks 1-2)
- **Tasks:** 2 of 3 completed (Task 3 is a blocking human-verify checkpoint, not run)
- **Files modified:** 7 (2 created, 5 modified)

## Accomplishments

- `UpdateOrchestrator.FormatDisplayVersion(Version)` + `RunningVersionText`: the one
  three-component Major.Minor.Patch display-string implementation, extracted from
  `CheckAsync`'s previously-inline interpolation with byte-identical output.
- New `AboutForm`/`AboutForm.Designer.cs`: a themed modal dialog showing "Rig Toggle", the
  running version, a `Check for Updates` button that invokes the caller-supplied
  `performManualUpdateCheck` delegate (never a re-implementation), and `Close`.
- New `ThemeApplier.ThemeMenuStrip(MenuStrip, bool)`: shallow, exhaustive two-level recolor
  for the fixed Help > About menu shape, reusing `ThemeFormSurface`'s `BackColor` literals and
  `ThemeButton`'s `ForeColor` literals.
- `MainForm` now carries a docked-top `MenuStrip` with a `Help > About` item, wired into the
  existing `ApplyDashboardTheming` call site and `LayoutDashboard`'s `stripTop` offset
  (`Math.Max(menuStrip.Height, menuStrip.PreferredSize.Height)`, `--tray`-safe).
- The tray `Check for Updates` item and the Settings dialog `Check for Updates` button are
  byte-for-byte untouched (`SettingsForm.cs`/`SettingsForm.Designer.cs`/`Program.cs` diff is
  empty; `trayCheckUpdatesMenuItem`/`traySettingsMenuItem`/`BtnCheckForUpdates_Click` all
  confirmed present).

## Task Commits

Each task was committed atomically:

1. **Task 1: One shared running-version display formatter in Core** - `389535e` (feat, TDD)
2. **Task 2: MenuStrip Help > About on MainForm, opening a themed AboutForm** - `04d765b` (feat)

Task 3 (checkpoint:human-verify, rig verification) was not executed — see "Pending Human
Verification" below.

**Plan metadata:** not yet committed (deferred to orchestrator's docs commit per this task's
constraints).

## Files Created/Modified

- `src/RigToggle.Core/UpdateOrchestrator.cs` - Added `FormatDisplayVersion(Version)` static
  method and `RunningVersionText` property; `CheckAsync` now reads `RunningVersionText`
  instead of re-deriving the string inline.
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` - Added `FormatDisplayVersion` normalization/
  revision-drop/null-guard tests and a `RunningVersionText` consistency test.
- `src/RigToggle.App/AboutForm.cs` - New themed About dialog.
- `src/RigToggle.App/AboutForm.Designer.cs` - Its layout, following `UpdatePromptDialog.Designer.cs`'s
  convention.
- `src/RigToggle.App/ThemeApplier.cs` - Added `ThemeMenuStrip(MenuStrip, bool)`.
- `src/RigToggle.App/MainForm.Designer.cs` - Added `menuStrip`/`helpMenuItem`/`helpAboutMenuItem`,
  `MainMenuStrip` assignment, appended to `Controls` last.
- `src/RigToggle.App/MainForm.cs` - Added `HelpAboutMenuItem_Click`/`ShowAboutDialog`, the
  `ThemeMenuStrip` call in `ApplyDashboardTheming`, and the `stripTop` offset in
  `LayoutDashboard`.

## Decisions Made

- Extraction in Task 1 is a pure refactor: `CheckAsync` reads the new `RunningVersionText`
  property (per the plan's explicit instruction), not the static `FormatDisplayVersion` method
  directly — both ultimately produce the identical string, so the balloon and the About dialog
  can never disagree.
- `ShowAboutDialog`'s null-orchestrator fallback mirrors `Program.cs` line 299's own version
  resolution (`GetEntryAssembly()?.GetName().Version ?? new Version(0, 0)`) exactly, routed
  through the same `FormatDisplayVersion`.
- `menuStrip` is appended to `MainForm.Controls` last (after the five pre-existing controls) so
  the documented D-09 tab order (tiles, Identify, toggle, Settings gear) is unaffected by
  `Controls.Add` order.

## Deviations from Plan

### Auto-fixed Issues

None — Rules 1/2/3 did not trigger; no bugs, missing critical functionality, or blocking
issues were found during Task 1/Task 2 implementation.

### Documentation-level discrepancy (not a functional issue)

Task 1's `<done>` criterion states the filtered grep
(`grep -v '^[[:space:]]*//' UpdateOrchestrator.cs | grep -c 'FormatDisplayVersion'`) should
count "at least 3" code-line occurrences, parenthetically describing only two direct items
("the static method, the `RunningVersionText` property body") plus a third, separately-verified
fact ("no remaining inline duplicate", checked by an entirely different, already-passing grep
for `Math.Max(_runningVersion.Build`). The actual, correct implementation — one static method
declaration plus one property expression body reading it, with `CheckAsync` deliberately routed
through the property rather than calling the static method a second time, per the plan's own
action text — produces exactly 2 non-comment matches, not 3. Inflating this to 3 would require
either duplicating the call (reintroducing exactly the drift risk this task exists to eliminate)
or deviating from the plan's explicit "replace with a read of `RunningVersionText`" instruction.
Treated as a minor authoring inconsistency in the plan's verify description, not a defect in the
shipped code — the substantive intent (single implementation, no duplicate, both the static
method and the property body present) is fully met and independently confirmed by the separate
zero-count duplicate-detection grep and the full passing test suite (229/229).

---

**Total deviations:** 0 auto-fixed. One documentation-level discrepancy noted above (verify-gate
count text vs. the plan's own described components), with no code impact.
**Impact on plan:** None on functionality. All success criteria for Tasks 1-2 are met.

## Issues Encountered

None during Task 1/Task 2 execution. Both build and full test suite (229/229 passing) confirm
correctness on the Linux dev host, which is as far as this environment can verify per the
plan's own "Environment facts confirmed at plan time" note.

## Pending Human Verification

**Task 3 (`checkpoint:human-verify`, gate=`blocking`) has NOT been performed.** It requires a
Windows machine able to build and run `RigToggle.App.exe` natively — the `net10.0-windows`
WinForms project compiles on this Linux worktree but cannot be launched or visually inspected
here.

Quoting the plan's exact checkpoint ask (`260829-fnt-PLAN.md` Task 3):

> Build and launch on the rig: `dotnet build RigToggle.sln`, then run
> `src/RigToggle.App/bin/Debug/net10.0-windows/RigToggle.App.exe`.
>
> 1. Main window layout — the menu bar sits at the top; the monitor tile row, the
>    Identify/toggle row and the Settings gear are all fully visible and NOT clipped or
>    overlapped. The window is taller by roughly the menu bar's height and nothing overflows
>    past the bottom edge.
> 2. Repeat check 1 at 125% and 150% Windows display scaling (this app has a documented
>    history of scale-dependent layout regressions) — no overlap at any scale.
> 3. `Help` > `About` opens the dialog centered on the main window. It shows "Rig Toggle"
>    and a version reading exactly `2.2.1` — three components, not `2.2` and not `2.2.1.0`.
> 4. Click `Check for Updates` in the About dialog. It behaves exactly like the tray item:
>    either an update prompt appears, or a tray balloon reports you are already on the
>    latest version naming `2.2.1`, or a warning balloon reports the failure reason.
> 5. Press `Esc`, then reopen and click `Close` — both dismiss the dialog. Reopen a third
>    time to confirm it can be opened repeatedly with no error and no leaked window.
> 6. ADDITIVE CHECK (the one regression that would make this change unacceptable): the tray
>    icon's `Check for Updates` item still works, and Settings' own `Check for Updates`
>    button still works. Both must behave exactly as they did before.
> 7. Theming — with Windows in dark mode (or the app's theme override set to Dark), confirm
>    the menu bar and the About dialog are legible: no dark-on-dark or white-on-white text
>    in the `Help` bar, the open `About` dropdown, the two dialog buttons, or the app
>    name/version labels. Repeat in light mode.
> 8. Live theme flip — with the main window open, flip Windows between light and dark. The
>    menu bar recolors along with the rest of the window. NOTE: the dropdown's separator/
>    arrow glyphs keeping a stale color across a live flip is a pre-existing, accepted
>    WinForms limitation (dotnet/winforms#12027, already documented for the tray menu) — not
>    a failure of this change.
> 9. `--tray` startup path — launch with the `--tray` argument, then restore the window from
>    the tray icon. Check 1's layout must be correct on that first paint too, since that
>    path never runs `Form.Load`.
>
> Report per-check pass/fail. For any failure, include what you saw (a screenshot is ideal
> for layout/theming issues) — do not report a check as passed if it was not actually run.
>
> **Resume signal:** Type "approved" if checks 1-9 pass, or list the failing check numbers
> with what you observed.

UPDATE-06 is NOT yet marked complete pending this checkpoint. Both build (`dotnet build
RigToggle.sln`, 0 errors) and the full test suite (229/229 passing, including the 6 new
`FormatDisplayVersion`/`RunningVersionText` cases) are confirmed on this dev host, and all of
Task 2's mechanical additive-only verify gates (AboutForm files present, `ThemeMenuStrip`
helper/call/layout-offset/handler/`new AboutForm` counts, tray/Settings preservation gates,
zero-file diff on `SettingsForm.cs`/`SettingsForm.Designer.cs`/`Program.cs`) pass.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Tasks 1-2 are complete, committed, and self-verified; the code is ready for rig testing.
- Blocked on Task 3's rig checkpoint (see above) before this quick task and UPDATE-06 can be
  marked fully complete.

---
*Phase: quick-260829-fnt*
*Completed: 2026-08-29 (Tasks 1-2 only; Task 3 pending)*

## Self-Check: PASSED

- FOUND: src/RigToggle.App/AboutForm.cs
- FOUND: src/RigToggle.App/AboutForm.Designer.cs
- FOUND: .planning/quick/260829-fnt-add-a-menustrip-help-about-item-to-mainf/260829-fnt-SUMMARY.md
- FOUND commit: 389535e (Task 1)
- FOUND commit: 04d765b (Task 2)
