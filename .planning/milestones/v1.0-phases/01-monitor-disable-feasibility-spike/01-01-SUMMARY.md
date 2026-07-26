---
phase: 01-monitor-disable-feasibility-spike
plan: 01
subsystem: infra
tags: [windows-ccd, displayapi, dotnet10, spike, monitor-detach]

# Dependency graph
requires: []
provides:
  - "spike/MonitorDetachSpike/MonitorDetachSpike.csproj — net10.0-windows console project referencing WindowsDisplayAPI 1.3.0.13, UseWindowsForms enabled, no elevation manifest"
  - "spike/MonitorDetachSpike/Program.cs — --list / --disable (dual-oracle verify + delayed re-check + restore) / --verify CLI spike tool"
affects: [01-monitor-disable-feasibility-spike, phase-4-orchestration]

# Tech tracking
tech-stack:
  added: ["WindowsDisplayAPI 1.3.0.13 (NuGet, CCD API wrapper)"]
  patterns:
    - "Topology-path-removal via PathInfo.ApplyPathInfos(allowChanges: true) for true CCD-level monitor detach"
    - "Dual-oracle verification (WindowsDisplayAPI re-query + System.Windows.Forms.Screen.AllScreens) rather than trusting a single API's success return"
    - "Delayed (~20s) re-verification pass to catch DisplayPort hotplug re-detection"

key-files:
  created:
    - spike/MonitorDetachSpike/MonitorDetachSpike.csproj
    - spike/MonitorDetachSpike/Program.cs
  modified: []

key-decisions:
  - "Spike is intentionally throwaway: no ApplicationManifest/elevation, no publish/trimming settings — packaging is out of scope until Phase 5"
  - "snapshot.json is an audit-trail-only JSON dump of PathInfo.ToString() output; the actual restore re-applies the in-memory originalActivePaths array, never a JSON round-trip"

patterns-established:
  - "Non-elevated (asInvoker) by construction: absence of any elevation manifest element, no pnputil/device-node calls anywhere in the primary code path (D-08)"

requirements-completed: []

# Metrics
duration: 6min
completed: 2026-07-24
---

# Phase 1 Plan 1: Monitor-Detach Feasibility Spike Tool Summary

**Throwaway .NET 10 console spike using WindowsDisplayAPI's CCD topology-path-removal (PathInfo.ApplyPathInfos) with dual-oracle (WindowsDisplayAPI + Screen.AllScreens) detach verification and delayed hotplug re-check, to be built/run by the user on the AMD Radeon rig PC**

## Performance

- **Duration:** 6 min
- **Started:** 2026-07-24T11:10:22Z (base commit)
- **Completed:** 2026-07-24T11:16:17Z
- **Tasks:** 2 completed
- **Files modified:** 2 created

## Accomplishments
- Produced `MonitorDetachSpike.csproj` targeting `net10.0-windows`, referencing `WindowsDisplayAPI` 1.3.0.13, with `UseWindowsForms` enabled purely to unlock `Screen.AllScreens` as a second verification oracle — no elevation manifest, no publish/trimming settings (throwaway spike, packaging deferred to Phase 5)
- Produced `Program.cs` implementing three CLI modes: `--list` (enumerate active display paths and print index/friendly-name/device-path/IsGDIPrimary/OutputTechnology for user identification of the DisplayPort/primary target), `--disable <index>` (bounds-checked disable with a pre-mutation snapshot, `ApplyPathInfos(allowChanges: true)` detach, immediate dual-oracle verification, a ~20s delayed re-verification to catch hotplug re-detection, and a final restore-on-Enter), and `--verify` (report current active-path count and screen count on demand)
- Bounds-checks the `--disable` index against the actual active-path array length before ever indexing into it, printing an error containing `valid range is 0..<upperBound>` and returning non-zero rather than throwing or acting on the wrong path
- Contains zero elevated-operation calls (no pnputil, no device-node disable APIs) anywhere in the file — the tool is asInvoker by construction

## Task Commits

Each task was committed atomically:

