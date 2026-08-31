---
gsd_state_version: 1.0
milestone: v2.2
milestone_name: Auto-Update, Single-Instance Guard & Smaller Footprint
status: Awaiting next milestone
stopped_at: Phase 27 Plan 01 paused at Task 3 (rig-hardware checkpoint) after Task 2 committed (6b3058b)
last_updated: "2026-08-31T12:57:00.834Z"
last_activity: 2026-08-29
last_activity_desc: Completed and rig-verified quick tasks 260829-fnt (Help>About menu) and 260829-ga9 (consolidate Check-for-Updates entry points + fix silent-feedback bug) - user approved
progress:
  total_phases: 1
  completed_phases: 0
  total_plans: 3
  completed_plans: 1
current_phase: 27
current_phase_name: monitor-activation-logic-redesign
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-08-18)

**Core value:** A single reliable action that disables the primary monitor (not just powers it off) and switches audio output — so games that mishandle secondary-monitor launches reliably open on the rig monitor — and just as reliably applies Normal mode's own explicitly configured monitor/audio state on toggle-back (revised at v2.0 — see DISPLAY-10/AUDIO-04).
**Current focus:** v2.2 milestone complete (Phases 24-26) — ready for milestone close

## Current Position

Phase: 27 - Monitor Activation Logic Redesign
Plan: 27-01 (paused at Task 3 — rig-hardware checkpoint, blocking)
Status: In progress — Task 2 committed (6b3058b), awaiting operator rig verification
Last activity: 2026-08-31 — ActivateMonitorsCore collapsed to single-pass Extend-only shape; solution builds clean, RigToggle.Tests 249/249; Task 3 (real-rig Checks A-E) not yet performed

## Performance Metrics

**Velocity:**

- Total plans completed: 86
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
| 20 | 3 | - | - |
| 21 | 3 | - | - |
| 22 | 5 | - | - |
| 23 | 3 | - | - |
| 24 | 1 | - | - |
| 25 | 4 | - | - |

**Recent Trend:**

- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 23 P01 | 35min | 2 tasks | 9 files |
| Phase 23 P02 | 25min | 3 tasks | 3 files |
| Phase 23 P03 | 40min | 2 tasks | 1 files |
| Phase 25 P04 | 40min | 2 tasks | 3 files |
| Phase 25 P04 | ~40min (Tasks 1-2); Task 3 closed via operator override | 3 tasks | 3 files |

## Accumulated Context

### Roadmap Evolution

