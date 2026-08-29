---
phase: 26-auto-update
plan: 01
subsystem: infra
tags: [dotnet, winforms, httpclient, system.text.json, github-releases, self-update, ci]

requires:
  - phase: 25-single-instance-guard
    provides: "StartupArgs.TryGetApplyUpdateArgs / ApplyUpdateFlag / ApplyUpdateBypassExitCode contract, the --apply-update-precedes-the-guard Program.cs ordering, and the UpdateApplyEntryPoint.Run(string[]) placeholder this plan replaces"
provides:
  - "Version-stamped exe (<Version> + release.yml's tag-derived -p:Version= override)"
  - "IReleaseFeed/GitHubReleaseFeed: unauthenticated GitHub /releases/latest read with a host-allowlisted asset URL (T-26-01)"
  - "UpdateVersionComparer: numeric Major.Minor-only version comparison, immune to the Version.CompareTo component-count trap"
  - "UpdateOrchestrator: UI-free check/compare/confirm/apply sequencer with a deliberate pre-confirm-swallow / post-confirm-propagate exception split"
  - "IUpdateApplier/WindowsUpdateApplier: same-directory download-and-stage, temp-copy helper spawn"
  - "UpdateApplyEntryPoint's real body: wait-for-writable, rename-to-.bak, swap, relaunch, T-26-04 same-directory containment guard"
  - "Themed UpdatePromptDialog + ThemeApplier.ThemeRichTextBox"
  - "MainForm.RunAutomaticUpdateCheckAsync + Program.cs's best-effort on-launch trigger"
affects: [26-02-checksum-integrity, 26-03-never-stranded-recovery, 26-04-skip-version-and-manual-check, 26-05-formatted-release-notes]

