---
phase: 25-single-instance-guard
plan: 02
subsystem: infra
tags: [startup-gate, cli-parsing, p-invoke-free, dotnet, single-instance]

requires:
  - phase: 25-single-instance-guard (plan 01)
    provides: "SingleInstanceGuard, Program.cs startup-gate ordering with guard acquisition immediately after ApplicationConfiguration.Initialize()"
provides:
  - "StartupArgs.ApplyUpdateFlag / TryGetApplyUpdateArgs / ApplyUpdateBypassExitCode (RigToggle.Core): the `--apply-update` CLI flag, its opaque-trailing-payload parse contract, and the bypass exit code — the exact cross-phase contract Phase 26 (UPDATE-07) will bind to"
  - "UpdateApplyEntryPoint.Run(string[]) (RigToggle.App): side-effect-free placeholder relaunch-helper entry point, reached only from the first branch of Main()"
  - "Program.cs first-branch bypass wiring: --apply-update short-circuits before the single-instance guard, settings/mode bootstrap, and any Form construction"
affects: [25-03-single-instance-guard, 26-auto-update]

actuals:
  tokens: 3757
  tasks: 3
  commits: 4

tech-stack:
  added: []
  patterns:
    - "RigToggle.Core static-helper-parses / Program.cs-branches idiom extended to a second flag (TryGetApplyUpdateArgs mirrors ShouldStartHidden's exact-token, StringComparer.OrdinalIgnoreCase, never-throws shape)"
    - "Opaque trailing-payload extraction: Try*(string[]?, out string[]) returns every token after the first match, unvalidated, so the cross-phase contract stays minimal and Phase 26 owns the payload's meaning"

key-files:
  created:
    - src/RigToggle.App/UpdateApplyEntryPoint.cs
  modified:
    - src/RigToggle.Core/StartupArgs.cs
    - src/RigToggle.Tests/StartupArgsTests.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "Task 1 checkpoint resolved to option-a (opaque trailing payload): TryGetApplyUpdateArgs(string[]? args, out string[] payload) returns every token after the first case-insensitive --apply-update match, in order, unvalidated and uninterpreted. Phase 26 owns and can freely evolve the positional meaning without touching Phase 25's parser or its tests. Selected over option-b (typed record, rejected as prematurely committing to Phase 26's unverified apply-mechanism schema) and option-c (flag-only predicate, rejected as contradicting D-04's Try*-with-payload shape). Recorded here verbatim as the resolution of D-03/D-04's one-way surface, per the plan's output instruction."
  - "Worktree infrastructure recovery: the orchestrator-provisioned worktree directory did not exist on disk at task start (only its task-output artifact existed under /tmp). The first RED test commit landed accidentally on the main repo's master branch because Bash/Read/Edit calls silently defaulted to the main checkout. Recovered by reverting the stray master commit (git revert, non-destructive) and self-provisioning the missing worktree via `git worktree add` at the exact expected path/branch name (worktree-agent-ac71828e093a895b0) before redoing all work correctly inside it. No plan-scoped code or behavior was affected; master is clean at the pre-existing tip plus one revert pair."

patterns-established:
  - "Comment-filtered negative-grep audit gate for side-effect-free placeholder entry points (`grep -vE '^\\s*(///|//|\\*|/\\*)' <file> | grep -cE '<forbidden-API-list>'`) — proves emptiness mechanically rather than by convention, reusable for any future placeholder hand-off point."

requirements-completed: [UPDATE-07]

