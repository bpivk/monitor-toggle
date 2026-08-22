---
phase: 26-auto-update
verified: 2026-08-22T23:59:00Z
status: human_needed
score: 6/6 truths code-verified (5 present-but-behavior-unverified for real-Windows runtime evidence)
behavior_unverified: 5
overrides_applied: 0
re_verification: null
behavior_unverified_items:
  - truth: "SC3: Confirming the prompt downloads the release, renames the running self-contained single-file exe in place at its original path, and relaunches on the new version, with autostart still pointed at the correct path."
    test: "On real Windows hardware, install a build, tag/push a newer release, click Update Now, and confirm the exe swap+relaunch succeeds at the identical install path with no SmartScreen interstitial and no autostart re-registration needed."
    expected: "The exe at the original HKCU-Run-key path is now the new version; no crash, no SmartScreen prompt, no manual re-toggle of autostart required."
    why_human: "Renaming a running PublishSingleFile+IncludeNativeLibrariesForSelfExtract exe is explicitly flagged in ARCHITECTURE.md as never yet verified for this project's specific publish mode. No automated test in this repo (Linux sandbox) can exercise it, the Windows-only child-process tests (UpdateApplyProcessTests) only compile here and have never actually run in CI (see Anti-Patterns/Gaps below — the branch containing this code was never pushed to origin, so windows-latest CI has not executed against it either), and the rig checkpoint designed to prove this (26-05-PLAN.md Task 3, steps 5-8) was closed by operator authorization without being run."
  - truth: "SC4 / UPDATE-05: A failed or interrupted update apply (killed download, disk-full/locked-file, or a swap interrupted mid-rename) always leaves a launchable exe at the original path — never neither the original nor a working replacement."
    test: "On real Windows hardware, deliberately interrupt an update (kill the helper mid-swap, mark the install folder read-only, or fill the disk) and confirm a launchable exe still exists afterward, with a Warning balloon explaining the failure."
    expected: "Exactly one launchable exe exists at the target path after the interruption; a Warning-icon toast names the failure; no crash loop."
    why_human: "This is the phase's own PLAN frontmatter backstop truth (26-03-PLAN.md must_haves.truths, verification: backstop) — explicitly authored as unprovable by automated coverage ('cannot kill a process mid-rename or fill a disk') and deferred to the rig checkpoint. Rig step 10 was never run; the operator's closure explicitly lists 'no live... interrupted-update recovery test' as not performed."
  - truth: "D-09 / UPDATE-05: An update that swaps successfully but the new exe never reaches confirmed-healthy is auto-reverted on the next launch (once, not looping), and a genuine quick-but-graceful exit within the 10-second health-watch window is NOT mistaken for a crash (CR-02 fix)."
    test: "Apply an update, exit within 10 seconds via each of (X-close, tray Exit, Windows shutdown/logoff), then relaunch and confirm no false 'reverted to v{previous}' balloon appears. Separately, kill the process (Task Manager) within 10 seconds and confirm the NEXT launch does revert with the correct balloon and does not loop on the launch after that."
    expected: "Graceful exits never trigger a false revert; only an actual crash/kill triggers one, exactly once."
    why_human: "This is a state-transition/ordering invariant (FormClosing must race and win against the 10-second timer, across multiple CloseReasons) that only exercises correctly on a live WinForms message loop and process-exit path. The code fix (363d488) is present and reviewed as structurally correct, but 26-REVIEW-FIX.md's own note for this fix explicitly flags it as untestable outside real Windows hardware ('this fix changes state-machine timing semantics... this sandbox has no Windows runtime to run the actual update-apply/exit/relaunch sequence end-to-end'). Rig step 9 was never run."
  - truth: "SC1 (partial): The on-launch update check fires reliably via mainForm.BeginInvoke without requiring the window handle to already exist, under both normal and --tray hidden startup."
    test: "Launch the app both normally and via --tray, and confirm the automatic update check still fires (no silently-dropped BeginInvoke) in both cases."
    expected: "The on-launch check runs in both startup modes without an exception or silent no-op."
    why_human: "26-01-PLAN.md's own must_haves.assumptions flags this as 'an open verification question' per ARCHITECTURE.md, with a documented fallback (a one-shot WinForms Timer) if it proves unsafe. This is WinForms message-loop/handle-creation timing — inherently unobservable via grep or a Linux unit test, and not covered by any rig step that was actually run."
  - truth: "UI-SPEC backstop: unsupported Markdown constructs in a REAL GitHub release body (tables, links, images, nested lists, code fences) degrade to readable plain text rather than mangled output, in the live rendered dialog."
    test: "View the actual v2.2 release notes (whatever formatting they contain) in the update prompt dialog and confirm unsupported constructs render as plain text, not garbled output or an empty area."
    expected: "Notes area shows readable text for every construct present in the real release body."
    why_human: "26-04-PLAN.md's own must_haves.truths marks this explicitly verification: backstop — the automated ReleaseNotesFormatterTests cover the parser's synthetic inputs (all passing), but the real-world rendering against an actual GitHub release body is explicitly deferred to the rig checkpoint (step 15), which was never run."
