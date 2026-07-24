# Stack Research

**Domain:** Windows-only desktop GUI utility (display/audio/process control automation)
**Researched:** 2026-07-24
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET | 10.0 (LTS, released 2025-11-11, supported to 2028-11-14) | Runtime/BCL | Current LTS. Self-contained publish is a first-class, well-documented deployment mode (unchanged mechanism since .NET 6), and .NET has the richest Win32 interop story (P/Invoke, COM interop) of any managed stack — required here since all three core features (CCD display API, audio COM interface, Win32 window control) are native Windows APIs with no cross-platform equivalent. Picking .NET also gives you 3 years of security patching without a rewrite. |
| C# | 13 (ships with .NET 10 SDK) | Language | Default with .NET 10; no reason to use F# or VB for this — P/Invoke and COM interop snippets found in the wild (NirSoft/EarTrumpet/AudioSwitcher source) are all C#, so following that reduces translation effort. |
| Windows Forms (WinForms) | .NET 10 (`Microsoft.NET.Sdk` with `UseWindowsForms=true`) | GUI framework | For a 2-screen personal utility (toggle view + settings view), WinForms is the lowest-friction choice: smallest self-contained publish footprint of the three Windows GUI stacks, no XAML compiler step, trivial to bind `ComboBox`/`ListBox` to enumerated monitors/audio devices/processes. WPF is a reasonable alternative (see Alternatives) if you want nicer visuals, but it adds no capability you need here. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `WindowsDisplayAPI` (falahati) | 1.3.0.13 (NuGet) | Managed wrapper around the Windows CCD (Connecting and Configuring Displays) API — `QueryDisplayConfig`/`SetDisplayConfig` | Use for both snapshotting the current display topology (`PathInfo.GetActivePaths()` / `GetAllPaths()`) and applying a new one (`PathInfo.ApplyPathInfos(...)`) that omits the target monitor's path — this is the actual mechanism that performs a true CCD-level "detach", not a power-off. Verified by reading the library source directly (see Sources): `ApplyPathInfos` calls `SetDisplayConfig` with `SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES` (or `SDC_TOPOLOGY_SUPPLIED` when no mode info is given), which is exactly the documented pattern for detaching a target from a source path. |
| Custom `IPolicyConfig` COM interop (hand-written, ~100 lines) | N/A — embed directly in project, not a NuGet dependency | Set the Windows default audio **playback** (render) device | This undocumented COM interface (`IPolicyConfig` / CLSID `{870af99c-171d-4f9e-af0d-e63df40c2bc9}`) is the *only* way to programmatically change the system default audio endpoint — there is no public WASAPI/Core Audio API for it. It is what every default-audio-switcher tool uses under the hood (NirSoft SoundVolumeView — actively updated, v2.53 as of July 2026, explicitly supports Windows 11 — and EarTrumpet, whose `IPolicyConfig.cs` interop file is public on GitHub). The interface GUID has been stable since Windows 8/8.1 (no separate "Vista" fallback needed since you only target Windows 10/11). Embed the interop code directly (copy the ~100-line file, e.g. from `File-New-Project/EarTrumpet` or `tartakynov/audioswitch`, both permissively usable as interop shims) rather than taking a package dependency — the wrapper is tiny and stable, and this avoids depending on a stale package (see AudioSwitcher.AudioApi below). |
| NAudio | 2.3.0 | Enumerate audio render endpoints (friendly names, device IDs, default device query) via `MMDeviceEnumerator` | Use for everything *except* the actual "set default" call: listing playback devices for the settings dropdown, resolving a saved device ID back to a live device at toggle-time, and reading the *current* default device before switching (for state restore). Actively maintained (v2.3.0, updated March 2026), MIT licensed, the de facto standard .NET audio library. Its `MMDeviceEnumerator`/`MMDevice` wrap the same public `IMMDeviceEnumerator` Core Audio API that Windows itself uses for enumeration — fully documented and stable, unlike the "set default" call. |
| `System.Text.Json` | Included in .NET 10 BCL | Persist user settings (selected monitor, audio device pair, app path) to a JSON file | No extra package needed. Serialize a small settings POCO to `%APPDATA%\RigToggle\settings.json`. Simpler and more transparent/debuggable than the Windows Registry or `app.config`/`user.config` (which are awkward for values the user should be able to hand-edit or that get regenerated per-publish). |
| Win32 P/Invoke (hand-written `DllImport`s) | N/A | Focus/minimize the Moza Companion window; detect if it's already running | You only need a handful of calls: `Process.GetProcessesByName` (BCL, no P/Invoke) to detect if it's running, `Process.MainWindowHandle` to get its window handle, then `user32.dll` `ShowWindow` (`SW_MINIMIZE` / `SW_RESTORE`), `IsIconic`, and `SetForegroundWindow` to focus it. This is ~15 lines of `DllImport` — not worth taking a dependency like `PInvoke.User32` (a NuGet package that wraps the same signatures) unless you want IntelliSense/typed constants; either is fine, but hand-rolling avoids a dependency for something this small. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| Visual Studio 2022 (17.14+) or `dotnet` CLI + VS Code | Build/publish | Either works; `dotnet publish` is what actually produces the standalone .exe (see Installation below) and is scriptable, so prefer driving the final build via CLI even if you develop in VS Code. |
| `dotnet publish -r win-x64 --self-contained true -p:PublishSingleFile=true` | Standalone .exe packaging | This is the mechanism that satisfies the "no separate runtime install" constraint — see dedicated section below. |

