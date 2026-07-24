---
phase: 01-monitor-disable-feasibility-spike
verified: 2026-07-24T12:00:00Z
status: human_needed
score: 14/14 deliverable must-haves verified (tool + docs); 0/4 ROADMAP Success Criteria empirically closed
overrides_applied: 0
human_verification:
  - test: "Build MonitorDetachSpike on the actual rig PC (per spike/RUN-INSTRUCTIONS.md) and run `--list` / `--disable <index>` / `--verify` from an ordinary (non-elevated) terminal"
    expected: "Tool compiles under net10.0-windows, `--list` prints the DisplayPort/primary monitor's index, `--disable <index>` prints PASS on both the immediate and ~20s delayed check (monitor gone from both WindowsDisplayAPI and Screen.AllScreens), and restore-on-Enter brings the monitor back"
    why_human: "The Linux verification sandbox has no .NET SDK and cannot execute Windows-native code or talk to the rig's AMD Radeon/DisplayPort driver — this is the one thing the entire phase exists to determine, and no static review substitutes for it (D-01)"
  - test: "Fill in spike/RESULTS-TEMPLATE.md with the observed results and check exactly one of the three Go/No-Go boxes"
    expected: "A completed RESULTS-TEMPLATE.md exists recording winver, dual-source before/after counts, immediate + delayed PASS/FAIL lines, restore result, UAC/elevation observation, and a checked GO / GO (with fallback) / NO-GO decision"
    why_human: "This is ROADMAP Phase 1 Success Criterion #3 ('a documented go/no-go decision') — as of this verification, spike/RESULTS-TEMPLATE.md is still the unfilled template (13 `___` placeholders, 0 checked boxes), so this criterion has not actually been satisfied yet despite ROADMAP.md marking the phase complete"
  - test: "If the primary approach FAILs, run the spike/FALLBACK.md pnputil escalation from a separate elevated terminal and re-verify with `--verify`"
    expected: "Either confirms GO (with fallback) or NO-GO; if GO (with fallback), Phase 4's elevation-isolation design becomes mandatory rather than optional (Assumption A1)"
    why_human: "Also requires the rig hardware and an elevated terminal; cannot be exercised from this sandbox"
---

# Phase 1: Monitor-Disable Feasibility Spike Verification Report

**Phase Goal:** Determine whether true OS-level monitor disable (not a power-off) is achievable on the actual rig hardware and GPU driver, before any other architecture or GUI work is treated as settled.
**Verified:** 2026-07-24T12:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Scope Note

Per the verification task's framing, this phase is a throwaway spike with no mapped REQUIREMENTS. The deliverable this session can actually inspect is the **spike tool + docs** (Program.cs, csproj, RUN-INSTRUCTIONS.md, RESULTS-TEMPLATE.md, FALLBACK.md) — not the empirical hardware result, which requires a Windows machine this sandbox does not have. Both plans' own `<verification>` sections and both SUMMARY.md files explicitly acknowledge this: "Phase 1's go/no-go decision is gated on the user-reported result... not on this plan completing." This report therefore verifies two things separately:

1. **Deliverable quality** — does the tool correctly implement true CCD-level detach (not power-off), and do the docs give a non-expert single operator enough to build/run/interpret on the real rig? (fully verifiable by code/doc review)
2. **ROADMAP Success Criteria** — has the actual go/no-go answer been determined and recorded? (requires the human round-trip; **not yet done**, see below)

## Goal Achievement

