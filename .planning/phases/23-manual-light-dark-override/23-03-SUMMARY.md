---
phase: 23-manual-light-dark-override
plan: 03
subsystem: verification
tags: [winforms, theme, rig-verification, dotnet10, static-audit]

requires:
  - phase: 23-manual-light-dark-override
    plan: 01
    provides: OverridableThemeProvider resolver, composition-root swap, application color-mode derivation
  - phase: 23-manual-light-dark-override
    plan: 02
    provides: SettingsForm System/Light/Dark radio group, live-preview/Save/Discard wiring
provides:
  - Six static-audit results confirming Pitfall 6's three-copy risk is closed and nothing outside the declared ten-file scope moved
  - The full fifteen-check rig checklist, ready to hand to the user, with no verdicts fabricated or inferred
affects: []

actuals:
  tokens: 3100
  tasks: 1
  commits: 1

tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - .planning/phases/23-manual-light-dark-override/23-03-SUMMARY.md
  modified: []

key-decisions:
  - "Task 1's six static audits all PASS with recorded verbatim evidence; no source file was touched (git status --porcelain src/ empty before and after)"
  - "This SUMMARY is written in two parts: Task 1's audit evidence (complete, below) and Task 2's rig checklist (unanswered — awaiting the user's own PASS/FAIL verdicts on real Windows hardware, per hard constraints 3 and 4)"
  - "status: halted — this plan reached its designed blocking checkpoint (Task 2) and intentionally stopped there; it is not done until the user reports rig verdicts and a continuation pass records them"

requirements-completed: []

coverage:
  - id: A1
    description: "Audit 1 — one resolver, three untouched consumers (Pitfall 6, D-04): exactly one type implements IThemeProvider as a decorator, the three resolution property bodies are byte-unchanged since the phase base commit, and no form reaches around the resolver"
    verification:
      - kind: other
        ref: "grep -rln ': IThemeProvider' src/ --include=*.cs | sort (3 files); git diff bbbfebc -- MainForm.cs SettingsForm.cs MonitorConfirmDialog.cs | grep -cE '^[-+].*(IsDark|IsDarkTheme) =>' (0); grep -cF 'ThemeOverride' MainForm.cs / MonitorConfirmDialog.cs (0 each)"
        status: pass
    human_judgment: false
  - id: A2
    description: "Audit 2 — the locked copy (D-06, D-07): exactly three RadioButtons, exactly one Checked=true, no fourth/alternative-wording option in SettingsForm.Designer.cs"
    verification:
      - kind: other
        ref: "grep -cF 'new System.Windows.Forms.RadioButton()' (3); grep -cF '.Checked = true;' (1); grep -cF 'Follow Windows' (0)"
        status: pass
    human_judgment: false
  - id: A3
    description: "Audit 3 — one color-mode decision point: exactly two non-comment Application.SetColorMode call sites project-wide, reached from ApplyDashboardTheming via both OnThemeChanged and InitializeTrayState"
    verification:
      - kind: other
        ref: "grep -rn 'Application.SetColorMode' src/RigToggle.App/ | grep -vE ':\\s*(//|\\*|/\\*)' | wc -l (2); awk-scoped grep for ApplyEffectiveColorMode inside ApplyDashboardTheming (1)"
        status: pass
    human_judgment: false
  - id: A4
    description: "Audit 4 — nothing outside scope moved: zero diff in RigToggle.Windows/ and *.csproj since base commit, exactly the ten declared files touched across src/, clean working tree"
    verification:
      - kind: other
        ref: "git diff --stat bbbfebc -- src/RigToggle.Windows/ '*.csproj' (empty); git diff --name-only bbbfebc -- src/ | sort (10 files, matches 23-01+23-02 declared set); git status --porcelain src/ (empty)"
        status: pass
    human_judgment: false
  - id: A5
    description: "Audit 5 — the preview writes nothing: exactly one _settingsStore.Save( call site in SettingsForm.cs, zero .Save( calls in OverridableThemeProvider.cs, ValidateSettingsForm count unchanged at 12"
    verification:
      - kind: other
        ref: "grep -cF '_settingsStore.Save(' SettingsForm.cs (1); grep -c '\\.Save(' OverridableThemeProvider.cs (0); grep -cF 'ValidateSettingsForm' SettingsForm.cs (12)"
        status: pass
    human_judgment: false
  - id: A6
    description: "Audit 6 — build and test baseline: solution builds with 0 errors, full test suite passes with 0 failures at 97 total (above the 92 floor)"
    verification:
      - kind: unit
        ref: "dotnet build RigToggle.sln --nologo (0 Error(s)); dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo (Passed: 97, Failed: 0, Total: 97)"
        status: pass
    human_judgment: false
  - id: D1
    description: "Rig checks 1-15 covering all three Phase 23 Success Criteria and Pitfall 6's warning-signs procedure on real Windows hardware"
    requirement: "THEME-09"
    verification: []
    human_judgment: true
    rationale: "This build host has no Windows GUI, no registry, no DWM and no display. Every visual/live-flip claim in 23-01 and 23-02 is unverifiable here by design (hard constraints 3 and 4, T-23-12). The checklist below is unanswered — the executor did not run it, infer it, or fabricate verdicts. It is presented as-is, ready for the user to execute on the real rig."

