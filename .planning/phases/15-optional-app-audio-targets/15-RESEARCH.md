# Phase 15: Optional App & Audio Targets - Research

**Researched:** 2026-08-04
**Domain:** C#/.NET 10 WinForms validation-gate relaxation + toggle-result modeling in an existing shipped Windows CCD/COM-interop rig-control app (no new external dependencies)
**Confidence:** HIGH — every finding below is grounded in direct reads of this repo's current source (`ToggleService.cs`, `ToggleResult.cs`/`ToggleStepOutcome.cs`/`ToggleStepResult.cs`, `ToggleResultFormatter.cs`, `WindowsAudioController.cs`, `SettingsForm.cs`/`.Designer.cs`, `MainForm.cs`, `ToggleServiceTests.cs`, `FakeControllers.cs`), the milestone-level v2.0 research already on disk, and one official-docs lookup (WinForms `TextBox.PlaceholderText`). No Context7 lookup was needed — this phase adds zero new packages.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Unset Affordance (Settings UI)**
- **D-01:** The companion-app path field (`txtAppPath`, a read-only textbox today, set only via Browse or drag-drop with no way to clear it) gets an explicit **Clear button** next to it — enabled only when a path is currently set. Matches the existing Browse button's affordance style; explicit and discoverable, not a hidden context-menu action.
- **D-02:** Each audio-device dropdown (`cboAudioNormal`, `cboAudioRig`) gets an explicit **"(None — don't switch audio)"** list item prepended to the real detected-device entries, rather than allowing a blank/`SelectedIndex = -1` state to mean unset. A deliberate list entry reads as an intentional choice; a blank dropdown looks like an unfinished form.

**Skipped-Step Outcome (Toggle Result)**
- **D-03:** `ToggleStepResult`/`ToggleStepOutcome` gains a new, distinct **`Skipped`** outcome — not a reuse of `NotAttempted` with different text. `NotAttempted` continues to mean "blocked because an earlier step in the same toggle failed" (stop-on-first-failure, D-04 in ToggleService.cs); `Skipped` means "the user deliberately left this target unconfigured." These must never look the same to a future reader of the result, matching this codebase's existing "don't collapse two different states into one" discipline (already applied to the App/Audio configured-vs-missing distinction).
- **D-04:** The toggle-result step list always contains all 3 entries (Monitor/Audio/App) on every toggle, regardless of what's configured — an unset step reads "Skipped (not configured)" rather than being omitted from the list. Keeps the checklist shape consistent toggle-to-toggle; whatever downstream formatter/MessageBox/tray-balloon logic currently renders the 3-row checklist should handle `Skipped` as a distinct, non-alarming visual state (not styled like `Failed`).

**Toggle-Readiness & Save Gating**
- **D-05:** `ToggleService.IsFullyConfigured`/`IsSettingsConfigured` (ToggleService.cs:201-205) drops the `NormalAudioDeviceId`/`RigAudioDeviceId`/`CompanionAppPath` required-field terms entirely — only the existing monitor-set check (`MonitorsToDisable?.Count > 0 || MonitorsToEnable?.Count > 0`, D-07) gates whether "Switch to Rig Mode" is enabled at all. Audio/App being unset never blocks toggling in either direction.
- **D-06:** `SettingsForm.ValidateSettingsForm`/`btnSaveSettings.Enabled` (SettingsForm.cs:634-688) is relaxed the same way: Save is enabled once the monitor grid validates, regardless of whether audio/app are set. A **configured-but-broken** audio device or app path (stale-path/stale-device warning already shown via `lblAppWarning`/`lblAudioNormalWarning`/`lblAudioRigWarning`) still blocks Save, consistent with "broken ≠ unset" — only a field that is cleanly unset (via the D-01/D-02 affordances) bypasses validation.

