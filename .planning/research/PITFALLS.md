# Pitfalls Research

**Domain:** Windows desktop GUI utility — programmatic display enable/disable (CCD/device-node APIs), default audio device switching (undocumented COM interface), cross-process window management, standalone .exe packaging
**Researched:** 2026-07-24
**Confidence:** MEDIUM-HIGH (Win32/CCD/COM behavior confirmed via Microsoft Learn + driver docs + multiple independent implementations; packaging/AV behavior confirmed via multiple community reports; some specifics — e.g. exact SmartScreen thresholds, per-GPU-vendor CCD quirks — are LOW confidence and flagged individually)

## Critical Pitfalls

### Pitfall 1: "CCD API" doesn't have a public "disconnect this display" flag — the naive implementation only powers the monitor off or fails silently

**What goes wrong:**
Teams assume `SetDisplayConfig`/the CCD API exposes something equivalent to the Settings app's "Disconnect this display" button. It does not. There is no documented `SDC_*` flag or `DISPLAYCONFIG_PATH_INFO` field that maps directly to that UI action — Microsoft has confirmed on Q&A that no supported way to script per-monitor "disconnect" exists via the public API surface. Developers who reach for the obvious API (`ChangeDisplaySettingsEx` with a null/zeroed `DEVMODE`, or naive `SetDisplayConfig` calls) end up with a monitor that is blanked/powered-down (DPMS-style) but still enumerated by Windows as an active display — which is exactly the failure mode this project exists to avoid (games still see two displays and BeamNG-style self-minimize bugs persist).

**Why it happens:**
The public Win32 display API was designed for topology configuration (position, resolution, primary designation), not device presence toggling. The actual mechanisms that achieve a true "vanishes from Windows' display list" effect are either (a) supplying `SetDisplayConfig` a topology array that **omits the target's path entirely** using `SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG` (or `SDC_TOPOLOGY_SUPPLIED`) without `SDC_PATH_PERSIST_IF_REQUIRED`, which removes the path from the active configuration without touching the device node — the closest public-API equivalent to "Disconnect this display" — or (b) disabling the monitor's device node in Device Manager via `CM_Disable_DevNode` (cfgmgr32), which is a heavier operation (affects the driver/device instance, generally needs elevation, and by default re-enables after reboot unless `CM_DISABLE_PERSIST` is used on Windows 10+). Tools like NirSoft's MultiMonitorTool exist precisely because neither of these is discoverable from casual API browsing.

**How to avoid:**
- Prototype both approaches (topology-path-removal via `SetDisplayConfig`, and `CM_Disable_DevNode`) against the actual rig hardware/GPU driver before committing to an architecture — behavior can differ by GPU vendor (NVIDIA/AMD/Intel) and driver version (LOW confidence, unverified for this specific rig — must be hardware-tested).
- Treat "topology-path-removal" as the preferred approach since it doesn't require touching the device driver state and is closer to what the OS-native "Disconnect" button does; reserve `CM_Disable_DevNode` as a fallback only if path-removal proves insufficient for the target games.
- Do not use `ChangeDisplaySettingsEx`/legacy DEVMODE-zeroing as the primary mechanism — it is a legacy API from before CCD existed and its "detach" behavior is inconsistent on modern WDDM drivers.

**Warning signs:**
- After "disable," the monitor still appears in `EnumDisplayMonitors`, Task Manager's Performance tab, or Windows' own Display Settings list.
- The target game still detects two displays or still exhibits the misbehavior (e.g., BeamNG self-minimize) that motivated this project.
- Disable "succeeds" (no error) but visually the monitor just goes black/standby instead of Windows re-arranging remaining desktop icons/taskbar as it does on a real disconnect.

**Phase to address:**
Must be resolved as a spike/proof-of-concept *before* the monitor-disable feature is considered architecturally settled — this determines the core technical approach for the whole project and should be the first phase, not discovered mid-implementation.

---

### Pitfall 2: Elevation (admin) requirements are inconsistent across the three subsystems, and requesting admin broadly breaks cross-process window control

**What goes wrong:**
Disabling a monitor via `CM_Disable_DevNode` (device-node disable) requires administrator privileges; disabling via `SetDisplayConfig` topology-path-removal generally works for the interactive console-session user without elevation; switching the default audio device via the undocumented `IPolicyConfig` COM interface does **not** require elevation. If a developer defaults to marking the whole app's manifest `requireAdministrator` "just to be safe" (common reflex once one operation needs it), every other Win32 call that touches *another process's* window — `SetForegroundWindow`, `ShowWindow`, `BringWindowToTop` on the Moza Companion window — silently stops working or behaves unreliably. This is because of User Interface Privilege Isolation (UIPI): a lower-integrity (non-elevated) process's window generally cannot receive focus-forcing calls from a higher-integrity (elevated) process, and normal foreground-lock rules also apply (a process can only steal foreground focus under specific conditions: it is the current foreground process, was started by it, received the last input event, etc.).

