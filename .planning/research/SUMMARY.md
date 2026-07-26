# Project Research Summary

**Project:** Rig Toggle
**Domain:** Windows tray-resident automation utility — v1.1 milestone (tray residency/autostart, global hotkey, CLI trigger + IPC, toast notification, multi-monitor enable/disable) built on top of a shipped, rig-validated v1.0 single-monitor/single-audio-pair toggle app
**Researched:** 2026-07-26 (v1.1 milestone research; supersedes/extends the 2026-07-24 v1.0 research, which remains valid foundation and is not repeated in full here)
**Confidence:** MEDIUM-HIGH overall (HIGH for stack/architecture mechanics; MEDIUM for two items requiring rig hardware validation)

## Executive Summary

v1.1 adds five features to an already-shipped, rig-validated WinForms utility: tray residency + autostart, a global hotkey trigger, a CLI trigger with single-instance IPC signaling, toast/status notifications, and generalized multi-monitor enable/disable sets (replacing the single "primary monitor to disable" setting). All four research passes agree this milestone needs **zero new third-party NuGet packages** — every feature is achievable with WinForms built-ins (`NotifyIcon`), BCL types (`System.IO.Pipes`, `Microsoft.Win32.Registry`, `System.Threading.Mutex`), hand-rolled `user32.dll` P/Invoke, and the already-referenced `WindowsDisplayAPI` package used in a broader mode (querying/applying inactive as well as active display paths). This is consistent with the project's established preference for minimal dependencies over heavier SDKs.

The recommended approach layers cleanly onto the existing architecture (`RigToggle.Core` orchestration + models, `RigToggle.Windows` Win32/COM adapters, `RigToggle.App` UI shell/composition root): new tray/hotkey/IPC/notification code belongs in `RigToggle.App` (UI-shell concerns), the multi-monitor generalization changes `IMonitorController`'s signature and `AppSettings`/`MonitorState` models (Core + Windows), and no new Core interface is warranted for notifications (a plain method tied to the `NotifyIcon` instance is sufficient — introducing `INotificationService` would be ceremony with one real implementation). The single highest-leverage piece of shared prep work is extracting `MainForm.BtnToggle_Click`'s inline confirm-toggle-report logic into a reusable, owner-optional orchestration helper — every new trigger (tray menu, hotkey, CLI/IPC) needs to run that same pipeline, and building it three more times inline is exactly the kind of drift that produced this project's own prior H9 regression.

Two categories of risk require rig hardware validation rather than being assumed safe from API-contract reasoning alone, and all three of Stack/Architecture/Pitfalls research independently flagged both: (1) **re-enabling a monitor that has been OS-disabled across a sleep/wake cycle or full reboot** — Microsoft's own docs confirm inactive CCD paths return with mode-index information marked invalid, so "does it come back, and at what resolution" is not guaranteed by the API contract alone; (2) **combined disable+enable topology construction** — composing what were two independently-reasoned single-target operations into one atomic `SetDisplayConfig` call is a materially harder topology-construction problem than the single-primary-removal case v1.0 actually validated. Both should get a dedicated go/no-go rig checkpoint mirroring v1.0 Phase 1's spike-first approach, not be folded into "the existing Disable/Restore already work" confidence. Separately — and this is the single most consequential cross-cutting implication of this research — **no reentrancy guard exists in `ToggleService` today**, which was safe when the GUI button was the only trigger but becomes a real correctness risk once tray menu, hotkey, and CLI can all invoke a toggle concurrently; this needs one explicit, up-front design decision (a guard in the shared orchestration helper), not a per-feature bolt-on.

## Key Findings

### Recommended Stack

