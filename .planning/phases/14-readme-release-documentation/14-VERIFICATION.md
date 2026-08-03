---
phase: 14-readme-release-documentation
verified: 2026-08-03T21:17:26Z
status: passed
score: 14/14 must-haves verified
overrides_applied: 0
---

# Phase 14: README & Release Documentation Verification Report

**Phase Goal:** A new GitHub-ready README.md communicates what the app does, how to get it, and how to build it, referencing the finished visual polish from Phases 12-13.
**Verified:** 2026-08-03T21:17:26Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | DOCS-01: README feature overview includes screenshots of both modes, or clearly marked placeholders | ✓ VERIFIED | `README.md` "Screenshots" section (lines 41-49) contains 4 real markdown-image placeholders in a 2x2 table: `docs/screenshots/main-normal.png`, `main-rig.png`, `settings.png`, `tray-menu.png`. Per locked CONTEXT.md decision D-07, these deliberately point at not-yet-existing files (user supplies screenshots later) — confirmed intentional, not a stub. |
| 2 | DOCS-02: README documents downloading the released .exe and building from source | ✓ VERIFIED | "Download" section links to `https://github.com/bpivk/monitor-toggle/releases/latest` with an honest caveat that v1.0/v1.1 are notes-only. "Build a standalone .exe" section preserves the exact `dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64` command plus fallback flags and the untrimmed/COM-interop rationale. |
| 3 | DOCS-03: README displays GitHub badges for build status, license, and latest release | ✓ VERIFIED | Three badges at top of README use live endpoints: `.../actions/workflows/build.yml/badge.svg`, `img.shields.io/github/license/bpivk/monitor-toggle`, `img.shields.io/github/v/release/bpivk/monitor-toggle`. Live-fetched in this verification pass: build badge SVG text = "passing"; license badge = "MIT"; release badge = "v1.1". |
| 4 | D-01: LICENSE at repo root, OSI-standard MIT text | ✓ VERIFIED | `LICENSE` exists at root, first line `MIT License`, contains `Copyright (c) 2026 Blaz Pivk` and the full warranty-disclaimer paragraph. Live-confirmed via GitHub API: `repos/bpivk/monitor-toggle` → `license.spdx_id: MIT` (GitHub's own license detector recognizes it). |
| 5 | D-02: build.yml runs dotnet build+test on windows-latest on push/PR | ✓ VERIFIED | `.github/workflows/build.yml`: `runs-on: windows-latest`, triggers on `push`/`pull_request` to `master` (repo's real default branch — confirmed via `gh repo view --json defaultBranchRef` → `master`), steps run `dotnet restore` / `dotnet build --no-restore -c Release` / `dotnet test --no-build -c Release`. Live-confirmed: `gh run list` shows run `30853093473` on `master`, conclusion `success`. |
| 6 | D-05: release.yml publishes exe on v* tag push, attaches to GitHub Release | ✓ VERIFIED (file-level; not yet triggered by design) | `.github/workflows/release.yml`: trigger `tags: ['v*']` only, `permissions: contents: write`, publish step byte-identical to README's documented command, `softprops/action-gh-release@v3` with `files: src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe` (matches `win-x64.pubxml` PublishDir). No v1.2 tag has been pushed yet — correctly deferred to milestone close per plan's own stated completion bar (Open Question #1), not a gap in this phase. |
| 7 | D-03: repo bpivk/monitor-toggle publicly viewable | ✓ VERIFIED | Live: `gh repo view bpivk/monitor-toggle --json isPrivate` → `false`; anonymous `curl -o /dev/null -w '%{http_code}' https://github.com/bpivk/monitor-toggle` → `200`. |
| 8 | D-04: v1.0 and v1.1 exist as notes-only GitHub Releases | ✓ VERIFIED | Live: `gh release list` → `v1.1` (Latest) and `v1.0`, no `v1.2`. Both confirmed 0 attached assets in 14-02-SUMMARY and re-confirmed live via `gh api repos/.../releases/latest` → `tag_name: v1.1`. |
| 9 | D-10: feature overview covers toggle, tray/autostart, hotkey, multi-monitor, live theme-following, redesigned icons | ✓ VERIFIED | README "Features" section lists all six: one-click toggle, tray residency/autostart, global hotkey, multi-monitor sets, live theme-following, redesigned icons. |
| 10 | D-12: generic problem statement, no specific game/app names | ✓ VERIFIED | "Why this exists" section describes the problem generically ("some games and apps misbehave..."); no "Moza"/"BeamNG" naming anywhere. |
| 11 | D-06/D-07/D-08: four screenshot placeholders, real markdown image syntax, under docs/screenshots/ | ✓ VERIFIED | Confirmed real `![alt](path)` syntax (not bracketed TODO text) for all 4 slots; `docs/screenshots/.gitkeep` persists the target directory in git (`git ls-files docs/screenshots/` → `.gitkeep` only, zero PNGs, as intended). |
| 12 | D-09: no animated GIF/demo clip added | ✓ VERIFIED | `grep -c 'gif\|\.gif' README.md` → `0`. |
| 13 | D-11: generic framing, no "Moza"/"BeamNG" naming | ✓ VERIFIED | `grep -ci 'moza\|beamng' README.md` → `0`. |
| 14 | D-13: README states Windows 10/11 x64 requirement + Windows-11-only Mica/rounded-corner graceful degradation | ✓ VERIFIED | "System requirements" section states Windows 10/11 x64 and that Mica backdrop/rounded corners are Windows-11-only, gracefully degrading on Windows 10. |

**Score:** 14/14 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `LICENSE` | MIT text, GitHub-detectable | ✓ VERIFIED | Exists, substantive, GitHub API confirms `license.spdx_id: MIT` — wired end-to-end (backs the live license badge). |
| `.github/workflows/build.yml` | windows-latest CI, build+test, push/PR to real default branch | ✓ VERIFIED | Exists, substantive, correctly targets `master` (not `main` — the bug found and fixed mid-phase per 14-03-SUMMARY). Wired: badge renders "passing", live run `30853093473` succeeded. |
| `.github/workflows/release.yml` | tag-triggered publish + attach exe | ✓ VERIFIED | Exists, substantive, correct publish command and output path. Not yet triggered (no v1.2 tag pushed) — intentional per plan scope, not a defect. |
| `README.md` | GitHub-ready: badges, feature overview, problem statement, install/build, screenshots, requirements | ✓ VERIFIED | All required sections present and content-correct; live-rendering confirmed for all three badges. |
| `docs/screenshots/.gitkeep` | Persists the screenshots target directory | ✓ VERIFIED | Exists, tracked in git, directory otherwise empty as intended (no premature PNGs). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| README build badge | `.github/workflows/build.yml` | `actions/workflows/build.yml/badge.svg` filename match | ✓ WIRED | Filename matches; live badge fetch returns "passing". |
| README license badge | `LICENSE` | shields.io reading GitHub's license-detector API | ✓ WIRED | Live badge fetch returns "MIT". |
| README release badge | GitHub Releases | shields.io reading latest release | ✓ WIRED | Live badge fetch returns "v1.1", matches `releases/latest` API. |
| `release.yml` publish step | `src/RigToggle.App/RigToggle.App.csproj` | `dotnet publish ... -p:PublishProfile=win-x64` | ✓ WIRED | Command byte-identical to README's documented command (verified by code review + direct read). |
| `release.yml` asset upload | `src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe` | `softprops/action-gh-release@v3 files:` | ✓ WIRED | Path matches `win-x64.pubxml` PublishDir (confirmed in 14-REVIEW.md cross-check). |
| README screenshot placeholders | `docs/screenshots/*.png` | markdown image syntax | ⚠️ NOT_WIRED (intentional) | Files do not exist yet — this is the deliberate, locked CONTEXT.md decision D-07 (placeholders for user-supplied screenshots), not a defect. Flagged by code review (WR-01) and explicitly confirmed non-blocking per this verification's task instructions. |

### Data-Flow Trace (Level 4)

Not applicable — this phase produces static documentation and CI/release config, not components rendering dynamic runtime data. Skipped.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Repo is publicly reachable | `curl -o /dev/null -w '%{http_code}' https://github.com/bpivk/monitor-toggle` | `200` | ✓ PASS |
| Repo visibility is public | `gh repo view bpivk/monitor-toggle --json isPrivate --jq '.isPrivate'` | `false` | ✓ PASS |
| GitHub detects MIT license | `curl https://api.github.com/repos/bpivk/monitor-toggle` → `.license.spdx_id` | `MIT` | ✓ PASS |
| Build badge renders passing | `curl https://github.com/.../build.yml/badge.svg` | contains `passing` | ✓ PASS |
| License badge renders MIT | `curl https://img.shields.io/github/license/bpivk/monitor-toggle` | contains `MIT` | ✓ PASS |
| Release badge renders v1.1 | `curl https://img.shields.io/github/v/release/bpivk/monitor-toggle` | contains `v1.1` | ✓ PASS |
| Latest workflow run succeeded | `gh run list --repo bpivk/monitor-toggle --limit 5` | run `30853093473`, `completed`/`success`, branch `master` | ✓ PASS |
| Releases list correct (v1.0, v1.1, no v1.2) | `gh release list --repo bpivk/monitor-toggle` | `v1.1` (Latest), `v1.0`, no `v1.2` | ✓ PASS |
| No Moza/BeamNG naming in README | `grep -ci 'moza\|beamng' README.md` | `0` | ✓ PASS |
| No GIF in README | `grep -c 'gif' README.md` | `0` | ✓ PASS |

All spot-checks independently re-executed in this verification session (not taken from SUMMARY.md claims) — results match what 14-03-SUMMARY.md reported the orchestrator/user confirmed at checkpoint time.

### Probe Execution

No `scripts/*/tests/probe-*.sh` conventions apply to this docs/infra phase, and none were declared in the PLAN/SUMMARY files. Skipped.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|--------------|--------|----------|
| DOCS-01 | 14-03 | README feature overview with screenshots/placeholders in both modes | ✓ SATISFIED | README Screenshots section, 4 placeholders, see Truth #1/#11 |
| DOCS-02 | 14-01, 14-03 | README instructions for downloading the released .exe and building from source | ✓ SATISFIED | README Download + Build sections, see Truth #2/#6/#8 |
| DOCS-03 | 14-01, 14-03 | README GitHub badges (build status, license, latest release) | ✓ SATISFIED | README badge row, live-rendering confirmed, see Truth #3/#5/#7 |

**Orphaned requirements:** None. All three DOCS-* requirements mapped to Phase 14 in REQUIREMENTS.md's traceability table were claimed by at least one plan (`14-01`: DOCS-02/03; `14-02`: DOCS-02/03; `14-03`: DOCS-01/02/03).

**Note (non-blocking, doc-sync only):** `.planning/REQUIREMENTS.md` still shows `[ ]` (unchecked) for DOCS-01/02/03 and "Pending" in its Traceability table (lines 28-30, 76-78), even though the phase is functionally complete and ROADMAP.md marks Phase 14 `[x]`. Every prior phase in this project's history (e.g. commits `9a4b106` "mark ICON-01/ICON-02 complete", `eab1774` "docs(phase-12): complete phase execution") updated these checkboxes as part of phase closure — Phase 14 has not yet had that doc-sync commit. This does not affect the actual deliverable (the README genuinely satisfies DOCS-01/02/03, independently verified above) and is not one of this phase's ROADMAP success criteria, so it does not block phase-goal achievement. Recommend a follow-up doc-sync commit to check the boxes and update the traceability table to "Complete" for consistency with established project convention.

### Anti-Patterns Found

None. Scanned `README.md`, `LICENSE`, `.github/workflows/build.yml`, `.github/workflows/release.yml`, `docs/screenshots/.gitkeep` for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` and "coming soon"/"not yet implemented" language — zero matches.

A prior code review (`14-REVIEW.md`) found 4 warnings, all CI/CD hardening advisories explicitly scoped as non-blocking by the review itself and by this verification task's instructions:
- WR-01 (README screenshot links point to non-existent PNGs) — **not a defect**, it is the locked CONTEXT.md decision D-07 (intentional user-supplied placeholder), reconfirmed here.
- WR-02 (`build.yml` missing explicit `permissions:` block) — hardening gap, not a goal blocker.
- WR-03 (Actions pinned to floating tags, not SHAs) — supply-chain hardening gap, not a goal blocker.
- WR-04 (`release.yml` has no test gate before publish) — hardening gap, not a goal blocker.
- IN-01 (no `timeout-minutes` on either workflow) — informational.

### Human Verification Required

None. The phase's one `checkpoint:human-verify` task (14-03 Task 3 — confirm honest-badge end state on the live public repo) was already executed and approved during phase execution: the orchestrator independently verified all 5 acceptance criteria via live `gh`/`curl` checks and the user replied "approved" (see 14-03-SUMMARY.md Deviations section, including the real `main`→`master` branch-trigger bug found and fixed at that time, commit `10f401f`). This verification pass independently re-ran the same class of live checks (repo visibility, license detection, badge rendering, latest workflow run, releases list) in a fresh session and obtained matching results — no discrepancies found, no further human action needed.

### Gaps Summary

No gaps found. All 14 merged must-have truths (3 ROADMAP success criteria + 11 plan-level D-decisions) are verified against the actual codebase and, where applicable, against live GitHub/shields.io state fetched independently in this verification session (not taken from SUMMARY.md narrative). The one non-blocking documentation-sync item (REQUIREMENTS.md checkboxes not yet flipped) is noted above for a quick follow-up but does not affect phase-goal achievement.

---

_Verified: 2026-08-03T21:17:26Z_
_Verifier: Claude (gsd-verifier)_
