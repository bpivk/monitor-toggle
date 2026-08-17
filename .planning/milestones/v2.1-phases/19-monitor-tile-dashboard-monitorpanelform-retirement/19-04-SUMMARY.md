---
phase: 19-monitor-tile-dashboard-monitorpanelform-retirement
plan: 04
subsystem: ui
tags: [winforms, monitor-tile, deletion, composition-root]

requires:
  - phase: 19-03
    provides: MainForm.OnTileAction/BtnIdentify_Click fully live (tile dashboard has absorbed all of MonitorPanelForm's capability)
provides:
  - MonitorPanelForm and both its entry points (Monitors button, tray Monitors item) fully deleted
  - MainForm five-argument constructor (orchestrator, settingsStore, monitorController, settingsFormFactory, themeProvider)
  - Composition root five-argument MainForm construction, no panel factory
affects: [19-05 (rig verification — must confirm tray menu has three items and no path can open a standalone panel)]

tech-stack:
  added: []
  patterns:
    - "Delete, do not deprecate — no [Obsolete] shim, no renamed-but-kept file, no commented-out block"
    - "Split the deletion into two commits: strip the caller-side coupling first (Task 1, deliberately leaves the solution non-compiling), then delete the callee and fix the composition root (Task 2), gating the build/test check on the second commit only — mirrors Plan 17-02's documented intermediate-build-state pattern"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/MonitorIdentifyOverlay.cs
  deleted:
    - src/RigToggle.App/MonitorPanelForm.cs
    - src/RigToggle.App/MonitorPanelForm.Designer.cs

decisions:
  - "Reworded five pre-existing doc-comment mentions of the literal string 'MonitorPanelForm' in MainForm.cs/MainForm.Designer.cs (none of which were in this plan's explicit removal-target list) to satisfy the plan's own zero-occurrence grep acceptance criteria, without losing the historical context those comments convey — see Deviations."

requirements-completed: [TILE-07]

duration: 20min
completed: 2026-08-09
---

# Phase 19 Plan 04: MonitorPanelForm Retirement Summary

**`MonitorPanelForm.cs` and `MonitorPanelForm.Designer.cs` are deleted outright (git rm, no shim), both entry points (Monitors button — already gone since Plan 19-02 — and the tray Monitors item, removed here) are gone, `MainForm` and the composition root both construct with five arguments instead of six, and the solution builds and tests clean at the same 81/81 baseline Plan 19-03 left.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-09 (worktree was already synced to `master` at the start of this session — Plan 19-03's fast-forward/merge fix carried forward, so `19-04-PLAN.md` and the phase's planning docs were present from the first Read)
- **Completed:** 2026-08-09
- **Tasks:** 2
- **Files modified:** 4, deleted: 2

## Accomplishments

- **Task 1** removed every trace of the panel's caller-side coupling from `MainForm`/`MainForm.Designer.cs`: the tray `trayMonitorsMenuItem` (instantiation, configuration block, `AddRange` entry, field declaration), the `_monitorPanelFormFactory`/`_monitorPanelForm` fields and their 17-03/PANEL-03 doc comment, the `monitorPanelFormFactory` constructor parameter, `TrayMonitorsMenuItem_Click`, and `OpenMonitorPanel()` (including its four-point divergence doc comment). The tray menu's `AddRange` order comment was updated to the reverted three-item order (Switch mode → Settings → separator → Exit). This task deliberately left the solution non-compiling — `Program.cs` still passed six arguments — matching the plan's own documented Plan 17-02 intermediate-build-state precedent.
- **Task 2** deleted `MonitorPanelForm.cs`/`MonitorPanelForm.Designer.cs` via `git rm`, removed the `MonitorPanelFormFactory` local function and its 17-03/T-17-12 doc comment from `Program.cs`, changed the `MainForm` construction call to the five-argument form, and updated `MonitorIdentifyOverlay.cs`'s stale class doc comment to name its current caller (`MainForm`'s Identify action, TILE-04) instead of the deleted panel. Build and test gate run at the end of this task: 0 errors, 81/81 tests passing.
- Reused collaborators (`MonitorConfirmDialog.cs`, `MonitorIdentifyOverlay.cs`, `ThemeApplier.ThemeMonitorGrid`) all survived untouched in behavior — `ThemeMonitorGrid` still has exactly 4 callers in `SettingsForm.cs`, confirmed by direct grep.
- No `.csproj` changes were needed (default SDK globbing, no explicit `Compile` items for the deleted files) and no `PackageReference` count changed (`grep -c 'PackageReference' RigToggle.App.csproj` → 0, unchanged).

