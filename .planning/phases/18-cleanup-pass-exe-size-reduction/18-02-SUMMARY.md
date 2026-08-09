---
phase: 18-cleanup-pass-exe-size-reduction
plan: 02
subsystem: infra
tags: [windowsdisplayapi, ccd, cleanup, dead-code, monitor-control]

# Dependency graph
requires:
  - phase: 16-normal-mode-explicit-monitor-config
    provides: Explicit Normal-mode target application via ActivateMonitors/DeactivateMonitors, which made the snapshot-restore path unreachable in production
provides:
  - A WindowsMonitorController free of dead snapshot-restore code (Restore, RestoreViaReconstruction, _originalPathsCache, CopyOutputTechnology, AssignSource)
  - A durable knowledge-base record (ccd-topology-restore-findings) of the rig-discovered CCD constraints that lived only in the deleted code's doc comments
  - An IMonitorController interface with no Restore member
  - A trimmed WindowsMonitorControllerTests.cs covering only the surviving MergeAllMonitors/AnyRectanglesOverlap helpers
affects: [18-03, 18-04, 18-05, exe-size-reduction]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Confirm-dead-before-delete gate: grep for all call sites of a member before deleting it, and STOP if any unexpected production call site appears"
    - "Knowledge-preservation-before-deletion: append hard-won findings to .planning/debug/knowledge-base.md before removing the code that encoded them, using the same field structure as existing entries"

key-files:
  created: []
  modified:
    - .planning/debug/knowledge-base.md
    - src/RigToggle.Windows/WindowsMonitorController.cs
    - src/RigToggle.Core/Abstractions/IMonitorController.cs
    - src/RigToggle.Windows/AssemblyInfo.cs
    - src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs

key-decisions:
  - "Preserved five rig-discovered CCD findings (OutputTechnology backing-field patch, source-collision rule, source-ID renumbering, SDC_ALLOW_CHANGES silent-accept, reconstruction rules) in knowledge-base.md before deleting the code that documented them, per CLEANUP-01's explicit review-and-preserve-first requirement"
  - "Deleted _originalPathsCache field write site (line 277 inside the still-live DeactivateMonitors) as part of Task 2, not left behind — the write had no remaining reader after Restore/RestoreViaReconstruction were removed"

patterns-established: []

requirements-completed: [CLEANUP-01]

# Metrics
duration: ~25min
completed: 2026-08-09
---

# Phase 18 Plan 02: Preserve & Delete Dead Monitor-Restore Subsystem Summary

**Extracted five rig-discovered WindowsDisplayAPI CCD findings into a durable knowledge-base entry, then deleted the now-dead `WindowsMonitorController.Restore`/`RestoreViaReconstruction`/`CopyOutputTechnology`/`AssignSource` subsystem and its `IMonitorController.Restore` interface member, shrinking `WindowsMonitorController.cs` from 727 to 352 lines while keeping all 8 surviving unit tests green.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 3/3 completed
- **Files modified:** 5

## Accomplishments

- Preserved all five rig-hard-won CCD constraints (OutputTechnology backing-field patch, source-collision assignment rule, source-ID renumbering across a mutation boundary, `SDC_ALLOW_CHANGES` silent-accept-of-stale-array behavior, and the live-identity reconstruction rules) in `.planning/debug/knowledge-base.md` under a new `ccd-topology-restore-findings` entry, matching the existing `moza-foreground-focus` entry's field structure
- Deleted the entire dead monitor-restore subsystem: `Restore`, `RestoreViaReconstruction`, the `_originalPathsCache` field and its one write site (inside the still-live `DeactivateMonitors`), `CopyOutputTechnology`, `AssignSource` — `WindowsMonitorController.cs` dropped from 727 to 352 lines (51.6% reduction)
- Removed `IMonitorController.Restore` from the interface contract; `MergeAllMonitors` and `AnyRectanglesOverlap` (the two still-live internal helpers) survive untouched, with `DeactivateMonitors`' `_originalPathsCache`-free flow verified to still compile and use the same `currentPaths` local
- Trimmed `WindowsMonitorControllerTests.cs` from 14 to exactly 8 `[Fact]`s, removing the two `CopyOutputTechnology_*` and four `AssignSource_*` tests plus their two now-unused fake-object helpers (`CreateFakeTarget`, `FakeSource`), and removed the three WindowsDisplayAPI usings those helpers alone required
- Confirmed zero remaining `.Restore(` call sites anywhere in `src/` — the test file's header comment held the last reference and was rewritten as part of Task 3
- Full solution builds with 0 errors; `RigToggle.Windows.Tests` builds with 0 warnings/0 errors (not executed — dev-host limitation, no `Microsoft.WindowsDesktop.App` runtime); `RigToggle.Tests` runs green: Failed 0, Passed 85, Total 85