**Why it happens:**
Windows elevation is not a single on/off switch conceptually developers can reason about uniformly — different subsystems (Device Manager/PnP, CCD, Core Audio policy) have different, undocumented elevation requirements, and UAC's per-manifest, whole-process elevation model doesn't allow "elevate just this one Win32 call." Developers discover the admin requirement for one call, apply it globally via the app manifest, and only later discover unrelated window-management code has broken.

**How to avoid:**
- Determine the actual minimum-privilege requirement per operation empirically before writing the manifest (test topology-path-removal disable specifically — it likely does NOT need elevation, unlike `CM_Disable_DevNode`).
- Prefer the non-elevation-requiring disable mechanism (Pitfall 1) specifically because it avoids this cascading problem.
- If any operation genuinely requires elevation, isolate it in a separate short-lived elevated helper process (invoked via `ShellExecute` with `runas`, communicating results back over a pipe/exit code) rather than elevating the main GUI process — keep the main app (which does window focus/management) running at the same integrity level as Moza Companion (normal user).
- Never set `requestedExecutionLevel="requireAdministrator"` reflexively; default to `asInvoker` and add elevation surgically only where proven necessary.

**Warning signs:**
- `SetForegroundWindow`/`ShowWindow` calls against the Moza Companion window return success/no exception but the window visually does not come to front (classic UIPI symptom, not a coding bug).
- Feature works fine when both apps are run from the same elevated/non-elevated launch context during dev testing, but breaks for the real end-user launch scenario (double-clicking .exe from Explorer, no elevation).

**Phase to address:**
Must be validated during the display-control and app-launch/window-management phases — specifically, before considering the "bring Moza Companion to focus" requirement done, test it with Rig Toggle running unelevated (the expected real-world case) and confirm the display-disable mechanism chosen doesn't force elevation.

---

### Pitfall 3: Race condition — disabling a monitor while windows/fullscreen apps are still displayed on it

