---
phase: 14-readme-release-documentation
plan: 01
subsystem: infra
tags: [github-actions, ci-cd, license, mit, release-automation]

# Dependency graph
requires: []
provides:
  - "MIT LICENSE file at repo root (backs README license badge)"
  - "CI build workflow (.github/workflows/build.yml) running dotnet build+test on windows-latest, push/PR to main"
  - "Tag-triggered release workflow (.github/workflows/release.yml) publishing the self-contained exe and attaching it to GitHub Releases on v* tag push"
affects: [14-02, 14-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "GitHub Actions two-workflow split: build.yml (push/PR, no side effects) vs release.yml (tag-push only, publish + asset upload)"
    - "Actions pinned to major-version tags (@v7/@v6/@v3), never @main/@latest/SHA"

key-files:
  created:
    - LICENSE
    - .github/workflows/build.yml
    - .github/workflows/release.yml
  modified: []

key-decisions:
  - "Copyright holder 'Blaz Pivk' (from git config user.name) per RESEARCH Assumption A1"
  - "release.yml reuses the exact existing dotnet publish command verbatim (no -r/--self-contained flags added) since RuntimeIdentifier is already set in RigToggle.App.csproj"
  - "Release workflow publish artifact path is src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe, matching the project's win-x64.pubxml PublishDir, not the SDK default publish path"

patterns-established:
  - "Two-workflow split for CI (build) vs release automation, keeping the noisy push/PR badge signal decoupled from the rarer tag-triggered publish+release job"

requirements-completed: [DOCS-02, DOCS-03]

# Metrics
duration: 3min
completed: 2026-08-03
---

# Phase 14 Plan 01: LICENSE and GitHub Actions Workflows Summary

**MIT LICENSE plus two GitHub Actions workflows (windows-latest build+test on push/PR, tag-triggered self-contained exe publish+release) backing the README's badges and download flow with real, live infrastructure.**

## Performance

- **Duration:** 3 min
- **Started:** 2026-08-03T20:45:47Z
- **Completed:** 2026-08-03T20:48:xxZ
- **Tasks:** 3 completed
- **Files modified:** 3 (all new)

## Accomplishments
- Added a standard OSI-canonical MIT LICENSE file at repo root, GitHub-license-detector-compatible, backing the README's license badge
- Added `.github/workflows/build.yml`: `windows-latest` CI running `dotnet restore` / `dotnet build --no-restore -c Release` / `dotnet test --no-build -c Release` on push/PR to `main` — required since this is a `net10.0-windows` WinForms/COM-interop/P-Invoke project that cannot build on `ubuntu-latest`
- Added `.github/workflows/release.yml`: triggers only on `v*` tag push, publishes the self-contained single-file exe via the project's existing canonical `dotnet publish` command, and attaches it to the auto-created GitHub Release via `softprops/action-gh-release@v3`

## Task Commits

Each task was committed atomically:

1. **Task 1: Create MIT LICENSE file at repo root** - `34220a4` (docs)
2. **Task 2: Create build workflow (.github/workflows/build.yml)** - `b033541` (feat)
3. **Task 3: Create release workflow (.github/workflows/release.yml)** - `e5acbb5` (feat)

_Note: no TDD tasks in this plan (infra/config files, no test-driven behavior)._

## Files Created/Modified
- `LICENSE` - standard OSI MIT License text, copyright "Blaz Pivk", 2026
- `.github/workflows/build.yml` - windows-latest CI: restore/build/test on push+PR to main
- `.github/workflows/release.yml` - windows-latest release: publish self-contained exe on v* tag push, attach to GitHub Release, `permissions: contents: write` scoped to the release job

## Decisions Made
- Copyright holder name taken from `git config user.name` ("Blaz Pivk") per RESEARCH Assumption A1 (low-risk, cosmetic).
- No deviations from the verified RESEARCH/PATTERNS example YAML — both workflows match the plan's verbatim specification exactly (action versions, trigger conditions, publish command, output path).

## Deviations from Plan

None - plan executed exactly as written. All three files match the verified examples in 14-RESEARCH.md and 14-PATTERNS.md verbatim, and all automated verification commands specified in the plan passed on first try.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. (Repo visibility flip and v1.0/v1.1 release backfill via `gh` CLI, per RESEARCH D-03/D-04, are one-time manual/scripted actions belonging to a later plan in this phase, not this plan's scope.)

## Next Phase Readiness

- LICENSE, build.yml, and release.yml are all in place and verified (YAML parses, content matches acceptance criteria byte-for-byte where specified).
- Plan 14-02/14-03 (README rewrite with badges, screenshots) can now safely reference these three files as real, live backing infrastructure — badges will render correctly once this plan's commits and a subsequent plan's README/release-backfill work land together (per RESEARCH Pitfall 2, badge correctness requires LICENSE + workflow + at least one release to co-exist).
- No blockers.

---
*Phase: 14-readme-release-documentation*
*Completed: 2026-08-03*