---

# Phase 26: Auto-Update Verification Report

**Phase Goal:** Users are notified when a newer release is available and can install it with one confirmation, without ever being left with a broken or non-launchable app.
**Verified:** 2026-08-22T23:59:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth (ROADMAP SC) | Status | Evidence |
|---|---------------------|--------|----------|
| 1 | The running build carries a version identifier reflecting the actual released git tag; on launch, the app checks GitHub Releases and detects when a newer version is available | ✓ VERIFIED (code) / ⚠️ partial behavior-unverified | `<Version>2.2</Version>` in `RigToggle.App.csproj`; `release.yml`'s `Resolve version from tag` step strips leading `v` and passes `-p:Version=` to `dotnet publish`; `GitHubReleaseFeed` queries `/releases/latest` with a `User-Agent` header and a host allow-list (`objects.githubusercontent.com` etc. present); `UpdateVersionComparer` numeric-not-lexical comparison covered by 14+ passing tests including the `v2.9`/`v2.10` and 4-vs-2-component guards; `Program.cs` wires `mainForm.BeginInvoke(... RunAutomaticUpdateCheckAsync ...)` after `guard.MarkReady()`. **Caveat:** the `BeginInvoke`-before-Handle-exists timing assumption is explicitly unresolved per 26-01-PLAN.md and untestable outside real Windows — see behavior_unverified_items. |
| 2 | When a newer version is found, the user sees a confirmation prompt naming the new version before any download or apply happens — nothing installs silently | ✓ VERIFIED | `UpdatePromptDialog` is a themed `Form` (never `MessageBox` — grep confirms zero matches), headline is `Rig Toggle {release.TagName} is available`; `UpdateOrchestrator.CheckAsync` invokes `confirm` and only proceeds to `DownloadAndStageAsync`/`ApplyAndRelaunch` when the result is `UpdateNow`; unit tests assert the applier is never invoked when confirm returns `Later`/`Skip`. `UpdatePromptDialog.Choice` maps `OK→UpdateNow`, `Ignore→Skip`, everything else (Esc, close-X, Cancel)`→Later` — confirmed in source, matching D-02's "closing is never silently a skip" requirement. |
| 3 | Confirming the prompt downloads the new release, applies it in place, and the app relaunches running the new version, with autostart still pointed at the correct exe path | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | All code artifacts present and wired: `WindowsUpdateApplier.DownloadAndStageAsync` (checksum-verify-before-return), `ApplyAndRelaunch` (temp-copy helper spawn), `UpdateApplyEntryPoint.Run` (wait-writable → rename-to-`.bak` → move staged into place → relaunch, with rollback-on-second-move-failure). This is the phase's single riskiest one-way mechanism (renaming a running `PublishSingleFile` exe) and has **never actually executed on Windows** — see behavior_unverified_items and the CI-never-ran finding below. |
| 4 | If the update is interrupted partway (killed download, simulated disk-full/locked-file), the original exe is left intact and still launchable — the app is never stranded | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | Code present: rename-not-overwrite ordering, immediate `.bak`-restore-on-throw in `UpdateApplyEntryPoint`, retained-backup + `Applied`/`FirstLaunchAttempted`/`Reverted` marker state machine in `UpdateRollbackChecker`, `SweepOrphanedHelperExes` cleanup (WR-01 fix). This is 26-03-PLAN.md's own explicit `verification: backstop` truth — not provable by any automated test, deferred entirely to the rig checkpoint's steps 9-10, which were never run. |
| 5 | A manual "Check for Updates" entry in the app's menu triggers the same check on demand and reports when already up to date, independent of the automatic on-launch check | ✓ VERIFIED | `trayCheckUpdatesMenuItem` present in `MainForm.Designer.cs`'s `AddRange` array between `traySettingsMenuItem` and `traySeparator` (confirmed via `awk`); `btnCheckForUpdates` present in `SettingsForm.Designer.cs`, outside `flpButtons`; both wired to the identical `MainForm.PerformManualUpdateCheck`. `UpdateOrchestrator.CheckOnDemandAsync` always passes `honourSkippedVersion: false` (unit-tested: confirm invoked even when the release matches a persisted skip) and `reportFailures: true` (unit-tested: the paired `CheckOnLaunchAndCheckOnDemand_SameThrowingFeed_YieldDistinctOutcomes` test proves the same throwing feed yields `NotAvailable` on launch vs `CheckFailed` on-demand — the D-07 distinctness guard). Verbatim UI-SPEC copy strings ("You're already on the latest version", "Couldn't check for updates") grep-confirmed in `MainForm.cs`. |

