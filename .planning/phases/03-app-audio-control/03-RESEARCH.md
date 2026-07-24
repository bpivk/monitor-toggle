# Phase 3: App & Audio Control - Research

**Researched:** 2026-07-24
**Domain:** Windows COM interop (undocumented `IPolicyConfig` default-audio-device switching) + Win32 P/Invoke (cross-process window launch/focus/minimize)
**Confidence:** HIGH (Win32 window APIs, NAudio verification calls, COM disposal pattern) / MEDIUM (exact `IPolicyConfig` vtable layout — undocumented interface, cross-verified across 3 independent sources, one of which is confirmed WRONG — see Pitfall below)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Settings keeps exactly one audio device picker per mode (normal, rig) — not per-role pickers. Internally, `SetDefault`/`Restore` apply that single chosen device ID to all three Windows audio roles (`eConsole`, `eMultimedia`, `eCommunications`) via `IPolicyConfig::SetDefaultEndpoint`, matching what the Sound Control Panel itself does. Per-role granularity was explicitly rejected as over-engineering for a personal 2-device rig setup.
- **D-02:** `AudioState`/`CaptureState` must be expanded to capture the default device per role (not just the single `Role.Multimedia` read Phase 2 left in place) so restore can be exact across all three roles, per REQUIREMENTS.md AUDIO-02 ("across all relevant audio roles") and `PITFALLS.md`'s Pitfall 5/7 (partial-role switch, incomplete snapshot).
- **D-03:** After calling `SetDefaultEndpoint` for a role, re-query the actual default device for that role and compare against what was requested. If it doesn't match, `SetDefault` throws — this is a real, visible failure signal in Phase 3, not a silently-trusted HRESULT. Directly addresses `PITFALLS.md`'s Pitfall 6 (APIs reporting success while state doesn't actually change).
- **D-04:** This exception is allowed to bubble up through `ToggleService`/`MainForm`'s existing exception handling as-is for now. Richer per-step failure reporting (which step succeeded/failed, partial-failure recovery) is explicitly Phase 5 / CORE-04 scope — Phase 3 only needs the underlying verification logic to exist and to surface *something* rather than nothing.
- **D-05:** `ToggleToRigMode` must verify the configured companion app `.exe` path still exists on disk as the very first step — before capturing or mutating monitor or audio state. A missing path throws immediately with nothing yet touched, avoiding the current ordering (monitor disable → audio switch → app launch last) leaving monitor/audio already mutated when the app step fails.
- **D-06:** `LaunchOrFocus` behavior differs by case:
  - **Not running:** `Process.Start`, then poll `MainWindowHandle` for a few seconds (window is still opening) before giving up.
  - **Already running but `MainWindowHandle` is zero:** do NOT retry/poll — per CLAUDE.md, treat this as "running but no window to manipulate right now" (e.g. genuinely tray-only) and move on without failing the toggle. Retrying here would add a pointless multi-second delay for an app that may never produce a window.
- **D-07:** `MinimizeIfRunning` stays best-effort per PROJECT.md's existing scope decision — `ShowWindow(hWnd, SW_MINIMIZE)` when a window handle is available; a zero handle is a no-op, not a failure.

### Claude's Discretion

- Exact retry/poll duration and interval for the fresh-launch window-handle wait (D-06) — the discussion settled the *behavior* (retry only on fresh launch), not the precise seconds/interval; left to planner/researcher to pick a reasonable value (discussion referenced "a few seconds" as the ballpark). **This research recommends 250ms interval / 10s timeout — see Pattern 4.**
- COM interop specifics (vtable layout, GUIDs, object lifecycle/disposal per call) — per STACK.md, only the modern Windows 8+ `IPolicyConfig` variant is needed; no Vista fallback. **This research resolves this concretely — see Pattern 1.**

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope. (Full step-by-step partial-failure reporting/recovery UI is already correctly scoped to Phase 5 per ROADMAP.md CORE-04 — not built here, only the underlying verification signal per D-03/D-04.)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| APP-01 | Toggling to rig mode launches the configured companion app if it isn't already running | Pattern 4 (`Process.Start` + `Refresh()`-aware poll loop, 250ms/10s) |
| APP-02 | If the companion app is already running when toggling to rig mode, its window is brought to focus instead of launching a duplicate instance | Pattern 5 (`SetForegroundWindow` P/Invoke) + D-06 no-poll-when-already-running-and-handle-zero behavior |
| APP-03 | Toggling back to normal mode minimizes the companion app's window (best-effort) | Pattern 5 (`ShowWindow(hWnd, SW_MINIMIZE)`) + D-07 best-effort/no-op-on-zero-handle behavior |
| AUDIO-01 | User can switch the default audio output device to the configured rig speakers when toggling to rig mode | Pattern 1 (verified `IPolicyConfig` vtable) + Pattern 2 (set-all-3-roles-and-verify) |
| AUDIO-02 | User can restore the exact previous default audio device (across all relevant audio roles) when toggling back to normal mode | Pattern 3 (per-role `AudioState` expansion, D-02) + Pattern 2's same verify-and-throw logic reused for `Restore` |
</phase_requirements>

## Summary

This phase replaces four no-op stub methods (`WindowsAppController.LaunchOrFocus`/`MinimizeIfRunning`, `WindowsAudioController.SetDefault`/`Restore`) with real Windows implementations, plus expands `AudioState` to a per-role snapshot (D-02) and inserts a preflight app-path check as the first line of `ToggleService.ToggleToRigMode` (D-05). Both real implementations are well-trodden ground — every existing default-audio-switcher tool (EarTrumpet, SoundSwitch, audioswitch, AudioDeviceCmdlets) uses the exact same undocumented `IPolicyConfig` COM interface, and Win32 process/window launch-and-focus is a decades-stable, fully-documented API surface.

The single most load-bearing finding in this research: **not all publicly-copied `IPolicyConfig` C# interop files agree on the vtable layout**, and at least one widely-circulated copy (`aifdsc/AudioChanger`) is missing a method (`ResetDeviceFormat`) relative to the canonical `tartakynov/audioswitch` C++ header — using that buggy copy would silently call the wrong vtable slot. This report cross-verified the header against three independent sources (the original `tartakynov/audioswitch` C++ header, the actively-maintained EarTrumpet 2026 C# interop, and one confirmed-buggy community copy) and settled on the correct, verified 12-method layout below. Do not substitute a copy-pasted `IPolicyConfig.cs` found via a quick search without checking it against the method list in this document.

For app launch/focus: the two things that will bite a naive implementation are (1) `Process.MainWindowHandle` is cached the first time it's read and will NOT update on subsequent reads unless `Process.Refresh()` is called first — a single poll loop that doesn't call `Refresh()` every iteration will spin forever seeing `IntPtr.Zero`; and (2) `SetForegroundWindow` has documented, non-negotiable conditions under which it silently fails (falls back to flashing the taskbar icon) that have nothing to do with elevation — this is expected, acceptable behavior per this project's existing "best-effort focus" scope (D-07/ARCHITECTURE.md), not a bug to work around with `AttachThreadInput` hacks.

**Primary recommendation:** Hand-embed a single `IPolicyConfig` interop file using the 12-method Windows-7-and-later vtable layout verified below (matching EarTrumpet's current `IPolicyConfigWin7`/tartakynov's canonical header), call `SetDefaultEndpoint` for all three `ERole` values per switch, verify each via NAudio's `MMDeviceEnumerator.GetDefaultAudioEndpoint` immediately after, and throw on mismatch (D-03/D-04). For app control, use `Process.Start` + a `Refresh()`-aware poll loop (250ms interval, 10s timeout) only on fresh launch, and plain `ShowWindow`/`SetForegroundWindow` calls with no elevation tricks.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Default audio device switch (all 3 roles) + verify | Control Adapters (`RigToggle.Windows.WindowsAudioController`) | — | COM interop is OS-interop risk; must stay isolated from `ToggleService` per established Adapter/Facade pattern (ARCHITECTURE.md Pattern 1) |
| Audio state capture/restore per role | Control Adapters (`WindowsAudioController`) | Core (`AudioState` model) | Reading is still OS interop (NAudio COM-backed `MMDeviceEnumerator`); the resulting POCO shape lives in Core since `RigToggle.Core` has zero Windows API references |
| Companion app launch/focus/minimize | Control Adapters (`WindowsAppController`) | — | `Process`/`user32.dll` P/Invoke is OS interop; same isolation rule applies |
| App-path-exists preflight (D-05) | Orchestration (`ToggleService`) | — | Plain `File.Exists` is a BCL filesystem call, not Windows-API interop — it does not violate the "Core has zero Windows API references" rule and does not need an adapter interface; belongs directly in `ToggleService.ToggleToRigMode` as the first statement |
| Snapshot-before-mutate sequencing (unchanged) | Orchestration (`ToggleService`) | — | No change this phase beyond preflight insertion — reuses D-08 pattern already implemented |

## Standard Stack

### Core (unchanged from project-level STACK.md — no new packages this phase)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| NAudio | 2.3.0 (already installed, `RigToggle.Windows.csproj`) | Read-back verification of default endpoint per role after `SetDefaultEndpoint` (D-03) | Already the project's chosen enumeration library; `MMDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role)` is the correct read-side counterpart to the write-side `IPolicyConfig::SetDefaultEndpoint` [VERIFIED: naudio/NAudio GitHub, `NAudio.Wasapi/CoreAudioApi/MMDeviceEnumerator.cs`] |
| Hand-embedded `IPolicyConfig` COM interop (~40-60 lines, no package) | N/A | The only way to programmatically set the Windows default audio playback device for all three roles | Confirmed no public/supported alternative exists; every reference tool in this space uses this same reverse-engineered interface [CITED: PITFALLS.md Pitfall 5, cross-verified against 3 independent GitHub sources this session] |
| Win32 P/Invoke (`user32.dll`: `ShowWindow`, `SetForegroundWindow`, `IsIconic`) | N/A | Launch/focus/minimize companion app window | Standard, decades-stable Win32 surface [CITED: learn.microsoft.com/windows/win32/api/winuser] |
| `System.Diagnostics.Process` (BCL) | Included in .NET 10 | Launch process, poll `MainWindowHandle` with `Refresh()` | Already partially used (`IsRunning` in Phase 2); this phase adds `Process.Start` + polling |

**No new NuGet packages are installed in this phase.** `RigToggle.Windows.csproj` already references `NAudio 2.3.0` and `WindowsDisplayAPI 1.3.0.13` (the latter unused until Phase 4). The `IPolicyConfig` interop is hand-written source, not a package reference, per project-level STACK.md's explicit recommendation (avoids depending on the unmaintained `AudioSwitcher.AudioApi` 3.0.3, last released May 2023).

## Package Legitimacy Audit

**Not applicable this phase.** No new external packages are installed — the audio-switch mutation is implemented via hand-embedded COM interop source (not a package reference), and the app-control mutation uses only BCL (`System.Diagnostics.Process`) and P/Invoke (`user32.dll`, part of the OS, not a package). Package Legitimacy Gate protocol was reviewed and does not apply.

## Architecture Patterns

### System Architecture Diagram (Phase 3 scope only)

```
ToggleService.ToggleToRigMode()
    │
    ├─ [NEW D-05] File.Exists(settings.CompanionAppPath) ──✗ throw "app not found" (nothing touched yet)
    │
    ├─ CaptureState() [existing, real since Phase 2]
    │     └─ MonitorController.CaptureState() / AudioController.CaptureState()
    │            [D-02: now reads GetDefaultAudioEndpoint for eConsole, eMultimedia,
    │             eCommunications — was Role.Multimedia only]
    │
    ├─ SnapshotStore.Save(...)  [existing — persisted before mutation]
    │
    ├─ MonitorController.Disable(...)  [still fake — Phase 4]
    │
    ├─ [NEW] AudioController.SetDefault(rigDeviceId)
    │     └─ for each role in {eConsole, eMultimedia, eCommunications}:
    │           1. new _CPolicyConfigClient() as IPolicyConfig
    │           2. SetDefaultEndpoint(rigDeviceId, role)
    │           3. Marshal.ReleaseComObject(policyConfig)          [D-lifecycle]
    │           4. re-query: enumerator.GetDefaultAudioEndpoint(Render, matchingNAudioRole)
    │           5. compare device.ID == rigDeviceId → else throw   [D-03/D-04]
    │
    └─ [NEW] AppController.LaunchOrFocus(companionAppPath)
          ├─ IsRunning? [existing, real]
          │     ├─ yes, MainWindowHandle != 0 → SetForegroundWindow (best-effort)
          │     ├─ yes, MainWindowHandle == 0 → no-op, do NOT poll (D-06)
          │     └─ no  → Process.Start(companionAppPath)
          │              → poll loop: Sleep(250ms) → process.Refresh() → check MainWindowHandle
          │                 up to 10s total → SetForegroundWindow if found, else give up silently

ToggleService.ToggleToNormalMode()
    ├─ MonitorController.Restore(...)  [still fake — Phase 4]
    ├─ [NEW] AudioController.Restore(previousAudioState)
    │     └─ for each of the 3 captured per-role snapshots:
    │           resolve deviceId (fallback to friendly-name match if ID missing — Pitfall 4)
    │           → same SetDefaultEndpoint + verify-and-throw sequence as SetDefault, per role
    └─ [NEW] AppController.MinimizeIfRunning(companionAppPath)
          └─ IsRunning? → MainWindowHandle != 0 → ShowWindow(hWnd, SW_MINIMIZE)
                        → MainWindowHandle == 0 → no-op (D-07, best-effort only)
```

### Pattern 1: Verified 12-method `IPolicyConfig` vtable layout (Windows 7 and later — the only variant this project needs)

**What:** The COM interface must declare every vtable slot in the *exact* order the native interface defines them, even if most are never called from C#. Cross-verifying three independent sources this session (tartakynov/audioswitch's original C++ header — the widely-cited canonical source; EarTrumpet's actively-maintained 2026 C# interop; and one confirmed-buggy community copy) produced this authoritative, agreed-upon layout for the Windows-7-and-later variant (the only one this project needs — no Vista fallback per STACK.md):

1. `GetMixFormat`
2. `GetDeviceFormat`
3. `ResetDeviceFormat` ← **missing in at least one commonly-circulated copy (`aifdsc/AudioChanger`) — a confirmed vtable bug in that source; do not use it as a reference**
4. `SetDeviceFormat`
5. `GetProcessingPeriod`
6. `SetProcessingPeriod`
7. `GetShareMode`
8. `SetShareMode`
9. `GetPropertyValue`
10. `SetPropertyValue`
11. **`SetDefaultEndpoint(string wszDeviceId, ERole eRole)`** ← the only method this project calls
12. `SetEndpointVisibility`

**When to use:** This exact 10-stub-then-`SetDefaultEndpoint`-then-1-stub shape must be preserved in the hand-written C# interface declaration, regardless of which of the two acceptable declaration styles is used (typed no-op stub methods returning `int`/HRESULT via `[PreserveSig]`, as in the classic `tartakynov`-derived copies; or generically-named `Unused1..Unused10` `void` placeholders, as in EarTrumpet's current file — both produce an identical vtable and are equally correct).

**Example (recommended: `[PreserveSig]` + `int` HRESULT style, since it lets `SetDefaultEndpoint`'s return code be checked directly without a marshaled exception, useful alongside the D-03 read-back verification):**
```csharp
// Source: cross-verified against tartakynov/audioswitch/IPolicyConfig.h (canonical C++
// header) and EarTrumpet dev branch IPolicyConfigWin7 (2026) — both agree on this
// 12-method / SetDefaultEndpoint-at-slot-11 layout. Windows 7 and later only (no Vista
// fallback needed per project STACK.md).
using System.Runtime.InteropServices;

namespace RigToggle.Windows.Audio;

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2,
}

[Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    [PreserveSig] int GetMixFormat();
    [PreserveSig] int GetDeviceFormat();
    [PreserveSig] int ResetDeviceFormat();
    [PreserveSig] int SetDeviceFormat();
    [PreserveSig] int GetProcessingPeriod();
    [PreserveSig] int SetProcessingPeriod();
    [PreserveSig] int GetShareMode();
    [PreserveSig] int SetShareMode();
    [PreserveSig] int GetPropertyValue();
    [PreserveSig] int SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceId, ERole role);
    [PreserveSig] int SetEndpointVisibility();
}

[ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
internal class PolicyConfigClient { }
```
[VERIFIED via source read: github.com/tartakynov/audioswitch/blob/master/IPolicyConfig.h (canonical, `@author EreTIk`); github.com/File-New-Project/EarTrumpet/blob/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs (2026, actively maintained)]
[Interface GUID `F8679F50-850A-41CF-9C72-430F290290C8` and CLSID `870AF99C-171D-4F9E-AF0D-E63DF40C2BC9` also match STACK.md's already-locked GUIDs.]

### Pattern 2: Set all three roles, verify each, release COM object each cycle

**What:** For every audio switch (both `SetDefault` and `Restore`), call `SetDefaultEndpoint` once per `ERole` value, and after each individual role's call, re-query the actual current default via NAudio and compare — do not batch the verification after all three roles, since one role can succeed while another silently fails (Pitfall 5/6).

**Example:**
```csharp
// Source: pattern cross-verified against aifdsc/AudioChanger's SetDefaultDevice
// (calls SetDefaultEndpoint 3x, once per role) + PITFALLS.md D-03/D-04 verification
// requirement (not present in any single reference implementation found — this
// project's own addition, since every existing tool trusts the HRESULT alone).
private static readonly (ERole Native, NAudio.CoreAudioApi.Role Managed)[] Roles =
{
    (ERole.eConsole,        NAudio.CoreAudioApi.Role.Console),
    (ERole.eMultimedia,     NAudio.CoreAudioApi.Role.Multimedia),
    (ERole.eCommunications, NAudio.CoreAudioApi.Role.Communications),
};

private void SetDefaultForAllRoles(string deviceId)
{
    foreach (var (nativeRole, managedRole) in Roles)
    {
        var client = (IPolicyConfig)new PolicyConfigClient();
        try
        {
            int hr = client.SetDefaultEndpoint(deviceId, nativeRole);
            if (hr != 0) // S_OK
            {
                throw new InvalidOperationException(
                    $"SetDefaultEndpoint failed for role {nativeRole} (HRESULT 0x{hr:X8}).");
            }
        }
        finally
        {
            Marshal.ReleaseComObject(client); // release every cycle — Pitfall 5 COM leak
        }

        // D-03/D-04: verify-and-throw, not trust-the-HRESULT (Pitfall 6)
        using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
        using var actual = enumerator.GetDefaultAudioEndpoint(
            NAudio.CoreAudioApi.DataFlow.Render, managedRole);
        if (!string.Equals(actual.ID, deviceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Audio default for role {managedRole} did not change to the requested " +
                $"device after SetDefaultEndpoint (expected '{deviceId}', got '{actual.ID}').");
        }
    }
}
```
NAudio's `Role` enum values (`Console=0, Multimedia=1, Communications=2`) [VERIFIED: github.com/naudio/NAudio, `NAudio.Wasapi/CoreAudioApi/Role.cs`] numerically match the native `ERole` enum used by `IPolicyConfig`, confirming the pairing above is not a coincidental cast.

### Pattern 3: `AudioState` expansion for per-role snapshot (D-02)

**What:** Replace `record AudioState(string? DefaultDeviceId)` with a structure holding one snapshot per role, each with ID + friendly-name fallback (Pitfall 4).

**Example:**
```csharp
namespace RigToggle.Core.Models;

public sealed record AudioRoleState(string? DeviceId, string? DeviceName);

public sealed record AudioState(
    AudioRoleState Console,
    AudioRoleState Multimedia,
    AudioRoleState Communications);
```
This is a breaking change to `AudioState`'s constructor — `JsonSnapshotStore`/`StateSnapshot` serialization will pick up the new shape automatically via `System.Text.Json` (no custom converter needed for a plain record), but any existing `state.json` written by Phase 2 with the old single-field shape will fail to deserialize on next load. Since this is a personal, single-user, pre-v1 tool with no shipped installs yet, this is acceptable — no migration path needed, but the planner should decide whether `JsonSnapshotStore.Load()` needs a defensive try/catch returning `null` (treated as "no snapshot" / normal mode) if a stale-shaped `state.json` is present on disk during development.

### Pattern 4: `Process.Refresh()`-aware polling for fresh launch (D-06)

**What:** `Process.MainWindowHandle` is cached on first read; it will NOT reflect a window created after that first read unless `Process.Refresh()` is called before each subsequent read. A naive single-check-then-give-up (or a poll loop missing `Refresh()`) will always see `IntPtr.Zero` even after the target app's window exists.

**Example:**
```csharp
// Source: learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.mainwindowhandle
// ("You must use the Refresh method to refresh the Process object to get the most up to
// date main window handle if it has changed... because the window handle is cached, use
// Refresh beforehand to guarantee that you'll retrieve the current handle.")
private static readonly TimeSpan LaunchPollTimeout = TimeSpan.FromSeconds(10);
private static readonly TimeSpan LaunchPollInterval = TimeSpan.FromMilliseconds(250);

private void LaunchAndWaitForWindow(string exePath)
{
    using var process = Process.Start(exePath)
        ?? throw new InvalidOperationException($"Failed to start '{exePath}'.");

    var deadline = DateTime.UtcNow + LaunchPollTimeout;
    while (DateTime.UtcNow < deadline)
    {
        process.Refresh(); // REQUIRED — omitting this makes MainWindowHandle never update
        if (process.MainWindowHandle != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(process.MainWindowHandle); // best-effort
            return;
        }
        Thread.Sleep(LaunchPollInterval);
    }
    // Timed out — D-06 says don't fail the whole toggle; app is launched, just not focused yet.
}
```
250ms/10s chosen from PITFALLS.md's own recommendation ("check every 200-300ms for up to 10-15 seconds") [CITED: PITFALLS.md Pitfall 9], picking the low end of the timeout range since CONTEXT.md's discussion ballparked "a few seconds" — 10s comfortably covers a slow/cold companion-app launch without making a failed launch feel hung.

### Pattern 5: Win32 P/Invoke signatures needed

```csharp
// Source: learn.microsoft.com/windows/win32/api/winuser — stable since Windows 2000,
// unchanged surface. All three calls operate on window handles already obtained via
// Process.MainWindowHandle — no FindWindow/FindWindowEx by title (STACK.md "What NOT to Use").
internal static class NativeMethods
{
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    public static void Minimize(IntPtr hWnd) => ShowWindow(hWnd, SW_MINIMIZE);
    public static void RestoreIfMinimized(IntPtr hWnd)
    {
        if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
    }
}
```
`SW_MINIMIZE = 6`, `SW_RESTORE = 9` are the standard, decades-stable `ShowWindow` command constants [CITED: learn.microsoft.com/windows/win32/api/winuser/nf-winuser-showwindow].

### Anti-Patterns to Avoid

- **Trusting a copy-pasted `IPolicyConfig.cs` without checking its method count against Pattern 1's 12-method list.** At least one commonly-circulated copy (`aifdsc/AudioChanger`) is missing `ResetDeviceFormat`, shifting every subsequent vtable slot by one — `SetDefaultEndpoint` would silently call `SetEndpointVisibility`'s native slot instead. This is exactly Pitfall 5's "version-dependent vtable layouts" risk, concretely confirmed this session, not just theoretical.
- **Calling `SetDefaultEndpoint` for only one role** ("`eConsole` only" — explicitly called out as never-acceptable in PITFALLS.md's Technical Debt Patterns table). D-01 requires all three.
- **Reading `MainWindowHandle` once, immediately after `Process.Start`, with no poll loop or `Refresh()` call.** Both mistakes independently produce a false "no window" result.
- **Implementing `AttachThreadInput`/simulated-keystroke `SetForegroundWindow` bypass tricks.** Not needed here: D-07/ARCHITECTURE.md already treat a taskbar-flash fallback as acceptable, and Rig Toggle + Moza Companion run at matching (non-elevated) integrity levels per D-08, so the main real-world trigger for `SetForegroundWindow` "silently failing" (the user actively using another window at that instant) is expected, not a bug.
- **Caching a `PolicyConfigClient`/`IPolicyConfig` COM object across toggle calls.** Create fresh, use, `Marshal.ReleaseComObject`, discard — matching the existing `WindowsAudioController` convention of never caching `MMDeviceEnumerator` across calls.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Determining the correct `IPolicyConfig` vtable layout from scratch | Reverse-engineering the interface yourself via a disassembler/experimentation | The verified 12-method layout in Pattern 1 above, cross-checked against 3 independent sources this session | Already solved correctly by the community; re-deriving it risks reproducing the exact vtable-offset bug this research caught in a circulating copy |
| Reading back the current default audio device for verification | A second custom COM interop call | NAudio's existing `MMDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role)` — already a project dependency, already proven working in `WindowsAudioController.CaptureState` | One less COM surface to hand-maintain; NAudio wraps the fully public/documented `IMMDeviceEnumerator`, unlike the write-side `IPolicyConfig` |
| Waiting for a freshly-launched process's window to appear | A custom message-hook or `WaitForInputIdle`-only wait | `Process.Refresh()` + poll loop (Pattern 4) | `WaitForInputIdle` only confirms the process's UI thread is idle/pumping messages — it does not guarantee a *main window* exists yet for apps with splash screens or delayed window creation; a poll loop directly checks the actual condition needed |

**Key insight:** Every piece of "custom" code this phase needs (vtable layout, verification call, poll loop) already has a proven, checkable reference implementation — the risk in this domain is copying a subtly wrong reference, not needing to invent one from scratch.

## Common Pitfalls

### Pitfall A: Vtable layout mismatch between circulating `IPolicyConfig.cs` copies (NEW finding this session, sharpens PITFALLS.md Pitfall 5)

**What goes wrong:** A plausible-looking, GUID-correct `IPolicyConfig.cs` found via a quick web search can still have the wrong number of stub methods before `SetDefaultEndpoint`, because at least one popular copy (`aifdsc/AudioChanger`, and any project copied from it) omits `ResetDeviceFormat`. Calling `SetDefaultEndpoint` through that interface actually invokes the native `SetDeviceFormat` or `SetEndpointVisibility` vtable slot instead, with unpredictable results (crash, no-op, or corrupting an unrelated audio setting) — with no compiler or runtime error, since COM vtable calls are just an offset jump.

**Why it happens:** The interface is entirely undocumented; every C# copy is a manual transcription of a reverse-engineered C++ header, and small transcription errors (a dropped method) are easy to introduce and easy to miss since the code compiles and often "mostly works" if the wrong slot happens to also take a similar `(string, int)`-shaped signature.

**How to avoid:** Use the verified 12-method layout in Pattern 1 above, or if sourcing from elsewhere, count the methods against this list before trusting it: `GetMixFormat, GetDeviceFormat, ResetDeviceFormat, SetDeviceFormat, GetProcessingPeriod, SetProcessingPeriod, GetShareMode, SetShareMode, GetPropertyValue, SetPropertyValue, SetDefaultEndpoint, SetEndpointVisibility` (12 total, `SetDefaultEndpoint` is #11).

**Warning signs:** `SetDefaultEndpoint` "succeeds" (HRESULT 0) but the D-03 verification read-back shows the device didn't change — this specific failure mode is one of the reasons D-03/D-04 exist, and this project's verify-and-throw design will actually catch a vtable-offset bug at runtime rather than silently misbehaving, which is a meaningful extra safety net beyond what any single reference implementation provides.

**Phase to address:** This phase — the interop file must be written or reviewed against Pattern 1's method list before being considered done.

### Pitfall B: `Process.MainWindowHandle` caching (sharpens PITFALLS.md Pitfall 9 with the exact mechanism)

**What goes wrong:** Already covered under Pattern 4 above — a poll loop that reads `process.MainWindowHandle` repeatedly without calling `process.Refresh()` first will observe `IntPtr.Zero` forever, even long after the target app's window actually exists.

**How to avoid:** Call `process.Refresh()` as the first statement inside every loop iteration, before reading `MainWindowHandle` (see Pattern 4 code).

**Phase to address:** This phase.

### Pitfall C: COM object lifecycle for the hand-embedded `IPolicyConfig` client

**What goes wrong:** If the RCW (`PolicyConfigClient`/`IPolicyConfig` instance) is cached as a field and reused across many toggle cycles in one running session, or simply never released, repeated toggling can eventually degrade (PITFALLS.md Pitfall 5's "works fine the first few times, then fails" pattern) — this is the same class of bug the existing `WindowsAudioController` code already avoids for `MMDeviceEnumerator`/`MMDevice`.

**How to avoid:** Create a new `PolicyConfigClient` instance per `SetDefaultEndpoint` call (or at minimum, once per `SetDefault`/`Restore` invocation), and call `Marshal.ReleaseComObject(client)` in a `finally` block immediately after use (see Pattern 2 code). This matches the existing codebase convention exactly — `WindowsAudioController.GetPlaybackDevices`/`CaptureState` already dispose their `MMDeviceEnumerator`/`MMDevice` per call via `using`.

**Phase to address:** This phase.

## Code Examples

See Architecture Patterns 1-5 above for the complete, verified code for:
- The 12-method `IPolicyConfig` interface + `PolicyConfigClient` COM-import class (Pattern 1)
- Set-all-three-roles-and-verify (Pattern 2)
- Expanded per-role `AudioState` model (Pattern 3)
- `Refresh()`-aware launch/poll loop (Pattern 4)
- `user32.dll` `ShowWindow`/`SetForegroundWindow`/`IsIconic` P/Invoke signatures (Pattern 5)

## State of the Art

| Old Approach (Phase 2, current code) | Current Approach (this phase) | When Changed | Impact |
|--------------------------------------|-------------------------------|---------------|--------|
| `AudioState(string? DefaultDeviceId)`, reads only `Role.Multimedia` | `AudioState(AudioRoleState Console, Multimedia, Communications)`, reads all 3 roles | This phase (D-02) | Breaking change to `AudioState`'s shape and any on-disk `state.json`; no migration needed pre-v1 |
| `SetDefault`/`Restore` no-op stubs | Real `IPolicyConfig` COM interop, all 3 roles, verify-and-throw | This phase (D-01/D-03/D-04) | Audio switching becomes real and self-verifying |
| `LaunchOrFocus`/`MinimizeIfRunning` no-op stubs | Real `Process.Start` + poll, `ShowWindow`/`SetForegroundWindow` | This phase (APP-01/02/03) | App control becomes real |
| `ToggleToRigMode` orders monitor → audio → app | App-path preflight (`File.Exists`) inserted as step 0, before any capture/mutation | This phase (D-05) | A missing companion-app path now fails fast with nothing touched, instead of failing last after monitor+audio already mutated |

**Deprecated/outdated:** None — no library/API in this phase has a newer replacement to migrate to; `IPolicyConfig` remains the only mechanism as of current Windows 11 builds (confirmed via SoundVolumeView v2.53, July 2026, per project-level STACK.md).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IPolicyConfig::SetDefaultEndpoint` returns HRESULT `0` (S_OK) specifically on success, with no other non-error success code in practice | Pattern 2 code example | Low — even if some non-zero-but-non-error HRESULT exists, the D-03 read-back verification independently catches a true failure; the HRESULT check is a fast-path optimization, not the sole safety net |
| A2 | 250ms/10s poll interval/timeout is a reasonable default for this specific Moza Companion app's real-world startup time | Pattern 4 | Low-medium — if Moza Companion's actual cold-start is unusually slow (e.g., > 10s due to a slow disk or startup update check), the poll will time out and the window won't be focused on first launch; app itself still launches fine (D-06 says don't fail the toggle), user can manually click it once — not a functional regression, just a UX rough edge; easy to bump if observed in practice |
| A3 | No `AttachThreadInput`/simulated-input workaround is needed for `SetForegroundWindow` against Moza Companion specifically | Anti-Patterns | Low — if Moza Companion's window genuinely never receives focus in practice (beyond the documented taskbar-flash fallback), this is a UX polish item, not a correctness bug, and is explicitly already scoped as acceptable per D-07/ARCHITECTURE.md |

**If this table is empty:** N/A — see entries above; all are LOW risk and do not block planning.

## Open Questions

1. **Should `AudioState`'s serialization break be handled with a defensive `try/catch` in `JsonSnapshotStore.Load()`, or is a clean/manual `state.json` delete acceptable during development?**
   - What we know: The record's constructor shape is changing (D-02); `System.Text.Json` will throw on deserializing the old shape.
   - What's unclear: Whether any `state.json` currently exists on the dev/rig machine from prior Phase 2 testing that would need to survive this change.
   - Recommendation: Treat as low-stakes (pre-v1, single developer) — either delete any stale `state.json` before running Phase 3 code, or add a defensive catch-and-treat-as-null in `JsonSnapshotStore.Load()` if the planner wants zero-friction dev iteration. Either is acceptable; not a design decision requiring user confirmation.

2. **Does `IAppController`/`IAudioController` need new interface methods (e.g., an explicit `Exists(path)` on `IAppController`), or does the D-05 preflight stay as a raw `File.Exists` call inline in `ToggleService`?**
   - What we know: CONTEXT.md's Integration Points section explicitly leaves this to the planner.
   - What's unclear: Nothing technical — this is a style/testability choice, not a capability gap.
   - Recommendation: Keep it as a plain `System.IO.File.Exists(settings.CompanionAppPath)` call directly in `ToggleService.ToggleToRigMode`, per the Architectural Responsibility Map above — it's a BCL filesystem check, not Windows-API interop, so it doesn't need adapter-interface indirection, and `ToggleServiceTests.cs` can already exercise both branches via a settings fixture pointing at a real vs. nonexistent path without needing a fake controller method.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK (dotnet CLI) | Compiling/testing all C# code this phase touches | ✗ (this research/execution sandbox is Linux, no `dotnet` installed) | — | None in this sandbox — code changes can be written here, but compilation, unit tests (`RigToggle.Tests`), and any real-hardware verification must happen on the actual Windows dev/rig machine, matching the pattern already established by the Phase 1 spike (`spike/MonitorDetachSpike`) which was necessarily run on real Windows hardware |
| Windows 10/11 runtime (for `IPolicyConfig`/COM, `user32.dll`, `Process.MainWindowHandle`) | All of this phase's real functionality | ✗ (Linux sandbox) | — | Same as above — no in-sandbox fallback; this is expected for a Windows-only utility and mirrors how Phases 1-2 were already validated outside this environment |

**Missing dependencies with no fallback:**
- Ability to compile, run, or test this phase's code in the current execution environment. This does not block *planning* (all patterns above are verified against documentation/source, not by running them here) but does mean the plan's verification/testing tasks must assume execution happens on the real Windows target machine, as prior phases evidently already did (existing `WindowsAudioController`/`WindowsMonitorController`/spike code already compiles/targets `net10.0-windows`, which cannot be exercised in this sandbox either).

## Project Constraints (from CLAUDE.md)

- Hand-embed the `IPolicyConfig` COM interop directly as source (do NOT add a NuGet reference to `AudioSwitcher.AudioApi` or any other packaged wrapper) — CLAUDE.md explicitly calls out embedding as preferred over that unmaintained package.
- Use NAudio only for enumeration/read-back, never for the "set default" mutation itself (no such NAudio API exists) — matches this research's Pattern 2 exactly.
- Do not add any elevation manifest (`requireAdministrator`) anywhere in the solution — CLAUDE.md and existing `.csproj` comments both flag this as required to keep cross-process `SetForegroundWindow` against Moza Companion working under UIPI; all three `.csproj` files already contain an explicit comment confirming no elevation manifest is present — this phase must not add one.
- Do not use `FindWindow`/`FindWindowEx` by window title as the primary running-detection mechanism — already correctly avoided by the existing `IsRunning` implementation; this phase's `LaunchOrFocus`/`MinimizeIfRunning` additions must continue relying on `Process.GetProcessesByName` + `MainWindowHandle`, not title matching.
- Do not use `PInvoke.User32` or other Win32-wrapper NuGet packages — hand-rolled `DllImport` signatures are the explicit recommendation for a surface this small (CLAUDE.md Alternatives Considered table).
- `RigToggle.Core` must have zero Windows API references (existing `.csproj` comment, D-08 structural enforcement) — the D-05 preflight (`File.Exists`) is safe to add directly to `ToggleService` since it's a plain BCL call, not a Windows API; the COM interop and P/Invoke code must live entirely in `RigToggle.Windows`.

## Sources

### Primary (HIGH confidence)
- https://raw.githubusercontent.com/tartakynov/audioswitch/master/IPolicyConfig.h — canonical, original `IPolicyConfig`/`IPolicyConfigVista` C++ header (fetched and read directly, full text) — confirmed the 12-method Windows-7+ layout, both GUIDs, and the Vista-variant differences
- https://raw.githubusercontent.com/File-New-Project/EarTrumpet/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs — actively-maintained (2026) C# interop, confirms the same 12-method layout using an `Unused1..8` stub-naming style, corroborating tartakynov's header
- https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.mainwindowhandle — official confirmation that `MainWindowHandle` is cached and `Refresh()` is required to get an updated value
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow — official documented conditions under which `SetForegroundWindow` succeeds/fails, and the taskbar-flash fallback behavior
- https://raw.githubusercontent.com/naudio/NAudio/master/NAudio.Wasapi/CoreAudioApi/Role.cs — confirmed `Role` enum values (`Console, Multimedia, Communications` = 0,1,2), matching native `ERole`
- https://raw.githubusercontent.com/naudio/NAudio/master/NAudio.Wasapi/CoreAudioApi/MMDeviceEnumerator.cs — confirmed `GetDefaultAudioEndpoint(DataFlow, Role)` signature and the `Marshal.ReleaseComObject`-based `Dispose` pattern already mirrored in this project's existing code

### Secondary (MEDIUM confidence)
- https://raw.githubusercontent.com/aifdsc/AudioChanger/master/AudioChanger/IPolicyConfig.cs — used specifically as a confirmed-buggy counter-example (missing `ResetDeviceFormat`), not as a reference to copy from
- Web search corroboration of the vtable-ordering sensitivity and `AttachThreadInput` foreground-focus workaround pattern (multiple independent community sources, cross-referenced against the official Microsoft Learn `SetForegroundWindow` page)

### Tertiary (LOW confidence)
- None — every load-bearing claim in this document was cross-verified against at least one primary/official source or two independent secondary sources.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, existing NAudio/BCL/P-Invoke surface, already proven in this codebase
- Architecture (COM interop vtable layout): MEDIUM — undocumented interface, but cross-verified against 3 independent sources this session with a concrete, confirmed discrepancy caught and resolved
- Architecture (Win32 window control): HIGH — fully documented, stable Win32 API
- Pitfalls: HIGH — the vtable and `MainWindowHandle`-caching pitfalls were independently confirmed via direct source/documentation reads, not just training-data recall

**Research date:** 2026-07-24
**Valid until:** ~90 days for the Win32/`.NET` BCL portions (extremely stable APIs); the undocumented `IPolicyConfig` interface should be re-checked against current EarTrumpet/SoundVolumeView source if a future Windows feature update changes audio-switching behavior (per PITFALLS.md Pitfall 5's own caveat that this interface "could break in a future Windows build with zero warning")
