# Feature Research

**Domain:** Windows display/audio/app-launch toggle utility — configurable monitor profiles, optional automation targets, live manual display control panel
**Researched:** 2026-08-04
**Confidence:** MEDIUM-HIGH (patterns verified against DisplayFusion, NirSoft MultiMonitorTool, Windows' own Settings > Display UI, and EarTrumpet/SoundVolumeView via WebSearch; codebase dependency claims verified by direct inspection of the current `RigToggle.Core`/`RigToggle.App`/`RigToggle.Windows` source, not just training data)

This supersedes the v1.2 `FEATURES.md` (dated 2026-08-02, which covered theme-following UI + tray icon redesign — that milestone is shipped, its research is no longer active). This file covers the **v2.0 milestone only**: optional app/audio toggle targets, explicit per-mode monitor configuration replacing snapshot-restore, and a new live manual monitor-toggle panel. It assumes everything currently shipped (v1.0-v1.2: GUI settings, tray residency, hotkey, multi-monitor sets for Rig mode, theming, icons) is already built and working per `.planning/PROJECT.md`.

## Comparable Tools Surveyed

- **DisplayFusion** (Monitor Profiles + hotkeys) — closest analog to features #3/#4: named profiles are explicit "these monitors on, these off" sets (not snapshots), switchable by hotkey, with a **confirmation-prompt toggle** ("disable confirmation prompt when changing monitor settings") that is *on by default*.
- **NirSoft MultiMonitorTool** — GUI checkboxes per monitor + `Ctrl+F6`/`F7`/`F8` (disable/enable/disable-enable-switch) + tray-icon quick access + CLI (`/disable`, `/enable`) for external triggers. Confirms the "per-monitor live toggle with a persistent status affordance" pattern is standard, not novel.
- **Windows Settings > System > Display** (built-in) — the canonical reference for #4's status-icon UX: numbered rectangle tiles per monitor, an **Identify** button that overlays a number on the physical screen, a **Detect** action for reconnecting displays, and (separately, in the classic resolution-change dialog) a **"Keep these display settings?" 15-second auto-revert** — the standard Windows pattern for any display mutation that risks locking the user out of a usable screen.
- **EarTrumpet / SoundVolumeView** (default audio device switching) — confirms "no target device configured → no-op, never an error" is the universal convention for audio-switch automation; neither tool treats an unset target as an error state.
- **NVIDIA/AMD driver display profiles** — same explicit-profile-not-snapshot pattern as DisplayFusion, applied per-game/per-app rather than per-hotkey; reinforces that "named target configuration, not runtime snapshot" is the dominant pattern once a tool moves beyond a single ad-hoc toggle.

## Feature Landscape (by target feature)

### 1. Optional App Launch Target

**Table stakes (expected baseline behavior):**

| Behavior | Why Expected | Complexity |
|----------|--------------|------------|
| Unset app path → toggle silently skips launch/focus (Rig direction) and minimize (Normal direction); no dialog, no error | Universal automation-tool convention (AutoHotkey optional macro step, Stream Deck "no action" slot, DisplayFusion's "Run this program" being an optional per-profile action) | LOW |
| Settings UI shows the app-path field as genuinely optional (no red/required styling) once unset | Matches the project's own existing convention for the hotkey fields (`HotkeyModifiers`/`HotkeyKey` are already nullable, "no default hotkey" per D-02) — this feature is bringing App into line with a pattern the codebase already established, not inventing a new one | LOW |
| Toggle result reporting distinguishes "skipped because not configured" from "not attempted because an earlier step failed" | `ToggleStepResult` currently only has `Succeeded`/`Failed`/`NotAttempted` — conflating "user chose not to configure this" with "upstream failure blocked this" is a real UX regression risk once App becomes optional, since both would otherwise render identically | LOW-MEDIUM (may need a 4th outcome or a distinct message string) |

**Differentiators:** None really — this is closing a scope gap, not a competitive feature. Do not over-invest here.

**Anti-features (avoid):**
- Do **not** add independent per-direction opt-in (e.g., "launch on Rig only, never minimize on Normal") — keep a single optional `CompanionAppPath`, symmetric across both directions, matching the existing single-field model. Splitting it is unrequested scope creep.
- Do **not** conflate "never configured" (`null`/empty — skip silently) with "configured but the file is now missing" (a real, user-facing failure state — see Edge Cases below). Collapsing these removes a useful signal.

**Dependencies on existing model:**
- `ToggleService.IsFullyConfigured` (ToggleService.cs:201-205) currently `&&`s `!string.IsNullOrEmpty(settings.CompanionAppPath)` into the required set — must drop this term.
- `ToggleService.ToggleToRigMode`'s preflight `File.Exists(settings.CompanionAppPath)` check (ToggleService.cs:70-78) currently throws unconditionally before any step runs — must be reworked to skip entirely when the path is null/empty, but still surface a real problem when a path *is* configured but the file is now missing (do not silently downgrade that to "skipped").
- `SettingsForm.ValidateSettingsForm`/the Save-button gating must drop the app-path field from its required-fields check.

---

### 2. Optional Audio Devices Per Direction

**Table stakes:**

| Behavior | Why Expected | Complexity |
|----------|--------------|------------|
| Unset `RigAudioDeviceId` → Rig-direction toggle skips the audio switch entirely, no error | Matches EarTrumpet/SoundVolumeView convention: unset target = no-op, never a failure | LOW |
| Unset `NormalAudioDeviceId` → Normal-direction toggle skips audio entirely | Symmetric with the Rig-direction behavior above | **MEDIUM — see architectural finding below** |
| Settings UI treats both audio-device pickers as optional, same visual treatment as the app path | Consistency across all three "optional target" settings | LOW |

**Differentiators:** None — parity feature, not a competitive one.

**Anti-features (avoid):**
- Do **not** make "RigAudioDeviceId set but NormalAudioDeviceId unset" fall back to "restore whatever was playing before" — that reintroduces exactly the snapshot-based hybrid behavior the milestone is deliberately moving away from for monitors. For consistency, an unset audio target should mean "touch nothing," full stop, not "fall back to snapshot."

**Complexity — architectural finding (important for roadmap):**
Direct inspection of `ToggleService.cs` shows `NormalAudioDeviceId` is currently used for **exactly two things**: the `IsFullyConfigured` required-field check, and pre-selecting the combo box in `SettingsForm`. It is **never read by `ToggleToNormalMode`** — that method restores audio via `_audioController.Restore(snapshot.Audio)`, i.e. the pre-toggle **snapshot**, completely independent of whatever `NormalAudioDeviceId` is set to. So "make `NormalAudioDeviceId` optional, skip audio switching when unset" isn't a validation-only change here — today, setting/unsetting it has **zero runtime effect on Normal-direction audio** at all. Delivering feature #2 as scoped requires *first* giving `NormalAudioDeviceId` a real runtime effect (switch `ToggleToNormalMode` from `Restore(snapshot.Audio)` to `SetDefault(settings.NormalAudioDeviceId)` when set, mirroring how Rig mode already works via `SetDefault(settings.RigAudioDeviceId)`), which is the *same* "snapshot-restore → explicit-config" architectural shift explicitly scoped for monitors (feature #3) — just not explicitly called out for audio in the milestone framing. Flag this for requirements/roadmap clarification: is Normal-mode audio meant to move to explicit-config symmetry with Rig mode (recommended, for consistency with feature #3's rationale), or does it stay snapshot-based with the optional flag only gating *whether* the (now-unused-for-Normal) restore call runs?

**Dependencies on existing model:**
- `ToggleService.IsFullyConfigured` — same relaxation as #1, drop both audio-ID required-field checks.
- `WindowsAudioController.SetDefault`/`ApplyAndVerify` already applies one device ID to all three Windows audio roles (eConsole/eMultimedia/eCommunications) in a single call — "optional per role" in the milestone framing means "optional per toggle **direction**" (Normal vs. Rig), not per individual Windows audio role; the existing per-role capture/restore machinery (`AudioState`/`AudioRoleState`) already operates correctly underneath a single `SetDefault(deviceId)` call and needs no change on that axis.
- If Normal-direction audio moves to explicit config (see above), `StateSnapshot.Audio`/`ISnapshotStore`'s audio half either becomes dead code or is repurposed purely as crash-recovery data (see Feature #3's dependency section — same question applies to audio as to monitors).

