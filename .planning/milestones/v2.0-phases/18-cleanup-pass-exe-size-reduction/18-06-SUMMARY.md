---
phase: 18-cleanup-pass-exe-size-reduction
plan: 06
subsystem: verification
tags: [dotnet, cleanup, exe-size, regression-gate, rig-verification]

# Dependency graph
requires:
  - phase: 18-01
    provides: deleted snapshot-persistence subsystem
  - phase: 18-02
    provides: preserved CCD knowledge + deleted monitor-restore subsystem
  - phase: 18-03
    provides: four MSBuild exe-size levers
  - phase: 18-04
    provides: four closed code-quality findings
  - phase: 18-05
    provides: test-double sweep + tree-wide zero-reference audit
provides:
  - "Merged-tree regression gate result for Phase 18"
  - "Final measured exe byte count on the fully-integrated code"
  - "Rig verification record for PERF-02"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  modified: []

key-decisions:
  - "PublishTrimmed grep returned 2 lines (an explanatory comment plus the actual property), not the plan's literally-expected 1 -- verified both lines directly: line 4 is a comment citing CLAUDE.md's rationale, line 31 is the real <PublishTrimmed>false</PublishTrimmed> property. Same false-positive class as prior phases' comment-inclusive acceptance-criteria greps -- treated as verified-compliant, not a defect."

requirements-completed: [CLEANUP-01, CLEANUP-02, PERF-01, PERF-02]

# Metrics
duration: ~20min (Task 1) + rig session (Task 2)
completed: 2026-08-09
---

# Phase 18 Plan 06: Merged Regression Gate + Final Size + Rig Verification Summary

**Phase 18 fully closed: full solution builds clean, 81/81 core tests pass, all CLEANUP-01/02 audits are clean, the merged-tree exe measures 49,356,430 bytes (57.79% below baseline), and all ten rig verification steps confirmed working on real hardware — no FAILs, no waived steps.**

**Measured artifact for rig testing:** `src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe`, **49,356,430 bytes**. Copy this exact file to the rig — do not rebuild there.

## Regression Gate

**Build** (`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`):
```
Build succeeded.
    4 Warning(s)
    0 Error(s)
```
4 warnings, matching the established `xUnit1031` baseline (4 pre-existing blocking-task-in-test lint warnings in `ToggleOrchestratorTests.cs`, unchanged since Phase 17).

**Test** (`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -p:EnableWindowsTargeting=true`):
```
Passed!  - Failed:     0, Passed:    81, Skipped:     0, Total:    81, Duration: 70 ms - RigToggle.Tests.dll (net10.0)
```
Exactly 81, reconciling cleanly against every plan's recorded delta: 85 baseline − 5 (18-01 snapshot tests) + 1 (18-04 new test) = 81. Plans 18-02/18-05 net zero against this project (18-02's 6 removed facts are in `RigToggle.Windows.Tests`, counted separately below).

**RigToggle.Windows.Tests build** (`dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj -p:EnableWindowsTargeting=true`):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
Built, not executed — `Microsoft.WindowsDesktop.App` runtime is not installed in this Linux dev environment. Pre-existing, documented limitation carried since Phase 16, not a Phase 18 regression. `[Fact]` count verified instead (below).

## CLEANUP-01 Closing Audit

All four greps return empty (exit 1 = no matches, confirmed):

| Check | Command | Result |
|---|---|---|
| No `.Restore(` call sites | `grep -rn "\.Restore(" src --include="*.cs"` | empty — **PASS** |
| No snapshot types | `grep -rn "ISnapshotStore\|JsonSnapshotStore\|StateSnapshot\|InMemorySnapshotStore" src --include="*.cs"` | empty — **PASS** |
| No removed helpers/fields | `grep -rn "RestoreViaReconstruction\|CopyOutputTechnology\|AssignSource\|_originalPathsCache\|throwOnRestore" src --include="*.cs"` | empty — **PASS** |
| No dead test prefix | `grep -rn "SnapshotStore_" src --include="*.cs"` | empty — **PASS** |

**Knowledge-preservation precondition** (CLEANUP-01 requires this before the deletion counts as satisfied):
```
grep -c "ccd-topology-restore-findings" .planning/debug/knowledge-base.md → 1
grep -c "<OutputTechnology>k__BackingField" .planning/debug/knowledge-base.md → 2
grep -c "SDC_ALLOW_CHANGES" .planning/debug/knowledge-base.md → 2
```
**PASS** — the `ccd-topology-restore-findings` entry landed in the merged tree and contains both fragile identifiers named in the acceptance criteria.

## CLEANUP-02 Closing Audit

| Check | Command | Result |
|---|---|---|
| IN-04 dead branch removed | `grep -rn "No audio devices detected\|items.Count == 0" src/RigToggle.App/SettingsForm.cs` | empty — **PASS** |
| IN-02 old capitalization gone | `grep -c "Skipped (not configured)" src/RigToggle.Core/ToggleResultFormatter.cs` | `0` — **PASS** |
| WindowsMonitorControllerTests trimmed | `grep -c "\[Fact\]" src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` | `8` — **PASS** (14→8, matches 18-02) |
| WindowsMonitorController.cs shrunk | `wc -l < src/RigToggle.Windows/WindowsMonitorController.cs` | `352` — **PASS** (under 400; was 727 pre-phase) |

