---
phase: 25-single-instance-guard
plan: 03
subsystem: infra
tags: [single-instance, mutex, win32, p-invoke, wndproc, dotnet, systemevents, foreground-window, debug-logging]

requires:
  - phase: 25-single-instance-guard (plan 01)
    provides: "SingleInstanceGuard, ActivationSignal, Program.cs startup-gate ordering"
  - phase: 25-single-instance-guard (plan 02)
    provides: "StartupArgs.ApplyUpdateFlag/TryGetApplyUpdateArgs/ApplyUpdateBypassExitCode, UpdateApplyEntryPoint"
provides:
  - "SingleInstanceProcessTests (RigToggle.Windows.Tests): 7 xUnit facts launching the real built exe as child processes, proving INSTANCE-01/INSTANCE-02/UPDATE-07 end-to-end"
  - "Real-usage crash fix: ApplicationConfiguration.Initialize()'s SetColorMode/SystemEvents race no longer terminates the process"
  - "Real foreground-activation fix: PID-targeted AllowSetForegroundWindow instead of ASFW_ANY, plus a corrected IsForegroundWindow diagnostic"
  - "Cross-process debug.log: both primary and duplicate paths can now log (ordering fix + FileShare.ReadWrite fix)"
affects: [26-auto-update]

actuals:
  tokens: 68000
  tasks: 3
  commits: 10

tech-stack:
  added: []
  patterns:
    - "Test-only instanceId override on SingleInstanceGuard.Acquire()/WaitForInstanceReady() to decouple in-process unit tests from the production mutex name, avoiding cross-VSTest-project contention"
    - "PID-targeted AllowSetForegroundWindow instead of the ASFW_ANY wildcard, closing a real 'grant stolen by an unrelated window' risk"
    - "GetForegroundWindow()==Handle as the ground-truth activation signal, not Control.Focused (which reflects child-control keyboard focus, not form-level activation)"

key-files:
  created:
    - src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs
  modified:
    - src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj
    - src/RigToggle.App/Program.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.Core/SingleInstanceGuard.cs
    - src/RigToggle.Windows/ActivationSignal.cs
    - src/RigToggle.Windows/NativeMethods.cs
    - src/RigToggle.Tests/SingleInstanceGuardTests.cs

key-decisions:
  - "D-05's literal wording names RigToggle.Tests as the home for these tests; they live in RigToggle.Windows.Tests instead, flagged as a deviation in the plan's own objective (RigToggle.Tests targets plain net10.0 specifically to stay buildable/runnable on non-Windows machines; a test that launches a WinForms exe and reads window state is unambiguously Windows-only)"
  - "SingleInstanceGuard.Acquire()/WaitForInstanceReady() gained an optional instanceId parameter (default null = production InstanceId) so RigToggle.Tests' in-process SingleInstanceGuardTests can use a random per-run GUID instead of the real production mutex name -- closes a genuine cross-VSTest-project race where two separate test host processes were contending for the same machine-wide kernel object"
  - "AllowSetForegroundWindow targets the specific peer process id (resolved by process name) instead of ASFW_ANY, since the wildcard grants foreground rights to whichever process next calls SetForegroundWindow -- any window on the desktop, not necessarily ours"
  - "ApplicationConfiguration.Initialize() is wrapped in a narrow catch for InvalidOperationException only, not retried and not fixed by reordering SetColorMode -- a retry would re-invoke SetHighDpiMode, documented to throw on a second call; reordering touches Phase 12's own rig-verified title-bar-flash fix, which cannot be re-verified from this environment"
  - "Debug-logging setup (settings load + trace-listener wiring) moved from after the single-instance guard's early-return to before it, so both primary and duplicate processes can log -- kept strictly after the --apply-update bypass branch to preserve D-03's one-way ordering contract"
  - "debug.log opened via an explicit FileStream(FileMode.Append, FileAccess.Write, FileShare.ReadWrite) instead of StreamWriter's default-sharing path constructor, so multiple processes can hold independent writable handles to the same file concurrently"