---

### 3. Normal Mode Explicit Monitor Target Set (replaces snapshot-restore)

**Table stakes:**

| Behavior | Why Expected | Complexity |
|----------|--------------|------------|
| Normal mode has its own `NormalMonitorsToDisable`/`NormalMonitorsToEnable`-shaped config, symmetric to Rig mode's existing `MonitorsToDisable`/`MonitorsToEnable` | This is literally how DisplayFusion Monitor Profiles and NVIDIA/AMD display profiles work — every named "mode" is an explicit target set, never a runtime snapshot | MEDIUM |
| Toggling to Normal mode disables/enables monitors to match the explicit Normal target set, not "whatever was active before the last toggle to Rig" | Direct scope statement in PROJECT.md; matches the profile-based pattern in every comparable tool surveyed | MEDIUM |
| Settings UI for Normal-mode monitors reuses the same picker UI already built for Rig-mode monitors (symmetric layout) | Users expect a "second copy" of a control they've already learned, not a differently-shaped one | LOW (UI reuse) |

**Differentiators:** None on their own — but this change is a *precondition* for feature #4 (live panel) sharing the safety-guard and apply logic cleanly, since both features now revolve around "the same monitor-set-apply operation, driven by three different sources" (Rig config, Normal config, or ad-hoc manual selection).