### Observable Truths (deliverable level — code/doc review)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Spike enumerates active display paths via `PathInfo.GetActivePaths` | ✓ VERIFIED | `Program.cs:55,74` — `PathInfo.GetActivePaths(virtualModeAware: false)` used in `RunList` and `RunDisable` |
| 2 | Spike detaches the chosen monitor via `PathInfo.ApplyPathInfos(..., allowChanges: true)` on a path array that omits it | ✓ VERIFIED | `Program.cs:100-107` — `reducedPaths` excludes `targetIndex`; `ApplyPathInfos(reducedPaths, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false)`. This is the true CCD topology-path-removal mechanism (`SetDisplayConfig` with `SDC_APPLY|SDC_USE_SUPPLIED_DISPLAY_CONFIG|SDC_ALLOW_CHANGES`), **not** a DPMS/power-off call — matches CLAUDE.md's "Must achieve true OS-level display disable" constraint and 01-RESEARCH.md Pattern 1 exactly |
| 3 | Detach verified via TWO independent oracles, re-checked after a delay | ✓ VERIFIED | `Program.cs:127-153` `VerifyOnce` cross-checks `PathInfo.GetActivePaths()` (re-query) AND `Screen.AllScreens.Length`; called once immediately (`Program.cs:110`) and again after `Thread.Sleep(20000)` (`Program.cs:112-116`) to catch DisplayPort hotplug re-detection (Pitfall C) |
| 4 | Tool is non-elevated by construction | ✓ VERIFIED | `MonitorDetachSpike.csproj` has no `<ApplicationManifest>`/`requireAdministrator`; `Program.cs` contains zero references to `pnputil`, `Get-PnpDevice`, `CM_Disable_DevNode` (grep-confirmed, exit 1/no match) |
| 5 | Out-of-range `--disable` index errors and exits non-zero rather than throwing | ✓ VERIFIED | `Program.cs:76-85` — bounds-checked before any array indexing, prints `valid range is 0..{upperBound}`, `return 1` |
| 6 | Restore returns topology to original state | ✓ VERIFIED | `Program.cs:118-122` — re-applies `originalActivePaths` (the in-memory array, not a JSON round-trip) on Enter, prints final `Screen.AllScreens.Length` |
| 7 | `snapshot.json` is an audit trail only, restore uses in-memory array | ✓ VERIFIED | `Program.cs:93-98` comment + code confirm; matches plan's explicit anti-pattern warning about not round-tripping `PathInfo` through JSON |
| 8 | RUN-INSTRUCTIONS.md gives a non-expert (only-VS-Code-installed) user a complete SDK-install → build → run → interpret path | ✓ VERIFIED | `spike/RUN-INSTRUCTIONS.md` Steps 0-5: `dotnet --list-sdks`, `winget install --id Microsoft.DotNet.SDK.10 -e` fallback to manual download, file placement + scaffold alternative, `dotnet build`, all three run modes with substitution instructions, explicit PASS/FAIL definition, troubleshooting for the Pitfall C symptom (Adrenalin service), explicit non-elevation warning, pointers to RESULTS-TEMPLATE.md/FALLBACK.md |
| 9 | RESULTS-TEMPLATE.md captures everything needed for a defensible go/no-go record | ✓ VERIFIED (template structure) | `spike/RESULTS-TEMPLATE.md` has fields for winver, GPU/driver, DisplayPort confirmation, dual-source before/after, immediate + delayed PASS/FAIL, restore result, elevation/UAC observation, and 3-way decision checkboxes tied to ROADMAP SC#3 |
| 10 | FALLBACK.md keeps the admin path strictly separate and manually invoked | ✓ VERIFIED | `spike/FALLBACK.md` — explicit "SEPARATE, MANUALLY-INVOKED" framing, forbids elevating the spike `.exe` itself, `Get-PnpDevice`→`pnputil /disable-device`(elevated)→`--verify`(non-elevated)→`pnputil /enable-device`, warns against deprecated `devcon.exe` |
| 11 | No debt markers / stub patterns in delivered files | ✓ VERIFIED | `grep -rniE "TBD\|FIXME\|XXX\|TODO\|HACK\|PLACEHOLDER"` across `spike/` returns no matches |
| 12 | D-05 identification (OutputTechnology + IsGDIPrimary) surfaced to the user | ✓ VERIFIED | `Program.cs:63-67` prints both fields per row in `--list` |
| 13 | D-06 pass criterion is enumeration-only, no game-launch requirement baked into the tool | ✓ VERIFIED | `VerifyOnce` only checks `DevicePath` presence + `Screen.AllScreens.Length`; no process-launch/game logic anywhere |
| 14 | Csproj matches exact required properties, no forbidden strings | ✓ VERIFIED | `net10.0-windows`, `UseWindowsForms=true`, `WindowsDisplayAPI` `1.3.0.13`, `OutputType=Exe`; no `ApplicationManifest`/`requireAdministrator`/`PublishTrimmed` (grep-confirmed) |

