---
phase: 19-monitor-tile-dashboard-monitorpanelform-retirement
plan: 03
subsystem: ui
tags: [winforms, monitor-tile, ccd, mutation, identify-overlay]

requires:
  - phase: 19-02
    provides: MainForm dashboard scaffold, RefreshMonitorTiles/LayoutDashboard/ApplyDashboardTheming, OnTileAction/BtnIdentify_Click stubs, canonical _lastKnownMonitors ordering
provides:
  - MainForm.OnTileAction — live tile-click mutation (lease -> confirmation gate -> stale-path re-validation -> IMonitorController call -> refresh)
  - MainForm.TryAcquireMonitorAccess — shared lease-acquisition helper for tile actions
  - MainForm.BtnIdentify_Click — ported Identify overlay handler, numbered from the canonical tile order
affects: [19-04 (MonitorPanelForm retirement — can now delete the panel these methods replace), 19-05 (rig verification)]

tech-stack:
  added: []
  patterns:
    - "Exclusive-access lease acquired BEFORE a nested-message-loop dialog (ShowDialog), not just around the mutation call, so a hotkey-triggered toggle cannot start mid-dialog"
    - "Post-dialog stale-identity re-validation against the live canonical list before acting on a pre-dialog snapshot"
    - "Ordinal counter that increments even on a skipped iteration, so two independently-rendered UI elements (tiles, overlays) stay numbered in agreement"

key-files:
  created: []
  modified:
    - src/RigToggle.App/MainForm.cs

decisions:
  - "Followed the plan's literal action text (naming ToggleService.ToggleToRigMode/ToggleToNormalMode and BtnToggle_Click in doc comments) even though three of the plan's own acceptance-criteria greps then report non-zero counts against those comments — see Issues Encountered."

requirements-completed: [TILE-02, TILE-03, TILE-04]

duration: 25min
completed: 2026-08-09
---

# Phase 19 Plan 03: Monitor-Tile Mutation & Identify Summary

