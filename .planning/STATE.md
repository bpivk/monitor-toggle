---
gsd_state_version: 1.0
milestone: v2.1
milestone_name: Modern UI Redesign & Theme Backlog
status: executing
stopped_at: Phase 19 UI-SPEC approved
last_updated: "2026-08-09T21:08:07.284Z"
last_activity: 2026-08-09
progress:
  total_phases: 5
  completed_phases: 0
  total_plans: 5
  completed_plans: 3
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-08-09)

**Core value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably applies Normal mode's own explicitly configured monitor/audio state on toggle-back (revised at v2.0 — see DISPLAY-10/AUDIO-04).
**Current focus:** Phase 19 — Monitor-Tile Dashboard & MonitorPanelForm Retirement

## Current Position

Phase: 19 (Monitor-Tile Dashboard & MonitorPanelForm Retirement) — EXECUTING
Plan: 4 of 5
Status: Ready to execute
Last activity: 2026-08-09

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 54
- Average duration: - min
- Total execution time: 0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 02 | 5 | - | - |
| 03 | 4 | - | - |
| 04 | 4 | - | - |
| 5 | 3 | - | - |
| 6 | 6 | - | - |
| 7 | 1 | - | - |
| 8 | 4 | - | - |
| 09 | 4 | - | - |
| 11 | 4 | - | - |
| 12 | 6 | - | - |
| 13 | 4 | - | - |
| 14 | 3 | - | - |
| 15 | 4 | - | - |
| 16 | 5 | - | - |
| 17 | 4 | - | - |
| 18 | 6 | - | - |

**Recent Trend:**

- Last 5 plans: -
- Trend: -

*Updated after each plan completion*

## Accumulated Context

### Roadmap Evolution

- Phase 11 edited: edited fields: title, goal, depends_on (corrected from Phase 10 to Phase 8)
- v1.2 roadmap created 2026-08-02: 3 phases derived from THEME/ICON/DOCS requirement categories (coarse granularity). Phase 12 (Theme Infrastructure) sequenced first per research risk-priority (highest-novelty work; live-update gap and `--tray` handle-timing risk). Phase 13 (Tray & App Icon Redesign) is architecturally independent of Phase 12 but sequenced second by convention, not dependency. Phase 14 (README) sequenced last, depends on both since it documents/screenshots the finished visual result. Rig-only-catchable risks (live theme flip while running, both taskbar themes, extended-session GDI handle stability, `--tray` hidden-start timing, Windows 10 fallback) were folded into Phase 12/13 success criteria and plan-level human verification rather than a separate verification phase, consistent with this project's established UAT-per-phase pattern (Phase 8, 9, 11).
- v2.0 roadmap created 2026-08-04: 4 phases (15-18) derived from the 19 v2.0 requirements (coarse granularity), continuing numbering from v1.2's Phase 14. Structure follows research/SUMMARY.md's proposed sequencing closely: Phase 15 (optional App/Audio targets) first as lowest-risk, no-prerequisite validation-gate relaxation; Phase 16 (Normal-mode explicit monitor config + mode-store redesign) second as the core, highest-risk piece — forces `IsInRigMode()` off snapshot-file-presence onto an explicit persisted flag, landed alongside the monitor-set rewrite per research's Pitfall 4/5 warnings; Phase 17 (manual monitor panel + shared safety guard) third, depends on Phase 16's controller-call unification so the panel doesn't introduce a second implementation of "toggle a monitor." DISPLAY-12 (safety guard enforced identically across Rig toggle, Normal toggle, and the manual panel) was assigned to Phase 17 rather than Phase 16 since full three-way enforcement can only be verified once the panel (the third path) exists. Phase 18 (cleanup + exe-size reduction) last, since the snapshot/restore subsystem is only confirmed genuinely dead once Phase 16 ships. DISPLAY-13 (crash-recovery marker) and PANEL-04/05 (confirmation gate, Identify action) — both resolved into requirements after research/SUMMARY.md was written — were folded into Phase 16 and Phase 17 respectively, consistent with the research's existing rationale for those phases.
- v2.1 roadmap created 2026-08-09: 5 phases (19-23) derived from the 14 v2.1 requirements (TILE-01..07, MAIN-01..02, SETTINGS-01..02, THEME-07..09; coarse granularity), continuing numbering from v2.0's Phase 18. Structure follows research/SUMMARY.md's proposed sequencing closely: Phase 19 (monitor-tile dashboard + MonitorPanelForm retirement) first — highest complexity/regression-risk, absorbs all 5 of the retiring panel's behaviors (TILE-01..07) plus the toggle/Settings repositioning (MAIN-01/02), sequenced so the tile replacement is proven working before the standalone panel is deleted. Phase 20 (custom toggle-switch control, THEME-08) second, right after the dashboard stabilizes since it redraws the same button Phase 19 repositions — avoids compounding two risky UI changes in one diff. Phase 21 (accent-color reading, THEME-07) third, soft-ordered before Phase 22 since it's architecturally independent but pairs naturally with an accent-tinted switch. Phase 22 (manual light/dark override, THEME-09) fourth, last among theme work since it decorates whatever `IThemeProvider` interface Phase 21 leaves behind. Phase 23 (SettingsForm layout pass) last — fully independent of every other phase, scheduled as the natural mop-up/filler with no dependency edges. All 14 requirements mapped 1:1, no orphans.

