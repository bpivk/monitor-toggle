---
phase: 18-cleanup-pass-exe-size-reduction
plan: 03
subsystem: infra
tags: [msbuild, publish, single-file, naudio, invariant-globalization, dotnet-publish]

# Dependency graph
requires: []
provides:
  - "Self-contained single-file exe shrunk from ~112 MB to ~47 MB (57.79% reduction) via four MSBuild-level levers"
  - "NAudio meta-package replaced with NAudio.Wasapi 2.3.0 (zero source-code changes)"
  - "PublishTrimmed remains explicitly false — confirmed by grep gate"
affects: [18-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "MSBuild-only size reduction levers (compression, satellite-language trim, invariant globalization) applied without touching application code"
    - "Package-family narrowing swap (NAudio -> NAudio.Wasapi) verified source-neutral via a namespace grep before changing the PackageReference"

key-files:
  created:
    - .planning/phases/18-cleanup-pass-exe-size-reduction/18-03-SUMMARY.md
  modified:
    - src/RigToggle.App/RigToggle.App.csproj
    - src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
    - src/RigToggle.Windows/RigToggle.Windows.csproj

key-decisions:
  - "Applied all four levers (EnableCompressionInSingleFile, SatelliteResourceLanguages=en, InvariantGlobalization=true, NAudio.Wasapi swap) in a single Task 1 commit since they are independent, low-risk MSBuild-only changes with no interaction risk"
  - "Left PublishTrimmed, PublishAot, PublishReadyToRun and elevation manifests untouched per hard constraints"

patterns-established:
  - "Byte-count regression gates (baseline vs. post-change stat -c %s comparison) as the acceptance mechanism for packaging-only size-reduction plans"

requirements-completed: [PERF-01]

# Metrics
duration: 25min
completed: 2026-08-09
---

# Phase 18 Plan 03: Exe Size Reduction via MSBuild Levers Summary

**Shrunk the self-contained single-file publish exe from 116,946,229 bytes to 49,360,497 bytes (57.79% reduction) using four MSBuild-only levers — compression, satellite-language trim, invariant globalization, and an NAudio meta-package narrowing swap — with zero application-code changes and PublishTrimmed still explicitly false.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-08-09T08:26:00Z
- **Completed:** 2026-08-09T08:29:00Z
- **Tasks:** 2 completed
- **Files modified:** 3 (plus this summary)

## Accomplishments
- Recorded a real pre-change publish baseline: 116,946,229 bytes (matches 18-RESEARCH.md's measured figure exactly)
- Applied `SatelliteResourceLanguages=en` and `InvariantGlobalization=true` to `RigToggle.App.csproj`'s existing `<PropertyGroup>`, each with an inline comment in the project's established citation style
- Added `EnableCompressionInSingleFile=true` to `win-x64.pubxml`, extending its top-of-file comment block with a new paragraph documenting the cold-start tradeoff (transferred to PERF-02's rig checkpoint), leaving `PublishTrimmed` untouched
- Swapped `RigToggle.Windows.csproj`'s `NAudio` 2.3.0 meta-package reference for `NAudio.Wasapi` 2.3.0 — confirmed zero `.cs` changes required since `WindowsAudioController.cs`'s only import (`using NAudio.CoreAudioApi;`) is unaffected by the package split
- Re-published post-change and measured 49,360,497 bytes — a 67,585,732-byte, 57.79% reduction, comfortably beating the 45% failure floor and the 60 MB ceiling
- Ran both regression gates: `PublishTrimmed` confirmed still `<PublishTrimmed>false</PublishTrimmed>` (exactly one XML declaration, in `win-x64.pubxml`); zero hits for `requestedExecutionLevel`/`ApplicationManifest`/`PublishAot`/`PublishReadyToRun` across all `.csproj`/`.pubxml` files
- Confirmed the publish output is still a genuine single file: `RigToggle.App.exe` plus `.pdb` files only, no loose managed `.dll`s
- Confirmed resolved NuGet versions: `NAudio.Wasapi/2.3.0` and (transitive) `NAudio.Core/2.3.0` — no version drift
- `dotnet build RigToggle.sln` reports `0 Error(s)`; `dotnet test RigToggle.Tests.csproj` reports `Failed: 0, Passed: 85`
- No `.cs` file was modified (`git diff --name-only -- 'src/**/*.cs'` returns nothing)

## Task Commits

Each task was committed atomically:

1. **Task 1: Capture the pre-change exe byte count, then apply all four size levers** - `bf14b38` (perf)
2. **Task 2: Re-publish, measure the delta, and run the trimming regression gate** - (this SUMMARY.md commit; no source files changed in Task 2, only measurement + documentation)

**Plan metadata:** (this commit)

_Note: Task 2 produced no code changes — it re-published, measured, and ran verification gates, then recorded results in this summary._

## Files Created/Modified
- `src/RigToggle.App/RigToggle.App.csproj` - Added `SatelliteResourceLanguages=en` and `InvariantGlobalization=true` to the existing PropertyGroup
- `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` - Added `EnableCompressionInSingleFile=true`, extended top-of-file comment block
- `src/RigToggle.Windows/RigToggle.Windows.csproj` - Replaced `NAudio` 2.3.0 PackageReference with `NAudio.Wasapi` 2.3.0
- `.planning/phases/18-cleanup-pass-exe-size-reduction/18-03-SUMMARY.md` - This summary, recording baseline/post-change byte counts and gate outputs

## Measurements

| Metric | Value |
|--------|-------|
| Baseline exe size (pre-change) | 116,946,229 bytes |
| Post-change exe size | 49,360,497 bytes |
| Absolute delta | 67,585,732 bytes |
| Percentage reduction | 57.79% |
| Failure floor (plan requirement) | ≥45% reduction, <60,000,000 bytes |
| Result | PASS (well beyond floor, well under ceiling) |

Resolved NuGet package versions (from `src/RigToggle.Windows/obj/project.assets.json`):
- `NAudio.Wasapi/2.3.0`
- `NAudio.Core/2.3.0` (transitive)

Publish directory listing after Task 2's re-publish:
```
RigToggle.App.exe   49360497 bytes
RigToggle.App.pdb      29248 bytes
RigToggle.Core.pdb     21788 bytes
RigToggle.Windows.pdb  21676 bytes
```
No loose managed `.dll` files — single-file shape intact.

### Regression gate verbatim output

`grep -rn "PublishTrimmed" src/`:
```
src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml:4:  PublishTrimmed is explicitly false per CLAUDE.md: IL trimming's static reachability
src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml:31:    <PublishTrimmed>false</PublishTrimmed>
```
(See "Deviations from Plan" below — this two-line grep result was expected given a pre-existing, unmodified top-of-file comment that also mentions the word "PublishTrimmed"; the actual XML element `<PublishTrimmed>` appears exactly once, correctly set to `false`.)

`grep -rin "requestedExecutionLevel\|ApplicationManifest\|PublishAot\|PublishReadyToRun" src/ --include="*.csproj" --include="*.pubxml"`:
```
(no output — exit status 1)
```

`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`: `0 Error(s)`, 4 pre-existing warnings (unrelated xUnit1031 blocking-task-operation warnings in `ToggleOrchestratorTests.cs`, out of scope for this plan).

`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`: `Failed: 0, Passed: 85, Skipped: 0, Total: 85`.

## Decisions Made
- Applied all four levers together in Task 1 since they are independent MSBuild properties/PackageReference changes with no interaction risk between them, matching the plan's task structure.
- Did not add an explicit `NAudio.Core` PackageReference — confirmed `NAudio.Wasapi` alone resolves it transitively at the correct pinned version (2.3.0), and the solution builds and tests cleanly.

## Deviations from Plan

### Auto-fixed Issues

None — no bugs, missing functionality, or blocking issues were encountered.

### Note on the `PublishTrimmed` grep gate literal wording

The plan's acceptance criteria state `grep -rn "PublishTrimmed" src/` "must return exactly one line." In practice it returns two lines: the actual `<PublishTrimmed>false</PublishTrimmed>` XML element (line 31) and a pre-existing top-of-file documentation comment (line 4, present in `win-x64.pubxml` before this plan touched the file, part of the project's established convention of citing prior research docs above each non-obvious property) that also contains the word "PublishTrimmed" in prose. This is not a regression introduced by this plan — the comment already existed verbatim in the file read at Task 1's start. The gate's actual intent (confirmed by re-reading the plan text: "...reading `<PublishTrimmed>false</PublishTrimmed>`. Absent or `true` is a hard failure") is satisfied: `grep -c "<PublishTrimmed>" win-x64.pubxml` returns exactly `1`, `grep -rl "PublishTrimmed" src/` shows the string appears in exactly one file, and that one XML declaration reads `false`. No deviation rule fix was needed — this is a pre-existing documentation/verification-literal mismatch, recorded here per the plan's instruction to record gate output verbatim rather than silently smoothing it over.

---

**Total deviations:** 0 auto-fixed. One documentation note (grep-literal vs. gate-intent mismatch, pre-existing, not introduced by this plan).
**Impact on plan:** None — all four levers applied exactly as specified, all acceptance criteria satisfied in spirit and in the literal XML-element sense.

## Issues Encountered
None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- PERF-01 fully delivered: 57.79% exe size reduction, well past the 45% floor, with `PublishTrimmed` confirmed still `false` and no elevation/AOT/ReadyToRun regressions.
- The cold-start cost of `EnableCompressionInSingleFile` is a known, deliberately deferred tradeoff — PERF-02 (plan 18-06) owns the real rig cold-autostart-boot verification of this tradeoff, per T-18-03-04 in this plan's threat model.
- No blockers for downstream plans in this phase; this plan's file set (`RigToggle.App.csproj`, `win-x64.pubxml`, `RigToggle.Windows.csproj`) is disjoint from plans 18-01, 18-02, 18-04, and 18-05 as scoped.

---
*Phase: 18-cleanup-pass-exe-size-reduction*
*Completed: 2026-08-09*