**Anti-features (avoid):**
- Do **not** keep snapshot-restore as a silent fallback "in case the explicit Normal set doesn't cover a monitor" — that reintroduces exactly the ambiguity (which state wins?) this milestone is meant to eliminate. If a monitor isn't mentioned in either the Normal disable-set or enable-set, its resulting state must be a clearly documented default (e.g., "left untouched" or "explicitly enabled") — not "whatever the snapshot says."
- Do **not** attempt "smart" migration that tries to synthesize a Normal-mode config by replaying the last captured snapshot at upgrade time as if it were a permanent setting — a one-time migration hint (e.g., pre-populate the Normal enable-set from `GetActiveMonitors()` at first-run-after-upgrade) is reasonable and expected; treating stale runtime snapshot data as durable configuration going forward is not.

**Critical architectural dependency — mode detection itself (must be called out explicitly for roadmap):**
`ToggleService.IsInRigMode()` currently derives the app's *entire* current-mode concept from snapshot-file presence: `_snapshotStore.Exists()` (documented inline as "D-14" — snapshot presence **is** the mode flag, there is no separate persisted mode field). Every consumer of "are we in Rig or Normal mode" — `MainForm`'s label/tray icon/tray text, the reentrancy-safe toggle routing, crash-recovery messaging — reads through this single signal. Once Normal mode stops depending on `ISnapshotStore` for monitor restore, **the snapshot file is no longer guaranteed to exist or be meaningful in Rig mode**, and mode detection needs a new, independently-persisted `CurrentMode` (or equivalent explicit flag) that is *not* coupled to whether a monitor/audio snapshot happens to be on disk. This is not optional plumbing — without it, the app has no way to know which mode it's in after this change ships. Recommend this be an explicit early requirement/phase in the roadmap, not an implementation detail folded silently into the monitor-config work.