## Task Commits

Each task was committed atomically:

1. **Task 1: Preserve the rig-discovered CCD knowledge before deleting anything** - `76b3e85` (docs)
2. **Task 2: Delete the monitor restore subsystem and the two unreachable CCD helpers** - `6393180` (refactor)
3. **Task 3: Trim WindowsMonitorControllerTests to the surviving helpers only** - `2705a8f` (test)

_Note: this plan runs in worktree isolation — plan metadata (SUMMARY.md) is committed separately by the orchestrator after merge; STATE.md/ROADMAP.md are not touched by this agent._

## Confirm-Dead-Before-Delete Gate (verbatim)

```
$ grep -rn "\.Restore(" src --include="*.cs"
src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs:12:// Covers the two pieces of WindowsMonitorController.Restore()'s reconstruction logic
```
(one hit, inside a comment — zero production call sites, as expected; that comment was rewritten in Task 3)

```
$ grep -rn "CopyOutputTechnology\|AssignSource" src --include="*.cs"
src/RigToggle.Windows/AssemblyInfo.cs:3:// Exposes internal members (CopyOutputTechnology, AssignSource) to
src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs:25:    // constructed fake target ID that matches nothing real. AssignSource's tests below
src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs:35:    public void CopyOutputTechnology_DefaultsToOther_BeforePatch()
src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs:43:    public void CopyOutputTechnology_PatchesBackingField_ToRequestedValue()
src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs:47:        WindowsMonitorController.CopyOutputTechnology(target, DisplayConfigVideoOutputTechnology.DisplayPortExternal);
src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs:56:    public void AssignSource_ActiveTarget_KeepsItsOwnSource()
[... 6 more hits, all inside the six tests removed in Task 3 ...]
src/RigToggle.Windows/WindowsMonitorController.cs:681:    internal static void CopyOutputTechnology(PathTargetInfo target, DisplayConfigVideoOutputTechnology technology)
src/RigToggle.Windows/WindowsMonitorController.cs:696:    // Phase 05 code review WR-02: like CopyOutputTechnology above, NOT currently
src/RigToggle.Windows/WindowsMonitorController.cs:710:    internal static PathDisplaySource AssignSource(
```
(only the declarations, the AssemblyInfo naming comment, and their own tests — exactly as expected, so deletion proceeded)

## Before/After Metrics

| Metric | Before | After | Delta |
|---|---|---|---|
| `WindowsMonitorController.cs` line count | 727 | 352 | -375 (-51.6%) |
| `WindowsMonitorControllerTests.cs` `[Fact]` count | 14 | 8 | -6 |
| `_originalPathsCache` occurrences | 8 | 0 | -8 |
| `.Restore(` call sites in `src/` | 1 (WindowsMonitorController public method) | 0 | -1 |

## Final Build Output