**Score:** 6/6 code-level truths verified via artifacts + 206/206 passing unit tests; 5 present-but-behavior-unverified items require real-Windows rig evidence not yet gathered (see below).

### Additional Decisions Verified (D-01 through D-11, code review fixes)

| Decision/Fix | Status | Evidence |
|---|---|---|
| D-01 (formatted release notes) | ✓ VERIFIED | `ReleaseNotesFormatter.Format` parses headers/bullets/bold into styled runs; `FallbackText` constant is the verbatim UI-SPEC string; `ReleaseNotesRenderer.Render` applies runs via `SelectionFont`/`SelectionBullet` without touching `BackColor`/`ForeColor`. 40+ `ReleaseNotesFormatterTests` pass, including the unsupported-construct degradation cases. Real-render against an actual release body remains a backstop item (see above). |
| D-02 (skip persists one version) | ✓ VERIFIED | `AppSettings.SkippedUpdateVersion`; `UpdateOrchestrator` compares numerically via `UpdateVersionComparer`, not string match — unit-tested paired cases (skip-equal suppressed, skip-older still-newer-prompts). |
| D-10/D-11 (SHA256 checksum, verify before swap) | ✓ VERIFIED | `release.yml`'s `Compute SHA256 checksum` step + `Attach exe to GitHub Release`'s multi-file `files:` list; `WindowsUpdateApplier.DownloadAndStageAsync` verifies before returning the staged path (verified textually before the `return`); fail-closed on missing checksum. `UpdateChecksumTests`/`GitHubReleaseFeedTests` pass (host allow-list, User-Agent, scheme rejection all covered with a stub `HttpMessageHandler`, no live network). |
| T-26-01 (host allow-list) | ✓ VERIFIED | `objects.githubusercontent.com`/`github.com`/`api.github.com` allow-list applied to both the exe and checksum asset URLs in `GitHubReleaseFeed.cs`. |
| CR-01 (mutual exclusion between automatic/manual checks) | ✓ VERIFIED (code) | `_updateCheckInProgress` `Interlocked.CompareExchange` guard added to both `RunAutomaticUpdateCheckAsync` and `PerformManualUpdateCheckAsync`, each releasing in `finally`. Structurally sound on review; no dedicated test exists (WinForms-only reentrancy scenario, cannot unit-test without a live message pump) — treated as code-verified, not requiring a separate human item since the fix mirrors the codebase's existing trusted `ToggleOrchestrator._busy` pattern closely enough that static review is sufficient confidence. |
| CR-02 (graceful exit ≠ crash) | ⚠️ PRESENT_BEHAVIOR_UNVERIFIED | `ConfirmUpdateHealthyOnce()` now called from both the 10s timer and `MainForm_FormClosing`'s genuine-exit fall-through. Included above in behavior_unverified_items — 26-REVIEW-FIX.md's own note explicitly asks for real-hardware confirmation. |
| WR-01 (orphaned %TEMP% helper exe cleanup) | ✓ VERIFIED | `UpdateRollbackChecker.SweepOrphanedHelperExes()` called unconditionally on every startup, sweeps `RigToggle-updater-*.exe` older than a 10-minute grace period. |
| WR-02 (staged-file cleanup on any failure) | ✓ VERIFIED | `DownloadAndStageAsync`'s entire download+verify body wrapped in try/catch deleting `stagedPath` on any exception before rethrowing. |
| WR-03 (trailing-newline checksum trim) | ✓ VERIFIED | `UpdateChecksum.Matches` now uses `.AsSpan().Trim()` (both ends) instead of `TrimStart()`. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `src/RigToggle.Core/Models/ReleaseInfo.cs` | Immutable release DTO incl. `ChecksumDownloadUrl` | ✓ VERIFIED | Present, sealed record, all members confirmed. |
| `src/RigToggle.Core/GitHubReleaseFeed.cs` | Unauthenticated `/releases/latest` reader with host allow-list | ✓ VERIFIED | Present and wired. |
| `src/RigToggle.Core/UpdateVersionComparer.cs` | Numeric Major.Minor comparison | ✓ VERIFIED | Present; 14+ tests pass. |
| `src/RigToggle.Core/UpdateOrchestrator.cs` | Check→compare→confirm→apply sequencer, shared `CheckAsync`, `CheckOnDemandAsync` | ✓ VERIFIED | Present, UI-free (`! grep -q 'using System.Windows'` holds), fully wired. |
| `src/RigToggle.Core/UpdateChecksum.cs` | SHA256 compute + fail-closed match | ✓ VERIFIED | Present; trailing-newline fix (WR-03) landed. |
| `src/RigToggle.Core/Models/UpdateAppliedMarker.cs` + `JsonUpdateAppliedMarkerStore.cs` | 3-stage disk-persisted marker | ✓ VERIFIED | Present, `JsonStringEnumConverter` used, atomic `File.Move` write confirmed. |
| `src/RigToggle.Core/ReleaseNotesFormatter.cs` | Markdown-lite formatter | ✓ VERIFIED | Present, platform-neutral, tested. |
| `src/RigToggle.Windows/WindowsUpdateApplier.cs` | Download+stage+verify+relaunch | ✓ VERIFIED | Present; WR-01/WR-02 fixes landed. |
| `src/RigToggle.App/UpdateApplyEntryPoint.cs` | Real swap body (Phase 25 placeholder replaced) | ✓ VERIFIED | Signature byte-for-byte intact (`internal static int Run(string[] applyUpdateArgs)`); no `File.Delete` present (backup never deleted here). |
| `src/RigToggle.App/UpdateRollbackChecker.cs` | Above-the-guard rollback state machine | ✓ VERIFIED | All 3 stages handled, `SweepOrphanedHelperExes`, `ConfirmHealthy`/`ConfirmUpdateHealthyOnce` present. |
| `src/RigToggle.App/UpdatePromptDialog.cs`/`.Designer.cs` | Themed 3-button dialog, 440x460 | ✓ VERIFIED | `btnSkip` present, `ClientSize(440, 460)` confirmed, `ReleaseNotesRenderer.Render` wired. |
| `src/RigToggle.App/MainForm.Designer.cs` / `SettingsForm.Designer.cs` | Tray item + Settings button | ✓ VERIFIED | Correct tray order and Settings placement confirmed. |
| `src/RigToggle.Windows.Tests/UpdateApplyProcessTests.cs` | Real child-process swap proof | ✓ EXISTS, compiles clean, **never executed** | See CI-never-ran finding below — this is the most consequential unresolved item for SC3/SC4 confidence. |
| `src/RigToggle.Tests/*` (5 new test files) | Comparer, orchestrator, checksum, marker-store, release-notes, GitHub-feed coverage | ✓ VERIFIED | 206/206 pass in this session (re-run directly, not taken from SUMMARY claims). |

