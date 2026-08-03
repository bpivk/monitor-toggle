---
phase: 14-readme-release-documentation
plan: 03
subsystem: docs
tags: [readme, badges, shields.io, github-actions, documentation]

# Dependency graph
requires:
  - phase: 14-01
    provides: .github/workflows/build.yml (badge URL filename dependency)
  - phase: 14-02
    provides: public repo + backfilled v1.0/v1.1 GitHub Releases (badge render dependency)
provides:
  - Rewritten GitHub-ready README.md (badges, feature overview, problem statement, download+build, screenshots, requirements)
  - docs/screenshots/.gitkeep placeholder directory
affects: [milestone-close, v1.2-completion]

# Tech tracking
tech-stack:
  added: []
  patterns: ["shields.io live-endpoint badges (never static/decorative)", "markdown image placeholders pointing at not-yet-existing files"]

key-files:
  created: [docs/screenshots/.gitkeep]
  modified: [README.md]

key-decisions:
  - "README kept generic framing per D-11 (no 'Moza'/'BeamNG' naming) even though internal docs (CLAUDE.md/PROJECT.md) use those names — deliberate divergence, verified via grep -ci"
  - "Preserved both existing dotnet publish code blocks, the untrimmed/COM-interop rationale, and the output-path callout verbatim per 14-PATTERNS.md instruction"

patterns-established:
  - "Badge row at top of README uses only live-endpoint URLs (GitHub Actions badge.svg, shields.io license/release) — never hardcoded static badges"

requirements-completed: [DOCS-01, DOCS-02, DOCS-03]

# Metrics
duration: 25min
completed: 2026-08-03
---

# Phase 14 Plan 03: README Rewrite Summary

**Rewrote root README.md with three live shields.io/GitHub-Actions badges, a generic feature overview and problem statement, download+build instructions, and four screenshot placeholders under a new docs/screenshots/ directory. Task 3's live-repo human verification passed after the orchestrator caught and fixed a real bug: build.yml was scoped to `branches: [main]` but this repo's actual default branch is `master`.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-03T20:41:00Z (approx)
- **Completed:** 2026-08-03T21:10:00Z (approx)
- **Tasks:** 3 of 3 completed
- **Files modified:** 3 (README.md rewritten, docs/screenshots/.gitkeep created, .github/workflows/build.yml branch-trigger fix)

## Accomplishments
- Created `docs/screenshots/` as a git-persistable placeholder directory via `.gitkeep`
- Rewrote README.md in place: three live-endpoint badges (build status, license, latest release), generic problem statement (no Moza/BeamNG naming), full feature overview (toggle, tray/autostart, hotkey, multi-monitor sets, live theme-following, redesigned icons), four screenshot placeholders in real markdown image syntax, a Download section linking to GitHub Releases, and a Windows 10/11 x64 system-requirements note with Windows-11-only Mica/rounded-corners graceful degradation
- Preserved the existing "Build a standalone .exe" section verbatim: both `dotnet publish` command blocks, the untrimmed/COM-interop/P-Invoke rationale paragraph, and the `src/RigToggle.App/bin/publish/win-x64/` output-path callout

## Task Commits

Each task was committed atomically:

1. **Task 1: Create docs/screenshots/ placeholder directory** - `8099525` (chore)
2. **Task 2: Rewrite README.md — badges, feature overview, install/build, screenshots, requirements** - `2f0014d` (feat)

3. **Task 3: Confirm honest-badge end state on the live public repo** - checkpoint, resolved by orchestrator + user

Task 3 required the orchestrator's human user to visually confirm the live public GitHub repo. Before presenting the checkpoint, the orchestrator pushed master to origin (first-ever push, 88 commits, user-confirmed) and independently verified all 5 acceptance criteria via `gh`/`curl` (repo public, Build workflow green, all 3 badges rendering correct labels, v1.0/v1.1 releases notes-only, README generic with 4 placeholders). The user reviewed this evidence and replied "approved".

## Files Created/Modified
- `docs/screenshots/.gitkeep` - empty-directory placeholder so git persists `docs/screenshots/` as the target for user-supplied screenshots
- `README.md` - rewritten in place: badges, problem statement, feature overview, screenshot placeholders, download+build section, system requirements

## Decisions Made
- Kept README generic (no "Moza"/"BeamNG" naming) per D-11's explicit deliberate divergence from PROJECT.md/CLAUDE.md's internal framing — verified via `grep -ci 'moza\|beamng' README.md` returning `0`
- Reused the exact existing `dotnet publish` commands and rationale verbatim rather than rewriting/improving them, per 14-PATTERNS.md's explicit instruction and RESEARCH.md's anti-pattern warning
- Grouped screenshots into two 2-column markdown tables (normal/rig mode pair, settings/tray-menu pair) — a formatting choice left to Claude's discretion per CONTEXT.md, not a vision requirement

## Deviations from Plan

- **Real bug found and fixed during checkpoint verification (not in original plan):** `.github/workflows/build.yml` (created in 14-01) triggered on `branches: [main]`, but this repo's actual default branch is `master` — the Build workflow would never have fired on a real push, leaving the build-status badge permanently unpopulated. Fixed to `branches: [master]` in commit `10f401f`, pushed, and confirmed the workflow triggered and passed (run `30853093473`). This is a correctness fix to 14-01's deliverable, caught only because Task 3's live verification forced an actual push+check that 14-01's own local acceptance criteria (grep-based, no live trigger) couldn't catch.
- Tasks 1 and 2 otherwise executed exactly as written.

## Issues Encountered

None beyond the branch-name bug above, which was caught and fixed before the checkpoint was presented.

## User Setup Required

None — the orchestrator pushed to origin and ran all verification directly; no external service configuration was required of the user beyond confirming the checkpoint.

## Next Phase Readiness

- README.md, docs/screenshots/, and both CI workflows are complete, correct, and pushed to GitHub `main` (branch `master`)
- All three DOCS-01/02/03 requirements are satisfied and live-verified: repo public, Build workflow green, all three badges rendering correct values (build=passing, license=MIT, release=v1.1), v1.0/v1.1 releases notes-only, README generic with 4 screenshot placeholders
- v1.2 release (with an attached .exe, automated via release.yml on tag push) is deferred to milestone close, per D-04 and RESEARCH Open Question #1 — not part of this phase's completion bar

## Self-Check: PASSED

- FOUND: docs/screenshots/.gitkeep
- FOUND: README.md (modified)
- FOUND: .github/workflows/build.yml (branch-trigger fix)
- FOUND commit 8099525 (Task 1)
- FOUND commit 2f0014d (Task 2)
- FOUND commit 10f401f (Task 3 — branch-trigger fix discovered during checkpoint verification)
- VERIFIED live: https://github.com/bpivk/monitor-toggle/actions/runs/30853093473 (Build workflow, conclusion=success)

---
*Phase: 14-readme-release-documentation*
*Completed: 2026-08-03*
