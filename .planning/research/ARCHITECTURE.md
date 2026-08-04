# Architecture Research

**Domain:** v2.0 redesign of an existing four-project .NET 10 WinForms desktop app (RigToggle.Core / RigToggle.Windows / RigToggle.App / RigToggle.Tests) — configurable per-mode monitor targets replacing snapshot-restore, optional app/audio targets, a new live manual-monitor panel, and a shared safety/concurrency story.
**Researched:** 2026-08-04
**Confidence:** HIGH on integration points and component placement (verified directly against the real source tree — `ToggleService.cs`, `ToggleOrchestrator.cs`, `WindowsMonitorController.cs`, `WindowsAudioController.cs`, `AppSettings.cs`, `JsonSnapshotStore.cs`, `MainForm.cs`, `SettingsForm.cs`, `Program.cs`). MEDIUM on one specific recommendation flagged explicitly below (whether audio also drops snapshot-restore in Normal mode) — the milestone's literal feature list only mandates this for the monitor step; extending it to audio is this research's own inference from evidence in the codebase (an already-existing-but-unwired `NormalAudioDeviceId` field), not a directly-stated requirement, and should be confirmed during roadmap/planning rather than treated as settled.

## Standard Architecture

### System Overview

This is not a new subsystem — v2.0 is a redesign threaded through the existing four-project solution, reusing the same Core-interface / Windows-adapter / App-composition-root pattern already established for every prior milestone (`IMonitorController`/`WindowsMonitorController`, `IAudioController`/`WindowsAudioController`, etc.).

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ RigToggle.App  (composition root, Program.cs)                                  │
│                                                                                   │
│  ┌──────────────┐   ┌───────────────┐   ┌─────────────────────────────────┐  │
│  │ MainForm      │   │ SettingsForm   │   │ MonitorPanelForm (NEW)          │  │
│  │ mode label,   │   │ Rig-mode grid  │   │ non-modal, live per-monitor     │  │
│  │ Toggle button,│   │ (existing) +   │   │ enable/disable + status icons,  │  │
│  │ Settings btn, │   │ NEW Normal-    │   │ independent of toggle direction │  │
│  │ NEW "Monitors"│   │ mode grid      │   │                                  │  │
│  │ button opens  │   │ section        │   │ calls ManualMonitorService,      │  │
│  │ MonitorPanel  │   │                │   │ NOT ToggleOrchestrator           │  │
│  └──────┬────────┘   └───────┬────────┘   └────────────┬─────────────────────┘  │
│         │ ToggleOrchestrator │ ISettingsStore            │ ManualMonitorService  │
│         ▼                    ▼                           ▼                      │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │ RigToggle.Core                                                          │  │
│  │                                                                          │  │
│  │  SingleFlightGuard (NEW, extracted from ToggleOrchestrator)             │  │
│  │    - ONE shared Interlocked.CompareExchange busy-flag instance          │  │
│  │    - injected into BOTH ToggleOrchestrator AND ManualMonitorService     │  │
│  │        ┌───────────────────┐        ┌────────────────────────────┐    │  │
│  │        │ ToggleOrchestrator │        │ ManualMonitorService (NEW)  │    │  │
│  │        │ (existing shape,   │        │ thin wrapper: Activate/     │    │  │
│  │        │  now delegates to  │        │ Deactivate a single monitor │    │  │
│  │        │  SingleFlightGuard)│        │ via SingleFlightGuard        │    │  │
│  │        └─────────┬──────────┘        └──────────────┬───────────────┘    │  │
│  │                  ▼                                    ▼                   │  │
│  │        ToggleService (MODIFIED)               IMonitorController          │  │
│  │        - Normal-mode Monitor step:             .ActivateMonitors/          │  │
│  │          apply configured Normal set,           DeactivateMonitors        │  │
│  │          not Restore(snapshot)                 (UNCHANGED — same          │  │
│  │        - App/Audio steps: skip when              methods, same safety     │  │
│  │          settings field is null                  guard, reused as-is)     │  │
│  │        - mode tracked via IModeStore                                      │  │
│  │          (repurposed from ISnapshotStore),                                │  │
│  │          not snapshot-file presence                                       │  │
│  └────────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────┬──────────────────────────────────────────────┘
                                    │ ProjectReference
                                    ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│ RigToggle.Windows — WindowsMonitorController/WindowsAudioController UNCHANGED  │
