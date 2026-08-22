---
phase: 26-auto-update
plan: 02
subsystem: infra
tags: [dotnet, security, sha256, github-releases, self-update, ci]

requires:
  - phase: 26-auto-update
    plan: 01
    provides: "GitHubReleaseFeed/ReleaseInfo/WindowsUpdateApplier.DownloadAndStageAsync from the auto-update tracer, extended in place here rather than replaced"
provides:
  - "release.yml Compute SHA256 checksum step publishing RigToggle.App.exe.sha256 alongside every release exe"
  - "ReleaseInfo.ChecksumDownloadUrl (nullable positional member) and GitHubReleaseFeed's checksum-asset resolution through the T-26-01 host allow-list"
  - "UpdateChecksum: pure Core SHA256 compute/compare utility, fail-closed on missing/malformed digest"
  - "WindowsUpdateApplier.DownloadAndStageAsync verify-before-return, closing PITFALLS Pitfall 5's partial-download gap"
affects: [26-03-never-stranded-recovery, 26-04-skip-version-and-manual-check, 26-05-formatted-release-notes]

actuals:
  tokens: 6800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Verify-before-return inside the still-running old process (not the helper process) so a checksum mismatch surfaces as an ordinary D-08 Warning toast, never a half-swapped installation"
    - "Fail-closed boolean utility (UpdateChecksum.Matches): every unestablishable-digest branch (null/empty/short/non-hex) resolves to false, never true"

key-files:
  created:
    - src/RigToggle.Core/UpdateChecksum.cs
    - src/RigToggle.Tests/UpdateChecksumTests.cs
    - src/RigToggle.Tests/GitHubReleaseFeedTests.cs
  modified:
    - .github/workflows/release.yml
    - src/RigToggle.Core/Models/ReleaseInfo.cs
    - src/RigToggle.Core/GitHubReleaseFeed.cs
    - src/RigToggle.Windows/WindowsUpdateApplier.cs
    - src/RigToggle.Tests/UpdateOrchestratorTests.cs

key-decisions:
  - "ReleaseInfo.ChecksumDownloadUrl is a new nullable positional record member, not an added optional-with-default -- forces every constructor call site (including plan 26-01's own UpdateOrchestratorTests fixture) to make an explicit choice rather than silently defaulting"
  - "GitHubReleaseFeed resolves the checksum asset independently of the exe asset and never discards the whole release for a missing/disallowed checksum URL -- absence is visible (null ChecksumDownloadUrl) and reportable, not indistinguishable from 'no release found'"
  - "WindowsUpdateApplier deletes the staged file and throws InvalidOperationException on both 'no checksum published' and 'checksum mismatch' -- both propagate to MainForm's existing post-confirm catch (D-08), deliberately not a separate failure path"
  - "UpdateChecksum.Matches extracts the first whitespace-delimited token from the published text so a full sha256sum-style line ('<hex>  <filename>') and a bare 64-char hex digest both work through the same code path"

requirements-completed: [UPDATE-04, UPDATE-05]

coverage:
  - id: D1
    description: "Every GitHub Release publishes a .sha256 checksum alongside the raw exe (D-10)"
    requirement: "UPDATE-04"
    verification:
      - kind: other
        ref: "grep -qi 'Get-FileHash' .github/workflows/release.yml && python3 -c \"import yaml; yaml.safe_load(open('.github/workflows/release.yml'))\""
        status: pass
    human_judgment: true
    rationale: "The release.yml checksum step can only be exercised end-to-end by an actual tagged CI run on GitHub; static grep/YAML-parse confirms the wiring but not a live release."
  - id: D2
    description: "UpdateChecksum.ComputeSha256/Matches: round-trip, case-insensitivity, sha256sum-line tolerance, and fail-closed on missing/malformed digest (D-11)"
    requirement: "UPDATE-05"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateChecksumTests.cs (10 Fact/Theory cases)"
        status: pass
    human_judgment: false
  - id: D3
    description: "GitHubReleaseFeed resolves ChecksumDownloadUrl through the T-26-01 host allow-list and tolerates its absence without discarding the release"
    requirement: "UPDATE-04"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/GitHubReleaseFeedTests.cs (10 Fact cases incl. both-assets, checksum-absent, http-downgrade, off-host, 404, transport exception, malformed JSON, User-Agent)"
        status: pass
    human_judgment: false
  - id: D4
    description: "WindowsUpdateApplier.DownloadAndStageAsync verifies the staged file's SHA256 before returning, deleting it and throwing on missing/mismatched checksum (D-11)"
    requirement: "UPDATE-05"
    verification:
      - kind: other
        ref: "grep -q 'UpdateChecksum' src/RigToggle.Windows/WindowsUpdateApplier.cs (call appears before the method's return statement)"
        status: pass
    human_judgment: true
    rationale: "The real HttpClient.GetStringAsync fetch of the checksum text, the staged-file delete, and the propagation into MainForm's D-08 Warning toast require a live Windows process and real network I/O not exercisable in this Linux build sandbox -- needs rig verification, same open item as plan 26-01's WindowsUpdateApplier/UpdateApplyEntryPoint."

