# Phase 3: App & Audio Control - Context

**Gathered:** 2026-07-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Replace Phase 2's fake mutation adapters for companion-app control and audio-device switching with real Windows implementations. Toggling to rig mode must really launch/focus the Moza Companion app and really switch the default audio playback device; toggling back must really minimize the app and really restore the previous default audio device. Monitor control stays faked until Phase 4 — this phase touches only `WindowsAppController` and `WindowsAudioController`'s currently-stubbed mutation methods (`LaunchOrFocus`, `MinimizeIfRunning`, `SetDefault`, `Restore`), plus whatever model/settings changes those real implementations require (e.g. `AudioState` needs to hold more than a single device ID once all three audio roles are captured/restored).

</domain>

<decisions>
## Implementation Decisions

### Audio Roles
- **D-01:** Settings keeps exactly one audio device picker per mode (normal, rig) — not per-role pickers. Internally, `SetDefault`/`Restore` apply that single chosen device ID to all three Windows audio roles (`eConsole`, `eMultimedia`, `eCommunications`) via `IPolicyConfig::SetDefaultEndpoint`, matching what the Sound Control Panel itself does. Per-role granularity was explicitly rejected as over-engineering for a personal 2-device rig setup.
- **D-02:** `AudioState`/`CaptureState` must be expanded to capture the default device per role (not just the single `Role.Multimedia` read Phase 2 left in place) so restore can be exact across all three roles, per REQUIREMENTS.md AUDIO-02 ("across all relevant audio roles") and `PITFALLS.md`'s Pitfall 5/7 (partial-role switch, incomplete snapshot).