### Key Link Verification

| From | To | Via | Status | Details |
|---|---|---|---|---|
| `Program.cs` | `UpdateOrchestrator.cs` | `mainForm.BeginInvoke(... RunAutomaticUpdateCheckAsync ...)` after `guard.MarkReady()` | ✓ WIRED | Confirmed textually after `guard.MarkReady()`. |
| `UpdateOrchestrator.cs` | `WindowsUpdateApplier.cs` | `IUpdateApplier` call after confirm returns `UpdateNow` | ✓ WIRED | Confirmed via orchestrator tests (ordering asserted). |
| `WindowsUpdateApplier.cs` | `UpdateApplyEntryPoint.cs` | `Process.Start` with `StartupArgs.ApplyUpdateFlag` | ✓ WIRED | Confirmed via `UpdateApplyProcessTests` source (compiles, references the real entry point contract). |
| `.github/workflows/release.yml` | `RigToggle.App.csproj` | `-p:Version=` | ✓ WIRED | Confirmed. |
| `release.yml` | `GitHubReleaseFeed.cs` | published `.sha256` asset | ✓ WIRED | Confirmed. |
| `Program.cs` | `UpdateRollbackChecker.cs` | call placed strictly before `SingleInstanceGuard.Acquire()` | ✓ WIRED | Line-number check confirms ordering. |
| `MainForm.cs` | `UpdateRollbackChecker.cs` | `BeginUpdateHealthWatch` timer tick + `MainForm_FormClosing` (CR-02) | ✓ WIRED | Confirmed both call sites present. |
| `SettingsForm.cs` | `MainForm.cs` | `performManualUpdateCheck` threaded through `SettingsFormFactory` | ✓ WIRED | Confirmed in `Program.cs`. |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|---|---|---|---|---|
| `UpdatePromptDialog.rtbReleaseNotes` | `release.Body` | `GitHubReleaseFeed` → `ReleaseInfo.Body` → `ReleaseNotesFormatter.Format` → `ReleaseNotesRenderer.Render` | Yes — real GitHub release body text flows through, not a hardcoded literal (fallback string only used when `Body` is null/whitespace) | ✓ FLOWING |
| `MainForm` toast strings | running/new version, failure reason | `UpdateCheckResult.RunningVersionText`/`FailureReason` derived from live `Version`/exception message, not hardcoded | Yes | ✓ FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Full cross-platform unit suite | `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj -c Release --nologo` | 206/206 passed | ✓ PASS |
| Named UpdateOrchestrator decision-branch tests | `dotnet test ... --filter "FullyQualifiedName~UpdateOrchestratorTests"` | 14/14 passed | ✓ PASS |
| Named checksum/comparer/marker/feed tests | `dotnet test ... --filter "...ReleaseNotesFormatterTests\|UpdateChecksumTests\|JsonUpdateAppliedMarkerStoreTests\|UpdateVersionComparerTests\|GitHubReleaseFeedTests"` | 71/71 passed | ✓ PASS |
| Solution build | `dotnet build RigToggle.sln -c Release --nologo` | 0 Errors, 6 pre-existing unrelated `xUnit1031` warnings (confirmed pre-dating this phase, in files this phase never touched) | ✓ PASS |
| RigToggle.Windows.Tests compiles | `dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj -c Release --nologo` | 0 Errors, 0 Warnings | ✓ PASS (compile-only; cannot execute in this Linux sandbox) |
| Real Windows execution of `UpdateApplyProcessTests`, `SingleInstanceProcessTests`, or any Phase 26 code | N/A | **Never executed anywhere** | ? SKIP — see finding below |