```
$ dotnet build src/RigToggle.Windows/RigToggle.Windows.csproj -p:EnableWindowsTargeting=true
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj -p:EnableWindowsTargeting=true
Build succeeded.
    0 Warning(s)
    0 Error(s)
(built, not executed — dev-host limitation: Microsoft.WindowsDesktop.App runtime absent on this Linux dev host)

$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded.
    4 Warning(s)   [pre-existing xUnit1031 warnings in src/RigToggle.Tests/ToggleOrchestratorTests.cs, out of scope for this plan]
    0 Error(s)

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true
Passed! - Failed: 0, Passed: 85, Skipped: 0, Total: 85
```

## Files Created/Modified

- `.planning/debug/knowledge-base.md` - Appended `ccd-topology-restore-findings` entry recording five rig-discovered WindowsDisplayAPI CCD constraints, before the source code encoding them was deleted
- `src/RigToggle.Windows/WindowsMonitorController.cs` - Deleted `Restore`, `RestoreViaReconstruction`, `_originalPathsCache` field + write site, `CopyOutputTechnology`, `AssignSource`; rewrote class doc comment and `AnyRectanglesOverlap`'s leading comment to describe only what remains
- `src/RigToggle.Core/Abstractions/IMonitorController.cs` - Removed `Restore(MonitorState previousState)` member and updated the interface's class doc summary
- `src/RigToggle.Windows/AssemblyInfo.cs` - Kept `[assembly: InternalsVisibleTo("RigToggle.Windows.Tests")]` unchanged; rewrote the naming comment to point at `MergeAllMonitors`/`AnyRectanglesOverlap` (the two members it now actually exposes) and repointed provenance at 06-RESEARCH.md/quick task 260728-qj1
- `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` - Removed 6 `[Fact]`s + 2 helpers (`CreateFakeTarget`, `FakeSource`) covering the deleted members; removed 3 now-unused `WindowsDisplayAPI` usings; rewrote file header comment

## Decisions Made

- Populated the knowledge-base entry with concrete, actionable detail (exact field-name string, exact flag name, exact rule text) rather than a prose summary, per the plan's acceptance criteria grepping for specific identifiers — this makes the entry genuinely useful to a future implementer, not just a checkbox
- Left stray in-comment mentions of "Restore()" inside `GetAllMonitors`'s and `ActivateMonitors`'s doc comments untouched (e.g. "the same 'inactive-path fields are unreliable' landmine already worked around elsewhere in this file (Restore()/DeactivateMonitors())") — the plan's own acceptance criteria explicitly excludes comment lines from the "no `Restore` identifier survives" check, and rewriting every historical cross-reference was out of this plan's stated scope (only the class doc summary and `AnyRectanglesOverlap`'s comment were called out for rewrite)

## Deviations from Plan

None - plan executed exactly as written. The confirm-dead-before-delete gate in Task 2 found exactly the expected hits (zero production `.Restore(` calls, `CopyOutputTechnology`/`AssignSource` referenced only in their own declarations, the `AssemblyInfo.cs` naming comment, and their own tests), so no STOP-and-report branch was triggered.

One self-correction during Task 2: the first draft of the rewritten class doc comment accidentally reintroduced the string `RestoreViaReconstruction` inside its own explanatory sentence ("the now-dead restore subsystem (Restore, RestoreViaReconstruction, ...)"), which would have failed the acceptance criterion `grep -c "RestoreViaReconstruction\|CopyOutputTechnology\|AssignSource" ... prints 0`. Caught by running the acceptance-criteria greps before committing; fixed by rephrasing the sentence to describe the subsystem without naming the deleted method, then re-verified all criteria passed before the Task 2 commit.

## Known Stubs

None.

## Threat Flags

None — this plan only deletes code and adds a documentation entry; no new network endpoints, auth paths, file access patterns, or schema changes were introduced. See the plan's own `<threat_model>` (T-18-02-01 through T-18-02-04, T-18-02-SC) for the pre-declared threat register, all dispositioned `mitigate`/`accept` and satisfied by this execution as described in that table.

## Self-Check

To be appended after file-existence and commit verification.
