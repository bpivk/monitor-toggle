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
  - Fifteen-check rig verification of all three Phase 23 success criteria, all PASS, run and reported by the user on real Windows hardware
  - THEME-09 authorized complete in REQUIREMENTS.md
affects: []

actuals:
  tokens: 4800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns: []

key-files:
  created:
    - .planning/phases/23-manual-light-dark-override/23-03-SUMMARY.md
  modified: []

key-decisions:
  - "Task 1's six static audits all PASS with recorded verbatim evidence; no source file was touched (git status --porcelain src/ empty before and after)"
  - "Task 2's fifteen rig checks were run by the user on real Windows hardware and reported back through the orchestrator; all fifteen PASS, including check 12's in-place-repaint half (which passed cleanly — better than the pre-scoped expected residual), so the Known Residual Risk section's pre-authorized gap-closure direction was not needed"
  - "All three Phase 23 success criteria are PASS; THEME-09 is ticked in REQUIREMENTS.md as authorized by this rig pass"
  - "status: complete — both tasks finished, all fifteen rig checks and all three success criteria carry a user-reported PASS verdict"

requirements-completed: [THEME-09]

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
    verification:
      - kind: manual_procedural
        ref: "23-03-PLAN.md Task 2, checks 1-15, run by the user on real Windows hardware and reported through the orchestrator"
        status: pass
    human_judgment: true
    rationale: "This build host has no Windows GUI, no registry, no DWM and no display — every visual/live-flip claim required a human on real rig hardware (hard constraints 3 and 4, T-23-12). The user ran all fifteen checks and reported PASS for every one; verdicts recorded verbatim below, not inferred or fabricated by the executor."

duration: ~40min (Task 1 ~15min static audits + Task 2 rig verification round trip)
completed: 2026-08-17
status: complete
---

# Phase 23 Plan 3: Static Audits & Rig Verification Checklist Summary

**Six static audits confirm Pitfall 6's three-copy consistency risk is closed with a single resolver, and all fifteen rig checks pass on real Windows hardware — all three Phase 23 success criteria are PASS, including the known open question (check 12's in-place Settings repaint) resolving cleanly with no residual gap.**

## Performance

- **Duration:** ~40 min (Task 1 ~15 min static audits + Task 2 rig verification round trip)
- **Started:** 2026-08-16T21:09:01Z
- **Completed:** 2026-08-17
- **Tasks:** 2 of 2 complete
- **Files modified:** 1 (this SUMMARY.md, created then updated with rig verdicts)

## Accomplishments

- All six static audits (Task 1) executed with recorded verbatim evidence — every one PASS, zero VIOLATIONs
- Confirmed exactly one type (`OverridableThemeProvider`) implements the theme-override decorator; the three consumer property bodies (`MainForm.IsDark`, `SettingsForm.IsDarkTheme`, `MonitorConfirmDialog.IsDark`) are byte-unchanged since the phase base commit
- Confirmed exactly two `Application.SetColorMode` call sites project-wide (`Program.cs` priming call, `ThemeApplier.ApplyEffectiveColorMode`), reached from both `MainForm` theming call sites (`OnThemeChanged`, `InitializeTrayState`) via the shared `ApplyDashboardTheming()` helper
- Confirmed nothing outside the declared ten-file scope moved: zero diff in `RigToggle.Windows/` and every `.csproj` since the phase base commit
- Confirmed the live-preview path writes nothing to disk (`SetPreviewOverride`/`RefreshOverride` never call `.Save(`)
- Build green (0 errors), full test suite green (97/97, above the 92-total floor)
- All fifteen rig checks (Task 2) PASS on real Windows hardware, reported by the user — including check 12's in-place Settings repaint, which resolved cleanly (no gap-closure needed)
- All three Phase 23 success criteria PASS; THEME-09 ticked in `REQUIREMENTS.md`

## Task Commits

