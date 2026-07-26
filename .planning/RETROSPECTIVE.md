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

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | 1 | 5 | First milestone — established the risk-first phase ordering (validate the one unproven hardware assumption before any GUI work) and the evidence-before-fix debugging discipline used throughout post-ship hardening. |

### Cumulative Quality

| Milestone | Tests | Coverage | Zero-Dep Additions |
|-----------|-------|----------|---------------------|
| v1.0 | 11 xUnit facts (Core logic, recording test doubles) | Core orchestration logic only — Windows adapters unverifiable without rig hardware | 2 (WindowsDisplayAPI, NAudio) |

### Top Lessons (Verified Across Milestones)

1. Evidence-before-fix discipline, once established, directly correlates with fix success rate in this project's one hardware-gated debug session (4/5 blind fixes failed pre-discipline; the fixes that followed evidence-gathering succeeded).