**Tile clicks now mutate exactly one monitor through the same `IMonitorController` methods both toggle directions already use, gated by a lease-held-across-dialog and a disable-only confirmation prompt, while a ported Identify handler numbers its overlays from the same canonical list the tiles use.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-09 (immediately after a `git merge master` to bring this worktree's branch up to date with Plan 19-01/19-02's landed code and the phase's planning artifacts, which had not yet reached this worktree)
- **Completed:** 2026-08-09
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments

- `TryAcquireMonitorAccess()` added: acquires `ToggleOrchestrator.BeginExclusiveMonitorAccess()`, classifying a busy rejection as `MessageBoxIcon.Information` (an expected condition), matching `BtnToggle_Click`'s existing classification.
- `OnTileAction(MonitorTile tile)` filled in as a line-for-line port of `MonitorPanelForm.DisableMonitor`/`EnableMonitor`: resolves the tile's monitor by `DevicePath`, takes the lease before opening `MonitorConfirmDialog` (disable-only, always-empty enable-names list), re-validates the device path against `_lastKnownMonitors` after the dialog closes (WR-03, guards against a hotplug-triggered refresh racing the nested dialog loop), calls `DeactivateMonitors`/`ActivateMonitors` with a single-element `HashSet<string>`, and refreshes the tile row in a `finally` regardless of outcome. No zero-survivors pre-check was added anywhere — the DISPLAY-12 guard audit (four grep patterns) returned `0` matches.
- `BtnIdentify_Click` filled in as a port of `MonitorPanelForm.BtnIdentify_Click`, with the two deliberate changes the plan called for: it iterates `_lastKnownMonitors` (the same canonical order the tiles are numbered from) instead of now-deleted `DataGridView` rows, and its ordinal counter increments on both skip guards (stale/missing snapshot, degenerate zero-size resolution) as well as the successful path — so overlay number N and tile number N always name the same physical monitor even when a monitor is skipped.
- `BtnToggle_Click`, `PerformBackgroundToggle`, `HandleHotkeyToggle`, and `TrayToggleMenuItem_Click` were not touched by either task's diff.

## Task Commits

1. **Task 1: OnTileAction — lease, confirmation gate, mutation, refresh (TILE-02, TILE-03)** - `daa6b6b` (feat)
2. **Task 2: BtnIdentify_Click — ported Identify with tile-consistent numbering (TILE-04)** - `34bd658` (feat)

## Files Created/Modified

- `src/RigToggle.App/MainForm.cs` - Adds `TryAcquireMonitorAccess()`, fills in `OnTileAction(MonitorTile tile)` and `BtnIdentify_Click(object?, EventArgs)`, replacing both Plan 19-02 stubs. No other method touched.

## Divergences from `MonitorPanelForm` (exactly three, as the plan specified)

1. **Entry point** — a `MonitorTile.DevicePath`/`MonitorTile.ActionRequested` click instead of a `DataGridView` cell click; monitor resolution is by `DevicePath` lookup into `_lastKnownMonitors` instead of a grid row's `Tag`.
2. **Refresh target** — `RefreshMonitorTiles()` instead of `PopulateMonitorGrid()`, in both the WR-03 stale-path branch and the mutation `finally`.
3. **Identify skip-increment** — the retired panel's `BtnIdentify_Click` did NOT increment its counter when a monitor had no `CaptureState()` snapshot or a degenerate resolution; the ported version does, on both skip guards, so overlay numbering stays in lockstep with tile numbering even when a monitor is OS-disabled.

## Build/Test Output

- `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` — **0 Error(s)** (4 pre-existing `xUnit1031` warnings, unrelated to this plan's files)
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true` — **Passed: 81, Failed: 0, Total: 81** (matches Plan 19-02's baseline, no regression)
- DISPLAY-12 no-duplicate-guard audit (four grep patterns against `MainForm.cs`, non-comment lines only): all four returned `0`.
- Static equivalence audit: `DeactivateMonitors` now appears 4 times in `MainForm.cs` (`BtnToggle_Click`'s pre-existing doc-comment mention, the two doc-comment mentions this plan added, and the one new executable call in `OnTileAction`) and 8 times in `ToggleService.cs` (unchanged) — the tile path is a third caller of the one guarded adapter method, not a second implementation of the guard.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Worktree was 20+ commits behind `master`, missing Phase 19's planning docs and Plan 19-01/19-02's landed code**
- **Found during:** Initial file-read step, before Task 1 — `19-03-PLAN.md` and the entire `.planning/phases/19-*` directory did not exist in this worktree's checkout, and `src/RigToggle.App/MainForm.cs` did not yet contain the `OnTileAction`/`BtnIdentify_Click` stubs Plan 19-02 was supposed to have left behind.
- **Issue:** This worktree branch was created before Phase 19 was planned/executed on `master`, and had not picked up the subsequent worktree-merge commits landing Plans 19-01 and 19-02.
- **Fix:** Ran `git merge master --no-edit` (a genuine three-way merge, not a fast-forward, since this worktree's branch had its own commit history) to bring in the phase's planning artifacts and both prior plans' code. The merge completed with no conflicts; `git status --short` was empty afterward.
- **Files modified:** None directly (bulk merge of pre-existing upstream commits, not a new code change).
- **Commit:** N/A (merge commit, not a task commit — `HEAD` moved from `ec29345` to `47b8119` via merge)

### Not fixed (documented, not code defects)

**2. Three of the plan's own literal acceptance-criteria greps report non-zero counts against text the plan's own action instructions explicitly required**
- The plan's Task 1 action text explicitly instructs: "Comment directly above the call, naming `ToggleService.ToggleToRigMode` and `ToggleService.ToggleToNormalMode` as the other callers of this exact method" and "Doc-comment [`TryAcquireMonitorAccess`] with the full Pitfall 3 rationale... it is NOT redundant with `BtnToggle_Click`'s reliance on `ToggleOrchestrator`'s internal `_busy` guard... matching how `BtnToggle_Click` already classifies it." Both comments were written exactly as instructed.
- Three of the plan's acceptance-criteria greps then count those same literal strings and expect `0`:
  - `awk '/private void OnTileAction/,/^        }$/' ... | grep -cE 'ToggleToRigMode|ToggleToNormalMode|ToggleResult|ToggleService|IModeStore'` returns `2` (both from the required `ToggleService.ToggleToRigMode`/`ToggleToNormalMode` comment), not `0`.
  - `grep -c 'BeginExclusiveMonitorAccess()' src/RigToggle.App/MainForm.cs` returns `2`, not `1` — the second match is a pre-existing doc-comment mention in `OpenMonitorPanel()` (`"...Plan 17-01's BeginExclusiveMonitorAccess() lease..."`), present before this plan's diff and unrelated to it.
  - `git diff -U0 src/RigToggle.App/MainForm.cs | grep -c 'BtnToggle_Click'` returns `2`, not `0` — both are new comment lines this plan's own action text required, not a modification to `BtnToggle_Click`'s method body (verified: `BtnToggle_Click`'s own line range is untouched in the diff).
- Verified in each case that the actual behavioral intent of the check (no toggle-pipeline coupling, one lease-acquisition call site, `BtnToggle_Click`'s method body unchanged) is satisfied — only the literal grep, which does not exempt comments or pre-existing text, reports otherwise. This is the same class of plan-authoring gap Plan 19-02's summary documented for its own `GetActiveMonitors`/`MonitorConfirmDialog` greps. No code change was made in response; documenting here as a clarification, matching that precedent, rather than rewording plan-mandated documentation to game a grep.

---

**Total deviations:** 1 blocking worktree-sync fix (pre-Task 1), 3 documented grep/comment false positives (no code impact).
**Impact on plan:** No scope change, no behavior change. All functional acceptance criteria (build, tests, mutation-call-count, lease-ordering, DISPLAY-12 audit, confirm-dialog/save counts, stale-notice copy, Identify iteration/numbering/lease-absence/GDI-absence/stub-removal) passed exactly as specified.

## Issues Encountered

None beyond the documented grep false positives above.

## Next Phase Readiness

- Plan 19-04 (MonitorPanelForm retirement) can proceed: `MainForm`'s tile dashboard is now fully live (population, layout, theming, hotplug refresh, click mutation, and Identify), so `MonitorPanelForm.cs`, `trayMonitorsMenuItem`, `TrayMonitorsMenuItem_Click`, `OpenMonitorPanel()`, `_monitorPanelForm`, and `_monitorPanelFormFactory` are now safe to delete — none of Plan 19-03's diff touched them, and they remain present and untouched per this plan's hard constraint 7.
- Real CCD mutation behavior, the hotkey-during-dialog race, and overlay-to-tile number agreement on physical hardware remain unverified in this Linux build environment — deferred to Plan 19-05's rig checkpoint, per the plan's own verification note.

---
*Phase: 19-monitor-tile-dashboard-monitorpanelform-retirement*
*Completed: 2026-08-09*