## Standalone .exe Packaging (Self-Contained Deployment)

This directly addresses the "no separate runtime install required" constraint.

**How it works:** .NET's self-contained single-file publish bundles the CLR, the BCL, and your app into one `.exe`. The target machine needs nothing pre-installed — no .NET runtime, no VC++ redistributable beyond what's already on any Windows 10/11 box. This has been a stable, documented feature since .NET 6 and is unchanged in mechanism for .NET 10.

**Recommended `.csproj` publish settings:**

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <PublishTrimmed>false</PublishTrimmed>
</PropertyGroup>
```

**Publish command:**

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Note on `PublishTrimmed`:** leave it `false`. Trimming reduces the ~150 MB self-contained payload down significantly, but it works by static analysis of what code paths are reachable, and it is well known to be unreliable with heavy reflection/COM-interop-marshalling and WinForms designer-generated code — exactly what this app uses (COM interop for audio, P/Invoke marshalling for CCD paths). Trimming failures here would surface as runtime `MissingMethodException`s that are painful to debug for a personal utility. Ship the untrimmed ~150 MB self-contained exe; disk space is not a constraint for this project, reliability is.

**Confidence:** HIGH — this is documented, current Microsoft guidance (Microsoft Learn: "Create a single file for application deployment"), unchanged in principle since .NET 6, and confirmed compatible with .NET 10 in Microsoft's own release notes.

## Installation

```bash
# Core project setup (no NuGet install needed for WinForms/System.Text.Json/P-Invoke — all in SDK/BCL)
dotnet new winforms -n RigToggle -f net10.0

# Supporting libraries
dotnet add package WindowsDisplayAPI --version 1.3.0.13
dotnet add package NAudio --version 2.3.0