### Probe Execution

Step 7c: SKIPPED — no `scripts/*/tests/probe-*.sh` declared or found for this phase.

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|---|---|---|---|---|
| UPDATE-01 | 26-01 | Build-time version reflects git tag | ✓ SATISFIED (code) | `<Version>`, `release.yml` step, comparer tests. Real release-workflow run not yet exercised. |
| UPDATE-02 | 26-01, 26-04 | On-launch GitHub Releases check | ✓ SATISFIED (code) | Wired, tested. `BeginInvoke` timing assumption unresolved on real hardware. |
| UPDATE-03 | 26-01, 26-04 | Confirm-before-download prompt | ✓ SATISFIED | Fully code + test verified. |
| UPDATE-04 | 26-01, 26-02 | Download, apply in place, relaunch | ⚠️ SATISFIED (code) / behavior-unverified | The riskiest mechanism in the phase; never executed on Windows anywhere (sandbox, CI, or rig). |
| UPDATE-05 | 26-02, 26-03 | Never-stranded recovery | ⚠️ SATISFIED (code) / behavior-unverified | Explicit PLAN-level `backstop` truth; rig steps 9-10 not run. |
| UPDATE-06 | 26-05 | Manual check, tray + Settings | ✓ SATISFIED | Fully code + test verified; only visual rendering unconfirmed (low risk). |
| UPDATE-07 | (Phase 25) | Single-instance bypass for relaunch | ✓ Already complete (Phase 25) | Confirmed regression-safe: `UpdateRollbackChecker.Run` and the `--apply-update` branch both sit above `SingleInstanceGuard.Acquire()`. |