**Score:** 14/14 deliverable-level truths verified.

### ROADMAP Phase 1 Success Criteria (the actual phase goal — requires human execution)

| # | Success Criterion | Status | Evidence |
|---|--------------------|--------|----------|
| 1 | Prototype confirms the primary monitor is fully removed from Windows' display enumeration (not merely blanked) when triggered | ? UNCERTAIN — tool ready, result not yet obtained | `spike/RESULTS-TEMPLATE.md` is still the blank template (13 `___` placeholders, 0 checked boxes) — no evidence the tool has been run on the rig |
| 2 | The BeamNG-style self-minimize misbehavior is resolved because the monitor is genuinely absent | ? UNCERTAIN — deferred by design | Per D-06, this phase's pass bar is enumeration-only; a real game-launch test is explicitly out of scope for Phase 1 and reserved for later (D-07). Logically follows from SC#1 if SC#1 passes, but not independently tested here |
| 3 | A documented go/no-go decision states which mechanism will be used in Phase 4 | ✗ NOT YET MET | No decision has been recorded anywhere — `RESULTS-TEMPLATE.md` has zero checked boxes among GO / GO (with fallback) / NO-GO |
| 4 | Elevation requirements for the chosen mechanism are confirmed empirically on this machine | ? UNCERTAIN — tool ready, result not yet obtained | The tool is designed to surface this (elevation/UAC observation field in the template) but the field is unfilled |

**These four criteria are the actual definition of "phase goal achieved" per ROADMAP.md, and none of them are currently satisfied by recorded evidence** — only the mechanism to satisfy them exists.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `spike/MonitorDetachSpike/MonitorDetachSpike.csproj` | net10.0-windows console, WindowsDisplayAPI ref, WinForms enabled, no manifest | ✓ VERIFIED | All required tokens present, all forbidden tokens absent |
| `spike/MonitorDetachSpike/Program.cs` | `--list`/`--disable`/`--verify` dispatch, dual-oracle verify, bounds-checked | ✓ VERIFIED | 161 lines, all required patterns present, no forbidden elevation calls |
| `spike/RUN-INSTRUCTIONS.md` | SDK install + build + run + interpret guide | ✓ VERIFIED | All required content sections present |
| `spike/RESULTS-TEMPLATE.md` | Fill-in-the-blanks go/no-go template | ✓ VERIFIED (structure) / ✗ NOT FILLED (content) | Template is well-formed but entirely unfilled — the actual decision is what closes the phase, and it's missing |
| `spike/FALLBACK.md` | Separate, manually-invoked admin escalation path | ✓ VERIFIED | All required content present |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `Program.cs` | `WindowsDisplayAPI PathInfo` | `GetActivePaths`/`ApplyPathInfos` | ✓ WIRED | Both calls present and used correctly in the detach/verify/restore flow |
| `Program.cs` | `System.Windows.Forms.Screen` | independent oracle | ✓ WIRED | `Screen.AllScreens.Length` used in 3 places (before, immediate-after via `VerifyOnce`, delayed-after via `VerifyOnce`, and final restore print) |
| `MonitorDetachSpike.csproj` | `WindowsDisplayAPI 1.3.0.13` | PackageReference | ✓ WIRED | Present with exact version |
| `RUN-INSTRUCTIONS.md` | `Program.cs` modes | documents `--list`/`--disable`/`--verify` | ✓ WIRED | Instructions match the tool's actual CLI contract exactly |
| `RESULTS-TEMPLATE.md` | ROADMAP Phase 1 SC#3 | go/no-go checkboxes | ⚠️ WIRED BUT EMPTY | The link exists (correct field names/structure) but no value has been recorded — the phase's actual decision output is missing |

### Anti-Patterns Found

None. `grep -rniE "TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER|not yet implemented|coming soon"` across `spike/` returns zero matches. No empty stub returns, no hardcoded fake success in `Program.cs`.

### Behavioral Spot-Checks

Skipped — no runnable entry point in this Linux sandbox (Windows-only .NET code, no dotnet SDK installed here per the environment note). This is expected and by design (D-01); the actual behavioral check is the human verification item below.