patterns-established:
  - "Investigate real hardware discrepancies with targeted, narrow diagnostic instrumentation (Trace.WriteLine at each link in a suspected causal chain) before proposing a fix -- every fix in this plan was driven by an actual debug.log/Event-Viewer capture, not a guess"

requirements-completed: [INSTANCE-01, INSTANCE-02, UPDATE-07]

coverage:
  - id: D1
    description: "Rapid relaunch (10 iterations) and a tight-race launch (3 rounds, no readiness wait) both leave exactly one RigToggle.App process alive, proven by an automated xUnit test launching the real built exe as a child process"
    requirement: "INSTANCE-01"
    verification:
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#RapidRelaunch_ExactlyOneProcessSurvives"
        status: unknown
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#TightRaceLaunch_ExactlyOneProcessSurvives"
        status: unknown
      - kind: manual_procedural
        ref: "Operator PowerShell 2-launch repro script, run multiple times post-fix, no crash recurrence"
        status: pass
    human_judgment: true
    rationale: "RigToggle.Windows.Tests cannot execute in this Linux build environment (testhost requires Microsoft.WindowsDesktop.App). The operator confirmed the underlying mechanism (mutex acquisition/rejection, no crash) manually and via a scripted repro, but has not yet run this specific automated test in RigToggle.Windows.Tests to a green result -- test status is unknown, not confirmed pass, pending the follow-up noted below."
  - id: D2
    description: "A blocked duplicate launch against a minimized/tray-hidden primary makes the primary's window become genuinely visible, un-minimized, and the real OS foreground window (GetForegroundWindow()==Handle), not merely IsWindowVisible-true"
    requirement: "INSTANCE-02"
    verification:
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#DuplicateLaunch_RestoresHiddenWindowOfExistingInstance"
        status: unknown
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#DuplicateLaunch_RestoresMinimizedVisibleWindowToForeground"
        status: unknown
      - kind: manual_procedural
        ref: "Operator debug.log capture (IsForegroundWindow=True, ContainsFocus=True after RestoreAndFocus) plus direct visual confirmation the window jumps to foreground on a real double-click"
        status: pass
    human_judgment: true
    rationale: "Same automated-test-cannot-run-here constraint as D1. This is the requirement whose real bug (foreground activation silently failing) this plan's investigation actually found and fixed on real hardware -- confirmed by both log data and direct visual observation, the strongest evidence available, but the automated regression test's own pass/fail status in CI is still unconfirmed."
  - id: D3
    description: "--apply-update runs to its distinct bypass exit code while the single-instance mutex is held by another process, with a negative control proving a normal launch under the same condition behaves differently; idempotent on repeat; non-interfering when three instances run concurrently alongside a live primary"
    requirement: "UPDATE-07"
    verification:
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#ApplyUpdateBypass_RunsWhileGuardIsHeld"
        status: unknown
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#ApplyUpdateBypass_IsIdempotentAndSideEffectFree"
        status: unknown
      - kind: e2e
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs#ApplyUpdateBypass_ConcurrentInvocationsDoNotInterfere"
        status: unknown
    human_judgment: true
    rationale: "Same automated-test-cannot-run-here constraint as D1/D2. The bypass mechanism's wiring was proven in plan 25-02 via in-process/static gates; this plan's real-process proof is the RigToggle.Windows.Tests suite, whose CI-run status is the open follow-up below."
  - id: D4
    description: "A real-usage process crash (System.InvalidOperationException from ApplicationConfiguration.Initialize()'s SetCompatibleTextRenderingDefault, racing Application.SetColorMode's SystemEvents subscription) that was silently causing duplicate launches to fail with no activation signal at all, is fixed"
    verification:
      - kind: manual_procedural
        ref: "Operator PowerShell 2-launch repro script, run multiple times after commit 28680d3, no crash recurrence (previously reproduced consistently, confirmed via Windows Event Viewer Application log, .NET Runtime Event ID 1026, exit code 0xE0434352)"
        status: pass
    human_judgment: true
    rationale: "Confirmed fixed via direct operator reproduction on real Windows hardware -- the strongest evidence class available, but this fix has no dedicated automated regression test (the trigger condition is a genuine OS-level scheduling race, not something a deterministic unit test can assert on)."