- Phase 27 added 2026-08-31: Monitor Activation Logic Redesign — post-v2.2 ad-hoc phase (not part of a milestone), scoped directly via `/gsd-phase` at user request following a reopened debug session (`.planning/debug/resolved/monitor-position-regre.md`, 22 rounds) that diagnosed the SAM7489/Odyssey G5 monitor's persistent activation flakiness as symptomatic of an overbuilt correction/retry architecture (15 independently-added mechanisms across 20+ incremental rounds) rather than a single fixable bug. User chose redesign over further incremental patching; full target-architecture proposal is written into that debug file's Resolution (round 22) section for this phase's planning to consume.
- Phase 11 edited: edited fields: title, goal, depends_on (corrected from Phase 10 to Phase 8)
- v1.2 roadmap created 2026-08-02: 3 phases derived from THEME/ICON/DOCS requirement categories (coarse granularity). Phase 12 (Theme Infrastructure) sequenced first per research risk-priority (highest-novelty work; live-update gap and `--tray` handle-timing risk). Phase 13 (Tray & App Icon Redesign) is architecturally independent of Phase 12 but sequenced second by convention, not dependency. Phase 14 (README) sequenced last, depends on both since it documents/screenshots the finished visual result. Rig-only-catchable risks (live theme flip while running, both taskbar themes, extended-session GDI handle stability, `--tray` hidden-start timing, Windows 10 fallback) were folded into Phase 12/13 success criteria and plan-level human verification rather than a separate verification phase, consistent with this project's established UAT-per-phase pattern (Phase 8, 9, 11).
- v2.0 roadmap created 2026-08-04: 4 phases (15-18) derived from the 19 v2.0 requirements (coarse granularity), continuing numbering from v1.2's Phase 14. Structure follows research/SUMMARY.md's proposed sequencing closely: Phase 15 (optional App/Audio targets) first as lowest-risk, no-prerequisite validation-gate relaxation; Phase 16 (Normal-mode explicit monitor config + mode-store redesign) second as the core, highest-risk piece — forces `IsInRigMode()` off snapshot-file-presence onto an explicit persisted flag, landed alongside the monitor-set rewrite per research's Pitfall 4/5 warnings; Phase 17 (manual monitor panel + shared safety guard) third, depends on Phase 16's controller-call unification so the panel doesn't introduce a second implementation of "toggle a monitor." DISPLAY-12 (safety guard enforced identically across Rig toggle, Normal toggle, and the manual panel) was assigned to Phase 17 rather than Phase 16 since full three-way enforcement can only be verified once the panel (the third path) exists. Phase 18 (cleanup + exe-size reduction) last, since the snapshot/restore subsystem is only confirmed genuinely dead once Phase 16 ships. DISPLAY-13 (crash-recovery marker) and PANEL-04/05 (confirmation gate, Identify action) — both resolved into requirements after research/SUMMARY.md was written — were folded into Phase 16 and Phase 17 respectively, consistent with the research's existing rationale for those phases.
- v2.1 roadmap created 2026-08-09: 5 phases (19-23) derived from the 14 v2.1 requirements (TILE-01..07, MAIN-01..02, SETTINGS-01..02, THEME-07..09; coarse granularity), continuing numbering from v2.0's Phase 18. Structure follows research/SUMMARY.md's proposed sequencing closely: Phase 19 (monitor-tile dashboard + MonitorPanelForm retirement) first — highest complexity/regression-risk, absorbs all 5 of the retiring panel's behaviors (TILE-01..07) plus the toggle/Settings repositioning (MAIN-01/02), sequenced so the tile replacement is proven working before the standalone panel is deleted. Phase 20 (custom toggle-switch control, THEME-08) second, right after the dashboard stabilizes since it redraws the same button Phase 19 repositions — avoids compounding two risky UI changes in one diff. Phase 21 (accent-color reading, THEME-07) third, soft-ordered before Phase 22 since it's architecturally independent but pairs naturally with an accent-tinted switch. Phase 22 (manual light/dark override, THEME-09) fourth, last among theme work since it decorates whatever `IThemeProvider` interface Phase 21 leaves behind. Phase 23 (SettingsForm layout pass) last — fully independent of every other phase, scheduled as the natural mop-up/filler with no dependency edges. All 14 requirements mapped 1:1, no orphans.
- v2.2 roadmap created 2026-08-18: 3 phases (24-26) derived from the 10 v2.2 requirements (UPDATE-01..07, INSTANCE-01/02, PERF-03; coarse granularity), continuing numbering from v2.1's Phase 23. Structure follows research/SUMMARY.md's proposed build order exactly: Phase 24 (exe-size reduction, PERF-03) first — fully isolated from the other two features, zero `Program.cs`/runtime code changes, so every later phase's manual builds already run against the smaller artifact. Phase 25 (single-instance guard, INSTANCE-01/02) second, sequenced before auto-update specifically because it restructures `Program.cs`'s startup gate sequence and must establish the internal-relaunch bypass pattern (UPDATE-07) deliberately, in isolation, rather than inventing it under pressure while also debugging the update-apply mechanism — UPDATE-07 was therefore mapped to Phase 25 (where the bypass is built and rig-verified via scripted relaunch) rather than Phase 26 (where it's consumed end-to-end). Phase 26 (auto-update, UPDATE-01..06) last, depends on Phase 25's bypass pattern; UPDATE-01 (version-stamping infra) folded in as an early sub-step of this phase rather than split into its own phase since it's meaningless without the check logic it unblocks, and UPDATE-05 (failed-apply safety) and UPDATE-06 (manual check) were folded into the same phase as UPDATE-04 per research guidance (safety property of the same apply step; thin additive UI hook once check logic exists). All 10 requirements mapped 1:1, no orphans.

### Decisions

Full decision log lives in PROJECT.md's Key Decisions table. v2.0's decisions (optional-target skip/fail semantics, `IModeStore`-backed mode tracking, explicit symmetric Normal-mode config replacing snapshot-restore, shared Manual Monitor Panel safety guard, MSBuild-only exe-size levers) are all implemented, rig-verified, and validated — see PROJECT.md for outcomes.

- [Phase ?]: OverridableThemeProvider decorator resolves preview ?? persisted ThemeOverride ?? live OS signal; composition-root swap alone gives all three IsDark/IsDarkTheme copies override awareness with zero per-form edits
- [Phase ?]: ThemeChanged is re-raised unconditionally on every inner OS flip (not diffed against effective theme) — deliberate deviation from ARCHITECTURE.md's suggested refinement
- [Phase ?]: Application color mode now derived from the effective theme at every theming call site plus one composition-root priming call, replacing the prior always-follow-OS SetColorMode(System) calls
- [Phase ?]: Two constructor-injected callback delegates (previewThemeOverride, applyThemeOverride) mirror the existing _applyTrayVisibility idiom -- no AppSettings-level event was invented
- [Phase ?]: FormClosed lambda calls _applyThemeOverride() unconditionally (not branched on DialogResult) -- correct for Discard/Esc/X/Save-then-close alike since a successful Save leaves it a no-op
- [Phase ?]: Phase 23 rig-verified 2026-08-17: all 15 checks PASS, all 3 success criteria PASS, THEME-09 ticked complete — check 12 (in-place Settings repaint) passed both halves, no gap-closure plan needed
- [Roadmap/v2.2]: No new NuGet packages for any of the three v2.2 capabilities — auto-update (HttpClient + System.Text.Json), single-instance guard (named `System.Threading.Mutex`), and exe-size reduction (MSBuild feature switches only) all hand-rolled, matching this codebase's existing pattern (hand-rolled `IPolicyConfig` COM interop, `RegisterHotKey` P/Invoke)
- [Roadmap/v2.2]: Single-instance guard sequenced before auto-update specifically so the internal-relaunch bypass (UPDATE-07) is built and rig-verified in isolation, not invented under pressure while also debugging the self-replace mechanism
- [Phase ?]: Abandoned readiness-mutex wait treated as acquired=true (CR-01 fix) -- routes into existing release branch rather than re-abandoning the mutex for the next waiter
- [Phase ?]: WR-02's readiness-mutex construction guarded with a broad catch, deliberately asymmetric to the main mutex's narrow catch, since the readiness mutex is a best-effort optimisation and the main mutex is correctness-critical
- [Phase ?]: WR-01's cross-namespace probe explicitly deferred -- accepted as a known limitation at ASVS L1 for this single-user rig
- [Phase ?]: Phase 25 Plan 04 Task 3 closed by explicit operator authorization on partial evidence (build succeeds + app works on real hardware); the three-run flakiness check, ApplyUpdateBypass_* confirmation, and live CR-01 repro were NOT performed and remain open follow-up items
- [Phase ?]: [Phase 27 Plan 01]: ActivateMonitorsCore collapsed to single-pass Extend-only shape (no scoped-plan branch, no retry loop); orphaned scoped-activation helpers left in place per operator's option-c staged-removal decision, pending Task 3's real-rig verification before Plan 02 deletes them

### Pending Todos

None.

### Known Limitations

- The relaunch-based launch redesign's `MinimizeIfRunning`/`IsRunning` (toggle-back) still derive the process name from the configured launch-target path via `Path.GetFileNameWithoutExtension`. If the user configures a `.lnk` (rather than the target `.exe` itself) as the launch target, that derived name will typically not match the real running process name, so toggle-back's minimize call may silently no-op. Documented, not patched (out of scope, carried over from v1.0).

### Blockers/Concerns

Open (2026-08-31): Phase 27 (monitor-activation-logic-redesign) HALTED at Plan 01 Task 3's real-rig
verification checkpoint. The Extend-only redesign (whole-topology `PathInfo.ApplyTopology(Extend)`
as the sole activation mechanism, commit `6b3058b`) reliably FAILS to activate the Samsung Odyssey
G5 (device path SAM7489) — confirmed on real rig hardware across 6 consecutive attempts, 0
successes, identical `InvalidOperationException: Monitor enable did not take effect` every time
(dialog text captured, debug.log captured — see `27-01-SUMMARY.md`'s Task 3 section for full
detail). This is exactly `root_cause (2)` flagged as a known risk in
`.planning/debug/monitor-position-regre.md` §22.3, now confirmed rather than hypothetical. Per the
plan's own acceptance criteria, a Check A/C/D failure halts execution and Plan 02/03 do not run —
they remain blocked (`27-01-SUMMARY.md` frontmatter `status: halted`). The operator was offered the
choice to revert commit `6b3058b` (restoring the still-intact scoped-activation path, which
previously handled this monitor) or leave the Extend-only code in place unresolved, and explicitly
chose the latter — **no revert has been performed; this is a deliberate pause, not an oversight.**
Next step is the operator's call: revert and close Phase 27 as "redesign rejected by rig evidence,"
or investigate further (e.g. whether Extend-only can be salvaged for this specific monitor) before
deciding. Do not proceed to Plan 02 or auto-revert without further explicit operator direction.

Open: Phase 24 Plan 01 Task 3 (blocking checkpoint) requires real Windows rig hardware verification — not available in this build environment. Operator must build `RigToggle.App.exe` natively on Windows at commit `67f4bfd`, run the six checks in `24-01-PLAN.md` Task 3, and report results to resume execution. PERF-03 is not yet marked complete and Phase 24 is not yet closed pending this.

Resolved 2026-08-16: Phase 22 rig verification originally FAILED (2026-08-15, recorded in `22-03-SUMMARY.md`, commit a40e173) — Check 1 (monitor grid/audio ComboBox absent) and Check 3 (manual resize not working). Gap-closure plans 22-04/22-05 fixed both root causes (`Percent 100F` `TableLayoutPanel` row collapsing inside `AutoSize=true` containers; `Form.AutoSize` fighting manual `WM_SIZING`); a second rig pass (22-05) confirmed all 17 checks PASS on Windows 11 25H2 at 100/125/150% scale. Phase 22 is complete (`ROADMAP.md`, `PROJECT.md` Validated (v2.1) section). No longer an open item.

Resolved 2026-08-17: Research flagged Phase 21 (accent color) and Phase 23 (manual-override) as needing deeper research during planning — see `.planning/research/SUMMARY.md` "Research Flags". Both phases have since shipped and rig-verified with no unresolved accent-color or theme-sequencing gaps (Phase 21 complete 2026-08-11; Phase 23 complete 2026-08-17, all 15 rig checks PASS). No longer an open item.

Open (carried from v2.2 research, `.planning/research/SUMMARY.md` "Gaps to Address"): the rename-while-running self-update mechanism, Mark-of-the-Web/SmartScreen behavior for HttpClient downloads, `mainForm.BeginInvoke`/`Handle` existence under `--tray` hidden startup, and the exact signal mechanism for single-instance activation (named pipe vs. `RegisterWindowMessage`) are all flagged MEDIUM confidence and not yet rig-verified — expected to be resolved during Phase 25/26 planning and rig checkpoints, not blocking roadmap creation.

Resolved 2026-08-21 (partial): Phase 25 Plan 04 Task 3's blocking checkpoint (real Windows rig verification) was closed by explicit operator authorization rather than by completing its original ask. The operator confirmed the solution builds and the app functions correctly on real Windows hardware, but declined to run the three-consecutive-clean-runs flakiness check, confirm the three `ApplyUpdateBypass_*` facts, or perform the live CR-01 (abandoned-mutex) reproduction ("doesn't work on my pc") — see `25-04-SUMMARY.md`'s "Task 3: Operator Verification" section for the verbatim exchange. INSTANCE-01/INSTANCE-02/UPDATE-07 are marked complete on the operator's sign-off; the three unperformed checks remain open follow-up items, not fabricated passes, and should be run if the opportunity arises.

Resolved 2026-08-29 (partial): Phase 26's `26-UAT.md` closed by explicit operator authorization rather than by completing its original 5-test ask. Test 1 (real exe swap and relaunch) was genuinely rig-verified live against a real v2.2.1 -> v2.2.2 release cut for this purpose — user confirmed the exe swapped correctly, no SmartScreen interstitial, no autostart regression. Tests 2-5 (interrupted-update recovery, auto-rollback timing under CR-02's 10s window, on-launch check reliability under `--tray`, and real release-notes Markdown rendering) were closed on the operator's sign-off ("everything seems fine so just confirm it") after the orchestrator explicitly flagged that tests 2-4 require deliberate failure-injection scenarios (killing the update helper mid-swap, simulating disk-full, killing the process within a 10s window) that normal use does not exercise — see `26-UAT.md`'s "Operator sign-off" section for the verbatim exchange. The four unperformed checks remain open follow-up items, not fabricated passes, and should be run if the opportunity arises — particularly tests 2/3's failure-recovery paths, since those protect against leaving the app unlaunchable after a bad update.

