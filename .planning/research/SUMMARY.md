# Project Research Summary

**Project:** Rig Toggle
**Domain:** Windows-only desktop GUI utility — single-user hardware-profile toggle (display CCD control, default audio device switching, companion-process management)
**Researched:** 2026-07-24
**Confidence:** MEDIUM-HIGH

## Executive Summary

Rig Toggle is a narrow, single-purpose composition of three well-understood but individually tricky Windows automation techniques: true CCD-level monitor disable (not power-off), undocumented-COM-interface default audio switching, and cross-process window management — wrapped in a two-state (normal/rig) snapshot-and-restore toggle. No single existing tool (DisplayFusion, SoundSwitch, NirSoft MultiMonitorTool/SoundVolumeView) does this exact combination, but every individual building block has multiple mature, actively-maintained reference implementations to draw from. The recommended stack is .NET 10 + WinForms, self-contained single-file published, using the `WindowsDisplayAPI` NuGet wrapper for CCD calls, a hand-embedded `IPolicyConfig` COM interop shim for audio switching, and NAudio for device enumeration — all justified by direct source-code verification (not just documentation) of how the CCD API's topology-path-removal actually achieves a true display detach.

The recommended architecture isolates all Windows interop (P/Invoke, COM, cross-process window calls) behind three small adapter interfaces (`IMonitorController`, `IAudioController`, `IAppController`) so the orchestration logic (`ToggleService`) and GUI are fully unit-testable against fakes, and the highest-risk component — monitor disable — can be swapped or fixed without touching anything else. Mode ("normal" vs "rig") should be derived purely from whether a persisted state snapshot exists on disk, never tracked as an independent flag, which eliminates an entire class of crash/restart desync bugs.

