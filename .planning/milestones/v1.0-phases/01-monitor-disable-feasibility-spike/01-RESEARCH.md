# Phase 1: Monitor-Disable Feasibility Spike - Research

**Researched:** 2026-07-24
**Domain:** Windows CCD (Connecting and Configuring Displays) API / device-node management, on AMD Radeon DisplayPort hardware, driven by a hand-built-and-run-by-the-user throwaway console tool
**Confidence:** MEDIUM — the managed API surface (`WindowsDisplayAPI`) and the underlying `SetDisplayConfig` mechanism are HIGH confidence (verified against library source and Microsoft docs); the AMD-specific driver behavior is genuinely unverified anywhere publicly — that is the entire reason this phase exists

## Summary

This phase is a go/no-go spike, not a feature build. The tool to hand the user is a single-file `dotnet new console` project that (1) enumerates active display paths via the `WindowsDisplayAPI` NuGet wrapper around `QueryDisplayConfig`, (2) lets the user identify the target (DisplayPort, primary) monitor by friendly name/device path, (3) calls `PathInfo.ApplyPathInfos()` with a path array that omits that monitor's path — the topology-path-removal technique already confirmed (by reading the library's source in project-level `STACK.md`) to invoke `SetDisplayConfig` with `SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES` — and (4) re-enumerates through a **second, independent** data path (`System.Windows.Forms.Screen.AllScreens`, which itself wraps `EnumDisplayMonitors`) to confirm the monitor is actually gone, not just that the API call returned success.