**What goes wrong:**
When a monitor is removed from the active topology, Windows must relocate any windows that were on it to a remaining display. This relocation is not always graceful: maximized windows can lose their maximized state, windows can end up with mismatched DPI/scaling if the two monitors have different scale factors, and windows can be repositioned using raw coordinate math that leaves them partially or fully off-screen once the monitor is later re-enabled (because their last-known position assumed the now-removed monitor's coordinate space). If the primary desk monitor is the one being disabled while normal desktop windows are on it (the exact scenario in this project — "disable the primary monitor"), users can end up with orphaned windows, a jarring taskbar/Start-menu relocation (both are tied to whichever monitor becomes primary), and potential focus-loss if the disabled monitor held the currently-focused window.

**Why it happens:**
CCD topology changes are applied as an atomic OS-level operation, but window-manager-level consequences (relocation, restacking, DPI rescaling) happen as a best-effort side effect the app has no direct control over and cannot roll back.

**How to avoid:**
- Enumerate windows on the target monitor before disabling and either explicitly move them to the remaining monitor (preserving relative position/maximized state yourself) or, at minimum, warn/require the user to have moved things off it (acceptable for a personal single-user tool, but must be a documented behavior, not an accident).
- Re-query monitor/window layout immediately after disable and log/verify no window ended up with negative or out-of-bounds coordinates.
- Test explicitly with a maximized window and a fullscreen-exclusive game window on the target monitor at disable-time — these are the two cases most likely to break silently.

**Warning signs:**
- Windows that were on the primary monitor "disappear" (moved off-screen) after toggling to rig mode.
- Taskbar or Start menu unexpectedly relocates to the rig monitor and doesn't return after toggle-back.
- The game itself crashes or renders incorrectly if launched/running exactly during the disable operation (timing-dependent — only reproduces if disable happens while the game process already has a window handle open).

**Phase to address:**
Must be validated before the monitor-disable feature is considered done — include an explicit test case in that phase's acceptance criteria: "disable with a maximized window on the target monitor; verify no window is lost or left inaccessible."

---

### Pitfall 4: Audio device identity is not stable — GUIDs/IDs used for the snapshot can silently point to nothing (or the wrong device) later

**What goes wrong:**
Windows Core Audio device IDs (the endpoint ID strings, e.g., `{0.0.0.00000000}.{guid}`) are tied to the underlying MMDevice enumeration in the registry (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio`). These IDs can change after a driver reinstall/update, a Windows Update that re-enumerates audio endpoints, or even (in some reported cases) a plain reboot when USB/HDMI audio devices re-enumerate in a different order. A snapshot-and-restore design that stores the raw endpoint ID string and blindly calls `SetDefaultEndpoint`/`IPolicyConfig::SetDefaultEndpoint` with it later can fail (device no longer exists) or, worse, silently do nothing if the ID happens to now belong to a different, unexpected device.

**Why it happens:**
Windows doesn't guarantee device-ID stability across driver/topology changes; it's an internal enumeration detail, not a documented stable contract, and Microsoft's own guidance/community tooling acknowledges this is a known pain point with no first-party stable-ID API.

**How to avoid:**
- Snapshot both the endpoint ID *and* a human-readable fallback (friendly name + device description) at toggle-time. At restore time, first try the exact ID; if `SetDefaultEndpoint`/the enumerator reports the device no longer exists, fall back to matching by friendly name, and if that also fails, surface a clear "couldn't restore audio device — it may have been removed/renamed" message rather than failing silently or applying a wrong device.
- Re-enumerate available devices at restore time rather than assuming the snapshot's device list is still accurate.
- Since this is a personal single-user rig with presumably static hardware, this is lower-severity in practice — but still must degrade gracefully (visible error) rather than silently switching to whatever device happens to be first in the new enumeration order.

**Warning signs:**
- Restore-to-normal-mode occasionally leaves audio on the rig speakers (or an unexpected device) with no error shown.
- Behavior differs after any Windows Update or audio driver update — regression that "used to work."

**Phase to address:**
Must be handled in the state-snapshot/restore component — the snapshot schema and restore logic should be designed with ID instability as a first-class case from the start, not patched in after a failure is observed.

---

### Pitfall 5: `IPolicyConfig` is a fully undocumented, unsupported private COM interface — it can change or vary by Windows build without warning, and has version-dependent vtable layouts

**What goes wrong:**
There is no public/supported Win32 or WinRT API to set the system default audio *playback* device programmatically — `MediaDevice`/WinRT audio APIs only let apps query the default, not set it (this is an intentional Microsoft restriction, not an oversight, to prevent malware from silently rerouting audio). The community workaround is `IPolicyConfig` (interface GUID `f8679f50-850a-41cf-9c72-430f290290c8`, class `CPolicyConfigClient` `870af99c-171d-4f9e-af0d-e63df40c2bc9`), reverse-engineered from the Sound control panel applet. This interface is explicitly unsupported by Microsoft, has a different vtable/method layout between "Vista" and "Windows 7+" variants, and Windows 10 1607+/11 introduced a third `ERole` target (console, multimedia, communications — the Settings app's "Output device" actually needs `SetDefaultEndpoint` called for multiple roles to fully match what the UI does) that naive single-role implementations miss, leaving one of the three roles pointed at the old device after a "switch."

**Why it happens:**
Because there is genuinely no supported alternative, every existing tool in this space (AudioDeviceCmdlets, SoundVolumeView, audioswitch, etc.) relies on the same reverse-engineered interface, copy-pasted across projects for years — its continued functioning on current Windows is observed/community-verified, not guaranteed by Microsoft, and could break in a future Windows build with zero warning or migration path.

**How to avoid:**
- Call `SetDefaultEndpoint` for all three roles (eConsole, eMultimedia, eCommunications) when switching the default output, matching what the Sound Control Panel actually does — a partial switch (one role only) is a common bug source where "some apps" still play through the old device after a "successful" switch.
- Isolate the `IPolicyConfig` P/Invoke definitions behind a single internal abstraction so that if Microsoft breaks compatibility in a future Windows release, the fix is localized to one file, not scattered call sites.
- Have a documented fallback plan (e.g., shell out to a maintained community tool like SoundVolumeView/AudioDeviceCmdlets as a process, or prompt the user to switch manually) in case the interface stops working after a Windows feature update — do not assume permanent compatibility.
- Ensure correct COM lifecycle: initialize COM apartment (`CoInitializeEx`) appropriately for a long-lived GUI app that will call this repeatedly across many toggles in a session, and release COM objects (`Marshal.ReleaseComObject` or proper RCW disposal) each time rather than leaking — repeated toggle-cycles in one running session is exactly this app's core usage pattern, so COM object leaks or improper reinitialization will surface as "works the first few times, then fails" bugs.

**Warning signs:**
- Some applications (e.g., games with a startup audio-device cache, or apps that read only one of the three roles) don't follow the switched default even though Settings shows the new device as default.
- After many toggle cycles in one running session, the audio switch starts silently failing or throwing COM exceptions (leak/reinit symptom).
- A Windows feature update ships and default-switching stops working entirely with no code change on your side.

**Phase to address:**
Must be handled in the audio-control component — implementing all three roles and correct COM lifecycle management should be part of that phase's "definition of done," not treated as a later polish item.

---

### Pitfall 6: Operations report success but don't actually take effect ("silent failure") — the API return code is not proof the display/audio actually changed

**What goes wrong:**
`SetDisplayConfig` has documented cases where it does not fail even when given an invalid/contradictory flag combination — it simply ignores the offending flag (e.g., violating the rule that `SDC_TOPOLOGY_SUPPLIED` can't combine with other `SDC_TOPOLOGY_XXX` flags causes the flag to be silently ignored rather than an error returned). More generally, both display and audio APIs in this space have known cases of returning a success `HRESULT`/non-error code while the visible system state doesn't actually change (e.g., driver rejects part of a topology request, or the "default" device change doesn't propagate to a role the code didn't set). Code that trusts the return value alone and immediately updates its own internal "we are now in rig mode" state can drift out of sync with actual Windows state.

**Why it happens:**
These are low-level, driver-mediated APIs where success often means "the request was accepted for processing," not "the requested end-state now exists" — validation happens at multiple layers (API, kernel, driver) and not all of them surface errors back up consistently.

**How to avoid:**
- After every mutating call (disable monitor, set default audio device), re-query actual state (`QueryDisplayConfig`/`EnumDisplayDevices` for displays; `IMMDeviceEnumerator::GetDefaultAudioEndpoint` for audio) and compare against the intended end-state before declaring the toggle successful in the UI.
- Surface a clear, specific error to the user if verification fails ("monitor did not disable — still detected as active") rather than showing a generic success message or silently leaving the internal mode-flag out of sync with reality.
- Never assume idempotent success — a "toggle to rig mode" that partially succeeds (e.g., audio switched but monitor disable was silently ignored) needs to be detectable and reported, not just assumed done.

**Warning signs:**
- User reports "I clicked toggle but nothing happened" with no error shown.
- Internal state tracking says "rig mode" but the visible monitor/audio configuration doesn't match.

**Phase to address:**
Must be part of both the display-control and audio-control components' definition of done — each mutating operation needs a verify-after-write step, and this should be a stated acceptance criterion before either feature is marked complete.

---

### Pitfall 7: State snapshot captures too little to guarantee exact restoration

**What goes wrong:**
A naive snapshot implementation captures only "which monitors were active" or "what was the default audio device name" — this loses information needed for *exact* restoration as required by this project (monitor position/arrangement, orientation, refresh rate, which monitor was primary; which of the three audio roles pointed to which device). Restoring from an incomplete snapshot can silently produce a topology that "looks similar" (same monitors active) but differs in primary-monitor designation, relative position (causing windows to jump/reflow), or scaling — a regression the user may not immediately notice but will hit the next time they try to use the desk monitor normally.

**Why it happens:**
`QueryDisplayConfig`'s output (`DISPLAYCONFIG_PATH_INFO[]` + `DISPLAYCONFIG_MODE_INFO[]`) is the actual complete, restorable representation of display state, but it's easy to under-scope a "snapshot" to just an enable/disable boolean per monitor because that's the surface-level thing the feature is about. Microsoft's own docs also warn that some fields/flags in a queried config aren't safely re-suppliable to `SetDisplayConfig` byte-for-byte without adjustment (certain "currently in use" validity flags must be cleared before reapplying), which is easy to miss and produces a restore call that fails or behaves unexpectedly.

**How to avoid:**
- Snapshot the full `QueryDisplayConfig` output (both path and mode arrays) for the "normal" state before ever mutating anything, not just a monitor-enabled boolean.
- Snapshot all three audio roles' current default device, not just "the" default.
- Test the round-trip explicitly: query → mutate to rig mode → restore from snapshot → re-query and diff against the original query result, confirming they match on primary designation, position, and resolution — not just "same set of monitors active."
- Treat this round-trip test as a required acceptance check for the state-restore feature, run repeatedly (toggle back and forth several times in a row) to catch drift that only appears after multiple cycles.

**Warning signs:**
- After toggle-back, the primary monitor designation is wrong, or monitor arrangement (left/right) is swapped from what it was before.
- Windows that were open before ever toggling end up in different positions after toggle-back even though "the same monitors" are active.

**Phase to address:**
Must be handled in the state-snapshot/restore component design from the start — this is the component most likely to "look done" (toggle back and forth once, looks fine) while having latent exact-restoration bugs that only surface with specific prior arrangements (non-default primary, non-default position) — explicitly test with a non-trivial monitor arrangement, not just the default side-by-side layout.

---

### Pitfall 8: Toggle called when internal mode-tracking is out of sync with actual OS state (no persisted source of truth)

**What goes wrong:**
If the app tracks "current mode" (normal vs. rig) purely as an in-memory flag, any scenario where that flag doesn't reflect reality — app restarted mid-session, app crashed during a toggle, user manually changed display/audio settings outside the app — causes the next toggle to snapshot the *wrong* state as "normal" (e.g., snapshotting rig-mode audio/display as if it were the baseline to restore to later), effectively corrupting the restore target permanently until manually fixed.

**Why it happens:**
It's simplest to implement toggle logic as "if flag says normal, snapshot and switch to rig; if flag says rig, restore from snapshot and clear flag" — this is correct only if the flag is always trustworthy, which it isn't across app restarts or crashes unless persisted and validated.

**How to avoid:**
- On startup, don't trust a persisted "we were in rig mode" flag blindly — actively detect current state by comparing live display/audio configuration against known rig-mode settings (from the settings view) to determine actual current mode.
- Persist the pre-toggle snapshot to disk (not just memory) immediately when toggling to rig mode, so an app crash/restart mid-rig-session doesn't lose the ability to restore.
- Consider treating "toggle to rig mode while already detected as in rig mode" as a safe no-op (or a "re-apply rig settings" action) rather than re-snapshotting.

**Warning signs:**
- After an app crash or forced-close while in rig mode, relaunching and toggling back doesn't restore the original desktop configuration.
- Toggling twice in a row without an intervening restore produces unexpected results.

**Phase to address:**
Must be addressed in the state-snapshot/restore component — persisted-state design should be decided alongside the in-memory toggle logic, not added afterward.

---

### Pitfall 9: Launching/focusing a third-party app's window races against that app's own startup time

**What goes wrong:**
Detecting "is Moza Companion already running" via process name/handle is straightforward, but immediately after launching it (when not already running), code that tries to find its main window handle and call `SetForegroundWindow` right away will often fail or silently no-op, because the target process hasn't finished creating its main window yet (process start and main-window creation are not simultaneous — there can be a real, sometimes multi-second, delay, especially for apps with splash screens or startup work).

**Why it happens:**
`Process.Start()` returns as soon as the process object exists, well before the app's UI thread has pumped its first message loop and created a window; a single immediate `FindWindow`/`GetProcesses()[0].MainWindowHandle` check right after `Start()` frequently returns null/zero.

**How to avoid:**
- Poll for the target window handle with a timeout (e.g., check every 200-300ms for up to 10-15 seconds) after launching, rather than a single immediate check.
- Handle the "launched but window never appeared within timeout" case explicitly (don't hang indefinitely or throw an unhandled exception) — surface a clear message rather than silently doing nothing.
- For the "already running, bring to focus" path specifically, be aware of the `SetForegroundWindow` restrictions described in Pitfall 2 (UIPI/foreground-lock rules) — even correct window-handle detection can still fail to visually focus the window if the caller doesn't qualify per Windows' foreground-switching rules.

**Warning signs:**
- "Launch Moza Companion" works in slow/debug testing (developer naturally pauses between steps) but fails intermittently or always in the packaged release build's real-world timing.
- Companion app window sometimes doesn't come to focus after a fresh launch specifically (vs. the "already running" bring-to-front path, which is more likely to work since the window already exists).

**Phase to address:**
Must be validated in the app-launch/window-management phase — the acceptance test should specifically include "launch when not already running, from a cold start (splash screen scenario if the app has one), and verify focus succeeds," not just the already-running case.

---

### Pitfall 10: Unsigned, self-contained single-file .exe gets flagged by SmartScreen/antivirus specifically because of what it does, not just how it's packaged

**What goes wrong:**
Two independent risk factors compound here: (1) .NET self-contained single-file publishing bundles the runtime and self-extracts/loads components at startup — a packing pattern that resembles malware droppers and is a documented trigger for Defender/AV heuristics and reduced SmartScreen reputation (new, rarely-downloaded, unsigned executables are treated as inherently riskier by SmartScreen's reputation model); and (2) the actual behavior of this app — P/Invoke calls into device-management (`cfgmgr32`/CCD), undocumented COM interfaces for audio routing, and cross-process window enumeration/manipulation of another running application — overlaps heavily with behaviors flagged in RAT/spyware/remote-control malware signatures. Combined, first-run (and possibly every-run, since SmartScreen reputation is per-binary-hash and rebuilding changes the hash) SmartScreen/Defender warnings are likely, and this can appear as an outright block rather than a dismissible "Run anyway" warning depending on Defender's SmartScreen mode.

**Why it happens:**
Both the distribution format (unsigned, self-contained, single-file, low download count — since this is a personal tool used by exactly one person) and the functional behavior independently raise flags; there is no realistic way to fully avoid this without either code-signing (which requires purchasing a certificate — a real cost/step for a personal tool) or reducing the app's file-system/behavioral footprint (neither of which eliminates the core P/Invoke behaviors that are the actual point of the app).

**How to avoid:**
- Set expectations correctly from the start: for a personal single-user tool, accept that a one-time "Windows protected your PC" SmartScreen click-through (More info -> Run anyway) or a Defender exclusion for the specific .exe path is the realistic outcome, not a bug to "fix" via code changes.
- If avoiding the warning entirely matters, budget for a code-signing certificate (OV certificates from providers like Comodo/Sectigo are available in the tens of euros/year range) — signing alone helps but reputation still needs to build over time/downloads with SmartScreen specifically (a fresh signature doesn't instantly grant trusted-publisher status).
- Consider not using aggressive trimming (`PublishTrimmed`) if it doesn't meaningfully help distribution size for a personal tool, since trimming has independently been reported to trigger Defender false positives in some .NET SDK versions.
- Document this as expected first-run friction in whatever setup notes accompany the tool, rather than treating a SmartScreen prompt during testing as evidence something is broken.

**Warning signs:**
- Windows Defender quarantines the built .exe on the build machine itself before you even test it, or after a rebuild changes its hash.
- SmartScreen shows "Windows protected your PC" on first launch on a clean machine/VM.

**Phase to address:**
Should be explicitly called out in the packaging/distribution phase's scope and acceptance criteria — decide up front whether code signing is in scope for v1 (likely not, given single-user/personal-tool context) and document the expected click-through step so it isn't mistaken for a bug during final validation.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|--------------------|-----------------|------------------|
| Hardcoding one audio role (`eConsole` only) in `SetDefaultEndpoint` instead of all three roles | Faster to implement, "works" in casual testing | Some apps/games keep using old device on the role that wasn't switched | Never — implement all three roles from the start; it's the same amount of code |
| In-memory-only mode/state tracking (no persisted snapshot to disk) | Simpler initial implementation | Crash/restart mid-rig-session permanently loses restore target | Acceptable only for a throwaway prototype/spike, never for the shipped v1 |
| Skipping post-mutation state verification ("trust the return code") | Less code, faster feature completion | Silent failures ship as "it works" until a user hits the specific driver/flag edge case | Never for monitor-disable or audio-switch; acceptable temporarily for early spikes only |
| Requesting `requireAdministrator` manifest-wide to unblock one operation | Immediately unblocks whichever call needed elevation | Breaks `SetForegroundWindow`/window management against the (non-elevated) Moza Companion via UIPI | Never — isolate elevation to a helper process instead |
| Matching audio devices by friendly name only (no ID-based snapshot) | Simple to implement, human-readable | Fails when devices are renamed, or ambiguous if 2 devices share a name | Only as a documented fallback layer, never as the sole matching strategy |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|-----------------|--------------------|
| CCD / Win32 display API (`SetDisplayConfig`/`QueryDisplayConfig`) | Assuming a documented "disconnect" flag exists; using legacy `ChangeDisplaySettingsEx` as the primary disable mechanism | Use topology-path-removal via `SetDisplayConfig` with a supplied path set that omits the target monitor; validate against real hardware first |
| `IPolicyConfig` COM interface | Using only one interface variant/vtable layout without version-checking; calling `SetDefaultEndpoint` for only one `ERole` | Support both Vista/7+ vtable variants defensively if targeting a range of Windows versions; always set all three roles |
| Win32 window APIs against Moza Companion | Assuming `SetForegroundWindow` always works if you have the right window handle | Account for UIPI/foreground-lock rules; keep integrity levels matched between Rig Toggle and Moza Companion |
| `Process.Start()` + window discovery | Single immediate check for `MainWindowHandle` after starting the process | Poll with timeout; handle the "window never appeared" case explicitly |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| Leaking COM objects (`IPolicyConfig`, `IMMDeviceEnumerator`) across repeated toggles in one long-running session | Audio switching starts failing intermittently after many toggle cycles in the same app session | Explicitly release/dispose COM RCWs after each use; consider re-creating rather than caching across calls if lifetime is unclear | Not a "scale" issue in the traditional sense — breaks after N toggle cycles within a single running session (N varies, but is a realistic failure mode for a tool meant to be toggled repeatedly per gaming session) |
| Polling loop for window-handle discovery with too-short timeout or too-tight interval | False "launch failed" errors on a slower-starting Moza Companion (e.g., after a Windows/driver update slows its startup) | Use a generous timeout (10-15s) with reasonable polling interval (200-300ms), and make the timeout configurable if possible | Breaks whenever the target app's startup time varies (cold cache, pending Windows updates, etc.) |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Running the whole app elevated "to be safe" | Breaks window-focus operations against non-elevated processes (UIPI); increases blast radius of any bug since elevated code can affect the whole system | Default to `asInvoker`; isolate any genuinely-required elevated operation to a minimal helper process |
| Storing the Moza Companion executable path from user input without validating it before `Process.Start()` | Launching arbitrary attacker-supplied paths if settings are ever tampered with or corrupted (low risk for a personal single-user tool, but still worth a basic sanity check) | Validate the configured path exists and is a `.exe` before launching; this is a personal tool so risk is low, but cheap to guard against corrupted config |
| Treating the undocumented `IPolicyConfig` GUID/vtable definitions as "safe because everyone copies them" | A future Windows update could subtly change behavior without any error, silently misrouting audio | Version-gate or defensively verify actual effect via post-call state verification (Pitfall 6), not just trust in community-sourced interop code |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|--------------|-------------------|
| No feedback distinguishing "toggle succeeded," "toggle partially succeeded," and "toggle failed" | User can't tell if rig mode is actually fully active, leading to launching a game onto the wrong monitor without realizing the toggle silently failed | Explicit per-step status in the UI (monitor: OK/failed, audio: OK/failed, app launch: OK/failed) rather than one binary success/fail indicator |
| Toggle button usable while a previous toggle operation is still in-flight | Rapid double-clicks can race two toggle operations against each other, corrupting the snapshot or leaving state genuinely inconsistent | Disable/debounce the toggle control while an operation is in progress; show a busy/in-progress state |
| No visible indication of current mode (normal vs rig) on app open | User can't tell current state without checking physical monitors/speakers, especially after app restart | Detect and display actual current mode on launch (tied to Pitfall 8's state-detection logic), not just an assumed default |

## "Looks Done But Isn't" Checklist

- [ ] **Monitor disable:** Often just powers off (DPMS-style) instead of removing from Windows' active display list — verify with `EnumDisplayMonitors`/Display Settings that the monitor is truly gone, and confirm the actual target game (BeamNG or similar) no longer misbehaves, not just that the screen goes black.
- [ ] **Audio device switch:** Often only sets one of the three roles (console/multimedia/communications) — verify via Sound Control Panel that all relevant apps (not just system sounds) route through the new device.
- [ ] **State restore:** Often restores "the same monitors active" but not the same primary designation/arrangement/position — verify with a non-default monitor arrangement (not just side-by-side defaults) and confirm an exact round-trip via `QueryDisplayConfig` diff.
- [ ] **App launch/focus:** Often only tested against the "already running" case — verify the cold-launch path (app not running, including from a state where its splash screen delays main-window creation) with a real timeout/poll, not a single immediate check.
- [ ] **Single-instance / already-running detection:** Often only tested with matching integrity levels during dev — verify it still works when Rig Toggle and Moza Companion run at different privilege levels (this should not happen per Pitfall 2, but verify explicitly).
- [ ] **Packaging:** Often "done" once it runs on the dev machine — verify on a clean VM/machine without dev tools or Defender exclusions already configured, to see the actual first-run SmartScreen/AV experience a real (even single) user would hit.
- [ ] **Toggle-twice / crash-recovery:** Often untested — verify behavior when toggling to rig mode twice in a row, and when the app is force-closed while in rig mode and relaunched.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|-----------------|
| Snapshot corrupted (wrong state saved as "normal") | MEDIUM | Manually restore correct monitor/audio configuration via Windows Settings, then perform one clean toggle cycle to re-baseline the snapshot; consider adding a manual "reset baseline snapshot" action in Settings for exactly this recovery case |
| `IPolicyConfig` breaks after a Windows update | HIGH (no code fix may exist immediately) | Fall back to manual switching via Windows Settings/Sound Control Panel as a stopgap; monitor community sources (NirSoft, audioswitch repo) for updated interop definitions; isolate the interop code so a fix is a single-file patch |
| Monitor disable silently no-ops due to a driver-specific CCD quirk | MEDIUM | Fall back to `CM_Disable_DevNode` (device-node disable) as an alternate mechanism for that specific hardware, accepting the elevation tradeoff (Pitfall 2) as a documented exception for that machine |
| Window left off-screen after a disable/restore cycle mismatch | LOW | Use Windows' built-in "Win+Shift+Arrow" or right-click taskbar "Move" to manually recover the window; consider adding a "reset window positions" utility action if this recurs |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| No public CCD "disconnect" API (Pitfall 1) | Spike/technical-approach phase (before feature build) | Confirm chosen mechanism (topology-path-removal vs. device-node disable) actually removes the monitor from `EnumDisplayMonitors`/Display Settings on real hardware, and that the target game no longer misbehaves |
| Elevation/UIPI conflicts (Pitfall 2) | Display-control phase + app-launch/window-management phase | Test window-focus of Moza Companion with Rig Toggle running unelevated, using whichever disable mechanism was chosen in Pitfall 1 |
| Race condition disabling monitor with windows on it (Pitfall 3) | Monitor-disable feature phase | Explicit test: maximized window and fullscreen game window on target monitor at disable-time; verify no window lost |
| Audio device ID instability (Pitfall 4) | State-snapshot/restore component | Test restore after simulating a "device ID changed" scenario (e.g., re-plug/driver update) and confirm graceful fallback/error, not silent misfire |
| Undocumented `IPolicyConfig` fragility (Pitfall 5) | Audio-control component | Verify all three roles are set; verify COM objects are released; run 10+ toggle cycles in one session to catch leak-related failures |
| Silent operation failures (Pitfall 6) | Display-control and audio-control components | Add and test post-mutation state verification for both display and audio changes |
| Incomplete state snapshot (Pitfall 7) | State-snapshot/restore component | Round-trip test: query, mutate, restore, re-query, diff against original — with a non-default monitor arrangement |
| Mode-tracking desync after crash/restart (Pitfall 8) | State-snapshot/restore component | Test: force-close app while in rig mode, relaunch, verify correct mode detection and successful restore |
| Launch/focus race with target app startup (Pitfall 9) | App-launch/window-management phase | Test cold-launch path with polling/timeout, not just already-running path |
| Unsigned .exe AV/SmartScreen flagging (Pitfall 10) | Packaging/distribution phase | Test the built .exe on a clean VM without prior Defender exclusions; document expected first-run warning behavior |

## Sources

- [Windows 11 – Is there a supported way to script/automate "Disconnect this display"? — Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/5662114/windows-11-is-there-a-supported-way-to-script-auto)
- [SetDisplayConfig function (winuser.h) — Microsoft Learn / sdk-api](https://github.com/MicrosoftDocs/sdk-api/blob/docs/sdk-api-src/content/winuser/nf-winuser-setdisplayconfig.md)
- [SetDisplayConfig summary and scenarios — Windows Drivers docs](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/setdisplayconfig-summary-and-scenarios)
- [Connecting and configuring displays (CCD) — Windows Drivers docs](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/connecting-and-configuring-displays)
- [CM_Disable_DevNode function — Microsoft Learn (cfgmgr32)](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_disable_devnode)
- [ChangeDisplaySettingsExW function — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw)
- [MultiMonitorTool — NirSoft](https://www.nirsoft.net/utils/multi_monitor_tool.html)
- [IPolicyConfig.h — tartakynov/audioswitch (GitHub)](https://github.com/tartakynov/audioswitch/blob/master/IPolicyConfig.h)
- [PolicyConfig.h — sgiurgiu/DefaultAudioChanger (GitHub)](https://github.com/sgiurgiu/DefaultAudioChanger/blob/master/DefaultAudioChanger/PolicyConfig.h)
- [AudioDeviceCmdlets — cdhunt/WindowsAudioDevice-Powershell-Cmdlet (GitHub)](https://github.com/cdhunt/WindowsAudioDevice-Powershell-Cmdlet)
- [SetForegroundWindow function — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
- [LockSetForegroundWindow function — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-locksetforegroundwindow)
- [How to get rid of/reset enumeration of audio devices — Sysnative Forums](https://www.sysnative.com/forums/threads/how-to-get-rid-of-reset-enumeration-of-audio-devices.45275/)
- [Global Audio Devices settings reset after Windows update — obs-studio issue #12050 (GitHub)](https://github.com/obsproject/obs-studio/issues/12050)
- [Seeking Feedback: Open-Source Solution for Stable Audio Device Identification on Windows — Microsoft Q&A](https://learn.microsoft.com/en-gb/answers/questions/2123506/seeking-feedback-open-source-solution-for-stable-a)
- [Dealing with Anti-Virus False Positives — Rick Strahl's Weblog](https://weblog.west-wind.com/posts/2016/oct/05/dealing-with-antivirus-false-positives)
- [/p:PublishTrimmed=true activates Windows Defender false positive — dotnet/runtime issue #33745 (GitHub)](https://github.com/dotnet/runtime/issues/33745)
- [Create a single file for application deployment — .NET, Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [How to report a false-positive in Microsoft SmartScreen — Ctrl blog](https://www.ctrl.blog/entry/how-to-false-smartscreen-positive.html)
- [Single-Instance .NET Apps: Mutexes, Named Pipes, UX](https://www.dotnet-guide.com/how-to-restrict-a-program-to-single-instance-in-net.html)

---
*Pitfalls research for: Windows system-level control desktop utility (display CCD/device APIs, default audio device switching, cross-process window management, standalone .exe packaging)*
*Researched: 2026-07-24*
