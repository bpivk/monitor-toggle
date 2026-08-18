# Project Research Summary

**Project:** Rig Toggle — v2.2 "Auto-Update, Single-Instance Guard & Smaller Footprint"
**Domain:** Windows desktop utility maintenance features (self-update, single-instance guarding, exe size reduction) — additive to an existing 4-project .NET 10 WinForms solution
**Researched:** 2026-08-18
**Confidence:** MEDIUM-HIGH

## Executive Summary

v2.2 is a scoped maintenance milestone adding three operational capabilities to the already-shipped Rig Toggle app: GitHub-Releases-based auto-update (check → confirm → download → self-replace → relaunch), a mutex-based single-instance guard with focus-existing-window behavior, and further self-contained-exe size reduction via safe MSBuild feature switches (no IL trimming). All research converges on a single strong recommendation: zero new NuGet packages, hand-rolled BCL-only implementations for all three features, following the same pattern this codebase already established with its hand-rolled IPolicyConfig COM interop and RegisterHotKey P/Invoke. Ready-made frameworks (Velopack, NetSparkle, Onova, WindowsFormsApplicationBase.IsSingleInstance) were all evaluated and rejected because they assume an installer/multi-file distribution model that conflicts with this project's "one standalone .exe attached to a GitHub Release" constraint.

The technical mechanism for self-update is well-understood: Windows forbids overwriting a running exe's file contents but permits renaming it, which is the basis of the recommended rename-in-place (or copy-self-to-temp-helper) swap pattern — no separate installer or helper download needed. The single most important sequencing fact across all four research files is that no version identifier exists anywhere in this codebase today (no `<Version>` in any `.csproj`, and `release.yml` never stamps one) — this is a hard prerequisite that must be built before any "compare running version to latest release" logic can function, and is easy to under-scope as "just call the GitHub API." The second major cross-cutting risk is the interaction between the two new features themselves: an auto-update relaunch can race the freshly-added single-instance mutex guard, causing the updated process to see the old process's still-held mutex and silently fail to relaunch — architecture research recommends building the single-instance guard first (with an explicit startup-gate bypass for the internal update-relaunch helper) specifically to structurally prevent this class of bug rather than relying on both features' implementers to coordinate it later.

Key risks to mitigate: (1) version-stamping must land before update-check logic — sequence it as an explicit prerequisite; (2) the update-apply sequence has multiple partial-failure points (interrupted download, disk full, AV lock) that can leave the app permanently unable to start — mitigate with a "keep .old backup until new exe confirmed running" discipline, mirroring the existing ToggleInProgressMarker crash-recovery precedent; (3) an unsigned freshly-downloaded exe could trigger SmartScreen on an unattended relaunch — mitigated by using an HttpClient-based download (does not apply Mark-of-the-Web the way browser downloads do) but this must be rig-verified, not assumed; (4) the update-apply path must preserve the exact original exe file path, since WindowsAutostartConfigurator bakes Environment.ProcessPath into the registry Run key and any path drift silently breaks autostart, discoverable only a full reboot later; (5) size-reduction work must not reach for IL trimming, partial trimming, Native AOT, or PublishReadyToRun — all either reproduce the COM/P-Invoke breakage this project already rejected, or actively increase exe size.

## Key Findings

### Recommended Stack