- Phase 27 Plan 01 Task 3 (blocking checkpoint) requires real Windows rig hardware verification of the Extend-only ActivateMonitorsCore redesign (Checks A-E: single-tile enable, position preservation, full swap ordering, Odyssey G5 flakiness x3, failure surfacing). Not available in this build environment. Task 2's commit (6b3058b) built and tested clean (RigToggle.Tests 249/249). Operator must build RigToggle.App.exe natively on Windows from this commit, run Task 3's checks, and report results verbatim to resume execution.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260728-qj1 | Fix WindowsMonitorController.GetAllMonitors() duplicate-row/dual-primary bug found during Phase 6's 06-06 rig checkpoint | 2026-07-28 | fe0aee7 | [260728-qj1-fix-windowsmonitorcontroller-getallmonit](./quick/260728-qj1-fix-windowsmonitorcontroller-getallmonit/) |
| 260728-rmp | Relabel Settings monitor grid columns (Disable/Enable -> Off (Rig)/On (Rig)) with tooltips and a permanent explanation label, clarifying the grid only configures the transition into Rig Mode | 2026-07-28 | d76b5db | [260728-rmp-improve-settings-monitor-grid-clarity-re](./quick/260728-rmp-improve-settings-monitor-grid-clarity-re/) |
| 260804-9rt | Fix UF-14-01 and UF-14-02 from Phase 14 security audit: add least-privilege `permissions: contents: read` to build.yml, add restore/build/test gate before publish in release.yml | 2026-08-04 | dd974e3 | [260804-9rt-fix-uf-14-01-and-uf-14-02-from-phase-14-](./quick/260804-9rt-fix-uf-14-01-and-uf-14-02-from-phase-14-/) |
| 260828-vit | Extend UpdateVersionComparer to three-component semver (vX.Y.Z) patch comparison, fix a latent phantom-update trap and a skip-tracking patch-discard bug found along the way, bump RigToggle.App to 2.2.1, and tag/push v2.2.1 to cut a real release for Phase 26 UAT testing | 2026-08-28 | 8087afd | [260828-vit-extend-updateversioncomparer-to-support-](./quick/260828-vit-extend-updateversioncomparer-to-support-/) |
| 260829-fnt | Add a MenuStrip Help > About entry point on MainForm opening a themed AboutForm (name, version, Check for Updates); extract shared FormatDisplayVersion/RunningVersionText formatter. Rig-verification checkpoint superseded by 260829-ga9 (not verified as-is; see that task) | 2026-08-29 | 04d765b | [260829-fnt-add-a-menustrip-help-about-item-to-mainf](./quick/260829-fnt-add-a-menustrip-help-about-item-to-mainf/) |
| 260829-ga9 | Remove the redundant tray and Settings "Check for Updates" entry points (About dialog becomes the sole manual entry point); fix a real silent-feedback bug where manual checks relied exclusively on notifyIcon.ShowBalloonTip (a no-op when the tray icon is hidden, the default) by giving the About dialog its own reliable inline status label via a new shared UpdateCheckMessageFormatter. Rig-verified and approved 2026-08-29 | 2026-08-29 | 5f3f577 | [260829-ga9-remove-tray-settings-check-for-updates-e](./quick/260829-ga9-remove-tray-settings-check-for-updates-e/) |
| 7 | Bump RigToggle.App to 2.2.2 and tag/push v2.2.2 to cut a real newer release for testing the update flow against the currently-installed 2.2.1 build | 2026-08-29 | 24b0867 | — |