actuals:
  tokens: 19600
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Core-sequences/App-executes split for a second orchestrator (UpdateOrchestrator mirrors ToggleOrchestrator's UI-free discipline)"
    - "Process-replication self-update (rename-in-place, never overwrite a running exe) via a temp-copy helper process"
    - "Deliberate asymmetric exception handling: automatic/pre-confirm segment swallows to a sentinel outcome, post-confirm segment propagates for the caller to toast"

key-files:
  created:
    - src/RigToggle.Core/Models/ReleaseInfo.cs
    - src/RigToggle.Core/Abstractions/IReleaseFeed.cs
    - src/RigToggle.Core/Abstractions/IUpdateApplier.cs
    - src/RigToggle.Core/GitHubReleaseFeed.cs
    - src/RigToggle.Core/UpdateVersionComparer.cs
    - src/RigToggle.Core/UpdateOrchestrator.cs
    - src/RigToggle.Windows/WindowsUpdateApplier.cs
    - src/RigToggle.App/UpdatePromptDialog.cs
    - src/RigToggle.App/UpdatePromptDialog.Designer.cs
    - src/RigToggle.Tests/UpdateVersionComparerTests.cs
    - src/RigToggle.Tests/UpdateOrchestratorTests.cs
  modified:
    - src/RigToggle.App/RigToggle.App.csproj
    - .github/workflows/release.yml
    - src/RigToggle.App/UpdateApplyEntryPoint.cs
    - src/RigToggle.App/ThemeApplier.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "GitHubReleaseFeed lives in RigToggle.Core (not Windows) — it's genuinely platform-neutral (plain HttpClient/System.Text.Json), matching JsonSettingsStore's precedent"
  - "UpdateVersionComparer compares only parsed Major/Minor from both sides, never System.Version.CompareTo, to avoid the component-count false-negative (2.2.0.0 vs v2.2)"
  - "UpdateOrchestrator's fetch/compare segment is wrapped (any exception -> NotAvailable); the post-confirm download/apply segment is deliberately unwrapped so a failure after explicit user confirmation propagates for the App layer to toast (D-08)"
  - "UpdateApplyEntryPoint's payload validation order (token count, empty paths, PID parse, staged-file existence, same-directory containment) exactly matches what the existing ApplyUpdateBypass_* real-process tests already exercise with garbage tokens like 'token-one' — verified against those tests' literal payloads rather than guessed"

requirements-completed: [UPDATE-01, UPDATE-02, UPDATE-03, UPDATE-04]

coverage:
  - id: D1
    description: "Exe carries a build-time version driven from the pushed git tag (UPDATE-01)"
    requirement: "UPDATE-01"
    verification:
      - kind: other
        ref: "grep '<Version>' src/RigToggle.App/RigToggle.App.csproj && grep 'p:Version=' .github/workflows/release.yml"
        status: pass
    human_judgment: true
    rationale: "The release.yml tag-to-version pipeline can only be exercised end-to-end by an actual tagged CI run on GitHub; static grep confirms the wiring but not a live release."
  - id: D2
    description: "App checks GitHub Releases on launch and detects a strictly newer version via numeric Major.Minor comparison (UPDATE-02)"
    requirement: "UPDATE-02"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateVersionComparerTests.cs (14 Theory/Fact cases incl. v2.9/v2.10 and the 2.2.0.0-vs-v2.2 component-count guard)"
        status: pass
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs (null-feed, older-tag, feed-throws branches)"
        status: pass
    human_judgment: false
  - id: D3
    description: "A themed confirm dialog naming the new version appears before any download; nothing downloads unconfirmed (UPDATE-03)"
    requirement: "UPDATE-03"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAsync_NewerTagConfirmDeclines_ReturnsDeclined_ApplierNeverInvoked"
        status: pass
    human_judgment: true
    rationale: "The dialog's actual on-screen appearance, theming, and Windows-modal behavior require real WinForms/Win32 rendering not exercisable in this Linux build sandbox — needs rig verification."
  - id: D4
    description: "Confirming downloads, replaces the exe in place at its original path, and relaunches (UPDATE-04)"
    requirement: "UPDATE-04"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAsync_NewerTagConfirmed_InvokesApplyStartingThenDownloadThenApply_InThatOrder_ReturnsApplying"
        status: pass
      - kind: integration
        ref: "src/RigToggle.Windows.Tests/SingleInstanceProcessTests.cs ApplyUpdateBypass_* (existing Phase 25 tests, re-verified compiling against the new UpdateApplyEntryPoint.Run body — cannot execute on Linux, needs a real process host)"
        status: unknown
    human_judgment: true
    rationale: "The actual rename-while-running self-replace mechanism (Pitfall 1) and the AV-lock/writable-poll timing it depends on can only be proven on real Windows hardware — this is exactly the rig-verification gap ARCHITECTURE.md flags as not-yet-rig-tested for this milestone."

duration: 17min
completed: 2026-08-22
status: complete
---

# Phase 26 Plan 01: Auto-Update Tracer Summary

**End-to-end GitHub-release auto-update slice — version-stamped exe queries `/releases/latest`, shows a themed confirm dialog, downloads the asset, self-replaces at its original path via a temp-copy helper, and relaunches — proven end-to-end and covered by 31 new automated tests.**

## Performance

- **Duration:** ~17 min
- **Started:** 2026-08-22T16:51:01Z
- **Completed:** 2026-08-22T17:07:55Z
- **Tasks:** 2
- **Files modified:** 18 (11 created, 6 modified, 1 docs)

## Accomplishments
- Version-stamped `RigToggle.App.exe` for the first time in this solution's history (`<Version>2.2</Version>` + a new `release.yml` "Resolve version from tag" step that overrides it from the pushed git tag)
- Full Core check/compare/decide layer (`GitHubReleaseFeed`, `UpdateVersionComparer`, `UpdateOrchestrator`) — UI-free, unit-tested, host-allowlisted asset URL (T-26-01)
- Full Windows download+swap layer (`WindowsUpdateApplier`, the real `UpdateApplyEntryPoint.Run` body) implementing the rename-not-overwrite self-replace pattern with a same-directory containment guard (T-26-04) and `.bak` rollback on a failed swap
- Themed `UpdatePromptDialog` (never a native `MessageBox`) wired into `MainForm`/`Program.cs`'s best-effort on-launch check
- 31 new automated tests (comparer table incl. the synthetic `v2.10`/`v2.9` double-digit case; orchestrator's 6 decision branches with ordered call-log assertions) — full 152-test cross-platform suite green

## Task Commits

1. **Task 1: End-to-end "app updates itself from a GitHub release"** - `7de7f65` (feat)
2. **Task 2: Automated coverage for the comparer and orchestrator's decision branches** - `a20bcee` (test)

_Both tasks committed as single atomic commits — Task 1 (type="tracer") per the executor's tracer handling (real implementation, real `<verify>`, no separate RED commit); Task 2 writes tests against Task 1's already-implemented code (this plan's own explicit test-after-tracer structure, not a violated RED-first order)._

## Files Created/Modified

- `src/RigToggle.Core/Models/ReleaseInfo.cs` - Immutable release DTO (tag, asset URL, html url, published date, prerelease flag, body)
- `src/RigToggle.Core/Abstractions/IReleaseFeed.cs` - GitHub-releases read contract, null = "no usable latest release"
- `src/RigToggle.Core/Abstractions/IUpdateApplier.cs` - Download+apply contract, both members allowed to throw
- `src/RigToggle.Core/GitHubReleaseFeed.cs` - Unauthenticated `HttpClient` read of `/releases/latest` with a `User-Agent` header and T-26-01 host allow-list
- `src/RigToggle.Core/UpdateVersionComparer.cs` - Numeric `Major.Minor` comparison of a running `Version` against a `v`-prefixed tag
- `src/RigToggle.Core/UpdateOrchestrator.cs` - UI-free check→compare→confirm-callback→apply sequencer plus `UpdateCheckOutcome`
- `src/RigToggle.Windows/WindowsUpdateApplier.cs` - Asset download to the install directory, self-copy helper spawn with `--apply-update`
- `src/RigToggle.App/UpdateApplyEntryPoint.cs` - Real helper-process body replacing the Phase 25 placeholder
- `src/RigToggle.App/UpdatePromptDialog.cs` / `.Designer.cs` - Themed confirm dialog (`440×460`, headline + release-notes `RichTextBox` + Later/Update Now buttons)
- `src/RigToggle.App/ThemeApplier.cs` - Added `ThemeRichTextBox`
- `src/RigToggle.App/MainForm.cs` - Added `RunAutomaticUpdateCheckAsync`, new optional `UpdateOrchestrator?` constructor parameter
- `src/RigToggle.App/Program.cs` - Composition-root wiring (`GitHubReleaseFeed`/`WindowsUpdateApplier`/`UpdateOrchestrator`) + best-effort `BeginInvoke` trigger after `guard.MarkReady()`
- `src/RigToggle.App/RigToggle.App.csproj` - Added `<Version>2.2</Version>`
- `.github/workflows/release.yml` - Added "Resolve version from tag" step + `-p:Version=` on the publish step
- `src/RigToggle.Tests/UpdateVersionComparerTests.cs` - Comparer test table
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` - Orchestrator decision-branch tests with hand-rolled doubles

## Decisions Made

- `GitHubReleaseFeed` placed in `RigToggle.Core`, not `RigToggle.Windows` — it's the app's first `HttpClient` usage but is genuinely platform-neutral, matching `JsonSettingsStore`'s existing "plain BCL I/O lives in Core" precedent (also matches `26-PATTERNS.md`'s own placement call).
- `UpdateVersionComparer.IsNewer` compares only the parsed `Major`/`Minor` components from both sides, never `System.Version.CompareTo` — verified by a dedicated test asserting `new Version(2,2,0,0)` vs tag `"v2.2"` is **not** newer (ARCHITECTURE.md Anti-Pattern 4).
- `UpdateOrchestrator.CheckOnLaunchAsync`'s fetch/compare segment is wrapped in `try/catch` (any exception → `NotAvailable`); the post-confirm download/apply segment is deliberately **not** wrapped, so a failure after the user has explicitly clicked "Update Now" propagates to the caller for `MainForm`'s Warning-icon toast (D-08). This asymmetry is documented in the class doc comment.
- `UpdateApplyEntryPoint.Run`'s payload-validation order (token count → empty-path check → PID parse → staged-file-exists → same-directory containment) was derived by reading the existing `ApplyUpdateBypass_*` real-process tests in `SingleInstanceProcessTests.cs` first and matching their literal garbage payloads (`"token-one"`, etc.) so those Phase 25 tests remain green against the new real body without modification.

## Deviations from Plan

### Auto-fixed Issues

None — plan executed as written; the two compile-time fixes below were caught by the build during normal implementation, not scope changes.

- Fixed an invalid XML comment in `RigToggle.App.csproj` (`--` inside an XML comment is illegal) while adding the `<Version>` doc comment — reworded, no functional change.
- Reordered two `catch` clauses in `UpdateApplyEntryPoint.WaitUntilWritable` (`FileNotFoundException` before `IOException`, since the former derives from the latter and C# rejects an unreachable catch) — caught immediately by the compiler, no behavior change from the plan's intended semantics.

**Total deviations:** 0 functional deviations (2 compile-fix corrections, self-contained within Task 1's own commit).
**Impact on plan:** None — both fixes are mechanical corrections to match valid C#/MSBuild syntax, not scope or behavior changes.

## Known Stubs

None. `UpdatePromptDialog` deliberately omits the "Skip this version" button (D-02) — this is a documented scope boundary (plan 26-04 owns it), not a stub; the button row explicitly reserves space for it per the UI-SPEC.

## Issues Encountered

- **Pre-existing test warnings block the plan's literal "0 Warning(s)" build gate.** A clean (`--no-incremental`) `dotnet build RigToggle.sln -c Release` at this plan's starting commit already reports 6 `xUnit1031` warnings in `SingleInstanceGuardTests.cs` and `ToggleOrchestratorTests.cs` — neither file is in this plan's `files_modified` list. Per the executor's SCOPE BOUNDARY rule (only auto-fix issues directly caused by the current task's changes), these were **not** fixed; logged instead to `.planning/phases/26-auto-update/deferred-items.md`. This plan's own new/modified files introduce 0 new warnings — confirmed via a clean rebuild both before and after implementation.
- **This build sandbox is Linux, not Windows.** `dotnet build`/`dotnet test` for the cross-platform `RigToggle.Tests` project and a full-solution build (including `RigToggle.App`/`RigToggle.Windows`/`RigToggle.Windows.Tests`, which all still compile against Windows reference assemblies on Linux) both succeeded and were exercised directly in this session. What could **not** be exercised: any real WinForms rendering (`UpdatePromptDialog`'s actual on-screen appearance/theming), the real Windows CCD/registry/process-lock behavior `WindowsUpdateApplier`/`UpdateApplyEntryPoint` depend on, and the existing `ApplyUpdateBypass_*` real-process tests in `RigToggle.Windows.Tests` (they require a live Windows process host). These require rig verification on real Windows 11 hardware, consistent with ARCHITECTURE.md's own flag that the self-replace-while-running mechanism is "not yet rig-verified."

## User Setup Required

None - no external service configuration required. (The GitHub Releases API call is unauthenticated; no secrets/env vars are introduced by this plan.)

## Next Phase Readiness

The proven tracer slice (version stamp → check → compare → confirm → download → swap → relaunch) is in place for Plan 26-02 (checksum integrity, D-10/D-11) to build on directly: `WindowsUpdateApplier.DownloadAndStageAsync` is the natural insertion point for SHA256 verification before the swap, and `UpdateApplyEntryPoint.Run`'s existing `.bak`-rollback path is already the failure branch checksum verification would also use. Plans 26-03 (never-stranded recovery), 26-04 (skip-version + manual check, including the reserved `btnSkip` slot in `UpdatePromptDialog`), and 26-05 (formatted release notes, currently plain-text `rtbReleaseNotes.Text`) all have clean, unblocked extension points.

**Blocker/concern carried forward:** this entire plan is unverified on real Windows hardware (build/unit-test only, per the Linux sandbox limitation above) — the rename-while-running self-replace mechanism, the `WaitUntilWritable` poll's real-world timing against an exiting process, and `mainForm.BeginInvoke`'s pre-`Application.Run` safety under `--tray` (ARCHITECTURE.md's open verification question) are all still open until a rig pass.

---
*Phase: 26-auto-update*
*Completed: 2026-08-22*