### Decisions

Full decision log lives in PROJECT.md's Key Decisions table. v2.0's decisions (optional-target skip/fail semantics, `IModeStore`-backed mode tracking, explicit symmetric Normal-mode config replacing snapshot-restore, shared Manual Monitor Panel safety guard, MSBuild-only exe-size levers) are all implemented, rig-verified, and validated — see PROJECT.md for outcomes.

### Pending Todos

None.

### Known Limitations

- The relaunch-based launch redesign's `MinimizeIfRunning`/`IsRunning` (toggle-back) still derive the process name from the configured launch-target path via `Path.GetFileNameWithoutExtension`. If the user configures a `.lnk` (rather than the target `.exe` itself) as the launch target, that derived name will typically not match the real running process name, so toggle-back's minimize call may silently no-op. Documented, not patched (out of scope, carried over from v1.0).

### Blockers/Concerns

None open. Research flags Phase 21 (accent color) and Phase 22 (manual override) as needing deeper research during planning — see `.planning/research/SUMMARY.md` "Research Flags" — no official Microsoft documentation confirms which registry key/API is "the" accent color, and the manual-override/live-theme-follow composition needs careful sequencing relative to the `IsDark` property consolidation.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260728-qj1 | Fix WindowsMonitorController.GetAllMonitors() duplicate-row/dual-primary bug found during Phase 6's 06-06 rig checkpoint | 2026-07-28 | fe0aee7 | [260728-qj1-fix-windowsmonitorcontroller-getallmonit](./quick/260728-qj1-fix-windowsmonitorcontroller-getallmonit/) |
| 260728-rmp | Relabel Settings monitor grid columns (Disable/Enable -> Off (Rig)/On (Rig)) with tooltips and a permanent explanation label, clarifying the grid only configures the transition into Rig Mode | 2026-07-28 | d76b5db | [260728-rmp-improve-settings-monitor-grid-clarity-re](./quick/260728-rmp-improve-settings-monitor-grid-clarity-re/) |
| 260804-9rt | Fix UF-14-01 and UF-14-02 from Phase 14 security audit: add least-privilege `permissions: contents: read` to build.yml, add restore/build/test gate before publish in release.yml | 2026-08-04 | dd974e3 | [260804-9rt-fix-uf-14-01-and-uf-14-02-from-phase-14-](./quick/260804-9rt-fix-uf-14-01-and-uf-14-02-from-phase-14-/) |

## Deferred Items

Items acknowledged and carried forward from previous milestone close:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| dropped | LOG-01 (toggle history/log) | Dropped, not carried forward | Deferred at v1.0, v1.1, v1.2, and v2.0 scoping; explicitly dropped by user at v2.1 scoping 2026-08-09 — will not resurface as a backlog candidate |
| v2.1 | THEME-07 (accent-color-aware highlight) | Scoped into v2.1 Phase 21 | Deferred at v1.2 scoping 2026-08-02; scoped into v2.1 roadmap 2026-08-09 |
| v2.1 | THEME-08 (custom-drawn toggle-switch control) | Scoped into v2.1 Phase 20 | Deferred at v1.2 scoping 2026-08-02; scoped into v2.1 roadmap 2026-08-09 |
| v2.1 | THEME-09 (manual theme override) | Scoped into v2.1 Phase 22 | Deferred at v1.2 scoping 2026-08-02; scoped into v2.1 roadmap 2026-08-09 |

