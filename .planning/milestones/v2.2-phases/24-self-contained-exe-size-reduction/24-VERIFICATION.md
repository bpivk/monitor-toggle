---
phase: 24-self-contained-exe-size-reduction
verified: 2026-08-19T19:31:57Z
status: passed
score: 8/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
---

# Phase 24: Self-Contained Exe Size Reduction Verification Report

**Phase Goal:** The self-contained exe is measurably smaller than the v2.1 baseline, using only safe, non-trimming MSBuild levers.
**Verified:** 2026-08-19T19:31:57Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

All truths below were independently re-executed against the current repository state (not read from SUMMARY.md) unless noted as hardware-dependent.

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Fresh self-contained win-x64 publish produces an exe smaller than the recorded v2.1 baseline (49,356,430 bytes) | ✓ VERIFIED | Independently re-ran `dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true` after `rm -rf bin/publish/win-x64`. Produced `RigToggle.App.exe` at **46,771,306 bytes** — 2,585,124 bytes under the 49,356,430 baseline. Matches SUMMARY's claimed 46,770,881 bytes within 425 bytes (expected build-timestamp noise, not a discrepancy). |
| 2 | Reduction comes from exactly one new MSBuild `<Target>`; zero `PackageReference` changes anywhere in `src/` | ✓ VERIFIED | `grep -c 'RemoveUnusedDesignerAndVbAssemblies' src/RigToggle.App/RigToggle.App.csproj` = 1. `git diff -- src/RigToggle.Windows/RigToggle.Windows.csproj src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` = empty. `git diff --name-only HEAD~1 HEAD` (commit 67f4bfd) shows only `src/RigToggle.App/RigToggle.App.csproj` (22 insertions, 0 deletions). `PackageReference` count in `RigToggle.Windows.csproj` still 2. |
| 3 | No IL-trimming, Native AOT, or ReadyToRun publish property present anywhere in `src/`; existing explicit `PublishTrimmed=false` unchanged | ✓ VERIFIED | `grep -rEn '<PublishTrimmed>[[:space:]]*true|<PublishAot>|<PublishReadyToRun>' src/` = 0 matches. `win-x64.pubxml` still contains `<PublishTrimmed>false</PublishTrimmed>` exactly once (read directly). |
| 4 | `UseSystemResourceKeys` still absent; no `DebugType`/`DebugSymbols` added | ✓ VERIFIED | `grep -rEn '<UseSystemResourceKeys>|<DebugType>|<DebugSymbols>|<ApplicationManifest>|requestedExecutionLevel' src/RigToggle.App/` = 0 matches. |
| 5 | No additional startup-latency-trading lever was needed (D-02 unused) | ✓ VERIFIED | `win-x64.pubxml` unchanged (`EnableCompressionInSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true` untouched); the new `<Target>` runs `AfterTargets="ComputeResolvedFilesToPublishList"`, before the single-file bundler/compressor consumes the list, so it removes files pre-compression rather than adding a decompression cost. |
| 6 | Every safe lever surfaced by the audit was applied; ruled-out candidates (mscordbi/mscordaccore/createdump, `IncludeNativeLibrariesForSelfExtract=false`, `DebugType=none`) documented with live-measured evidence | ✓ VERIFIED | `24-RESEARCH.md` records live-measured results for all three ruled-out candidates (already excluded via `DropFromSingleFile=true`; byte-identical result; 167-byte non-effect respectively), consistent with the applied 7-file deny-list being the only actionable lever. |
| 7 | `RigToggle.sln` still builds with no new errors/warnings; `RigToggle.Tests` suite still passes with zero failures | ✓ VERIFIED | Independently ran `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true`: `0 Error(s)`, 4 warnings (all pre-existing `xUnit1031` in `ToggleOrchestratorTests.cs`, none mentioning the new target). Independently ran `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj`: `Failed: 0, Passed: 97, Skipped: 0, Total: 97`. Both match SUMMARY's claimed figures exactly. |
| 8 | Full toggle round trip (Rig → Normal → Rig) and cold autostart boot succeed on real rig hardware with no functional regression, including the Settings window | ✓ VERIFIED (operator-attested) | Task 3 is a `type="checkpoint:human-verify" gate="blocking"` task — execution cannot mechanically proceed past it without real user input. Commit history shows a genuine block/resume gap consistent with real hardware access, not a fabricated continuation: `264d6d9` ("halted at Task 3") at 2026-08-18T10:44:01Z, then no Phase-24 activity until `8a19426` ("transcribe rig verification, close Task 3, mark PERF-03 complete") at 2026-08-19T19:24:57Z — over 24 hours later, interleaved with unrelated bug-fix commits from a separate debugging session in between. SUMMARY records six numbered PASS results with specific, non-generic operator wording (e.g. check 5: "autostart felt about the same as before") rather than templated text. This class of hardware-dependent truth cannot be independently re-executed by this verifier (no Windows rig, CCD display topology, or Moza Companion install available in this environment) — same structural limitation the plan itself documents and the same evidence shape accepted for Phase 18's PERF-02. |