1. **Task 1: Create the spike project file (MonitorDetachSpike.csproj)** - `1e3eeb0` (feat)
2. **Task 2: Implement the spike logic (Program.cs)** - `74fcc11` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified
- `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` - net10.0-windows console project, WindowsDisplayAPI 1.3.0.13 reference, UseWindowsForms enabled, no elevation manifest
- `spike/MonitorDetachSpike/Program.cs` - argument-dispatched --list / --disable / --verify logic, bounds-checked index, dual-oracle detach verification with delayed re-check, restore-on-Enter

## Decisions Made
- Followed the plan's exact API surface (`PathInfo.GetActivePaths`, `PathInfo.ApplyPathInfos(allowChanges: true)`, `Screen.AllScreens`) with no substitutions.
- `snapshot.json` is documented in-code as an audit trail only — the real restore call re-applies the in-memory `originalActivePaths` array captured at the start of `--disable`, never re-parsing the JSON, since `PathInfo.ToString()` is not designed to round-trip.

## Deviations from Plan

**1. [Rule 1 - Bug] Reworded a doc comment that accidentally triggered its own forbidden-literal acceptance check**

- **Found during:** Task 1 and Task 2 (verification step)
- **Issue:** The plan's acceptance criteria forbid the literal strings `ApplicationManifest`, `requireAdministrator`, `PublishTrimmed` (csproj) and `pnputil`/`Get-PnpDevice`/`CM_Disable_DevNode`/`requireAdministrator` (Program.cs) appearing anywhere in the file — including inside explanatory comments describing *why* those things are absent. My first draft of both files included comments like "No ApplicationManifest element..." and "...never invokes pnputil / CM_Disable_DevNode..." which are negative-context sentences but still contain the literal forbidden substrings, so the automated grep-based verification failed.
- **Fix:** Reworded both comments to convey the same intent (no elevation manifest present; no elevated device-node calls anywhere) without using the literal forbidden strings.
- **Files modified:** `spike/MonitorDetachSpike/MonitorDetachSpike.csproj`, `spike/MonitorDetachSpike/Program.cs`
- **Verification:** Re-ran the plan's exact grep-based acceptance checks for both tasks; both now pass (`CSPROJ_OK` equivalent conditions all true; forbidden-string grep returns exit 1/no match).
- **Committed in:** `1e3eeb0` (Task 1 commit), `74fcc11` (Task 2 commit) — both fixes were made before the respective task commit, so no separate fix commit was needed.

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Cosmetic wording fix only; no functional or architectural change. No scope creep.

## Issues Encountered
None beyond the deviation above.

## User Setup Required

**The user must build and run this spike on their own Windows rig PC** — the Linux execution sandbox cannot compile or run Windows-native code (D-01). See plan `01-02` for the full RUN-INSTRUCTIONS.md covering .NET SDK installation and step-by-step usage. In brief, on the rig PC in an ordinary (non-elevated) terminal:
1. Confirm/install .NET SDK 10.0.x (`dotnet --list-sdks`; if missing, `winget install --id Microsoft.DotNet.SDK.10 -e`)
2. `cd spike/MonitorDetachSpike && dotnet run -- --list` to identify the DisplayPort/primary monitor's index
3. `dotnet run -- --disable <index>` to run the detach + dual-oracle verify + delayed re-check + restore cycle
4. Report the PASS/FAIL results back — that is the actual go/no-go signal for Phase 1, not this plan's completion.

## Next Phase Readiness
- Both spike source files exist and pass every source-content acceptance check defined in the plan (this plan's verification is source-content-only by design; no `dotnet build`/`dotnet run` was — or could be — executed from this Linux sandbox).
- The real functional verification (does the AMD Radeon/DisplayPort rig actually drop the monitor from both enumeration oracles) is blocked on the user building and running this tool on Windows and reporting results back — that follow-up interaction, not this plan, gates Phase 1's go/no-go decision.
- No blockers to plan `01-02` (packaging RUN-INSTRUCTIONS/FALLBACK docs for the user) — it can proceed independently since it only documents how to run the artifacts produced here.

---
*Phase: 01-monitor-disable-feasibility-spike*
*Completed: 2026-07-24*

## Self-Check: PASSED

- FOUND: spike/MonitorDetachSpike/MonitorDetachSpike.csproj
- FOUND: spike/MonitorDetachSpike/Program.cs
- FOUND commit: 1e3eeb0
- FOUND commit: 74fcc11