### Audio Switch Verification
- **D-03:** After calling `SetDefaultEndpoint` for a role, re-query the actual default device for that role and compare against what was requested. If it doesn't match, `SetDefault` throws — this is a real, visible failure signal in Phase 3, not a silently-trusted HRESULT. Directly addresses `PITFALLS.md`'s Pitfall 6 (APIs reporting success while state doesn't actually change).
- **D-04:** This exception is allowed to bubble up through `ToggleService`/`MainForm`'s existing exception handling as-is for now. Richer per-step failure reporting (which step succeeded/failed, partial-failure recovery) is explicitly Phase 5 / CORE-04 scope — Phase 3 only needs the underlying verification logic to exist and to surface *something* rather than nothing.

### Companion App — Preflight & Launch Ordering
- **D-05:** `ToggleToRigMode` must verify the configured companion app `.exe` path still exists on disk as the very first step — before capturing or mutating monitor or audio state. A missing path throws immediately with nothing yet touched, avoiding the current ordering (monitor disable → audio switch → app launch last) leaving monitor/audio already mutated when the app step fails.

### Companion App — Launch/Focus Window-Handle Handling
- **D-06:** `LaunchOrFocus` behavior differs by case:
  - **Not running:** `Process.Start`, then poll `MainWindowHandle` for a few seconds (window is still opening) before giving up.
  - **Already running but `MainWindowHandle` is zero:** do NOT retry/poll — per CLAUDE.md, treat this as "running but no window to manipulate right now" (e.g. genuinely tray-only) and move on without failing the toggle. Retrying here would add a pointless multi-second delay for an app that may never produce a window.
- **D-07:** `MinimizeIfRunning` stays best-effort per PROJECT.md's existing scope decision — `ShowWindow(hWnd, SW_MINIMIZE)` when a window handle is available; a zero handle is a no-op, not a failure.

### Claude's Discretion
- Exact retry/poll duration and interval for the fresh-launch window-handle wait (D-06) — the discussion settled the *behavior* (retry only on fresh launch), not the precise seconds/interval; left to planner/researcher to pick a reasonable value (discussion referenced "a few seconds" as the ballpark).
- COM interop specifics (vtable layout, GUIDs, object lifecycle/disposal per call) — per STACK.md, only the modern Windows 8+ `IPolicyConfig` variant is needed; no Vista fallback.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project-level
- `.planning/PROJECT.md` — core value, requirements, evolution rules
- `.planning/REQUIREMENTS.md` — AUDIO-01/02, APP-01/02/03 (mapped to this phase)

### Research (from /gsd:new-project)
- `.planning/research/STACK.md` — hand-embedded `IPolicyConfig` COM interop (not `AudioSwitcher.AudioApi`), NAudio for enumeration, modern-only GUID/vtable (no Vista fallback)
- `.planning/research/ARCHITECTURE.md` — `IAudioController`/`IAppController` interfaces, `PolicyConfigAudioController` component boundary, elevation/UIPI constraints
- `.planning/research/PITFALLS.md` — Pitfall 5 (partial-role switch, undocumented/version-dependent `IPolicyConfig`), Pitfall 6 (silent success-but-no-effect on both display and audio APIs), Pitfall 7 (incomplete snapshot breaks exact restore) — all directly load-bearing for D-01 through D-04
- `.planning/research/SUMMARY.md` — Phase 5 (renumbered to Phase 3 in current roadmap) rationale: "set all three roles per switch," "explicit COM object release each cycle," "ID + friendly-name fallback"

### Prior phases
- `.planning/phases/01-monitor-disable-feasibility-spike/01-CONTEXT.md` — D-08 (non-elevated, `asInvoker`) applies here too; elevation must stay minimal so cross-process `SetForegroundWindow` on the Moza Companion window doesn't break under UIPI
- `.planning/phases/02-foundations-gui-shell/02-CONTEXT.md` — D-07 (real `IsRunning` via `Process.GetProcessesByName`, already implemented), D-08 (`ToggleService` orchestration already wired to fake mutation adapters — this phase swaps adapters, not orchestration logic)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/RigToggle.Windows/WindowsAppController.cs` — `IsRunning` is already real (`Process.GetProcessesByName` matched on filename without extension, correct disposal of `Process` handles). `LaunchOrFocus`/`MinimizeIfRunning` are the documented no-op stubs this phase fills in.
- `src/RigToggle.Windows/WindowsAudioController.cs` — `GetPlaybackDevices` and `CaptureState` are already real via NAudio's `MMDeviceEnumerator` (correct per-call enumerator/device disposal already in place, matching the "never cache across session" pattern). `CaptureState` currently only reads `Role.Multimedia` — must be expanded per D-02. `SetDefault`/`Restore` are the no-op stubs this phase fills in with `IPolicyConfig` interop.
- `src/RigToggle.Windows/WindowsAudioController.cs`'s `TryResolveDevice` — defensive saved-device-ID resolution already exists and can be reused/extended for per-role restore resolution.
- `src/RigToggle.Core/ToggleService.cs` — orchestrates snapshot → mutate → restore; `ToggleToRigMode` currently does monitor → audio → app in that order (D-05 requires inserting the app-path preflight check before any of this).

### Established Patterns
- Interface-per-concern (`IAppController`, `IAudioController`) with a real `RigToggle.Windows` implementation — this phase continues that pattern, does not introduce a new one.
- Fake-then-real adapter swap: Phase 2 built `ToggleService` against fakes for the exact methods this phase makes real; `ToggleService`'s call sites should need minimal to no changes beyond the D-05 preflight insertion.

### Integration Points
- `AudioState` (`src/RigToggle.Core/Models/AudioState.cs`) currently holds a single nullable `DefaultDeviceId` — must be extended to hold a per-role snapshot (D-02), which also touches `StateSnapshot`/`JsonSnapshotStore` serialization.
- `IAppController`/`IAudioController` interface contracts likely need no signature changes — same method set, just real implementations — unless preflight validation (D-05) is better expressed as a new interface method (e.g. `IAppController.Exists(path)`) rather than living inline in `ToggleService`. Planner's call.

</code_context>

<specifics>
## Specific Ideas

- Audio: one device selection per mode in Settings (unchanged from Phase 2), fanned out to all 3 roles internally — no UI changes needed in Settings for this phase.
- Verification is a hard requirement, not a nice-to-have: SetDefault must re-query and throw on mismatch, per Pitfall 6.
- Preflight the companion app path before touching anything else in `ToggleToRigMode` (D-05) — this is a behavior/ordering change to existing Phase 2 code, not just new code.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (Full step-by-step partial-failure reporting/recovery UI is already correctly scoped to Phase 5 per ROADMAP.md CORE-04 — not built here, only the underlying verification signal per D-03/D-04.)

</deferred>

---

*Phase: 3-App-Audio-Control*
*Context gathered: 2026-07-24*
