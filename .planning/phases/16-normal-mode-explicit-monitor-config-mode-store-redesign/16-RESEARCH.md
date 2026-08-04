# Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign - Research

**Researched:** 2026-08-04
**Domain:** Internal architecture redesign of an existing shipped .NET 10 WinForms Windows utility (RigToggle.Core/.Windows/.App) — no new external technology, pure application-logic/data-model rework
**Confidence:** HIGH

## Summary

This phase has no library-selection risk — it is a targeted, well-bounded rewrite of two already-identified hot spots in `ToggleService.cs`: (1) `ToggleToNormalMode`'s Monitor step, which today restores from a pre-toggle snapshot and must instead apply an explicit, symmetric `NormalMonitorsToDisable`/`NormalMonitorsToEnable` set the same way `ToggleToRigMode` already applies its own; and (2) `IsInRigMode()`, which today is literally `_snapshotStore.Exists()` and must become a read from a new, independently-persisted `IModeStore`. A third, smaller addition (`IToggleInProgressStore`, DISPLAY-13) records a lightweight crash marker around every toggle attempt so a mid-flight crash can be detected and reported at the next launch. All three pieces are additive/rewrite work inside `RigToggle.Core`, following exactly the same interface/adapter/composition-root pattern (and the same atomic temp-file-then-`File.Move` JSON persistence idiom) already used by `JsonSettingsStore`/`JsonSnapshotStore` — no new NuGet packages, no new Win32/COM surface, and `IMonitorController`'s existing `ActivateMonitors`/`DeactivateMonitors` methods need zero signature changes.

The genuinely hard part of this phase is not the mechanics of adding two new JSON files — it's three subtle sequencing/timing questions the milestone-level research (PITFALLS.md Pitfalls 4/5) flagged but didn't fully resolve, because they only become concrete once you read the actual current code: (a) how the new mode store bootstraps itself for **existing users upgrading from v1.x**, whose Rig/Normal state today is entirely encoded in whether `state.json` exists — get this wrong and every current Rig-mode user sees a "mode unknown, verify manually" blocking dialog on their very first v2.0 launch; (b) how CR-01's "never let the mode flag misrepresent whether the display was really touched" safety net generalizes now that **both** `ToggleToRigMode` and `ToggleToNormalMode` call the same guarded `DeactivateMonitors` (today only the Rig-mode direction can hit that guard); and (c) exactly when the mode flag should flip relative to the Monitor step's outcome, now that it is a pure UI-truth signal rather than restore payload. This document works through all three with concrete code-location references and a recommended design, flagged clearly as research recommendations (not locked decisions) for the plan to adopt or adjust.

**Primary recommendation:** Add `IModeStore`/`JsonModeStore` (`mode.json`) and `IToggleInProgressStore`/`JsonToggleInProgressStore` (`toggle-in-progress.json`) as new, narrowly-scoped Core abstractions persisted the same way `JsonSnapshotStore` already persists `state.json`; rewrite `ToggleToNormalMode`'s Monitor step to mirror `ToggleToRigMode`'s existing `ActivateMonitors(enableSet); DeactivateMonitors(disableSet);` shape against the new `NormalMonitorsToDisable`/`NormalMonitorsToEnable` `AppSettings` fields; remove `ISnapshotStore` from `ToggleService`'s constructor entirely (its only remaining job — one-time mode-store bootstrap for upgrading users — belongs in the composition root, not in the hot toggle path); and land two new startup-time blocking-dialog checks in `Program.cs`/a small new `RigToggle.App` helper, run before `MainForm` is shown, gated on mode-store corruption (D-06/D-07) and crash-marker presence (D-02/D-03) respectively.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DISPLAY-09 | User can configure which monitors are enabled/disabled specifically for Normal mode, symmetric to Rig mode's existing config | New `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields on `AppSettings` (flat siblings, not nested) + a mirrored `SettingsForm` grid, both detailed below |
| DISPLAY-10 | Toggling to Normal mode applies the explicit Normal-mode monitor set instead of restoring a pre-toggle snapshot | `ToggleToNormalMode`'s Monitor step rewrite (lines 299-450 today), mirroring `ToggleToRigMode`'s existing `ActivateMonitors`/`DeactivateMonitors` shape |
| DISPLAY-11 | App tracks current mode via an explicit persisted flag, independent of snapshot-file presence | New `IModeStore`/`JsonModeStore` replacing `_snapshotStore.Exists()` at `ToggleService.cs:456`; bootstrap/migration strategy for existing installs detailed below |
| DISPLAY-13 | A lightweight "toggle in progress" marker persists across the toggle operation for crash detection | New `IToggleInProgressStore`, written/cleared in `ToggleOrchestrator.RunGuarded` (mirrors the existing busy-flag's own `finally` discipline) |
</phase_requirements>

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01 (Normal-mode monitor set semantics):** A monitor not listed in either the Normal-mode disable-set or enable-set is left untouched on toggle-to-Normal — mirrors Rig mode's existing convention exactly. This must be a documented default, not silent snapshot-fallback behavior.

**D-02 (crash-recovery UX):** When the app detects a "toggle in progress" marker left behind by a crash mid-toggle, it shows a **blocking dialog at startup** stating the last toggle didn't finish cleanly, which mode it was heading to, and that the marker has been cleared.

**D-03 (crash-recovery UX):** The dialog is **inform-only** — no inline "retry the toggle" action. The user manually verifies state and re-toggles if needed.

**D-04 (Settings UI layout):** The new Normal-mode monitor picker is a **second grid stacked directly below** the existing Rig-mode grid in `SettingsForm`, same width, clearly labeled "Normal Mode." Both configs stay visible simultaneously.

**D-05 (Settings UI layout):** The new grid mirrors the Rig grid's column-header + explanation-label convention **exactly** — headers read "Off (Normal)/On (Normal)," with its own permanent explanation label underneath, not a shared explanation.

**D-06 (mode-marker corruption fallback):** If the persisted mode flag is missing or corrupted on launch, the app **fails loudly** — it does not silently default to Normal mode. Matches the corrupted-snapshot precedent and this project's "never silently guess state" discipline.

**D-07 (mode-marker corruption fallback):** This corruption check fires **at app startup**, producing a **blocking dialog** explaining the mode is unknown and asking the user to verify manually before using Toggle.

### Claude's Discretion

- Exact shape of the mode-store abstraction (`IModeStore` interface, its file format, whether it's a new JSON file or repurposes the existing snapshot file's location) — must be file-backed (PITFALLS.md Pitfall 4).
- Whether the "toggle in progress" marker (DISPLAY-13) is a separate file from the mode flag or folded into the same store — both must independently survive a crash.
- Preserving the CR-01 "verify nothing actually changed before trusting the mode flag" safety net when Monitor-step failure handling is rewritten against the new `IModeStore` — the *requirement* is locked, the exact code shape is not.
- Exact wording of the crash-recovery dialog (D-02/D-03) and the mode-corruption dialog (D-06/D-07) — tone must match existing error messages (one clear statement of what's wrong, one instruction on what to do); precise phrasing is not locked.
- Whether `AppSettings` gains flat `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields or a nested `MonitorTarget`-shaped structure reused for both Rig and Normal.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. (Also explicitly out of scope per REQUIREMENTS.md: independent per-direction app-launch opt-in; snapshot-restore as a silent fallback; "smart" migration synthesizing Normal-mode monitor config from the retired snapshot — new fields stay null until an explicit Settings visit.)
</user_constraints>

