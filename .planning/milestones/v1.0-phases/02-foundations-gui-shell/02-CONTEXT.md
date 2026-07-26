# Phase 2: Foundations & GUI Shell - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver the settings/persistence foundation and full two-window GUI shell (Main + Settings), built against fake mutation adapters so the entire user-facing experience — configuring monitor/audio/app-path settings, seeing them persist, and clicking Toggle — can be validated with zero hardware risk before any real OS interop (Phase 3: app+audio, Phase 4: monitor) is built. Monitor/audio/app-running *enumeration* is real starting in this phase (read-only, zero-risk, already proven safe by Phase 1); only the *mutating* actions (disable monitor, switch audio device, launch/focus/minimize app) stay faked until Phases 3–4.

</domain>

<decisions>
## Implementation Decisions

### GUI Framework
- **D-01:** WinForms (per CLAUDE.md's primary recommendation) — not WPF. Two windows (Main, Settings), no XAML.
- **D-02:** System-default WinForms visual styling — no custom colors/fonts/theming.
- **D-03:** Settings window is a **modal dialog** launched from a button on Main — blocks Main until closed/saved. Not a separate non-modal window.
- **D-04:** Main window is **fixed-size**, not resizable.

### Real vs. Fake Boundary
- **D-05:** Monitor and audio device pickers in Settings show **real enumerated hardware** (via `WindowsDisplayAPI` for monitors, NAudio's `MMDeviceEnumerator` for audio endpoints, per STACK.md) — not placeholder/hardcoded data. Only the actual `Disable()` / `SetDefault()` mutation calls are faked (no-op) in this phase.
- **D-06:** Companion app path field uses a **file-browser dialog** (`OpenFileDialog` filtered to `.exe`), not a free-text field.
- **D-07:** "Is companion app already running" detection is **real now** (`Process.GetProcessesByName`, per CLAUDE.md — trivial, zero-risk, read-only). Only launch/focus/minimize actions stay faked until Phase 3.
- **D-08:** Phase 2 includes a **real `ToggleService` orchestration class** (per ARCHITECTURE.md's Pattern 2 — snapshot → mutate → restore sequencing), wired to the real enumeration adapters and fake mutation adapters. Phase 5 becomes an adapter swap, not new orchestration logic. `ToggleService` itself has zero Windows API references and is fully unit-testable now.

### Settings Screen Layout & Flow
- **D-09:** Settings dialog is **one window with three labeled sections** (Monitor, Audio devices, App path) — no tabs. Content is small enough to not need tab structure.
- **D-10:** If a previously-saved monitor/audio device/app path is no longer found when Settings reopens (hardware changed), show the picker as **unselected with an inline warning** (e.g. "Previously selected device not found — please reselect"), rather than silently keeping a stale ID or showing a greyed-out stale entry.
- **D-11:** No manual "Refresh" button — Settings **re-enumerates every time it opens**. This is a rarely-opened one-time setup screen, not left open while plugging/unplugging hardware.
- **D-12:** Settings' Save button is **blocked/disabled until all three fields are validly selected** (monitor, both audio devices, app path) — no partial saves.

### Main Window / Mode Indicator
- **D-13:** Main window ships with its **full intended layout in Phase 2**, wired to real `ToggleService` + fake mutation adapters — not a placeholder mockup. Clicking Toggle actually runs the full snapshot → fake-mutate → flip-mode sequence end-to-end; it just has no real hardware effect yet. Avoids reworking the window in Phase 5.
- **D-14:** Current mode (Normal vs. Rig) is **derived from snapshot-file presence** on disk (per ARCHITECTURE.md's Pattern 3: `Mode == RigMode` iff a valid snapshot file exists), not a separate in-memory/persisted flag. This means startup-mode-detection (CORE-05, mapped to Phase 5) is effectively already exercised correctly in Phase 2 with the fake snapshot store.
- **D-15:** Main window shows a **small status line** for companion-app running state (e.g. "Moza Companion: Running" / "Not running"), reflecting the real detection from D-07 — not left as invisible internal state.

### Claude's Discretion
None — every discussed question reached an explicit user choice (all "Recommended" options were accepted as presented).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, requirements, evolution rules
- `.planning/REQUIREMENTS.md` — SETTINGS-01/02/03/04 (mapped to this phase)

### Research (from /gsd:new-project)
- `.planning/research/STACK.md` — WinForms/.NET 10 stack, `WindowsDisplayAPI` (monitor enumeration) and NAudio (`MMDeviceEnumerator`, audio enumeration) as the real-enumeration libraries for D-05
- `.planning/research/ARCHITECTURE.md` — layered architecture (GUI / Orchestration / Control Adapters / Persistence), `IMonitorController`/`IAudioController`/`IAppController` interfaces, `ToggleService` orchestration pattern (D-08), snapshot-before-mutate pattern, and snapshot-presence-as-mode pattern (D-14) — this phase implements these patterns with fake mutation adapters
- `.planning/research/FEATURES.md` — confirms Settings/config UI as table-stakes, one-time-setup framing
- `.planning/research/PITFALLS.md` — general interop pitfalls (not directly load-bearing for this phase's fake-adapter scope)

### Prior phase
- `.planning/phases/01-monitor-disable-feasibility-spike/01-CONTEXT.md` — D-08 (non-elevated, `asInvoker`) applies here too; D-01/D-02 (Linux sandbox execution boundary) means this phase's build/run verification also depends on the user testing on the actual Windows rig
- `.planning/phases/01-monitor-disable-feasibility-spike/01-VERIFICATION.md` and `.planning/phases/01-monitor-disable-feasibility-spike/RESULTS-TEMPLATE.md` — confirms real monitor enumeration via `WindowsDisplayAPI` (`PathInfo.GetActivePaths()`) works non-elevated on this rig's actual AMD/DisplayPort hardware, directly supporting D-05's real-enumeration decision for the monitor picker

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `spike/MonitorDetachSpike/Program.cs` — demonstrates working, non-elevated `WindowsDisplayAPI.PathInfo.GetActivePaths()` enumeration on this rig's hardware. The monitor-picker's real-enumeration code (D-05) can reuse this exact enumeration call; do not reuse the spike's disable/restore logic (that's Phase 4 scope, and it has a known primary-monitor repositioning gap — see `spike/RESULTS-TEMPLATE.md` Finding 3).

### Established Patterns
None yet — greenfield project, no production `src/` code exists.

### Integration Points
- This phase creates the `RigToggle.Core` (ToggleService, interfaces, fake adapters, JSON persistence) and `RigToggle.App` (WinForms Main/Settings) projects per `ARCHITECTURE.md`'s recommended structure. Monitor/audio *enumeration* implementations (real, per D-05) likely live in a thin `RigToggle.Windows` project or directly in `RigToggle.App` — planner's call, since only enumeration (not mutation) is real here.
- Phase 3 and Phase 4 will later swap the fake mutation adapters for real ones (`CcdMonitorController`, `PolicyConfigAudioController`, `Win32AppController`) without touching `ToggleService`, `Core`, or the GUI.

</code_context>

<specifics>
## Specific Ideas

- Settings dialog: exactly 3 sections in one window — Monitor (dropdown), Audio devices (2 dropdowns: normal + rig), App path (file-browser field) — no tabs, no wizard steps.
- Main window: mode indicator + Toggle button + Settings button + companion-app status line, all fixed-size, all wired to real `ToggleService` logic (fake mutation, real enumeration/detection underneath).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (Confirmation dialog before disabling the primary monitor is already correctly scoped to Phase 4 per ROADMAP.md DISPLAY-03 / Phase 4 Success Criterion #3 — not raised here to avoid scope creep.)

</deferred>

---

*Phase: 2-Foundations-GUI-Shell*
*Context gathered: 2026-07-24*
