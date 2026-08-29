# Deferred Items — Phase 26 Plan 01

Out-of-scope discoveries logged per the executor's SCOPE BOUNDARY rule (only
auto-fix issues directly caused by the current task's changes; pre-existing
warnings/failures in unrelated files are logged here, not fixed).

## Pre-existing xUnit1031 warnings (6, unrelated to this plan)

`26-01-PLAN.md`'s Task 1 `<verify>` requires
`dotnet build RigToggle.sln -c Release --nologo` to report `0 Warning(s)`. A clean
(`--no-incremental`) build of this worktree at the plan's starting commit already
reports 6 pre-existing `xUnit1031` ("Test methods should not use blocking task
operations") warnings, entirely in files this plan does not touch:

- `src/RigToggle.Tests/SingleInstanceGuardTests.cs:198`
- `src/RigToggle.Tests/SingleInstanceGuardTests.cs:199`
- `src/RigToggle.Tests/ToggleOrchestratorTests.cs:131`
- `src/RigToggle.Tests/ToggleOrchestratorTests.cs:157`
- `src/RigToggle.Tests/ToggleOrchestratorTests.cs:190`
- `src/RigToggle.Tests/ToggleOrchestratorTests.cs:292`

Neither file is in `26-01-PLAN.md`'s `files_modified` list. Verified via a baseline
`--no-incremental` build before any Plan 26-01 changes were made, and re-confirmed
after Task 1's implementation was committed — the warning count and locations are
unchanged; this plan's own new/modified files introduce 0 new warnings.

Not fixed here per the executor's scope boundary. Left for a future
cleanup/gap-closure pass if the zero-warning gate needs to be genuinely restored
project-wide.