│ at the method-signature level. ActivateMonitors/DeactivateMonitors' existing   │
│ "at least one active display must remain" guard (already in                    │
│ DeactivateMonitors, see Data Flow below) is reused verbatim by both the        │
│ toggle path and the new manual panel — this is what satisfies feature 5        │
│ ("one shared validation point, not duplicated logic") with ZERO new code.      │
│ WindowsMonitorController.Restore()/RestoreViaReconstruction() (~260 LOC)       │
│ become DEAD (no caller) once ToggleToNormalMode stops calling Restore().       │
└───────────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Layer / Status |
|-----------|----------------|-----------------|
| `AppSettings` | Gains `NormalMonitorsToDisable`/`NormalMonitorsToEnable` (new, mirrors existing `MonitorsToDisable`/`MonitorsToEnable`, which become implicitly "the Rig-mode set" — do not rename them, see Data Model section). `CompanionAppPath`, `RigAudioDeviceId` become genuinely optional (already nullable — the change is in `ToggleService`'s validation/branching, not the model). | `RigToggle.Core.Models` — **MODIFIED** |
| `IModeStore` | Repurposed `ISnapshotStore`: a minimal persisted "is a toggle currently in Rig mode" marker, replacing D-14's "mode == snapshot file exists" trick now that there is no snapshot payload to key off. Same `Exists()`/`Clear()` shape; `Save(StateSnapshot)` becomes something like `SetRigMode()` (no payload). | `RigToggle.Core.Abstractions` — **MODIFIED (repurposed)**, was `ISnapshotStore` |
| `ToggleService` | Normal-mode Monitor step becomes symmetric with Rig-mode's: `ActivateMonitors(normalEnableSet); DeactivateMonitors(normalDisableSet);` instead of `Restore(snapshot.Monitor)`. App step (`LaunchOrFocus`) and each direction's Audio step are skipped (recorded `NotAttempted`, not `Failed`) when the relevant settings field is null. Mode flag flips via `IModeStore` at the same points the snapshot used to be saved/cleared (see Data Flow). | `RigToggle.Core` — **MODIFIED** |
| `SingleFlightGuard` | NEW: the non-blocking `Interlocked.CompareExchange` busy-flag logic extracted verbatim out of `ToggleOrchestrator` into its own small class, so it can be shared. Same D-01 semantics (non-blocking, immediate rejection, no queueing). | `RigToggle.Core` — **NEW** (extracted) |
| `ToggleOrchestrator` | Unchanged public shape/behavior; internally delegates to an injected `SingleFlightGuard` instance instead of owning `_busy` itself. Gains a read-only `IsBusy` passthrough if the panel needs to grey out its buttons proactively (optional, see Anti-Patterns). | `RigToggle.Core` — **MODIFIED (internal only)** |
| `ManualMonitorService` | NEW: thin wrapper exposing `Activate(string devicePath)` / `Deactivate(string devicePath)`, guarded by the **same** `SingleFlightGuard` instance `ToggleOrchestrator` uses — a toggle in flight rejects a manual panel action and vice versa. Calls straight through to `IMonitorController`, no new business logic. | `RigToggle.Core` — **NEW** |
| `WindowsMonitorController` | `ActivateMonitors`/`DeactivateMonitors` **unchanged** — already implement the exact primitives both the redesigned `ToggleService` and the new `ManualMonitorService` need, including the existing "at least one active display must remain" throw inside `DeactivateMonitors` (line ~297-309 of the current file). `Restore()`/`RestoreViaReconstruction()` (~260 LOC) become dead once nothing calls them. | `RigToggle.Windows` — **MODIFIED (net LOC decrease)** |
| `WindowsAudioController` | `SetDefault`/`CaptureState` unchanged. `Restore()` becomes dead **only if** the recommended audio redesign (see below) is adopted; otherwise stays live. | `RigToggle.Windows` — **MODIFIED or UNCHANGED**, see Data Model section |
| `MainForm` | Gains one new button ("Monitors…") that opens/activates the new `MonitorPanelForm` (lazily constructed, kept as a singleton reference so re-clicking brings the existing instance forward instead of creating duplicates — same idea as `NotifyIcon_MouseClick`'s show/restore pattern already in this file). No other change to `MainForm`'s toggle/tray/hotkey logic — `RefreshUi()` keeps deriving from `_orchestrator.IsInRigMode()`, now backed by `IModeStore` instead of snapshot presence, with zero call-site changes. | `RigToggle.App` — **MODIFIED (additive)** |
| `MonitorPanelForm` | NEW non-modal `Form`: lists every monitor from `IMonitorController.GetAllMonitors()` with a status icon (active/OS-disabled) and an Enable/Disable action per row, backed by `ManualMonitorService`. A manual "Refresh" action re-queries `GetAllMonitors()` — no live push/polling for externally-caused changes (e.g. unplugging a monitor) is in scope; this mirrors `SettingsForm`'s existing one-shot `PopulateMonitorGrid()` pattern, just re-invoked on demand instead of once at `Load`. | `RigToggle.App` — **NEW** |
| `SettingsForm` | Gains a second monitor-target section (Normal-mode disable/enable sets), most naturally as a second `DataGridView` or a mode-selector toggling which set the existing grid edits — implementation detail for planning, not architecture. The **existing** `dgvMonitors` continues to edit `MonitorsToDisable`/`MonitorsToEnable` (Rig-mode set) unchanged. Validation (`ValidateSettingsForm`) gains a parallel check for the Normal-mode set and relaxes the current hard-requirement on `CompanionAppPath`/`RigAudioDeviceId` (features 1/2) to "required only if the user is configuring that target," not "required unconditionally." | `RigToggle.App` — **MODIFIED** |
| `JsonSettingsStore` | Needs a **new, one-time migration branch** (same shape as the existing `MonitorDevicePath` → `MonitorsToDisable` migration at lines 55-58): if `NormalMonitorsToDisable`/`NormalMonitorsToEnable` are both null on load from a pre-v2.0 settings.json, they stay null (not silently populated from anything) — v2.0's `IsFullyConfigured` gate should require the user to configure Normal-mode targets once via Settings before Rig-mode toggling is available again post-upgrade (see Anti-Patterns — do NOT try to auto-derive a Normal-mode set from the retired snapshot mechanism). | `RigToggle.Core.Persistence` — **MODIFIED** |

## Data Model: Where the Normal-Mode Monitor Set Belongs

**Recommendation: two new sibling fields on `AppSettings`, not a rename of the existing pair.**

```csharp
public sealed class AppSettings
{
    // ... existing fields unchanged ...

    // EXISTING — becomes, in effect, "the Rig-mode target set." Do not rename:
    // renaming forces a migration-guard rewrite (JsonSettingsStore.cs lines 39-58
    // key off these exact field names) for zero behavioral benefit.
    public List<string>? MonitorsToDisable { get; set; }
    public List<string>? MonitorsToEnable { get; set; }

    // NEW — the Normal-mode target set, parallel in shape and semantics.
    public List<string>? NormalMonitorsToDisable { get; set; }
    public List<string>? NormalMonitorsToEnable { get; set; }
}
```

Rationale:
- **Symmetry with the existing, already-proven pattern.** `ToggleService.ToggleToRigMode`'s Monitor step is already exactly `ActivateMonitors(enableSet); DeactivateMonitors(disableSet);` sourced from `MonitorsToEnable`/`MonitorsToDisable`. The milestone context explicitly asks for Normal mode to become "parallel to how Rig mode already applies its configured disable-set" — two new fields with the same `List<string>?` shape, consumed by the exact same `IMonitorController.ActivateMonitors`/`DeactivateMonitors` primitives, is the literal parallel, not a new data shape or a new interface method.
- **No new `IMonitorController` surface needed.** `ActivateMonitors`/`DeactivateMonitors` already take an arbitrary `IReadOnlySet<string>` — they have no idea whether the caller is "Rig mode," "Normal mode," or the new manual panel. This is the single biggest simplification v2.0 gets almost for free: the Windows-adapter layer requires **zero changes** to support the redesign.
- **Renaming `MonitorsToDisable`/`MonitorsToEnable` to `RigMonitorsToDisable`/`RigMonitorsToEnable` was considered and rejected** for this recommendation: it's more "correct-looking" but touches `JsonSettingsStore`'s migration guard (which specifically checks `loaded.MonitorsToDisable is null`, per the CR-01 fix documented inline — see D-08/CR-01 comments), every existing test (`ToggleServiceTests`, `JsonStoreTests`, `ToggleOrchestratorTests` all construct `AppSettings` with these exact field names), and `SettingsForm`'s existing grid-binding code, for a purely cosmetic gain. If the team wants the rename for clarity, do it as an isolated, mechanical, test-covered rename commit — not bundled into this redesign.
- **Null-vs-empty semantics carry over unchanged.** Consistent with the existing convention (`AppSettings`'s own doc comment: "a null field means never configured... not stale"), `NormalMonitorsToDisable == null` means "not yet configured," `NormalMonitorsToDisable == []` means "deliberately configured as empty" (identical to how `MonitorsToDisable` already works, including the CR-01 lesson about not conflating null with empty in a migration guard).

## Mode Tracking: `ISnapshotStore` → `IModeStore` (repurposed, not simply deleted)

This is the load-bearing architectural consequence of feature 3 that the milestone context does not spell out directly, and it must be solved for the redesign to work at all.

**The problem:** `ToggleOrchestrator.IsInRigMode()` → `ToggleService.IsInRigMode()` → `_snapshotStore.Exists()` (D-14, `ToggleService.cs` line 380). Every mode-dependent UI decision in the app — `MainForm.RefreshUi()`'s label/button text/tray icon/tooltip, the tray and hotkey toggle handlers' branch between `ToggleToRigMode()`/`ToggleToNormalMode()`, `BtnToggle_Click`'s branch — ultimately reads this one boolean. It currently works because "a state snapshot exists on disk" and "we are in Rig mode" happen to be the same fact. Once Normal mode stops depending on that snapshot for its own restore logic, snapshot-file-presence stops being a meaningful proxy for "which mode are we in" — nothing is left updating or consulting it for that purpose.

**Recommendation: repurpose `ISnapshotStore`/`JsonSnapshotStore` into a minimal mode marker, keeping the exact same file-presence idiom.**

```csharp
// RigToggle.Core/Abstractions/IModeStore.cs (was ISnapshotStore.cs)
public interface IModeStore
{
    bool IsInRigMode();      // was Exists()
    void SetRigMode();       // was Save(StateSnapshot) — no payload needed anymore
    void SetNormalMode();    // was Clear()
}
```

- Keeps `JsonSnapshotStore`'s already-correct atomic temp-file + `File.Move(..., overwrite: true)` write pattern (crash-safe writes, D-08-equivalent for this new marker) — this is genuinely reusable machinery, not boilerplate to throw away.
- Keeps the crash-recovery property D-14 was designed for: if the process dies mid-toggle or is killed by Task Manager, the next startup still reads the correct last-known mode from disk, because the marker file's presence (not an in-memory flag) is authoritative — exactly the property `ToggleOrchestratorTests`/`MainForm`'s doc comments already rely on ("correct on startup even after a crash while in Rig mode").
- Every existing call site (`ToggleOrchestrator.IsInRigMode()`, `MainForm.RefreshUi()`, the tray/hotkey handlers, `BtnToggle_Click`) needs **zero changes** beyond the method name if kept identical, or a one-line rename — none of them need to know mode tracking changed shape underneath.
- **When to flip the flag** (mirrors today's exact ordering, just swapping "snapshot save/clear" for "flag set/clear"): `SetRigMode()` should fire after the Monitor step of `ToggleToRigMode` succeeds (not before, since a failed Monitor step today means Audio/App are never attempted and nothing really changed — flipping the mode indicator before confirming the mutation actually happened would misrepresent state, unlike the old design where the snapshot had to be saved before mutation for restore-safety reasons that no longer apply once there's no restore). `SetNormalMode()` should fire at the same point `_snapshotStore.Clear()` fires today in `ToggleToNormalMode` — after the Monitor+Audio+App steps have all been attempted, but only reached if Monitor didn't fail (preserving the existing D-05 "no further recovery, snapshot/flag survives a failed monitor restore" invariant, now read as "mode stays Rig so a retry has an accurate starting point").

**Fate of the rest of the old snapshot subsystem — the direct answer to "partially or fully dead":**

| Type/Method | Fate | Why |
|---|---|---|
| `ISnapshotStore`/`JsonSnapshotStore` | **Repurposed** into `IModeStore`/`JsonModeStore` (see above) | Its file-presence-as-flag mechanism is exactly what mode tracking still needs |
| `StateSnapshot` record | **Dead** | Nothing constructs or reads the combined Monitor+Audio payload anymore once neither side restores from it |
| `MonitorState`, `MonitorPathSnapshot` (as a *restore* payload) | **Dead as restore input.** `MonitorState` itself (the return type of `IMonitorController.CaptureState()`) may still have a legitimate use — see note below | `IMonitorController.Restore(MonitorState)` has no caller once `ToggleToNormalMode` stops calling it |
| `IMonitorController.Restore(MonitorState)` / `WindowsMonitorController.Restore()` + `RestoreViaReconstruction()` (~260 LOC, the single largest method in the codebase) | **Dead — the single biggest cleanup-pass target (feature 7)** | No remaining caller anywhere in `RigToggle.App`/`RigToggle.Core` once `ToggleService` stops calling it |
| `IMonitorController.CaptureState()` / `WindowsMonitorController.CaptureState()` | **Recommend keeping**, but its only remaining caller becomes the existing `ToggleToRigMode` CR-01 fix (re-capture-and-compare to decide whether a failed Disable actually mutated anything) — worth re-examining during cleanup whether that fix still needs full-topology capture or can be simplified now that its downstream consumer (the snapshot) no longer exists in the same form | Still structurally useful, but its *purpose* narrows — flag for the cleanup-pass phase to look at, not a clear-cut keep-as-is |
| `AudioState`, `AudioRoleState` (as a *restore* payload) | **Dead only if** the audio redesign below is adopted; **otherwise stays fully alive** | See next section |
| `IAudioController.Restore(AudioState)` / `WindowsAudioController.Restore()` | **Dead only if** the audio redesign below is adopted | See next section |

## Audio: Does It Also Need to Drop Snapshot-Restore? (MEDIUM-confidence recommendation, not a stated requirement)

The milestone's literal feature list scopes feature 3 ("explicit configured set replacing snapshot-restore") to the **monitor** step only; feature 2 ("audio devices become optional per role") only says skip-when-unset, which is a separate concern from restore-vs-configured-target. Taken completely literally, `ToggleToNormalMode`'s audio step could keep calling `_audioController.Restore(snapshot.Audio)` unchanged, and only the monitor half of the redesign would apply.

However, three pieces of direct evidence in the existing codebase point toward extending the same redesign to audio, and this research recommends doing so:

1. **`AppSettings.NormalAudioDeviceId` already exists and is already collected in `SettingsForm`** (bound at `SettingsForm.cs` line 530, saved at line 815) **but is never read by `ToggleService` at all** — grep confirms its only runtime consumers are `ToggleService.IsFullyConfigured`'s validation check (line 203) and test setup. This is a latent, already-scaffolded field with no wired behavior — exactly the shape of something "meant to be used" that the original v1.0/v1.1 design left for later.
2. **Symmetry.** `ToggleToRigMode`'s audio step is already `_audioController.SetDefault(settings.RigAudioDeviceId!)` — a configured-target call, not a restore. If Normal mode's monitor step becomes configured-target too, leaving only audio's Normal-mode step on the old restore mechanism produces a genuinely asymmetric design (monitor: configured both directions; audio: configured one direction, restored the other) that's harder to reason about and harder to explain in the Settings UI ("why does the Normal-mode monitor grid represent my target state, but the Normal-mode audio dropdown is just a label that's actually ignored at toggle-time?" — which is literally true today).
3. **`PROJECT.md`'s own milestone framing** states plainly: "Normal mode moves from snapshot-restore to an explicitly configured target state, matching how Rig mode already works" — worded in terms of "Normal mode" broadly (the Core Value being revised references both monitor *and* audio), not scoped to "Normal mode's monitor step."

**If adopted:** `ToggleToNormalMode`'s Audio step becomes `_audioController.SetDefault(settings.NormalAudioDeviceId!)` (skipped/`NotAttempted` when null, mirroring the Rig-mode audio step's new optional-skip behavior from feature 2), replacing `_audioController.Restore(snapshot.Audio)` entirely. This makes `AudioState`/`AudioRoleState`-as-restore-payload, `IAudioController.Restore`, `IAudioController.CaptureState`, and `WindowsAudioController.Restore`/`CaptureState` **fully dead**, and `StateSnapshot`/`ISnapshotStore` (fully, not just the monitor half) become dead-code-turned-mode-marker as described above — the cleanest possible outcome, and the one that makes the cleanup pass (feature 7) maximally effective.

One real behavior change this implies, worth surfacing explicitly rather than burying: today's audio restore is **per-role-precise** (independently restores whatever was actually default for Console/Multimedia/Communications before the toggle, which might differ from each other on a machine with unusual audio routing). Once Normal mode uses `SetDefault(NormalAudioDeviceId)`, it — like Rig mode already does — applies **one configured device to all three roles uniformly**, losing that per-role precision. This is a deliberate, in-scope consequence of the milestone's own stated Core Value revision, not an accidental regression, but it should be a conscious call during planning, not something that's discovered mid-implementation.

**If not adopted** (the conservative reading — only the monitor step changes): `ToggleToNormalMode` still needs *some* snapshot capture before mutation, since audio restore still needs `AudioState` — meaning `ToggleToRigMode` would keep calling `_snapshotStore.Save(new StateSnapshot(monitorState, audioState))`, but `MonitorState` inside that payload would be write-only (captured, persisted, never read). Mode tracking still cannot use "does a snapshot exist" as the mode proxy either way, for the same reason described above — even in the conservative reading, mode-tracking needs the `IModeStore` redesign, because a snapshot could legitimately exist (for audio-restore purposes) independent of which mode is actually configured/active. **This is the one part of the mode-tracking redesign that is not optional under either interpretation of the audio question.**

## Manual Monitor Panel: Placement and Concurrency

### Where it lives

**Recommendation: a new non-modal `MonitorPanelForm` in `RigToggle.App`, opened via a new button on `MainForm`** (e.g. "Monitors…", alongside the existing "Settings" button), not a panel embedded directly in `MainForm` and not a section inside `SettingsForm`.

- **Not inside `SettingsForm`:** `SettingsForm` is a modal dialog whose entire existing model is "edit a working copy of `AppSettings`, then Save or Discard" (`_settings` field, `ValidateSettingsForm`, `btnSaveSettings`/`btnDiscardChanges`). A live, immediate-effect "click this row to actually disable that monitor right now" action does not fit that deferred-edit model at all — mixing "configure what Rig/Normal mode will do next time" with "mutate my display topology right now" in the same modal risks a user accidentally live-toggling a monitor while they think they're just editing a checkbox, and complicates `SettingsForm`'s own Save/Discard semantics for no benefit.
- **Not embedded directly in `MainForm`:** `MainForm`'s existing responsibilities (mode indicator, Toggle button, tray/hotkey dispatch, `InitializeTrayState`'s carefully-sequenced `--tray`-safe startup path) are already dense and well-documented; bolting a multi-row monitor grid with per-row action buttons and status icons onto that same form's layout is a larger, riskier surface-area change than a new, focused form. A separate form also lets the panel stay open (non-modal) while the user alt-tabs into a game to check monitor state mid-session, which a modal `SettingsForm`-style dialog cannot do.
- **Non-modal, not modeless-blocking:** `ShowDialog()` (as `SettingsForm` uses) would block `MainForm`, defeating "independent of the Rig/Normal toggle" — use `Show()` (non-modal) instead, with `MainForm` holding a nullable reference and reusing/activating the existing instance on repeat clicks (mirrors the existing `NotifyIcon_MouseClick`'s `Show(); WindowState = Normal; Activate();` restore idiom already in `MainForm.cs`).
- Constructed via the same composition-root factory pattern as `SettingsForm` (`Program.cs`'s `SettingsFormFactory` local function) — a `MonitorPanelForm` factory or lazy singleton constructed in `Program.cs`, injected with `IMonitorController`, the new `ManualMonitorService`, and `IThemeProvider` (for consistency with the live-theme-follow work already applied to every other form in this app).

### Concurrency: one shared guard, not two independent ones

The milestone context explicitly flags that the panel "isn't gated by `ToggleOrchestrator`'s toggle-specific reentrancy flow" but "needs its own safe-concurrency story if a toggle is in progress at the same time." Two genuinely independent `Interlocked`-based busy-flags (one inside `ToggleOrchestrator`, a separate new one inside a hypothetical standalone panel service) would each correctly prevent *same-source* reentrancy but **cannot prevent a cross-source race**: the toggle could see "panel idle" and the panel could see "toggle idle" at the same instant, and both proceed to mutate the same CCD display topology concurrently — likely producing exactly the kind of "verify-and-throw" failures `WindowsMonitorController` is full of, or worse, a genuinely inconsistent topology neither side expected.

**Recommendation:** extract `ToggleOrchestrator`'s existing `Interlocked.CompareExchange`-based busy-flag (currently private state inside `ToggleOrchestrator`, `ToggleOrchestrator.cs` lines 39/58-76) into a small standalone `SingleFlightGuard` class in `RigToggle.Core`, constructed **once** in `Program.cs`, and inject the **same instance** into both `ToggleOrchestrator` and the new `ManualMonitorService`. `ToggleOrchestrator`'s existing public shape, tests, and D-01/D-02 documented semantics (non-blocking, immediate rejection, no queueing, one shared flag across both toggle directions) are fully preserved — this is a pure internal delegation refactor, verifiable by the fact that all 4 existing `ToggleOrchestratorTests` reentrancy tests should pass unchanged against the refactored version. `ManualMonitorService.Activate`/`Deactivate` wrap their `IMonitorController` calls in the same guard, throwing the same (or a sibling) `*InProgressException` the panel's UI catches and surfaces (e.g., disable the row's buttons briefly, or a small inline message) — no new concurrency primitive, no new failure mode to design from scratch.

This satisfies the milestone's "own safe-concurrency story" requirement while still guaranteeing mutual exclusion with the toggle pipeline — which is the actually load-bearing requirement, since both paths mutate the same non-transactional OS resource (CCD display topology) with no OS-level locking of their own.

### Safety constraint (feature 5): already solved, reuse don't rebuild

`WindowsMonitorController.DeactivateMonitors()` **already** throws `InvalidOperationException("Cannot disable all configured monitors — at least one active display must remain...")` whenever the survivor set (currently-active paths minus the requested disable targets) would be empty (see the `survivors.Length == 0` guard, `WindowsMonitorController.cs` lines 295-309). Because both the toggle path (`ToggleService` → `IMonitorController.DeactivateMonitors`) and the recommended manual-panel path (`ManualMonitorService.Deactivate` → the same `IMonitorController.DeactivateMonitors`) route through this exact method with the live, currently-active topology as the baseline, **the safety constraint is already enforced from a single shared validation point with zero new code**, provided the panel's Disable action is implemented as a call to `IMonitorController.DeactivateMonitors(new HashSet<string> { devicePath })` and not as a new, separate P/Invoke or CCD call. This is the cleanest possible resolution of the quality gate's "ideally from one shared validation point, not duplicated logic" requirement — the shared point already exists; the job is to route the new caller through it, not to build a new one.

## Recommended Project Structure

```
src/
├── RigToggle.Core/
│   ├── Abstractions/
│   │   ├── IModeStore.cs                  # MODIFIED (renamed/reshaped from ISnapshotStore.cs)
│   │   └── ... (IMonitorController, IAudioController, IAppController, ISettingsStore — UNCHANGED)
│   ├── Models/
│   │   ├── AppSettings.cs                 # MODIFIED — + NormalMonitorsToDisable/NormalMonitorsToEnable
│   │   ├── MonitorState.cs                # UNCHANGED shape; usage narrows (see Data Model section)
│   │   ├── StateSnapshot.cs               # DEAD (delete) once IModeStore fully replaces it
│   │   └── AudioState.cs / AudioRoleState.cs   # DEAD if audio redesign adopted, else UNCHANGED
│   ├── Persistence/
│   │   ├── JsonModeStore.cs               # MODIFIED (renamed/reshaped from JsonSnapshotStore.cs)
│   │   └── JsonSettingsStore.cs           # MODIFIED — new migration-guard branch, no-op-safe for null NormalMonitors*
│   ├── SingleFlightGuard.cs               # NEW — extracted busy-flag primitive
│   ├── ManualMonitorService.cs            # NEW — thin guarded wrapper over IMonitorController.Activate/DeactivateMonitors
│   ├── ToggleOrchestrator.cs              # MODIFIED — delegates to injected SingleFlightGuard
│   └── ToggleService.cs                   # MODIFIED — symmetric Normal-mode Monitor step, optional-skip Audio/App steps, IModeStore flips
│
├── RigToggle.Windows/
│   ├── WindowsMonitorController.cs        # MODIFIED — Restore()/RestoreViaReconstruction() deleted (dead); ActivateMonitors/DeactivateMonitors UNCHANGED
│   └── WindowsAudioController.cs          # MODIFIED (Restore/CaptureState deleted) or UNCHANGED, per Audio section above
│
├── RigToggle.App/
│   ├── Program.cs                         # MODIFIED — construct SingleFlightGuard once, inject into ToggleOrchestrator + ManualMonitorService; construct MonitorPanelForm factory
│   ├── MainForm.cs                        # MODIFIED (additive) — new "Monitors…" button, lazy MonitorPanelForm show/activate
│   ├── MonitorPanelForm.cs (+.Designer.cs)# NEW — non-modal live per-monitor enable/disable panel
│   └── SettingsForm.cs (+.Designer.cs)    # MODIFIED — second Normal-mode monitor-target section; relaxed validation for optional App/Audio fields
│
└── RigToggle.Tests/
    ├── Doubles/
    │   ├── InMemoryStores.cs              # MODIFIED — InMemorySnapshotStore → InMemoryModeStore (drop payload)
    │   └── FakeControllers.cs             # MODIFIED — FakeMonitorController.Restore/FakeAudioController.Restore removed if dead
    └── (new tests) SingleFlightGuardTests.cs, ManualMonitorServiceTests.cs,
        extended ToggleServiceTests.cs (Normal-mode symmetric-set assertions, optional-skip assertions)
```

## Architectural Patterns

### Pattern 1: Symmetric per-mode target application (extends an existing pattern, does not introduce a new one)

**What:** Both `ToggleToRigMode` and `ToggleToNormalMode`'s Monitor step become the identical two-call shape — `ActivateMonitors(enableSet); DeactivateMonitors(disableSet);` — differing only in which `AppSettings` fields supply the sets. Audio, if the redesign above is adopted, becomes the same symmetric shape for `SetDefault`.
**When to use:** Any future third "mode" (unlikely here, but this is the generalizable shape) would slot in the same way — configured-target application is now the ONE pattern for both directions, not two different mechanisms.
**Trade-offs:** Gains simplicity and testability (one code shape asserted twice in tests instead of two different shapes) at the cost of the per-role audio restore precision noted above, and at the cost of losing "restores exactly what was there before" for any monitor arrangement the user didn't explicitly configure (an accepted, milestone-stated trade-off).

**Example:**
```csharp
// ToggleService.ToggleToNormalMode's new Monitor step — literally mirrors
// ToggleToRigMode's existing Monitor step shape (same ordering rule applies:
// ActivateMonitors before DeactivateMonitors, per the existing Pitfall-2 CCD
// persistence-database ordering constraint, unchanged from today).
var normalDisableSet = (settings.NormalMonitorsToDisable ?? new List<string>()).ToHashSet();
var normalEnableSet = (settings.NormalMonitorsToEnable ?? new List<string>()).ToHashSet();

TryExecuteStep("Monitor", () =>
{
    _monitorController.ActivateMonitors(normalEnableSet);
    _monitorController.DeactivateMonitors(normalDisableSet);
}, steps);
```

### Pattern 2: Repurposed persistence primitive for a narrower payload (mode marker, not full snapshot)

**What:** Reuse `JsonSnapshotStore`'s atomic temp-file-then-`File.Move` write mechanism for a much smaller payload (or none at all — presence alone can still be the signal). See Mode Tracking section above.
**When to use:** Whenever an existing persistence adapter's *mechanism* (atomic write, crash-safety) is still needed but its *payload* is being retired — cheaper and lower-risk than writing a new persistence class from scratch, and keeps one atomic-write idiom in the codebase instead of two slightly different ones.
**Trade-offs:** The renamed type (`IModeStore` vs. `ISnapshotStore`) changes call-site vocabulary everywhere it's referenced — a mechanical but real diff across `Program.cs`, `ToggleService.cs`, and every test double. Worth doing as an explicit, reviewable rename rather than leaving `ISnapshotStore`'s name in place while its meaning quietly changes underneath it (which would actively mislead the next reader).

### Pattern 3: Shared single-flight guard across two independent entry points

**What:** One `SingleFlightGuard` instance, constructed once in the composition root, injected into both `ToggleOrchestrator` and `ManualMonitorService`. See Manual Monitor Panel section above for the full rationale.
**When to use:** Any time two independent UI-triggerable code paths mutate the same non-transactional, externally-observable OS resource (here: CCD display topology) and neither path can tolerate the other running concurrently.
**Trade-offs:** Slightly reduces panel responsiveness during an in-flight toggle (the panel's actions are rejected, not queued — same non-blocking philosophy as the existing toggle guard, D-01) — acceptable and consistent with this codebase's established "reject immediately, never silently queue or block" stance.

## Data Flow

### `ToggleToRigMode`, v2.0 (Monitor step unchanged in shape; App/Audio steps gain optional-skip)

```
BtnToggle_Click / TrayToggleMenuItem_Click / HandleHotkeyToggle
    │
    ▼
ToggleOrchestrator.ToggleToRigMode()  ── guarded by SingleFlightGuard (shared with ManualMonitorService)
    │
    ▼
ToggleService.ToggleToRigMode()
    │
    ├─ preflight: IsFullyConfigured() — now requires only the fields actually
    │   being used (monitor disable/enable-set requirement unchanged; App path
    │   and per-mode audio device requirements become conditional, not absolute)
    │
    ├─ Monitor step (unchanged shape): ActivateMonitors(enableSet); DeactivateMonitors(disableSet);
    │       └─ on success: IModeStore.SetRigMode()  (NEW — replaces the old
    │                        pre-mutation _snapshotStore.Save(...) call entirely;
    │                        no capture-before-mutate step remains for monitor)
    │
    ├─ Audio step (MODIFIED): if settings.RigAudioDeviceId is null → step recorded
    │       NotAttempted, skipped entirely (feature 2). Otherwise unchanged
    │       SetDefault(settings.RigAudioDeviceId) call.
    │
    └─ App step (MODIFIED): if settings.CompanionAppPath is null → step recorded
            NotAttempted, skipped entirely (feature 1). Otherwise unchanged
            LaunchOrFocus(settings.CompanionAppPath) call.
```

### `ToggleToNormalMode`, v2.0 (Monitor+Audio steps rewritten to configured-target; App step gains optional-skip; isolate-and-continue policy unchanged)

```
ToggleService.ToggleToNormalMode()
    │
    ├─ Monitor step (REWRITTEN): ActivateMonitors(normalEnableSet); DeactivateMonitors(normalDisableSet);
    │       (was: _monitorController.Restore(snapshot.Monitor) + a separate
    │        unconditional DeactivateMonitors(enableSet) call for D-02's old asymmetry —
    │        that asymmetry disappears entirely: both directions now use the identical
    │        two-call shape, D-02's special-case comment in ToggleService.cs can be deleted)
    │       └─ isolate-and-continue unchanged: failure recorded, subsequent steps
    │            still attempted per existing D-05 policy — EXCEPT the mode flag
    │            is NOT flipped to Normal if this step failed (mirrors today's
    │            "snapshot survives a failed monitor restore" invariant)
    │
    ├─ Audio step (REWRITTEN if audio redesign adopted): SetDefault(settings.NormalAudioDeviceId),
    │       skipped/NotAttempted when null (feature 2's Normal-mode half)
    │       (was: _audioController.Restore(snapshot.Audio))
    │
    ├─ App step (MODIFIED): MinimizeIfRunning skipped/NotAttempted when
    │       settings.CompanionAppPath is null (feature 1) — same
    │       isolate-and-continue try/catch wrapper as today otherwise
    │
    └─ IModeStore.SetNormalMode()  (NEW — replaces _snapshotStore.Clear(),
         same "only if Monitor step didn't fail" gating as today's Clear() call)
```

### Manual panel flow (independent entry point, shares the mutation guard)

```
MonitorPanelForm row action (Enable/Disable click)
    │
    ▼
ManualMonitorService.Activate(devicePath) / Deactivate(devicePath)
    │  guarded by the SAME SingleFlightGuard instance ToggleOrchestrator uses
    │
    ├─ if a toggle is currently in flight → immediate rejection (same
    │    non-blocking, no-queue semantics as ToggleOrchestrator's existing
    │    ToggleInProgressException) → panel surfaces a brief inline message
    │
    └─ otherwise → IMonitorController.ActivateMonitors/DeactivateMonitors
         (UNCHANGED methods — same "at least one active display must remain"
          guard fires here exactly as it does for the toggle path)
         │
         ▼
       MonitorPanelForm refreshes its row's status icon from the call's
       success/failure — no broadcast/event needed since this is a
       synchronous, single-row, user-initiated action
```

## Anti-Patterns

### Anti-Pattern 1: Two independent `Interlocked` busy-flags (one per entry point) instead of one shared guard

**What people do:** Build `ManualMonitorService` with its own private `_busy` field, reasoning "the panel needs its own concurrency story" literally as "its own flag."
**Why it's wrong:** Cannot prevent a genuine cross-path race (toggle and panel both observe "the other side is idle" and proceed concurrently against the same CCD topology) — see Manual Monitor Panel / Concurrency section above.
**Instead:** One `SingleFlightGuard` instance, constructed once in `Program.cs`, injected into both `ToggleOrchestrator` and `ManualMonitorService`.

### Anti-Pattern 2: Trying to auto-derive `NormalMonitorsToDisable`/`NormalMonitorsToEnable` from the retired snapshot mechanism at migration time

**What people do:** On first load of a pre-v2.0 `settings.json`, attempt to seed the new Normal-mode fields from whatever `state.json` snapshot happens to exist at that moment (if the user happens to be mid-rig-session during the upgrade), to avoid forcing a re-configuration step.
**Why it's wrong:** A snapshot's presence/content at migration time is incidental (depends entirely on whether the user happened to be in Rig mode at the moment they upgraded) — it is not a reliable source for "what the user wants Normal mode to look like going forward," and building migration logic that depends on it re-couples the new design to the exact mechanism being retired, undermining the whole point of the redesign. It also cannot handle the common case (user upgrades while in Normal mode, when no snapshot exists at all).
**Instead:** Leave `NormalMonitorsToDisable`/`NormalMonitorsToEnable` null after migration (matches the existing null-means-unconfigured convention) and let `IsFullyConfigured()` correctly block Rig-mode toggling until the user configures Normal-mode targets once via the new `SettingsForm` section — the same "fail closed, not silently wrong" posture `IsFullyConfigured` already takes for every other required field (WR-01's existing rationale, `ToggleService.cs` lines 60-68).

### Anti-Pattern 3: Skipping a step by silently omitting it from `ToggleResult.Steps` instead of recording `NotAttempted`

**What people do:** When `settings.CompanionAppPath`/`settings.RigAudioDeviceId`/`settings.NormalAudioDeviceId` is null, simply don't add anything to the `steps` list for that step name, reasoning "nothing happened, so nothing to report."
**Why it's wrong:** `ToggleResultFormatter.FormatChecklist` and every UI consumer of `ToggleResult` already assume a consistent, predictable step shape (the existing D-04 stop-on-first-failure path already explicitly appends `NotAttempted` entries for this exact reason — see `ToggleService.cs` lines 108-109, 142). Silently omitting a step produces an inconsistent checklist shape depending on configuration, and loses the (useful, intentional) distinction between "this step ran and failed" / "this step never got a chance to run because an earlier step failed" / "this step was never configured, by design" — three genuinely different states a user benefits from being able to tell apart.
**Instead:** Always append a `ToggleStepResult` with `ToggleStepOutcome.NotAttempted` and a `null` reason when a step is skipped due to being unconfigured — same pattern already used for the stop-on-first-failure NotAttempted entries, just triggered by a different condition (unconfigured vs. blocked-by-an-earlier-failure).

### Anti-Pattern 4: Putting live CCD/P-Invoke calls in `MonitorPanelForm`'s code-behind

**What people do:** Since the panel is "just a live list with buttons," reach directly for `WindowsMonitorController` (or worse, raw `WindowsDisplayAPI` calls) inside the new form, reasoning it's simpler than threading another interface through.
**Why it's wrong:** Breaks the one architectural invariant this codebase enforces without exception across every existing form (`Program.cs`'s own doc comment: "MainForm/SettingsForm never `new` a concrete adapter or store themselves") and — more concretely for this feature — bypasses the shared `SingleFlightGuard`, reintroducing the exact cross-path race Anti-Pattern 1 describes.
**Instead:** `MonitorPanelForm` depends only on `IMonitorController` (for `GetAllMonitors()`/status display) and `ManualMonitorService` (for the guarded Activate/Deactivate actions), both injected via the composition root, exactly like every other form in this app.

## Integration Points

### External Services (OS-level)

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Windows CCD API (`SetDisplayConfig` via `WindowsDisplayAPI`) | `IMonitorController.ActivateMonitors`/`DeactivateMonitors` — **unchanged**, reused as-is by both `ToggleService` and the new `ManualMonitorService` | No new CCD interaction pattern introduced by v2.0 — the redesign is entirely about which caller invokes these two already-proven methods and with which device-path set, not about the CCD mechanism itself. |
| `IPolicyConfig` COM interop (default audio device) | `IAudioController.SetDefault` — unchanged if the audio redesign is adopted; `.Restore`/`.CaptureState` become dead in that case | No new audio interaction pattern either way. |
| `%LocalAppData%\RigToggle\state.json` (or renamed `mode.json`) | Repurposed from full `StateSnapshot` payload to a minimal mode marker, same atomic-write mechanism | See Mode Tracking section. Consider renaming the file itself (`state.json` → `mode.json`) for clarity, since its contents no longer represent "state" in the old sense — a naming-only decision, not an architectural one, but worth doing deliberately rather than leaving a stale filename. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `RigToggle.Core` ↔ `RigToggle.Windows` | `IMonitorController`/`IAudioController`/`IAppController` — **no interface signature changes** | The entire v2.0 redesign is achievable without touching any Core-interface method signature — confirmed directly against the current interfaces (`ActivateMonitors`/`DeactivateMonitors` already take arbitrary sets; `SetDefault` already takes an arbitrary device id). This significantly de-risks the milestone: the highest-uncertainty layer (real CCD/COM interop, rig-hardware-dependent) is untouched. |
| `ToggleOrchestrator` ↔ `ManualMonitorService` | Shared `SingleFlightGuard` instance, injected by the composition root — **new boundary** | The only genuinely new cross-component coordination this milestone introduces; everything else is either additive (optional-skip guards) or a like-for-like swap (Restore call → configured-target call). |
| `RigToggle.App`'s composition root ↔ `MonitorPanelForm` | Constructor injection (`IMonitorController`, `ManualMonitorService`, `IThemeProvider`), same pattern as every existing form | No precedent break — follows `SettingsForm`'s/`MonitorConfirmDialog`'s existing injection pattern exactly. |
| Exe-size reduction (feature 6) ↔ everything else in this document | **None** — publish/MSBuild configuration only | Explicitly out of scope for this architecture research per the milestone's own framing ("light touch here, main coverage is in Stack research"); it has no dependency on, or interaction with, any component described above. Safe to schedule independently of the other four features. |

## Suggested Build Order

1. **Optional targets first** (feature 1, and the Rig-mode half of feature 2 — `RigAudioDeviceId`/`CompanionAppPath` becoming skippable in `ToggleToRigMode`). This is the lowest-risk, most self-contained slice: it only adds null-guards around two already-existing steps in `ToggleToRigMode`, requires no data-model additions, no mode-tracking change, and is fully covered by extending the existing `ToggleServiceTests` pattern (hand-written fakes, no new test infrastructure). Shipping this first also immediately delivers real user value (a user with no companion app, or no distinct rig audio device, can already use the tool) independent of everything else.
2. **Monitor-set + mode-store redesign** (feature 3, plus the Normal-mode half of feature 2 if the audio recommendation above is adopted). This is the core, highest-risk, most novel piece: new `AppSettings` fields, the `ISnapshotStore`→`IModeStore` repurposing, `ToggleService.ToggleToNormalMode` rewritten, `SettingsForm` UI extended with a second monitor-target section, the migration-guard decision (Anti-Pattern 2), and (if adopted) the audio-symmetry change. Sequence this **before** the manual panel and the cleanup pass, since both depend on decisions made here (the shared-guard extraction point, and the final shape of what's dead).
3. **Shared concurrency guard extraction + manual live panel** (features 4 + 5). Technically, `ManualMonitorService` could be built against `IMonitorController` directly without waiting for step 2 (the interface doesn't change) — but the `SingleFlightGuard` extraction is far more natural to do as a companion refactor to `ToggleOrchestrator` while that class is already being touched for the `IModeStore` wiring in step 2, rather than extracting it once for the panel and re-touching `ToggleOrchestrator` again later. Building the panel after step 2 also means `MonitorPanelForm`'s status icons can reflect the final, redesigned notion of monitor state without a second UI pass.
4. **Cleanup pass + exe-size reduction last** (features 7 + 6). The cleanup pass should run after step 2 has retired the snapshot/restore subsystem, so it can remove `WindowsMonitorController.Restore()`/`RestoreViaReconstruction()` (~260 LOC), `StateSnapshot`, and (if the audio recommendation is adopted) `AudioState`/`AudioRoleState`-as-restore-payload and `WindowsAudioController.Restore()`/`CaptureState()` in one pass, rather than incrementally. It should also revisit the two already-known, already-documented dead internal methods flagged in `WindowsMonitorController.cs` itself (`CopyOutputTechnology`, `AssignSource` — both explicitly marked "NOT currently called by production code" in their own doc comments, kept only as documented fallback knowledge) and make an explicit keep-or-delete call now that the surrounding method they were extracted from (`Restore`) is itself being deleted. Exe-size reduction (publish/MSBuild config) has no ordering dependency on anything else and can land any time — batching it with the cleanup pass at the end is a scheduling convenience, not an architectural requirement.

## Sources

- Direct source-tree reads (HIGH confidence — the actual current codebase, not documentation): `src/RigToggle.Core/ToggleService.cs`, `ToggleOrchestrator.cs`, `Abstractions/IMonitorController.cs`, `Abstractions/ISnapshotStore.cs`, `Abstractions/IAudioController.cs`, `Abstractions/IAppController.cs`, `Abstractions/ISettingsStore.cs`, `Models/AppSettings.cs`, `Models/AudioState.cs`, `Models/AudioRoleState.cs`, `Models/MonitorState.cs`, `Models/MonitorPathSnapshot.cs`, `Models/StateSnapshot.cs`, `Models/MonitorInfo.cs`, `Models/ToggleResult.cs`, `Models/ToggleStepResult.cs`, `Persistence/JsonSettingsStore.cs`, `Persistence/JsonSnapshotStore.cs`; `src/RigToggle.Windows/WindowsMonitorController.cs`, `WindowsAudioController.cs`, `WindowsAppController.cs`; `src/RigToggle.App/MainForm.cs`, `Program.cs`, `SettingsForm.cs` (partial); `src/RigToggle.Tests/Doubles/FakeControllers.cs`, `Doubles/InMemoryStores.cs`
- `.planning/PROJECT.md` — milestone framing, target features list, Key Decisions table (D-14 mode-derivation, D-02 monitor-restore asymmetry, D-04/D-05 stop-on-first-failure vs. isolate-and-continue policies, CORE-06 reentrancy guard rationale)
- Grep verification across the full `src/` tree confirming `NormalAudioDeviceId` has exactly two runtime call sites (`SettingsForm.cs` binding/save, `ToggleService.IsFullyConfigured` validation) and zero toggle-execution call sites — the direct evidence underpinning the audio-redesign recommendation

---
*Architecture research for: RigToggle v2.0 (Configurable Monitors, Optional Targets & Cleanup) — integration of the five target features into the existing Core/Windows/App/Tests solution*
*Researched: 2026-08-04*