## Deferred Items

Items acknowledged and carried forward from previous milestone close:

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| dropped | LOG-01 (toggle history/log) | Dropped, not carried forward | Deferred at v1.0, v1.1, v1.2, and v2.0 scoping; explicitly dropped by user at v2.1 scoping 2026-08-09 — will not resurface as a backlog candidate |
| shipped | THEME-07 (accent-color-aware highlight) | Scoped into v2.1 Phase 21, shipped | Deferred at v1.2 scoping 2026-08-02; scoped into v2.1 roadmap 2026-08-09; shipped 2026-08-17 |
| shipped | THEME-08 (custom-drawn toggle-switch control) | Scoped into v2.1 Phase 20, shipped | Deferred at v1.2 scoping 2026-08-02; scoped into v2.1 roadmap 2026-08-09; shipped 2026-08-17 |
| shipped | THEME-09 (manual theme override) | Scoped into v2.1 Phase 23, shipped | Deferred at v1.2 scoping 2026-08-02; scoped into v2.1 roadmap 2026-08-09; reordered from Phase 22 to Phase 23 at user request 2026-08-11; shipped 2026-08-17 |
| backlog | UPDATE-08 (WM_COPYDATA/named-pipe IPC between instances) | Deferred, tracked in REQUIREMENTS.md Future Requirements | Explicitly TRIG-02/TRIG-03 territory, permanently out of scope per v1.1 close; not reopened at v2.2 scoping 2026-08-18 |
| backlog | DIST-01 (Velopack/installer packaging reconsideration) | Deferred, tracked in REQUIREMENTS.md Future Requirements | Only relevant if a future milestone deliberately drops the standalone-.exe distribution constraint; not applicable to v2.2 |