No new packages. The five features are covered by: `System.Windows.Forms.NotifyIcon` (tray icon, context menu, minimize-to-tray, `ShowBalloonTip` notifications); `user32.dll` P/Invoke `RegisterHotKey`/`UnregisterHotKey` + `WM_HOTKEY` against a live window handle (`MainForm`'s, kept alive even while hidden); `Microsoft.Win32.Registry` (`HKCU\...\Run`) for autostart; `System.Threading.Mutex` (named, `Local\` scope) + `System.IO.Pipes` (`NamedPipeServerStream`/`ClientStream`, **must** set `PipeOptions.CurrentUserOnly` on both ends — a confirmed real gotcha, not theoretical) for single-instance detection and CLI-to-resident-instance signaling; and `WindowsDisplayAPI` 1.3.0.13's `PathInfo.GetAllPaths(onlyActivePaths: false)` (already confirmed via source read to wrap `QDC_ALL_PATHS`) for multi-monitor enable/disable, including enumerating currently-inactive targets.

**Core technologies (v1.1 additions):**
- `NotifyIcon` (WinForms built-in) — tray icon, context menu, minimize-to-tray, and `ShowBalloonTip` notifications, zero dependency
- `RegisterHotKey`/`WM_HOTKEY` P/Invoke — global hotkey, must run on the UI thread that owns the registering window
- `Microsoft.Win32.Registry` (`HKCU\...\Run`) — autostart, non-elevated, matches existing execution model
- Named `Mutex` + `NamedPipeServerStream`/`ClientStream` (`CurrentUserOnly` required) — single-instance detection + CLI IPC
- `WindowsDisplayAPI` (already referenced), used in broader mode — query/apply inactive display paths for multi-monitor enable/disable

**Reconciled disagreement 1 — toast library:** FEATURES.md initially recommended `CommunityToolkit.WinUI.Notifications`; STACK.md found that package is now **archived** and recommends `NotifyIcon.ShowBalloonTip` instead (zero dependency); PITFALLS.md independently confirms unpackaged/non-shortcut-registered apps silently get **no toast at all** without AUMID + Start Menu shortcut registration — and that this failure mode specifically appears in the **published, self-contained single-file exe** while sometimes appearing to work under a VS debug session, which is exactly this project's distribution mode. **Resolved toward `NotifyIcon.ShowBalloonTip`** as the default — zero new dependency, zero AUMID/shortcut work, acceptable fidelity (transient banner; Windows 11 doesn't persist it to Action Center history, which is fine since this is a live confirmation, not a durable log — LOG-01 stays deferred anyway). Do not build the AUMID/shortcut path preemptively.

**Reconciled disagreement 2 — autostart mechanism:** STACK.md recommends a plain `HKCU\...\Run` key (non-elevated, simplest, matches existing non-elevated execution model); PITFALLS.md's open questions lean toward Task Scheduler "At log on" with a startup delay, out of concern for driver-readiness timing, but explicitly flags this as untested/not rig-validated. **Resolved toward the Registry Run key as the default** — simplest, matches the app's existing non-elevated posture, and Task Scheduler's main advantage (silent elevation) is irrelevant here since the app deliberately never elevates. Only revisit Task Scheduler with a startup delay if plain autostart is found, on a real reboot, to race the Moza Companion driver/USB stack — do not build the more complex option preemptively without evidence it's needed.

Packaging is unchanged from v1.0: `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`, untrimmed.

### Expected Features

**Must have (table stakes for this milestone):**
- Close (X) minimizes to tray, does not exit; explicit "Exit" always available in the tray menu
- Tray icon reflects current mode; left-click vs. right-click behavior distinct
- Autostart is an explicit Settings checkbox, off by default
- Hotkey has a visible current binding, a way to rebind/clear it, and registration failures are surfaced (not silently swallowed — `RegisterHotKey` can fail if another app, e.g. Moza Companion or other rig software, already owns the combo)
- CLI trigger works whether or not an instance is already running (launch-or-signal transparently)
- Notification appears only for headless-trigger paths (hotkey/CLI/tray-while-hidden), not redundantly alongside the already-visible GUI click path
- Multi-monitor Settings UI prevents "disable everything" configurations (Windows CCD refuses to disable every display)
- Confirmation dialog names *all* monitors being disabled/enabled, not just one

