---
phase: 24-self-contained-exe-size-reduction
plan: 01
subsystem: infra
tags: [msbuild, dotnet-publish, self-contained-exe, winforms, exe-size]

# Dependency graph
requires:
  - phase: 18-cleanup-pass-exe-size-reduction
    provides: the four existing MSBuild-only size levers (compression, satellite-language trim, invariant globalization, NAudio meta-package split) this plan adds a fifth to
provides:
  - RemoveUnusedDesignerAndVbAssemblies MSBuild <Target> in RigToggle.App.csproj, deny-listing 7 unused WinForms Design-time/VB-compat assemblies from self-contained publish output
  - Fresh same-session before/after byte-count measurement proving the reduction
  - Five automated regression/prohibition audit gates (Task 2) all passing
affects: [26-auto-update, future exe-size-reduction work]

actuals:
  tokens: 610
  tasks: 2
  commits: 1

tech-stack:
  added: []
  patterns: ["Exact-name MSBuild ItemGroup Remove deny-list on @(ResolvedFileToPublish), hooked via AfterTargets=\"ComputeResolvedFilesToPublishList\", as a non-trimming publish-output-shrinking lever"]

key-files:
  created: []
  modified: [src/RigToggle.App/RigToggle.App.csproj]

key-decisions:
  - "Applied exactly one new lever (the 7-file deny-list target); D-01's mandated PackageReference audit closed negative (zero package changes) since WindowsDisplayAPI and NAudio.Wasapi/NAudio.Core are already minimal"
  - "D-02's startup-latency allowance deliberately left unused — the lever removes files before single-file compression runs, so cold-start decompression work is unchanged or marginally reduced"
  - "D-03 (skip UseSystemResourceKeys) extended to DebugType=none for the same reasoning; both confirmed absent by Task 2 Gate C"

patterns-established:
  - "MSBuild deny-list lever pattern: <Target AfterTargets=\"ComputeResolvedFilesToPublishList\"> with an ItemGroup Remove Condition matching exact '%(FileName)%(Extension)' equality — reusable for any future confirmed-unused-assembly exclusion, never a wildcard/Contains() match"


coverage:
  - id: D1
    description: "Fresh self-contained win-x64 publish produces an exe strictly smaller than both the same-session fresh baseline and the recorded v2.1 baseline (49,356,430 bytes)"
    requirement: "PERF-03"
    verification:
      - kind: other
        ref: "Task 1 GATE-1-PASS automated verify command (byte-count comparison)"
        status: pass
    human_judgment: false
  - id: D2
    description: "No excluded static-analysis publish lever (IL trimming, Native AOT, ReadyToRun) introduced; existing PublishTrimmed=false opt-out intact; declined levers (UseSystemResourceKeys, DebugType/DebugSymbols) and elevation manifest still absent; dependency graph (PackageReference set) unchanged"
    requirement: "PERF-03"
    verification:
      - kind: other
        ref: "Task 2 Gates A-D (grep-based negative assertions + empty git diff)"
        status: pass
      - kind: unit
        ref: "dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj (97 passed, 0 failed)"
        status: pass
  - id: D3
    description: "Full Rig -> Normal -> Rig toggle round trip, cold autostart boot, Settings window, and tray-triggered toggle all succeed on real Windows rig hardware running the post-change exe"
    requirement: "PERF-03"
    verification:
      - kind: other
        ref: "Task 3 six-check operator verification on real Windows rig hardware, all PASS"
        status: pass
    human_judgment: true
    rationale: "Requires real Windows rig hardware (CCD display topology control, IPolicyConfig COM audio switching, Moza Companion process, autostart registration) not present in this build environment. Operator ran all six checks on the real rig and confirmed PASS."

requirements-completed: [PERF-03]

duration: 20min
completed: 2026-08-19
status: complete
---

# Phase 24 Plan 01: Self-Contained Exe Size Reduction Summary

**Added one MSBuild deny-list target excluding 7 unused WinForms Design/VB assemblies, cutting the self-contained exe by 2,596,463 bytes (5.26%) — all three tasks complete, including operator rig verification on real Windows hardware.**

## Performance

- **Duration:** 20 min (Tasks 1-2) + operator rig session (Task 3, elapsed time not tracked by this environment)
- **Started:** 2026-08-18T10:38:XXZ (approx, per orchestrator init)
- **Completed (Tasks 1-2):** 2026-08-18T10:41:46Z
- **Completed (Task 3, operator-reported):** 2026-08-19
- **Tasks:** 3 of 3 completed
- **Files modified:** 1 (`src/RigToggle.App/RigToggle.App.csproj`)