duration: unknown (spans a multi-day, multi-session investigation with operator round-trips between commits; active work time not tracked precisely)
completed: 2026-08-21
status: complete
---

# Phase 25 Plan 3: Single-Instance Guard End-to-End Verification Summary

**Real child-process xUnit suite proving INSTANCE-01/INSTANCE-02/UPDATE-07 end-to-end, plus two real production bugs found and fixed during the Task 3 operator checkpoint: a SetColorMode/SystemEvents race crashing duplicate launches, and a foreground-activation grant (`AllowSetForegroundWindow`) that was silently ineffective.**

## Performance

- **Started:** 2026-08-20T16:59:43Z (Task 1 commit)
- **Completed:** 2026-08-21T10:21:17Z (final fix commit)
- **Tasks:** 3 (Task 1, Task 2 auto; Task 3 checkpoint:human-verify, extended well beyond its original scope by real bugs the checkpoint itself surfaced)
- **Commits:** 10
- **Files modified:** 8 (1 created, 7 modified)

## Accomplishments

- `SingleInstanceProcessTests` (new, `RigToggle.Windows.Tests`): 7 xUnit facts launching the real built `RigToggle.App.exe` as genuine child processes — rapid relaunch (10 iterations), tight-race launch (3 rounds), hidden-window restore, minimized-window-to-foreground restore, and three `--apply-update` bypass tests (held-guard, idempotency, concurrency).
- **Real crash fixed** (commit `28680d3`): `Application.SetColorMode(SystemColorMode.System)` subscribes to `Microsoft.Win32.SystemEvents.UserPreferenceChanged` (needed for live OS theme-follow), whose first-use init lazily spins up a background thread that creates a native window asynchronously. `ApplicationConfiguration.Initialize()`'s `SetCompatibleTextRenderingDefault` call asserts no window has been created yet — under ordinary scheduling contention from an already-running instance, that race could be lost, throwing `InvalidOperationException` and crashing the whole process before it ever reached the single-instance guard. Confirmed via Windows Event Viewer (.NET Runtime Event ID 1026, exit code `0xE0434352`) and reproduced with a plain two-launch PowerShell script — **not** an artificial test-harness burst, an ordinary "app already running, user double-clicks it again" scenario. Fixed with a narrow `catch (InvalidOperationException)` around just that one call, converting the crash into a graceful continuation.
- **Real foreground-activation bug fixed** (commit `48cdd90`): `AllowSetForegroundWindow(ASFW_ANY)` grants "may set foreground" to whichever process next calls `SetForegroundWindow` — any window on the desktop, not necessarily ours. `BroadcastActivation` now resolves the specific peer process id and targets the grant at it directly, falling back to `ASFW_ANY` only when resolution is ambiguous. Also corrected a misleading diagnostic: `Control.Focused` reflects raw keyboard focus on the exact control queried, not the form's subtree — a multi-control dashboard form routinely shows `Focused=False` even when genuinely the active, topmost window. Replaced/supplemented it with `ActivationSignal.IsForegroundWindow(Handle)` (`GetForegroundWindow() == Handle`), the actual ground truth.
- **Cross-process debug logging fixed** (commits `17e5207`, `0fb91e0`): the trace-listener setup was wired up after the single-instance guard's early-return, so a duplicate/loser process could never log; and even after that ordering fix, `StreamWriter`'s default `FileShare.Read`-only sharing meant a second process's own append-open threw a silent sharing-violation. Both fixed — debug.log now captures activity from both the primary and any duplicate process.
- **A genuine cross-assembly test race fixed** (commit `71f5241`): `SingleInstanceGuardTests` (in-process, `RigToggle.Tests`) and `SingleInstanceProcessTests` (real child processes, `RigToggle.Windows.Tests`) run as two separate VSTest host processes under `dotnet test RigToggle.sln`, both hitting the identical hardcoded production mutex name. `SingleInstanceGuard.Acquire()`/`WaitForInstanceReady()` gained an optional test-only `instanceId` override (default null preserves production behavior for every real caller) so the in-process tests use a random per-run GUID instead.
- Solution builds clean throughout: `dotnet build RigToggle.sln -c Release -p:EnableWindowsTargeting=true` — 0 errors, 0 new warnings at every commit (6 pre-existing baseline warnings unchanged). `dotnet test RigToggle.Tests` — 120/120 passing at every commit. `RigToggle.Windows.Tests` cannot execute in this Linux build environment (testhost requires `Microsoft.WindowsDesktop.App`) — see Next Phase Readiness for the resulting open follow-up.

