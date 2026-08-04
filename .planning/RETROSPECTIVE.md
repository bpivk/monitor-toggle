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

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | 1 | 5 | First milestone — established the risk-first phase ordering (validate the one unproven hardware assumption before any GUI work) and the evidence-before-fix debugging discipline used throughout post-ship hardening. |
| v1.1 | — | 5 (6-9, 11) | Automation milestone (tray/hotkey triggers, multi-monitor generalization) — retrospective section not captured at close; see MILESTONES.md for accomplishments. |
| v1.2 | multiple | 3 (12-14) | Visual-polish + docs milestone — extended rig-verification discipline from v1.0's debug session into a general pattern (live verification over static checks), and added a new class of phase: infrastructure-as-documentation (Phase 14 needed real CI/public-repo/releases to make a README honest, not just prose). |

### Cumulative Quality

| Milestone | Tests | Coverage | Zero-Dep Additions |
|-----------|-------|----------|---------------------|
| v1.0 | 11 xUnit facts (Core logic, recording test doubles) | Core orchestration logic only — Windows adapters unverifiable without rig hardware | 2 (WindowsDisplayAPI, NAudio) |
| v1.2 | Existing suite extended (RigToggle.Tests, RigToggle.Windows.Tests); Phase 12 6/6, Phase 13 5/5, Phase 14 14/14 must-haves verified | Theme/icon logic covered by existing test projects; Phase 14's CI/release infra has no unit tests (verified live instead — GitHub Actions run + gh/curl checks) | 0 new NuGet packages (theme/icon work is BCL/WinForms-native + dev-time GDI+); Phase 14 added 3 GitHub Actions (checkout, setup-dotnet, action-gh-release) as its only new "dependencies" |

### Top Lessons (Verified Across Milestones)

1. Evidence-before-fix discipline, once established, directly correlates with fix success rate in this project's one hardware-gated debug session (4/5 blind fixes failed pre-discipline; the fixes that followed evidence-gathering succeeded).
2. Live/deployed verification (rig hardware, real CI runs, live GitHub state) catches bugs that static/grep-based acceptance criteria structurally cannot — confirmed again in v1.2 across three independent phases (theming, icon rendering, CI trigger scoping), extending the v1.0 lesson from "debugging a regression" to "verifying a phase before calling it done."