## Rig Verification (Task 3)

All six checks performed by the operator on the real Windows rig, using a Windows-native build at commit `67f4bfd`. All PASS.

1. **Byte count on Windows** — **PASS**. Windows-native size: **46,774,899 bytes**, confirmed under the 49,356,430-byte v2.1 baseline (margin: 2,581,531 bytes). This differs from Task 1's Linux-cross-compile AFTER figure (46,770,881 bytes) by 4,018 bytes — consistent with the same class of SDK/platform build drift already documented for the BEFORE measurement (FA-2); the pass/fail margin absorbs it by a wide factor.
2. **Cold launch, window mode** — **PASS**. Operator confirmed: main window, tiles, and toggle switch render normally on cold launch, no exception dialog or missing-assembly error.
3. **Settings window** — **PASS**. Operator confirmed: all controls (monitor grid, audio device pickers, launch-target picker, theme override) render and are interactive.
4. **Full toggle round trip** — **PASS**. Operator confirmed: Rig → Normal → Rig sequence completes correctly, monitors/audio/Moza Companion all behave as expected both directions.
5. **Cold autostart boot** — **PASS**. Operator's own words: "autostart felt about the same as before" — no regression in perceived tray-icon startup timing.
6. **Tray-triggered toggle** — **PASS**. Operator confirmed toggle from the tray icon completes correctly.

No `FileNotFoundException`, `TypeLoadException`, `MissingMethodException`, or unhandled-exception dialog observed during any check.

## Checkpoint Status

**RESOLVED.** Task 3's `type="checkpoint:human-verify" gate="blocking"` checkpoint was not waived, deferred, or marked passed on a build-output byte count alone (per `must_haves.prohibitions` prohibition 2) — it was closed by the operator's own six-check confirmation on real Windows rig hardware, transcribed above. PERF-03 is now marked complete in `.planning/REQUIREMENTS.md`.

## Accomplishments

- Added `RemoveUnusedDesignerAndVbAssemblies` MSBuild `<Target>` to `RigToggle.App.csproj`, matching the file's existing four-lever inline-comment convention (what/why/citation)
- Fresh same-session measurement: 49,367,344 → 46,770,881 bytes, a 2,596,463-byte (5.2595%) reduction — matches `24-RESEARCH.md`'s live-tested figure exactly
- Both size gates cleared: strictly below the same-session fresh baseline AND strictly below the recorded v2.1 baseline (49,356,430 bytes), by a margin of 2,585,549 bytes
- All five Task 2 audit gates passed: no forbidden static-analysis publish lever introduced, existing `PublishTrimmed=false` opt-out intact, all declined levers/manifest forms still absent, dependency graph (PackageReference set) byte-identical, solution builds with 0 errors, and `RigToggle.Tests` reports 97/97 passing with 0 failures
- D-01's mandated publish-output audit closed negative: zero `PackageReference` changes anywhere in `src/`

## Task Commits

1. **Task 1: End-to-end — fresh baseline, add the exclusion target, prove a smaller exe** - `67f4bfd` (feat)
2. **Task 2: Regression and prohibition audit — prove nothing else moved** - no commit (read-only audit task; produced no file changes, per its own `reversibility` rating: "nothing to reverse")
3. **Task 3: Rig verification** - no code commit (operator-verification task; result transcribed into this SUMMARY, phase-completion commit follows)

## Measurement

| Metric | Value |
|---|---|
| Fresh BEFORE byte count (pre-edit tree) | 49,367,344 bytes |
| Fresh AFTER byte count (post-edit tree) | 46,770,881 bytes |
| Absolute delta | 2,596,463 bytes |
| Percentage delta | 5.2595% |
| Recorded v2.1 baseline (49,356,430) margin | AFTER is 2,585,549 bytes below the v2.1 baseline |
| .NET SDK version | 10.0.302 |
| Publish command (identical for before/after) | `export PATH="$HOME/.dotnet:$PATH"; rm -rf src/RigToggle.App/bin/publish/win-x64 && dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true` |

FA-2 note: the fresh BEFORE count (49,367,344) differs from `24-RESEARCH.md`'s previously-measured 49,367,342 by 2 bytes — consistent with the flagged SDK-patch/environment drift, well within the multi-MB pass/fail margin. Both figures still diverge from the historical recorded v2.1 baseline (49,356,430) by ~11 KB, as anticipated by FA-2; this drift is not attributable to any code change (confirmed: the BEFORE measurement was taken on a clean `src/` tree before any edit).