duration: ~15min (Task 1 only; Task 2 pending)
completed: PENDING — plan not done until Task 2's rig verdicts are recorded
status: halted
---

# Phase 23 Plan 3: Static Audits & Rig Verification Checklist Summary

**Six static audits confirm Pitfall 6's three-copy consistency risk is closed with a single resolver and nothing outside the declared ten-file scope moved; the fifteen-check rig verification checklist is prepared and handed to the user unanswered — this build host cannot observe any of Windows' rendering, theme flips, or settings.json writes.**

## Performance

- **Duration:** ~15 min (Task 1 only)
- **Started:** 2026-08-16T21:09:01Z
- **Completed:** PENDING (Task 2 is a blocking checkpoint awaiting the user's rig verdicts)
- **Tasks:** 1 of 2 complete
- **Files modified:** 1 (this SUMMARY.md, created)

## Accomplishments

- All six static audits (Task 1) executed with recorded verbatim evidence — every one PASS, zero VIOLATIONs
- Confirmed exactly one type (`OverridableThemeProvider`) implements the theme-override decorator; the three consumer property bodies (`MainForm.IsDark`, `SettingsForm.IsDarkTheme`, `MonitorConfirmDialog.IsDark`) are byte-unchanged since the phase base commit
- Confirmed exactly two `Application.SetColorMode` call sites project-wide (`Program.cs` priming call, `ThemeApplier.ApplyEffectiveColorMode`), reached from both `MainForm` theming call sites (`OnThemeChanged`, `InitializeTrayState`) via the shared `ApplyDashboardTheming()` helper
- Confirmed nothing outside the declared ten-file scope moved: zero diff in `RigToggle.Windows/` and every `.csproj` since the phase base commit
- Confirmed the live-preview path writes nothing to disk (`SetPreviewOverride`/`RefreshOverride` never call `.Save(`)
- Build green (0 errors), full test suite green (97/97, above the 92-total floor)
- The fifteen-check rig verification checklist (Task 2) is prepared below, unanswered, ready to hand to the user

## Task Commits

1. **Task 1: Static audits — prove the three-copy risk is closed and nothing outside scope moved** - (commit recorded below, this SUMMARY.md is the task's sole deliverable; no source file changed)

_Task 2 (rig verification) is a `checkpoint:human-verify` and has not been executed or committed — it awaits the user's own PASS/FAIL verdicts on real Windows hardware._

## Files Created/Modified

- `.planning/phases/23-manual-light-dark-override/23-03-SUMMARY.md` - this file (static-audit evidence + prepared rig checklist)

## Static Audit Evidence (Task 1, verbatim)

### Audit 1 — one resolver, three untouched consumers (Pitfall 6, D-04)

```
$ grep -rln ': IThemeProvider' src/ --include=*.cs | sort
src/RigToggle.Core/OverridableThemeProvider.cs
src/RigToggle.Tests/Doubles/FakeThemeProvider.cs
src/RigToggle.Windows/WindowsThemeProvider.cs

$ git diff bbbfebc -- src/RigToggle.App/MainForm.cs src/RigToggle.App/SettingsForm.cs src/RigToggle.App/MonitorConfirmDialog.cs | grep -cE '^[-+].*(IsDark|IsDarkTheme) =>'
0

$ grep -cF 'ThemeOverride' src/RigToggle.App/MainForm.cs
0

$ grep -cF 'ThemeOverride' src/RigToggle.App/MonitorConfirmDialog.cs
0
```

`ThemeOverride` occurrences in `SettingsForm.cs` are exclusively the pending-value field, constructor params, `SettingsForm_Load` selection, the radio handler, and the `settingsToSave` initializer — no bypass of the resolver.

**Verdict: PASS**

### Audit 2 — the locked copy (D-06, D-07)

```
$ grep -cF 'new System.Windows.Forms.RadioButton()' src/RigToggle.App/SettingsForm.Designer.cs
3

$ grep -cF '.Checked = true;' src/RigToggle.App/SettingsForm.Designer.cs
1

$ grep -cF 'Follow Windows' src/RigToggle.App/SettingsForm.Designer.cs
0

$ grep -cF 'System theme' src/RigToggle.App/SettingsForm.Designer.cs
0
```

Note: a raw `grep -cF 'Auto'` against this file returns 129 (from `AutoSize`/`AutoScaleMode`/etc. tokens throughout the designer file, not from any radio-button label) — this is not part of the plan's binding `acceptance_criteria` list. A precise check (`grep -nE '\.Text = "[^"]*Auto[^"]*"'`) confirms zero radio-button `Text` assignments contain the word "Auto".

**Verdict: PASS**

### Audit 3 — one color-mode decision point

```
$ grep -rn 'Application.SetColorMode' src/RigToggle.App/ --include=*.cs | grep -vE ':\s*(//|\*|/\*)'
src/RigToggle.App/Program.cs:42:            System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
src/RigToggle.App/ThemeApplier.cs:319:                Application.SetColorMode(dark ? SystemColorMode.Dark : SystemColorMode.Classic);

$ ... | wc -l
2

$ awk '/private void ApplyDashboardTheming/,/^        }$/' src/RigToggle.App/MainForm.cs | grep -cF 'ApplyEffectiveColorMode'
1
```

Confirmed both `OnThemeChanged` (line 190) and `InitializeTrayState` (line 293) call `ApplyDashboardTheming()`, which is the sole call site reaching `ApplyEffectiveColorMode`.

**Verdict: PASS**

### Audit 4 — nothing outside scope moved

```
$ git diff --stat bbbfebc -- src/RigToggle.Windows/
(empty)

$ git diff --stat bbbfebc -- '*.csproj'
(empty)

$ git diff --name-only bbbfebc -- src/ | sort
src/RigToggle.App/MainForm.cs
src/RigToggle.App/MonitorConfirmDialog.cs
src/RigToggle.App/Program.cs
src/RigToggle.App/SettingsForm.cs
src/RigToggle.App/SettingsForm.Designer.cs
src/RigToggle.App/ThemeApplier.cs
src/RigToggle.Core/Models/AppSettings.cs
src/RigToggle.Core/OverridableThemeProvider.cs
src/RigToggle.Tests/Doubles/InMemoryStores.cs
src/RigToggle.Tests/OverridableThemeProviderTests.cs

$ git status --porcelain src/
(empty)
```

Exactly ten files, matching 23-01-SUMMARY.md's 9 (7 modified + 2 created) and 23-02-SUMMARY.md's 3 (with overlap on `Program.cs`).

**Verdict: PASS**

### Audit 5 — the preview writes nothing

```
$ grep -cF '_settingsStore.Save(' src/RigToggle.App/SettingsForm.cs
1

$ grep -c '\.Save(' src/RigToggle.Core/OverridableThemeProvider.cs
0

$ grep -cF 'ValidateSettingsForm' src/RigToggle.App/SettingsForm.cs
12
```

**Verdict: PASS**

### Audit 6 — build and test baseline

```
$ dotnet build RigToggle.sln --nologo
...
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo
...
Passed!  - Failed:     0, Passed:    97, Skipped:     0, Total:    97, Duration: 94 ms - RigToggle.Tests.dll (net10.0)
```

**Verdict: PASS**

**`OverridableThemeProvider.CurrentTheme` exact resolution expression:**
```csharp
public AppTheme CurrentTheme
{
    get
    {
        lock (_lock)
        {
            if (_hasPreview)
            {
                return _previewOverride ?? _inner.CurrentTheme;
            }

            return _persistedOverride ?? _inner.CurrentTheme;
        }
    }
}
```

**Public method signatures (verbatim):**
```csharp
public void SetPreviewOverride(AppTheme? previewOverride)
public void RefreshOverride()
```

## Three Phase 23 Success Criteria — Machine-Verified vs. Rig-Pending

| # | Criterion | Machine-verified (this task) | Rig-pending (Task 2) |
|---|-----------|-------------------------------|------------------------|
| 1 | Settings offers System/Light/Dark, defaulting to System | Audit 2: exactly 3 radios, exactly 1 pre-checked, correct labels in source | Checks 1, 5 — actual rendered appearance/pre-selection |
| 2 | Selecting Light/Dark locks and applies without restart | Audit 1 (single resolver), Audit 3 (color-mode derivation), Audit 5 (no premature write) | Checks 3, 5, 12 — actual live repaint across all three surfaces, including the known residual (in-place Settings repaint) |
| 3 | A live OS flip does not override a lock; System restores live-follow | Audit 1 (resolution order unchanged in source) | Checks 6, 7, 8, 9 — actual Pitfall 6 warning-signs procedure on real hardware |

**All three criteria remain rig-pending.** Static inspection cannot confirm rendered/runtime behavior — that is exactly what Task 2 exists for.

---

## Task 2: Rig-Hardware Verification Checklist (UNANSWERED — awaiting user execution)

**This build host has no Windows GUI, no registry, no DWM and no display. None of the fifteen checks below can be observed, inferred, or fabricated here.** The executor has not attempted to answer any of them. They are presented exactly as scoped in `23-03-PLAN.md` Task 2, ready for the user to run on real Windows hardware.

**Publish command:** `dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` (reuse whichever workflow Phase 21/22 actually used if it differed, and record which).

**Before starting:** note current Windows theme (Settings > Personalization > Colors > "Choose your mode") and copy `%LOCALAPPDATA%\RigToggle\settings.json` — checks 10 and 11 compare against it.

1. **Default is System.** Open Settings. Confirm the Theme group exists near the bottom of the shared section, shows exactly three options reading `System (default)`, `Light` and `Dark` in that order, and that `System (default)` is selected (assuming no override was ever saved on this machine). Report the exact label text, character for character.

2. **It looks like a sibling, not a feature.** Confirm the Theme group carries no bold caption, no border/box, no different font, sits in the same flat stack as the debug-logging and tray checkboxes above it. Confirm nothing above it moved/overlaps and no resize was needed.

3. **Light/Dark applies immediately, no Save, no restart.** With Settings open, click `Dark` — the main window behind it should repaint immediately (background, tiles, switch, buttons, title bar). Do not Save. Report. Then click `Light`, report. Then click `Dark` again and leave it for check 4.

4. **Discard reverts the preview.** With `Dark` previewed and unsaved, click **Discard Changes** — should revert to pre-open state (System/live-follow). Repeat, exiting via **Esc**, then via the window's **X** — all three should revert identically. Report each.

5. **Save persists.** Open Settings, select `Dark`, click **Save Changes**. App stays dark. Close and relaunch entirely — should come up dark with no flash, and Settings should show `Dark` selected. Report both.

6. **Pitfall 6, main window.** With `Dark` saved and running, flip Windows Settings > Personalization > Colors to **Light**. The app's main window must stay dark (background, tiles, switch, buttons, title bar). Report which elements, if any, followed Windows to light. **Most important check in this list.**

7. **Pitfall 6, Settings window.** With Windows on Light and app locked Dark, open Settings — must open fully dark (labels, checkboxes, radios, both grids, dropdowns, hotkey box, buttons). Report anything light.

8. **Pitfall 6, confirmation dialog.** With Windows on Light and app locked Dark, trigger the monitor confirmation dialog. Must appear fully dark. Cancel. Report anything light.

9. **System restores live-follow.** Select `System (default)`, Save. App should immediately follow Windows' current mode (Light). Flip Windows to Dark — app follows live, no restart. Flip to Light again — follows again. Report all three transitions; confirm main window, a freshly-opened Settings window, and the confirmation dialog all agree each time.

10. **Windows itself is untouched.** Confirm nothing in the app changed Windows' "Choose your mode", accent color, or other personalization settings. Report current Windows mode and confirm it's where you last put it.

11. **Nothing else in settings.json moved.** Change only Theme, Save, compare `%LOCALAPPDATA%\RigToggle\settings.json` against the pre-start copy — only `ThemeOverride` should differ (integer: `0`=Light, `1`=Dark, absent/null=System). Confirm monitors/audio/app path/hotkey/tray-autostart checkboxes unchanged (verify by reopening Settings too). Separately: preview a change, click Discard, confirm settings.json is byte-identical afterward.

12. **The known open question — in-place repaint of the Settings window itself.** With Windows on Light and no override saved, open Settings and click `Dark` while it stays open. Report specifically: do the radio labels, the four checkbox labels, section captions, and window/panel backgrounds *inside that already-open window* turn dark, or stay light while buttons/grids/dropdowns go dark? Then close and reopen Settings — should now be fully dark. Report both halves separately. **A FAIL on the first half with a PASS on the second is the specific outcome this check exists to detect** — see the plan's Known Residual Risk section for the pre-authorised gap-closure direction if it fails.

13. **Main window background matches its tiles.** In dark mode, confirm the main window's background matches the monitor tiles' shade — no seam, no change to rounded-corner/Mica look versus before this phase.

14. **No functional regression.** One full Rig→Normal→Rig toggle with a theme lock active. Confirm monitors/audio switch as always, companion app launches/minimizes as usual, tray icon/menu work, global hotkey triggers a toggle. Confirm accent color still tints the switch's ON state and focus rings (THEME-07 must not regress). Report anything different from before this phase.

15. **Tray startup with a lock set.** Save a `Dark` override, exit, start with `--tray`. Restore from tray — should already be dark, no light flash. Report what you see.

**For any FAIL:** state which check, what was expected, what was actually seen, and (where relevant) a screenshot. Do not fix anything — a FAIL becomes a gap-closure plan, per hard constraint 1.

### Criteria → Check Mapping (for the continuation pass to fill in verdicts)

| Criterion | Checks | Verdict |
|-----------|--------|---------|
| 1 — System/Light/Dark, default System | 1, 5 | *(pending)* |
| 2 — Lock applies without restart | 3, 5, 12 | *(pending)* |
| 3 — Live flip doesn't override lock; System restores live-follow | 6, 7, 8, 9 | *(pending)* |

**THEME-09 requirement status:** NOT ticked in `.planning/REQUIREMENTS.md` — will only be ticked once every criterion above is PASS, per this plan's acceptance criteria.

## Decisions Made

- Followed the plan's `acceptance_criteria` (the binding contract) over its `<action>` narrative prose where the two diverged slightly (Audit 2's raw `Auto` grep count) — documented above with the precise alternative check that confirms no violation exists.
- Wrote this SUMMARY.md in two halves so Task 1's completed, evidence-backed work is committed and durable even though Task 2 (the blocking checkpoint) has not resolved — this plan is explicitly `status: halted`, not `complete`.

## Deviations from Plan

None — Task 1 executed exactly as written, all six audits recorded PASS with verbatim evidence, `git status --porcelain src/` confirmed empty both before and after.

## Issues Encountered

None for Task 1. Task 2 has not been attempted — it requires real Windows rig hardware this build host does not have.

## User Setup Required

**This plan cannot complete without you.** Task 2 is a blocking human-verify checkpoint (`gate="blocking"`, `autonomous: false`) requiring:
1. A Windows 10/11 rig with the app's configured monitors and audio devices
2. Publishing the app per the command above and running it there
3. Working through all fifteen checks in order and reporting each as PASS or FAIL with notes

See the full checklist above.

## Next Phase Readiness

- Not ready — Phase 23 is not done until Task 2's rig verdicts are recorded and all three success criteria carry a traceable PASS or FAIL.
- `git status --porcelain src/` is clean; no source file was touched by this plan.
- Once the user reports verdicts, a continuation pass should: record each check's verdict verbatim in this SUMMARY, fill in the criteria table above, tick THEME-09 in REQUIREMENTS.md only if every criterion is PASS, flip `status: halted` to `status: complete`, and update STATE.md/ROADMAP.md.

## Known Stubs

None.

## Threat Flags

None — this plan introduced no new surface; it only reads and audits existing code.

---
*Phase: 23-manual-light-dark-override*
*Completed: PENDING (Task 2 rig checkpoint outstanding)*
