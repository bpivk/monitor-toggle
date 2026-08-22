---
phase: 26-auto-update
fixed_at: 2026-08-22T23:30:00Z
review_path: .planning/phases/26-auto-update/26-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 5
skipped: 0
status: all_fixed
---

# Phase 26: Code Review Fix Report

**Fixed at:** 2026-08-22T23:30:00Z
**Source review:** .planning/phases/26-auto-update/26-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (CR-01, CR-02, WR-01, WR-02, WR-03 — IN-01 excluded, Info-scope, out of `fix_scope: critical_warning`)
- Fixed: 5
- Skipped: 0

## Fixed Issues

### CR-01: No mutual exclusion between automatic and manual update checks — concurrent invocation can double-apply

**Files modified:** `src/RigToggle.App/MainForm.cs`
**Commit:** `3c20ad1`
**Applied fix:** Added an `Interlocked`-guarded `_updateCheckInProgress` flag (mirroring `ToggleOrchestrator`'s own `_busy` pattern) to `MainForm`. Both `RunAutomaticUpdateCheckAsync` and `PerformManualUpdateCheckAsync` now attempt a `CompareExchange(ref _updateCheckInProgress, 1, 0)` at entry and no-op (return immediately) if a check is already in flight; each releases the flag in a `finally` block. This closes the reentrancy hole where `ShowUpdatePromptDialog`'s nested `ShowDialog()` message pump could dispatch a tray "Check for Updates" click while the automatic check's own dialog was still open, previously allowing two concurrent `DownloadAndStageAsync`/`ApplyAndRelaunch` sequences to race on the same staging path.

### CR-02: Auto-rollback "confirmed healthy" signal is a bare 10-second timer — a normal quick exit is indistinguishable from a crash and triggers a false revert

**Files modified:** `src/RigToggle.App/MainForm.cs`
**Commit:** `363d488`
**Applied fix:** `BeginUpdateHealthWatch` now retains the confirmed-healthy callback in a new `_confirmUpdateHealthyAction` field. A new idempotent `ConfirmUpdateHealthyOnce()` helper (guarded by `_updateHealthConfirmed`) is invoked both from the existing 10-second timer's `Tick` and — new — from `MainForm_FormClosing`'s genuine-exit fall-through, which is the single point every real-exit `CloseReason` converges on (X-without-tray, tray Exit's `ApplicationExitCall`, `WindowsShutDown`, `TaskManagerClosing`). A graceful exit within the 10-second window now correctly earns confirmed-healthy instead of being silently treated as a crash on the next launch.

**Note — requires human verification on real Windows hardware:** this fix changes state-machine *timing* semantics (when "confirmed healthy" is reached relative to process exit) that cannot be exercised by a syntax check or by this repo's cross-platform unit tests, and this sandbox has no Windows runtime to run the actual update-apply/exit/relaunch sequence end-to-end. Recommend a real-hardware pass per 26-CONTEXT.md's existing rig-verification convention: apply an update, exit within 10 seconds via each of (X with tray-minimize off, tray "Exit", a second exit reached via Windows shutdown/logoff if practical), and confirm the *next* launch does NOT show the "reverted to v{previous}" balloon.

### WR-01: Copied update-relaunch helper exe in %TEMP% is never cleaned up

**Files modified:** `src/RigToggle.Windows/WindowsUpdateApplier.cs`, `src/RigToggle.App/UpdateRollbackChecker.cs`
**Commit:** `6ac6d52`
**Applied fix:** Took the "sweep on next launch" option from the Fix section (rather than a delayed self-delete of the running helper image, which risks Windows exe-delete-while-executing quirks). `WindowsUpdateApplier` now exposes the helper filename prefix/search pattern (`HelperFileNamePrefix`, `HelperFileSearchPattern`) as public constants instead of an inline string literal. `UpdateRollbackChecker.Run` calls a new `SweepOrphanedHelperExes()` unconditionally, before its marker-stage switch, on every normal startup — it enumerates `%TEMP%\RigToggle-updater-*.exe` and deletes any file whose `LastWriteTimeUtc` is older than a 10-minute grace period (chosen so a helper exe genuinely mid-relaunch on the current boot, which is by definition freshly written, is never swept). Mirrors the existing orphaned-`.bak`/`.failed` cleanup pattern already in the same method, but runs independent of marker state (a helper can be orphaned by an otherwise-successful swap just as easily as by a failed one).

### WR-02: Staged update exe is not cleaned up on a partial-download or checksum-fetch failure

**Files modified:** `src/RigToggle.Windows/WindowsUpdateApplier.cs`
**Commit:** `56df948`
**Applied fix:** Wrapped `DownloadAndStageAsync`'s entire download+verify body in a `try`/`catch` that deletes `stagedPath` (best-effort, individually try/caught) on any exception before rethrowing (`throw;`, preserving the original exception and stack trace), exactly as the Fix section suggested — matching the two previously-explicit `File.Delete(stagedPath)` calls on the checksum branches were removed since the catch-all now covers them plus the two previously-uncovered paths (`CopyToAsync` and `GetStringAsync` failures).

### WR-03: `UpdateChecksum.Matches`'s documented "bare digest" support breaks on a trailing newline

**Files modified:** `src/RigToggle.Core/UpdateChecksum.cs`
**Commit:** `bdbc22a`
**Applied fix:** Changed `publishedText.AsSpan().TrimStart()` to `.Trim()` (both ends) before the delimiter search, exactly as the Fix section specified. A bare 64-hex-character digest with a trailing newline (from `echo`, most text editors, or any tool other than this project's own `-NoNewline` PowerShell step) now correctly trims to exactly 64 characters instead of 65, passing the length check. Verified this does not regress the `sha256sum`-style "digest  filename" line case — `Trim()` only strips whitespace from the string's ends, leaving the interior delimiter search unaffected.

## Skipped Issues

None — all 5 in-scope findings were fixed.

## Verification

Ran entirely in the isolated worktree `/home/bpivk/moza/.claude/worktrees/rf-26-1658861-1787440306` (branch `gsd-reviewfix/26-1658861`), per `workflow.use_worktrees` (not set in `.planning/config.json`, defaults to `true`). All commands below were run there before the fast-forward/cleanup tail; they are reproducible from the main checkout post-merge since the worktree only ever diverged by these 5 fix commits.

- `dotnet build src/RigToggle.App/RigToggle.App.csproj` — **Build succeeded, 0 warnings, 0 errors** after each of the 5 commits (re-verified after all 5 combined). Notably, `RigToggle.App` (net10.0-windows, WinForms) compiles in this Linux sandbox via the .NET cross-targeting toolchain, so this gave real compiler verification of `MainForm.cs`, not just a syntax check.
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` (cross-platform suite) — **206/206 passed**, 0 failed, after all 5 fixes combined. Includes `UpdateChecksumTests` (WR-03) and `UpdateOrchestratorTests` (CR-01-adjacent call-ordering coverage).
- `dotnet build src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj` — **Build succeeded, 0 warnings, 0 errors**. This project (Windows-only, exercises `WindowsUpdateApplier`/`UpdateApplyEntryPoint` process-replacement behavior) compiles cleanly against the WR-01/WR-02 changes but its tests cannot execute in this Linux sandbox, consistent with every prior plan in this phase.

No test coverage was added by this fix pass (out of scope per the Fix sections, which specified source-code remediation only). CR-02 in particular should be exercised on real Windows hardware before considering this phase's update-reliability guarantees fully closed — see that finding's note above.

---

_Fixed: 2026-08-22T23:30:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