## Task Commits

1. **Task 1: MainForm — remove the tray Monitors entry, the panel factory, and the open helper** - `4f09bcd` (feat)
2. **Task 2: Delete MonitorPanelForm, unwire the composition root, and gate on a clean build** - `1efc08a` (feat)

## Files Created/Modified

- `src/RigToggle.App/MainForm.Designer.cs` — removed `trayMonitorsMenuItem` (instantiation, config block, `AddRange` entry, field), updated the tray-order comment.
- `src/RigToggle.App/MainForm.cs` — removed the panel factory/cached-instance fields, the constructor parameter, `TrayMonitorsMenuItem_Click`, `OpenMonitorPanel()`; updated the class doc comment for TILE-07; reworded five stray doc-comment mentions of `MonitorPanelForm` (see Decisions/Deviations).
- `src/RigToggle.App/Program.cs` — removed `MonitorPanelFormFactory` and its doc comment; `MainForm` construction is now five-argument.
- `src/RigToggle.App/MonitorIdentifyOverlay.cs` — updated the class doc comment to name `MainForm` as the current caller.
- `src/RigToggle.App/MonitorPanelForm.cs` (deleted, 406 lines) — the standalone panel form.
- `src/RigToggle.App/MonitorPanelForm.Designer.cs` (deleted, 179 lines) — its designer-generated layout.

Combined diff across both task commits: `6 files changed, 33 insertions(+), 683 deletions(-)` — matches the plan's own `files_modified` list exactly (4 modifications, 2 deletions).

## Build/Test Output

