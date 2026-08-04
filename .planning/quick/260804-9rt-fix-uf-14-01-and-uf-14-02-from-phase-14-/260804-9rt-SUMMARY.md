---
phase: quick-260804-9rt
plan: 01
subsystem: infra
tags: [github-actions, ci, cd, security-hardening, least-privilege]

# Dependency graph
requires:
  - phase: 14-readme-release-documentation
    provides: 14-SECURITY.md audit findings UF-14-01 (build.yml missing explicit permissions) and UF-14-02 (release.yml publishes without a build/test gate)
provides:
  - "build.yml declares explicit least-privilege `permissions: contents: read`"
  - "release.yml runs restore -> build -> test before publish, mirroring build.yml"
affects: [ci, cd, release-workflow]

# Tech tracking
tech-stack:
  added: []
  patterns: ["CI/CD workflows declare explicit least-privilege GITHUB_TOKEN permissions", "Release workflows gate publish behind a passing build+test, never publish untested code"]

key-files:
  created: []
  modified: [.github/workflows/build.yml, .github/workflows/release.yml]

key-decisions:
  - "Added permissions block directly under the on: block (before jobs:) in build.yml, matching GitHub Actions convention for top-level workflow permissions"
  - "Mirrored build.yml's exact restore/build/test commands and order in release.yml per plan constraint, rather than inventing new step names"

patterns-established:
  - "Least-privilege permissions: any workflow that only reads/builds/tests declares `permissions: contents: read` explicitly rather than relying on the (mutable, org-default-dependent) implicit token scope"
  - "Release gate: publish/release-asset steps are always preceded by the same restore/build/test sequence used in CI, so a tag pushed against untested code fails before any asset is attached"

requirements-completed: [UF-14-01, UF-14-02]

# Metrics
duration: 3min
completed: 2026-08-04
---

# Quick Task 260804-9rt: CI Hardening (UF-14-01, UF-14-02) Summary

**Closed two Phase 14 security-audit findings by adding a least-privilege `permissions: contents: read` block to build.yml and a restore/build/test gate before publish in release.yml.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-08-04T07:04:31Z
- **Completed:** 2026-08-04T07:06:02Z
- **Tasks:** 2 completed
- **Files modified:** 2

## Accomplishments
- build.yml now declares an explicit top-level `permissions: contents: read`, so PR-triggered restore/build/test code cannot rely on a broader implicit default `GITHUB_TOKEN` scope (UF-14-01)
- release.yml now runs `dotnet restore` -> `dotnet build --no-restore -c Release` -> `dotnet test --no-build -c Release` before the publish step, so a `v*` tag pushed against a commit that never passed CI fails before any release asset is built or attached (UF-14-02)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add least-privilege permissions block to build.yml (UF-14-01)** - `cb64ce4` (fix)
2. **Task 2: Add build/test gate before publish in release.yml (UF-14-02)** - `dd974e3` (fix)

**Plan metadata:** committed separately by orchestrator (docs)

## Files Created/Modified
- `.github/workflows/build.yml` - Added top-level `permissions: contents: read` block between `on:` and `jobs:`
- `.github/workflows/release.yml` - Inserted `dotnet restore`, `dotnet build --no-restore -c Release`, `dotnet test --no-build -c Release` steps after `setup-dotnet` and before the "Publish self-contained single-file exe" step

## Decisions Made
- Placed the `permissions:` block immediately after the `on:` triggers block in build.yml (standard GitHub Actions top-level placement)
- Mirrored build.yml's restore/build/test commands verbatim in release.yml (same commands, same order) per plan constraint, rather than deduplicating into a reusable workflow — kept the change minimal and additive as required

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `<action>` and `<verify>` specifications exactly; diffs are strictly additive (confirmed via `git diff` against the pre-dispatch base commit — only the two intended blocks were added, no other lines touched).

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. These changes only affect GitHub Actions workflow YAML; they take effect automatically on the next push/PR (build.yml) and next `v*` tag push (release.yml).

## Next Phase Readiness
- Both UF-14-01 and UF-14-02 findings from `14-SECURITY.md` are now closed.
- No blockers. This was a standalone quick task; no downstream phase depends on it beyond the closed audit findings.

---
*Phase: quick-260804-9rt*
*Completed: 2026-08-04*

## Self-Check: PASSED

- FOUND: .github/workflows/build.yml
- FOUND: .github/workflows/release.yml
- FOUND: .planning/quick/260804-9rt-fix-uf-14-01-and-uf-14-02-from-phase-14-/260804-9rt-SUMMARY.md
- FOUND: cb64ce4 (Task 1 commit)
- FOUND: dd974e3 (Task 2 commit)
