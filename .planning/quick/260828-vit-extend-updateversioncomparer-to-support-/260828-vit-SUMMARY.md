---
phase: quick-260828-vit
plan: 01
subsystem: update
tags: [semver, version-comparison, github-releases, ci-cd]

requires: []
provides:
  - Three-component (Major.Minor.Patch) UpdateVersionComparer, backward-compatible with two-segment historical tags
  - Patch-aware UpdateOrchestrator skip-tracking round trip
  - Published GitHub release v2.2.1 with exe + sha256 assets

affects: [26-auto-update]

actuals:
  tokens: 5927
  tasks: 2
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Version comparison always compares raw parsed integer components (major/minor/patch) directly, never System.Version.CompareTo, to avoid the Build==-1-vs-explicit-0 false-negative trap"

key-files:
  created: []
  modified:
    - src/RigToggle.Core/UpdateVersionComparer.cs
    - src/RigToggle.Core/UpdateOrchestrator.cs
    - src/RigToggle.Tests/UpdateVersionComparerTests.cs
    - src/RigToggle.Tests/UpdateOrchestratorTests.cs
    - src/RigToggle.App/RigToggle.App.csproj

key-decisions:
  - "Task 1's comparer/orchestrator/test changes and Task 2's version bump were committed together in a single commit, per the plan's explicit action instructions (commit Task 1 and the bump together), rather than as two separate task commits"
  - "Pushed the tagged commit to origin/master via `git push origin HEAD:master` rather than `git push origin master`, since this worktree-isolated agent's local `master` ref is checked out read-only in the main worktree at an older commit — this fast-forwards origin's master directly from the worktree branch tip without touching the protected local `master` ref"

patterns-established:
  - "Two-segment tags (vX.Y) parse as patch 0 for backward compatibility with git history predating the three-component switch"

requirements-completed: [UPDATE-01, UPDATE-02]

coverage:
  - id: D1
    description: "UpdateVersionComparer.TryParseTag extended with a patch out-parameter; two-segment tags parse as patch 0, three-segment tags parse their patch, unparseable third segments fail"
    requirement: "UPDATE-02"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateVersionComparerTests.cs#TryParseTag_ValidTag_ParsesMajorMinorPatch"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateVersionComparerTests.cs#TryParseTag_InvalidTag_ReturnsFalse"
        status: pass
    human_judgment: false
  - id: D2
    description: "UpdateVersionComparer.IsNewer orders major-then-minor-then-patch off raw integers, with a two-component running Version's Build==-1 normalized via Math.Max(...,0) so it never misreads as older than a tag's explicit patch 0"
    requirement: "UPDATE-02"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateVersionComparerTests.cs#IsNewer_RunningVersusTag_UsesNumericMajorMinorPatchOrdering"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateVersionComparerTests.cs#IsNewer_TwoComponentRunningVersion_BuildMinusOneNormalizedToZero_IsNotNewer"
        status: pass
    human_judgment: false
  - id: D3
    description: "UpdateOrchestrator's skip-tracking round trip carries the patch component, so a persisted skip of a two-segment tag (e.g. v2.2) no longer suppresses a later point release (e.g. v2.2.1) of the same X.Y"
    requirement: "UPDATE-02"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAsync_HonourSkippedVersion_PointReleaseStrictlyNewerThanSkippedMinor_StillInvokesConfirm"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAsync_HonourSkippedVersion_LatestTagEqualsSkippedPatchVersion_ReturnsSkipped_ConfirmNeverInvoked"
        status: pass
    human_judgment: false
  - id: D4
    description: "RigToggle.App stamped 2.2.1, comparer+bump committed and pushed to master, annotated tag v2.2.1 pushed to origin, Release workflow run completed successfully, and a GitHub release v2.2.1 is published carrying RigToggle.App.exe and RigToggle.App.exe.sha256 — unblocking Phase 26's real-hardware auto-update UAT"
    requirement: "UPDATE-01"
    verification:
      - kind: other
        ref: "gh run watch --exit-status 33218597527 (release.yml, completed successfully in 2m4s)"
        status: pass
      - kind: other
        ref: "gh release view v2.2.1 --json assets (2 assets: RigToggle.App.exe, RigToggle.App.exe.sha256)"
        status: pass
    human_judgment: true
    rationale: "Publishing a real GitHub release and confirming its download works on the actual rig is the whole point of unblocking Phase 26's hardware UAT — the operator should pull the exe and confirm it downloads/runs before relying on it for the update-path UAT."