## Architectural Responsibility Map

This is a single-process desktop app, not a multi-tier web app — "tiers" below map to this project's own established layers (`RigToggle.Core` business logic, `RigToggle.Windows` OS/COM adapters, `RigToggle.App` WinForms UI + composition root, JSON file persistence), not the generic browser/API/CDN framework.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Normal-mode monitor target-set storage (DISPLAY-09) | `RigToggle.Core` (`AppSettings` + `JsonSettingsStore`) | `RigToggle.App` (`SettingsForm` grid) | Data model lives in Core (already the pattern for `MonitorsToDisable`/`ToEnable`); UI is a thin binding layer, never owns persistence |
| Applying the Normal-mode monitor set on toggle (DISPLAY-10) | `RigToggle.Core` (`ToggleService.ToggleToNormalMode`) | `RigToggle.Windows` (`WindowsMonitorController.ActivateMonitors`/`DeactivateMonitors`) | Orchestration/sequencing in Core; actual CCD mutation stays in the Windows adapter — zero interface changes needed |
| Current-mode tracking (DISPLAY-11) | `RigToggle.Core` (new `IModeStore`) | `RigToggle.App` (`MainForm.RefreshUi`, tray/hotkey handlers read it) | Mode is business state, not UI state — Core owns the source of truth; App only renders it |
| Crash-in-progress marker (DISPLAY-13) | `RigToggle.Core` (new `IToggleInProgressStore`, written by `ToggleOrchestrator`) | `RigToggle.App` (`Program.cs` startup check + blocking dialog) | Marker lifecycle is a cross-cutting orchestration concern (same tier as the existing busy-flag guard); the *dialog* is necessarily UI-tier |
| JSON persistence mechanics (atomic write) | `RigToggle.Core.Persistence` | — | Already established pattern (`JsonSettingsStore`/`JsonSnapshotStore`'s temp+`File.Move` idiom) — new stores reuse it verbatim, no new persistence pattern needed |
| Startup mode-corruption / crash-recovery dialogs | `RigToggle.App` (composition root / new startup-check helper) | — | Must run before `MainForm` is meaningfully usable, on both the visible and `--tray` startup paths — same timing constraint `InitializeTrayState()`/`RegisterHotkeyAtStartup()` already satisfy |

## Standard Stack

No new packages for this phase. Confirmed by direct inspection of the existing solution (all four `.csproj` files reference only `WindowsDisplayAPI`, `NAudio`, and BCL/WinForms — see milestone-level `SUMMARY.md`, unchanged by this phase) `[VERIFIED: direct source read]`.

### Core (reused, unchanged)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Text.Json` | Included in .NET 10 BCL | Persist the new `mode.json`/`toggle-in-progress.json` files | Identical pattern to `JsonSettingsStore`/`JsonSnapshotStore` — zero new serialization work, both new record types (`ToggleMode` enum, `ToggleInProgressMarker` record) round-trip with zero custom converters |
| `WindowsDisplayAPI` 1.3.0.13 (via `WindowsMonitorController`) | unchanged | `ActivateMonitors`/`DeactivateMonitors` reused as-is for the Normal-mode Monitor step | Already generalized to arbitrary sets since Phase 6 — zero interface changes needed `[VERIFIED: direct source read, IMonitorController.cs]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Two new small JSON files (`mode.json`, `toggle-in-progress.json`) | Fold both into one file, or repurpose `state.json`'s existing path | Folding both into one file is viable (Claude's Discretion explicitly leaves this open) but couples two independently-lifecycled concerns (mode persists across the app's whole life; the marker exists only during a single in-flight toggle) — separate files keep each store's `Save`/`Clear` semantics simple and match the existing one-concern-per-file convention (`settings.json`, `state.json`). Repurposing `state.json`'s literal path for the new mode marker would be confusing since `ISnapshotStore`/`state.json` still exists through this phase (kept for one-time migration bootstrap, deleted in Phase 18) — a second, unrelated meaning for the same filename is a footgun for anyone reading the persisted directory later. |

**Installation:** None — no `dotnet add package` needed for this phase.

**Version verification:** Not applicable — no package versions change in this phase.

## Package Legitimacy Audit

**Not applicable to this phase.** No new external packages are introduced — the entire phase is implemented with existing dependencies (`System.Text.Json` from the BCL, the already-referenced `WindowsDisplayAPI`/`NAudio` packages via existing adapter classes) `[VERIFIED: direct source read]`. The Package Legitimacy Gate protocol (slopcheck, registry verification) was not run because there is nothing to verify — `dotnet list package` for this phase's diff would show zero additions.

## Architecture Patterns

### System Architecture Diagram

```
STARTUP (Program.cs, before MainForm is shown — both visible and --tray paths)
  settingsStore.Load() ─────────────────────────────────────────────────┐
  snapshotStore = new JsonSnapshotStore(state.json)  [legacy, migration only]
  modeStore = new JsonModeStore(mode.json)                              │
  markerStore = new JsonToggleInProgressStore(toggle-in-progress.json)  │
        │                                                               │
        ▼                                                               │
  modeStore.Exists()? ── no ──► bootstrap: modeStore.Save(               │
        │                         snapshotStore.Exists() ? Rig : Normal)│
        │ yes                                                          │
        ▼                                                               │
  modeStore.TryLoad() ── null (corrupted) ──► [D-06/D-07 BLOCKING DIALOG]
        │ ok                                    "mode unknown, verify manually"
        ▼
  markerStore.TryLoad() ── present ──► [D-02/D-03 BLOCKING DIALOG]
        │ (none)                         "last toggle to <Mode> didn't finish"
        ▼                                 markerStore.Clear()
  construct ToggleService / ToggleOrchestrator (now depends on
  IModeStore + IToggleInProgressStore, NOT ISnapshotStore)
        │
        ▼
  mainForm.InitializeTrayState() / RegisterHotkeyAtStartup() / Application.Run(...)


TOGGLE TRIGGER (BtnToggle_Click / TrayToggleMenuItem_Click / HandleHotkeyToggle)
        │
        ▼
  orchestrator.IsModeKnown()? ── no ──► show reminder, refuse to toggle
        │ yes
        ▼
  ToggleOrchestrator.RunGuarded(targetMode, pipeline)
        │  busy-flag CompareExchange (unchanged, CORE-06)
        │  markerStore.Save(new ToggleInProgressMarker(targetMode, utcNow))
        ▼
  ToggleService.ToggleToRigMode() / ToggleToNormalMode()
        │  captures pre-mutation MonitorState (CR-01-equivalent baseline)
        │  ActivateMonitors(enableSet); DeactivateMonitors(disableSet);
        │  (Rig: settings.MonitorsToDisable/ToEnable — unchanged)
        │  (Normal: settings.NormalMonitorsToDisable/ToEnable — NEW, mirrors Rig)
        │  on Monitor-step failure: recapture + compare against baseline,
        │  decide mode-flag write per the CR-01-preservation logic below
        │  Audio step (unchanged, already symmetric since Phase 15)
        │  App step (unchanged)
        │  modeStore.Save(<new mode>) — ONLY on confirmed Monitor-step success
        ▼
  finally { markerStore.Clear(); busy-flag reset }   ← clears even on managed
        │                                               exceptions; NOT on a
        ▼                                               real process kill/crash
  MainForm.RefreshUi() reads orchestrator.CurrentMode
```

### Recommended Project Structure (additions only — existing structure unchanged)
```
src/RigToggle.Core/
├── Abstractions/
│   ├── IModeStore.cs                  # NEW — Exists()/TryLoad()/Save()
│   └── IToggleInProgressStore.cs      # NEW — TryLoad()/Save()/Clear()
├── Models/
│   ├── ToggleMode.cs                  # NEW — enum { Normal, Rig }
│   └── ToggleInProgressMarker.cs      # NEW — record(ToggleMode TargetMode, DateTimeOffset StartedAtUtc)
├── Persistence/
│   ├── JsonModeStore.cs               # NEW — mirrors JsonSnapshotStore's atomic-write shape
│   └── JsonToggleInProgressStore.cs   # NEW — same shape
└── ToggleService.cs                   # REWRITTEN: ToggleToNormalMode Monitor step,
                                        #   IsInRigMode() → IModeStore-backed, ISnapshotStore
                                        #   dependency REMOVED from constructor

src/RigToggle.App/
├── Program.cs                          # MODIFIED: construct new stores, bootstrap,
│                                        #   run the two startup-check dialogs
├── StartupRecoveryChecker.cs           # NEW (recommended) — the two blocking-dialog checks,
│                                        #   kept out of Program.cs/MainForm for testability
├── SettingsForm.Designer.cs            # MODIFIED: new dgvMonitorsNormal grid + labels,
│                                        #   every control below the Monitor section shifts
│                                        #   down, ClientSize grows
└── SettingsForm.cs                     # MODIFIED: PopulateMonitorGrid/GetGridSelection/
                                         #   ValidateSettingsForm logic duplicated (not shared)
                                         #   for the Normal grid, per existing single-grid
                                         #   precedent style
```

### Pattern 1: Mode-store bootstrap for upgrading installs (critical, not covered by milestone-level research)

**What:** The very first time `mode.json` doesn't exist (a truly fresh install, or an existing v1.x/pre-v2.0 install that has never had this file), seed it from the **legacy** `ISnapshotStore.Exists()` signal — the exact D-14 proxy this phase is retiring — rather than unconditionally defaulting to `Normal`.

**When to use:** Once, in the composition root (`Program.cs`), before `ToggleService`/`ToggleOrchestrator` are constructed.

**Why this matters:** Without this, every existing user who happens to currently be in Rig mode (i.e., `state.json` exists on their machine right now) would launch the v2.0 build for the first time, find `mode.json` missing, and — per the locked D-06 "missing = fail loudly" rule — immediately hit the "mode unknown, verify manually" blocking dialog on a perfectly healthy install. That is a real regression for every current user, not a hypothetical edge case, since the whole point of this milestone is that *today's* users are mid-use of the exact mechanism being replaced.

```csharp
// Program.cs, composition root — after constructing snapshotStore/modeStore/markerStore,
// before constructing ToggleService.
if (!modeStore.Exists())
{
    // One-time migration seed, mirroring the D-14 proxy this phase retires. A fresh
    // install (neither file present) seeds to Normal; an upgrading install carries
    // its current Rig/Normal state forward exactly once.
    modeStore.Save(snapshotStore.Exists() ? ToggleMode.Rig : ToggleMode.Normal);
}
```

**Anti-pattern to avoid:** Do NOT treat "mode.json missing" as unconditionally meaning "first run, default to Normal" — that silently and incorrectly flips every currently-Rig-mode user's tracked state on upgrade, which is exactly the kind of state-flattening bug this milestone's own dominant risk theme (SUMMARY.md: "silent regression through flattening distinct states into one") warns against.

### Pattern 2: Mode flag write timing — after the mutation, not before (revises the old snapshot timing)

**What:** Unlike the retired `_snapshotStore.Save()` call (which ran *before* any mutation, at `ToggleService.cs:83`, specifically to guarantee restore-payload data existed before a risky mutation), the new mode flag should be written **after** the Monitor step's real outcome is known.

**Why:** PITFALLS.md's own Technical Debt table flags this explicitly: the old timing existed for a reason (crash-recovery data availability) that no longer applies to a pure "UI truth signal." That crash-recovery job is now the *separate* `IToggleInProgressStore` marker (DISPLAY-13), decoupling the two concerns cleanly. Writing mode-after-mutation also means there is no more "written early, then conditionally un-written" dance — CR-01's original code existed specifically to undo an early write; if nothing is written until success is confirmed, most of that undo logic becomes unnecessary for the *simple* case.

**The remaining hard case (why CR-01's reasoning still must be preserved, not deleted):** A Monitor-step failure has two different sub-cases that need different mode-flag treatment:
1. **Pre-mutation guard failure** (e.g. `DeactivateMonitors`'s "at least one active display must remain" check throws *before* any CCD mutation — `WindowsMonitorController.cs:295-309`) — nothing on screen changed. The mode flag must simply **not be written** (stays whatever it already was).
2. **Post-partial-mutation failure** (e.g. `ApplyPathInfos` succeeded but the verify-and-throw at `WindowsMonitorController.cs:342-366` failed) — the physical topology *did* change, but doesn't cleanly match either the old mode's target or the new mode's target. This is the scenario the original CR-01 fix was written for (`ToggleServiceTests.cs`'s `ToggleToRigMode_KeepsSnapshot_WhenDisableThrowsAfterPartiallyMutating` test, lines 172-188).

Distinguish the two exactly the way CR-01 already does today — recapture `MonitorState` and structurally compare (`MonitorStateUnchanged`, `ToggleService.cs:236-237`) against the state captured before the mutation attempt:

```csharp
// Shared helper — used by BOTH ToggleToRigMode and ToggleToNormalMode now, since both
// directions call the same guarded DeactivateMonitors as of this phase (previously only
// the Rig-mode Disable path could hit this guard at all).
private void ReconcileModeAfterMonitorFailure(Models.MonitorState before)
{
    try
    {
        if (MonitorStateUnchanged(before, _monitorController.CaptureState()))
        {
            return; // nothing actually changed — leave the mode flag exactly as-is
        }

        // Partial mutation: the physical topology no longer cleanly matches either
        // configured target. Recommended default (see Assumptions Log A3): leave the
        // mode flag at its PRIOR value rather than introduce a third "Indeterminate"
        // mode — matches this phase's overall bias toward minimal new UI states, at
        // the cost of the mode label not perfectly reflecting a genuinely-partial
        // physical state. This is a plan-time call, not something to decide silently.
    }
    catch
    {
        // Re-capture failed — can't confirm anything, same fail-safe posture as
        // today's CR-01 catch block (ToggleService.cs:129-134): do nothing, leave the
        // mode flag as-is rather than guess.
    }
}
```

**Critical generalization the milestone-level PITFALLS.md doesn't spell out:** today, this recapture-and-compare logic only exists inside `ToggleToRigMode` (lines 110-134), because only the Rig-mode Disable path can hit `DeactivateMonitors`'s zero-survivors guard. Once `ToggleToNormalMode`'s Monitor step is rewritten to call the *same* `DeactivateMonitors` method against `NormalMonitorsToDisable` (the correct, Pitfall-2-compliant design), **Normal-mode toggles can now also hit this exact guard** — something that was structurally impossible under the old `Restore(snapshot.Monitor)` design (a restore reconstructs a previously-valid topology by definition, so it could never "leave zero monitors active" the way an arbitrary disable-set can). This means the CR-01-preservation logic must be extracted into a shared helper and called from **both** `ToggleToRigMode`'s and `ToggleToNormalMode`'s failure paths — not just ported from one to the other.

### Pattern 3: Startup blocking-dialog sequencing (DISPLAY-13 + D-06/D-07)

**What:** Two independent startup checks, run in `Program.cs` before `Application.Run(...)`, on **both** the visible and `--tray` startup paths (same timing constraint `mainForm.InitializeTrayState()`/`RegisterHotkeyAtStartup()` already satisfy today, `Program.cs:132-143`).

**Order:** Check mode-store corruption (D-06/D-07) **first** — if the mode itself is unknown, that is the more severe condition and its dialog text already tells the user to "verify manually before using Toggle," which subsumes whatever the crash-marker dialog would additionally say. Only check the crash marker if the mode read succeeded (a known-good mode reading is still useful context even when a toggle was left mid-flight). Both dialogs use `MessageBox.Show(null, ...)` (no owner window required — `MainForm` may not be visible yet under `--tray`); this is a synchronous, blocking call safe to make before `Application.Run`.

```csharp
// Program.cs, after settingsStore/snapshotStore/modeStore/markerStore construction
// and the bootstrap step (Pattern 1), before constructing ToggleService:

ToggleMode? currentMode = modeStore.TryLoad();
if (currentMode is null)
{
    MessageBox.Show(
        null,
        "Rig Toggle can't determine whether you're currently in Rig Mode or Normal " +
        "Mode — the saved mode file is missing or unreadable. Please check your " +
        "monitors and audio device manually before using Toggle.",
        "Rig Toggle",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning);
}
else
{
    var marker = markerStore.TryLoad();
    if (marker is not null)
    {
        markerStore.Clear();
        MessageBox.Show(
            null,
            $"Rig Toggle didn't finish its last toggle to {marker.TargetMode} Mode " +
            "cleanly (the app may have crashed or been closed mid-toggle). Please " +
            "check your monitors and audio device manually — no automatic retry has " +
            "been attempted.",
            "Rig Toggle",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
```

**MainForm/trigger-handler guard when mode is unknown:** `ToggleOrchestrator` needs an `IsModeKnown()` (or `CurrentMode` returning `ToggleMode?`) pass-through alongside the existing `IsInRigMode()`. Every toggle-trigger handler (`BtnToggle_Click`, `TrayToggleMenuItem_Click`, `HandleHotkeyToggle` — `MainForm.cs:273`, `574`, `621`) must check this **before** branching into `ToggleToRigMode()`/`ToggleToNormalMode()`, since that branch decision itself requires knowing the current mode. Recommended behavior when unknown: refuse to toggle, re-surface a short reminder (MessageBox for the GUI trigger, balloon tip for tray/hotkey, matching each handler's existing chrome convention), and leave the resolution path as "delete the mode file and restart" (see Assumptions Log A7) — matching the existing corrupted-snapshot precedent's own recovery text (`ToggleService.cs:312-315`: "Fix or delete the corrupted state file before retrying").

### Pattern 4: `ToggleToNormalMode` Monitor-step rewrite (mirrors `ToggleToRigMode` exactly)

The current method (lines 299-450) is built entirely around `_snapshotStore.Load()`/`.Exists()`/`.Clear()` — the snapshot-or-nothing branch (`if (snapshot is null) { ... } else { ... }`, lines 308-412) is the method's *entire* control-flow spine. This is a genuine rewrite, not a patch. The corrupted-snapshot exception (lines 312-316) and the "never was in rig mode, no-op" branch (lines 318-326) both disappear entirely — that responsibility moves to the startup-time mode-corruption check (Pattern 3), not a per-call guard inside `ToggleService`.

```csharp
// NEW shape — mirrors ToggleToRigMode's existing Monitor-step shape (lines 87-102)
public ToggleResult ToggleToNormalMode()
{
    var settings = _settingsStore.Load();
    var steps = new List<ToggleStepResult>();

    var monitorState = _monitorController.CaptureState(); // CR-01-equivalent baseline

    var disableSet = (settings.NormalMonitorsToDisable ?? new List<string>()).ToHashSet();
    var enableSet = (settings.NormalMonitorsToEnable ?? new List<string>()).ToHashSet();

    Exception? monitorFailure = null;
    try
    {
        // Same 06-RESEARCH.md Pitfall 2 ordering constraint as ToggleToRigMode:
        // Activate before Deactivate.
        _monitorController.ActivateMonitors(enableSet);
        _monitorController.DeactivateMonitors(disableSet);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"Normal-mode monitor apply failed: {ex}");
        monitorFailure = ex;
    }

    // Audio step: UNCHANGED from today (already migrated to SetDefault(settings
    // .NormalAudioDeviceId) in Phase 15 — ToggleService.cs:369-397 — this phase does
    // not touch audio at all).

    steps.Add(new ToggleStepResult("Monitor",
        monitorFailure is null ? ToggleStepOutcome.Succeeded : ToggleStepOutcome.Failed,
        monitorFailure?.Message));
    steps.Add(/* Audio step, unchanged shape */);

    if (monitorFailure is null)
    {
        _modeStore.Save(ToggleMode.Normal);
    }
    else
    {
        ReconcileModeAfterMonitorFailure(monitorState); // Pattern 2
        steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
        return new ToggleResult(steps);
    }

    // App step (minimize-if-running): UNCHANGED shape from today (lines 419-443),
    // still isolate-and-continue, still wrapped in try/catch (CR-02).

    return new ToggleResult(steps);
}
```

**Note the D-02 doc comment at `ToggleService.cs:19-23` becomes actively stale and must be removed, not just its code:** it documents the *old*, now-retired asymmetry ("the enable-set is ALWAYS unconditionally re-disabled via DeactivateMonitors — never snapshot-restored... this is also intentional and must not be 'fixed' into snapshot-based symmetry"). Post-rewrite, `ToggleToNormalMode` no longer references `settings.MonitorsToEnable` (Rig's field) at all — it uses `settings.NormalMonitorsToEnable` exclusively, which *is* now explicit, symmetric configuration. A future reader who trusts the old comment without noticing the code changed would be actively misled about a design constraint that no longer exists (the same "stale copy" failure mode PITFALLS.md's Pitfall 7 describes for the error-message text, applied to a doc comment instead).

### Pattern 5: Removing `ISnapshotStore` from `ToggleService`'s dependencies

Once mode is not derived from the snapshot and `ToggleToNormalMode` no longer restores from it, `ToggleService` has no remaining runtime use for `ISnapshotStore` at all — including the `_snapshotStore.Save(new StateSnapshot(...))` call at line 83 inside `ToggleToRigMode`, which today exists purely to give the old `IsInRigMode()`/restore mechanism something to read. Recommended: remove the `ISnapshotStore snapshotStore` constructor parameter from `ToggleService` entirely. The only remaining consumer of `ISnapshotStore`/`JsonSnapshotStore`/`StateSnapshot` becomes the one-time bootstrap read in the composition root (Pattern 1) — a single `.Exists()` call, nothing more.

**Test-suite impact this causes (flag for planning, not a surprise mid-implementation):** every `ToggleServiceTests.CreateService(...)` factory call currently wires an `InMemorySnapshotStore` into `ToggleService`'s constructor (`InMemoryStores.cs:11-33`). Removing the parameter is a mechanical but wide-blast-radius change across the entire `ToggleServiceTests.cs` file (~25 KB, 23 test methods confirmed by direct grep of `public void` — `[VERIFIED: direct source read]`) — every one of them will need `InMemoryModeStore`/`InMemoryToggleInProgressStore` doubles wired in its place. This is expected, contained churn, not a design risk, but should be sized into the plan's task breakdown rather than discovered mid-task.

### Anti-Patterns to Avoid
- **Duplicating the CR-01 recapture-compare logic separately for Rig and Normal** instead of extracting a shared helper (Pattern 2) — the exact kind of "two independent implementations of the same guard" PITFALLS.md's Pitfall 1/2 warn against in the concurrency-guard and zero-monitor contexts; the same discipline applies here.
- **Adding a second, Settings-time "would this leave zero monitors" cross-check between the two independently-edited grids** — PITFALLS.md Pitfall 2 explicitly warns against this; rely on the existing apply-time guard in `WindowsMonitorController.DeactivateMonitors` (already shared once both toggle directions route through it), and treat any Settings-time hint as non-blocking advisory UX only, never a second safety mechanism.
- **Copying `_snapshotStore.Save()`'s pre-mutation write timing verbatim for the new mode flag** — the old timing solved a different problem (guarantee restore data exists before a risky mutation); a pure UI-truth signal has different requirements (Pattern 2).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Atomic JSON file persistence for the two new stores | A new/different write-safety mechanism | The existing temp-file-then-`File.Move(overwrite: true)` idiom already used by `JsonSettingsStore.Save`/`JsonSnapshotStore.Save` | Already proven crash-safe (T-02-CORRUPT) in this exact codebase; copy verbatim, don't reinvent |
| Corrupted-JSON degrade-to-safe-default handling | New try/catch shapes per new store | The existing `catch (JsonException)`/`catch (IOException)` pattern from `JsonSettingsStore.Load()`/`JsonSnapshotStore.Load()` | Same failure modes apply (truncated write, hand-edited file, antivirus lock) — the existing pattern is already correct for this |
| "Would this leave zero monitors active" validation | A second implementation of the survivor-count check for the Normal-mode grid or the Normal-mode toggle path | `WindowsMonitorController.DeactivateMonitors`'s existing `survivors.Length == 0` guard (lines 295-309), reused as-is once `ToggleToNormalMode` routes through the same method | This is Pitfall 2's central warning — a second implementation is exactly the drift risk to avoid |
| Recapture-and-compare "did anything actually change" logic | A new comparison mechanism for the Normal-mode side | `ToggleService.MonitorStateUnchanged` (lines 236-237), already correctly handles the `IReadOnlyList<T>` record-equality trap — reuse via the shared helper in Pattern 2 | Already solved once; the trap (interface-typed record members fall back to reference equality) is non-obvious and already fixed here |

**Key insight:** every mechanism this phase needs (atomic file writes, corrupted-JSON degradation, the zero-monitor safety guard, state-comparison logic) already exists correctly in this codebase for a structurally identical problem. This phase's job is almost entirely "apply the same pattern to a new (but structurally identical) case," not "invent a new pattern" — which is exactly why the risk is concentrated in the sequencing/timing questions above, not in any new mechanism.

## Common Pitfalls

### Pitfall 1: Mode-store bootstrap forgotten — every upgrading user hits the corruption dialog
**What goes wrong:** `mode.json` unconditionally defaults to `Normal` (or worse, isn't seeded at all and `TryLoad()` returns null) on first v2.0 launch, regardless of whether the user's machine currently has `state.json` (i.e., is currently in Rig mode).
**Why it happens:** D-06's "missing = fail loudly" rule is written from the perspective of steady-state operation, not first-launch-after-upgrade — it's easy to implement literally without separately handling the one-time bootstrap case.
**How to avoid:** Implement Pattern 1 exactly — seed from `snapshotStore.Exists()` once, before the corruption check ever runs.
**Warning signs:** A test/manual-verification pass that only exercises "fresh install, no files at all" will not catch this — it specifically requires testing "upgrade while currently in Rig mode" (state.json present, mode.json absent).

### Pitfall 2: CR-01 preservation logic ported to only one of the two toggle directions
**What goes wrong:** The recapture-and-compare safety net (Pattern 2) gets copied into `ToggleToNormalMode` (the "new" code getting the most implementation attention) but the underlying reasoning — mode flag must never misrepresent whether the display was really touched — isn't recognized as now *also* applying to `ToggleToRigMode`'s existing failure path, or vice versa.
**Why it happens:** Today, only `ToggleToRigMode` can hit the zero-survivors guard; a developer focused on "make Normal mode symmetric with Rig mode" may correctly copy Rig's CR-01 logic into Normal but not notice that Rig's own CR-01 logic *also* needs to change (from writing to `_snapshotStore` to writing to `_modeStore`, per Pattern 2's shared-helper design).
**How to avoid:** Extract the shared `ReconcileModeAfterMonitorFailure` helper (Pattern 2) and call it from both methods' failure paths — not two separately-written, potentially-diverging implementations.
**Warning signs:** Any code review that finds two similar-but-not-identical recapture-compare blocks in `ToggleService.cs`.

### Pitfall 3: Stale doc comments and UI text survive the rewrite
**What goes wrong:** Three specific pieces of existing text become actively wrong once this phase ships and nothing forces them to be revisited, since none of them are compile errors: (1) `ToggleService.cs:19-23`'s class-doc paragraph about the "deliberate D-02 asymmetry" (Pattern 4); (2) `SettingsForm.Designer.cs:181`'s `lblMonitorExplain.Text`, whose second sentence — *"Normal Mode is always restored exactly as it was before — nothing to set up separately"* — is now false; (3) the two `colDisable`/`colEnable` tooltip strings (`SettingsForm.Designer.cs:165,173`), which both say "restored automatically when switching back to Normal Mode" / "turned off again automatically when switching back to Normal Mode" — also now false, since Normal mode applies its own explicit set rather than restoring anything.
**Why it happens:** None of these are reachable by a compiler or a typical unit test — they're prose that silently drifts from the code's actual behavior, the same failure class PITFALLS.md's Pitfall 7 describes for the safety-guard exception text (`WindowsMonitorController.cs:307-308`, "...before switching to Rig Mode" — also stale once `DeactivateMonitors` gains a second/third caller from this phase and Phase 17's manual panel).
**How to avoid:** Treat all four of these strings as required edits in this phase's task list, not incidental cleanup — grep for `"Restored automatically"`, `"restored exactly as it was before"`, and `"before switching to Rig Mode"` across `SettingsForm.Designer.cs` and `WindowsMonitorController.cs` as an explicit verification step. Also fix the `WindowsMonitorController.cs:307-308` message itself (generalize away from "before switching to Rig Mode") in this phase — this is the first phase to add a second caller (`ToggleToNormalMode`'s rewritten Monitor step) to the guarded `DeactivateMonitors`, matching PITFALLS.md's own Pitfall-7-to-phase mapping ("fix at the point of the first new caller, before Phase 17's manual panel adds a third").
**Warning signs:** A rig/manual verification pass that only checks toggle *behavior*, never reads the actual Settings dialog tooltips or the actual displayed exception text end-to-end.

### Pitfall 4: `SettingsForm.Designer.cs` layout coordinates not fully re-flowed
**What goes wrong:** The new Normal-mode grid panel is inserted between the existing `pnlMonitor` (Location `(12,12)`, Size `(396,234)`) and `pnlAudioDevices` (Location `(12,258)`), but only `pnlAudioDevices`'s Y-coordinate is adjusted — every control below it (`pnlAppPath` at `(12,402)`, `chkEnableDebugLogging` at `(12,484)`, `lblHotkeyCaption`/`txtHotkey` at `(12,532)`/`(76,529)`, `lblHotkeyWarning` at `(12,556)`, `chkCloseMinimizesToTray`/`chkMinimizeToTray`/`chkStartWithWindows` at `(12,600)`/`(12,632)`/`(12,664)`, `lblAutostartWarning` at `(12,688)`, `btnSaveSettings`/`btnDiscardChanges` at `(180,720)`/`(298,720)`, and the form's own `ClientSize`) is left unmoved, producing an overlapping/broken dialog layout.
**Why it happens:** WinForms Designer.cs is a flat sequence of absolute `Point`/`Size` assignments, not a flow layout — there is no single "insert a panel" operation that cascades; every downstream control's Y-coordinate is an independent literal that must be updated by hand (or the whole file regenerated via the visual designer, not available in a CLI-only workflow).
**How to avoid:** Budget this as its own explicit task with a full list of every control below the insertion point (given above) and its new Y-coordinate (existing Y + new panel's height + existing inter-panel spacing, which is consistently 12px in the current layout, e.g. `258 → 12`, `402 → 258`, gap pattern), plus the `ClientSize.Height` increase by the same delta.
**Warning signs:** A build that compiles fine but a Settings dialog that visually overlaps or clips controls — only catchable by actually opening the dialog (rig/manual verification), not by any automated test.

### Pitfall 5 (carried from PITFALLS.md Pitfall 6, restated with this phase's exact fields): Reintroducing the null-vs-empty migration bug for the new fields
**What goes wrong:** A developer adds default-population logic for `NormalMonitorsToDisable`/`NormalMonitorsToEnable` in `JsonSettingsStore.Load()` (e.g., "if the Normal set is empty, seed it from the currently-active monitor list") using a `Count > 0`-style check.
**Why it happens:** Feels like reasonable UX polish for upgrading users; the specific reason this codebase avoids it (`JsonSettingsStore.cs:47-58`'s CR-01 comment, keyed on `is null` only) is easy to miss if the new fields' logic is written fresh rather than copied.
**How to avoid:** Per REQUIREMENTS.md's own Out-of-Scope table ("no smart migration... leave new fields null"), the safest and *simplest* implementation is to add **zero** new logic to `JsonSettingsStore.Load()` — `AppSettings`'s two new nullable `List<string>?` properties round-trip through `System.Text.Json` with no code changes needed at all. This sidesteps the entire bug class by construction, not by careful vigilance.
**Warning signs:** Any diff to `JsonSettingsStore.cs` at all for this phase should be treated as a red flag requiring justification — the correct diff for this file is empty.

## Code Examples

### `IModeStore` and `JsonModeStore`
```csharp
// Source: sketch, this session — mirrors ISnapshotStore.cs / JsonSnapshotStore.cs exactly
namespace RigToggle.Core.Abstractions;

public interface IModeStore
{
    bool Exists();
    /// <summary>Null if the file is missing OR fails to parse — callers distinguish
    /// "never bootstrapped" (Exists() == false, checked separately at startup) from
    /// "exists but corrupted" (Exists() == true, TryLoad() == null) as needed.</summary>
    ToggleMode? TryLoad();
    void Save(ToggleMode mode);
}
```

```csharp
namespace RigToggle.Core.Persistence;

public sealed class JsonModeStore : IModeStore
{
    private readonly string _path;
    public JsonModeStore(string path) => _path = path;

    public bool Exists() => File.Exists(_path);

    public ToggleMode? TryLoad()
    {
        if (!Exists()) return null;
        try
        {
            return JsonSerializer.Deserialize<ToggleMode>(File.ReadAllText(_path));
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    public void Save(ToggleMode mode)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(mode));
        File.Move(tempPath, _path, overwrite: true);
    }
}
```

`JsonToggleInProgressStore` follows the identical shape, serializing `ToggleInProgressMarker` instead, plus a `Clear()` method mirroring `JsonSnapshotStore.Clear()` (lines 59-65).

### `ToggleOrchestrator` — marker lifecycle alongside the existing busy-flag
```csharp
// Source: sketch, this session — extends ToggleOrchestrator.cs:56-77
private ToggleResult RunGuarded(ToggleMode targetMode, Func<ToggleResult> pipeline)
{
    if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        throw new ToggleInProgressException(
            "A toggle is already in progress. Wait for it to finish, then try again.");

    try
    {
        _markerStore.Save(new ToggleInProgressMarker(targetMode, DateTimeOffset.UtcNow));
        return pipeline();
    }
    finally
    {
        // Clears on any managed exception path (including ToggleService's own
        // preflight throws) — mirrors the existing busy-flag's own finally discipline.
        // Deliberately does NOT clear on an actual process kill/crash — that is
        // precisely the condition DISPLAY-13 exists to detect at next launch.
        _markerStore.Clear();
        Volatile.Write(ref _busy, 0);
    }
}
```

**Naming collision to avoid in the plan:** the existing `ToggleInProgressException` class (`src/RigToggle.Core/ToggleInProgressException.cs`) is an unrelated, already-shipped in-memory reentrancy-guard concept (thrown by `ToggleOrchestrator.RunGuarded` today when a second toggle call arrives while one is already in flight, CORE-06). The new DISPLAY-13 crash marker is a *different* concept (a disk-persisted "a toggle started and didn't finish" record, surviving a process kill). Do not name the new marker type/record `ToggleInProgress*` in a way that reads as the same thing as the exception — `ToggleInProgressMarker`/`IToggleInProgressStore` (as sketched above) are close enough in name to the existing `ToggleInProgressException` that the plan should call out the distinction explicitly in code comments, the same way this document does here.

## State of the Art

| Old Approach | Current Approach (this phase) | When Changed | Impact |
|--------------|-------------------------------|---------------|--------|
| Mode == `_snapshotStore.Exists()` (D-14) | Explicit `IModeStore` flag, decoupled from restore data | This phase | Removes the ambiguity PITFALLS.md Pitfall 4 warns about; `ISnapshotStore`'s only remaining job is a one-time migration-bootstrap read |
| `ToggleToNormalMode` restores via `_monitorController.Restore(snapshot.Monitor)` | Applies `NormalMonitorsToDisable`/`NormalMonitorsToEnable` via `ActivateMonitors`/`DeactivateMonitors`, mirroring Rig's shape | This phase | `WindowsMonitorController.Restore()`/`RestoreViaReconstruction()` (~260 LOC, the largest method in the codebase) become unreachable — deletion is explicitly deferred to Phase 18/CLEANUP-01, not this phase |
| Crash-mid-toggle recovery implicit via snapshot-on-disk presence | Explicit `IToggleInProgressStore` marker, independent of mode/restore data | This phase (DISPLAY-13) | First dedicated crash-recovery UX in the app; previously there was no user-facing signal at all that a toggle had been interrupted |
| CR-01 recapture-compare reachable only from Rig-mode's Disable path | Same pattern needed on **both** Rig and Normal Monitor steps, since both now call the same guarded `DeactivateMonitors` | This phase | Must be extracted into a shared helper (Pattern 2) — this is new, not carried over unchanged |
| `ToggleService` depends on `ISettingsStore`, `ISnapshotStore`, `IMonitorController`, `IAudioController`, `IAppController` | Drops `ISnapshotStore`; gains `IModeStore` | This phase | Constructor signature change ripples through every `ToggleServiceTests` test-double wiring (Pattern 5) |

**Deprecated/outdated (as of this phase):**
- `ISnapshotStore`'s role as the mode-detection mechanism (D-14) — fully retired by DISPLAY-11; the interface itself is not deleted this phase (still used for one-time bootstrap), full removal is Phase 18/CLEANUP-01 scope.
- `_monitorController.Restore(MonitorState)` as a live call site — becomes unreachable in production code once this phase ships, though the method itself is not deleted until Phase 18 (per REQUIREMENTS.md CLEANUP-01's explicit "review before deleting" scope, and PITFALLS.md Pitfall 9's warning about the rig-specific knowledge this method encodes).

## Assumptions Log

All entries below are this session's own design recommendations, not locked CONTEXT.md decisions — they follow directly from the locked decisions and the codebase's existing patterns, but involve product/behavior tradeoffs a human should confirm rather than have silently decided by an implementer.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Mode-store bootstrap should seed from legacy `ISnapshotStore.Exists()` at first-ever launch post-upgrade (one-time), defaulting to `Normal` only when neither file exists | Architecture Patterns, Pattern 1 | Every existing Rig-mode user sees the D-06/D-07 "mode unknown" dialog on their very first v2.0 launch instead of a correct, silent state carry-over |
| A2 | Mode flag should be written only after the Monitor step's outcome is known (post-mutation), not before (unlike the old pre-mutation snapshot-save timing) | Architecture Patterns, Pattern 2 | Could reintroduce the exact bug class CR-01 originally fixed if the partial-mutation fallback isn't implemented correctly |
| A3 | On a partial-mutation Monitor-step failure, the mode flag should conservatively stay at its prior value rather than introduce a third "Indeterminate" mode value | Architecture Patterns, Pattern 2 | Mode label could show a mode that doesn't match the true (partially-mutated) physical display state; the more-correct alternative (an Indeterminate mode) is more invasive across `MainForm`/`ToggleOrchestrator` |
| A4 | Toggling to Normal mode with `NormalMonitorsToDisable`/`ToEnable` both still null (pre-Settings-visit, post-upgrade) should silently no-op the Monitor step rather than block via an `IsFullyConfigured`-style gate | Architecture Patterns | Users could toggle to "Normal" mode believing monitors were reset when nothing happened, with no warning surfaced |
| A5 | `ISnapshotStore` should be removed entirely from `ToggleService`'s constructor, kept only in the composition root for the one-time bootstrap read | Architecture Patterns, Pattern 5 | Unnecessary test-double churn across `ToggleServiceTests.cs` if the plan instead prefers keeping the (now-unused) dependency wired until Phase 18's cleanup |
| A6 | Recommended file names `mode.json` / `toggle-in-progress.json` under the existing `%LocalAppData%\RigToggle\` directory | Recommended Project Structure | Cosmetic only, no functional risk — should just stay consistent with the existing `settings.json`/`state.json` naming convention |
| A7 | Recovery from the mode-corruption dialog (D-06/D-07) is manual file-deletion + restart (matching the existing corrupted-snapshot precedent's guidance text), with no new in-app recovery affordance | Architecture Patterns, Pattern 3; Open Questions | Could leave users without a clear, discoverable recovery path if manual file deletion isn't acceptable UX for this milestone |

**If this table is empty:** N/A — see entries above; all are design recommendations requiring plan/discuss confirmation, not locked facts.

## Open Questions

1. **Toggle-button gating while mode is Unknown**
   - What we know: D-06/D-07 lock the *startup* dialog; they don't specify ongoing behavior afterward.
   - What's unclear: Should `btnToggle` be disabled entirely until the mode file is fixed, or remain clickable and just re-show a short reminder each time (Pattern 3's recommendation)?
   - Recommendation: Re-show a short reminder per click (matches this app's existing "never permanently lock the UI, always allow retry" posture — e.g. the busy-flag rejection is also just a message, not a disabled button) — but confirm at plan time.

2. **Mode-corruption recovery affordance**
   - What we know: The existing corrupted-snapshot precedent's recovery is purely textual ("delete the corrupted state file"), no in-app action.
   - What's unclear: Whether v2.0 should add a lightweight in-app recovery (e.g., a Settings-page "which mode am I actually in right now?" picker that reseeds `IModeStore` directly) instead of requiring manual file deletion.
   - Recommendation: Match the existing precedent (text-only) for this phase's scope; flag an in-app picker as a candidate enhancement, not a requirement, since it's not mentioned in any locked decision.

3. **Advisory (non-blocking) Settings-time warning when both grids independently disable every monitor**
   - What we know: PITFALLS.md Pitfall 2 explicitly forbids this from being the *safety mechanism* (that stays apply-time-only, in `WindowsMonitorController`).
   - What's unclear: Whether an advisory-only hint (informational, non-blocking) is worth adding for UX polish, given the milestone's own SUMMARY.md flags it only as "candidate... roadmap to accept or defer."
   - Recommendation: Treat as optional polish, not required for DISPLAY-09/10 to be considered complete.

## Environment Availability

Skipped — this phase has no external dependencies beyond the already-established .NET 10 SDK / Windows 10-11 runtime that every prior phase already builds and runs against. No new CLI tools, services, or runtimes are introduced (confirmed by the Standard Stack section above: zero new packages).

## Security Domain

`security_enforcement` is not set to `false` in `.planning/config.json` (key absent), so this section is included per the default-enabled rule. This app's actual attack surface is narrow and already documented (`PROJECT.md`'s `T-03-09` TOCTOU stance): a personal, single-user, non-networked Windows utility. The relevant boundary for this phase is local-file trust (the two new JSON stores this phase introduces), not any conventional web/app security category (no auth, no session, no network input).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Single-user local desktop app, no auth boundary — consistent with every prior milestone |
| V3 Session Management | No | No session concept in this app |
| V4 Access Control | No | No multi-principal access-control model — the OS user account is the only trust boundary, unchanged by this phase |
| V5 Input Validation | Yes (narrow) | The two new JSON files (`mode.json`, `toggle-in-progress.json`) are deserialized with `System.Text.Json` into a closed `enum`/`record` shape (no free-form strings, no dynamic typing) — malformed/truncated/hand-edited input degrades via the existing `catch (JsonException)`/`catch (IOException)` pattern (see Don't Hand-Roll) to a well-defined "corrupted" state (`TryLoad()` returns null), never an unhandled crash. This mirrors `JsonSettingsStore`/`JsonSnapshotStore`'s existing, already-proven degrade-safely posture — no new validation logic needed beyond copying that pattern verbatim. |
| V6 Cryptography | No | No cryptographic operations in this phase (unchanged from the rest of the app — the only "secret" material anywhere in this codebase is the Windows-managed audio/display device identifiers, which are not credentials) |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Hand-edited/malformed `mode.json` or `toggle-in-progress.json` causing an unhandled exception at startup (local file tampering — the same local user who can edit this file already has full account-level access to the app and its data, so this is not a privilege-escalation vector, but it is a reliability/DoS-on-self concern) | Tampering (local, low severity) / Denial of Service (local) | The `catch (JsonException)`/`catch (IOException)` degrade-to-null pattern (`JsonModeStore.TryLoad()`, `Code Examples` above) turns any malformed file into the well-defined "corrupted" state that D-06/D-07's blocking dialog already handles by design — never an unhandled crash. This is the *same* mitigation already proven for `settings.json`/`state.json`, applied verbatim to the two new files. |
| TOCTOU between `modeStore.TryLoad()` at startup and the mode actually being read again inside a later toggle (the file could theoretically be edited between the two reads, in a single-user context) | Tampering (theoretical, local-only) | Accepted risk, matching this project's existing documented `T-03-09` posture (the companion-app-path `File.Exists`-then-`Process.Start` TOCTOU window is already an accepted risk for the same reason: single local user, no adversarial multi-principal boundary to defend). No new mitigation needed beyond the existing project-wide acceptance already on record — do not add file-locking or hash-verification machinery, which would be disproportionate to the actual threat model. |
| A crash mid-write to `mode.json`/`toggle-in-progress.json` leaving a corrupted or truncated file on disk (distinct from hand-editing — this is the actual production failure mode DISPLAY-13 partly exists to detect) | Tampering (accidental, not adversarial) / reliability | The existing atomic temp-file-then-`File.Move(overwrite: true)` write idiom (`JsonSettingsStore.Save`/`JsonSnapshotStore.Save`, reused verbatim per Don't Hand-Roll) already prevents an interrupted *write* from corrupting the *previous* good file — a crash during `File.WriteAllText` only ever corrupts the `.tmp` sibling, which is silently overwritten (or ignored) on the next successful save. This is the primary defense; the `TryLoad()` degrade-to-null path is the backstop for any file the write-safety idiom doesn't cover (e.g. a file corrupted by something outside this app's own writes, such as disk-level filesystem corruption). |

## Sources

### Primary (HIGH confidence)
- Direct source-tree reads this session (all current as of 2026-08-04, post-Phase-15): `src/RigToggle.Core/ToggleService.cs` (full file, 457 lines), `src/RigToggle.Core/ToggleOrchestrator.cs`, `src/RigToggle.Core/ToggleInProgressException.cs`, `src/RigToggle.Core/Abstractions/ISnapshotStore.cs`, `IMonitorController.cs`, `ISettingsStore.cs`, `src/RigToggle.Core/Models/AppSettings.cs`, `StateSnapshot.cs`, `MonitorState.cs`, `MonitorPathSnapshot.cs`, `AudioState.cs`, `AudioRoleState.cs`, `ToggleResult.cs`, `ToggleStepOutcome.cs`, `ToggleStepResult.cs`, `src/RigToggle.Core/Persistence/JsonSettingsStore.cs`, `JsonSnapshotStore.cs`, `src/RigToggle.Core/ToggleResultFormatter.cs`, `src/RigToggle.App/MainForm.cs` (full file, 741 lines), `Program.cs` (full file), `SettingsForm.cs` (full file, 985 lines), `SettingsForm.Designer.cs` (grep-targeted plus lines 1-270, all layout coordinates), `src/RigToggle.Windows/WindowsMonitorController.cs` (lines 260-370, `DeactivateMonitors` + verify-and-throw), `src/RigToggle.Tests/ToggleServiceTests.cs` (full file, 532 lines — CR-01 test cases, 23 test methods confirmed via grep), `src/RigToggle.Tests/Doubles/InMemoryStores.cs` (full file), `src/RigToggle.Core/Abstractions/IMonitorController.cs`.
- `.planning/phases/16-.../16-CONTEXT.md` — locked decisions D-01 through D-07, Claude's Discretion, canonical references.
- `.planning/REQUIREMENTS.md` — DISPLAY-09/10/11/13 exact wording, Out-of-Scope table ("no smart migration").
- `.planning/STATE.md`, `.planning/config.json` — confirmed Phase 15 (optional App/Audio targets, including `NormalAudioDeviceId` symmetry) already shipped; confirmed `nyquist_validation: false` (Validation Architecture section omitted per config) and no `security_enforcement` override present (Security Domain section included per the default-enabled rule, tailored to this app's actual local trust boundary).

### Secondary (already-verified milestone-level research, reused not re-derived)
- `.planning/research/SUMMARY.md` §"Phase 2: Normal-Mode Explicit Monitor Config + Mode-Store Redesign" — phase-level rationale, build-order justification.
- `.planning/research/FEATURES.md` §"3. Normal Mode Explicit Monitor Target Set" — table-stakes/anti-features, the mode-detection architectural dependency (source of the core problem this document works through in depth).
- `.planning/research/PITFALLS.md` — Pitfalls 1, 2, 4, 5, 6, 7 and their Phase-to-Pitfall mapping, Technical Debt table (mode-flag timing entry), Recovery Strategies table.

No web sources were consulted for this phase — it involves zero new external technology, and the milestone-level research (already HIGH confidence, verified against this exact codebase) already established that no new library research was needed. This session's contribution is exclusively deeper direct-source analysis beyond what the milestone-level pass captured.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; every reused mechanism verified by direct source read.
- Architecture (bootstrap/timing/CR-01 generalization): HIGH on the problem identification (all grounded in direct reads of the actual current code and its exact line numbers); MEDIUM on the specific recommended resolutions in the Assumptions Log, since those are genuine design choices with more than one defensible answer — flagged explicitly for plan/discuss confirmation rather than presented as settled fact.
- Pitfalls: HIGH — all five pitfalls above are grounded in this session's own direct reads of the current `SettingsForm.Designer.cs` coordinates, `ToggleService.cs` doc comments, and `WindowsMonitorController.cs` guard text, not inference.
- Security: HIGH on ASVS applicability determination (this app's local-only, single-user, non-networked shape is already established project fact, not a new claim); MEDIUM on the specific STRIDE framing of the two new JSON files, since it is this session's own reasoning applied to files that don't exist yet, not a verified-against-an-attack finding.

**Research date:** 2026-08-04
**Valid until:** No expiry driver — this is internal-codebase research, not dependent on external package/API currency. Re-verify only if Phase 15's shipped shape (confirmed read this session) changes before Phase 16 planning begins.