### Probe Execution

No probes declared for this phase (no `scripts/*/tests/probe-*.sh` found, none referenced in PLAN/SUMMARY).

### Requirements Coverage

N/A — this phase has zero mapped REQ-IDs by design (`requirements: []` in both plan frontmatters, confirmed by ROADMAP.md: "Requirements: None — this is a throwaway validation spike"). No orphaned requirements found in `.planning/REQUIREMENTS.md` for Phase 1.

## Process/Tracking Inconsistency (flag for developer attention)

**`.planning/ROADMAP.md` already marks Phase 1 as `[x]` complete** ("Prove true OS-level monitor disable works on the actual rig hardware... completed 2026-07-24", Progress table: "2/2 plans, Status: Complete") — but per the evidence above, the actual empirical result (ROADMAP's own Success Criteria #1, #3, #4) has not been recorded anywhere in the repo. `spike/RESULTS-TEMPLATE.md` is still blank. Separately, `.planning/STATE.md` is inconsistent with ROADMAP.md in the opposite direction — it still shows `status: executing`, `stopped_at: Phase 1 context gathered`, `completed_phases: 0`, `percent: 0`, i.e., it was never updated after either plan completed. **Per this task's constraints I have not modified either file** — flagging both inconsistencies here for the developer/orchestrator to reconcile. The important one: Phase 4 (`DISPLAY-01/02/03`) depends on Phase 1's outcome, and that outcome does not yet exist in any recorded form.

## Human Verification Required

### 1. Build and run the spike tool on the rig PC

**Test:** Follow `spike/RUN-INSTRUCTIONS.md` end-to-end on the actual Windows rig PC (confirm/install .NET SDK, build, run `--list`, `--disable <index>`, `--verify`).
**Expected:** Tool builds cleanly; `--list` shows the DisplayPort/primary monitor; `--disable <index>` reports PASS on both the immediate and the ~20s delayed check, with a successful restore on Enter.
**Why human:** No .NET/Windows runtime exists in this verification sandbox; this is precisely the hardware/driver behavior the phase exists to determine (D-01) — no static analysis substitutes for it.

### 2. Record the go/no-go decision

**Test:** Fill in `spike/RESULTS-TEMPLATE.md` with the observed results, including the winver, dual-source counts, PASS/FAIL lines, restore result, elevation/UAC observation, and check exactly one of GO / GO (with fallback) / NO-GO.
**Expected:** A completed results file exists, giving a defensible answer to ROADMAP Success Criterion #3 and setting up Phase 4's implementation approach.
**Why human:** This is the literal deliverable of the phase; it cannot be fabricated or inferred from code review.

### 3. If FAIL, exercise the admin fallback

**Test:** If the primary approach fails, follow `spike/FALLBACK.md` from a separately-opened elevated terminal, then re-verify from the non-elevated spike terminal.
**Expected:** Either a GO (with fallback) or a NO-GO outcome, with the corresponding Phase 4 elevation-isolation implication noted.
**Why human:** Requires the rig hardware and an elevated terminal session; not exercisable here.

## Gaps Summary

No code-level or documentation-level gaps were found — the spike tool correctly implements true CCD topology-path-removal (not a power-off), verifies through two independent, delay-rechecked oracles, stays non-elevated by construction, bounds-checks user input, and is accompanied by thorough, copy-pasteable, non-expert-friendly instructions plus a properly separated admin fallback. Everything that *can* be verified from static review in this sandbox passes.

The one substantive gap is not in the deliverables but in the phase's actual completion state: **the empirical go/no-go determination that is this phase's entire purpose has not yet happened** — `RESULTS-TEMPLATE.md` remains unfilled, and ROADMAP.md's "Complete" marking for Phase 1 is therefore premature relative to its own stated Success Criteria. This is expected given the Linux-sandbox execution boundary explicitly documented throughout the phase's planning artifacts, and is resolved by a human round-trip (build/run on the rig, fill in the template), not by further code changes.

---

*Verified: 2026-07-24T12:00:00Z*
*Verifier: Claude (gsd-verifier)*
