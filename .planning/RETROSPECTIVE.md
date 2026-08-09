# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — MVP

**Shipped:** 2026-07-26
**Phases:** 5 | **Plans:** 18 | **Sessions:** 1 (continuous, multi-day)

### What Was Built
- A standalone Windows .exe that one-click toggles between normal desktop mode and Moza rig mode — true OS-level (CCD) primary-monitor disable/restore, default audio device switch/restore across all roles, and companion-app launch/focus/minimize, with a full pre-mutation snapshot so toggle-back always restores exactly what was active before.
- A WinForms Settings UI (monitor/audio/app pickers, persisted JSON settings) and a Main window with a single toggle action and honest per-step partial-failure reporting.
- Post-ship (same day as v1.0 close): a real regression — Moza Companion's own close button going permanently inert after RigToggle brought it to the foreground — root-caused across a 10-round evidence-driven debug session and fixed by redesigning the launch/focus mechanism to relaunch-based (`ShellExecute`) activation that never manipulates a window it doesn't own. Settings also generalized to accept any single-instance app (drag-and-drop `.lnk`/`.exe`), and diagnostic logging made opt-in via a Settings checkbox.

### What Worked
- **Evidence-before-fix discipline in the debug session.** Once established (after a few early guesses), every subsequent fix attempt was gated on a real rig-captured `debug.log` excerpt showing the actual before/after Win32 window state, not a guess. This is what actually found the true root cause (raw external Win32 state-mutation calls desyncing something in the target app's own window procedure) rather than chasing surface symptoms.
- **Treating debugging as an iterative loop across many small quick tasks** (each adding targeted diagnostic instrumentation, waiting for one specific piece of rig evidence, then acting only on what came back) rather than one large speculative fix. Every fix that landed cleanly was preceded by a diagnostic-only round that captured the exact evidence needed to justify it.
- **A no-Windows-runtime sandbox constraint was treated as a hard boundary, not worked around.** Every plan/task explicitly said what could be verified here (compilation, grep, structural review) versus what needed the user's rig, and asked for exactly the rig-test evidence needed — never fabricated or assumed a result.

### What Was Inefficient
- Several early debug-session fix attempts (documented in the session's own discipline notes: "4 of 5 blind fixes failed") were applied before the evidence-first pattern was fully established — each cost a full rig round-trip for a fix that didn't address the real mechanism.
- When the launch/focus mechanism was redesigned to eliminate raw window manipulation, the adjacent `MinimizeIfRunning` call site (which does the exact same class of raw `ShowWindow` call, just for a different purpose) was initially left untouched on the assumption it was categorically different ("minimizing needs real window control"). It turned out to have the identical failure mode, discovered only after a real rig regression report. A broader sweep for "every remaining raw Win32 state-mutation call on a window the app doesn't own" at redesign time would have caught this in the same pass.
- Quick-task `PLAN.md`/`SUMMARY.md` files were named with a `{quick_id}-` prefix throughout the entire session (an assumption carried forward from the first quick task without checking against the actual tooling contract). The milestone-close audit revealed the SDK's own scanner expects unprefixed `PLAN.md`/`SUMMARY.md` — all 6 quick tasks had to be renamed retroactively. Checking a canonical convention against the actual tool source (or the `list`/`status` subcommand's own documented file-existence checks) up front would have avoided this.

### Patterns Established
- **Relaunch-based single-instance activation** instead of window-handle focus/enumeration for controlling an external app RigToggle doesn't own: call `Process.Start(UseShellExecute=true)` unconditionally and trust the target's own "already running, activate me" handling, rather than hunting for and manipulating its window via P/Invoke. Generalizes to any well-behaved single-instance Windows app, not just the one it was built for.
- **"Only touch a window if it needs touching"** as the underlying principle after this milestone: any raw external Win32 state-mutation call (`ShowWindow`, `SetForegroundWindow`, etc.) on a window belonging to another process is a candidate for the same class of bug — check every remaining call site when one is found, not just the one that was reported.

### Key Lessons
1. When a rig/hardware round-trip is the only way to get truth (no local runtime to verify against), every fix must be evidence-justified by a rig-captured artifact before being applied — treat "I can reason my way to this" as a hypothesis to test, not a fix to ship.
2. When removing a class of risky call (e.g., raw window manipulation) from one code path during a redesign, grep for every other call site doing the same kind of thing before declaring the redesign complete — a partial fix on a shared failure mode will resurface on the very next adjacent path exercised.
3. Verify file-naming conventions against what the actual tooling (audit scanners, list/status commands) expects, not against a pattern inferred from one earlier example — a systemic naming mismatch across an entire session is cheap to introduce and only surfaces later, in bulk, at points like milestone close.

### Cost Observations
- Model mix: planning delegated to Opus (gsd-planner), execution delegated to Sonnet (gsd-executor) throughout, consistent with a milestone-scale project.
- Sessions: 1 continuous session covering all 5 phases plus post-ship hardening.
- Notable: the debug-and-redesign work after v1.0's phases technically completed was substantial enough to be its own retrospective-worthy body of work (10-round debug session + 4 follow-up quick tasks) — folded into this same v1.0 close rather than treated as a separate milestone, since it was root-causing and fixing a real regression in what had just shipped, not new scope.

---

## Milestone: v1.2 — Visual Polish & Documentation

**Shipped:** 2026-08-04
**Phases:** 3 | **Plans:** 13 | **Sessions:** multiple (spanning discuss/plan/execute across phases 12-14)

### What Was Built
- A theme-aware, modern WinForms UI: MainForm and SettingsForm follow the Windows light/dark theme live (title bar, every control, flat button styling), with Windows-11 Mica/rounded corners and graceful Windows-10 fallback (Phase 12, 6/6 must-haves).
- A shape-distinct tray/exe icon pair (monitor vs. tri-spoke steering wheel silhouettes) via a new hand-rolled `RigToggle.IconGen` dev-time GDI+ generator with its own multi-frame ICO writer — no external design tool or asset pipeline (Phase 13, 5/5 must-haves).
- A GitHub-ready README backed by real, live infrastructure that didn't exist before this milestone: MIT LICENSE, two GitHub Actions workflows (CI build/test + tag-triggered release), the repo flipped private→public, and v1.0/v1.1 backfilled as GitHub Releases (Phase 14, 14/14 must-haves).

### What Worked
- **Rig/live verification kept catching what static checks couldn't.** Three separate real bugs were found only because a human or the orchestrator actually looked at the running/deployed artifact rather than trusting grep-based acceptance criteria: Phase 12's `Application.SetColorMode` dark-mode assumption (title bar/buttons/combos stayed light on real Windows 11), Phase 13's `GraphicsPath.DrawPath` seam artifacts (invisible in a quick glance, caught by code review + a new pixel-level diagnostic), and Phase 14's `build.yml` branch-trigger bug (`main` vs. this repo's actual `master` — would have silently never fired CI). The pattern held across three different phases and three different kinds of verification (rig hardware, pixel diagnostic, live GitHub push) — worth treating as a standing expectation, not a fluke.
- **Independent verification, not just trusting agent self-reports.** The Phase 14 verifier explicitly re-derived live state via its own `gh`/`curl` calls rather than trusting 14-03-SUMMARY.md's claims, and the orchestrator did the same before presenting the human-verify checkpoint. This caught the `main`/`master` bug before the human checkpoint was even presented, saving a round-trip.
- **Treating a permission-classifier denial as a signal to stop, not a wall to route around.** When 14-02's executor subagent hit a blocked `gh` command (including read-only calls), it correctly reported a checkpoint instead of trying workarounds — matching this project's established discipline (from v1.0's retrospective) of not fabricating results when blocked.

### What Was Inefficient
- The planner agent hit a session usage limit mid-run for Phase 14, right before its own final step of updating ROADMAP.md's Plans list — losing that one bookkeeping step (the 3 PLAN.md files themselves were already written and intact). Recovering required manually inspecting disk state, confirming the plans were complete and well-formed, and reconstructing the ROADMAP.md Plans list by hand. A resumable "planner already wrote N plans, just missing the final roadmap sync" fast-path would have avoided the manual reconstruction.
- `gh` CLI syntax assumptions from RESEARCH.md didn't hold on the actual installed CLI version in this environment: `--accept-visibility-change-consequences` doesn't exist, and `--notes-from-tag` is incompatible with an explicit `--repo` flag. Both were caught only at execution time, not research time, because research verified flag syntax against the CLI manual/docs rather than by actually invoking the commands. For CLI-tool-heavy phases, a quick smoke-test of the exact planned invocation (even a `--help` grep) during research would catch version drift before planning locks in the syntax.
- The SUMMARY.md→code-review file-scoping automation undercounted files scoped for review (missed `LICENSE`, `README.md`, `docs/screenshots/.gitkeep`) because two of three plans' SUMMARY.md frontmatter used inline YAML array syntax (`key-files: {created: [...]}`) that the extraction script's line-based parser doesn't handle — it only matches multi-line hyphen-list format. A canonical SUMMARY.md schema check (or fixing the parser to handle both forms) would prevent silently narrow review scope on future phases.

### Patterns Established
- **Two-workflow CI split (build vs. release)** for a project with both a build-status badge and tag-triggered release automation: keep the noisy, frequent push/PR signal decoupled from the rarer, higher-stakes publish job, so a red build badge never implies a bad release went out and vice versa.
- **Badges must be live-endpoint, never static/decorative** — a locked decision from Phase 14's discuss step that shaped the whole phase's scope (it's why the phase grew from "write a README" into "also add real CI, flip the repo public, and backfill releases"). Worth carrying forward: a "make X honest" requirement often implies real backing infrastructure, not just prose.
- **Independent live re-verification before presenting a human checkpoint** — don't just relay what the executor claims; re-derive the same facts via direct tool calls (gh/curl/grep) when the tooling to do so is available, so the human's checkpoint approval is confirming already-strong evidence, not doing first-pass discovery.

### Key Lessons
1. Verification that only re-checks the artifact's own claims (grep/static acceptance criteria) will miss bugs that only manifest when the artifact actually runs somewhere real (a rig, a CI runner, a live GitHub push) — this milestone found three such bugs across three different phases via three different live-verification mechanisms. Treat "did we actually run/deploy this and look" as a required step, not an optional nice-to-have, whenever a phase's plan can be executed for real before the phase closes.
2. When research verifies CLI/tool syntax against documentation rather than by invoking the actual installed tool, flag that as a residual risk explicitly — version drift between "what the docs say" and "what this environment's installed version accepts" is cheap to hit and costly to discover mid-execution.
3. A subagent's own permission/tool restrictions can be stricter than the orchestrator's — when a subagent hits a wall the orchestrator doesn't, the orchestrator running the same verified-safe commands directly (with explicit user confirmation for irreversible actions) is a legitimate escalation path, not a workaround to avoid.

### Cost Observations
- Model mix: planning delegated to Opus (gsd-planner), research/execution/verification delegated to Sonnet, consistent with prior milestones.
- Sessions: multiple, spanning discuss-phase → plan-phase → execute-phase across all 3 phases, plus this milestone-close session.
- Notable: Phase 14 alone required 6 subagent spawns (research, pattern-mapper, planner, plan-checker, 3 executors across 2 waves, code-reviewer, verifier) plus significant orchestrator-direct work (gh CLI calls, a live CI bug fix) — the highest orchestrator-direct-intervention phase of the milestone, driven by the phase's real-world-infrastructure nature (public repo, live CI, real releases) rather than pure code changes.

---

## Milestone: v2.0 — Configurable Monitors, Optional Targets & Cleanup

**Shipped:** 2026-08-09
**Phases:** 4 (15-18) | **Plans:** 19 | **Sessions:** multiple (spanning discuss/plan/execute across phases 15-18, plus rig checkpoints)

### What Was Built
- Genuinely optional companion-app and audio-device targets: unset skips the corresponding toggle step cleanly with no error, while a configured-but-broken target (missing exe, removed device) still fails loudly — and `NormalAudioDeviceId` gained real runtime effect for the first time via `SetDefault` (Phase 15, 5/5 requirements).
- A full mode-tracking redesign: Normal mode now applies its own explicitly configured, symmetric monitor set instead of restoring a pre-toggle snapshot; "which mode am I in" moved off snapshot-file-presence onto a persisted `IModeStore` flag; a disk-persisted crash-in-progress marker plus `StartupRecoveryChecker` dialogs detect and surface a crash mid-toggle (Phase 16, 4/4 requirements).
- A live Manual Monitor Panel (per-monitor status icons, immediate enable/disable, hotplug refresh, Identify overlay) that mutates monitors through the exact same controller calls the Rig/Normal toggle already uses, so the "at least one monitor enabled" safety guard has exactly one implementation across all three mutation paths (Phase 17, 6/6 requirements).
- Full removal of the now-dead snapshot-restore subsystem after preserving its rig-discovered CCD knowledge in a durable knowledge-base entry, a general code-quality pass closing four review findings, and a 57.79% self-contained exe-size reduction (116.9 MB → 49.4 MB) via four MSBuild-only levers with no IL trimming (Phase 18, 4/4 requirements).

### What Worked
- **Risk-ordered phase sequencing paid off again.** Phase 15 (lowest-risk, no-prerequisite validation-gate relaxation) landed first to build confidence before Phase 16's higher-risk mode-tracking redesign; Phase 18's cleanup was correctly held until last, since the snapshot-restore subsystem was only confirmed genuinely dead once Phase 16's rewrite shipped — deleting it earlier would have removed code still in use.
- **Reusing existing shapes instead of inventing new ones.** Phase 16's `IModeStore`/`IToggleInProgressStore` were built to mirror the existing `ISnapshotStore`/`JsonSnapshotStore`/`InMemorySnapshotStore` pattern exactly, and Phase 17's Manual Monitor Panel mutates monitors through the exact same `IMonitorController.DeactivateMonitors`/`ActivateMonitors` calls the toggle already used — both choices meant DISPLAY-12's safety guard needed zero new code to stay consistent across three mutation paths, verified by a static audit rather than by hoping three independent implementations stayed in sync.
- **A rig checkpoint mid-phase (16-05) caught and root-caused a false alarm before it became a real fix.** A manually-hand-typed `toggle-in-progress.json` producing no recovery dialog looked like a defect, but was correctly traced to hand-typed JSON not matching `System.Text.Json`'s default integer enum encoding — not a code bug. Distinguishing "test artifact is wrong" from "code is wrong" avoided a wasted fix cycle.
- **Preserving domain knowledge before deleting the code that encoded it.** Phase 18 explicitly extracted five rig-discovered CCD findings from the doomed `Restore()`/`RestoreViaReconstruction()` code into `.planning/debug/knowledge-base.md` before deletion — the same discipline this project used for other hard-won rig knowledge, applied here to a cleanup phase rather than a debug session.

### What Was Inefficient
- All four phases' code reviews came back `issues_found` rather than clean on the first pass (Phase 15: `IAudioController.Restore` left dead after the `SetDefault` switch, plus an `is null` vs `IsNullOrEmpty` unset-check mismatch between `SettingsForm.cs` and `ToggleService.cs`; similar small correctness/consistency gaps recurred in Phases 16-18) — each required a follow-up fix-and-reverify pass rather than landing clean. None were severe, but four consecutive `issues_found` outcomes suggests the plan-checker/pattern-mapper step could be tightened to catch "old method still referenced after its caller is rewired away" and "two call sites re-implement the same null-check differently" before code review, not after.
- Phase 15's review debt (dead `IAudioController.Restore`, the null-check mismatch) was explicitly deferred to Phase 18 rather than fixed inline — the right call given Phase 18 was already scoped as the cleanup phase, but it meant carrying two known small inconsistencies across three phases before closing them, with the risk they get forgotten if Phase 18 had been cut from scope.
- DISPLAY-13's exact-crash-mid-toggle rig scenario was ultimately waived rather than tested (user judged it niche/low-probability) — a reasonable call, but it was scoped as a rig-verify requirement from the start; recognizing during Phase 16 planning that this specific scenario is hard to trigger deliberately on real hardware (vs. the other, more reproducible rig checks) might have suggested waiving it earlier rather than carrying it as an open UAT item through two sessions.

### Patterns Established
- **Mirror existing store/controller shapes when adding a parallel concept**, rather than inventing a new pattern — `IModeStore` copying `ISnapshotStore`'s shape and the Manual Monitor Panel reusing the toggle's own controller calls both eliminated a class of "guard enforced in N places, they drift" bug by construction.
- **Extract domain knowledge before deleting the code that encoded it.** When a cleanup phase removes code that happened to encode hard-won environment-specific facts (here: CCD API quirks discovered only via rig hardware), pull those facts into a durable reference doc as an explicit task before the deletion task, not as an afterthought.
- **A static "single implementation, N call sites" audit is a valid substitute for N independent manual checks** when a shared-guard requirement (DISPLAY-12) spans multiple entry points — Phase 17 verified this by code audit, not by manually re-testing the guard three times.

### Key Lessons
1. Deferring known review debt to a later, already-planned cleanup phase is legitimate risk management, not procrastination — but treat every deferred item as a tracked line item (this project used `15-REVIEW.md`'s findings as literal Phase 18 task inputs, IN-01 through IN-04) so "we'll clean it up later" has a concrete later, not an implicit one.
2. When a phase scopes a rig-verify requirement around a scenario that's inherently hard to trigger deliberately on real hardware (a crash at one specific instant mid-operation), flag that difficulty at planning time — the option to formally waive it with documented rationale is available and legitimate, but surfacing it early avoids carrying an open UAT item across multiple sessions before reaching the same conclusion.
3. Reusing an existing pattern's exact shape (a store interface, a controller call) for a new-but-parallel concept isn't just less code — it structurally prevents the "guard enforced in three places, one drifts" bug class that a shared-safety-invariant requirement (like DISPLAY-12) is specifically worried about.

### Cost Observations
- Model mix: planning delegated to Opus (gsd-planner), research/execution/verification delegated to Sonnet, consistent with prior milestones.
- Sessions: multiple, spanning discuss-phase → plan-phase → execute-phase across all 4 phases, each including at least one real-rig checkpoint, plus this milestone-close session.
- Notable: this was the fastest milestone by calendar time (5 days across 165 commits, v1.1's 6-day/186-commit pace) despite being architecturally the riskiest (Phase 16's mode-tracking redesign touched the core toggle path) — attributable to the risk-ordered phase sequencing and the "mirror existing shapes" pattern reducing net-new design surface.

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | 1 | 5 | First milestone — established the risk-first phase ordering (validate the one unproven hardware assumption before any GUI work) and the evidence-before-fix debugging discipline used throughout post-ship hardening. |
| v1.1 | — | 5 (6-9, 11) | Automation milestone (tray/hotkey triggers, multi-monitor generalization) — retrospective section not captured at close; see MILESTONES.md for accomplishments. |
| v1.2 | multiple | 3 (12-14) | Visual-polish + docs milestone — extended rig-verification discipline from v1.0's debug session into a general pattern (live verification over static checks), and added a new class of phase: infrastructure-as-documentation (Phase 14 needed real CI/public-repo/releases to make a README honest, not just prose). |
| v2.0 | multiple | 4 (15-18) | Redesign + cleanup milestone — replaced a core architectural mechanism (snapshot-restore) with an explicit-config pattern mid-project, ordered by risk (lowest-risk optional-target work first, highest-risk mode-tracking redesign second, cleanup last once the old mechanism was confirmed dead) rather than roadmap order alone. |

### Cumulative Quality

| Milestone | Tests | Coverage | Zero-Dep Additions |
|-----------|-------|----------|---------------------|
| v1.0 | 11 xUnit facts (Core logic, recording test doubles) | Core orchestration logic only — Windows adapters unverifiable without rig hardware | 2 (WindowsDisplayAPI, NAudio) |
| v1.2 | Existing suite extended (RigToggle.Tests, RigToggle.Windows.Tests); Phase 12 6/6, Phase 13 5/5, Phase 14 14/14 must-haves verified | Theme/icon logic covered by existing test projects; Phase 14's CI/release infra has no unit tests (verified live instead — GitHub Actions run + gh/curl checks) | 0 new NuGet packages (theme/icon work is BCL/WinForms-native + dev-time GDI+); Phase 14 added 3 GitHub Actions (checkout, setup-dotnet, action-gh-release) as its only new "dependencies" |
| v2.0 | Existing suite net-shrunk to 81/81 core tests after removing snapshot-restore's tests alongside its code (still 100% green); Phase 17 84/84 mid-milestone before Phase 18's dead-test removal | Optional-target skip/fail paths, mode-store persistence, and the shared monitor-safety-guard all unit-covered; all rig-only scenarios (crash-mid-toggle timing, hotplug, cold boot) verified live, not simulated | 0 new NuGet packages — the entire milestone (mode store, panel, exe-size levers) used only existing dependencies plus MSBuild configuration |

### Top Lessons (Verified Across Milestones)

1. Evidence-before-fix discipline, once established, directly correlates with fix success rate in this project's one hardware-gated debug session (4/5 blind fixes failed pre-discipline; the fixes that followed evidence-gathering succeeded).
2. Live/deployed verification (rig hardware, real CI runs, live GitHub state) catches bugs that static/grep-based acceptance criteria structurally cannot — confirmed again in v1.2 across three independent phases (theming, icon rendering, CI trigger scoping), extending the v1.0 lesson from "debugging a regression" to "verifying a phase before calling it done."
3. Risk-ordered phase sequencing (lowest-risk validation first, highest-risk redesign second, cleanup last) keeps a milestone's riskiest architectural change from also being its first — confirmed in v2.0, where Phase 16's core mode-tracking redesign benefited from confidence built by Phase 15's lower-risk optional-target work, and Phase 18's cleanup was correctly held until the code it was deleting was verifiably dead.
4. Mirroring an existing store/controller shape for a new-but-parallel concept, rather than inventing a new pattern, structurally prevents "guard enforced in N places, one drifts" bugs — v2.0's `IModeStore` (mirroring `ISnapshotStore`) and Manual Monitor Panel (reusing the toggle's own controller calls) both avoided a class of bug a shared-safety-invariant requirement is specifically worried about, verified by static audit rather than N independent manual checks.