No new NuGet packages for any of the three v2.2 capabilities. Auto-update uses `System.Net.Http.HttpClient` (GitHub Releases API, with required `User-Agent` header) + `System.Text.Json` (already a dependency) to fetch and parse `GET /repos/{owner}/{repo}/releases/latest`, plus hand-written rename/move file operations for the self-replace. Single-instance guarding uses `System.Threading.Mutex` (named `Global\RigToggle-{fixed-GUID}`, `Global\` prefix required for cross-session correctness) plus `RegisterWindowMessage`/`PostMessage(HWND_BROADCAST,...)` (architecture research) or a named pipe (some sources) for the "wake up and show yourself" signal — both are zero-payload, matching the project's prior explicit decision to rule out full CLI-trigger/IPC (TRIG-02/TRIG-03) at v1.1 close. Size reduction uses only MSBuild/`.csproj` feature switches (`DebugType=none`, `EventSourceSupport=false`, `UseSystemResourceKeys=true` with caution, `HttpActivityPropagationSupport=false`) — explicitly never `PublishTrimmed`, `PublishAot`, or `PublishReadyToRun`.

**Core technologies:**
- `System.Net.Http.HttpClient` + `System.Text.Json` — GitHub Releases API polling and DTO parsing — BCL-only, matches existing settings-persistence pattern
- `System.Threading.Mutex` (named, `Global\` prefix) — cross-process single-instance detection — standard, unchanged .NET mechanism since .NET Framework
- Rename-in-place / process-replication self-update — the only mechanism that lets a self-contained single-file exe replace itself without an installer or admin rights
- MSBuild feature switches (no trimming) — the only size levers compatible with this app's COM/P-Invoke-heavy surface (`IPolicyConfig`, `WindowsDisplayAPI`/CCD)

### Expected Features

**Must have (table stakes, all P1):**
- Version-stamping infrastructure (`release.yml` → build-time `<Version>`) — hard prerequisite, currently missing entirely
- On-launch GitHub Releases version check with confirm-before-install prompt (not silent, not manual-only — locked in by PROJECT.md)
- Download + rename-in-place apply + relaunch, completing the actual update loop
- Named-Mutex single-instance guard at process startup
- Focus-existing-instance-on-duplicate-launch (not silent-exit)
- Additional safe MSBuild exe-size levers (no fixed target — any measurable reduction satisfies this)

**Should have / add after validation:**
- Manual "Check for Updates" menu item (cheap add-on once check logic exists)

**Defer (explicitly out of scope):**
- Any installer/MSIX/Velopack-style packaging framework
- Delta/differential updates
- Named-pipe/WM_COPYDATA IPC richer than a bare focus signal (permanently ruled out at v1.1 close)

### Architecture Approach

Additive to the existing 4-project layering (`RigToggle.Core` / `RigToggle.Windows` / `RigToggle.App`) — no new project needed. Platform-neutral pieces (`GitHubReleaseFeed`, `UpdateVersionComparer`, `UpdateOrchestrator`) live in Core, matching the existing `ToggleService`/`ToggleOrchestrator` split of "Core sequences, Windows/App execute OS/UI work." OS-touching pieces (`WindowsUpdateApplier`, `SingleInstanceGuard`, `UpdateApplyEntryPoint`) live in Windows. `Program.cs` gains two new ordered startup gates ahead of existing bootstrap logic.

**Major components:**
1. `StartupArgs.TryGetApplyUpdateArgs` + `UpdateApplyEntryPoint` — the very first branch in `Main()`, bypassing the single-instance guard entirely, so the internal self-relaunch helper process never touches the mutex
2. `SingleInstanceGuard` — named Mutex acquire/detect + signal mechanism (pipe or `RegisterWindowMessage`), started as early as possible after the mutex is won, before any other startup work
3. `UpdateOrchestrator` / `GitHubReleaseFeed` / `WindowsUpdateApplier` — check → compare → confirm-prompt → download → rename-swap → relaunch, run as fire-and-forget after the main form and tray are already up (never blocks startup)
4. `RigToggle.App.csproj` / `win-x64.pubxml` / `release.yml` — build-time `<Version>` stamping and the new size-reduction MSBuild feature switches

**Recommended build order:** (1) exe-size reduction first (fully isolated, zero interaction with the other two), (2) single-instance guard second (establishes the startup-gate ordering auto-update depends on), (3) auto-update last (reuses the already-verified bypass pattern rather than inventing it under pressure).

### Critical Pitfalls

1. **Cannot overwrite the running exe in place** — Windows permits rename but not overwrite of a running process's image; must download to temp, rename current exe to `.old`, move new exe into place, relaunch, clean up `.old` on next launch.
2. **No version identifier exists anywhere in this codebase today** — no `<Version>`/`<AssemblyVersion>` in any `.csproj`; must be added and driven from the git tag in `release.yml` before any comparison logic is written, or the check is meaningless (always/never reports an update).
3. **Naive lexical/mismatched-component version comparison** — tags are two-part (`v2.1`, not `v2.1.0`); must parse into numeric components (`Major`/`Minor` only) and compare numerically, with a unit test covering a synthetic double-digit case (`v2.9` vs `v2.10`).
4. **Update relaunch races the single-instance mutex** — the old process must spawn the new process, then release the mutex/exit immediately after (not before); the new process's own instance check must tolerate a still-exiting predecessor.
5. **Partial update failure can leave the app permanently unable to start** — never delete the `.old` backup until the new exe is confirmed to have started successfully; roll back on any failure, mirroring the existing `ToggleInProgressMarker` crash-recovery pattern.
6. **Update-apply must preserve the exact original exe path** — `WindowsAutostartConfigurator` bakes `Environment.ProcessPath` into the registry Run key at enable-time; any path drift silently breaks autostart, only discoverable after a full reboot.
7. **Reaching for Native AOT / partial trimming / `PublishReadyToRun` as size levers** — all either reproduce the exact COM/P-Invoke breakage `PublishTrimmed=false` was chosen to avoid, or actively increase exe size (ReadyToRun is a startup-speed lever, not a size lever).

## Implications for Roadmap

Based on research, suggested phase structure (matches the "Recommended Build Order" from ARCHITECTURE.md):

### Phase 1: Exe Size Reduction (MSBuild-only)
**Rationale:** Fully isolated from the other two features — no `Program.cs` changes, no runtime code. Doing it first means every later phase's manual builds already run against the smaller artifact, and any regression is trivially bisectable.
**Delivers:** `<Version>` scaffolding (also needed by Phase 3, so worth landing here or as its own prerequisite step), `DebugType=none`, `EventSourceSupport=false`, `HttpActivityPropagationSupport=false`, and a carefully-verified `UseSystemResourceKeys=true` (or explicit rejection if error-text readability regresses).
**Addresses:** "Additional safe MSBuild exe-size levers" (FEATURES.md P2)
**Avoids:** Pitfall 10 (Native AOT/partial trimming reach), Pitfall 11 (UseSystemResourceKeys degrading error text)

### Phase 2: Single-Instance Guard
**Rationale:** Restructures `Program.cs`'s startup sequence and establishes the earliest new gate and the `StartupArgs` extension pattern that auto-update's relaunch step will depend on. Must be rig-verified in isolation before auto-update has to reason about it.
**Delivers:** Named `Mutex` acquire/detect, signal mechanism (pipe or `RegisterWindowMessage`) started immediately after acquiring the mutex, `MainForm.RestoreAndFocus()` extraction shared with the existing tray-click handler, retry-with-backoff on the signaling side.
**Uses:** `System.Threading.Mutex`, `RegisterWindowMessage`/`PostMessage` or named pipe (STACK.md)
**Implements:** `SingleInstanceGuard` (Windows layer)
**Avoids:** Pitfall 7 (conflating with `ToggleOrchestrator._busy`), Pitfall 8 (signal-delivery race — verify via scripted rapid-relaunch loop, not manual double-click)

### Phase 3: Auto-Update
**Rationale:** Depends on both prior phases — needs version-stamping (ideally landed in Phase 1) and the single-instance guard's bypass pattern (Phase 2) already rig-verified, so this phase only has to *use* the bypass correctly rather than invent it under pressure while also debugging a brand-new self-replace mechanism.
**Delivers:** `GitHubReleaseFeed`, `UpdateVersionComparer`, `UpdateOrchestrator` (Core); `WindowsUpdateApplier`, `UpdateApplyEntryPoint` (Windows); `UpdatePromptDialog` (App, theme-aware, not `MessageBox`); `release.yml` `-p:Version=` wiring; full check → confirm → download → rename-swap → relaunch loop.
**Addresses:** All P1 auto-update features from FEATURES.md
**Avoids:** Pitfalls 1-6, 9 (self-replace mechanics, version comparison, relaunch race, partial-failure recovery, SmartScreen, autostart path preservation)

### Phase Ordering Rationale

- Size reduction has no dependencies and no downstream dependents — safe to sequence anywhere, placed first as a low-risk warm-up and to avoid re-measuring size deltas after later phases change the binary.
- Single-instance guard must precede auto-update because auto-update's relaunch is itself a full process launch that must correctly bypass the guard — building the bypass structurally (as the guard's very first check) rather than retrofitting it avoids Pitfall 4 entirely.
- Version-stamping is a cross-cutting prerequisite for auto-update specifically — it can be done as part of Phase 1 (build config) or as an explicit first sub-step of Phase 3; either way it must land before any version-comparison code is written, per Pitfall 2.
- All three phases should each include an explicit real-Windows rig verification pass (cold reboot for autostart, scripted rapid-relaunch for single-instance, interrupted-update simulation for the apply step) — none of this is safely verifiable through unit tests or a single manual happy-path click, consistent with this project's established rig-testing discipline.

### Research Flags

Phases likely needing deeper research during planning:
- **Auto-update phase:** Needs a `--research-phase` pass specifically for the exact rename/relaunch code shape (Pattern 2 in ARCHITECTURE.md is illustrative, not literal) and for confirming Mark-of-the-Web/SmartScreen behavior via direct rig testing rather than the MEDIUM-confidence web sources cited.
- **Single-instance guard phase:** Needs research/verification for the exact signal mechanism choice (named pipe vs. `RegisterWindowMessage`/`PostMessage` — STACK.md and ARCHITECTURE.md suggest slightly different mechanisms) and for the `mainForm.BeginInvoke`/`Handle` timing question flagged as an open verification item under the `--tray` hidden-launch path.

Phases with standard patterns (skip research-phase):
- **Exe size reduction phase:** Well-documented, direct MSBuild property changes with clear before/after verification steps already specified in research — no deeper research needed.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM | No curated research provider (Context7/Exa/Tavily) was configured this session; raw websearch/webfetch findings upgraded to MEDIUM/HIGH only where cross-checked against official docs or this repo's own source files (read directly) |
| Features | MEDIUM-HIGH | Two of three areas (self-update, exe size) verified directly against this repo's actual `.csproj`/`.pubxml`/CI files; ecosystem claims (Velopack behavior, GitHub API limits) corroborated across multiple independent sources but not all cross-checked against official Microsoft Learn pages |
| Architecture | HIGH for integration points and component placement (based on direct reading of current `Program.cs`, `MainForm.cs`, `ToggleOrchestrator.cs`, etc.); MEDIUM-HIGH for the self-update mechanism and exe-size feature switches (not yet rig-tested) |
| Pitfalls | MEDIUM-HIGH | Every pitfall anchored either in this codebase's own current source (read directly) or external websearch findings flagged MEDIUM where no official Microsoft Learn source was found — these are entirely new capabilities with no prior art in this project's own history |

**Overall confidence:** MEDIUM-HIGH

### Gaps to Address

- **Rename-while-running mechanism has not been rig-verified** — the entire self-update mechanism rests on the well-corroborated-but-not-officially-documented Windows behavior that a running exe can be renamed but not overwritten; must be confirmed on real hardware against the actual self-contained single-file publish mode (`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`), not just a scratch-folder simulation.
- **Mark-of-the-Web / SmartScreen behavior for `HttpClient` downloads** — MEDIUM-LOW confidence claim that `HttpClient` downloads don't apply Zone.Identifier the way browser downloads do; needs direct rig verification during the auto-update phase, with a documented fallback UX if an interstitial does appear on an unattended relaunch.
- **`mainForm.BeginInvoke`/`Handle` existence under `--tray` hidden startup** — architecture research flags this as unresolved; needs verification on real hardware before trusting the update-check invocation timing, with a `Timer`-based fallback ready if `BeginInvoke` doesn't work as expected in that path.
- **Signal mechanism choice for single-instance activation** (named pipe vs. `RegisterWindowMessage`/`PostMessage`) — STACK.md and ARCHITECTURE.md lean slightly differently; should be resolved as an explicit decision at the start of that phase's plan, not left ambiguous.
- **GitHub API rate-limit specifics (60/hour unauthenticated)** — sourced via websearch, not independently fetched/read in full from `docs.github.com`; low risk for actual usage but worth a final confirmation before relying on it in code comments/design docs.

## Sources

### Primary (HIGH confidence)
- Direct reads of this repository: `Program.cs`, `MainForm.cs`, `ToggleOrchestrator.cs`, `StartupArgs.cs`, `WindowsAppController.cs`, `WindowsAutostartConfigurator.cs`, `StartupRecoveryChecker.cs`, `RigToggle.App.csproj`, `Properties/PublishProfiles/win-x64.pubxml`, `.github/workflows/{build,release}.yml`, `git tag -l`, `.planning/PROJECT.md`
- https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run — official ReadyToRun size/startup tradeoff documentation
- https://docs.github.com/en/rest/releases/releases — `/releases/latest` semantics and asset response fields
- https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api — 60 req/hour unauthenticated limit

### Secondary (MEDIUM confidence)
- https://github.com/velopack/velopack, https://docs.velopack.io — installed-app assumption, `NotInstalledException` for non-installed runs
- https://github.com/velopack/velopack/issues/314 — confirms single-portable-exe friction with Velopack
- https://github.com/NetSparkleUpdater/NetSparkle, https://github.com/Tyrrrz/Onova — evaluated and rejected alternatives
- https://www.nikouusitalo.com/blog/shrinking-a-self-contained-net-6-wordle-clone-executable/ — real-world size-reduction case study
- Multiple corroborating web sources on: Windows exe-rename-while-running technique, `Mutex`-based single-instance detection, `SetForegroundWindow` restrictions, Mark-of-the-Web/SmartScreen behavior, `UseSystemResourceKeys`/`DebugType` semantics, WinForms + Native AOT COM-interop incompatibility

### Tertiary (LOW confidence)
- General web search corroboration on Windows platform behavior (running exe rename/overwrite semantics) not backed by a single canonical Microsoft Learn page — flagged throughout for rig verification rather than treated as settled

---
*Research completed: 2026-08-18*
*Ready for roadmap: yes*
