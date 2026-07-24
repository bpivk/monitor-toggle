---
phase: 01-monitor-disable-feasibility-spike
plan: 02
subsystem: docs
tags: [user-facing-docs, spike, monitor-detach, go-no-go]

# Dependency graph
requires: ["01-01"]
provides:
  - "spike/RUN-INSTRUCTIONS.md — SDK install + build + run-per-mode + PASS/FAIL interpretation guide"
  - "spike/RESULTS-TEMPLATE.md — fill-in-the-blanks go/no-go results capture template"
  - "spike/FALLBACK.md — separate, manually-invoked elevated pnputil escalation path"
affects: [01-monitor-disable-feasibility-spike, phase-4-orchestration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Three-document user round-trip pattern for Linux-sandbox-cannot-run-Windows-code phases: instructions doc + results template + separate escalation doc"

key-files:
  created:
    - spike/RUN-INSTRUCTIONS.md
    - spike/RESULTS-TEMPLATE.md
    - spike/FALLBACK.md
  modified: []

key-decisions:
  - "Kept all three docs strictly markdown/commands-only — no C# source duplicated into the docs, per plan's execution boundary"
  - "FALLBACK.md explicitly forbids elevating the spike .exe itself, keeping elevation isolated to a separately-opened admin terminal (D-08 / Pitfall D)"

requirements-completed: []

# Metrics
duration: 2min
completed: 2026-07-24
---

# Phase 1 Plan 2: Spike Run/Results/Fallback Documentation Summary

**Three user-facing markdown docs (RUN-INSTRUCTIONS, RESULTS-TEMPLATE, FALLBACK) that turn the Wave-1 spike tool into a self-contained round-trip the user can execute on the rig PC and report back from, keeping the admin pnputil escalation strictly separate from the primary non-elevated tool**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-07-24T11:18:54Z
- **Completed:** 2026-07-24T11:20:28Z (approx)
- **Tasks:** 3 completed
- **Files modified:** 3 created

## Accomplishments
- Produced `spike/RUN-INSTRUCTIONS.md`: ordered, copy-pasteable steps covering .NET SDK confirm/install (winget + manual fallback, since only VS Code is confirmed on the rig PC per D-02), getting the project onto the rig PC (copy-existing-files or scaffold-from-scratch), build, running all three CLI modes (`--list`/`--disable`/`--verify`), how to read PASS vs FAIL output, an AMD Adrenalin-service troubleshooting note for the delayed-recheck (Pitfall C) failure mode, an explicit non-elevated instruction, and pointers to the other two docs.
- Produced `spike/RESULTS-TEMPLATE.md`: a fill-in-the-blanks report capturing environment (`winver`, GPU/driver, DisplayPort target identity), before-disable dual-source counts (WindowsDisplayAPI `--list` + `Screen.AllScreens`), immediate and ~20-second delayed PASS/FAIL lines with Pitfall A/C call-outs, restore result, an elevation/UAC observation field, and a three-way go/no-go decision (`GO` / `GO (with fallback)` / `NO-GO`) mapped to ROADMAP Phase 1 Success Criterion #3.
- Produced `spike/FALLBACK.md`: documents the admin `pnputil /disable-device`/`/enable-device` escalation path as a strictly separate, manually-invoked mechanism run in its own elevated terminal — explicitly forbidding running the spike `.exe` itself as administrator — with a `Get-PnpDevice` lookup step, a re-verification step via the non-elevated spike's `--verify`, a warning against the deprecated `devcon.exe`, and a note that a "GO (with fallback)" outcome makes Phase 4's elevated-helper-process isolation mandatory (Assumption A1).

## Task Commits

Each task was committed atomically:

1. **Task 1: Write RUN-INSTRUCTIONS.md** - `db87c1d` (docs)
2. **Task 2: Write RESULTS-TEMPLATE.md** - `fb50784` (docs)
3. **Task 3: Write FALLBACK.md** - `300dcaa` (docs)

## Files Created/Modified
- `spike/RUN-INSTRUCTIONS.md` - SDK install/build/run/interpret guide for all three CLI modes, non-elevated by instruction
- `spike/RESULTS-TEMPLATE.md` - dual-source before/after capture, immediate+delayed checks, restore, elevation observation, three-way go/no-go decision
- `spike/FALLBACK.md` - separate elevated pnputil escalation path with re-verification and Phase-4 implications

## Decisions Made
- Followed the plan's exact structure and content requirements for all three docs with no substitutions.
- Kept every command in fenced code blocks, no embedded C# source in any of the three files (execution boundary from the plan's `<objective>`).

## Deviations from Plan

None - plan executed exactly as written. All three tasks' automated grep-based acceptance checks passed on the first attempt with no rework needed.

## Issues Encountered
None.

## User Setup Required

**The user must follow `spike/RUN-INSTRUCTIONS.md` on their Windows rig PC** to actually answer this phase's go/no-go question — the Linux execution sandbox cannot compile or run Windows-native code (D-01). In brief:
1. Confirm/install .NET SDK 10.0.x per Step 0 of `RUN-INSTRUCTIONS.md`.
2. Build and run the spike tool's three modes (`--list`, `--disable <index>`, `--verify`) from an ordinary (non-elevated) terminal.
3. Fill in `spike/RESULTS-TEMPLATE.md` with the observed results.
4. If the primary approach FAILs, follow `spike/FALLBACK.md` from a separately-opened elevated terminal, then re-verify from the non-elevated spike tool.
5. Report the filled-in `RESULTS-TEMPLATE.md` back — that is the actual go/no-go signal for Phase 1, not this plan's completion.

## Next Phase Readiness
- All three documentation artifacts exist and pass every content-assertion acceptance check defined in the plan (this plan's verification is markdown-content-only by design; no `dotnet build`/`dotnet run` was — or could be — executed from this Linux sandbox).
- Phase 1's actual go/no-go decision (ROADMAP Success Criteria #1-#4) remains gated on the user building/running the spike tool on the rig PC and reporting back a filled-in `RESULTS-TEMPLATE.md` — that follow-up interaction, not this plan, closes out the phase.
- No blockers to that follow-up interaction — both this plan (docs) and plan `01-01` (spike tool source) are complete.

---
*Phase: 01-monitor-disable-feasibility-spike*
*Completed: 2026-07-24*

## Self-Check: PASSED

- FOUND: spike/RUN-INSTRUCTIONS.md
- FOUND: spike/RESULTS-TEMPLATE.md
- FOUND: spike/FALLBACK.md
- FOUND: .planning/phases/01-monitor-disable-feasibility-spike/01-02-SUMMARY.md
- FOUND commit: db87c1d
- FOUND commit: fb50784
- FOUND commit: 300dcaa