Permanently out of scope (not v2 candidates):

| Category | Item | Status | Decided At |
|----------|------|--------|-------------|
| out-of-scope | TRIG-02 (CLI toggle argument) | Removed — not needed | v1.1 milestone close 2026-08-01 — Phase 10 never planned/executed; tray+hotkey triggers already cover the daily-use need, user confirmed CLI trigger unnecessary |
| out-of-scope | TRIG-03 (CLI status query argument) | Removed — not needed | v1.1 milestone close 2026-08-01 — same as TRIG-02 |
| out-of-scope | UI modernization second pass (rounded corners, borderless chrome, motion/animation, Fluent iconography) | Explicitly raised and dropped | v2.2 scoping 2026-08-18 — user explicitly raised then dropped it; v2.1's redesign stands as-is |
| out-of-scope | Silent background auto-update / manual-check-only auto-update / silent-exit on duplicate launch / delta updates / IL trimming / Native AOT / PublishReadyToRun for exe size | User-locked design choices, see REQUIREMENTS.md Out of Scope table | v2.2 requirements definition 2026-08-18 |

Items acknowledged and deferred at v1.0 milestone close on 2026-07-26, reconfirmed at v1.1 close on 2026-08-01, reconfirmed again at v1.2 close on 2026-08-04, reconfirmed once more at v2.0 close on 2026-08-09, reconfirmed again at v2.1 close on 2026-08-17, and reconfirmed again at v2.2 close on 2026-08-29 (pre-close artifact audit — all are scanner false positives, not real gaps, confirmed by direct inspection each time):