No public source — including AMD's own developer documentation, Microsoft's CCD docs, or the community tools that already do this (NirSoft MultiMonitorTool, the newer "Monarch" detach utility) — states a GPU-vendor-specific difference in `SetDisplayConfig` topology-removal behavior between AMD and NVIDIA. The commonly-referenced examples and reference implementations found during this research are vendor-agnostic or NVIDIA-flavored; none document an AMD-specific quirk, and none document that it reliably works on AMD either. **This is a genuine gap, not an oversight** — it is precisely what empirical testing on the actual rig hardware must resolve, and no amount of further web research substitutes for that test. Budget for at least one full build→run→report round-trip failing (e.g., monitor blanks but doesn't disappear from `Screen.AllScreens`) and a second round-trip with the `CM_Disable_DevNode`/`pnputil /disable-device` fallback path.

**Primary recommendation:** Ship the user a single `Program.cs` + `.csproj` using `WindowsDisplayAPI` for the primary (non-elevated) topology-path-removal approach, with `System.Windows.Forms.Screen.AllScreens` as an independent verification oracle, and hold `pnputil /disable-device` (built into Windows, no extra download, requires an elevated terminal) in reserve as the fallback the user can try manually from an admin PowerShell/cmd window if the primary approach only blanks the monitor instead of removing it from enumeration.

## Architectural Responsibility Map

This phase operates entirely within a single native Windows process talking to OS subsystems — there is no browser/API/CDN tiering here. The relevant "tiers" are OS/driver layers, not web-app layers:

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Display topology enumeration (which paths are active, target friendly names) | OS Display Subsystem (CCD API via `QueryDisplayConfig`) | Console spike app (reads via `WindowsDisplayAPI`) | CCD is the OS's own source of truth for topology; the app only reads it |
| Display topology mutation (detach primary monitor's path) | OS Display Subsystem / GPU driver (WDDM, `SetDisplayConfig`) | Console spike app (constructs the new path array) | The app decides *what* topology to request; the GPU driver decides whether it can honor it — this is exactly the AMD-vs-NVIDIA unknown |
| Verification that detach actually took effect | Console spike app (cross-checks two independent enumeration APIs) | OS Display Subsystem (source of both) | Per project PITFALLS.md Pitfall 6 — a success return code is not proof of effect; the app must re-query, not trust the call |
| Device-node disable (fallback mechanism) | OS PnP/Device Manager subsystem (`cfgmgr32`/`pnputil`) | Elevated helper process or manual admin terminal | Heavier, driver-instance-level operation; must never run inside the main non-elevated process (Pitfall 2 / D-08) |
| Elevation isolation | Separate elevated helper process (or manual admin shell for the spike) | Main console app (stays `asInvoker`) | Protects future cross-process window-focus code (Phase 3) from UIPI breakage — decided in CONTEXT.md D-08, applies even to this throwaway tool's design |

## Project Constraints (from CLAUDE.md)

`./CLAUDE.md` in this repo is GSD-generated (mirrors PROJECT.md/STACK.md, no custom hand-written rules beyond that). Directives relevant to this phase:

- **Platform**: Windows only — the spike tool has no cross-platform requirement and should not attempt to be portable.
- **Monitor control constraint** (carried from PROJECT.md): "Must achieve true OS-level display disable/enable... not merely a monitor power signal" — this is the exact hypothesis this phase tests, not something to assume true.
- **GSD Workflow Enforcement**: file-changing work should route through GSD commands (`/gsd-execute-phase`, etc.) rather than ad hoc edits — applies to how the *planner*/executor structure the work, not to the research content itself.
- **Distribution constraint** ("standalone .exe requiring no separate runtime") is a Phase 5 packaging concern — **does not apply to this spike**. The spike tool may (and should) require the user to install the .NET SDK; it is explicitly throwaway and never gets packaged/shipped.

No constraints here conflict with the CONTEXT.md decisions; both sources agree the spike is scoped narrowly to feasibility, not production packaging.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|---------------|
| .NET SDK | 10.0.302 (current, released 2026-07-14) [VERIFIED: web search cross-referencing dotnet.microsoft.com/download and versionsof.net] | Build/run the console spike | Matches the project-level STACK.md's already-chosen runtime; no reason to use a different SDK version for a throwaway tool that shares code lineage with the eventual production adapter |
| C# | 13 (ships with .NET 10 SDK) [CITED: ships-with relationship, Microsoft .NET 10 release notes] | Language | Default; no reason to deviate |
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 [VERIFIED: nuget.org package page + GitHub source read] | Managed wrapper over CCD `QueryDisplayConfig`/`SetDisplayConfig` | Already selected and verified in project-level STACK.md by direct source read of `PathInfo.cs`; reusing it here (rather than hand-rolling raw P/Invoke for the spike) minimizes the chance that a spike failure is actually a struct-marshalling bug in the spike code, not a genuine driver limitation |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Windows.Forms` (`UseWindowsForms=true`, part of .NET 10 Windows Desktop SDK, no NuGet install needed) | Bundled with SDK | `Screen.AllScreens` / `Screen.PrimaryScreen` as a **second, independent** verification source (wraps `EnumDisplayMonitors` under the hood) | Use immediately after every `ApplyPathInfos()` call to confirm the monitor count actually dropped — never trust `WindowsDisplayAPI`'s own re-query alone, since a bug or driver quirk in the wrapper's model of "active" could agree with itself while disagreeing with the OS's monitor-enumeration surface that games actually observe |
| `cfgmgr32.dll` P/Invoke (hand-written, ~40 lines) OR `pnputil.exe /disable-device` (built into Windows, no code) | N/A | Fallback mechanism (`CM_Disable_DevNode` equivalent) if topology-path-removal only blanks the monitor instead of removing it from enumeration | Only invoke if the primary approach fails verification. Requires elevation — for the spike, the simplest and lowest-risk path is testing this manually from an elevated PowerShell/cmd window (`pnputil /disable-device "<instance id>"`) rather than writing an elevated-helper-process architecture just for a throwaway prototype. If Phase 4 ends up needing this mechanism in production, *that* is where the helper-process isolation (D-08) gets built. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `WindowsDisplayAPI` managed wrapper | Raw P/Invoke of `QueryDisplayConfig`/`SetDisplayConfig`/`DisplayConfigGetDeviceInfo` | Zero dependencies, full control over flags — but ~300-400 lines of struct marshalling to get right for a tool that gets thrown away after this phase; not worth it unless the wrapper itself is suspected of masking the AMD behavior (see Open Questions) |
| `pnputil /disable-device` (manual, elevated terminal) for the fallback test | Hand-written `CM_Disable_DevNode` P/Invoke in the same console tool | `pnputil` ships with every Windows install (no code, no build step) and is Microsoft's own currently-recommended replacement for the older `devcon.exe` (which Microsoft no longer distributes as a standalone download); writing P/Invoke is only worth it if Phase 4 needs this mechanism baked into the shipped app — for this spike, manual `pnputil` testing answers the go/no-go question faster |
| .NET 10 | An older LTS (.NET 8) | No reason — this is a fresh spike in a fresh repo already committed to .NET 10 project-wide |

**Installation (on the rig PC, in an ordinary — not elevated — terminal):**
```powershell
# 1. Confirm/install the .NET SDK (see "Environment Availability" below for exact steps)
dotnet --list-sdks

# 2. Scaffold the spike project — avoid words like "setup"/"install"/"update" in the name
#    (legacy UAC installer-detection heuristics key off those substrings in unmanifested exe names;
#    .NET apps are asInvoker by default regardless, but avoiding the words removes any doubt)
dotnet new console -n MonitorDetachSpike -f net10.0-windows
cd MonitorDetachSpike

# 3. Add the CCD wrapper
dotnet add package WindowsDisplayAPI --version 1.3.0.13
```

Then add `<UseWindowsForms>true</UseWindowsForms>` to the `.csproj` (see Code Examples) to unlock `Screen.AllScreens` as the independent verification source — this does not turn it into a GUI app; a console app can reference WinForms types purely for their static APIs.

**Version verification:** `WindowsDisplayAPI` 1.3.0.13 confirmed current via direct `nuget.org` page fetch (152.5K total downloads, last published 2020-02-10, zero dependencies, LGPL-3.0, source at `github.com/falahati/WindowsDisplayAPI`, used by two notable open-source projects — Lenovo Legion Toolkit and WinDynamicDesktop). .NET SDK 10.0.302 confirmed via web search cross-referencing `dotnet.microsoft.com/download` and `versionsof.net` (July 14, 2026 release date, current as of this research date).

## Package Legitimacy Audit

> `slopcheck` (installed and run this session, v0.6.1) does **not** support the NuGet ecosystem — its `install` subcommand only recognizes `pypi, npm, crates.io, go, rubygems, maven, packagist`. NuGet packages here were manually vetted against the criteria slopcheck would otherwise apply (age, download count, linked source repo, third-party usage).

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|--------------|-----------|-------------|
| `WindowsDisplayAPI` | NuGet | ~6 years (published 2020-02-10, no ecosystem-churn risk since it wraps a stable Win32 surface) | 152.5K total | github.com/falahati/WindowsDisplayAPI (LGPL-3.0, 122 stars) | N/A — ecosystem unsupported by slopcheck | **Approved (manual verification)** — stable, zero-dependency, used by two independently-notable open-source projects (Lenovo Legion Toolkit, WinDynamicDesktop), already source-verified in project-level STACK.md |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

No other external packages are needed for this phase — audio (`NAudio`/`IPolicyConfig`) and app-control libraries from the project-level stack are explicitly out of scope for Phase 1 per the phase boundary.

## Architecture Patterns

### System Architecture Diagram

```
                    ┌───────────────────────────────────────────┐
                    │        User (on rig PC, VS Code)           │
                    │  builds & runs MonitorDetachSpike.exe       │
                    │  from an ordinary (non-elevated) terminal   │
                    └───────────────────┬─────────────────────────┘
                                        │ dotnet run -- --list
                                        ▼
              ┌─────────────────────────────────────────────────────┐
              │  MonitorDetachSpike (console, asInvoker by default)  │
              │                                                       │
              │  Step 1: PathInfo.GetActivePaths()  ───────────────┐ │
              │           │ (WindowsDisplayAPI → QueryDisplayConfig)│ │
              │           ▼                                         │ │
              │  Print each path's target FriendlyName/DevicePath   │ │
              │  → user identifies the DisplayPort/primary monitor  │ │
              │                                                       │ │
              │  Step 2: dotnet run -- --disable <targetId>          │ │
              │           │                                          │ │
              │           ▼                                          │ │
              │  Build new PathInfo[] excluding target's path         │ │
              │  PathInfo.ApplyPathInfos(newPaths, allowChanges:true)│ │
              │           │ (→ SetDisplayConfig, SDC_APPLY|          │ │
              │           │    SDC_USE_SUPPLIED_DISPLAY_CONFIG|      │ │
              │           │    SDC_ALLOW_CHANGES)                    │ │
              │           ▼                                          │ │
              │  Step 3: VERIFY via independent source:               │ │
              │    - PathInfo.GetActivePaths() (same lib, re-query)  │ │
              │    - Screen.AllScreens.Length (different code path,  │ │
              │      wraps EnumDisplayMonitors)                       │ │
              │           │                                          │ │
              │           ▼                                          │ │
              │  PASS: monitor count dropped in BOTH sources          │ │
              │  FAIL: monitor still present in either source         │ │
              │        → try fallback (see below), report to user    │ │
              └───────────────────────┬───────────────────────────────┘ │
                                      │ FAIL path                        │
                                      ▼                                  │
              ┌─────────────────────────────────────────────────────┐  │
              │  FALLBACK (manual, elevated terminal, separate from  │  │
              │  the main spike process):                            │  │
              │    Get-PnpDevice -Class Monitor   (find instance id) │  │
              │    pnputil /disable-device "<instance id>"           │  │
              │  Re-run Step 3 verification from the non-elevated    │  │
              │  spike tool to confirm effect.                        │  │
              └─────────────────────────────────────────────────────┘  │
                                                                         │
                    OS Display Subsystem (CCD / WDDM / GPU driver) ◄────┘
                    — AMD Radeon driver behavior here is the unknown
                      this entire phase exists to resolve empirically
```

### Recommended Project Structure

For a throwaway spike, do not over-structure it — a single project, single file is correct:

```
MonitorDetachSpike/
├── MonitorDetachSpike.csproj    # net10.0-windows, UseWindowsForms=true, WindowsDisplayAPI ref
├── Program.cs                    # everything: list / disable / restore / verify, argument-switched
└── snapshot.json                 # written by --disable, read by --restore (spike-only, not the
                                   # production SnapshotStore design from ARCHITECTURE.md)
```

### Pattern 1: Topology-path-removal via `ApplyPathInfos`

**What:** Query the currently active paths, remove the one whose target matches the monitor to disable, and re-apply the reduced set.
**When to use:** As the first, non-elevated approach — this is the mechanism the project-level STACK.md already confirmed (via direct source read) is what `WindowsDisplayAPI` does under the hood, and it's the closest public-API equivalent to the Windows Settings "Disconnect this display" action.
**Example:**
```csharp
// Source: WindowsDisplayAPI PathInfo.cs (github.com/falahati/WindowsDisplayAPI),
// method signatures confirmed by direct source read.
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

// Step 1 — enumerate and print for the user to identify the target monitor
PathInfo[] activePaths = PathInfo.GetActivePaths(virtualModeAware: false);
for (int i = 0; i < activePaths.Length; i++)
{
    var path = activePaths[i];
    foreach (var targetInfo in path.TargetsInfo)
    {
        var target = targetInfo.DisplayTarget;
        Console.WriteLine(
            $"[{i}] Target={target.FriendlyName ?? "(unavailable)"} " +
            $"DevicePath={target.DevicePath} " +
            $"IsGDIPrimary={path.IsGDIPrimary} " +
            $"OutputTechnology={targetInfo.OutputTechnology}");
    }
}

// Step 2 — snapshot BEFORE mutating (needed to test restore, and as a safety net)
File.WriteAllText("snapshot.json", JsonSerializer.Serialize(
    activePaths.Select(p => p.ToString()).ToArray())); // human-readable audit trail;
    // the actual restore below re-applies the in-memory `activePaths` array directly —
    // do not round-trip PathInfo through JSON for the real restore call.

// Step 3 — build the reduced topology (exclude the chosen index) and apply
var targetIndex = 2; // chosen by the user after reviewing Step 1's printed list
var reducedPaths = activePaths.Where((_, idx) => idx != targetIndex).ToArray();
PathInfo.ApplyPathInfos(reducedPaths, allowChanges: true, saveToDatabase: false, forceModeEnumeration: false);
// allowChanges:true is required here (not optional) — it lets Windows auto-promote a
// remaining display to primary (position 0,0) if the removed path held that designation,
// which topology-path-removal on the *primary* monitor specifically will trigger.
```

### Pattern 2: Independent cross-check verification (do not trust the same library twice)

**What:** After mutating, verify success through a *second* API surface, not just `WindowsDisplayAPI`'s own re-query.
**When to use:** Always — directly addresses project PITFALLS.md Pitfall 6 ("operations report success but don't actually take effect").
**Example:**
```csharp
// Source: System.Windows.Forms.Screen (BCL, wraps EnumDisplayMonitors internally) —
// requires <UseWindowsForms>true</UseWindowsForms> in the .csproj even for a console app.
using System.Windows.Forms;

int screenCountBefore = Screen.AllScreens.Length; // capture before Step 3 above
// ... perform ApplyPathInfos ...
int screenCountAfter = Screen.AllScreens.Length;

// Also re-query WindowsDisplayAPI itself as a first check, then compare against
// the independent Screen.AllScreens source — require BOTH to agree the monitor is gone:
var stillActive = PathInfo.GetActivePaths()
    .SelectMany(p => p.TargetsInfo)
    .Any(t => t.DisplayTarget.DevicePath == targetDevicePath);

bool verified = !stillActive && screenCountAfter < screenCountBefore;
Console.WriteLine(verified
    ? "PASS: monitor removed from BOTH WindowsDisplayAPI and Screen.AllScreens enumeration."
    : "FAIL: monitor still detected by at least one enumeration source — try fallback.");
```

### Pattern 3: `.csproj` for the spike

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>  <!-- for Screen.AllScreens verification only -->
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- No <ApplicationManifest> element = default asInvoker behavior, per D-08 -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="WindowsDisplayAPI" Version="1.3.0.13" />
  </ItemGroup>
</Project>
```

### Anti-Patterns to Avoid

- **Trusting `ApplyPathInfos`'s lack of an exception as proof of success:** it can silently ignore invalid/contradictory flag combinations (Pitfall 6) — always re-verify via a second source (Pattern 2).
- **Marking the whole app `requireAdministrator` to unblock the `CM_Disable_DevNode`/`pnputil` fallback "just in case":** per D-08 and Pitfall 2, keep the primary tool `asInvoker`; test the fallback manually from a separately-opened elevated terminal instead of baking elevation into this throwaway tool.
- **Naming the spike project/exe with "Setup", "Install", "Update", "Patch":** triggers legacy UAC installer-detection heuristics on some Windows configurations for unmanifested executables; irrelevant for a properly-built .NET app (which is asInvoker by manifest default) but avoid it anyway to remove any variable while debugging.
- **Testing only with the target monitor's windows already moved off it:** per Pitfall 3, at least one round-trip should be tested with an ordinary window (not necessarily maximized/fullscreen — that's Phase 4's concern) still open on the primary monitor at disable-time, so the spike also surfaces whether window relocation happens gracefully or windows go missing — worth noting for Phase 4 even though it's not this phase's pass/fail criterion.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|--------------|-----|
| CCD struct marshalling (`DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`, etc.) | Raw P/Invoke definitions from scratch | `WindowsDisplayAPI`'s `PathInfo`/`PathTargetInfo`/`PathDisplayTarget` classes | Already done correctly, source-verified, zero dependencies, and reusing it means a spike failure is more likely to reflect a genuine driver limitation rather than a marshalling bug in fresh P/Invoke code |
| Monitor enumeration cross-check | A second hand-rolled `EnumDisplayMonitors` P/Invoke wrapper | `System.Windows.Forms.Screen.AllScreens` (BCL) | Already wraps `EnumDisplayMonitors` correctly; zero extra code for an independent verification source |
| Device-node instance-ID lookup for the fallback path | `SetupDiGetClassDevs`/`SetupDiEnumDeviceInfo` P/Invoke just to find the monitor's instance ID | `Get-PnpDevice -Class Monitor` (PowerShell, built into Windows) or Device Manager UI | For a one-time spike test, the built-in PowerShell cmdlet or GUI is far faster than writing SetupAPI P/Invoke to find one instance ID; only worth automating in Phase 4 if the fallback mechanism is actually needed in production |

**Key insight:** Every piece of this spike has an existing, already-verified building block (a maintained NuGet wrapper, a BCL class, or a built-in Windows CLI tool) — the only genuinely unknown, unbuildable-around piece is whether the AMD Radeon driver on this specific rig honors `SetDisplayConfig` topology removal the way NVIDIA-tested references imply it should. No amount of additional tooling substitutes for running it on the real hardware.

## Common Pitfalls

### Pitfall A: No documented AMD-vs-NVIDIA difference exists — silence is not a green light
**What goes wrong:** Assuming that because `WindowsDisplayAPI`/`SetDisplayConfig` "works" (per NirSoft MultiMonitorTool, Monarch, and Microsoft's own docs) it therefore works identically on AMD. Multiple community threads researched during this phase (Microsoft Q&A "Why does EnumDisplayMonitors still return just disconnected monitor" — reported on Intel UHD hardware, not AMD; AMD Adrenalin support docs — describe manual Eyefinity/extend-mode UI configuration, not programmatic detach) confirm the *general* mechanism but never confirm or deny AMD-specific quirks.
**Why it happens:** Reference implementations and forum discussions of this technique skew toward whatever hardware the author happened to have; AMD-specific validation is simply not a well-documented topic online.
**How to avoid:** Treat every finding about "it works" as vendor-unverified until the rig's actual AMD Radeon + DisplayPort combination is tested. Budget explicitly for the possibility that topology-path-removal blanks the monitor (DPMS-style) without removing it from enumeration on this driver — this is exactly the Pitfall 1 failure mode from project-level PITFALLS.md, and AMD is not proven immune to it.
**Warning signs:** Monitor goes black but `Screen.AllScreens.Length`/`PathInfo.GetActivePaths()` count doesn't drop; Windows doesn't reflow the taskbar/desktop icons the way a true disconnect would.

### Pitfall B: Removing the *primary* monitor's path specifically can fail if the remaining path isn't repositioned to (0,0)
**What goes wrong:** Windows requires exactly one active path to be positioned at (0,0) (GDI primary). If the code that builds the reduced path array passes `allowChanges: false`, `SetDisplayConfig` may reject the request outright since no path in the supplied set is at the origin once the (formerly primary) target is removed.
**Why it happens:** `SDC_ALLOW_CHANGES` exists precisely so the OS can auto-adjust remaining paths' positions/primary designation — it is easy to assume "no changes needed, I'm just removing a path" and pass `allowChanges: false` for a "purer" request, which then fails specifically for the primary-monitor-removal case that this project needs.
**How to avoid:** Always pass `allowChanges: true` (the `WindowsDisplayAPI` default) for this operation; do not "clean up" the call by disabling the allow-changes behavior.
**Warning signs:** `ApplyPathInfos` throws or silently no-ops specifically when removing the currently-primary path but works fine when removing a non-primary secondary monitor in earlier tests.

### Pitfall C: Hotplug re-detection may silently re-add the "disconnected" path after a delay
**What goes wrong:** Some driver/monitor combinations re-run hotplug detection periodically or on certain OS events (e.g., waking from a display-power-state change) and can re-enumerate a topology-removed display back into the active set without any explicit re-enable call.
**Why it happens:** DisplayPort hotplug-detect (HPD) signaling continues at the hardware level even after Windows stops actively driving the output in software; some drivers re-poll HPD and "helpfully" restore a display Windows previously dropped.
**How to avoid:** Don't just verify immediately after the `ApplyPathInfos` call — re-verify again after a 10-30 second wait (and ideally after triggering a benign display-related event, e.g., locking/unlocking the session) to confirm the removal is stable, not momentary. This is directly relevant to the "iterate as many round-trips as needed" instruction in D-03 — a single instantaneous pass/fail check may give a false PASS.
**Warning signs:** Verification passes immediately after disable but the monitor reappears in Windows Display Settings a short time later without any explicit re-enable action.

### Pitfall D: UAC/elevation contamination even in a throwaway spike
**What goes wrong:** If the fallback `pnputil`/`CM_Disable_DevNode` path is tested by launching the *whole spike tool* elevated (e.g., right-click "Run as administrator" on the .exe) rather than only running the fallback command in a separate elevated terminal, any conclusions drawn about the primary (`ApplyPathInfos`) path's elevation requirements become invalid — it may appear to "need" admin when it doesn't, or hide a UIPI-style problem that would actually affect the real production app (per D-08's concern about the Moza Companion window-focus feature in Phase 3).
**How to avoid:** Always run the main spike tool from an ordinary terminal for the primary approach. Only if/when testing the fallback, open a *separate* elevated PowerShell/cmd window and run `pnputil`/`Get-PnpDevice` there — never elevate the spike .exe itself.
**Warning signs:** Confusion in later phases about "does monitor disable need admin?" traceable to an initial test having been run elevated for convenience.

## Code Examples

### Verified patterns from official/library sources

**Enumerate active paths and identify targets (WindowsDisplayAPI, source-verified signatures):**
```csharp
// Source: github.com/falahati/WindowsDisplayAPI/blob/master/WindowsDisplayAPI/DisplayConfig/PathInfo.cs
// and PathTargetInfo.cs / PathDisplayTarget.cs (confirmed via direct source read this session)
PathInfo[] paths = PathInfo.GetActivePaths(virtualModeAware: false);
// PathInfo.GetAllPaths(virtualModeAware: false) also exists — includes inactive paths;
// use GetActivePaths for the "what's on screen right now" snapshot this phase needs.
```

**Apply a reduced topology (the actual detach call):**
```csharp
// Signature confirmed via direct source read:
// public static void ApplyPathInfos(IEnumerable<PathInfo> pathInfos,
//     bool allowChanges = true, bool saveToDatabase = false, bool forceModeEnumeration = false)
PathInfo.ApplyPathInfos(reducedPaths, allowChanges: true);
```

**Raw Win32 fallback signatures, if `WindowsDisplayAPI` needs to be bypassed for lower-level control (only if the wrapper itself is suspected of masking real behavior — see Open Questions):**
```csharp
// Source: Microsoft Learn / sdk-api (learn.microsoft.com/windows/win32/api/winuser/nf-winuser-querydisplayconfig,
// nf-winuser-setdisplayconfig) — HIGH confidence, official reference.
[DllImport("user32.dll")]
static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

[DllImport("user32.dll")]
static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements,
    [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements,
    [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

[DllImport("user32.dll")]
static extern int SetDisplayConfig(uint numPathArrayElements, DISPLAYCONFIG_PATH_INFO[] pathArray,
    uint numModeInfoArrayElements, DISPLAYCONFIG_MODE_INFO[] modeInfoArray, uint flags);

// flags for QueryDisplayConfig: QDC_ONLY_ACTIVE_PATHS = 0x00000002
// flags for SetDisplayConfig:  SDC_APPLY = 0x00000080, SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020,
//                               SDC_ALLOW_CHANGES = 0x00000400, SDC_TOPOLOGY_SUPPLIED = 0x00000001
```

**Fallback: `pnputil` device-node disable (built into Windows, no code, run from an elevated terminal):**
```powershell
# Source: learn.microsoft.com/windows-hardware/drivers/devtest/pnputil-command-syntax (HIGH confidence, official)
# 1. Find the monitor's device instance ID (ordinary terminal is fine for this read-only step):
Get-PnpDevice -Class Monitor | Format-Table FriendlyName, InstanceId, Status

# 2. Disable it (REQUIRES an elevated/admin PowerShell or cmd window):
pnputil /disable-device "<InstanceId from step 1>"

# 3. Re-enable (also requires elevation):
pnputil /enable-device "<InstanceId from step 1>"
```
Note: Microsoft's own guidance states `devcon.exe` (the historically-cited P/Invoke-adjacent tool for this) is "no longer available as a separate download" and that `pnputil` is the currently-recommended replacement — do not have the user hunt down a `devcon.exe` binary from a third-party mirror.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|-------------------|----------------|--------|
| `devcon.exe` for device-node enable/disable | `pnputil.exe /enable-device` / `/disable-device` | Microsoft has stated `devcon` is "originally... a code sample... not a tool to be relied upon" and no longer ships it as a standalone download; `pnputil` (built into every Windows release) is the recommended replacement | Simplifies the fallback path for this phase — no third-party binary download/trust decision needed for the user |
| `ChangeDisplaySettingsEx` with zeroed `DEVMODE` for "detaching" a display | CCD topology-path-removal via `SetDisplayConfig` | CCD API introduced with WDDM/Windows 7+, is the modern mechanism; the legacy approach is inconsistent on modern WDDM drivers (confirmed in project-level STACK.md's "What NOT to Use") | Already reflected in the chosen approach — no action needed, just confirming the recommendation still holds |

**Deprecated/outdated:**
- `devcon.exe`: superseded by `pnputil`; do not direct the user to download it.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|----------------|
| A1 | `WindowsDisplayAPI`'s topology-path-removal behaves the same on AMD Radeon/DisplayPort as on the vendor-agnostic/NVIDIA-flavored references found during this research | Summary, Pitfall A | If wrong, the primary mechanism fails on the actual rig and the fallback (`CM_Disable_DevNode`/`pnputil`, which requires elevation) becomes the production mechanism instead — this changes Phase 4's elevation-isolation design from "probably unnecessary" to "definitely needed," a real scope change |
| A2 | `SDC_ALLOW_CHANGES` is sufficient for Windows to auto-reassign primary-monitor designation when the primary path is removed, without the app needing to explicitly reposition the remaining path to (0,0) | Pattern 1, Pitfall B | If wrong, the spike's first `ApplyPathInfos` call could fail or produce an invalid/no-primary topology; the code would need to explicitly read the remaining path's position and rewrite it to (0,0) before applying |
| A3 | Hotplug re-detection (Pitfall C) is a real enough risk on this hardware to warrant a delayed re-verification step, not just an immediate one | Pitfall C | If this doesn't apply to the rig's specific monitor/driver combo, the extra wait is just harmless overhead; if it does apply and is skipped, a false PASS could be reported to the planner for Phase 4 |
| A4 | .NET apps produced via `dotnet new console` (no explicit `<ApplicationManifest>`) run `asInvoker` by default with no UAC prompt, satisfying D-08's non-elevation requirement for the primary approach | Standard Stack, Anti-Patterns | Low risk — this is well-documented default .NET/Windows behavior; if somehow wrong, the user would immediately see a UAC prompt on `dotnet run`, which is self-evidently observable and easy to correct |

**All A1-A3 above require the user's empirical test results to resolve — they cannot be resolved by further research and are the explicit reason D-03 allows unlimited build→run→report iterations.**

## Open Questions (DEFERRED TO EMPIRICAL TEST)

> These are NOT further researchable from the Linux planning sandbox — each is resolved (or not) only by the USER's empirical test on the rig PC. Treat them as risks/diagnostics to watch during that test, not as open research tasks.

1. **Does `WindowsDisplayAPI`'s managed layer add any behavior (validation, exception-swallowing, flag defaults) that could mask a driver-level partial failure differently than a raw P/Invoke call would?**
   - What we know: the library's `ApplyPathInfos` is a thin wrapper that constructs flags and calls `SetDisplayConfig` directly (confirmed via source read) — no evidence of extra validation logic that would hide failures.
   - What's unclear: whether its exception types map cleanly to all possible `SetDisplayConfig` error codes, or whether some driver-rejection cases surface as a return value the wrapper doesn't surface as an exception.
   - Recommendation: if the primary approach reports PASS via `WindowsDisplayAPI`'s own state but Screen.AllScreens/visual inspection disagrees, drop to the raw P/Invoke signatures provided above to rule out a wrapper-layer masking issue before concluding the driver itself is at fault.

2. **Does AMD's Adrenalin software (Radeon driver control panel) have its own display-topology state that could conflict with or override `SetDisplayConfig` changes made outside it?**
   - What we know: AMD's own support documentation only describes manual UI-driven Eyefinity/extend-display configuration; nothing in AMD's public docs addresses programmatic CCD API interaction.
   - What's unclear: whether Adrenalin's background service re-asserts a topology it "remembers" independent of what `SetDisplayConfig` callers request.
   - Recommendation: if Pitfall C (hotplug re-detection) manifests, check whether disabling/closing the AMD Software background service changes the result — this would indicate an AMD-software-specific interference layer distinct from the driver itself.

3. **What exact Windows build/version is the rig PC running, and does that matter?**
   - What we know: CCD/`SetDisplayConfig` behavior has been stable since Windows 7/WDDM 1.1; no version-specific AMD regression was found in this research.
   - What's unclear: whether the user's specific Windows 10 vs 11 build interacts with the AMD driver differently.
   - Recommendation: have the user report `winver` output alongside their test results — cheap to capture, useful if a discrepancy needs later debugging.

## Environment Availability

> This research session runs on Linux and cannot probe the actual rig PC directly (D-01) — the commands below are for the **user** to run themselves on the rig PC as the first step of executing this phase's plan, not something this research verified empirically.

| Dependency | Required By | Available | Version | Fallback |
|------------|--------------|-----------|---------|----------|
| .NET SDK 10.0.x | Building/running the spike (`dotnet new`, `dotnet run`) | Unknown — user confirmed only VS Code is installed, no confirmed SDK | — | Install via `winget install --id Microsoft.DotNet.SDK.10 -e` or manual download from `dotnet.microsoft.com/download/dotnet/10.0`; verify after install with `dotnet --list-sdks` |
| Internet access on rig PC (for NuGet restore) | `dotnet add package WindowsDisplayAPI` | Unknown | — | If unavailable, the raw P/Invoke fallback signatures in Code Examples remove the NuGet dependency entirely (more code, zero network requirement) |
| Windows 10/11 with AMD Radeon driver installed | The entire spike — this is the exact hardware/driver combination under test | Assumed present (per CONTEXT.md D-04/D-05) | Unknown exact Windows build (see Open Question 3) | None — this is the environment being validated, not a dependency to substitute |
| Elevated (admin) terminal access | Fallback `pnputil`/`CM_Disable_DevNode` test only | Assumed available (user's own PC) | — | None needed unless primary approach fails verification |

**Missing dependencies with no fallback:**
- None — every dependency either has a documented install path or an already-planned code-level fallback.

**Missing dependencies with fallback:**
- .NET SDK: install via winget or manual installer (see above) — this is expected, not a blocker, and D-02 already anticipated it.
- NuGet/internet access: raw P/Invoke fallback removes this dependency if the rig PC has no internet access at build time.

## Security Domain

> `security_enforcement` is absent from `.planning/config.json`'s `features` block — treated as enabled per policy, though this phase is a single-user, no-network, no-persistent-credential throwaway console tool, so most ASVS categories are not applicable.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|----------------|---------|-------------------|
| V2 Authentication | No | Single local interactive user, no auth surface |
| V3 Session Management | No | No sessions — single-run console process |
| V4 Access Control | No | No multi-user/role concept |
| V5 Input Validation | Yes (minimal) | Validate the user-supplied target-index/target-ID argument is within the enumerated range before indexing into the path array — an out-of-range index should print an error, not throw an unhandled exception or silently act on the wrong path |
| V6 Cryptography | No | No secrets, no persisted credentials — the `snapshot.json` this spike writes is a plaintext audit trail of display topology, not sensitive data |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|------------------------|
| Elevation of privilege via reflexive `requireAdministrator` manifest | Elevation of Privilege | Default `asInvoker` (D-08); isolate any genuinely-required elevated operation (the `pnputil`/`CM_Disable_DevNode` fallback) to a manually-invoked separate elevated terminal for this spike, or a dedicated helper process in the eventual Phase 4 production build |
| Argument-driven out-of-bounds array access (user types an invalid `--disable <index>`) | Tampering / Denial of Service (crash) | Bounds-check the index against the enumerated path array length before use; print a clear error and exit non-zero rather than throwing |

## Sources

### Primary (HIGH confidence)
- Project-level `.planning/research/STACK.md`, `ARCHITECTURE.md`, `PITFALLS.md` — already-verified direct source reads of `WindowsDisplayAPI`, official Microsoft CCD docs, LGPL license text
- https://raw.githubusercontent.com/falahati/WindowsDisplayAPI/master/WindowsDisplayAPI/DisplayConfig/PathInfo.cs — direct source read this session, confirmed `GetActivePaths`/`GetAllPaths`/`ApplyPathInfos` signatures and internal `SetDisplayConfigFlags` usage
- https://raw.githubusercontent.com/falahati/WindowsDisplayAPI/master/WindowsDisplayAPI/DisplayConfig/PathTargetInfo.cs and PathDisplayTarget.cs — direct source read this session, confirmed `FriendlyName`/`DevicePath`/`EDIDManufactureId` properties
- https://raw.githubusercontent.com/falahati/WindowsDisplayAPI/master/WindowsDisplayAPI/Display.cs — direct source read this session, confirmed `Display.GetDisplays()`, `DisplayName`, `IsAvailable`
- https://www.nuget.org/packages/WindowsDisplayAPI — fetched directly, confirmed version 1.3.0.13, 152.5K downloads, 2020-02-10 publish date, dependency-free, usage by Lenovo Legion Toolkit and WinDynamicDesktop
- https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax — official, confirmed `/disable-device`/`/enable-device` syntax and elevation requirement
- https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/devcon-migration — official, confirmed `devcon` deprecated in favor of `pnputil`
- https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_disable_devnode — official reference for the raw fallback API, confirmed elevation requirement and reboot-persistence behavior

### Secondary (MEDIUM confidence)
- WebSearch: ".NET SDK current version 2026" cross-referencing dotnet.microsoft.com and versionsof.net — confirmed .NET SDK 10.0.302, released 2026-07-14
- WebSearch/WebFetch: github.com/Nuzair46/Monarch (community per-monitor detach tool) — confirmed the same `SetDisplayConfig` topology mechanism is used cross-vendor by a modern (2026-era) community tool, but explicitly did not find AMD-specific implementation notes in its README
- WebFetch: learn.microsoft.com/answers/questions/2156326 (Microsoft Q&A on `EnumDisplayMonitors` returning a disconnected monitor) — confirms this class of enumeration-lag/virtual-display issue is real and hardware/driver-dependent, but the reported case was Intel UHD Graphics, not AMD — used as evidence for Pitfall A/C, not as an AMD-specific confirmation
- WebSearch: .NET default manifest / `asInvoker` behavior (jonathancrozier.com, dotnet/runtime GitHub issues) — corroborated across multiple independent sources that .NET apps default to `asInvoker` without an explicit manifest

### Tertiary (LOW confidence)
- General WebSearch results on "AMD Radeon driver SetDisplayConfig" and "AMD Adrenalin programmatic display detach" — did not surface any AMD-specific technical documentation or bug reports; this absence is itself the key finding (see Pitfall A) and should not be read as "AMD is fine," only as "no public evidence exists either way"

## Metadata

**Confidence breakdown:**
- Standard stack (WindowsDisplayAPI, .NET SDK version, pnputil fallback): HIGH — all directly source-verified or official-docs-verified this session
- Architecture (topology-removal pattern, verification cross-check pattern): HIGH — mechanism confirmed via source read; MEDIUM for whether it will actually succeed on this hardware
- Pitfalls: MEDIUM-HIGH for the general Windows/CCD pitfalls (inherited from project-level PITFALLS.md, itself well-sourced); LOW-and-explicitly-flagged for anything AMD-specific, since no AMD-specific public source exists

**Research date:** 2026-07-24
**Valid until:** ~30 days (stable Win32/CCD API surface; the only fast-moving element — the .NET SDK patch version — doesn't affect this phase's conclusions)