**Net line delta across `src/` for the whole phase** (`git diff --stat` against the pre-phase commit):
```
26 files changed, 162 insertions(+), 913 deletions(-)
```
**Net: 751 lines removed.** This is the quantitative "measurably less duplication/cruft" evidence for ROADMAP success criterion 2.

## PERF-01 Final Measurement

```
rm -rf src/RigToggle.App/bin/publish/win-x64/
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
```
Publish succeeded, 0 errors.

```
stat -c %s src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe → 49356430
```

| Metric | Value |
|---|---|
| Pre-phase baseline (18-RESEARCH.md, measured live) | 116,946,229 bytes |
| Post-Plan-18-03 (isolated measurement) | 49,360,387 bytes |
| **Final merged-tree measurement** | **49,356,430 bytes** |
| Absolute reduction (baseline → final) | 67,589,799 bytes |
| Percentage reduction | **57.79%** |

The final merged number is 3,957 bytes smaller than 18-03's isolated measurement — consistent with the plan's own prediction ("the merged tree also deletes several hundred lines of dead code, so the final figure should land at or slightly below the research number"). Under the 60,000,000-byte ceiling; well past the 45% reduction floor.

**Publish directory contents:**
```
RigToggle.App.exe    49,356,430 bytes
RigToggle.App.pdb
RigToggle.Core.pdb
RigToggle.Windows.pdb
```
Genuine single-file exe — no loose managed `.dll` files beside it (`.pdb` debug-symbol files are expected and not managed assemblies).

## Prohibited-Lever Regression Gates

**`PublishTrimmed`:**
```
grep -rn "PublishTrimmed" src/
src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml:4:  PublishTrimmed is explicitly false per CLAUDE.md: IL trimming's static reachability
src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml:31:    <PublishTrimmed>false</PublishTrimmed>
```
2 lines, not the plan's literally-expected 1 — line 4 is an explanatory comment citing CLAUDE.md's rationale (pre-existing, added in Plan 18-03), line 31 is the real property. Both confirm the same fact: **PASS** — `PublishTrimmed` is `false`.

**Elevation/AOT/ReadyToRun:**
```
grep -rin "requestedExecutionLevel\|ApplicationManifest\|PublishAot\|PublishReadyToRun" src/ --include="*.csproj" --include="*.pubxml"
```
Empty — **PASS**. No elevation manifest, no AOT, no ReadyToRun anywhere in the merged tree.

## Rig Verification Script (for Task 2)

Exe to test: **`src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe`, 49,356,430 bytes.**

See `18-06-PLAN.md` Task 2 for the full ten-step script (cold autostart boot, full toggle round trip, cleanup-pass spot checks). Presented to the operator in this session; results to be appended below once received.

## Rig Verification

Operator (Blaz Pivk) ran all ten steps on real rig hardware using the exact measured exe (`RigToggle.App.exe`, 49,356,430 bytes) and reported all steps working.

| # | Step | Verdict | Evidence |
|---|------|---------|----------|
| 1 | Cold autostart boot — tray icon appears | PASS | Operator confirmed |
| 2 | Reboot, autostart runs genuinely cold | PASS | Operator confirmed |
| 3 | Cold-boot timing impression (the PERF-01 compression risk) | PASS | Operator's own words: "it's hard to tell because my computer has to load a lot of startup apps but the icon did pop up quite fast." No noticeable regression — consistent with the documented sub-second-delta-is-expected tradeoff of `EnableCompressionInSingleFile`, and the operator's own machine has enough concurrent startup load that a small delta would be masked regardless. |
| 4 | A/B against old exe if slow | N/A | Not needed — no slowdown observed in step 3 |
| 5 | Rig-mode toggle: monitors/audio/companion app | PASS | Operator confirmed |
| 6 | Result checklist correct, lowercase `skipped (not configured)` wording | PASS | Operator confirmed |
| 7 | Normal-mode toggle: monitor set/audio/companion minimize | PASS | Operator confirmed |
| 8 | Close + relaunch reports correct mode (rewritten `Program.cs` bootstrap) | PASS | Operator confirmed |
| 9 | Settings audio dropdowns + sentinel selection | PASS | Operator confirmed |
| 10 | Manual monitor panel: list, enable/disable, last-monitor rejection | PASS | Operator confirmed |

**Verdict: PERF-02 fully confirmed on real hardware.** Cold autostart boot shows no perceptible regression from the compression lever, and the full toggle round trip plus both cleanup-pass spot checks (Settings audio pickers, manual monitor panel) all behave correctly after the dead-code deletion and packaging changes. No FAILs, no waived steps.

## Phase 18 Final Verdict

All four ROADMAP success criteria evidenced:
- **CLEANUP-01**: snapshot-restore subsystem provably absent (4 clean tree-wide audits), rig-specific CCD knowledge preserved first in `knowledge-base.md` before deletion.
- **CLEANUP-02**: 751 net lines removed across `src/`, four previously-deferred review findings closed, no user-facing behavior change confirmed on the rig (steps 5-10).
- **PERF-01**: exe reduced 57.79% (116.9 MB → 49.4 MB) via four MSBuild-only levers, `PublishTrimmed` confirmed still `false`, no elevation manifest/AOT/ReadyToRun.
- **PERF-02**: cold autostart boot and full toggle round trip both confirmed working on real rig hardware (steps 1-8).

---
*Phase: 18-cleanup-pass-exe-size-reduction*
*Completed: 2026-08-09*