duration: ~10min
completed: 2026-08-22
status: complete
---

# Phase 26 Plan 02: Checksum Integrity Summary

**Publishes a SHA256 checksum alongside every release exe and verifies it in the still-running old process before any code path can reach the swap step — a mismatch or missing checksum now surfaces as the same D-08 Warning toast as any other apply failure.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-08-22T17:11:00Z (approx.)
- **Completed:** 2026-08-22T17:18:31Z
- **Tasks:** 2
- **Files modified:** 8 (3 created, 5 modified)

## Accomplishments
- `release.yml` now computes a SHA256 digest of the published exe via PowerShell `Get-FileHash` and attaches `RigToggle.App.exe.sha256` alongside `RigToggle.App.exe` on every future GitHub Release — the raw exe attachment path is unchanged, per D-10.
- `ReleaseInfo.ChecksumDownloadUrl` (new nullable positional member) and `GitHubReleaseFeed`'s independent checksum-asset resolution — same T-26-01 scheme/host allow-list as the exe asset, absence never discards the release.
- New `UpdateChecksum` Core utility (`ComputeSha256`/`Matches`) — pure, no I/O beyond the file it's handed, fail-closed on any unestablishable published digest.
- `WindowsUpdateApplier.DownloadAndStageAsync` now verifies the staged file's checksum before ever returning: missing checksum or mismatch deletes the staged file and throws, propagating to `MainForm`'s existing post-confirm catch (D-08 Warning toast) — no separate failure mode, closing PITFALLS Pitfall 5's partial-download gap.
- 20 new automated tests (`UpdateChecksumTests` + `GitHubReleaseFeedTests`, the repo's first hand-rolled `HttpMessageHandler` stub) — full 172-test cross-platform suite green, zero new warnings, zero new NuGet packages.

## Task Commits

1. **Task 1: Publish a .sha256 alongside the release exe and verify it before the swap** - `89caf96` (feat)
2. **Task 2: Automated coverage for checksum matching and release-asset resolution** - `df6a4c6` (test)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP writes are owned by the wave orchestrator in worktree mode)