**Orphaned requirements check:** REQUIREMENTS.md maps exactly UPDATE-01..06 to Phase 26; all six appear in at least one plan's `requirements:` frontmatter field. No orphans.

**Documentation gap found (non-blocking):** `.planning/REQUIREMENTS.md`'s checkbox list and Traceability table were updated by commit `96d3607` (26-01) marking UPDATE-01..04 `[x]`/Complete, but were **never updated after 26-02/26-03 (UPDATE-05) or 26-05 (UPDATE-06)** — both still show `[ ]` and "Pending" in the current file, despite the underlying functionality being implemented and unit-tested. This is a stale-documentation issue, not a functional gap; the actual code satisfies both requirements at the level automated verification can confirm. Recommend updating REQUIREMENTS.md's checkboxes/traceability table for UPDATE-05 and UPDATE-06 alongside closing the rig-verification gap.

### Anti-Patterns Found

None blocking. No `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` debt markers in any Phase-26-modified file (the five `PLACEHOLDER` grep hits in `MainForm.Designer.cs` predate this phase by 12 days — confirmed via `git blame`, unrelated pre-existing dashboard-layout comments). No stub `return null`/empty-body patterns beyond `GitHubReleaseFeed`'s documented "null means no usable release" contract. No `ToolTipIcon.Error` usage, no `MessageBox` in the update dialog.