**Score:** 8/8 truths verified (0 present-but-behavior-unverified)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.App/RigToggle.App.csproj` | New `<Target Name="RemoveUnusedDesignerAndVbAssemblies" AfterTargets="ComputeResolvedFilesToPublishList">` with what/why/citation comment, matching file's existing convention | ✓ VERIFIED | Present at lines 50-70, comment block matches the file's existing four-lever inline-comment convention (what/why/citation), hook and all 7 exact file names confirmed present and correctly formed. |
| `24-01-SUMMARY.md` | Records fresh before/after byte counts, delta, comparison, audit-gate results, rig checklist outcome | ✓ VERIFIED | Contains a Measurement section, Audit Gates section (all 5 gates' literal output), and Rig Verification section (6 numbered checks) as required. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| `RigToggle.App.csproj` `<Target>` | MSBuild publish pipeline | `AfterTargets="ComputeResolvedFilesToPublishList"` | ✓ WIRED | Confirmed the hook attribute is present exactly once via grep; a live publish run (reproduced above) actually produced a smaller exe, proving the target executes and its `Remove` condition matches real items — not merely present in XML. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Publish produces smaller exe than v2.1 baseline | `dotnet publish ... ; stat -c%s ...exe` | 46,771,306 bytes (< 49,356,430) | ✓ PASS |
| Solution builds clean | `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` | 0 Error(s), 4 pre-existing warnings | ✓ PASS |
| Test suite passes | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` | Failed: 0, Passed: 97, Total: 97 | ✓ PASS |
| Forbidden static-analysis levers absent | `grep -rEn '<PublishTrimmed>[[:space:]]*true\|<PublishAot>\|<PublishReadyToRun>' src/` | 0 matches | ✓ PASS |
| Rig hardware functional regression check | N/A (requires real Windows rig hardware) | — | ? SKIP — routed to operator-attested evidence, see Truth 8 |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PERF-03 | 24-01-PLAN.md | Self-contained exe measurably smaller than v2.1 baseline via additional safe MSBuild-level levers only (no IL trimming, no Native AOT, no PublishReadyToRun) | ✓ SATISFIED | Truths 1, 3, 7 above; `.planning/REQUIREMENTS.md` line 27 confirms `[x] PERF-03` and traceability table (line 70) shows `PERF-03 \| Phase 24 \| Complete`. |

No orphaned requirements — PERF-03 is the only requirement mapped to Phase 24 in `.planning/REQUIREMENTS.md`'s traceability table, and it matches the single requirement declared in `24-01-PLAN.md`'s frontmatter.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | None found | — | `grep -n -iE "TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER"` against `src/RigToggle.App/RigToggle.App.csproj` returned zero matches. `24-REVIEW.md` (code review, standard depth) found 0 critical, 1 warning (WR-01: deny-list has no build-time guard against future SDK renames silently regressing the size win), 1 info — neither blocks the phase goal; WR-01 is a forward-looking maintainability suggestion, not a defect in the current, verified behavior. |

### Human Verification Required

None outstanding. The one hardware-dependent truth (full toggle round trip + cold autostart boot on real rig hardware) was already resolved through this plan's own blocking `checkpoint:human-verify` gate during execution — see Truth 8's evidence above for why this is treated as resolved rather than re-opened as a pending human-verification item.

### Gaps Summary

No gaps. All 8 must-have truths (roadmap Success Criteria 1-3 plus the plan's additional must-haves) verified, both `must_haves.prohibitions` upheld (no forbidden lever introduced; blocking rig checkpoint was not waived), PERF-03 marked complete and correctly traced in REQUIREMENTS.md, build and test suite independently reproduced clean, and the code review found no blocking issues.

One documentation-hygiene note (non-blocking, does not affect goal achievement): `.planning/ROADMAP.md` line 88's phase-header checkbox (`- [ ] **Phase 24...`) and the phase-status table row (`| 24. ... | In Progress | |`) were not updated to reflect Task 3's completion, and `.planning/STATE.md` still reads "halted at blocking checkpoint" / 0% progress from the pre-Task-3 session. This is inconsistent with `.planning/REQUIREMENTS.md`, which *was* correctly updated to mark PERF-03 complete in the same commit (`8a19426`). This is a bookkeeping gap in the planning artifacts, not in the shipped code, and is expected to be closed by the phase-completion/ship step that follows this verification.

---

_Verified: 2026-08-19T19:31:57Z_
_Verifier: Claude (gsd-verifier)_