| Category | Item | Status |
|----------|------|--------|
| debug | knowledge-base | `.planning/debug/knowledge-base.md` is a persistent reference doc (not a debug session) sitting directly in `.planning/debug/`; the audit scanner treats every `.md` file in that directory as a session and flags it "unknown" for lacking a `status:` field. Not an open investigation — acknowledged and left as-is. |
| uat_gap | Phase 05 (05-HUMAN-UAT.md) | File's own frontmatter already reads `status: resolved` with 0 pending scenarios; the scanner surfaces any existing UAT file regardless of resolution. Already resolved — acknowledged, no action needed. |
| quick_task | 260728-qj1-fix-windowsmonitorcontroller-getallmonit | Scanner flags "missing" status, but `260728-qj1-SUMMARY.md` exists and STATE.md's Quick Tasks Completed table confirms it shipped in commit `fe0aee7`. False positive — the scanner's completion check doesn't recognize this quick-task's status format. |
| quick_task | 260728-rmp-improve-settings-monitor-grid-clarity-re | Same false-positive pattern as above; shipped in commit `d76b5db`, confirmed via `260728-rmp-SUMMARY.md` and the Quick Tasks Completed table. |
| quick_task | 260804-9rt-fix-uf-14-01-and-uf-14-02-from-phase-14- | Same false-positive pattern; shipped in commit `dd974e3`, confirmed via `260804-9rt-SUMMARY.md` and the Quick Tasks Completed table. First flagged at v2.1 close (2026-08-17) — did not appear as open at v2.0 close, but the underlying evidence (SUMMARY.md + completed-table row + commit) predates this and is not new work. |
| uat_gap | Phase 13 (13-HUMAN-UAT.md) | Same pattern as Phase 05: file's own body already showed 2/2 passed, 0 pending, "Gaps: None" before v1.2 close — frontmatter `status: partial` was stale and has been flipped to `resolved` (commit `38d756a`). Not a real gap. |