# No package needed for IPolicyConfig — copy the interop source file directly into the project
# (e.g. adapt from https://github.com/File-New-Project/EarTrumpet/blob/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs)
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| WinForms | WPF (`UseWPF=true`) | If you want more polished visuals (better data binding, styling, animations) for the settings screen and don't mind XAML — WPF is equally capable of self-contained single-file publish and equally good at hosting Win32/COM interop. Functionally interchangeable for this project; choose based on taste, not capability. |
| WinForms | Avalonia UI (cross-platform XAML) | Only if there's a chance this becomes cross-platform later — it isn't (Windows-only utility per constraints), so the extra abstraction buys nothing and adds a third-party UI framework dependency. Do not use. |
| Hand-written `IPolicyConfig` interop | `AudioSwitcher.AudioApi` (NuGet, MIT) | If you'd rather take a ready-made dependency than embed ~100 lines yourself. Its stable release (3.0.3) was last published May 2023 and hasn't been updated since (a 4.0.0-alpha line exists but is pre-release); the underlying COM interface it wraps hasn't changed, so it likely still works, but you'd be depending on an unmaintained package for your single most fragile API call. Embedding the interop directly gives you the same reliability with zero dependency risk. |
| `WindowsDisplayAPI` managed wrapper | Raw P/Invoke of `user32.dll` `QueryDisplayConfig`/`SetDisplayConfig`/`DisplayConfigGetDeviceInfo` | If you want zero third-party dependencies and are comfortable writing ~300-400 lines of struct marshalling yourself (the CCD structs are numerous: `DISPLAYCONFIG_PATH_INFO`, `DISPLAYCONFIG_MODE_INFO`, etc.). `WindowsDisplayAPI` already did this correctly and is LGPLv3 (dynamic-link/DLL-reference use is fine in a closed-source app; you just need to include a notice/link back per its README) — using it saves significant P/Invoke debugging time for a personal-project timeline. |
| Both `WindowsDisplayAPI` (display) + custom `IPolicyConfig` (audio) as in-process API calls | NirSoft `MultiMonitorTool.exe` /disable + `SoundVolumeView.exe` /SetDefault, invoked as external processes | Only if you want to prototype fast without writing any interop code at all. Not recommended for the shipped product — see "What NOT to Use" below for why. |
| `Process.MainWindowHandle` + `ShowWindow`/`SetForegroundWindow` | `PInvoke.User32` NuGet package | If you want typed wrappers/constants instead of hand-rolled `DllImport` signatures. Functionally identical; only worth it if you're also doing other Win32 interop elsewhere in the app that would benefit from a fuller wrapper library. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `ChangeDisplaySettingsEx` with `CDS_UPDATEREGISTRY`/detach flags as your *only* display API | This older (pre-CCD, Windows XP-era) display API path is what people reach for first, but on many modern driver/GPU combinations it does not reliably remove a display from Windows' active display list the way the newer CCD (`SetDisplayConfig`) API does — it's more suited to resolution changes than topology (attach/detach) changes. Since the project's core value is a display that is *genuinely absent* from Windows (so BeamNG.drive stops misbehaving), this is the wrong tool for that specific job. | CCD API via `WindowsDisplayAPI`'s `PathInfo.ApplyPathInfos()` (confirmed in source to call `SetDisplayConfig` with topology-changing flags). |
| Bundling NirSoft `MultiMonitorTool.exe` / `SoundVolumeView.exe` and shelling out to them | (1) NirSoft's freeware license explicitly states you "must include all files in the distribution package, without modification" and forbids distributing it "as a part of commercial product" — messy to reason about even for a personal non-commercial tool, and directly conflicts with "one self-contained custom .exe." (2) NirSoft tools are frequently flagged by antivirus/SmartScreen as PUP/riskware because they're widely abused in malware droppers (they're legitimate but "look like" credential/config-stealing tools to heuristic scanners) — bundling one inside your own exe risks your own exe getting flagged too. (3) Shelling out to an external process is strictly less reliable than an in-process API call: it depends on locating/bundling the correct architecture binary, parsing text output, handling the child process's own console window, and has higher latency. | In-process calls via `WindowsDisplayAPI` (display) and a custom `IPolicyConfig` COM interop (audio) — no external processes, no bundled third-party binaries, no separate license to comply with beyond LGPL attribution for one library. |
| `AudioDeviceCmdlets` (PowerShell module) invoked via `Start-Process powershell` | Requires either PowerShell to be present with the module pre-installed (not guaranteed, and would violate the "standalone .exe, no separate runtime" spirit even though PowerShell itself ships with Windows) or bundling/installing the module at runtime — adds a moving part (process spawn, PowerShell startup latency ~200-500ms, output parsing) for something a 100-line in-process COM call does instantly. | Custom `IPolicyConfig` interop, in-process. |
| `FindWindow`/`FindWindowEx` by window title/class as the *primary* way to detect if Moza Companion is running | Window titles change (localization, "Moza Companion - v2.1.3" style version strings, unsaved-changes asterisks) and are fragile to match reliably; also only finds a window if one currently exists (misses "running but window destroyed/tray-only" states). | `Process.GetProcessesByName("MozaCompanion")` (match on the process's executable name, which you already have stored from Settings since the user picks the app path) to detect running state; use `Process.MainWindowHandle` only to get the handle for focus/minimize once you already know the process exists. Note: `MainWindowHandle` returns `IntPtr.Zero` if the process has no visible top-level window at that moment (e.g., minimized to tray with window destroyed rather than hidden) — treat that as "running but no window to manipulate" rather than "not running," and don't fail the whole toggle over it. |
| `PublishTrimmed=true` for this project | IL trimming's static analysis frequently misidentifies COM-interop and P/Invoke marshalling code paths (used heavily here) as unreachable and strips them, causing runtime failures that only show up when you hit the audio-switch or display-toggle code path — exactly the code you can least afford to have silently break. | Publish self-contained, untrimmed. Accept the larger (~150 MB) exe. |
| Framework-dependent deployment (`--self-contained false`) | Requires the target machine to already have the matching .NET runtime installed — this is precisely what the "no separate runtime install" constraint rules out. | `--self-contained true` with `PublishSingleFile=true` (see Packaging section). |

## Stack Patterns by Variant

**If you later add global hotkey support (explicitly out of scope for v1 per PROJECT.md, but worth flagging for the roadmap):**
- Use `RegisterHotKey`/`UnregisterHotKey` (user32.dll P/Invoke) rather than a hooking library — it's 2 P/Invoke calls and needs a message-pump window (WinForms already gives you one), no extra dependency needed.

**If you later add system tray residency (also out of scope for v1):**
- WinForms' built-in `NotifyIcon` component covers this natively — no extra library needed either.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| `WindowsDisplayAPI` 1.3.0.13 | .NET 10 (net10.0-windows) via .NET Standard/Framework target | The package predates .NET's newer TFMs but is plain P/Invoke-based C# with no framework-specific API surface, so it resolves and runs fine under net10.0-windows self-contained publish — this is the standard situation for small, stable Win32-wrapper NuGet packages (no runtime dependency conflicts to worry about). |
| NAudio 2.3.0 | .NET 10 | Actively maintained against current .NET; no compatibility concerns. |
| Custom `IPolicyConfig` interop | Windows 10 / Windows 11 (all builds through 24H2/25H2 per current SoundVolumeView v2.53, July 2026, and EarTrumpet's continued reliance on the same interface) | Only the modern (Windows 8+) `IPolicyConfig` GUID is needed; skip the legacy `IPolicyConfigVista` fallback entirely since Windows 7/Vista support is irrelevant here. |

## Sources

- `/falahati/windowsdisplayapi` (Context7) — confirmed CCD API wrapper existence, LGPLv3 license, general shape of the library
- https://github.com/falahati/WindowsDisplayAPI (source read directly, `PathInfo.cs`) — confirmed exact `SetDisplayConfig` flag usage (`SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES` / `SDC_TOPOLOGY_SUPPLIED`) and the `GetActivePaths()`/`ApplyPathInfos()` snapshot-and-restore pattern — HIGH confidence, verified against actual code, not just docs
- https://learn.microsoft.com/en-us/windows-hardware/drivers/display/setdisplayconfig-summary-and-scenarios — official CCD API background — HIGH confidence
- https://www.nirsoft.net/utils/multi_monitor_tool.html — confirmed `/disable`/`/enable` CLI switches and, critically, the exact freeware redistribution license text — HIGH confidence (fetched directly)
- https://www.nirsoft.net/utils/sound_volume_view.html — confirmed v2.53, July 2026, explicit Windows 11 support claim — HIGH confidence (fetched directly), used as evidence the underlying `IPolicyConfig` technique still functions on current Windows
- https://github.com/File-New-Project/EarTrumpet/blob/dev/EarTrumpet/Interop/MMDeviceAPI/IPolicyConfig.cs — confirms the modern `IPolicyConfig`/`IPolicyConfigVista` GUIDs and that an actively-developed, widely-used open-source project still relies on this exact undocumented interface — MEDIUM-HIGH confidence (GitHub source, not official docs, but corroborated by multiple independent projects using the identical interface)
- https://github.com/xenolightning/AudioSwitcher and https://libraries.io/nuget/AudioSwitcher.AudioApi — confirmed last stable release 3.0.3 (May 2023), alpha 4.0.0 line inactive — MEDIUM confidence, used to justify recommending embedded interop over this package
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview and https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained — official self-contained/single-file/trimming guidance — HIGH confidence
- https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md and https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/ — confirmed .NET 10 GA 2025-11-11, LTS to 2028-11-14 — HIGH confidence
- https://www.nuget.org/packages/NAudio/ and https://github.com/naudio/NAudio — confirmed v2.3.0, MIT license, actively maintained (updated March 2026) — HIGH confidence

---
*Stack research for: Windows desktop GUI utility (display/audio/process automation)*
*Researched: 2026-07-24*