## Audit Gates (Task 2)

All five gates run against the post-Task-1 tree, literal output below.

**Gate A — forbidden-lever absence:**
```
$ grep -rEn '<PublishTrimmed>[[:space:]]*true|<PublishAot>|<PublishReadyToRun>' src/ | wc -l
0
```

**Gate B — PublishTrimmed=false opt-out intact:**
```
$ grep -c '<PublishTrimmed>false</PublishTrimmed>' src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
1
```

**Gate C — declined-lever and manifest absence:**
```
$ grep -rEn '<UseSystemResourceKeys>|<DebugType>|<DebugSymbols>|<ApplicationManifest>|requestedExecutionLevel' src/RigToggle.App/ | wc -l
0
```

**Gate D — zero dependency-graph change:**
```
$ git diff -- src/RigToggle.Windows/RigToggle.Windows.csproj src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml
(empty output)
$ grep -c 'PackageReference' src/RigToggle.Windows/RigToggle.Windows.csproj
2
```
`WindowsDisplayAPI` 1.3.0.13 and `NAudio.Wasapi` 2.3.0 confirmed present and unchanged.

**Gate E — build and test regression:**
```
$ dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded.
    4 Warning(s)   (all pre-existing xUnit1031 warnings in ToggleOrchestratorTests.cs, none mentioning
                    RemoveUnusedDesignerAndVbAssemblies or ResolvedFileToPublish — matches the 4
                    pre-existing baseline warnings documented in 18-VERIFICATION.md)
    0 Error(s)

$ dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj
Passed!  - Failed: 0, Passed: 97, Skipped: 0, Total: 97, Duration: 104 ms
```

## Files Created/Modified
- `src/RigToggle.App/RigToggle.App.csproj` - Added `RemoveUnusedDesignerAndVbAssemblies` MSBuild `<Target>`, deny-listing 7 exact-named WinForms Design-time/VB-compat assemblies from `@(ResolvedFileToPublish)` before single-file bundling

## Decisions Made
None beyond what the plan already specified - executed exactly as written for Tasks 1 and 2. No architectural deviations, no Rule 4 escalations.

## Deviations from Plan

None - Tasks 1 and 2 executed exactly as written. Task 3 was intentionally not fabricated per the plan's own prohibition 2 and this execution's explicit instruction to stop rather than simulate rig verification.

## Issues Encountered

None during Tasks 1-2. `bc` was unavailable in this environment for percentage-delta arithmetic; substituted `awk` (a Rule 3 blocking-issue auto-fix, purely for computing a report figure — no plan file or acceptance criterion depended on `bc` specifically).

## Flagged assumptions resolution

- **FA-1** (PERF-03 returned `unclassified` by the deterministic edge probe): resolved — the concrete acceptance criteria derived from ROADMAP's three success criteria (Task 1's byte-count gate, Task 2's five audit gates, Task 3's six rig checks) are all fully satisfied and documented above.
- **FA-2** (fresh vs. recorded baseline drift): resolved. The Task 3 check 1 native-Windows byte count (46,774,899) differs from Task 1's Linux-cross-compile AFTER figure (46,770,881) by 4,018 bytes — a small, expected platform/toolchain build delta, not attributable to any code difference. Both figures clear the 49,356,430-byte v2.1 baseline by well over 2.5 MB, so the drift has zero practical effect on the pass/fail outcome. Cross-platform build measurement remains a reliable proxy for this class of MSBuild-only change; native measurement is the authoritative number and is now recorded.
- **FA-3** (grep cannot prove the seven assemblies are unreachable): resolved. The operator's six-check rig verification — including the Settings window (the app's heaviest WinForms surface) and a full toggle round trip on real hardware — surfaced no `FileNotFoundException`, `TypeLoadException`, `MissingMethodException`, or unhandled exception. This is the actual evidence the checkpoint existed to produce; no reflection-reachable dependency on the excluded assemblies was found.

## User Setup Required

None beyond the completed rig verification — no ongoing external service configuration required.

## Next Phase Readiness

**Phase complete.** All three tasks done, all ROADMAP success criteria met, PERF-03 marked complete. Phase 25 (Single-Instance Guard) has no dependency on this phase and can proceed independently.

---
*Phase: 24-self-contained-exe-size-reduction*
*Status: complete — all 3 tasks done, rig-verified 2026-08-19*
