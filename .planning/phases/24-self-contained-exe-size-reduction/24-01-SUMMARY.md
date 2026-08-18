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

requirements-completed: []  # PERF-03 NOT marked complete — Task 3 (blocking rig-hardware checkpoint) is still pending

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
    verification: []
    human_judgment: true
    rationale: "Requires real Windows rig hardware (CCD display topology control, IPolicyConfig COM audio switching, Moza Companion process, autostart registration) not present in this build environment. Task 3 is a blocking checkpoint pending operator execution on the rig — see 'Checkpoint Status' below."

duration: 20min
completed: 2026-08-18
status: halted
---

# Phase 24 Plan 01: Self-Contained Exe Size Reduction Summary

**Added one MSBuild deny-list target excluding 7 unused WinForms Design/VB assemblies, cutting the self-contained exe by 2,596,463 bytes (5.26%) — Tasks 1-2 complete and all automated gates pass; Task 3 (blocking rig-hardware verification) is pending operator execution.**

## Performance

- **Duration:** 20 min (Tasks 1-2 only; Task 3 not yet run)
- **Started:** 2026-08-18T10:38:XXZ (approx, per orchestrator init)
- **Completed (Tasks 1-2):** 2026-08-18T10:41:46Z
- **Tasks:** 2 of 3 completed (Task 3 blocked on real Windows rig hardware)
- **Files modified:** 1 (`src/RigToggle.App/RigToggle.App.csproj`)

## Checkpoint Status

**BLOCKED at Task 3 — `type="checkpoint:human-verify" gate="blocking"`.**

This build environment has no Windows GUI, no rig monitor, no rig audio endpoint, and no Moza Companion install. Task 3 requires all six checks to be performed on real Windows rig hardware by the operator, using a Windows-built (not Linux-cross-compiled) `RigToggle.App.exe` produced from commit `67f4bfd`. Per the plan's `must_haves.prohibitions` (prohibition 2) and the plan's own note, this checkpoint "must not be waived, deferred to a later phase, or marked passed on a build-output byte count" — so it was not fabricated. PERF-03 is **not** marked complete in REQUIREMENTS.md and the phase does not close until the operator reports the six checks.

**What the operator needs to do:**
1. Get a Windows-native build of `RigToggle.App.exe` at commit `67f4bfd` — either run the release publish command natively on the rig (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64`, omitting `EnableWindowsTargeting` since that flag is only needed for the Linux cross-compile), or pull the artifact from the repo's `release.yml` `windows-latest` workflow run against this commit.
2. Replace the installed exe with this build.
3. Work through the six numbered checks in Task 3's `<how-to-verify>` (byte count on Windows; cold launch; Settings window; full Rig→Normal→Rig round trip; cold autostart boot; tray-triggered toggle), reporting each as PASS/FAIL with a short observational note.
4. Report back — a follow-up execution pass will transcribe the result into this SUMMARY's Rig Verification section, mark PERF-03 complete in REQUIREMENTS.md, and make the final phase-completion commit. If any check FAILs, `git revert 67f4bfd` restores the prior publish output exactly, and the failing check's exact exception text plus the implicated assembly should be reported so the deny-list can be narrowed.

## Accomplishments

- Added `RemoveUnusedDesignerAndVbAssemblies` MSBuild `<Target>` to `RigToggle.App.csproj`, matching the file's existing four-lever inline-comment convention (what/why/citation)
- Fresh same-session measurement: 49,367,344 → 46,770,881 bytes, a 2,596,463-byte (5.2595%) reduction — matches `24-RESEARCH.md`'s live-tested figure exactly
- Both size gates cleared: strictly below the same-session fresh baseline AND strictly below the recorded v2.1 baseline (49,356,430 bytes), by a margin of 2,585,549 bytes
- All five Task 2 audit gates passed: no forbidden static-analysis publish lever introduced, existing `PublishTrimmed=false` opt-out intact, all declined levers/manifest forms still absent, dependency graph (PackageReference set) byte-identical, solution builds with 0 errors, and `RigToggle.Tests` reports 97/97 passing with 0 failures
- D-01's mandated publish-output audit closed negative: zero `PackageReference` changes anywhere in `src/`

## Task Commits

1. **Task 1: End-to-end — fresh baseline, add the exclusion target, prove a smaller exe** - `67f4bfd` (feat)
2. **Task 2: Regression and prohibition audit — prove nothing else moved** - no commit (read-only audit task; produced no file changes, per its own `reversibility` rating: "nothing to reverse")
3. **Task 3: Rig verification** - NOT STARTED (blocking checkpoint, pending operator on real Windows rig hardware)

**Plan metadata:** pending (this SUMMARY commit; final phase-completion commit deferred until Task 3 resolves)

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

- **FA-1** (PERF-03 returned `unclassified` by the deterministic edge probe): resolved as intended — the concrete acceptance criteria derived from ROADMAP's three success criteria (Task 1's byte-count gate, Task 2's five audit gates) are fully satisfied and documented above. Only Task 3's operator-judgment criterion remains open, which was always the intended shape (grep/build gates cannot prove hardware-level correctness).
- **FA-2** (fresh vs. recorded baseline drift): further confirmed this session — a second fresh Linux cross-compile BEFORE measurement (49,367,344) again differs from the recorded 49,356,430 by ~11 KB, and by only 2 bytes from `24-RESEARCH.md`'s own prior same-session figure (49,367,342). Impact remains negligible (bar clears by >2.5 MB either way). Not yet fully closed — Task 3 check 1 (native-Windows byte count) is what resolves the Linux-cross-compile-vs-native-build attribution question, and that check has not yet run.
- **FA-3** (grep cannot prove the seven assemblies are unreachable): not yet resolved — this is precisely what Task 3's blocking rig checkpoint exists to close. No shortcut was taken; the checkpoint is reported as pending, not waived.

## User Setup Required

None - no external service configuration required. Operator action needed is the Task 3 rig verification described above, not environment/service setup.

## Next Phase Readiness

**Not ready to close this phase.** Task 3 (blocking rig-hardware checkpoint) must be completed by the operator before:
- PERF-03 can be marked complete in `.planning/REQUIREMENTS.md`
- `.planning/ROADMAP.md` can show Phase 24 as fully done
- The final phase-metadata commit can be made

Once the operator reports the six rig checks, resume execution from Task 3: transcribe the Rig Verification results into this SUMMARY, update requirements/roadmap/state, and make the final commit. If any check FAILs, `git revert 67f4bfd` isolates the exclusion target as the sole candidate cause, and the failing assembly should be identified so the deny-list can be narrowed rather than the whole lever abandoned.

---
*Phase: 24-self-contained-exe-size-reduction*
*Status: halted at Task 3 (blocking checkpoint) — Tasks 1-2 complete 2026-08-18*