**Should have (differentiators for this single-user tool):**
- Left-click tray icon = instant toggle (matches Core Value even more tightly than the old GUI-button click)
- Notification shows *what changed* (mode + affected devices), not just "Toggled" — reuses existing `ToggleResult`/checklist data
- Partial-failure surfaced via notification when triggered headlessly (parity with the GUI's existing partial-failure MessageBox)
- Enable-set and disable-set both independently restorable to whatever was true before, not a fixed on/off — architectural extension of the existing snapshot/restore mechanism, not a new UI feature
- CLI `--status` query verb — cheap once the IPC channel exists, not required this milestone

**Defer / anti-features (do not build):**
- Elevated/Task-Scheduler autostart — would silently reintroduce the UIPI cross-process-focus problem the H9 debug session already worked around
- Hotkey chord/sequence engine — unused complexity for a single binding, single action
- Full Windows App SDK / MSIX toast packaging — conflicts with the standalone-.exe distribution constraint
- Per-monitor sets keyed by index/position instead of stable `DevicePath` — already burned once in v1.0, must not be reintroduced at N-monitor scale
- CLI trigger that force-launches a brand-new process per invocation — defeats tray residency and risks concurrent file access on `settings.json`/`state.json`
- Toggle history/log (LOG-01) — already deferred in PROJECT.md, unrelated to this milestone

### Architecture Approach

The existing three-project split holds: `RigToggle.Core` (zero Windows API references, enforced) owns `ToggleService` and models; `RigToggle.Windows` implements Core's adapter interfaces via P/Invoke and COM/CCD; `RigToggle.App` is the UI shell and composition root. New tray/hotkey/IPC/notification code belongs in `RigToggle.App`, **not** `RigToggle.Windows`, even though `RigToggle.Windows.csproj` already has `UseWindowsForms=true` — that flag is there only because of the `WindowsDisplayAPI` dependency chain, not because that project owns any UI; it currently has zero WinForms controls and should stay that way. No new Core `INotificationService` interface — a toast is inseparable from the `NotifyIcon` instance that only exists in `RigToggle.App`, so it's a plain method there, not a Core abstraction with a single real implementation.

**Major components (new/changed for v1.1):**
1. **Shared toggle-orchestration helper** (new, `RigToggle.App`) — extracts `MainForm.BtnToggle_Click`'s inline confirm-toggle-report logic into a reusable, trigger-source-aware method (`Button`/`Hotkey`/`TrayMenu`/`Cli`) so all four entry points run identical logic and only differ in whether a modal confirmation is shown vs. skipped, and MessageBox vs. toast reporting.
2. **`TrayIcon`** (new, `RigToggle.App`) — owns `NotifyIcon` + `ContextMenuStrip`, minimize-to-tray via `FormClosing` cancel+`Hide()`, and `ShowToggleNotification` built from existing `ToggleResult` data.
3. **Hotkey P/Invoke + `MainForm.WndProc` override** (new, `RigToggle.App`) — `RegisterHotKey`/`WM_HOTKEY` against `MainForm.Handle`, alive independent of visibility.
4. **Single-instance `Mutex` + named-pipe server/client, restructured `Program.Main`** (new, `RigToggle.App`) — CLI process-lifecycle handling, entirely new subsystem with no v1.0 precedent.
5. **`IAutostartService` (Core interface) / `WindowsAutostartService` (Windows implementation)** — Registry `Run` key read/write, following the existing interface-in-Core/implementation-in-Windows pattern.
6. **Generalized `IMonitorController`** — `Disable(IReadOnlyList<string>)` (breaking signature change) plus new `Enable(IReadOnlyList<string>)` and `GetAllMonitors()` (active+inactive) methods; recommend refactoring `WindowsMonitorController` around one shared internal primitive (e.g. `ApplyExactActiveSet`) that `Disable`/`Enable`/`Restore` all call, rather than three independently-evolving methods.

**Build order (reconciled across Architecture and Features research):** multi-monitor's core data-model/interface changes (`AppSettings` fields, `IMonitorController` signature) should land early since they ripple into every other trigger's confirmation-dialog call sites — but this can proceed **in parallel** with extracting the shared toggle-orchestration helper, since these touch different layers (Core/Windows vs. App). Then tray residency + autostart (foundation for "headless" triggers) ships together with toast notification (which hard-depends on the `NotifyIcon` instance tray residency creates). Then global hotkey (depends on tray's keep-alive-on-close for real background usefulness). Then CLI trigger last (depends on tray's resident-instance model for "signal an already-running instance" to have any point, and benefits from the orchestration helper already being exercised by two other trigger paths before the highest-risk IPC path lands).

### Critical Pitfalls

1. **Toast notifications silently do nothing on the published exe without AUMID/shortcut registration** — a confirmed, documented failure mode for unpackaged apps that can appear to work in a VS debug session and then silently fail specifically in the shipped single-file exe. Avoid by defaulting to `NotifyIcon.ShowBalloonTip` (zero AUMID/shortcut requirement) and, if ever revisiting real toasts, always testing against the **published** exe from its real install location.
2. **Re-enabling a monitor that's been OS-disabled across a sleep/wake cycle or reboot is an unvalidated, architecturally distinct scenario** from anything v1.0 tested — `GetAllPaths()` confirms inactive paths are enumerable, but Microsoft's own docs confirm their mode-index info is marked invalid (no stored resolution/position). Design `Enable()` as a new operation, not a `Restore()` variant, and require a dedicated rig checkpoint (disable, sleep/reboot, attempt enable, confirm resolution) before shipping.
3. **Combined disable+enable topology construction multiplies CCD risk** beyond the single-primary-removal case v1.0 validated — `SetDisplayConfig` validates the entire proposed topology atomically, so composing two independently-reasoned operations risks overlapping positions or no GDI primary. Design as one deliberate, tested code path with a dedicated rig go/no-go checkpoint using the real configured sets, not synthetic single-monitor tests.
4. **No reentrancy guard exists in `ToggleService` today** — safe only because the GUI button was the sole synchronous trigger; tray menu, hotkey, and CLI/IPC each add an independent entry point that can now invoke a toggle concurrently or in rapid double-fire succession, risking a second `CaptureState()` running mid-flight of a first call's mutation and corrupting the snapshot the whole app depends on for correct restore. This needs one explicit guard (lock/busy-flag) checked by every trigger path, decided up front — not discovered as a bug later.
5. **`RegisterHotKey` conflicts silently with other rig software** — Moza Companion and other sim-racing peripheral tools are known to register their own global hotkeys; a silent `FALSE` return with no UI feedback makes the feature appear to "do nothing" specifically on the real, fully-loaded rig PC while working in isolated dev testing. Always check the return value, surface conflicts in Settings, and rig-test with Moza Companion actually running.

## Implications for Roadmap

Based on combined research, suggested phase structure for v1.1 (five features plus one cross-cutting prerequisite):

### Phase 1: Multi-Monitor Data Model & Controller Generalization
**Rationale:** Changes `AppSettings`, `MonitorState`, `MonitorInfo`, and `IMonitorController`'s signature — every other new trigger path (hotkey, tray menu, CLI) will call into `ToggleService`/the confirmation dialog, so it's cheaper to build those call sites once against the final multi-monitor-aware shape than to build against the old single-monitor shape and revisit later. Also the highest-CCD-risk change, benefiting from isolation from unrelated tray/hotkey/CLI work during debugging. Can proceed in parallel with Phase 2 (different layers: Core/Windows vs. App).
**Delivers:** `MonitorsToDisable`/`MonitorsToEnable` (plural lists) replacing `MonitorDevicePath`, `IMonitorController.Disable(IReadOnlyList<string>)`/`Enable(IReadOnlyList<string>)`/`GetAllMonitors()`, a shared `ApplyExactActiveSet`-style internal primitive, plural `MonitorConfirmDialog`, and **the one-time settings migration in `JsonSettingsStore.Load()`** that seeds `MonitorsToDisable` from the legacy singular `MonitorDevicePath` field so existing v1.0 installs don't silently lose their configured monitor on upgrade.
**Addresses:** Multi-monitor enable/disable configuration (PROJECT.md v1.1 scope)
**Avoids:** Pitfalls 2, 3, 5 (architecture research) — anti-pattern of implementing multi-monitor disable as a loop of single-target calls
**Requires a dedicated rig checkpoint** (mirroring v1.0 Phase 1's spike-first approach) before being considered production-ready: (a) disable-sleep/wake or reboot-enable round-trip, confirming the target is still enumerable and comes back at a sane resolution; (b) combined disable+enable topology applied together, confirming exactly one GDI primary results and no position overlap.

### Phase 2: Shared Toggle-Orchestration Helper Extraction
**Rationale:** `MainForm.BtnToggle_Click` today inlines confirm-dialog, toggle invocation, and MessageBox reporting, coupled to a visible owning window. Every one of the next three features (tray menu, hotkey, CLI) needs to run the identical pipeline; building it three more times inline risks the exact kind of drift (GUI click behaves differently than hotkey) that produced this project's own prior H9 regression. Refactor against the now-final multi-monitor confirmation-dialog shape from Phase 1.
**Delivers:** A trigger-source-aware orchestration method (e.g. `Execute(TriggerSource)`) deciding, in one place, whether to show the modal confirmation (interactive triggers only) and how to report outcomes (MessageBox vs. notification, once Phase 3 exists).
**Uses:** No new stack elements — pure refactor of existing `ToggleService` call sites.
**Cross-cutting requirement to resolve here, not later:** add the reentrancy guard (lock/busy-flag) in this helper (or in `ToggleService` directly) so every subsequent trigger path is safe by construction — this is the single most consequential design decision surfaced across all four research files and must not be a per-feature bolt-on.

### Phase 3: Tray Residency, Autostart & Toast Notification
**Rationale:** These ship together in practice — toast notification hard-depends on the `NotifyIcon` instance tray residency creates, and this phase establishes the "don't exit on close" behavior every remaining feature's headless/background usefulness depends on.
**Delivers:** `TrayIcon` (icon + context menu: Switch mode/Settings/Exit), minimize-to-tray via `FormClosing` cancel+`Hide()`, `IAutostartService`/`WindowsAutostartService` (Registry `Run` key, re-written from current `Environment.ProcessPath` on every startup to self-heal if the exe moves), Settings checkboxes, and `ShowToggleNotification` via `NotifyIcon.ShowBalloonTip` gated on trigger source from Phase 2.
**Implements:** `TrayIcon` component, `IAutostartService` (Core interface/Windows implementation pattern)
**Avoids:** Pitfall 1 (toast/AUMID trap — resolved by using `ShowBalloonTip`), Pitfall 6 (window destroyed instead of hidden, silently killing hotkey registration later), Pitfall 10 (stale Run-key path), Pitfall 12/13 (ghost tray icon, duplicated menu handlers)

### Phase 4: Global Hotkey Trigger
**Rationale:** Depends on Phase 2's orchestration helper and benefits from Phase 3's keep-alive-on-close (so the hotkey remains useful once the window is hidden), though the `RegisterHotKey` call itself only needs a live window handle, which `MainForm` has regardless.
**Delivers:** `RegisterHotKey`/`WM_HOTKEY` P/Invoke, `MainForm.WndProc` override forwarding to the Phase 2 helper, hotkey-capture Settings control, explicit surfacing of `RegisterHotKey` return-value failures.
**Implements:** Hotkey P/Invoke component (App layer, not Windows layer — see Anti-Pattern 2 in ARCHITECTURE.md)
**Avoids:** Pitfall 7 (silent conflict with Moza Companion/other rig software's own hotkeys — must rig-test with that software running) and Pitfall 6 (must survive a minimize-to-tray cycle)

### Phase 5: CLI Trigger + Single-Instance IPC
**Rationale:** Highest technical risk of the six features and the one subsystem with no v1.0 precedent at all (process lifecycle, Mutex, named pipes). Depends on Phase 3's resident-instance model (without it, "signal an already-running instance" has no point) and Phase 2's orchestration helper. Sequenced last so the pipe-server's command handler calls into a helper already exercised by two other real trigger paths, reducing the chance of an undiscovered edge case (e.g. confirmation-on-a-headless-trigger).
**Delivers:** Named `Mutex` (`Local\` scope) + `NamedPipeServerStream`/`ClientStream` (`PipeOptions.CurrentUserOnly` on both ends), restructured `Program.Main` (parse args, try acquire mutex, signal-or-launch-and-execute), explicit decided behavior for all four combinations of (resident running/not) x (autostart on/off).
**Avoids:** Pitfall 4 (reentrancy — relies on Phase 2's guard), Pitfall 8 (undefined no-resident-instance behavior), Pitfall 9 (IPC timeout mismatched against the toggle's real multi-second duration — size generously or acknowledge receipt immediately rather than blocking the CLI client)

### Phase Ordering Rationale

- Phase 1 (multi-monitor) is deliberately first among the five user-facing features because it changes shapes (`AppSettings`, `IMonitorController`) that every other trigger's confirmation/orchestration code will call into — cheaper to build once against the final shape.
- Phase 2 (shared orchestration helper) is the load-bearing prerequisite for Phases 3-5; skipping straight to feature work risks three divergent copies of confirm/report logic.
- Phases 3 through 5 follow a dependency chain each subsequent phase actually needs: tray residency's keep-alive enables the hotkey's background usefulness, which in turn is fully exercised before CLI (the highest-risk, most novel subsystem) lands.
- Both multi-monitor's "enable a long-disabled monitor" and "combined disable+enable topology" capabilities need their own rig-validation checkpoint before being called done, mirroring the exact go/no-go discipline v1.0 Phase 1 used for the original single-monitor disable — don't treat this milestone's higher-risk CCD scenarios as safe by inference from v1.0's success.
- The reentrancy guard is called out explicitly as a Phase 2 deliverable (not deferred to whichever feature phase "notices" the problem) because it's a cross-cutting invariant that all of Phases 3-5 depend on being correct.

### Research Flags

Needs research/rig-validation during planning:
- **Phase 1 (Multi-Monitor):** The two flagged rig checkpoints (long-idle/reboot enable recovery, combined disable+enable topology) have no documentation-only answer — genuinely needs hands-on hardware validation before being considered shippable, exactly like v1.0 Phase 1's original monitor-disable spike.
- **Phase 5 (CLI + IPC):** Standard, well-understood .NET pattern (Mutex + named pipe) per all four research files — the design decisions (reentrancy behavior, no-resident-instance behavior, IPC timeout sizing) are the risk, not the mechanism itself; a short design pass during planning should resolve these rather than requiring external research.

Phases with standard, well-documented patterns (skip research-phase):
- **Phase 2 (Orchestration helper extraction):** Pure refactor of existing, already-read code; no new API surface.
- **Phase 3 (Tray residency + autostart + toast):** `NotifyIcon`, `FormClosing` override, and Registry `Run` key are all long-stable, well-documented WinForms/Win32 patterns with no rig-hardware dependency.
- **Phase 4 (Global hotkey):** `RegisterHotKey`/`WM_HOTKEY` is a decades-stable Win32 API; the only project-specific risk (conflict with Moza Companion's own hotkeys) is a rig-test item, not a research gap.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH for mechanisms (all verified against official Microsoft Learn docs and direct source reads of `WindowsDisplayAPI`); MEDIUM for the multi-monitor long-disabled-enable direction, which depends on graphics-driver behavior not verifiable from documentation alone |
| Features | MEDIUM-HIGH | Patterns cross-checked against Win32/.NET official docs and multiple independent OSS/commercial reference implementations (EarTrumpet, DisplayFusion); several judgment calls (e.g. left-click-to-toggle) explicitly flagged as single-user-tool opinion, not externally validated |
| Architecture | HIGH | All claims about current codebase architecture verified by direct source reads of this repo (not assumed from convention); the five feature-integration plans and three anti-patterns are grounded in that verified baseline |
| Pitfalls | HIGH for the toast/AUMID trap and reentrancy gap (confirmed via official docs and direct code read, respectively); MEDIUM for the two CCD rig-validation items (API contract is HIGH confidence, but driver-level behavior at reboot/sleep-wake time scale is explicitly unverified) |

**Overall confidence:** MEDIUM-HIGH — the tray/hotkey/CLI/autostart mechanisms are all HIGH confidence, well-trodden Win32/.NET patterns; the two multi-monitor CCD scenarios (long-idle enable, combined topology) are the only areas requiring empirical rig validation before being considered settled, consistent with how v1.0's original monitor-disable capability was also gated behind a hardware spike rather than accepted from documentation alone.

### Gaps to Address

- **Multi-monitor long-disabled-monitor enable recovery** (sleep/wake and full-reboot time scales) is unverified by documentation alone and needs a dedicated rig round-trip test before the feature ships — treat as a go/no-go checkpoint within Phase 1, not an assumption.
- **Combined disable+enable topology construction** (multiple monitors changing state in one atomic `SetDisplayConfig` call) is a materially harder case than anything v1.0 validated — needs its own rig checkpoint with the real configured sets, not synthetic single-monitor tests, within Phase 1.
- **Reentrancy guard design** (lock vs. busy-flag vs. queue-and-run) is not fully specified by research — a concrete decision (reject with "toggle already in progress" vs. queue) should be made explicitly during Phase 2 planning, not left implicit.
- **`ToggleService.IsFullyConfigured` validation rule** for the new list-typed monitor settings (should an empty disable-set now be allowed, given the tool generalizes beyond "always disable exactly one monitor"?) is flagged by ARCHITECTURE.md as an open design question, not resolved by research — needs a decision during Phase 1 planning.
- **Modal Settings dialog vs. hotkey/CLI/tray-menu delivery** — WinForms modal dialogs still pump `WM_HOTKEY` messages, so a hotkey press while Settings is open could fire a toggle mid-edit, something impossible in v1.0 (single trigger source blocked while Settings is modal). Needs an explicit decision (suppress vs. queue) during Phase 4 planning, not left as an untested edge case.
- **Settings migration correctness** (seeding `MonitorsToDisable` from legacy `MonitorDevicePath` in `JsonSettingsStore.Load()`) is a concrete, testable task, not just new-feature scope — should have its own acceptance test loading a genuine v1.0-era `settings.json` file during Phase 1.

## Sources

### Primary (HIGH confidence)
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig and .../nf-winuser-setdisplayconfig — official CCD reference confirming `QDC_ALL_PATHS` inactive-path enumeration and mode-index-invalid limitation, and `SDC_ALLOW_CHANGES`/best-mode-logic recovery behavior
- https://github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathInfo.cs — source-verified `GetAllPaths(onlyActivePaths: false)` and `ApplyPathInfos` flag usage
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon.showballoontip — confirmed API surface and deprecated `timeout` parameter behavior
- https://learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid — official confirmation of the AUMID/Start-Menu-shortcut requirement for desktop-app toast notifications (unpackaged apps)
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey — official `RegisterHotKey`/`WM_HOTKEY`/thread-ownership reference
- Direct source reads of this repository (`src/RigToggle.Core/**`, `src/RigToggle.Windows/**`, `src/RigToggle.App/**`, all `.csproj` files) — ground truth for current architecture, confirmed absence of reentrancy guard, `FormClosing` handling, and single-instance detection
- `.planning/PROJECT.md` and `.planning/milestones/v1.0-phases/01-monitor-disable-feasibility-spike/01-RESEARCH.md` + `01-VERIFICATION.md` — v1.1 scope and the project's own established go/no-go rig-spike methodology
- `.planning/debug/resolved/moza-foreground-focus.md` — the 10-round debug session establishing "raw external state-mutation on a resource you don't fully control can desync unpredictably" as a transferable lesson

### Secondary (MEDIUM confidence)
- https://medo64.com/posts/single-instance-application-for-net-6-or-7 and https://www.autoitconsulting.com/site/development/single-instance-winform-app-csharp-mutex-named-pipes/ — Mutex + named-pipe single-instance/IPC pattern, including the `PipeOptions.CurrentUserOnly` gotcha
- https://github.com/Ivy-Interactive/Rustino/issues/11 and community csharpforums.net thread — corroborating reports of the "works in dev/IDE, silently fails in published exe" toast trap
- CommunityToolkit.WinUI.Notifications NuGet listing / GitHub issue threads — confirmed the package is archived, informing the reconciled toast-library decision
- howtoguides.org autostart-mechanism comparison, cross-checked against Microsoft's own Startup Apps documentation concepts — used for the Registry-Run-key-vs-Task-Scheduler comparison
- https://www.displayfusion.com/Discussions/View/how-to-disable-all-monitors/ — community confirmation that Windows CCD refuses to disable every display

### Tertiary (LOW confidence)
- Community examples (lostindetails.com, sudhirdotnet blog) for `NativeWindow`/hidden-window `WM_HOTKEY` pattern — corroborates an officially-documented requirement but is itself not an official source
- Exact behavior of "best mode logic" reconstructing a long-idle monitor's true native resolution across a full reboot — explicitly flagged by STACK.md as not verifiable from documentation, driver-dependent, and the reason Phase 1's rig checkpoint is required

---
*Research completed: 2026-07-26*
*Ready for roadmap: yes*