duration: 10min
completed: 2026-08-28
status: complete
---

# Quick Task 260828-vit: Three-Component Semver + v2.2.1 Release Summary

**Extended `UpdateVersionComparer`/`UpdateOrchestrator` to three-component (Major.Minor.Patch) semver with full backward compatibility for two-segment historical tags, then cut and published the real `v2.2.1` GitHub release that unblocks Phase 26's auto-update rig UAT.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-08-28T22:49:03Z
- **Completed:** 2026-08-28T22:59:18Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- `UpdateVersionComparer.TryParseTag` now yields a patch component: two-segment tags (v1.0...v2.2) parse as patch 0 for backward compatibility, three-segment tags parse their real patch, and a present-but-unparseable third segment (`"v2.2.x"`) is a parse failure rather than a silent 0
- `IsNewer` orders major-then-minor-then-patch off raw parsed integers on both sides (never `System.Version.CompareTo`), with the running side's `Build` normalized via `Math.Max(runningVersion.Build, 0)` so a two-component running `Version` (`Build == -1`) never misreads a tag's explicit patch 0 as greater
- `UpdateOrchestrator`'s skip-tracking round trip (`TryParseTag` -> `new Version(major, minor, patch)` -> `IsNewer`) now carries the patch through, fixing the regression where a skip of a two-segment tag like `v2.2` would swallow every later point release of that same minor (e.g. `v2.2.1`)
- `UpdateOrchestrator`'s running-version text is now three-component (`"2.2.1"` instead of `"2.2"`), matching the actual stamped assembly version
- `RigToggle.App` bumped to `<Version>2.2.1</Version>`; the commit was pushed to `master` and the annotated tag `v2.2.1` pushed to `origin`
- Release workflow run (`33218597527`) completed successfully on `windows-latest` in 2m4s, including the solution-wide build/test covering the Windows-only projects
- GitHub release `v2.2.1` published with both `RigToggle.App.exe` and `RigToggle.App.exe.sha256`: https://github.com/bpivk/monitor-toggle/releases/tag/v2.2.1

## Task Commits

Task 1 (comparer/orchestrator extension, TDD RED->GREEN) and Task 2's version bump were committed together in a single commit, per the plan's explicit instruction to commit them jointly:

1. **Task 1 + Task 2 (bump):** `8087afd` — `feat(update): three-component semver comparison; bump to 2.2.1`

**Tag:** `v2.2.1` (annotated) pushed to `origin` at commit `8087afd`.

**Plan metadata:** commit pending (orchestrator handles the docs commit).

_Note: This plan's Task 1 was `tdd="true"`. RED phase (compile-error-first, since a 4th `out` param is a breaking signature change) was verified via a failing `dotnet test` build before any production code changed; GREEN was verified via 223/223 passing (up from 209) after the comparer/orchestrator changes landed. No separate `test(...)`/`feat(...)` commits were made — the plan explicitly asked for a single combined commit spanning Task 1 and Task 2's bump._

