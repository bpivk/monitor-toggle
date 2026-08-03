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

requirements-completed: []  # DOCS-01/02/03 not yet complete — plan paused at Task 3 human-verify checkpoint

# Metrics
duration: 20min
completed: 2026-08-03
---

# Phase 14 Plan 03: README Rewrite Summary (Tasks 1-2 of 3; Task 3 checkpoint pending)

**Rewrote root README.md with three live shields.io/GitHub-Actions badges, a generic feature overview and problem statement, download+build instructions, and four screenshot placeholders under a new docs/screenshots/ directory — Task 3's live-repo human verification is still pending.**

## Performance

- **Duration:** ~20 min (Tasks 1-2 only)
- **Started:** 2026-08-03T20:41:00Z (approx)
- **Completed (this pass):** 2026-08-03T21:01:21Z
- **Tasks:** 2 of 3 completed (Task 3 is a blocking human-verify checkpoint, not yet reached/answered)
- **Files modified:** 2 (README.md rewritten, docs/screenshots/.gitkeep created)

## Accomplishments
- Created `docs/screenshots/` as a git-persistable placeholder directory via `.gitkeep`
- Rewrote README.md in place: three live-endpoint badges (build status, license, latest release), generic problem statement (no Moza/BeamNG naming), full feature overview (toggle, tray/autostart, hotkey, multi-monitor sets, live theme-following, redesigned icons), four screenshot placeholders in real markdown image syntax, a Download section linking to GitHub Releases, and a Windows 10/11 x64 system-requirements note with Windows-11-only Mica/rounded-corners graceful degradation
- Preserved the existing "Build a standalone .exe" section verbatim: both `dotnet publish` command blocks, the untrimmed/COM-interop/P-Invoke rationale paragraph, and the `src/RigToggle.App/bin/publish/win-x64/` output-path callout

## Task Commits

Each task was committed atomically:

1. **Task 1: Create docs/screenshots/ placeholder directory** - `8099525` (chore)
2. **Task 2: Rewrite README.md — badges, feature overview, install/build, screenshots, requirements** - `2f0014d` (feat)

Task 3 is `type="checkpoint:human-verify" gate="blocking"` — requires the orchestrator's human user to visually confirm the live public GitHub repo (badge rendering, green build workflow, Releases tab). Not attempted by this agent per plan instructions; execution paused here awaiting human verification/resume.

**Plan metadata:** not yet committed (final metadata commit deferred until Task 3 resolves)

## Files Created/Modified
- `docs/screenshots/.gitkeep` - empty-directory placeholder so git persists `docs/screenshots/` as the target for user-supplied screenshots
- `README.md` - rewritten in place: badges, problem statement, feature overview, screenshot placeholders, download+build section, system requirements

## Decisions Made
- Kept README generic (no "Moza"/"BeamNG" naming) per D-11's explicit deliberate divergence from PROJECT.md/CLAUDE.md's internal framing — verified via `grep -ci 'moza\|beamng' README.md` returning `0`
- Reused the exact existing `dotnet publish` commands and rationale verbatim rather than rewriting/improving them, per 14-PATTERNS.md's explicit instruction and RESEARCH.md's anti-pattern warning
- Grouped screenshots into two 2-column markdown tables (normal/rig mode pair, settings/tray-menu pair) — a formatting choice left to Claude's discretion per CONTEXT.md, not a vision requirement

## Deviations from Plan

None - Tasks 1 and 2 executed exactly as written. Task 3 (checkpoint) intentionally not attempted, per this plan's explicit instruction that it requires a human to view the live public GitHub repo.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required for Tasks 1-2. Task 3 itself is the pending human-verification checkpoint (see below).

## Next Phase Readiness

- README.md and docs/screenshots/ are complete and committed; ready for the human-verify checkpoint (Task 3) once this phase's commits are pushed to GitHub `main`
- Task 3 requires: repo loads publicly, "Build" workflow ran green, all three badges render (build=passing, license=MIT, release=v1.1), v1.0/v1.1 releases show notes-only, README reads generically with broken-image screenshot placeholders
- Blocked on: human visually confirming the live public repo state (cannot be automated/verified by this agent)

## Self-Check: PASSED

- FOUND: docs/screenshots/.gitkeep
- FOUND: README.md (modified)
- FOUND commit 8099525 (Task 1)
- FOUND commit 2f0014d (Task 2)

---
*Phase: 14-readme-release-documentation*
*Completed (partial — Tasks 1-2 only, Task 3 checkpoint pending): 2026-08-03*