- `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` — **0 Error(s)**, 4 pre-existing `xUnit1031` warnings (same baseline as Plan 19-03, unrelated to this plan's files).
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` — **Passed: 81, Failed: 0, Total: 81** (matches Plan 19-03's baseline exactly, no regression).
- Solution-wide executable-reference sweep: `grep -rn 'MonitorPanelForm' src/ --include=*.cs --include=*.csproj | grep -vE ':\s*(//|\*|/\*)' | wc -l` → `0`.
- `git status --porcelain` after both commits: empty (clean working tree, no untracked leftovers).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Documentation gap] Task 1's own acceptance criteria required zero `MonitorPanelForm` occurrences in `MainForm.cs`/`MainForm.Designer.cs`, but five pre-existing doc-comment mentions (not in the plan's explicit removal-target list) survived a literal read of the interfaces block**
- **Found during:** Task 1 verification, after making the explicitly-listed removals.
- **Issue:** The plan's `<interfaces>` block enumerated exact removal targets (specific fields, the constructor parameter, two methods, two doc-comment paragraphs) but did not mention five other pre-existing doc-comment lines in `MainForm.cs`/`MainForm.Designer.cs` that name `MonitorPanelForm` as historical context — three added by Plan 19-03 documenting which retired-panel method each tile handler ports from, one in the Dispose(bool)-adjacent comment contrasting MainForm's always-on subscription with a closable form's pattern, and one in the `btnIdentify` Designer comment. The acceptance criteria's literal `grep -c 'MonitorPanelForm' ... ` = 0 check would fail against these.
- **Fix:** Reworded each of the five comments to preserve their rationale (which retired method a handler ports from, why the pattern differs) while replacing the literal type name with a generic descriptor ("the retired standalone panel", "a closable Form"). No behavioral or structural code change — comments only.
- **Files modified:** `src/RigToggle.App/MainForm.cs`, `src/RigToggle.App/MainForm.Designer.cs` (both already in the plan's `files_modified` list; no additional files touched).
- **Commit:** `4f09bcd` (folded into Task 1's commit, since these were resolved before that commit was made).

### Not fixed (documented, not code defects)

**2. Acceptance criterion `grep -c 'Monitors' src/RigToggle.App/MainForm.Designer.cs` outputs `0` is unsatisfiable without deleting required Plan 19-02 functionality**
- The plan's Task 1 acceptance criteria include a literal `grep -c 'Monitors'` = 0 check against `MainForm.Designer.cs`. After removing every panel-related occurrence (including one I authored myself, reworded to avoid contributing an avoidable match), 12 occurrences remain, all legitimate and required: `lblNoMonitors` (the Plan 19-02 empty-state label — its field name, `Name`/`Text`/`Location`/`Size` assignments, and `Controls.Add` call) and one `GetAllMonitors()` reference inside its own explanatory comment.
- Hard constraint 2 of this plan explicitly forbids touching anything Plan 19-02/19-03 added to the tile dashboard, and `lblNoMonitors` is exactly that — the empty-state control shown when `GetAllMonitors()` returns zero rows. Deleting or renaming it to satisfy this grep would violate hard constraint 3 ("Do NOT change any tile, layout, mutation, Identify, hotplug, or theming behaviour in this plan") and break the dashboard's documented empty state.
- This is the same class of plan-authoring gap Plan 19-03's summary documented for its own acceptance-criteria greps (literal string checks that don't distinguish "the panel-related occurrence this task should remove" from "an unrelated, required occurrence of the same substring"). Verified the actual behavioral intent (no standalone-panel-Monitors coupling survives) is satisfied — every other Task 1 acceptance criterion checking a more specific string (`trayMonitorsMenuItem`, `TrayMonitorsMenuItem_Click`, `MonitorPanelForm`, `Func<MonitorPanelForm>`) passed at exactly the required count. No code change was made in response to this one broad literal check.

---

**Total deviations:** 1 auto-fixed (comment rewording to meet a literal grep, no behavior change), 1 documented unsatisfiable literal acceptance criterion (no code impact, required functionality preserved).
**Impact on plan:** No scope change, no behavior change. All functional acceptance criteria (build, tests, deletion, zero executable references, composition-root arity, reused-collaborator survival, no Obsolete shim, no package delta) passed exactly as specified.

## Issues Encountered

None beyond the two documented deviations above.

## Next Phase Readiness

- Plan 19-05 (rig verification) can proceed: the tray context menu is back to `Switch to Rig/Normal Mode → Settings → separator → Exit`, no button or menu item anywhere can open a standalone panel, `MainForm`'s constructor and the composition root both use five dependencies, and the solution builds/tests clean.
- Runtime confirmation that the tray menu genuinely renders three items and that no path can still open a standalone panel is a rig observation, deferred to Plan 19-05 per this plan's own `<verification>` note — unverifiable in this Linux build environment.

---
*Phase: 19-monitor-tile-dashboard-monitorpanelform-retirement*
*Completed: 2026-08-09*

## Self-Check: PASSED

- FOUND: src/RigToggle.App/MainForm.cs
- FOUND: src/RigToggle.App/MainForm.Designer.cs
- FOUND: src/RigToggle.App/Program.cs
- FOUND: src/RigToggle.App/MonitorIdentifyOverlay.cs
- CONFIRMED DELETED: src/RigToggle.App/MonitorPanelForm.cs
- CONFIRMED DELETED: src/RigToggle.App/MonitorPanelForm.Designer.cs
- FOUND commit: 4f09bcd (Task 1 — tray/factory/helper removal)
- FOUND commit: 1efc08a (Task 2 — panel deletion, composition root, build/test gate)