Items acknowledged and deferred at v2.2 milestone close on 2026-08-29 (new this milestone, not scanner false positives — genuine open follow-up items, recorded per the audit's [A]cknowledge path):

| Category | Item | Status |
|----------|------|--------|
| quick_task | 260829-fnt-add-a-menustrip-help-about-item-to-mainf | Task 3's rig-verification checkpoint was never independently answered — instead, follow-up quick task `260829-ga9` (which WAS completed, rig-verified, and approved) superseded it by removing/reworking the exact UI surfaces Task 3 would have checked. `260829-fnt-SUMMARY.md` frontmatter records `superseded_by: quick-260829-ga9`. Not a real gap — this task's own deliverables (MenuStrip, AboutForm, version formatter) are live in master, only its own checkpoint response is superseded. |
| uat_gap | Phase 26 (26-UAT.md) | Test 1 (exe swap/relaunch) genuinely rig-verified live against a real v2.2.1 -> v2.2.2 release. Tests 2-5 (interrupted-update recovery, auto-rollback/CR-02 timing, `--tray` on-launch check, real release-notes rendering) closed by explicit operator sign-off after being told what remained unverified ("everything seems fine so just confirm it") — not independently tested. Genuine open follow-up items, not fabricated passes; see `26-UAT.md`'s "Operator sign-off" section and `26-VERIFICATION.md`'s "Post-Verification Update" section for the full record. Should be run for real if the opportunity arises, particularly the failure-recovery paths (tests 2/3). |
| tech_debt | 6 pre-existing xUnit1031 warnings (`SingleInstanceGuardTests.cs:198,199`, `ToggleOrchestratorTests.cs:131,157,190,292`) | Predate Phase 26 entirely (confirmed via baseline `--no-incremental` build before any Phase 26 changes, re-confirmed after); none of Phase 26's own new/modified files introduce new warnings. Left as-is per each executor's stated scope boundary — a future cleanup pass can restore the zero-warning gate project-wide if desired. Not blocking, not new debt from this milestone. |

Resolved at v1.1 close (2026-08-01): Phase 11's `11-HUMAN-UAT.md` (2 pending human-verify scenarios — CR-01 lockout-guard re-verification, tray-behavior regression spot-check) — both confirmed pass on the real rig; `11-VERIFICATION.md` status updated `human_needed` → `passed`. No longer an open item.

Resolved at v1.2 close (2026-08-04): Phase 13's `13-VERIFICATION.md` had the same stale-status issue as Phase 11 above — body already showed ICON-01..04 confirmed on real rig hardware via prior in-phase checkpoints (13-03, 13-04 Task 3), frontmatter `status: human_needed` was stale and flipped to `passed` (commit `38d756a`). No longer an open item.

Resolved 2026-08-08: Phase 16's `16-HUMAN-UAT.md` (2 pending items — DISPLAY-13 real crash-mid-toggle test, post-gap-closure Settings layout re-confirmation). Item 2 (Settings layout) confirmed passed directly by the user on the real rig. Item 1 (DISPLAY-13) was formally waived rather than tested — user judged the exact-crash-mid-toggle scenario niche/low-probability and not worth dedicated rig time; recorded as a documented override (`accepted_by: "Blaz Pivk"`, `accepted_at: 2026-08-08`) in `16-VERIFICATION.md` frontmatter per `verification-overrides.md` convention, not a false "tested and passed" claim. `16-VERIFICATION.md` status updated `human_needed` → `passed`. No longer an open item.

## Session Continuity

Last session: 2026-08-31T12:56:53.256Z
Stopped at: Phase 27 Plan 01 paused at Task 3 (rig-hardware checkpoint) after Task 2 committed (6b3058b)
Resume file: .planning/phases/27-monitor-activation-logic-redesign/27-01-PLAN.md

## Operator Next Steps

- Start the next milestone with /gsd-new-milestone