coverage:
  - id: D1
    description: "The one-way --apply-update flag name and opaque-trailing-payload parse contract were confirmed at a blocking decision checkpoint (Task 1) before being published; StartupArgs exposes the flag and exit-code constants publicly so both Phase 26 and the test project can name them."
    requirement: "UPDATE-07"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/StartupArgsTests.cs#TryGetApplyUpdateArgs_DetectsExactToken_D04 (and 7 companion Fact methods)"
        status: pass
    human_judgment: false
  - id: D2
    description: "Main() checks the --apply-update flag as its first branch, strictly above the single-instance gate and above every bootstrap step, and returns after publishing UpdateApplyEntryPoint's exit code via Environment.ExitCode."
    requirement: "UPDATE-07"
    verification:
      - kind: other
        ref: "line-ordering assertion: grep -n line number of StartupArgs.TryGetApplyUpdateArgs (63) > ApplicationConfiguration.Initialize() (52) and < using var guard (81), in src/RigToggle.App/Program.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: "The Phase 25 entry point (UpdateApplyEntryPoint.Run) does nothing observable except return the bypass exit code — provably, by a comment-filtered negative grep over File./Directory./Process./Registry./HttpClient/Mutex/EventWaitHandle/Acquire(."
    requirement: "UPDATE-07"
    verification:
      - kind: other
        ref: "grep -vE '^\\s*(///|//|\\*|/\\*)' src/RigToggle.App/UpdateApplyEntryPoint.cs | grep -cE 'File\\.|Directory\\.|Process\\.|Registry\\.|HttpClient|Mutex|EventWaitHandle|Acquire\\(' => 0"
        status: pass
    human_judgment: false
  - id: D4
    description: "No generic guard-disable token exists anywhere in the touched files (StartupArgs.cs, Program.cs, UpdateApplyEntryPoint.cs) — the bypass stayed a specific, dedicated, single-purpose flag (D-03)."
    requirement: "UPDATE-07"
    verification:
      - kind: other
        ref: "grep -rcE 'skip-instance|no-guard|force-start|ignore-instance|--force' over all three files => 0"
        status: pass
    human_judgment: false
  - id: D5
    description: "End-to-end proof that the bypass actually survives a held guard (a real second process launched with --apply-update while a normal instance holds the mutex) is out of this plan's scope — it is plan 25-03's D-05 bypass-simulation test, launching the real built exe as a child process."
    verification: []
    human_judgment: true
    rationale: "This plan proves the wiring, ordering, and emptiness in-process and by static grep/build gates only. Spawning a real second OS process against a real held single-instance mutex — the actual UPDATE-07 end-to-end claim — requires plan 25-03's dedicated test infrastructure, per this plan's own <verification> section and 25-CONTEXT.md's D-05/D-06."

duration: 35min
completed: 2026-08-20
status: complete
---

# Phase 25 Plan 02: Internal Relaunch Bypass Contract Summary

**`--apply-update` startup-gate bypass: `StartupArgs.TryGetApplyUpdateArgs` (opaque trailing-payload parse contract, option-a) checked as the first branch in `Main()`, transferring control to a deliberately empty `UpdateApplyEntryPoint.Run` before the single-instance guard is ever touched.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-08-20T16:16:24Z
- **Completed:** 2026-08-20T16:51:14Z
- **Tasks:** 3 (1 checkpoint, 2 auto)
- **Files modified:** 4 (1 created, 3 modified)

## Accomplishments

- **Task 1 (checkpoint):** the one-way parse-contract decision was surfaced to the human via a blocking `checkpoint:decision` and resolved to **option-a** (opaque trailing payload) — see Decisions Made.
- `StartupArgs` (`RigToggle.Core`) gains `ApplyUpdateFlag` (public const, `"--apply-update"`), `ApplyUpdateBypassExitCode` (public const, `20`), and `TryGetApplyUpdateArgs(string[]?, out string[])` — exact-token, case-insensitive, never-throwing, mirroring `ShouldStartHidden`'s existing discipline; returns every token after the first match, unvalidated, or a non-null empty array on the false path.
- `StartupArgsTests` extended with 13 new test cases (6-row detection theory + 7 payload/null/coexistence facts), each doc-commented with the decision ID (D-03/D-04) it proves. Full suite: 120/120 passing (107 baseline + 13 new).
- `UpdateApplyEntryPoint` (new, `RigToggle.App`): `internal static int Run(string[])` returning the bypass exit code, provably side-effect-free (no filesystem/process/registry/network/named-kernel-object access) via a comment-filtered negative-grep audit gate.
- `Program.cs`: the bypass branch is inserted immediately after `ApplicationConfiguration.Initialize()` and strictly above `using var guard = SingleInstanceGuard.Acquire()` — publishes the exit code and returns before the guard, settings/mode-store bootstrap, or any `Form` is touched. Verified by line-number ordering assertion, not just code review.
- Solution builds with 0 errors, 6 pre-existing warnings (unchanged from the 25-01 baseline — no new warnings introduced).

## Task Commits

Each task was committed atomically:

1. **Task 1: Confirm the one-way `--apply-update` parse contract** — checkpoint, no commit (decision-only; human selected option-a)
2. **Task 2: Parse the flag in Core, test-first** — `0a38c38` (test, RED) then `79116a3` (feat, GREEN)
3. **Task 3: Wire the bypass as the first branch in Main()** — `219288d` (feat)

**Plan metadata:** pending (this commit)

_Note: Task 2 is a TDD task — RED (`0a38c38`) then GREEN (`79116a3`), both included above. An accidental commit (`b333936`) that landed on the main repo's `master` branch during worktree-infrastructure recovery was reverted (`6e88292`) before the correct RED commit was made inside the actual isolated worktree — see Deviations from Plan._

## Files Created/Modified

- `src/RigToggle.Core/StartupArgs.cs` (modified) — adds `ApplyUpdateFlag`, `ApplyUpdateBypassExitCode`, `TryGetApplyUpdateArgs`; `ShouldStartHidden` and `TrayFlag` untouched
- `src/RigToggle.Tests/StartupArgsTests.cs` (modified) — 13 new test methods covering detection, payload extraction/ordering, absence, null/empty safety, and tray-flag coexistence
- `src/RigToggle.App/UpdateApplyEntryPoint.cs` (new) — placeholder relaunch-helper entry point, `Run(string[])` returns the bypass exit code, no other observable behavior
- `src/RigToggle.App/Program.cs` (modified) — bypass branch as the first branch in `Main()`, above the single-instance guard; class remarks extended to document the new path

## Decisions Made

- **Task 1 checkpoint resolved to option-a (opaque trailing payload), verbatim as approved:** `TryGetApplyUpdateArgs(string[]? args, out string[] payload)` returns every token after the first case-insensitive `--apply-update` occurrence, in order, unvalidated and uninterpreted. Phase 26 owns and can freely evolve the positional meaning without ever touching Phase 25's parser or this plan's tests. This resolves D-03/D-04's one-way parse-contract surface — Phase 26 (UPDATE-07) binds directly to this shape when it replaces `UpdateApplyEntryPoint.Run`'s body.
- Implemented the exact-token match via `Array.FindIndex(args, arg => StringComparer.OrdinalIgnoreCase.Equals(arg, ApplyUpdateFlag))` rather than `args.Contains(...)` (which `ShouldStartHidden` uses) — `TryGetApplyUpdateArgs` needs the match *index* (to slice the trailing payload), not just presence, so `FindIndex` is the natural equivalent while still using the same `StringComparer.OrdinalIgnoreCase` comparison discipline (acceptance criterion: exactly 2 occurrences of `StringComparer.OrdinalIgnoreCase` in the file, one per helper).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Worktree infrastructure was not provisioned; recovered by self-provisioning the missing worktree**
- **Found during:** Start of Task 2, immediately after the Task 1 checkpoint was approved
- **Issue:** The orchestrator's stated isolated-worktree path (`/home/bpivk/moza/.claude/worktrees/agent-ac71828e093a895b0`) did not exist on disk (confirmed via `find` and a failed `cd`); only a harness task-output log file with a matching name existed under `/tmp`. Bash calls without an explicit `cd` silently defaulted to the main repo checkout (`/home/bpivk/moza`, branch `master`), and Read/Edit calls using main-repo-style absolute paths (rather than worktree-prefixed paths) operated on the main repo's working tree. As a result, the first RED test commit (`test(25-02): add failing tests for TryGetApplyUpdateArgs`, hash `b333936`) landed directly on `master` in the main repository — a direct violation of the worktree-isolation requirement.
- **Fix:** (1) Reverted the stray commit non-destructively with `git revert --no-edit b333936` (produced `6e88292`), restoring `master` to its pre-existing state — no `git reset --hard`, no branch rewrite, no force-push. (2) Self-provisioned the missing worktree with `git worktree add /home/bpivk/moza/.claude/worktrees/agent-ac71828e093a895b0 -b worktree-agent-ac71828e093a895b0 master`, recreating exactly the isolated worktree and per-agent branch name the orchestrator's original instructions specified. (3) Verified isolation before every subsequent commit: confirmed `git rev-parse --git-dir` resolved to `/home/bpivk/moza/.git/worktrees/agent-ac71828e093a895b0` (not the main repo's `.git`) and `git rev-parse --abbrev-ref HEAD` returned the per-agent branch name, matching the allow-list pattern from the pre-commit HEAD safety assertion. (4) Redid Task 2's RED and GREEN commits, and all of Task 3, correctly inside the recreated worktree using worktree-prefixed absolute paths for every Read/Edit/Write call and a `cd <worktree> &&` prefix on every Bash call (cwd was confirmed to reset to the main repo between every Bash invocation, so this prefix is required on every command, not just the first).
- **Files modified:** None outside plan scope — `master`'s net diff after the revert pair is identical to its pre-task state; all actual plan code changes live only on `worktree-agent-ac71828e093a895b0`.
- **Verification:** `git log --oneline` on `master` shows the accidental commit immediately followed by its clean revert, with no other history disturbed. `git worktree list` and `git rev-parse --git-dir`/`--abbrev-ref HEAD` confirmed correct isolation for every commit made afterward (`0a38c38`, `79116a3`, `219288d`).
- **Committed in:** `6e88292` (revert, on `master`, outside this plan's own commit sequence)

### Documented False Positives (not fixed)

**1. `grep -c 'throw' src/RigToggle.Core/StartupArgs.cs` outputs `3`, not the plan's stated `0`**
- The plan's acceptance criterion for Task 2 states this gate should output `0` ("the never-throws contract holds structurally, not just by test"). Actual output is `3` — all three matches are inside `///` doc-comment prose describing the never-throw contract (`"may ever throw on null/empty/garbage args"`, `"Never indexes into args and never throws"`, `"throws for any input"`), not executable `throw` statements. The pre-existing baseline file (before this plan touched it) already contained 2 such doc-comment occurrences (`"must never throw on null/empty/garbage args"` in the class summary, `"never throws"` in `ShouldStartHidden`'s own doc comment) — so the literal `0` criterion was already unsatisfiable against the untouched baseline, and the plan's own Task 2 `<action>` explicitly *requires* "restating the never-throw contract" in the extended class doc comment, which necessarily reintroduces the substring. Confirmed via manual read and `git diff -U0` that `ShouldStartHidden`'s own doc comment and body were not modified (only the class-level summary comment was extended), and that there are zero actual `throw` *statements* anywhere in the file. This mirrors 25-01-SUMMARY.md's precedent (Gate A's `PackageReference`-in-comment false positive) — documented rather than fixed by degrading required documentation.
- **Not fixed because:** stripping the word "throw" from the doc comments to force the count to `0` would mean omitting the very never-throw contract documentation the plan's own `<action>` instructs be written, which is a worse outcome than an accurate, documented gate false positive.