The single biggest risk, called out consistently across all four research files, is that there is no officially documented public API for "make Windows treat this monitor as disconnected." The commonly-used technique (supplying `SetDisplayConfig` a topology array that omits the target monitor's path) is confirmed only by reading library source code and community tooling, not Microsoft documentation, and its behavior may vary by GPU vendor/driver. This must be validated as a throwaway spike against the actual rig hardware before any other architecture or GUI work is treated as settled — if it doesn't achieve a true detach (vs. just a power-off), the project's entire premise needs re-evaluation. Secondary risks (audio device ID instability, undocumented COM interface fragility, UIPI/elevation conflicts with cross-process window focus, and SmartScreen/AV flagging of an unsigned self-contained .exe) are all well-understood with established mitigation patterns and lower severity.

## Key Findings

### Recommended Stack

.NET 10 (LTS, GA Nov 2025) with C# 13 and WinForms is the recommended core stack — .NET has the richest Win32/COM interop story of any managed stack, which matters here because all three core features are native Windows APIs with no cross-platform equivalent. WinForms is recommended over WPF for lowest-friction data binding to enumerated monitors/audio devices/processes and smallest self-contained publish footprint, though WPF is an equally valid alternative if nicer visuals matter more than minor friction — the choice doesn't affect the architecture's testability or interop isolation.

**Core technologies:**
- .NET 10 + C# 13: runtime/language — richest Win32 P/Invoke + COM interop support, 3 years of LTS patching
- WinForms: GUI framework — lowest friction for a 2-screen personal utility, smallest self-contained publish size
- `WindowsDisplayAPI` (falahati, NuGet, LGPLv3): managed CCD API wrapper — verified via source read that `PathInfo.ApplyPathInfos()` calls `SetDisplayConfig` with the exact flags needed for true topology-level detach
- Hand-embedded `IPolicyConfig` COM interop (~100 lines, not a NuGet dependency): the only way to programmatically set the Windows default audio playback device — no public API exists; embed rather than depend on the unmaintained `AudioSwitcher.AudioApi` package (last released May 2023)
- NAudio 2.3.0: audio endpoint enumeration only (not the "set default" call) — actively maintained, MIT licensed
- `System.Text.Json` (BCL): settings/snapshot persistence to `%LocalAppData%\RigToggle\`

Packaging: `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true`, with `PublishTrimmed=false` explicitly — trimming's static analysis is known to break COM-interop/P-Invoke-heavy code like this app's core features.

### Expected Features

Rig Toggle sits at the intersection of monitor-profile switchers, audio-device switchers, and rig-automation utilities, and its full v1 scope is already captured as "Active" requirements in PROJECT.md — feature research confirms this scope matches ecosystem norms with no missing table-stakes items.

**Must have (table stakes):**
- One-click GUI toggle (both directions)
- True OS-level monitor disable/enable (not power-off) — the single most technically load-bearing feature
- Default audio output device switch
- State snapshot captured at toggle-time, exact restore on toggle-back
- Companion-app launch-or-focus with duplicate-instance prevention
- Best-effort minimize of companion app on toggle-back
- Settings screen (monitor, audio device pair, app path pickers)
- Basic failure feedback (not silent no-op)
- Standalone self-contained .exe

**Should have (v1.x, correctly deferred per PROJECT.md):**
- Global hotkey trigger
- System tray residency + autostart
- Confirmation dialog before disabling primary monitor (cheap insurance against self-lockout — consider pulling into v1 if a lockout incident occurs during testing)
- Toast notification on toggle
- Toggle history/log

**Defer (v2+):**
- Auto-trigger on game/app launch — reintroduces the exact display-detection-timing problem this tool exists to solve manually
- Visual persistent mode indicator (needs tray residency first)
- Anti-features to actively avoid: N-way profile manager, per-app auto-switch rules engine, plugin/scripting system, multi-user config, auto-update, telemetry, licensing/DRM, cloud sync — all wrong-shaped for a single-user, two-state, single-machine personal tool

### Architecture Approach

A thin layered desktop app with strict separation between GUI, orchestration (`ToggleService`), OS-interop adapters, and JSON persistence — not a generic n-tier structure. The core rule: `RigToggle.Core` (orchestration + models) has zero Windows API references, so the actual business logic (snapshot to mutate to restore sequencing, partial-failure handling) is fully unit-testable with fakes, while all P/Invoke/COM risk is isolated in a separate `RigToggle.Windows` project.

**Major components:**
1. `ToggleService` (orchestration) — owns the snapshot to mutate to restore state machine; sequences monitor, audio, app actions; treats each step's success/failure independently rather than as an all-or-nothing block
2. `IMonitorController` / `IAudioController` / `IAppController` (adapters) — one interface per OS subsystem, each with exactly one real implementation living in `RigToggle.Windows`; enables fakes for testing and isolates the riskiest component (monitor disable) for replacement without touching the rest of the app
3. `SettingsStore` / `SnapshotStore` (persistence) — JSON files; critically, "current mode" is derived from snapshot-file presence, never tracked as a separate flag, eliminating flag/reality desync after crashes or restarts

Suggested build order deliberately separates "clean dependency order" from "risk-first validation order": data contracts and persistence first (zero OS dependency), then a GUI shell wired to fake controllers (validates full UX with no hardware needed), then real adapters in ascending risk order (app/process control, audio, monitor), with orchestration wiring assembled last from already-validated parts. The monitor-disable feasibility question, however, should be spiked in parallel with or even before the GUI shell, since it is the project's core value proposition and has no documented API guarantee.

### Critical Pitfalls

1. **No public CCD "disconnect" API exists** — the naive implementation just powers the monitor off (DPMS-style) while Windows still lists it as active, which doesn't fix the BeamNG-style misbehavior this project exists to solve. Avoid by prototyping topology-path-removal via `SetDisplayConfig` against real rig hardware as an early throwaway spike, with device-node disable (`CM_Disable_DevNode`, requires elevation) as a documented fallback only if path-removal proves insufficient.
2. **Elevation requirements differ per subsystem, and requesting admin broadly breaks cross-process window control** — UIPI prevents an elevated process from reliably focusing a non-elevated process's window (Moza Companion). Avoid by defaulting to `asInvoker`, determining actual per-operation elevation needs empirically, and isolating any genuinely-required elevated call in a short-lived helper process rather than elevating the whole GUI app.
3. **Operations report success without actually taking effect** — `SetDisplayConfig` and audio APIs can return success while the real system state doesn't change (invalid flag combos silently ignored, one of three audio roles not actually switched). Avoid by re-querying actual state after every mutating call and surfacing a specific "didn't actually change" error rather than trusting return codes.
4. **Incomplete state snapshot breaks exact restoration** — capturing only "which monitors are active" loses primary designation, position, and per-role audio defaults, producing a restore that "looks right" once but drifts on later toggles. Avoid by snapshotting the full `QueryDisplayConfig` output and all three audio roles, and testing round-trip fidelity with a non-default monitor arrangement, not just the default side-by-side layout.
5. **Companion-app window focus/launch races the target app's own startup time, and single COM-object leaks accumulate across repeated toggles in one session** — a single immediate `FindWindow` check after `Process.Start()` frequently fails; COM RCWs for `IPolicyConfig`/`IMMDeviceEnumerator` must be explicitly released each toggle cycle or audio switching degrades after N toggles in one running session.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Monitor-Disable Feasibility Spike
**Rationale:** This is the project's entire core value proposition and the one component with no officially documented API guarantee (Pitfall 1). If topology-path-removal doesn't achieve a true detach on the actual rig hardware/GPU driver, the whole architecture needs to be re-evaluated before any other work is sunk in. Architecture research explicitly recommends validating this before or in parallel with GUI work, not last, despite it being last in "clean" build order.
**Delivers:** A go/no-go answer on whether `SetDisplayConfig` topology-path-removal (via `WindowsDisplayAPI` or raw P/Invoke) genuinely removes the primary monitor from `EnumDisplayMonitors`/Display Settings, and whether the actual problem game (BeamNG-style misbehavior) is resolved — not just that the screen goes black.
**Addresses:** The "true OS-level monitor disable/enable" requirement from PROJECT.md/FEATURES.md
**Avoids:** Pitfall 1 (no public disconnect API), and surfaces early whether Pitfall 2 (elevation) or Pitfall 3 (windows-on-target-monitor race) apply to the chosen mechanism

### Phase 2: Foundations — Data Contracts & Persistence
**Rationale:** `AppSettings` and `StateSnapshot` models and their JSON stores have zero Windows API dependency and are trivial to unit test; every other component's shape flows from these models, so getting the schema right first avoids rework in the adapters.
**Delivers:** `SettingsStore`/`SnapshotStore` JSON persistence, settings/snapshot POCOs designed from the start to hold full display topology (path + mode arrays, primary designation) and all three audio roles — not just booleans (directly prevents Pitfall 7)
**Uses:** `System.Text.Json` (BCL), `%LocalAppData%\RigToggle\`

### Phase 3: GUI Shell Against Fake Controllers
**Rationale:** Validates the full UX (settings round-trip, toggle button, mode indicator, snapshot-derived mode-on-startup) with zero hardware risk, using hand-written fakes for `IMonitorController`/`IAudioController`/`IAppController`.
**Delivers:** `MainWindow`, `SettingsWindow`, mode indicator driven by snapshot-presence logic (Architecture Pattern 3), settings screen with monitor/audio/app pickers
**Implements:** Adapter/Facade pattern (interfaces defined here, real implementations plugged in later phases)

### Phase 4: App/Process Control (Real Implementation)
**Rationale:** Lowest-risk real OS integration — `Process`/`user32.dll` APIs are well documented — making it the best first "swap fake for real" validation before tackling the two riskier adapters.
**Delivers:** Real `IAppController`: process detection via `Process.GetProcessesByName`, launch with polling/timeout for window-handle discovery (not a single immediate check), focus/minimize via `ShowWindow`/`SetForegroundWindow`
**Avoids:** Pitfall 9 (launch/focus race against target app startup time) and validates Pitfall 2's elevation-matching concern (Rig Toggle must stay unelevated to focus Moza Companion reliably)

### Phase 5: Audio Control (Real Implementation)
**Rationale:** Undocumented but well-trodden API surface — a known-working path exists across many reference implementations (SoundVolumeView, EarTrumpet, AudioDeviceCmdlets), moderate risk.
**Delivers:** Real `IAudioController`: NAudio enumeration + hand-embedded `IPolicyConfig` COM interop, setting all three audio roles (console/multimedia/communications) per switch, snapshot with ID + friendly-name fallback, explicit COM object release each cycle
**Avoids:** Pitfall 4 (device ID instability — ID+name fallback), Pitfall 5 (partial-role switch, COM leaks across repeated toggles)

### Phase 6: Monitor Control (Real, Production Implementation)
**Rationale:** Highest technical risk, but by this phase the feasibility question from Phase 1 is already answered — this phase turns the validated spike into the production `CcdMonitorController` adapter.
**Delivers:** Real `IMonitorController` using the spike-validated mechanism, full topology snapshot/restore (not just enable/disable booleans), post-mutation state verification
**Avoids:** Pitfall 3 (windows/fullscreen apps on the disabled monitor), Pitfall 6 (silent operation failures — verify-after-write), Pitfall 7 (incomplete snapshot losing primary/position/orientation)

### Phase 7: Orchestration Wiring & Full Toggle Flow
**Rationale:** Once all three adapters have real implementations, this is mostly "swap fakes for reals" into the already-exercised `ToggleService`, rather than writing new orchestration logic from scratch.
**Delivers:** Full snapshot-before-mutate toggle flow (both directions), per-step partial-failure handling and reporting, mode-detection-on-startup validated against a forced-crash-while-in-rig-mode scenario
**Avoids:** Pitfall 8 (mode-tracking desync after crash/restart) — explicit acceptance test: force-close while in rig mode, relaunch, verify correct detection and restore

### Phase 8: Packaging & Distribution
**Rationale:** Packaging mechanics are well documented, but the real-world first-run experience (SmartScreen/AV flagging) is a known, unavoidable friction point for this app's specific behavior profile, not a bug to code around.
**Delivers:** Self-contained single-file publish (`win-x64`, untrimmed), tested on a clean VM without prior Defender exclusions, with an explicit up-front decision on whether code signing is in scope for v1 (likely not, for a single-user personal tool)
**Avoids:** Pitfall 10 (unsigned .exe SmartScreen/AV flagging treated as a bug during final validation instead of documented expected behavior)

### Phase Ordering Rationale

- Phase 1 is deliberately out of normal dependency order — architecture and pitfalls research both independently flag the monitor-disable mechanism as the one unvalidated assumption underpinning the entire project, so it must be answered before investing in anything else.
- Phases 2-3 build everything OS-interop-free first (data contracts, then GUI against fakes), maximizing the amount of the app that can be built and iterated on with zero hardware dependency, per architecture's suggested build order.
- Phases 4-6 introduce real OS integration in ascending risk order (process control, audio, monitor), so each swap-in validates the fake-to-real pattern before tackling the riskiest adapter.
- Phase 7 (orchestration) is assembled last from already-validated parts by design — this keeps the state-machine wiring itself low-risk since every dependency it needs was proven independently first.
- Phase 8 is last because packaging/distribution concerns (SmartScreen, single-file publish settings) are orthogonal to feature correctness and only matter once the app actually works.

### Research Flags

Needs research (deeper investigation likely required during planning):
- **Phase 1 (Monitor-Disable Feasibility Spike):** No officially documented API confirms this capability; GPU-vendor/driver-specific behavior is unverified for the actual rig hardware — this is a hands-on hardware spike, not a documentation lookup.
- **Phase 6 (Monitor Control, production):** Same underlying uncertainty as Phase 1, plus the additional complexity of full topology snapshot/restore fidelity (non-default primary/arrangement) and window-relocation races (Pitfall 3) that only surface with specific real-world monitor arrangements.
- **Phase 5 (Audio Control):** Moderate — the `IPolicyConfig` interface is undocumented and unsupported by Microsoft; while well-trodden by community tools, per-role behavior and COM lifecycle correctness benefit from cross-checking multiple reference implementations rather than a single source.
- **Phase 8 (Packaging):** Low-medium — mechanics are well documented, but the SmartScreen/AV real-world impact and the code-signing cost/benefit decision may warrant a short investigation before finalizing distribution scope.

Phases with standard patterns (skip research-phase):
- **Phase 2 (Foundations):** Plain JSON persistence via `System.Text.Json`, no OS dependency — standard .NET patterns.
- **Phase 3 (GUI Shell):** Standard WinForms/MVVM-lite structure against fakes — well-documented, established patterns.
- **Phase 4 (App/Process Control):** `Process` class + `user32.dll` P/Invoke for window focus/minimize is a long-standing, widely-documented Windows pattern (single-instance detection, `SetForegroundWindow`).
- **Phase 7 (Orchestration Wiring):** Standard state-machine composition once all adapters are validated — the pattern itself (snapshot to mutate to restore, derive mode from snapshot presence) is fully specified in ARCHITECTURE.md already.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified via official Microsoft docs, direct source-code reads of `WindowsDisplayAPI`, and current package/version confirmation (NAudio, .NET 10 LTS dates) |
| Features | MEDIUM-HIGH | Established ecosystem with multiple corroborating sources (DisplayFusion, SoundSwitch, NirSoft), but no single tool matches this exact feature combination — synthesis is inferred from adjacent tools, not directly observed |
| Architecture | HIGH (general structure) / MEDIUM-LOW (monitor-disconnect capability specifically) | Layering, adapter pattern, and snapshot-derived-mode patterns are well-established desktop app practice; the one explicitly flagged gap is that no officially documented public API confirms true monitor disconnect is achievable at all |
| Pitfalls | MEDIUM-HIGH | Confirmed via Microsoft Learn, driver docs, and multiple independent implementations for the majority of pitfalls; a few specifics (exact SmartScreen thresholds, per-GPU-vendor CCD quirks) are explicitly LOW confidence and hardware-dependent |

**Overall confidence:** MEDIUM-HIGH

### Gaps to Address

- Monitor true-disable mechanism is unconfirmed by official Microsoft documentation — must be resolved via a hands-on spike against the actual rig hardware/GPU driver as Phase 1, before treating any downstream architecture as settled.
- WinForms vs. WPF: STACK.md recommends WinForms for lowest friction; ARCHITECTURE.md's illustrative examples use WPF/MVVM. Functionally interchangeable — the adapter/orchestration architecture applies regardless of which UI framework is chosen — but this choice should be made explicitly during Phase 3 rather than left ambiguous.
- Per-operation elevation requirements (monitor disable, audio switch, window focus) are not empirically confirmed for this specific machine's GPU/driver combination — must be tested directly rather than assumed from general Windows behavior, since getting this wrong breaks cross-process window focus via UIPI.
- Real-world SmartScreen/AV impact of the packaged .exe is unverified until tested on a clean VM — treat as expected first-run friction to document, not a defect to eliminate, unless a code-signing budget is explicitly approved.
- Audio device ID stability across driver updates/reboots is a known general risk but untested on this specific rig's hardware — build the ID+friendly-name fallback from the start (Phase 5) rather than waiting for a real failure to reveal the gap.

## Sources

### Primary (HIGH confidence)
- https://github.com/falahati/WindowsDisplayAPI (source read directly) — confirmed exact `SetDisplayConfig` flag usage for topology-based monitor path removal
- https://learn.microsoft.com/en-us/windows-hardware/drivers/display/setdisplayconfig-summary-and-scenarios — official CCD API reference
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview and /trimming/trim-self-contained — official self-contained/single-file/trimming guidance
- https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md — confirmed .NET 10 GA date and LTS window
- https://www.nuget.org/packages/NAudio/ — confirmed current version, license, maintenance status
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow and related Win32 window API docs
- https://github.com/dotnet/docs-desktop — official WinForms/WPF application settings architecture guidance

### Secondary (MEDIUM confidence)
- https://learn.microsoft.com/en-us/answers/questions/5662114/windows-11-is-there-a-supported-way-to-script-auto — Microsoft Q&A confirming no documented public "disconnect display" API exists
- https://github.com/File-New-Project/EarTrumpet/blob/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs — confirms modern `IPolicyConfig` GUIDs, corroborated by multiple independent open-source projects
- https://www.nirsoft.net/utils/multi_monitor_tool.html and /sound_volume_view.html — official vendor pages confirming CLI behavior and current Windows 11 support
- https://www.displayfusion.com/HelpGuide/WorkingWithDisplayFusionMonitorProfiles/ and related DisplayFusion discussion threads — official vendor documentation and community forum on profile-switching and sim-racing use cases
- https://github.com/belphemur/soundswitch — official open-source repo, corroborates default-audio-switch approach
- https://github.com/dotnet/runtime/issues/33745 — confirms `PublishTrimmed=true` has triggered Defender false positives in the wild

### Tertiary (LOW confidence)
- GPU-vendor-specific (NVIDIA/AMD/Intel) CCD behavior differences — flagged as unverified for this project's specific hardware, needs direct testing
- Exact SmartScreen reputation thresholds — inferred from community reports (Rick Strahl's weblog, Ctrl blog), not an official Microsoft specification
- https://github.com/supercam19/GameMonitor — single-source community tool, used only as a minor corroborating data point for the "auto-trigger on game launch" anti-feature/differentiator boundary

---
*Research completed: 2026-07-24*
*Ready for roadmap: yes*