Permanently out of scope (not v2 candidates):

| Category | Item | Status | Decided At |
|----------|------|--------|-------------|
| out-of-scope | TRIG-02 (CLI toggle argument) | Removed — not needed | v1.1 milestone close 2026-08-01 — Phase 10 never planned/executed; tray+hotkey triggers already cover the daily-use need, user confirmed CLI trigger unnecessary |
| out-of-scope | TRIG-03 (CLI status query argument) | Removed — not needed | v1.1 milestone close 2026-08-01 — same as TRIG-02 |

Items acknowledged and deferred at v1.0 milestone close on 2026-07-26, reconfirmed at v1.1 close on 2026-08-01, reconfirmed again at v1.2 close on 2026-08-04, and reconfirmed once more at v2.0 close on 2026-08-09 (pre-close artifact audit — all are scanner false positives, not real gaps, confirmed by direct inspection each time):

| Category | Item | Status |
|----------|------|--------|
| debug | knowledge-base | `.planning/debug/knowledge-base.md` is a persistent reference doc (not a debug session) sitting directly in `.planning/debug/`; the audit scanner treats every `.md` file in that directory as a session and flags it "unknown" for lacking a `status:` field. Not an open investigation — acknowledged and left as-is. |
| uat_gap | Phase 05 (05-HUMAN-UAT.md) | File's own frontmatter already reads `status: resolved` with 0 pending scenarios; the scanner surfaces any existing UAT file regardless of resolution. Already resolved — acknowledged, no action needed. |
| quick_task | 260728-qj1-fix-windowsmonitorcontroller-getallmonit | Scanner flags "missing" status, but `260728-qj1-SUMMARY.md` exists and STATE.md's Quick Tasks Completed table confirms it shipped in commit `fe0aee7`. False positive — the scanner's completion check doesn't recognize this quick-task's status format. |
| quick_task | 260728-rmp-improve-settings-monitor-grid-clarity-re | Same false-positive pattern as above; shipped in commit `d76b5db`, confirmed via `260728-rmp-SUMMARY.md` and the Quick Tasks Completed table. |
| uat_gap | Phase 13 (13-HUMAN-UAT.md) | Same pattern as Phase 05: file's own body already showed 2/2 passed, 0 pending, "Gaps: None" before v1.2 close — frontmatter `status: partial` was stale and has been flipped to `resolved` (commit `38d756a`). Not a real gap. |

Resolved at v1.1 close (2026-08-01): Phase 11's `11-HUMAN-UAT.md` (2 pending human-verify scenarios — CR-01 lockout-guard re-verification, tray-behavior regression spot-check) — both confirmed pass on the real rig; `11-VERIFICATION.md` status updated `human_needed` → `passed`. No longer an open item.

Resolved at v1.2 close (2026-08-04): Phase 13's `13-VERIFICATION.md` had the same stale-status issue as Phase 11 above — body already showed ICON-01..04 confirmed on real rig hardware via prior in-phase checkpoints (13-03, 13-04 Task 3), frontmatter `status: human_needed` was stale and flipped to `passed` (commit `38d756a`). No longer an open item.

Resolved 2026-08-08: Phase 16's `16-HUMAN-UAT.md` (2 pending items — DISPLAY-13 real crash-mid-toggle test, post-gap-closure Settings layout re-confirmation). Item 2 (Settings layout) confirmed passed directly by the user on the real rig. Item 1 (DISPLAY-13) was formally waived rather than tested — user judged the exact-crash-mid-toggle scenario niche/low-probability and not worth dedicated rig time; recorded as a documented override (`accepted_by: "Blaz Pivk"`, `accepted_at: 2026-08-08`) in `16-VERIFICATION.md` frontmatter per `verification-overrides.md` convention, not a false "tested and passed" claim. `16-VERIFICATION.md` status updated `human_needed` → `passed`. No longer an open item.

## Session Continuity

Last session: 2026-08-09T19:46:21.186Z
Stopped at: Phase 19 UI-SPEC approved
Resume file: .planning/phases/19-monitor-tile-dashboard-monitorpanelform-retirement/19-UI-SPEC.md

## Operator Next Steps

- Run `/gsd:plan-phase 19` to plan the Monitor-Tile Dashboard & MonitorPanelForm Retirement phase