**2. `grep -c 'PackageReference' src/RigToggle.Core/RigToggle.Core.csproj` outputs `1`, not `0`**
- Same pre-existing false positive documented in 25-01-SUMMARY.md: the single match is inside an explanatory comment predating both plans ("Do NOT add a PackageReference to WindowsDisplayAPI or NAudio here..."), not an actual `<PackageReference>` XML element. `git diff --stat HEAD~2 -- '*.csproj'` confirms `0` — no `.csproj` file was touched by this plan's commits.

---

**Total deviations:** 1 auto-fixed (Rule 3, infrastructure recovery, fully self-contained to `master`'s revert pair — no plan-scoped code affected), 2 documented false positives (pre-existing/plan-mandated, not fixed)
**Impact on plan:** Zero impact on the shipped code's correctness or scope. The worktree-infrastructure incident affected only *where* commits initially landed, not *what* was built; it was fully and non-destructively corrected before any Task 2/3 code was finalized. Both grep false positives are mechanical gate limitations against required documentation/pre-existing comments, not actual defects.

## Issues Encountered

See Deviations from Plan above — the worktree-provisioning gap was the only issue encountered, and it was fully resolved before Task 2/3 work proceeded.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `StartupArgs.ApplyUpdateFlag`, `TryGetApplyUpdateArgs`, and `ApplyUpdateBypassExitCode` are the final, real contract Phase 26 (UPDATE-07) will bind to — the flag token, parse shape, and exit code are locked per the Task 1 checkpoint decision (option-a) and must not change without touching both phases.
- `UpdateApplyEntryPoint.Run(string[])`'s signature and return contract are final; Phase 26 replaces only the method body with real update-apply logic (download/rename/file-swap/relaunch), which was explicitly out of scope here per the plan's prohibitions.
- Plan 25-03 owns the remaining end-to-end proof this plan's `must_haves` explicitly defers: a real second OS process launched with `--apply-update` while a normal instance holds the single-instance mutex, proving the bypass actually survives a held guard (D-05's bypass-simulation test, launching the real built exe as a child process) — this plan proves only the wiring/ordering/emptiness via in-process and static grep/build gates.
- No blockers for 25-03 or Phase 26. The worktree-infrastructure gap discovered during this plan (expected path not provisioned by the orchestrator) is worth flagging to the orchestrator/user for the *next* wave's setup step, since it required self-recovery here.

---
*Phase: 25-single-instance-guard*
*Plan: 02*
*Completed: 2026-08-20*

## Self-Check: PASSED

All 5 created/modified files confirmed present on disk (`src/RigToggle.Core/StartupArgs.cs`, `src/RigToggle.Tests/StartupArgsTests.cs`, `src/RigToggle.App/Program.cs`, `src/RigToggle.App/UpdateApplyEntryPoint.cs`, this SUMMARY.md). All 3 task commit hashes (`0a38c38`, `79116a3`, `219288d`) confirmed present in `git log --oneline --all`.