**Significant finding — CI has never executed against any Phase 26 code:** `git rev-list --left-right --count origin/master...HEAD` shows the local `master` branch is 127 commits ahead of `origin/master`; the entire v2.2 milestone (Phases 24, 25, 26) has never been pushed to GitHub. `gh run list` confirms the most recent CI run (`Build`/`Release`, both on `windows-latest`) is from 2026-08-17 (the v2.1 retrospective commit), predating all Phase 26 work. This means every claim across all five 26-0X-SUMMARY.md files that Windows-only tests (`UpdateApplyProcessTests`, and the pre-existing `SingleInstanceProcessTests`/`ApplyUpdateBypass_*` regression guards this phase's swap logic depends on) "execute in CI on windows-latest" is describing a mechanism that is **available** but has **not yet actually run** for this code. Combined with the rig checkpoint being closed without running its 15 steps, this means the phase's single riskiest mechanism — renaming a running self-contained single-file exe and relaunching it — has literally never executed on a real Windows process anywhere, on any machine, for this phase's code. This is not a code defect (the implementation is careful, well-documented, and structurally sound on review) but it is a material verification gap the SUMMARY files' repeated "verified via CI" framing does not fully disclose.

## Human Verification Required

The five items below are the highest-value subset of the original 26-05-PLAN.md Task 3 15-step rig checklist (closed by operator authorization without being run) plus the CR-02 fix's own explicitly-requested follow-up, re-derived from what this verification pass found to still require real Windows hardware evidence. Full detail and step numbering for the complete original checklist remains in `26-05-PLAN.md`'s Task 3 and `26-05-SUMMARY.md`'s "Task 3: Operator Verification" section.

### 1. Real exe swap and relaunch (SC3 / UPDATE-04)

**Test:** Publish and install a build, tag/push a real newer release, click "Update Now."
**Expected:** The exe at the original install path is now the new version; no SmartScreen interstitial; autostart still works without re-toggling.
**Why human:** Never executed on Windows anywhere — not in this sandbox, not in CI (unpushed), not on the rig (checkpoint closed unrun). This is the phase's single riskiest one-way mechanism.

### 2. Interrupted-update recovery (SC4 / UPDATE-05)

**Test:** Deliberately interrupt an update (kill the helper mid-swap, or simulate disk-full/locked-file).
**Expected:** A launchable exe still exists at the original path afterward; a Warning balloon explains the failure.
**Why human:** Explicitly authored as a `verification: backstop` truth in 26-03-PLAN.md — "cannot kill a process mid-rename or fill a disk" via automated coverage.

### 3. Auto-rollback timing correctness, including CR-02's fix (D-09 / UPDATE-05)

**Test:** Apply an update, then (a) exit gracefully within 10 seconds via tray Exit/window close/shutdown and confirm NO false revert on next launch; (b) kill the process within 10 seconds and confirm a correct, single (non-looping) revert with the right balloon text.
**Expected:** Graceful exits never trigger a false "reverted to v{previous}" message; only genuine crashes do, exactly once.
**Why human:** State-machine timing invariant across real `FormClosing`/process-exit paths; 26-REVIEW-FIX.md's own note for this exact fix requests real-hardware confirmation.

### 4. On-launch check reliability under both startup modes (SC1)

**Test:** Launch normally and via `--tray`; confirm the automatic update check fires in both cases.
**Expected:** No silently-dropped `BeginInvoke` call in either mode.
**Why human:** 26-01-PLAN.md's own must_haves.assumptions flags `mainForm.Handle` existence at this call site as an open verification question, with a documented Timer-based fallback if it proves unsafe.

### 5. Real release-notes rendering (UI-SPEC backstop)

**Test:** View the actual next release's notes in the update dialog.
**Expected:** Any unsupported Markdown constructs (tables, links, images, nested lists) render as readable plain text, not garbled output.
**Why human:** 26-04-PLAN.md's own must_haves.truths marks this explicitly `verification: backstop` — synthetic parser tests all pass, but real-world rendering was never observed.

## Gaps Summary

No FAILED truths, missing artifacts, broken key links, or blocking anti-patterns were found — every artifact this phase's five plans committed to exists, is substantive, and is correctly wired, and all 206 cross-platform tests (including 71 newly added/relevant ones) pass on a fresh run in this session. The code review's 2 Critical + 3 Warning findings were all genuinely fixed in the codebase (verified by reading the actual diffs, not the review-fix report's claims) and the fixed build/test suite still passes clean.

The gap is entirely in **runtime evidence**, not implementation: this phase's core value proposition — "install with one confirmation, without ever being left with a broken or non-launchable app" — hinges on a rename-while-running self-replace mechanism, a crash-detection auto-rollback state machine, and interrupted-update recovery, none of which have ever executed on a real Windows process. The 26-05-PLAN.md Task 3 rig checkpoint was purpose-built to supply exactly this evidence and was explicitly closed by the operator without being run, with the stated intent to exercise it naturally when v2.2 is actually tagged and released. That is a reasonable, well-disclosed engineering tradeoff — but it means the phase goal is **not yet independently confirmed true**, only confirmed *implemented*. Routing to human_needed rather than accepting a blanket override reflects that the deferred evidence spans the phase's central risk (self-replace-while-running) and its central promise (never-stranded recovery), not a peripheral or cosmetic item.

---

_Verified: 2026-08-22T23:59:00Z_
_Verifier: Claude (gsd-verifier)_