A second, related consequence: the snapshot file currently doubles as **crash-recovery data** — if the app dies mid-toggle, the snapshot on disk is both "proof we were mid-transition" and "the exact state to recover to." Moving Normal-mode monitor restore off snapshots weakens or removes that free crash-recovery story for monitors (audio may retain it, per feature #2's open question above, or may not, if it's also moved to explicit config for consistency). Decide explicitly whether v2.0 needs a *replacement* crash-recovery mechanism (e.g., persist "toggle in progress, started at T, target mode X" as a small marker file) or whether losing automatic crash-recovery for monitors is an accepted tradeoff — this should be a stated decision, not a silent regression.

**Dependencies on existing model:**
- `AppSettings` needs new fields (or a nested `MonitorTarget`-shaped structure used twice) for the Normal-mode monitor set, mirroring `MonitorsToDisable`/`MonitorsToEnable`.
- `ToggleService.ToggleToNormalMode`'s current body (ToggleService.cs:243-374) is built entirely around `_snapshotStore.Load()`/`.Exists()`/`.Clear()` plus `_monitorController.Restore(snapshot.Monitor)` — this is a genuine rewrite of the method, not a small patch, since the snapshot-or-nothing branching (`if (snapshot is null) { ... }`) is the method's entire control-flow spine today.
- `StateSnapshot`/`MonitorState`/`MonitorPathSnapshot`/`ISnapshotStore`/`JsonSnapshotStore` either become dead code (if audio also drops snapshot-restore) or shrink to audio-only (if audio keeps it) — either way, this model needs an explicit "keep, shrink, or delete" decision, not silent abandonment. `WindowsMonitorController.Restore(MonitorState)` likely becomes unused and should be removed rather than left as dead code, per the milestone's own "code quality/cleanup pass" goal.
- The existing safety-guard error text ("Cannot disable all configured monitors — at least one active display must...", `WindowsMonitorController.cs:307`) is currently only reachable from the Rig-mode disable path; it must also guard whatever new "apply the Normal-mode target set" code path is added (see Feature #5).

---

### 4. Live Manual Monitor Toggle Panel (status icons)

**Table stakes:**

| Behavior | Why Expected | Complexity |
|----------|--------------|------------|
| One row/tile per detected monitor, independent of Rig/Normal mode, each with an on/off toggle control | Direct match to MultiMonitorTool's checkbox-per-monitor GUI and DisplayFusion's per-monitor enable state list | MEDIUM |
| Per-monitor status shown via **icon**, not just a text label ("Enabled"/"Disabled") | Explicit scope requirement; also matches Windows' own Settings > Display numbered-tile pattern, which is icon/graphic-first, text-second | MEDIUM (new icon assets — but the project already has a working procedural-icon pipeline: `RigToggle.IconGen`, used for the Phase 13 tray/exe icons, shape-distinct and colorblind-safe by explicit prior decision — reuse that generator/convention rather than sourcing new bitmap art) |
| Panel reflects **live** state — reacts to a monitor being connected/disconnected/hot-plugged while the panel is open, not just a snapshot taken when the panel opened | "Live" is explicit in the scope; matches Windows Settings' "Detect" affordance for newly connected displays | MEDIUM (requires either a refresh-on-focus/poll, or a `WM_DISPLAYCHANGE` message hook — the latter is idiomatic WinForms/Win32 and low-cost since the app already pumps Win32 messages for the global hotkey, Phase 9) |
| Acting on a monitor here has an immediate, real effect (not "stage a change, apply on Save" like the Settings picker) | This is the defining difference from the existing Settings-form monitor pickers — "on-demand" in the scope explicitly means immediate application, not deferred | LOW once the underlying "apply a target set" logic is unified with #3 |

**Differentiators (worth doing well, not just minimally):**
- An **Identify** action (briefly overlay a number/label on the physical screen) — standard in Windows' own Display settings and genuinely useful once a user has 3+ monitors and needs to map "row 2 in this panel" to "the physical screen on my left." Not explicitly scoped, but cheap relative to value and directly informed by the dominant pattern in every comparable tool surveyed; worth flagging as a candidate differentiator for the roadmap to accept or explicitly defer.
- A brief **revert-if-unconfirmed** safety window (Windows' own "Keep these display settings?" 15-second auto-revert pattern) for the live panel specifically, since unlike the Rig/Normal toggle (which is driven by pre-vetted config the user set up in advance), the live panel invites impulsive single-click changes that could disable the monitor the user is currently looking at. The project already has a related, lighter-weight pattern for this class of risk — `SkipMonitorConfirmation`, an existing Settings flag that gates a confirmation dialog before the Rig/Normal toggle's monitor changes apply (`MainForm.cs:307`) — extending that convention (or the stronger auto-revert variant) to the new live panel is a natural fit, not a new pattern being introduced.

**Anti-features (avoid):**
- Do **not** let the live panel silently persist its changes into `AppSettings`/Rig-mode/Normal-mode config — a manual on-demand toggle is explicitly independent of the Rig/Normal target sets per scope; conflating "I just want this one screen off right now" with "redefine what Normal mode means" would be a serious, confusing scope violation.
- Do **not** build a full drag-and-drop position/arrangement editor (Windows' own Display settings already owns that job well) — the scope is enable/disable status only, not display topology/arrangement editing. Scope creep to avoid.
- Do **not** attempt resolution/refresh-rate/orientation controls in this panel — same reasoning; stay narrowly scoped to on/off, matching the rest of this app's existing "disable/enable," not "configure," monitor model.

**Dependencies on existing model:**
- Needs read access to the full monitor inventory (`WindowsMonitorController`'s existing `GetAllMonitors()`), independent of whichever `AppSettings` target sets exist — this data path already exists (used by the Settings pickers) and can be reused directly.
- The actual apply-one-monitor-on/off operation should reuse `WindowsMonitorController.ActivateMonitors`/`DeactivateMonitors` (already generalized to sets in Phase 6) rather than introducing a third code path — pass a single-element set.
- New icon assets: reuse the `RigToggle.IconGen` GDI+ generator pattern (dev-time icon generation, not runtime-drawn) established in Phase 13, for consistency with the existing tray/exe icon pipeline and the project's stated colorblind-safe, shape-distinct visual convention.
- Must call through the same safety-guard codepath as #5 below — do not duplicate the "at least one monitor must stay enabled" check as a second implementation.

---

### 5. Safety Constraint: At Least One Monitor Always Enabled (carried over, no regression)

**Table stakes:**

| Behavior | Why Expected | Complexity |
|----------|--------------|------------|
| Enforced identically across all three now-independent ways to change monitor state: Rig-mode toggle, Normal-mode toggle, and the new live manual panel | Already true today for the toggle path (`WindowsMonitorController.cs:307`); scope explicitly requires it extend to the manual panel without regressing the toggle path | LOW-MEDIUM if the guard lives in one shared place; HIGH risk of drift/bugs if duplicated per call site |
| A blocked action fails clearly and immediately (before mutating anything), not after a partial mutation | Matches the existing guard's behavior (`WindowsMonitorController.cs` pre-mutation validation, confirmed in `ToggleService.cs` CR-01 comments as throwing "before any real CCD mutation is attempted") | Already established convention — extend, don't redesign |

**Differentiators:** None — this is a hard safety floor, not a feature to differentiate on.

**Anti-features (avoid):**
- Do **not** relax the guard to "at least one monitor enabled across the whole system" while allowing e.g. Normal-mode config to define zero enabled monitors "because Rig mode will re-enable something" — the guard must be evaluated against the **actual resulting state of the specific action being taken**, not a cross-mode assumption about what happens next. See Edge Cases below for the "both modes configured to zero enabled monitors" case this implies.

**Dependencies on existing model:**
- The current guard is implemented inside `WindowsMonitorController`'s disable path and is therefore already reasonably positioned to protect all three call sites (Rig toggle, Normal toggle once rewritten, and the manual panel) **if** all three route through the same controller methods (`ActivateMonitors`/`DeactivateMonitors`) rather than each growing bespoke apply logic — reinforces the "unify the apply-a-monitor-set operation" dependency called out under #3 and #4.

---

## Cross-Cutting Edge Cases (optional-target semantics)

These are the specific "what if" scenarios the scope's optionality introduces; each needs an explicit, intentional answer (not an accidental one) before roadmap phases are cut:

| Edge case | Recommended handling | Rationale |
|---|---|---|
| Both Rig-mode and Normal-mode monitor sets end up configured to zero enabled monitors (e.g., user empties both independently, or a migration bug does it) | The safety guard (#5) must block *applying* such a config at toggle-time / panel-time — reject the specific mutating action, don't merely warn in Settings — since Settings already allows saving an empty set today (only the apply-time guard in `WindowsMonitorController` currently prevents harm) | Confirmed existing pattern: Settings-side validation and apply-time guard are already two separate, intentionally redundant layers in the current code; keep both layers for the new Normal-mode set too |
| Configured `CompanionAppPath` is non-null but the file/shortcut no longer exists at toggle time (moved, uninstalled, external drive unplugged) | This is **not** the same as "unset" — must still surface a clear, real failure (as today), not be silently downgraded to "skipped" once App becomes optional | Conflating "never configured" with "configured but broken" removes a real, currently-working failure signal (ToggleService.cs:70-78 already does this correctly for the required case; must not regress when the field becomes optional) |
| Configured `RigAudioDeviceId`/`NormalAudioDeviceId` references a device ID that no longer exists (headset unplugged, USB DAC removed) | Same principle as above — a configured-but-now-invalid device ID is a real failure to surface, distinct from "not configured, skip silently." The existing `SettingsForm` already has device-availability warning UI (`lblAudioNormalWarning`/`lblAudioRigWarning`) for this at config time; the toggle-time behavior should be consistent with that, not silently no-op | Same "don't collapse two different states into one" principle as the app-path case |
| User empties the app path *after* it was previously configured (was required, now goes to null) | Treat purely as "now optional/unset," no migration ceremony needed — this is the field's designed new steady state, not an error condition | Matches how `MonitorsToEnable`/`MonitorsToDisable` already tolerate being emptied post-configuration (guarded against re-injection bugs per the existing "migration guard keys off null only, never null-or-empty" decision in PROJECT.md) — same discipline should apply to the newly-optional fields |
| Manual live-panel action targets a monitor that gets physically unplugged between the panel refreshing its list and the user clicking the toggle | Fail the specific action cleanly (existing "still-present" guard pattern already used in `WindowsMonitorController.Restore`, per the `"Still-present guard first"` comment at `WindowsMonitorController.cs:539`) rather than crashing or silently no-op-ing | This is a pre-existing, already-solved pattern in the codebase for a structurally identical race — reuse it rather than inventing a new one |
| Toggle-to-Rig with App optional-and-unset succeeds on Monitor+Audio — what does the result checklist show for the App row? | A distinct, positive "Skipped (not configured)" state, not blank and not styled like a failure | Prevents the 3-row toggle-result checklist (Monitor/Audio/App) from looking incomplete or broken once rows can legitimately be intentionally absent — directly relevant to the existing `ToggleResultFormatter` shared by GUI MessageBox and tray balloon-tip surfaces |

## Feature Dependencies

```
[3: Normal-mode explicit monitor set]
    └──requires──> [new persisted CurrentMode flag, replacing snapshot-presence-as-mode-signal (D-14)]
    └──requires──> [decision: keep/shrink/delete ISnapshotStore + StateSnapshot/MonitorState model]
    └──enables───> [4: Live manual monitor panel] (shares the same "apply a target set" controller calls)

[4: Live manual monitor panel]
    └──requires──> [5: safety guard, called from a single shared codepath, not duplicated]
    └──requires──> [3's controller-call unification, to avoid a third bespoke apply path]

[1: Optional app target] ──parallel, independent──> [2: Optional audio targets]
    (both are validation-gate relaxations + guard clauses; neither blocks the other or
     features 3/4/5, but 2 has its own internal dependency — see below)

[2: Optional audio targets]
    └──requires (if Normal-direction symmetry is adopted, recommended)──>
           [Normal-mode audio switches from Restore(snapshot.Audio) to
            SetDefault(settings.NormalAudioDeviceId), mirroring Rig mode]
```

### Dependency Notes

- **Feature 3 requires a new mode-detection mechanism:** this is the single highest-risk hidden dependency in the whole milestone. `IsInRigMode()` today *is* `_snapshotStore.Exists()` — there is no other mode flag anywhere in the codebase. Any phase that touches Normal-mode monitor behavior must also land explicit mode persistence, or the app will have no reliable way to know which mode it's in.
- **Feature 4 depends on Feature 3's unification work, not its data model:** the live panel doesn't need the new `NormalMonitorsToDisable`/`Enable` settings fields themselves, but it does need the same underlying `ActivateMonitors`/`DeactivateMonitors` + safety-guard codepath that Feature 3's rewritten `ToggleToNormalMode` will also route through — sequencing these in the same phase, or Feature 3 clearly before Feature 4, reduces the risk of two divergent "toggle a monitor" implementations.
- **Feature 2's audio-symmetry question should be resolved before implementation, not during it:** unlike the other four features, #2 has a genuine open architectural question (does `NormalAudioDeviceId` gain real runtime effect, matching Rig mode's `SetDefault` call, or does the optionality only gate a still-snapshot-based restore that the field itself doesn't otherwise influence). Recommend the roadmap surface this explicitly as a requirements-clarification item rather than letting an implementer decide it silently.
- **Features 1 and 2 have no hard dependency on 3/4/5** and could ship in an earlier phase independently — they are self-contained validation/guard-clause changes against the existing `AppSettings`/`ToggleService` shape.

## MVP-equivalent Scoping (this is a defined milestone, not a fresh MVP)

Since all 5 features are already scoped as "must ship this milestone" per PROJECT.md, there is no MVP-trim decision to make here — but ordering by dependency risk for phase sequencing:

1. **Features 1 & 2 (optional targets)** — lowest risk, no architectural prerequisites, good early-phase candidates to build confidence before touching the mode-detection redesign.
2. **Feature 3 (Normal-mode explicit config)** — do this before Feature 4; it forces the mode-detection and controller-unification decisions that Feature 4 then benefits from for free.
3. **Feature 4 (live panel)** — sequenced after 3 so it can reuse the unified apply/safety-guard path rather than introducing a third one.
4. **Feature 5 (safety guard)** — not a standalone phase; verify-and-extend as a cross-cutting concern threaded through 3 and 4's implementation and their test/verification passes, not a separate deliverable.

## Sources

- https://www.displayfusion.com/HelpGuide/WorkingWithDisplayFusionMonitorProfiles/ — Monitor Profiles as explicit named target sets, hotkey-switchable — MEDIUM confidence (vendor help guide, not independently corroborated elsewhere, but internally consistent and matches training-data knowledge of the product)
- https://www.displayfusion.com/Discussions/View/enable-disable-monitors-with-hotkey-or-rotate-profiles-with-single-hot-key/ — confirms confirmation-prompt-suppression setting exists and is opt-out (implying prompt-by-default) — MEDIUM confidence (community forum, single source)
- https://www.nirsoft.net/articles/turn-off-monitor.html and MultiMonitorTool product page — GUI checkbox + `Ctrl+F6/F7/F8` shortcuts, tray icon, CLI `/disable`/`/enable` — MEDIUM-HIGH confidence (NirSoft's own docs, cross-checked against multiple independent write-ups in search results)
- https://support.microsoft.com/en-us/windows/how-to-use-multiple-monitors-in-windows-329c6962-5a4d-b481-7baa-bec9671f728a — Windows Settings > Display's numbered-tile + Identify + Detect pattern — HIGH confidence (official Microsoft support doc)
- Windows' classic display-resolution-change "Keep these display settings?" 15-second auto-revert behavior — MEDIUM confidence (well-established, long-standing Windows UX pattern from training data; not independently re-verified via a fresh fetch this session, but extremely well-documented historically and low-risk to state as fact)
- https://github.com/File-New-Project/EarTrumpet (issues/discussions surveyed) — confirms unset/default-device audio switching is treated as a no-op convention, not an error path — MEDIUM confidence (GitHub issue discussions, not official docs, but consistent across multiple threads)
- Direct source inspection (this session): `/home/bpivk/moza/src/RigToggle.Core/ToggleService.cs`, `Models/AppSettings.cs`, `Models/StateSnapshot.cs`, `Models/MonitorState.cs`, `Models/AudioState.cs`, `Models/AudioRoleState.cs`, `/home/bpivk/moza/src/RigToggle.Windows/WindowsAudioController.cs`, `/home/bpivk/moza/src/RigToggle.Windows/WindowsMonitorController.cs`, `/home/bpivk/moza/src/RigToggle.App/SettingsForm.cs`, `/home/bpivk/moza/src/RigToggle.App/MainForm.cs` — HIGH confidence (primary source, current repo state as of this research)

---
*Feature research for: Rig Toggle v2.0 (Configurable Monitors, Optional Targets & Cleanup)*
*Researched: 2026-08-04*