1. **Task 1: Static audits — prove the three-copy risk is closed and nothing outside scope moved** - `7f24ec7` (docs; this SUMMARY.md is the task's sole deliverable, no source file changed)
2. **Task 2: Rig-hardware verification of all three Phase 23 success criteria and Pitfall 6** - (recorded in this update; the checkpoint's deliverable is the verdicts below, committed alongside this SUMMARY update)

_Task 2 is a `checkpoint:human-verify` — its content is the user's own PASS/FAIL verdicts, run on real Windows hardware and reported through the orchestrator. No source file changed._

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

## Three Phase 23 Success Criteria — Final Verdicts

| # | Criterion | Checks | Verdict |
|---|-----------|--------|---------|
| 1 | Settings offers System/Light/Dark, defaulting to System | 1, 5 | **PASS** |
| 2 | Selecting Light/Dark locks and applies without restart | 3, 5, 12 | **PASS** (including check 12's first half — see below) |
| 3 | A live OS flip does not override a lock; System restores live-follow | 6, 7, 8, 9 | **PASS** |

**All three success criteria are PASS.** `THEME-09` is ticked in `.planning/REQUIREMENTS.md`, authorized by this rig pass.

---

## Task 2: Rig-Hardware Verification Checklist — Verdicts (run by the user on real Windows hardware)

**This build host has no Windows GUI, no registry, no DWM and no display — the executor could not run, infer, or fabricate any of these checks.** The user personally ran all fifteen checks on real Windows hardware and reported the verdicts below through the orchestrator, itemized by group, matching this plan's exact checklist from `23-03-PLAN.md` Task 2.

**Publish command used:** `dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`.

1. **Default is System.** **PASS** — no notes.
2. **It looks like a sibling, not a feature.** **PASS** — no notes.
3. **Light/Dark applies immediately, no Save, no restart.** **PASS** — no notes.
4. **Discard reverts the preview (Discard Changes / Esc / window X).** **PASS** — no notes.
5. **Save persists across relaunch.** **PASS** — no notes.
6. **Pitfall 6, main window.** **PASS** — nothing followed Windows to light; main window stayed dark through the live OS flip.
7. **Pitfall 6, Settings window.** **PASS** — Settings opened fully dark with Windows on Light and the app locked Dark.
8. **Pitfall 6, confirmation dialog.** **PASS** — confirmation dialog rendered fully dark under the same conditions.
9. **System restores live-follow (both directions).** **PASS** — app followed Windows' live flips correctly in both directions, no notes.
10. **Windows itself is untouched.** **PASS** — no notes.
11. **Nothing else in settings.json moved.** **PASS** — only `ThemeOverride` differed after a theme-only Save; a previewed-then-discarded change left `settings.json` byte-identical.
12. **The known open question — in-place repaint of the Settings window itself.** **PASS, both halves.** The already-open Settings window fully repainted to dark in place the instant `Dark` was clicked (radio labels, checkbox labels, section captions, and panel backgrounds all turned dark, not just the `ThemeApplier`-managed buttons/grids/dropdowns) — better than the pre-scoped expected residual. It was also fully dark on close/reopen. **The Known Residual Risk section's pre-authorized gap-closure direction was NOT needed — no gap to record.**
13. **Main window background matches its tiles.** **PASS** — no seam, no notes.
14. **No functional regression.** **PASS** — full toggle, tray, hotkey, and accent-color (THEME-07) behavior all unchanged with a theme lock active.
15. **Tray startup with a lock set.** **PASS** — app started already dark from `--tray` with a saved `Dark` override, no light flash.

**No FAILs.** No gap-closure plan is needed for this phase.

## Decisions Made

- Followed the plan's `acceptance_criteria` (the binding contract) over its `<action>` narrative prose where the two diverged slightly (Audit 2's raw `Auto` grep count) — documented above with the precise alternative check that confirms no violation exists.
- Recorded Task 1's evidence-backed audit results in a first commit, then updated the same SUMMARY.md with Task 2's rig verdicts once the user reported them — the plan spent one round as `status: halted` (blocking checkpoint outstanding) before flipping to `status: complete` here.
- Check 12 passing both halves means the plan's pre-authorized gap-closure direction (extending `ThemeApplier` with an explicit `ForeColor`/`BackColor` pass over `SettingsForm`) is not applied — it remains documented in the plan as a contingency that was not triggered.

## Deviations from Plan

None — Task 1 executed exactly as written, all six audits recorded PASS with verbatim evidence, `git status --porcelain src/` confirmed empty both before and after. Task 2 was executed by the user exactly per the plan's checklist, with no FAILs and no need for the pre-authorized gap-closure fallback.

## Issues Encountered

None. All fifteen rig checks and all six static audits passed on the first attempt.

## User Setup Required

None remaining — the user has already completed the one setup step this plan required (running the fifteen-check rig verification on real Windows hardware).

## Next Phase Readiness

- Phase 23 (Manual Light/Dark Override, THEME-09) is complete. All three success criteria are PASS, all fifteen rig checks are PASS, THEME-09 is ticked in `REQUIREMENTS.md`.
- `git status --porcelain src/` is clean; no source file was touched anywhere in this plan.
- No blockers. No gap-closure plan is needed — check 12 (the plan's one open question) resolved cleanly.
- This closes out the v2.1 milestone's THEME-07/08/09 backlog (Phases 20, 21, 23) alongside Phase 19 (tile dashboard) and Phase 22 (Settings layout) — v2.1's full requirement set is now shipped.

## Known Stubs

None.

## Threat Flags

None — this plan introduced no new surface; it only reads and audits existing code.

---
*Phase: 23-manual-light-dark-override*
*Completed: 2026-08-17*