_Both tasks committed as single atomic commits, tdd="true" per the plan; Task 1 implements UpdateChecksum/GitHubReleaseFeed/WindowsUpdateApplier changes together (they're one coherent verify-before-swap feature), Task 2 writes the dedicated test files against Task 1's already-implemented code — the plan's own explicit structure, not a violated RED-first order._

## Files Created/Modified
- `.github/workflows/release.yml` - Added "Compute SHA256 checksum" step (PowerShell `Get-FileHash`), extended the release attachment `files:` from a scalar to a two-file list
- `src/RigToggle.Core/Models/ReleaseInfo.cs` - Added nullable `ChecksumDownloadUrl` positional member with fail-closed documentation
- `src/RigToggle.Core/GitHubReleaseFeed.cs` - Resolves the `.sha256` asset through the same T-26-01 allow-list as the exe asset; absence yields `null`, not a discarded release
- `src/RigToggle.Core/UpdateChecksum.cs` - New pure static class: `ComputeSha256(path)` (BCL `SHA256.HashData`), `Matches(computedHex, publishedText)` (sha256sum-line-tolerant, fail-closed)
- `src/RigToggle.Windows/WindowsUpdateApplier.cs` - `DownloadAndStageAsync` verifies checksum before returning; deletes staged file and throws `InvalidOperationException` on missing/mismatched checksum
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` - Updated `ReleaseInfo` construction for the new record member (Rule 3 blocking fix — record shape change breaks every positional-constructor call site)
- `src/RigToggle.Tests/UpdateChecksumTests.cs` - 10 test cases covering round-trip, case-insensitivity, sha256sum-line tolerance, and the fail-closed group
- `src/RigToggle.Tests/GitHubReleaseFeedTests.cs` - 10 test cases covering asset resolution, host allow-list rejection, missing-checksum tolerance, and every failure-to-null path, via a hand-rolled `HttpMessageHandler` stub (no network)

## Decisions Made
- `ReleaseInfo.ChecksumDownloadUrl` added as a required (nullable) positional member rather than an optional-with-default parameter — this forces every constructor call site to make an explicit choice about checksum availability instead of silently defaulting, and the compiler catches every site that needs updating (only one production call site, `GitHubReleaseFeed`, plus the one test fixture).
- Checksum resolution in `GitHubReleaseFeed` is fully independent of exe-asset resolution — a release with the exe but no `.sha256` still returns a `ReleaseInfo` (with `ChecksumDownloadUrl: null`), letting `WindowsUpdateApplier` be the single place that decides "no checksum published" is fail-closed, per D-11's explicit prohibition against an implicit pass.
- Both `WindowsUpdateApplier` throw sites (missing checksum, mismatch) use `InvalidOperationException` and are placed textually before the method's `return` — verified structurally (no code path can reach `ApplyAndRelaunch` with an unverified staged file) and documented in the method's doc comment as a deliberate reuse of the existing D-08 Warning-toast failure path, not a new one.
- `UpdateChecksum.Matches` truncates the mismatch error message to the first 16 hex characters of each digest (in `WindowsUpdateApplier`, not in `UpdateChecksum` itself) to stay inside `ToggleResultFormatter.TruncateForBalloon`'s ~250-character balloon budget.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated `UpdateOrchestratorTests.cs`'s `ReleaseInfo` construction for the new record member**
- **Found during:** Task 1 (adding `ReleaseInfo.ChecksumDownloadUrl`)
- **Issue:** `ReleaseInfo` is a positional-parameter record; adding `ChecksumDownloadUrl` as a new positional member breaks every existing constructor call, including the `NewerRelease` test fixture in plan 26-01's `UpdateOrchestratorTests.cs` (not in this plan's `files_modified` list) — the build would not compile without this fix.
- **Fix:** Added `ChecksumDownloadUrl: "https://objects.githubusercontent.com/asset.exe.sha256"` as a named argument to the existing constructor call. No test behavior changed — `UpdateOrchestratorTests` exercises orchestration decisions, not checksum verification, so any non-null placeholder value is correct here.
- **Files modified:** `src/RigToggle.Tests/UpdateOrchestratorTests.cs`
- **Verification:** `dotnet build RigToggle.sln -c Release --nologo` succeeds; full 172-test suite passes with no `UpdateOrchestratorTests` regressions.
- **Committed in:** `89caf96` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking record-shape fix)
**Impact on plan:** Necessary compile-time consequence of adding a positional record member per the plan's own Task 1 action (b). No scope creep — the fixture's actual test intent (orchestration decisions) is unaffected.

## Issues Encountered

- **Pre-existing `xUnit1031` warnings (6, unrelated to this plan) still present.** Same gap plan 26-01 documented in `.planning/phases/26-auto-update/deferred-items.md`: `SingleInstanceGuardTests.cs` (2) and `ToggleOrchestratorTests.cs` (4) carry pre-existing "blocking task operations" warnings from before this plan. A clean (`--no-incremental`) rebuild confirms the same 6 warnings at the same line numbers both before and after this plan's changes — this plan's own new/modified files introduce 0 new warnings. Not fixed here per the executor's SCOPE BOUNDARY rule; already tracked in the existing deferred-items.md, not duplicated.
- **This build sandbox is Linux, not Windows.** `dotnet build`/`dotnet test` for the full solution (including `RigToggle.App`/`RigToggle.Windows`/`RigToggle.Windows.Tests`, which compile against Windows reference assemblies on Linux) succeeded and were exercised directly. What could **not** be exercised: the real `HttpClient.GetStringAsync` checksum fetch, the real staged-file delete on a live filesystem under the running exe's directory, and the real propagation into `MainForm`'s D-08 Warning toast — these require a live Windows process host and real network I/O. Same open rig-verification gap plan 26-01 already flagged for the broader auto-update mechanism.

## User Setup Required

None - no external service configuration required. (The checksum fetch reuses the same unauthenticated `HttpClient` the release feed already uses; no new secrets/env vars introduced.)

## Next Phase Readiness

The verify-before-swap insertion point (`WindowsUpdateApplier.DownloadAndStageAsync`, immediately before its `return`) is now the natural checksum gate for anything plan 26-03 (never-stranded recovery) adds downstream — a checksum failure here already guarantees `ApplyAndRelaunch`/`UpdateApplyEntryPoint` are never reached with a corrupted file, so 26-03's recovery design can assume the swap step only ever begins with an integrity-verified exe. `ReleaseInfo.ChecksumDownloadUrl`'s nullability (pre-checksum releases) is a clean, already-tested edge case any future plan touching `ReleaseInfo` should preserve.

**Blocker/concern carried forward:** this plan is unverified on real Windows hardware (build/unit-test only, per the Linux sandbox limitation above) — the real checksum HTTP fetch, the staged-file delete under a live process, and the D-08 toast rendering for a checksum-mismatch message are all still open until a rig pass, consistent with plan 26-01's existing carried-forward blocker.

---
*Phase: 26-auto-update*
*Completed: 2026-08-22*

## Self-Check: PASSED

All files created/modified verified present on disk; commits `89caf96` (Task 1) and `df6a4c6` (Task 2) both found in `git log --oneline`.