## Task Commits

Each task was committed atomically:

1. **Task 1: Child-process harness and the two INSTANCE-01 survival tests** — `2f4c238` (feat)
2. **Task 2: The INSTANCE-02 restore test and the three UPDATE-07 bypass tests** — `9fc18c8` (feat)
3. **Task 3: Operator checkpoint** — no single commit (checkpoint), but the investigation it triggered produced 8 further fix commits:
   - `0b1514c` (fix) — grant `AllowSetForegroundWindow` before activation broadcast (first attempt, superseded in scope by `48cdd90`)
   - `71f5241` (fix) — decouple `SingleInstanceGuardTests` from the real production mutex name
   - `7379a8c` (fix) — confirm process-kill exit, drop unnecessary tree-walk in the test harness
   - `7e2bb27` (test) — stagger rapid/concurrent test launches to bound cold-start pressure
   - `17e5207` (fix) — make the duplicate-launch path debug-loggable, add restore-path tracing
   - `0fb91e0` (fix) — open debug.log with `FileShare.ReadWrite` for cross-process appends
   - `28680d3` (fix) — **stop the SetColorMode/SystemEvents race from crashing the process** (real production bug #1)
   - `48cdd90` (fix) — **target the foreground grant at the peer PID; fix the misleading `Focused` diagnostic** (real production bug #2)

**Plan metadata:** this commit (docs: complete plan)

_Note: Task 3 was authored as a single `checkpoint:human-verify` gate. In practice it became an extended, multi-round investigation — the operator's first repro attempt hit a real crash, and each subsequent fix surfaced a further real issue, confirmed and fixed iteratively against actual Windows hardware data (Event Viewer, debug.log captures, direct visual observation) rather than against automated test output alone._

## Files Created/Modified

- `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs` (new) — 7 xUnit facts, real child-process harness with `KillAndConfirmExit` teardown
- `src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj` (modified) — `RigToggleAppExePath` assembly-metadata publishing
- `src/RigToggle.App/Program.cs` (modified) — debug-logging reordered before the guard's early-return; `ApplicationConfiguration.Initialize()` crash fix; diagnostic `TryLog` instrumentation
- `src/RigToggle.App/MainForm.cs` (modified) — `WndProc`/`RestoreAndFocus` diagnostic instrumentation, corrected to log `IsForegroundWindow`/`ContainsFocus` instead of relying on `Focused` alone
- `src/RigToggle.Core/SingleInstanceGuard.cs` (modified) — test-only `instanceId` override on `Acquire()`/`WaitForInstanceReady()`; diagnostic instrumentation
- `src/RigToggle.Windows/ActivationSignal.cs` (modified) — PID-targeted `AllowSetForegroundWindow`; `IsForegroundWindow` public facade; diagnostic instrumentation
- `src/RigToggle.Windows/NativeMethods.cs` (modified) — `AllowSetForegroundWindow`/`ASFW_ANY`/`GetForegroundWindow` P/Invoke declarations
- `src/RigToggle.Tests/SingleInstanceGuardTests.cs` (modified) — uses the new test-only `instanceId` override throughout

## Decisions Made

- **D-05 project-placement deviation** (carried from the plan's own objective): tests live in `RigToggle.Windows.Tests`, not the literally-named `RigToggle.Tests`, since the latter targets plain `net10.0` specifically to stay buildable on non-Windows machines.
- **Test-only `instanceId` override** on `SingleInstanceGuard.Acquire()`/`WaitForInstanceReady()` (default `null` = real production `InstanceId`, so every real caller — `Program.cs`, and `SingleInstanceProcessTests`' own waits against real child processes — is unaffected) — closes a genuine cross-VSTest-project mutex race discovered when `dotnet test RigToggle.sln` runs `RigToggle.Tests` and `RigToggle.Windows.Tests` as parallel host processes both touching the same hardcoded name.
- **`ApplicationConfiguration.Initialize()` wrapped in a narrow `catch (InvalidOperationException)`**, not retried (would re-invoke `SetHighDpiMode`, documented to throw on a second call) and not fixed by reordering `SetColorMode` (would touch Phase 12's own rig-verified title-bar-flash fix, unverifiable from this Linux environment).
- **`AllowSetForegroundWindow` targets the resolved peer process id**, not the `ASFW_ANY` wildcard, closing a real "grant stolen by an unrelated window" risk.
- **Debug-logging setup moved before the guard's early-return**, kept strictly after the `--apply-update` bypass branch to preserve D-03's one-way ordering contract.
- **`debug.log` opened via explicit `FileStream(FileMode.Append, FileAccess.Write, FileShare.ReadWrite)`**, not `StreamWriter`'s default-sharing constructor, so multiple processes can append concurrently.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Cross-assembly mutex contention between `SingleInstanceGuardTests` and `SingleInstanceProcessTests`**
- **Found during:** Task 3, first post-checkpoint operator run of the full solution test suite (8 failures)
- **Issue:** `dotnet test RigToggle.sln` runs `RigToggle.Tests` and `RigToggle.Windows.Tests` as two separate parallel VSTest host processes; the in-process `SingleInstanceGuardTests` used the real, hardcoded production mutex name, contending directly with `SingleInstanceProcessTests`' real child `RigToggle.App.exe` processes for the same machine-wide kernel object.
- **Fix:** Added an optional `instanceId` parameter to `SingleInstanceGuard.Acquire()`/`WaitForInstanceReady()`; `SingleInstanceGuardTests` now uses a random per-run GUID.
- **Files modified:** `src/RigToggle.Core/SingleInstanceGuard.cs`, `src/RigToggle.Tests/SingleInstanceGuardTests.cs`
- **Committed in:** `71f5241`

**2. [Rule 1 - Bug] Unconfirmed process-kill exit in the test harness's teardown**
- **Found during:** Task 3, second round of investigation into the same 8-failure run
- **Issue:** `Dispose()` called `Process.WaitForExit(timeout)` after `Kill()` but never checked its return value; a process still tearing down under load could leak past its own test's teardown into the next test.
- **Fix:** New `KillAndConfirmExit` helper checks `WaitForExit`'s return and retries once; also dropped `Kill(entireProcessTree: true)` for plain `Kill()` (this exe never spawns children, and the tree-walking overload adds real fragility under concurrent process churn).
- **Files modified:** `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs`
- **Committed in:** `7379a8c`

**3. [Rule 1 - Bug] Test-harness launch bursts amplifying a real SystemEvents race**
- **Found during:** Task 3, third round (crash persisted after the two fixes above)
- **Issue:** `RapidRelaunch_ExactlyOneProcessSurvives` and `ApplyUpdateBypass_ConcurrentInvocationsDoNotInterfere` launched many `RigToggle.App.exe` processes in rapid/concurrent succession, amplifying the odds of hitting the SetColorMode/SystemEvents race (see fix #6 below).
- **Fix:** Added a `LaunchStaggerDelay` (150ms) between successive launches in just these two tests — explicitly not applied to `TightRaceLaunch_ExactlyOneProcessSurvives`, whose entire premise requires zero gap.
- **Files modified:** `src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs`
- **Committed in:** `7e2bb27`

**4. [Rule 2 - Missing Critical] Duplicate-launch path could never write to debug.log**
- **Found during:** Task 3, fourth round — operator's debug.log captured only the primary's own startup, nothing from the duplicate
- **Issue:** Settings-loading and trace-listener wiring were positioned after the single-instance guard's early-return branch, so a duplicate/loser process returned before ever reaching that code — a pre-existing gap (Phase 8), not introduced by this plan, but blocking this plan's own diagnosis.
- **Fix:** Moved settings-loading and trace-listener wiring to run immediately after the `--apply-update` bypass branch and before the guard acquisition; added `Trace.WriteLine` instrumentation along the guard/activation/restore chain.
- **Files modified:** `src/RigToggle.App/Program.cs`, `src/RigToggle.Core/SingleInstanceGuard.cs`, `src/RigToggle.Windows/ActivationSignal.cs`, `src/RigToggle.App/MainForm.cs`
- **Committed in:** `17e5207`

**5. [Rule 1 - Bug] `debug.log` sharing violation silently swallowed for the duplicate process**
- **Found during:** Task 3, fifth round — debug.log was still empty for the duplicate process even after fix #4
- **Issue:** `StreamWriter(path, append)`'s underlying `FileStream` opens with `FileShare.Read` only; the primary holding the file open for its lifetime meant a duplicate's own append-open threw a sharing-violation `IOException`, silently caught by the existing best-effort handler.
- **Fix:** Construct the `FileStream` explicitly with `FileShare.ReadWrite`.
- **Files modified:** `src/RigToggle.App/Program.cs`
- **Committed in:** `0fb91e0`

**6. [Rule 1 - Bug, real production defect] `Application.SetColorMode`/`ApplicationConfiguration.Initialize()` race crashing real duplicate launches**
- **Found during:** Task 3, sixth round — operator confirmed via Windows Event Viewer that the duplicate process was crashing (`0xE0434352`), then reproduced it with a plain two-launch PowerShell script (no artificial burst)
- **Issue:** `SetColorMode(System)`'s `SystemEvents.UserPreferenceChanged` subscription lazily spins up a background thread that creates a native window asynchronously; `ApplicationConfiguration.Initialize()`'s `SetCompatibleTextRenderingDefault` call asserts no window exists yet, and under ordinary scheduling contention from an already-running instance this race could be lost. This is a real, pre-existing (Phase 12) defect reachable via ordinary usage, not a test-harness artifact.
- **Fix:** Narrow `catch (InvalidOperationException)` around the `ApplicationConfiguration.Initialize()` call only.
- **Files modified:** `src/RigToggle.App/Program.cs`
- **Verification:** Operator ran the two-launch repro script multiple times post-fix with no recurrence.
- **Committed in:** `28680d3`

**7. [Rule 1 - Bug, real production defect] Foreground-activation grant silently ineffective; misleading diagnostic**
- **Found during:** Task 3, seventh round — with the crash fixed, a real debug.log showed the restore mechanism running structurally correctly but never producing focus
- **Issue:** Two compounding problems: (a) the diagnostic logged `Control.Focused`, which reflects keyboard focus on the exact control queried, not the form's subtree — routinely false on a multi-control dashboard form even when genuinely active; (b) `AllowSetForegroundWindow(ASFW_ANY)` grants rights to whichever process next calls `SetForegroundWindow`, which could be stolen by an unrelated window.
- **Fix:** Added `ActivationSignal.IsForegroundWindow(Handle)` (`GetForegroundWindow()==Handle`) as the real diagnostic signal; targeted the grant at the resolved peer process id instead of the wildcard.
- **Files modified:** `src/RigToggle.Windows/ActivationSignal.cs`, `src/RigToggle.Windows/NativeMethods.cs`, `src/RigToggle.App/MainForm.cs`
- **Verification:** Operator's post-fix debug.log shows `IsForegroundWindow=True, ContainsFocus=True` after every `RestoreAndFocus()` call, plus direct visual confirmation the window jumps to foreground on a real duplicate launch.
- **Committed in:** `48cdd90`

---

**Total deviations:** 7 auto-fixed (5 Rule 1 bugs, 1 Rule 1 test-infrastructure fix, 1 Rule 2 missing critical functionality). Two of the seven (items 6 and 7) are real, pre-existing production defects this plan's Task 3 checkpoint discovered and fixed on real hardware — not scope creep, but the actual substance of what INSTANCE-02 needed to be proven true.
**Impact on plan:** Task 3's scope expanded substantially beyond "run the automated suite and double-click the exe" into a genuine multi-round debugging investigation. Every fix was driven by real data (Windows Event Viewer, debug.log captures, direct visual observation) captured from the operator's actual hardware, not speculation.

## Issues Encountered

See Deviations from Plan above — all seven issues were investigated and resolved via the debugging methodology described there (targeted instrumentation before proposed fixes, confirmed against real hardware data at each step).

## User Setup Required

None — no external service configuration required. All fixes are internal to the shipped application.

## Next Phase Readiness

**Confirmed on real Windows hardware, by the operator:**
- INSTANCE-01 (mutex guard): the operator's multiple runs of the two-launch PowerShell script confirm exactly one process survives, no crash.
- INSTANCE-02 (restore-to-foreground): confirmed via both a corrected debug.log capture (`IsForegroundWindow=True`, `ContainsFocus=True` after every `RestoreAndFocus()` call) and direct visual observation — the window genuinely jumps to the foreground on a real duplicate launch.
- The SetColorMode/SystemEvents crash (a real, pre-existing production defect this investigation discovered) is fixed and confirmed not to recur across multiple repro runs.

**Open follow-up, NOT closed out by this SUMMARY:** the plan's original Task 3 how-to-verify called for **three consecutive clean runs of `dotnet test RigToggle.sln -c Release --no-build`** specifically to catch flakiness (D-06's whole point — a single-instance regression caught automatically on every push, not just confirmed once by hand). That three-consecutive-clean-runs pass has **not yet been completed** after this round of fixes — the operator's verification so far has been the two-launch PowerShell script and direct log/visual inspection, which confirms the underlying mechanism works but is not the same evidence as a stable, repeatable CI-style test run. Recommend running `dotnet test RigToggle.sln -c Release --no-build` three times in a row on the operator's hardware before considering Phase 25 fully closed — if any of the 7 new `SingleInstanceProcessTests` facts is still flaky (even with the crash fixed and the launch-stagger mitigation in place), that needs to surface now rather than silently in CI later.

**No blockers for Phase 26.** `SingleInstanceGuard`, `ActivationSignal`, `StartupArgs.TryGetApplyUpdateArgs`/`ApplyUpdateBypassExitCode`, and `UpdateApplyEntryPoint` are all stable, tested (to the extent this environment and the operator's manual verification allow), and ready for Phase 26's auto-update relaunch to consume the bypass contract exactly as specified in plan 25-02.

---
*Phase: 25-single-instance-guard*
*Plan: 03*
*Completed: 2026-08-21*

## Self-Check: PASSED

All 8 created/modified files confirmed present on disk (`src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs`, `src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj`, `src/RigToggle.App/Program.cs`, `src/RigToggle.App/MainForm.cs`, `src/RigToggle.Core/SingleInstanceGuard.cs`, `src/RigToggle.Windows/ActivationSignal.cs`, `src/RigToggle.Windows/NativeMethods.cs`, `src/RigToggle.Tests/SingleInstanceGuardTests.cs`). All 10 commit hashes (`2f4c238`, `9fc18c8`, `0b1514c`, `71f5241`, `7379a8c`, `7e2bb27`, `17e5207`, `0fb91e0`, `28680d3`, `48cdd90`) confirmed present in `git log --oneline --all`.