## Files Created/Modified
- `src/RigToggle.Core/UpdateVersionComparer.cs` - Three-component parse (`TryParseTag` gains `out int patch`) and compare (`IsNewer` compares major/minor/patch off raw integers, normalizing `Build == -1`); class + method doc comments rewritten to describe the new scheme
- `src/RigToggle.Core/UpdateOrchestrator.cs` - Skip-tracking block reconstructs `new Version(skippedMajor, skippedMinor, skippedPatch)`; running-version text renders three components via `Math.Max(_runningVersion.Build, 0)`
- `src/RigToggle.Tests/UpdateVersionComparerTests.cs` - Extended `IsNewer` theory with 7 patch-level rows, extended `TryParseTag` theories with third-segment rows and the `"v2.2.x"` failure case, added a dedicated two-component `Build == -1` guard theory; all pre-existing rows retained
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` - Added the point-release regression test (skip `"v2.2"`, release `"v2.2.1"` -> confirm invoked) and its equal-patch sibling (skip `"v2.2.1"`, release `"v2.2.1"` -> still suppressed)
- `src/RigToggle.App/RigToggle.App.csproj` - `<Version>2.2</Version>` -> `<Version>2.2.1</Version>`, comment's trailing sentence updated to match

## Decisions Made
- Followed the plan's explicit instruction to commit Task 1's comparer/orchestrator/test changes together with Task 2's version bump in a single commit (`feat(update): three-component semver comparison; bump to 2.2.1`), rather than the default one-commit-per-task pattern, since the plan's own `<action>` text says "Commit Task 1 and this bump" as one step.
- Pushed via `git push origin HEAD:master` instead of `git push origin master`: this agent runs in a git worktree whose local `master` ref is checked out (read-only, at an older commit) in the main working copy, so pushing the literal local `master` ref would have pushed a stale commit. `HEAD:master` pushes the worktree branch's actual tip, confirmed as a clean fast-forward of `origin/master` before pushing (`git merge-base --is-ancestor origin/master HEAD`).

## Deviations from Plan

**One auto-fixed doc-accuracy issue found during self-check, no scope creep:**

**1. [Rule 2 - stale doc comment] Updated `UpdateCheckResult.RunningVersionText` XML doc**
- **Found during:** Task 1, post-implementation grep sweep for stale two-component claims (per the plan's own verification item 3: "No doc comment... still describes a two-component-only scheme")
- **Issue:** The `<param name="RunningVersionText">` doc comment on `UpdateCheckResult` still said `"the currently-running build's Major.Minor version text (e.g. \"2.2\")"` after the running-version text itself became three-component
- **Fix:** Updated the comment to `"Major.Minor.Patch version text (e.g. \"2.2.1\")"`
- **Files modified:** src/RigToggle.Core/UpdateOrchestrator.cs
- **Verification:** `dotnet test` re-run, still 223/223 passing; grep confirms no remaining stale two-component-only claims
- **Committed in:** 8087afd (same commit as Task 1/2)

---

**Total deviations:** 1 auto-fixed (doc accuracy, Rule 2)
**Impact on plan:** No functional scope creep — pure documentation-accuracy fix required by the plan's own verification criteria.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. The operator can now download `RigToggle.App.exe` from https://github.com/bpivk/monitor-toggle/releases/tag/v2.2.1 onto the rig to exercise Phase 26's auto-update UAT (the running build, whatever it is, should now see `v2.2.1` as a newer release via `UpdateVersionComparer.IsNewer`).

## Next Phase Readiness
- Phase 26's auto-update rig UAT is unblocked: a real release (`v2.2.1`) now exists that is newer than any previously-installed build, with both the exe and its sha256 sidecar published.
- The three-component comparer and orchestrator skip-tracking fix are unit-tested (223/223 passing, 14 new test cases) and ready for the tag scheme going forward (vX.Y.Z), while every historical two-segment tag (v1.0...v2.2) still compares exactly as before.
- No blockers.

## Self-Check: PASSED

- FOUND: src/RigToggle.Core/UpdateVersionComparer.cs
- FOUND: src/RigToggle.App/RigToggle.App.csproj
- FOUND: commit 8087afd in git log
- FOUND: refs/tags/v2.2.1 on origin (a9087ec...)
- FOUND: GitHub release v2.2.1 (https://github.com/bpivk/monitor-toggle/releases/tag/v2.2.1)

---
*Phase: quick-260828-vit*
*Completed: 2026-08-28*