**Broken-Target Error Messages**
- **D-07:** Audio gets the same friendly, actionable toggle-time failure message pattern the app path already has (ToggleService.cs:76-77's `"The companion app could not be found at '{path}'. Open Settings and reselect..."`). New wording: something like `"The configured Rig/Normal-mode audio device could not be found. Open Settings and reselect it."` — applied per-direction, replacing whatever raw NAudio/IPolicyConfig exception message would otherwise surface. Exact wording is Claude's discretion at implementation time, but the tone/actionability/one-sentence-with-a-fix-instruction shape must match the app-path precedent.

### Claude's Discretion
- Exact enum/property shape for the new `Skipped` outcome (new `ToggleStepOutcome` case vs. some other representation) — implementation detail, not a vision decision.
- Exact wording of the new audio-device-not-found message (D-07) — must match the app-path message's tone and one-sentence-plus-fix-instruction shape, but precise phrasing is not locked.
- Exact placement/styling of the app-path Clear button and the audio dropdowns' "(None...)" list item — visual layout is Claude's call, following the app's existing Settings-form conventions (button style matching `btnBrowse`, list-item styling matching real device entries minus a device icon/detail).
- Whether audio-device-not-found detection happens via a pre-flight existence check (mirroring `File.Exists` for the app path) or by catching `SetDefault`'s own exception and re-wrapping the message — left to research/planning; NAudio's `MMDeviceEnumerator` likely offers a cheap existence check worth investigating.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| APP-04 | Leave companion app launch target unset; toggle skips launch/focus (Rig) and minimize (Normal) with no error | See "App Step Redesign" in Architecture Patterns — move the existing `File.Exists` preflight (ToggleService.cs:70-78) into a per-step guard so an unset path produces a `Skipped` step instead of blocking the whole toggle |
| APP-05 | Configured-but-missing app path still surfaces as a real `Failed` step, not silently unset | Same redesign — `File.Exists` check runs *inside* the App step body only when the path is non-empty, throwing a friendly exception caught by the existing `TryExecuteStep` pattern |
| AUDIO-03 | Leave Rig-mode audio device unset; toggle-to-Rig skips Rig-direction audio switching | See "Audio Step Redesign (Rig direction)" — same optional-step pattern as App, applied to `RigAudioDeviceId` |
| AUDIO-04 | Configuring a Normal-mode audio device makes it actually apply on toggle-to-Normal (replacing snapshot-based restore); unset skips | See "Audio Step Redesign (Normal direction)" — this is the one genuine behavior change, not just a validation relaxation: `ToggleToNormalMode`'s Audio step must stop reading `snapshot.Audio` and instead call `SetDefault(settings.NormalAudioDeviceId)`/skip, while `Monitor`'s restore stays snapshot-based (Phase 16 territory, do not touch) |
| AUDIO-05 | Configured-but-invalid audio device ID still surfaces as a real `Failed` step, not silently skipped | See "Cheap Audio-Device Existence Check" — promote `WindowsAudioController.TryResolveDevice` onto `IAudioController` so `ToggleService` can pre-check existence without a Windows-project reference |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

These are locked project-wide conventions this phase must not violate:

- **No new third-party dependency for this phase.** CLAUDE.md's stack table already covers everything this phase touches (NAudio 2.3.0 for `MMDeviceEnumerator`/device enumeration, WinForms ComboBox/TextBox for the Settings UI). Nothing here needs `AudioSwitcher.AudioApi`, `PInvoke.User32`, or any other package.
- **`RigToggle.Core` stays Windows-API-free.** Any new capability `ToggleService` needs (e.g. "does this audio device still exist") must be exposed through the existing `IAudioController`/`IAppController` abstractions, never by referencing `RigToggle.Windows` types from Core. `RigToggle.Core.csproj` has an explicit `Do NOT add a PackageReference to WindowsDisplayAPI or NAudio here` guard comment — respect it.
- **`PublishTrimmed=true` stays off project-wide** (COM/P-Invoke reachability-analysis risk) — irrelevant to this phase's diff but do not introduce anything that only works under trimming.
- **GSD workflow enforcement**: file edits for this phase must happen through the `/gsd:execute-phase` (or equivalent) planned-work flow, not ad hoc.

## Summary

This phase is a validation-gate relaxation plus one genuine runtime-behavior change, not new subsystem work — no new packages, no new external APIs, no Context7 lookups were needed. The three optional-target fields (`CompanionAppPath`, `RigAudioDeviceId`, `NormalAudioDeviceId`) are already fully nullable in `AppSettings`; the work is entirely in `ToggleService`, `IAudioController`, `SettingsForm`, and the `ToggleResult`/`ToggleStepOutcome` model.

Two load-bearing findings drive the implementation shape:

1. **D-04 ("all 3 steps always present in the result") forces the existing preflight exceptions to become in-step failures.** Today, `ToggleToRigMode`'s companion-app-missing check (ToggleService.cs:70-78) throws *before* capturing any state or running any step — if it fires, `MainForm` never even builds a `ToggleResult`, so the 3-row checklist is never shown (a bare MessageBox fires instead, via the generic `catch (Exception ex)` at MainForm.cs:384). That contradicts D-04's requirement that the checklist always have 3 rows. The fix is to move the `File.Exists` check *into* the App step's own `TryExecuteStep` body (throw a friendly exception there instead of pre-flighting it), and do the parallel thing for audio-device existence. This is not optional refactoring — it is required to satisfy D-04 as written.

2. **AUDIO-04 is a real behavior change, confined to the Audio half of `ToggleToNormalMode`.** `ToggleToNormalMode`'s Monitor step still restores from the pre-toggle snapshot (`_monitorController.Restore(snapshot.Monitor)`) — that is out of scope for this phase (Phase 16 territory, per the phase boundary and PITFALLS.md Pitfall 4). Only the Audio half changes: replace `_audioController.Restore(snapshot.Audio)` with the same optional-`SetDefault`-or-skip pattern Rig mode already uses for `RigAudioDeviceId`, keyed off `settings.NormalAudioDeviceId`. The `snapshot is null` / `wasInRigMode` branch structure that gates the whole method (the "never was in Rig mode, true no-op" case) must stay exactly as-is, because Monitor restore still needs it.

**Primary recommendation:** Add `ToggleStepOutcome.Skipped`; update `ToggleResult.Success` to treat `Skipped` as non-failing; move both the app-path and audio-device "does it still exist" checks from top-level preflight exceptions into per-step guarded bodies (a small `TryExecuteOptionalStep` helper, described below, keeps this from duplicating `TryExecuteStep`'s catch/trace logic); promote `WindowsAudioController.TryResolveDevice` onto `IAudioController`; relax `IsFullyConfigured` and `ValidateSettingsForm` in the same commit (Pitfall 8); and fix the two stale "both audio devices, and the companion app" message strings (`ToggleService.cs:67`, `MainForm.cs:292`) that will otherwise mislead users once those fields are genuinely optional.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Optional-target validation gating (Save button, toggle-enabled check) | App (WinForms `SettingsForm`) | Core (`ToggleService.IsFullyConfigured`) | Two independently-maintained gates already exist (Pitfall 8) — both are UI/business-rule layers, no Windows API involvement |
| Skipped-vs-Failed step classification | Core (`ToggleService`) | — | Pure business logic; no Win32/COM surface. `IAudioController`/`IAppController` remain the only Windows-touching boundary |
| Audio-device existence check | Core (`IAudioController` interface) → Windows (`WindowsAudioController` impl) | — | Core defines the contract (`TryResolveDevice`), Windows implements it via `MMDeviceEnumerator.GetDevice` — same pattern as every other controller method |
| App-path existence check | Core (`ToggleService`, direct `File.Exists` BCL call) | — | No interface indirection needed — `File.Exists` is a plain BCL call, already used this way today, not a Windows-API-specific operation |
| Skipped/Failed rendering (checklist text, MessageBox/tray balloon) | App (`ToggleResultFormatter`, shared by GUI + tray) | — | Single shared formatter already used by both `MainForm`'s dialog path and its tray/hotkey balloon-tip paths — fixing it once fixes both surfaces |
| "(None...)" sentinel picker item / Clear button | App (`SettingsForm`) | — | Pure WinForms UI concern |

## Standard Stack

No new libraries. Everything needed is already a dependency of this solution.

### Core (already present, reused as-is)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| NAudio (`NAudio.CoreAudioApi`) | 2.3.0 `[VERIFIED: existing PackageReference, RigToggle.Windows.csproj]` | `MMDeviceEnumerator.GetDevice(id)` — the cheap existence check this phase needs for AUDIO-05 | Already wrapped by `WindowsAudioController.TryResolveDevice` (WindowsAudioController.cs:219-236); this phase only needs to expose that existing method through `IAudioController`, not add anything new |
| WinForms (`System.Windows.Forms`) | .NET 10 SDK (`UseWindowsForms=true`) `[VERIFIED: existing PackageReference, RigToggle.App.csproj]` | `ComboBox` sentinel item (D-02), new Clear `Button` (D-01), `TextBox.PlaceholderText` (optional, see UX pitfall below) | Matches the rest of `SettingsForm`'s existing controls |
| xunit | 2.9.2 `[VERIFIED: existing PackageReference, RigToggle.Tests.csproj]` | Unit tests for the new `Skipped`/`Failed` branching | Already the project's test framework — no change needed |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Promoting `TryResolveDevice` onto `IAudioController` | Catching `SetDefaultEndpoint`'s own COM exception in `ApplyAndVerify` and re-wrapping the message at that layer | Rejected: `ApplyAndVerify`'s exception text is currently generic ("Audio default for role X did not change...") and shared by every role in the loop — re-wrapping it well enough to produce D-07's one-sentence-plus-fix-instruction wording would mean re-deriving "is this a not-found vs. some other COM failure" from a raw HRESULT, which `TryResolveDevice`'s existing `MMDeviceEnumerator.GetDevice` null-check already does cleanly and is already proven-reliable code (used by `Restore` today) |
| A new `TextBox.PlaceholderText`-based "unset" indicator for `txtAppPath` | A `_pendingAppPath` nullable-string field mirroring the existing `_pendingHotkeyModifiers`/`_pendingHotkeyKey` pattern (SettingsForm.cs:29-30) | Recommended: see "Landmine: the App-path placeholder text lives IN `txtAppPath.Text`" below — `PlaceholderText`'s interaction with `ReadOnly=true` textboxes is not confirmed safe, while the `_pending*` field pattern is already proven in this exact file for an identical "nullable, user-clearable, Save-time-persisted" need |

**Installation:** None — no new packages this phase.

## Package Legitimacy Audit

Not applicable — this phase introduces zero new external packages. `slopcheck`/registry verification was not run because there is nothing to verify.

## Architecture Patterns

### System Data Flow (this phase's diff)

```
┌─────────────────────────── SettingsForm (App) ───────────────────────────┐
│                                                                             │
│  txtAppPath (ReadOnly) ──Browse/DragDrop──> sets _pendingAppPath (NEW)     │
│       │                                                                     │
│       └──Clear button (NEW, D-01)──> _pendingAppPath = null                │
│                                                                             │
│  cboAudioNormal / cboAudioRig ──sentinel "(None...)" prepended (D-02)──>   │
│       SelectedItem is always a PickerItem (real device OR sentinel,        │
│       Id = null for sentinel) — ValidateSettingsForm's existing            │
│       `SelectedItem is PickerItem` check needs NO change                   │
│                                                                             │
│  ValidateSettingsForm (D-06): appPathOk / audioNormalOk / audioRigOk now   │
│  true when "cleanly unset" OR "set+valid"; false only when "set+broken"    │
│                                                                             │
│  BtnSaveSettings_Click ──persists AppSettings with null fields for──>      │
│       cleanly-unset targets (unchanged shape, no new AppSettings fields)   │
└──────────────────────────────────┬────────────────────────────────────────┘
                                    │ ISettingsStore.Load()
                                    ▼
┌─────────────────────────── ToggleService (Core) ──────────────────────────┐
│                                                                             │
│  IsFullyConfigured (D-05): ONLY the monitor-set gate remains               │
│                                                                             │
│  ToggleToRigMode:                                                          │
│    Monitor step (unchanged) ──fail──> Audio/App = NotAttempted (unchanged) │
│         │ succeed                                                          │
│         ▼                                                                  │
│    Audio step (NEW shape): RigAudioDeviceId null? ──> Skipped              │
│                              else: TryResolveDevice miss? ──> Failed        │
│                              else: SetDefault() ──> Succeeded/Failed        │
│         │ Skipped/Succeeded (not Failed)                                   │
│         ▼                                                                  │
│    App step (NEW shape): CompanionAppPath null? ──> Skipped                │
│                            else: File.Exists miss? ──> Failed               │
│                            else: LaunchOrFocus() ──> Succeeded/Failed       │
│                                                                             │
│  ToggleToNormalMode:                                                       │
│    (unchanged) wasInRigMode/snapshot-null gate — still the whole-method    │
│    no-op guard, still keyed off ISnapshotStore.Exists() (D-14, untouched   │
│    this phase — Phase 16 territory)                                       │
│         │ snapshot present                                                 │
│         ▼                                                                  │
│    Monitor step: _monitorController.Restore(snapshot.Monitor) (UNCHANGED)  │
│    Audio step (CHANGED, AUDIO-04): snapshot.Audio no longer read.          │
│         NormalAudioDeviceId null? ──> Skipped                              │
│         else: TryResolveDevice miss? ──> Failed (friendly message)         │
│         else: SetDefault() ──> Succeeded/Failed                            │
│         (still isolate-and-continue — Monitor failure doesn't block this)  │
│    App step (CHANGED, symmetric with Rig direction's Skipped semantics)    │
└──────────────────────────────────┬────────────────────────────────────────┘
                                    │ ToggleResult { Steps: [Monitor, Audio, App] }
                                    ▼
┌────────────────── ToggleResultFormatter (Core, shared) ───────────────────┐
│  FormatChecklist: add `Skipped => "{name}: Skipped (not configured)"`     │
│  ToggleResult.Success: now `Steps.All(s => s.Outcome is Succeeded          │
│                                             or Skipped)` (CHANGED)         │
│  Consumed identically by MainForm's dialog checklist AND its tray/hotkey  │
│  balloon-tip paths — one fix covers both surfaces                        │
└─────────────────────────────────────────────────────────────────────────┘
```

### Pattern 1: `TryExecuteOptionalStep` — extend, don't duplicate, the existing step-runner

**What:** A small wrapper around the existing `TryExecuteStep(string, Action, List<ToggleStepResult>)` helper (ToggleService.cs:157-175) that adds a "configured at all?" guard in front of it, so the `Skipped` case doesn't need its own separate try/catch/trace block.

**When to use:** Every optional-target step (Audio in both directions, App in both directions).

**Example (illustrative shape, not literal required code — exact structure is implementation detail):**
```csharp
// Extends the existing TryExecuteStep (ToggleService.cs:157-175) rather than duplicating
// its catch/Trace.WriteLine/ToggleStepResult-append logic. Returns true when the step did
// NOT block the chain (Skipped counts as "did not block", same as Succeeded) — mirrors
// TryExecuteStep's existing bool-return contract so callers can still gate subsequent
// steps identically to how Monitor gates Audio/App today.
private static bool TryExecuteOptionalStep(
    string stepName,
    string? configuredValue,
    Action<string> action,
    List<ToggleStepResult> steps)
{
    if (string.IsNullOrEmpty(configuredValue))
    {
        // D-03/D-04: a distinct Skipped outcome, never NotAttempted — "user chose not to
        // configure this," not "blocked by an earlier failure."
        steps.Add(new ToggleStepResult(stepName, ToggleStepOutcome.Skipped, null));
        return true;
    }

    return TryExecuteStep(stepName, () => action(configuredValue), steps);
}
```

Usage in `ToggleToRigMode` (replacing the current unconditional `TryExecuteStep("Audio", ...)`/`TryExecuteStep("App", ...)` calls):
```csharp
if (!TryExecuteOptionalStep("Audio", settings.RigAudioDeviceId, deviceId =>
    {
        if (_audioController.TryResolveDevice(deviceId) is null)
        {
            throw new InvalidOperationException(
                "The configured Rig-mode audio device could not be found. Open Settings and reselect it.");
        }
        _audioController.SetDefault(deviceId);
    }, steps))
{
    steps.Add(new ToggleStepResult("App", ToggleStepOutcome.NotAttempted, null));
    return new ToggleResult(steps);
}

TryExecuteOptionalStep("App", settings.CompanionAppPath, path =>
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"The companion app could not be found at '{path}'. Open Settings and reselect the companion app path before switching to Rig Mode.");
        }
        _appController.LaunchOrFocus(path);
    }, steps);
```

This preserves D-04's existing stop-on-first-failure semantics for Rig mode (a *Failed* Audio step still blocks App via the `if (!TryExecuteOptionalStep(...))` short-circuit, exactly like today) while adding the `Skipped` branch without touching `TryExecuteStep`'s own body at all.

**Behavior change this implies (flag explicitly for the plan):** Today, the App-path `File.Exists` check runs as a preflight *before* Monitor/Audio ever execute (ToggleService.cs:70-78) — a broken app path currently blocks the entire toggle, including monitor disable, from running at all. Under the redesign above, App is still the last step in the chain, so Monitor and Audio will have already run (and likely succeeded) by the time a broken app path is discovered. This is a deliberate, necessary consequence of D-04 ("all 3 steps always present in the result") — the old preflight-throw approach cannot produce a 3-row `ToggleResult` at all, so it cannot satisfy D-04 as written. Call this out explicitly in the plan/PR description so it isn't mistaken for an accidental regression.

### Pattern 2: `ToggleToNormalMode`'s Audio step — same optional pattern, isolate-and-continue variant

**What:** `ToggleToNormalMode` (unlike `ToggleToRigMode`) is isolate-and-continue, not stop-on-first-failure (class doc, ToggleService.cs:14-16) — every step is attempted and wrapped in its own try/catch, with the *step's own* try/catch already present today (see the existing `audioFailure`/`monitorFailure` pattern at ToggleService.cs:273-334). The optional-target guard needs to live *inside* that existing try block, not replace it.

**Example (illustrative):**
```csharp
Exception? audioFailure = null;
ToggleStepOutcome audioOutcome;
if (string.IsNullOrEmpty(settings.NormalAudioDeviceId))
{
    audioOutcome = ToggleStepOutcome.Skipped;
}
else
{
    try
    {
        if (_audioController.TryResolveDevice(settings.NormalAudioDeviceId) is null)
        {
            throw new InvalidOperationException(
                "The configured Normal-mode audio device could not be found. Open Settings and reselect it.");
        }
        _audioController.SetDefault(settings.NormalAudioDeviceId);
        audioOutcome = ToggleStepOutcome.Succeeded;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Trace.WriteLine($"Audio switch failed, continuing: {ex}");
        audioFailure = ex;
        audioOutcome = ToggleStepOutcome.Failed;
    }
}

steps.Add(new ToggleStepResult("Audio", audioOutcome, audioFailure?.Message));
```

**Critical scope boundary — do not touch Monitor's snapshot dependency:** The `if (snapshot is null) { ... } else { ... }` branch structure that gates all of `ToggleToNormalMode` (ToggleService.cs:252-335) exists because `_monitorController.Restore(snapshot.Monitor)` needs the snapshot payload — that call, and the whole-method no-op-when-never-in-rig-mode guard, are Phase 16 territory (explicit monitor config replacing snapshot-restore) and must NOT be rewritten in this phase. Only the *body* of the Audio branch inside that `else` changes — from `_audioController.Restore(snapshot.Audio)` to the `SetDefault`-or-skip pattern above. `snapshot.Audio` becomes unread dead data once this ships (still captured and persisted as part of `StateSnapshot`, just never consumed for restore anymore) — this is expected and matches FEATURES.md's own flagged architectural finding; leave `AudioState`/`AudioRoleState`/`_audioController.CaptureState()` untouched, their cleanup is explicitly Phase 18 scope (PITFALLS.md Technical Debt table).

### Pattern 3: Cheap Audio-Device Existence Check (AUDIO-05)

**What:** `WindowsAudioController` already has exactly the method this phase needs — `TryResolveDevice(string? deviceId) : AudioDeviceInfo?` (WindowsAudioController.cs:219-236), which wraps `MMDeviceEnumerator.GetDevice(id)` with a defensive null-check + broad try/catch, returning `null` for both "not found" and "enumerator threw." It is currently `public` on the concrete class but **not part of `IAudioController`**, so `ToggleService` cannot call it without violating the Core-stays-Windows-API-free rule (CLAUDE.md constraint).

**Fix:** Add `AudioDeviceInfo? TryResolveDevice(string? deviceId);` to `IAudioController` (Abstractions/IAudioController.cs). `WindowsAudioController` already implements the method with the exact right signature — this is a header-only, zero-new-logic change on the production side. The test double (`FakeAudioController` in `src/RigToggle.Tests/Doubles/FakeControllers.cs`) needs a matching implementation added (e.g. a configurable `bool _deviceExists` returning a fake `AudioDeviceInfo` or `null`, so tests can drive both the AUDIO-05 "Failed" path and the AUDIO-03/04 "Skipped" path independently of the "Succeeded" path).

**Source:** Direct inspection of `src/RigToggle.Windows/WindowsAudioController.cs` (this session) — `[VERIFIED: read directly from repo, not training data]`.

### Pattern 4: The `(None — don't switch audio)` sentinel — extend `PickerItem`, not the ComboBox binding shape

**What:** `PickerItem` (SettingsForm.cs:51) is currently `sealed record PickerItem(string Id, string DisplayLabel)`. Change `Id` to `string?` and prepend one sentinel instance (`new PickerItem(null, "(None — don't switch audio)")`) to the `items` list built in `PopulateAudioPickers`/`PopulateAudioCombo` (SettingsForm.cs:516-580), unconditionally — even when zero real devices are enumerated (see UX pitfall below, "don't gate the sentinel behind device-enumeration success").

**Why `ValidateSettingsForm` needs no change to its existing expression:** `audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;` (SettingsForm.cs:636) already returns `true` for *any* selected `PickerItem`, sentinel or real — the type check doesn't care about `Id`'s value. The actual behavior change is entirely in `PopulateAudioCombo`: today, when `savedId is null`, the combo is left at `SelectedIndex = -1` (nothing selected) — which is *why* the field currently blocks Save when unset. The fix is to select the sentinel explicitly when `savedId is null`, not to change the validation expression.

**`BtnSaveSettings_Click` change:** `NormalAudioDeviceId = audioNormalItem.Id` (SettingsForm.cs:815) already produces the right nullable value once `PickerItem.Id` is `string?` and the sentinel's `Id` is `null` — no further change needed at the save site itself.

### Landmine: the App-path "unset" text currently lives IN `txtAppPath.Text`, not just visually

**What goes wrong if missed:** `PopulateAppPathField` (SettingsForm.cs:582-604) sets `txtAppPath.Text = "No app shortcut or .exe selected"` as a literal string *value* when `CompanionAppPath` is null — this is not a WinForms cue-banner/placeholder, it is the textbox's actual `.Text`. `BtnSaveSettings_Click` reads `CompanionAppPath = txtAppPath.Text` directly (SettingsForm.cs:819). If the new `appPathOk` check in `ValidateSettingsForm` becomes something naive like `string.IsNullOrEmpty(txtAppPath.Text) || IsValidLaunchTarget(txtAppPath.Text)`, it will find `.Text` non-empty (it's the placeholder sentence) and non-a-valid-path, so `appPathOk` stays `false` — **the exact same "impossible to save while unset" bug this phase exists to fix**, just moved one layer deeper.

**How to avoid:** Track "is the app path configured" via a separate nullable field, mirroring the codebase's own already-proven pattern for exactly this shape of problem — `_pendingHotkeyModifiers`/`_pendingHotkeyKey` (SettingsForm.cs:29-30, both `int?`, mutated only by explicit user actions, read directly at Save time, with `null` meaning "not configured"). Introduce `_pendingAppPath` (`string?`) the same way: `null` after Clear or on first-run-never-configured; set by Browse/drag-drop; read directly by `BtnSaveSettings_Click` (not derived from `.Text`). `txtAppPath.Text` becomes purely a *display* concern — show the real path when `_pendingAppPath is not null`, show a friendly "not configured" string when it's null, but never round-trip that display string back into `AppSettings`.

**Considered but not recommended as primary: `TextBox.PlaceholderText`.** WinForms' `System.Windows.Forms.TextBox.PlaceholderText` property exists and is documented for the current `windowsdesktop-10.0` API surface `[CITED: learn.microsoft.com/en-us/dotnet/api/system.windows.forms.textbox.placeholdertext]` — it would let `.Text` stay genuinely empty while still showing a greyed hint. However, its interaction with `ReadOnly = true` (which `txtAppPath` has, confirmed at `SettingsForm.Designer.cs:295`) was not confirmed safe by this research — WebSearch turned up an open `dotnet/winforms` GitHub issue (#4089) about `PlaceholderText`'s rendering diverging from the native Win32 cue-banner behavior, with no explicit confirmation either way for the `ReadOnly` case `[LOW confidence — inconclusive, flagged in Assumptions Log]`. The `_pendingAppPath` field approach sidesteps this uncertainty entirely and costs nothing extra (it's ~4 lines mirroring an existing pattern in the same file) — recommended as primary; `PlaceholderText` is a "nice to have if it turns out to work" polish item, not a dependency for correctness.

### Anti-Patterns to Avoid
- **Reusing `NotAttempted` for the unset case** — explicitly rejected by D-03. Note this directly *overrides* the milestone-level `ARCHITECTURE.md`'s own Anti-Pattern 3 recommendation ("Always append a `ToggleStepResult` with `ToggleStepOutcome.NotAttempted`... when a step is skipped due to being unconfigured") — that milestone-level research predates this phase's `discuss-phase` session; the phase-level CONTEXT.md decision (a distinct `Skipped` case) is the one to build against. Flag this divergence explicitly in the plan so a reviewer comparing against `ARCHITECTURE.md` doesn't flag D-03 as a regression.
- **Collapsing "unset" and "configured but broken" into the same check.** Every optional field needs two independent test cases (Pitfall 3): one asserting `Skipped` for null/empty, one asserting `Failed` for "set to a value that no longer resolves." A PR that only covers the first is incomplete.
- **Leaving `IsFullyConfigured`/`ValidateSettingsForm` out of sync** (Pitfall 8) — both files must be relaxed in the same commit/plan. This phase's own definition of done requires both.
- **Deriving "is App path configured" from `txtAppPath.Text`'s emptiness** — see the Landmine above.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| "Does this audio device ID still exist?" | A fresh `MMDeviceEnumerator.EnumerateAudioEndPoints(...).Any(d => d.ID == id)` scan, or a raw `enumerator.GetDevice(id)` try/catch written from scratch in `ToggleService` | `WindowsAudioController.TryResolveDevice` (already exists, already handles the undocumented throw-vs-null uncertainty per its own doc comment referencing "02-RESEARCH.md Pitfall 2 / Assumptions Log A2") | Promoted onto the interface, this is zero new logic — writing a second implementation in a different layer would duplicate a defensive pattern that was already deliberately hardened once |
| "Track a nullable, user-clearable Settings-form value that shouldn't leak its placeholder text into the persisted model" | A bespoke solution invented fresh for `txtAppPath` | The existing `_pendingHotkeyModifiers`/`_pendingHotkeyKey` pattern in the same file (SettingsForm.cs:29-30) | Same shape of problem, already solved once in this exact class — copy the idiom, don't reinvent it |

**Key insight:** This phase's entire surface area is "extend two already-correct patterns to two more fields" (the App-path missing-vs-unset distinction already existed for App; the isolate-and-continue step try/catch already existed for Normal-mode restore) — the risk is not missing capability, it's accidentally re-deriving a slightly-different, slightly-wrong version of a pattern that already exists correctly elsewhere in the same file/class.

## Common Pitfalls

### Pitfall 1: Silently skipping masks a genuinely different failure state (configured-but-broken)
**What goes wrong:** Treating "field is null" and "field is set but the underlying resource is gone" (moved `.exe`, unplugged audio device) identically — both reduce to "the step didn't run."
**Why it happens:** The mechanically simplest way to "make a required field optional" is `string.IsNullOrEmpty(x) ? skip : run` — correct for null/empty, but if that's the *only* branch added, the separate "configured but now invalid" case silently regresses from `Failed` to indistinguishable-from-`Skipped`.
**How to avoid:** Write one test per optional field asserting `Skipped` for null/empty, and a *separate* test asserting `Failed` for "set to a value that no longer resolves" (moved app path / removed audio device). See `Pattern 1`/`Pattern 2` above for the exact branch shape that keeps these two cases structurally distinct.
**Warning signs:** A test suite that only exercises the null-field case for a newly-optional target.
**Source:** `.planning/research/PITFALLS.md` Pitfall 3 (milestone-level) — HIGH confidence, directly grounded in this repo's own `ToggleService.cs` comments.

### Pitfall 2: `IsFullyConfigured`/`ValidateSettingsForm` drifting out of sync
**What goes wrong:** Two independent "is this configured" gates already exist (`ToggleService.IsFullyConfigured` in Core, `SettingsForm.ValidateSettingsForm` in App) — relaxing only one produces contradictory UX (Settings blocks Save on a field the toggle logic would happily skip, or vice versa).
**How to avoid:** Change both in the same commit/plan; add a code-review checklist item comparing the two "required fields" lists side by side.
**Source:** `.planning/research/PITFALLS.md` Pitfall 8 — HIGH confidence.

### Pitfall 3: `ToggleResult.Success` not updated for the new `Skipped` case
**What goes wrong:** `ToggleResult.Success => Steps.All(s => s.Outcome == ToggleStepOutcome.Succeeded)` (ToggleResult.cs:11) is a strict equality check against `Succeeded` only. Once `Skipped` exists, a toggle where the user deliberately left Audio/App unset would have `Success == false` even though nothing actually failed — `MainForm` would then show the "toggle did not fully complete" warning dialog/balloon (MainForm.cs:353-364, 605-609, 652-656) on every single toggle for any user who has ever left a target unconfigured. This is the single easiest-to-miss regression in this phase because it's three call sites downstream of `ToggleService`, in a different project, and none of them mention "Skipped" by name.
**How to avoid:** Update `Success` to `Steps.All(s => s.Outcome is ToggleStepOutcome.Succeeded or ToggleStepOutcome.Skipped)`. Add a unit test asserting `Success == true` for a result containing a mix of `Succeeded` and `Skipped` steps (no `Failed`/`NotAttempted`).
**Warning signs:** Any manual test where a user with an intentionally-unset App path sees a "did not fully complete" warning on an otherwise-clean toggle.

### Pitfall 4: Stale "fully configured" messaging in two places outside the CONTEXT.md canonical-refs list
**What goes wrong:** Two user-facing strings explicitly say "both audio devices, and the companion app" are required — `ToggleService.cs:67`'s `IsFullyConfigured`-guard exception message, and `MainForm.cs:292`'s "Please finish configuring Settings..." dialog. Neither file is called out in CONTEXT.md's `<canonical_refs>` "Existing code" list (which cites `ToggleService.cs` lines 201-205/70-78/140/306/195, `AppSettings.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`) — a plan that only touches the explicitly-listed lines will ship with these two now-misleading strings unchanged.
**How to avoid:** Update both strings to only reference the monitor-set requirement, matching D-05's new gate.
**Warning signs:** grep for `"both audio devices"` post-implementation — should return zero matches.

### Pitfall 5: `TextBox.PlaceholderText` + `ReadOnly=true` interaction not proven safe
**What goes wrong:** If the plan adopts `PlaceholderText` for `txtAppPath` display without spot-checking it actually renders on a `ReadOnly` textbox, the field could silently show a blank box instead of a helpful hint when unset.
**How to avoid:** Use the `_pendingAppPath` field pattern (Landmine section above) for the *correctness*-critical part (what gets persisted); treat `PlaceholderText` purely as an optional visual nicety to be spot-checked manually during implementation, not relied upon.
**Source:** WebSearch, inconclusive — `[LOW confidence, see Assumptions Log A1]`.

## Code Examples

### Updated `ToggleStepOutcome` enum
```csharp
namespace RigToggle.Core.Models;

/// <summary>
/// Outcome of a single toggle step (Monitor / Audio / App). NotAttempted covers steps
/// skipped because an earlier step in a stop-on-first-failure sequence (ToggleToRigMode,
/// D-04) already failed. Skipped (added Phase 15/D-03) covers a step deliberately left
/// unconfigured by the user (optional App/Audio targets, APP-04/AUDIO-03/AUDIO-04) —
/// these are NOT the same state and must never render identically: NotAttempted means
/// "blocked by an earlier failure," Skipped means "nothing to do here by design."
/// </summary>
public enum ToggleStepOutcome
{
    Succeeded,
    Failed,
    NotAttempted,
    Skipped,
}
```

### Updated `ToggleResult.Success`
```csharp
namespace RigToggle.Core.Models;

public sealed record ToggleResult(IReadOnlyList<ToggleStepResult> Steps)
{
    // Phase 15/D-03: a Skipped step (deliberately unconfigured target) is not a failure —
    // only Failed/NotAttempted should flip Success to false. Do not revert this to a
    // strict `== Succeeded` check; that would make every toggle with any optional target
    // left unset report as "did not fully complete" (see Common Pitfalls: Pitfall 3).
    public bool Success => Steps.All(s => s.Outcome is ToggleStepOutcome.Succeeded or ToggleStepOutcome.Skipped);
}
```

### Updated `ToggleResultFormatter.FormatChecklist` switch arm
```csharp
// Source: RigToggle.Core/ToggleResultFormatter.cs:28-34, extended with a Skipped arm
result.Steps.Select(step => step.Outcome switch
{
    ToggleStepOutcome.Succeeded => $"{step.StepName}: OK",
    ToggleStepOutcome.Failed => $"{step.StepName}: FAILED ({step.Reason})",
    ToggleStepOutcome.NotAttempted => $"{step.StepName}: not attempted",
    ToggleStepOutcome.Skipped => $"{step.StepName}: Skipped (not configured)",  // D-04 wording
    _ => $"{step.StepName}: unknown",
})
```

### `IAudioController` interface addition
```csharp
// Source: RigToggle.Core/Abstractions/IAudioController.cs — add one member, no other change
public interface IAudioController
{
    IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices();
    AudioState CaptureState();
    void SetDefault(string deviceId);
    void Restore(AudioState previousState);

    // Phase 15/AUDIO-05: cheap existence check, promoted from WindowsAudioController's
    // existing internal helper (previously only used by Restore's stale-ID fallback).
    // Returns null for both "not found" and "enumerator threw" — same defensive contract
    // TryResolveDevice already has.
    AudioDeviceInfo? TryResolveDevice(string? deviceId);
}
```
`WindowsAudioController.TryResolveDevice` (WindowsAudioController.cs:219-236) already matches this signature exactly — only the interface gains the member; the implementation is unchanged.

## State of the Art

Not applicable in the usual "library/API evolved" sense — this phase is internal-only refactoring against a stable, already-shipped codebase. The one relevant "state of the art" note is that WinForms' `TextBox.PlaceholderText` (a genuinely newer WinForms feature, added post-.NET-Framework) exists and could theoretically replace the older "store literal placeholder text in `.Text`" pattern this codebase currently uses for `txtAppPath` — but see Pitfall 5 above for why this research does not recommend adopting it as load-bearing in this phase.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | `TextBox.PlaceholderText` may not render correctly when the textbox has `ReadOnly = true` | Architecture Patterns → Landmine section, Pitfall 5 | LOW — this research already recommends NOT depending on `PlaceholderText` for correctness (the `_pendingAppPath` field pattern is the recommended primary mechanism); if `PlaceholderText` turns out to work fine with `ReadOnly`, the only cost is a missed minor visual polish opportunity, not a functional bug |
| A2 | The exact audio-not-found message wording (`"The configured Rig/Normal-mode audio device could not be found. Open Settings and reselect it."`) matches D-07's required tone closely enough to ship as-is | Code Examples, Architecture Patterns Pattern 1/2 | LOW — D-07 explicitly leaves exact wording to Claude's discretion; only the tone/shape is locked, and the proposed wording mirrors the existing app-path message's exact structure (one sentence problem, one sentence fix) |

**All other claims in this research are either `[VERIFIED]` (direct source-tree reads, most of this document) or `[CITED]` (the one official-docs WebSearch, `TextBox.PlaceholderText`'s existence — not its `ReadOnly` interaction, which is separately flagged as A1).**

## Open Questions

1. **Should `IsFullyConfigured`/`IsSettingsConfigured` be renamed once it no longer checks "fully" anything but the monitor set?**
   - What we know: D-05 locks the *behavior* (drop the three field checks); CONTEXT.md's `<code_context>` doesn't mandate a rename, and the method is called from `MainForm.cs` (`IsSettingsConfigured()`) as well as internally.
   - What's unclear: Whether leaving the name as-is (now semantically misleading — it no longer means "fully" configured) is acceptable, or whether a rename (e.g. `HasMinimumMonitorConfiguration`) is worth the larger diff (call-site updates in `MainForm.cs`, `ToggleServiceTests.cs`).
   - Recommendation: Keep the name for this phase (minimize diff, matches "validation-gate relaxation, not a redesign" scope) but update the doc comment thoroughly (the existing D-07 comment at ToggleService.cs:197-200 is already stale in spirit and should be rewritten to reflect D-05, not just have code deleted out from under it). Flag rename as a Phase 18 cleanup-pass candidate if it bothers the team later.

2. **Does `FakeAudioController` need a `Restore` call-log entry removed from `ToggleServiceTests.cs`'s existing `ToggleToNormalMode_RestoresAudioViaRestore_NeverSetDefault` test?**
   - What we know: That test (ToggleServiceTests.cs:123-134) currently asserts `audio.Restore` IS called and `audio.SetDefault` is NOT called during `ToggleToNormalMode` — this assertion is the literal opposite of AUDIO-04's required new behavior.
   - What's unclear: Nothing, really — this test's name and body directly encode the *old* behavior AUDIO-04 explicitly replaces.
   - Recommendation: This test must be rewritten (not just updated) to assert the new contract: `audio.SetDefault:{NormalAudioDeviceId}` is called and `audio.Restore` is NOT called for the Audio step specifically (Monitor's `monitor.Restore` call is unaffected and should still appear). Flag this explicitly in the plan's task list — it's an easy one to miss since the test currently passes and nothing about compiling will surface it as wrong.

## Environment Availability

Skipped — this phase has no external tool/service/runtime dependencies beyond the existing .NET 10 SDK + NAudio/WinForms already present in the solution (confirmed via direct `.csproj` inspection). Nothing new to probe.

## Validation Architecture

Skipped — `.planning/config.json`'s `workflow.nyquist_validation` is explicitly `false`.

## Security Domain

This is a personal, single-user, non-networked desktop utility (per CLAUDE.md's own stated constraints) — most ASVS categories genuinely do not apply. This phase specifically touches settings persistence and Windows COM/audio-device interop, not authentication, sessions, or network input.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Single-user local app, no auth surface |
| V3 Session Management | No | No sessions |
| V4 Access Control | No | No multi-principal access model |
| V5 Input Validation | Yes (narrow) | `File.Exists`/path validation for the app-path field (already exists, `IsValidLaunchTarget`), device-ID existence check via `TryResolveDevice` — both are already-established patterns this phase extends, not new validation surface |
| V6 Cryptography | No | Not touched by this phase |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|-----------------------|
| TOCTOU between app-path existence check and later `Process.Start`/`ShellExecute` | Tampering (low severity, already accepted) | Already documented and accepted by this codebase as `T-03-09` (ToggleService.cs:73-75) — "this is a fail-fast UX guard, not a security control." This phase's changes (moving the check into the App step body) do not alter that stance or its acceptance; explicitly keep the existing `File.Exists` check's strictness identical, per PITFALLS.md's Security Mistakes table ("optionality changes *whether* the check runs, not *how strict* it is when it does run") |
| Persisting an attacker-substituted device ID or path silently because "it's optional now, so don't validate as carefully" | Tampering | Not applicable — this phase's redesign keeps the exact same validation strictness for the "configured" branch of both App and Audio; only the "never configured" branch changes |

## Sources

### Primary (HIGH confidence — direct source-tree reads, this session)
- `src/RigToggle.Core/ToggleService.cs` (full file)
- `src/RigToggle.Core/Models/ToggleResult.cs`, `ToggleStepOutcome.cs`, `ToggleStepResult.cs`
- `src/RigToggle.Core/ToggleResultFormatter.cs`
- `src/RigToggle.Core/ToggleOrchestrator.cs`
- `src/RigToggle.Core/Models/AppSettings.cs`
- `src/RigToggle.Core/Abstractions/IAudioController.cs`, `IAppController.cs`
- `src/RigToggle.Windows/WindowsAudioController.cs` (full file)
- `src/RigToggle.App/SettingsForm.cs` (lines 1-100, 500-909)
- `src/RigToggle.App/SettingsForm.Designer.cs` (grep for `txtAppPath`/`btnBrowse`/`cboAudioNormal`/`cboAudioRig`/audio-warning-label declarations)
- `src/RigToggle.App/MainForm.cs` (lines 280-410, 560-660)
- `src/RigToggle.Tests/ToggleServiceTests.cs` (full file), `src/RigToggle.Tests/Doubles/FakeControllers.cs` (full file)
- `.planning/phases/15-optional-app-audio-targets/15-CONTEXT.md`
- `.planning/REQUIREMENTS.md`, `.planning/STATE.md`
- `.planning/research/FEATURES.md`, `PITFALLS.md`, `ARCHITECTURE.md` (v2.0 milestone sections)
- `CLAUDE.md` (project root)

### Secondary (MEDIUM confidence)
- `https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.textbox.placeholdertext?view=windowsdesktop-10.0` — confirms `TextBox.PlaceholderText` exists on the current WinForms API surface `[CITED]`

### Tertiary (LOW confidence, flagged for validation)
- `https://github.com/dotnet/winforms/issues/4089` — WebSearch result, inconclusive on `PlaceholderText` + `ReadOnly` interaction specifically; used only to justify NOT depending on this API for correctness (Assumption A1)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — zero new packages, everything reused is already a verified existing dependency
- Architecture: HIGH — every pattern is grounded in direct reads of this repo's own existing code, extending proven idioms already present in the same files
- Pitfalls: HIGH for Pitfalls 1-4 (directly derived from source reads and milestone-level PITFALLS.md, itself HIGH-confidence per its own sourcing); LOW-flagged-as-such for Pitfall 5 (external API interaction, unconfirmed)

**Research date:** 2026-08-04
**Valid until:** No expiry concern — this is internal-only refactoring against code that does not change out from under this research (not a fast-moving external dependency). Safe to treat as valid through this phase's execution regardless of elapsed time.

---
*Phase: 15-Optional-App-Audio-Targets*
*Research completed: 2026-08-04*
