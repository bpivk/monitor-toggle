# Architecture Research

**Domain:** Windows WinForms desktop utility — v2.2 Auto-Update, Single-Instance Guard & Smaller Footprint
**Researched:** 2026-08-18
**Confidence:** HIGH for integration points and component placement (based on direct reading of the current `src/RigToggle.App/Program.cs`, `MainForm.cs`, `ToggleOrchestrator.cs`, `StartupArgs.cs`, `WindowsAppController.cs`, `RigToggle.App.csproj`, `win-x64.pubxml`, `.github/workflows/{build,release}.yml`) · MEDIUM-HIGH for the self-update mechanism and exe-size feature switches (WebSearch-verified against Microsoft docs and multiple independent implementations, not yet rig-tested — flagged explicitly below) · LOW-MEDIUM for anything claiming "the accent-color source is officially documented" style certainty about SmartScreen/AV heuristic behavior, which is inherently non-deterministic and vendor-specific.

> **Supersedes** the previous (2026-08-09, v2.0-scoped) version of this file — that content described the now-shipped configurable-monitors/optional-targets/manual-panel milestone and is no longer current. This file is scoped entirely to v2.2 (auto-update, single-instance guard, further exe-size reduction). v2.1's UI architecture (MainForm tile dashboard, `ToggleSwitch`, `OverridableThemeProvider`) is unchanged by this milestone and is not re-described here — see git history for that content if needed.

## Standard Architecture

### System Overview

This remains the same 4-project .NET 10 solution (`RigToggle.Core` / `RigToggle.Windows` / `RigToggle.App` / `RigToggle.Tests` + `RigToggle.Windows.Tests`, plus dev-time `RigToggle.IconGen`) with the same layering discipline: **Core** = pure/testable orchestration + abstractions + `System.Text.Json` persistence; **Windows** = concrete adapters that touch real OS state (Win32 P/Invoke, COM, `Process`, file/registry I/O); **App** = WinForms UI + the `Program.cs` composition root. All three v2.2 features are additive along this same seam — none of them require a new project or a change to the existing Core/Windows/App split.

```
┌────────────────────────────────────────────────────────────────────────────┐
│                        RigToggle.App (composition root)                     │
├────────────────────────────────────────────────────────────────────────────┤
│  Program.cs — NEW startup gate ordering (see Recommended Build Order /      │
│  Data Flow below):                                                          │
│    1. SetColorMode / ApplicationConfiguration.Initialize()  (unchanged)     │
│    2. StartupArgs.TryGetApplyUpdateArgs(args) → UpdateApplier.Run(...)      │
│       and RETURN — bypasses everything below, including the guard (NEW)     │
│    3. SingleInstanceGuard.TryAcquire() → if false, signal existing          │
│       instance via named pipe, then RETURN (NEW)                            │
│    4. settingsStore / modeStore / markerStore bootstrap        (unchanged)  │
│    5. StartupRecoveryChecker.Run(...)                          (unchanged)  │
│    6. controllers + themeProvider + toggleService/orchestrator (unchanged)  │
│    7. mainForm construction, InitializeTrayState(),                        │
│       RegisterHotkeyAtStartup()                                 (unchanged) │
│    8. SingleInstanceGuard.StartListening(mainForm.RestoreAndFocus) (NEW)    │
│    9. mainForm.BeginInvoke(UpdateOrchestrator.CheckOnLaunchAsync) (NEW)     │
│   10. Application.Run(...)                                      (unchanged) │
│                                                                              │
│  UpdatePromptDialog (NEW, Form) — theme-aware confirm dialog, built like    │
│  MonitorConfirmDialog (constructed with themeProvider, no MessageBox)       │
└──────────────┬───────────────────────────────────┬─────────────────────────┘
               │                                    │
┌──────────────▼───────────────────┐  ┌─────────────▼────────────────────────┐
│  RigToggle.Core (pure, testable)  │  │  RigToggle.Windows (OS adapters)      │
├────────────────────────────────────┤  ├────────────────────────────────────────┤
│ Abstractions/IReleaseFeed.cs (NEW) │  │ WindowsUpdateApplier.cs (NEW)          │
│ Abstractions/IUpdateApplier.cs(NEW)│  │  implements IUpdateApplier — spawns a  │
│ Models/ReleaseInfo.cs (NEW)        │  │  temp-copy helper process, exits self  │
│ UpdateVersionComparer.cs (NEW)     │  │                                         │
│ UpdateOrchestrator.cs (NEW)        │  │ SingleInstanceGuard.cs (NEW)           │
│  — check → compare → decide-to-    │  │  named Mutex (acquire/detect) + named  │
│  prompt; no UI, no OS calls        │  │  pipe (server: listen+focus-callback,  │
│                                     │  │  client: best-effort signal-and-exit)  │
│ GitHubReleaseFeed.cs (NEW)         │  │  — mirrors GlobalHotkey.cs precedent:  │
│  HttpClient-based, lives in Core   │  │  leaf startup concern, no Core         │
│  since it is genuinely platform-   │  │  interface needed for the guard itself │
│  neutral (no Win32/COM), matching  │  │                                         │
│  JsonSettingsStore's precedent of  │  │ UpdateApplyEntryPoint.cs (NEW)          │
│  "plain BCL I/O lives in Core"     │  │  the --apply-update helper-process      │
│                                     │  │  logic: wait-for-lock-release, rename-  │
│ StartupArgs.cs (EXTENDED)          │  │  swap, relaunch (see Pattern 2 below)   │
│  + TryGetApplyUpdateArgs(args)     │  │                                         │
│  alongside existing                │  └────────────────────────────────────────┘
│  ShouldStartHidden(args)           │
└──────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────┐
│  RigToggle.App.csproj / win-x64.pubxml (MODIFIED, no new project)           │
│  + <Version>2.2</Version> (NEW — no version source currently exists at all) │
│  + DebugType=none, EventSourceSupport=false, UseSystemResourceKeys=true,    │
│    HttpActivityPropagationSupport=false (NEW — see Pattern 3)               │
│  PublishTrimmed stays explicitly false (UNCHANGED — CLAUDE.md constraint)   │
└────────────────────────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────────────────────────┐
│  .github/workflows/release.yml (MODIFIED)                                   │
│  + pass -p:Version=<tag-without-v> to dotnet publish so the shipped exe's   │
│    embedded version always matches the GitHub Release tag exactly (NEW)     │
└────────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Layer | Responsibility | Status |
|-----------|-------|-----------------|--------|
| `StartupArgs` | Core | Parse `--tray` (existing) and the new internal `--apply-update` args; never throws on garbage input (Security Domain V5, same discipline as today) | Extended |
| `IReleaseFeed` / `GitHubReleaseFeed` | Core | Fetch `GET /repos/{owner}/{repo}/releases/latest`, deserialize to `ReleaseInfo` (tag, asset download URL, published date). Sets a `User-Agent` header (GitHub API rejects requests without one). Unauthenticated — fine at 60 req/hr for a launch-time check by one user | New |
| `ReleaseInfo` | Core | Plain model: `TagName`, `AssetDownloadUrl`, `HtmlUrl`, `PublishedAt`, `Prerelease` | New |
| `UpdateVersionComparer` | Core | Pure, unit-testable: strips a leading `v` from the tag, parses `Major.Minor` only (the project's tags are two-part: `v1.0`…`v2.1`, not full semver), and compares against the running assembly's version — see Pitfall note below on `System.Version` component-count mismatch | New |
| `UpdateOrchestrator` | Core | Sequences check → compare → decide "prompt user?" and, on confirm, delegates download+apply to `IUpdateApplier`. No `MessageBox`/`Form` reference — mirrors `ToggleService`/`ToggleOrchestrator`'s split of "Core sequences, App/Windows execute OS/UI work" | New |
| `IUpdateApplier` / `WindowsUpdateApplier` | Core iface / Windows impl | Downloads the release asset to a staging path, copies the *running* exe to a temp helper path, launches that helper with `--apply-update`, then triggers app exit | New |
| `UpdateApplyEntryPoint` | Windows (invoked from `Program.cs` gate 2) | Runs *inside the temp-copy helper process*: waits for the original exe file to become writable, renames it to `.bak`, moves staged exe into place, deletes `.bak` (best-effort retry), relaunches the real exe path (preserving `--tray` if the pre-update session was hidden), exits | New |
| `SingleInstanceGuard` | Windows | Named `Mutex` for first-instance detection; named pipe **server** (started only by the first instance, after `mainForm` exists) that invokes `mainForm.RestoreAndFocus` on message receipt; named pipe **client** (used by a second launch to signal, best-effort, then exit) | New |
| `MainForm.RestoreAndFocus()` | App | Extracted from the existing `NotifyIcon_MouseClick` left-click handler (`Show(); WindowState = Normal; Activate();`) so both the tray click path and the new pipe-server callback share one implementation — same DRY discipline the codebase already applies elsewhere (`SendToTray()`, `ThemeApplier` helpers) | Refactor of existing code |
| `UpdatePromptDialog` | App | Theme-aware "update available" confirm dialog, constructed with `themeProvider` like `MonitorConfirmDialog` — **not** a bare `MessageBox.Show`, which would not pick up `OverridableThemeProvider` and would regress v2.1's theming investment | New |
| `RigToggle.App.csproj` / `win-x64.pubxml` | Build | `<Version>`, and four size-reduction feature switches (`DebugType=none`, `EventSourceSupport=false`, `UseSystemResourceKeys=true`, `HttpActivityPropagationSupport=false`); `PublishTrimmed` stays `false` | Modified |
| `release.yml` | CI | Extracts the version from the pushed tag and passes `-p:Version=...` to `dotnet publish`, so the exe's embedded `AssemblyVersion` always matches the tag the update-checker compares against | Modified |

## Recommended Build Order

The three features are not equally coupled. Build in this order:

**1. Exe-size reduction first.** Fully isolated — four `csproj`/`pubxml` property edits plus a `dotnet publish` size/behavior check. Zero interaction with `Program.cs` control flow, zero interaction with the other two features. Doing it first also means every subsequent phase's manual test builds/downloads are already working against the smaller artifact, and any regression is trivially bisectable (it's the only change in that phase).

**2. Single-instance guard second.** This is the feature that actually restructures `Program.cs`'s startup sequence — it introduces the earliest new gate (`SingleInstanceGuard.TryAcquire()`) and extends `StartupArgs` with the pattern (`TryGetApplyUpdateArgs`) that auto-update's relaunch step depends on. Building this first establishes a stable, rig-verified startup ordering before auto-update has to reason about it.

**3. Auto-update last.** Its relaunch step is the one concrete scenario that exercises the interaction the milestone context explicitly worries about: *"a relaunch-after-update must not trip the guard against itself."* By the time this phase starts, `StartupArgs.TryGetApplyUpdateArgs` and the "gate 2 bypasses gate 3 entirely" ordering (see Pattern 1/Data Flow) already exist and are rig-verified in isolation from a real single-instance guard — auto-update only has to *use* that bypass correctly, not invent it under pressure while also debugging a brand-new self-replace-and-relaunch mechanism.

Do not build auto-update before the single-instance guard exists in `Program.cs`, even as a stub — the self-relaunch helper process (`--apply-update`) is itself a full launch of `RigToggle.App.exe`, and if the guard is added *after* the updater's relaunch code already assumes "gate 2 always runs before I do," it is easy to accidentally route the helper process through the normal guard/mutex path and have it either (a) silently no-op because it thinks a "real" instance is already running (the one about to be replaced), or (b) successfully acquire the mutex itself and then race the *actual* relaunched instance for it seconds later. Building the guard first, with the bypass check as its very first line, removes this ordering hazard structurally rather than relying on both phases' authors remembering to coordinate it.

## Architectural Patterns

### Pattern 1: Startup-gate bypass for the internal relaunch helper

**What:** `Program.cs` checks `StartupArgs.TryGetApplyUpdateArgs(args)` as literally the first branch after the two calls that must run before any Form/control is constructed (`SetColorMode`, `ApplicationConfiguration.Initialize()` — both already documented as position-sensitive in the current file). If present, control transfers entirely to `UpdateApplyEntryPoint.Run(...)` and `Main()` returns from there — the single-instance guard, settings load, controller construction, `MainForm`, and `Application.Run` are never reached.

**When to use:** Any time a process needs to relaunch *itself* as a privileged/special-purpose helper. The alternative — letting the helper process go through the normal single-instance guard and just special-casing its behavior *after* acquiring the mutex — is strictly worse: it makes the helper's brief existence observable to (and blockable by) the same mutex the "real" app uses, for no benefit, and it means the guard's acquire/signal logic has to know about update-apply mode at all (it shouldn't have to).

**Trade-offs:** Requires `StartupArgs` to grow a second parser alongside `ShouldStartHidden`, and requires the helper's relaunch call to explicitly forward the pre-update `--tray` state (see Pattern 2) since nothing else will preserve visible/hidden session continuity across the swap.

**Example (illustrative, not literal code):**
```csharp
// Program.cs, very top of Main(), before basePath/settingsStore setup
if (StartupArgs.TryGetApplyUpdateArgs(args, out var applyRequest))
{
    UpdateApplyEntryPoint.Run(applyRequest); // waits, swaps, relaunches, exits
    return;
}

if (!SingleInstanceGuard.TryAcquire())
{
    SingleInstanceGuard.SignalExistingInstance(); // best-effort, short timeout
    return;
}
```

### Pattern 2: Detached-helper self-update via process replication, not in-place overwrite

**What:** A running Windows exe cannot be deleted or opened for write while its process is live, but it *can* be renamed — this is the basis of the standard "process replication" self-update pattern: the running app copies its own image to a distinct temp path, launches that copy as a helper with `--apply-update`, and exits (releasing its lock). The helper — now running from a file the *original* app never touches — waits for the original exe path to become writable (poll/retry `File.Open` with `FileAccess.Write`, short delay, bounded timeout; the passed original PID is an optional fast-path via `Process.WaitForExit`, not the sole mechanism, since PIDs can be reused), renames the target to `.bak`, moves the already-downloaded staged exe into the target's place, best-effort-deletes the `.bak`, relaunches the target path (forwarding whether the pre-update session was `--tray`), and exits.

**When to use:** Whenever a self-contained single-file app needs to replace its own binary on Windows without an installer/MSI and without admin rights. This is exactly this project's situation (standalone flat `.exe` attached directly to a GitHub Release, no installer, no elevation manifest by design per `CLAUDE.md`).

**Trade-offs:**
- The temp-copy helper is itself an unsigned `.exe` copied to `%TEMP%` and executed autonomously. Files an app downloads itself via `HttpClient` generally do **not** pick up the browser-style Mark-of-the-Web `Zone.Identifier` alternate data stream the way an Edge/Chrome download does, so this path is unlikely to trigger the classic SmartScreen "Windows protected your PC" browser-download prompt — but "app copies itself to a temp path and re-executes" is a behavior pattern some antivirus/EDR heuristics associate with self-replicating malware, independent of MoTW. This is the same class of risk `CLAUDE.md` already flags for NirSoft-tool bundling (heuristic false positives on a legitimate personal tool) — worth a documented, non-blocking known-limitation note in this milestone rather than a blocker, since there is no code-signing budget for a personal single-user project.
- This mechanism has **not** been rig-verified yet (unlike almost everything else in this codebase's history, which is rig-tested before being trusted) — flag it explicitly for real-Windows-11 verification: cold self-replace while the app is running from a self-contained single-file publish (not `dotnet run`), confirm the rename-while-running assumption holds for this specific publish mode (`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`), and confirm `--tray` state survives the relaunch.
- Do **not** reach for Velopack or `AutoUpdater.NET.Official` here (see Anti-Patterns) — both assume a packaging/installer model that conflicts with this project's "one flat exe artifact attached to a GitHub Release" distribution, which `release.yml` already implements and which the user has not asked to change.

**Example (illustrative, not literal code):**
```csharp
// WindowsUpdateApplier.ApplyAndRelaunch(...)
string runningExePath = Environment.ProcessPath!;
string helperPath = Path.Combine(Path.GetTempPath(), $"RigToggle-updater-{Environment.ProcessId}.exe");
File.Copy(runningExePath, helperPath, overwrite: true);

var psi = new ProcessStartInfo(helperPath)
{
    Arguments = $"--apply-update \"{stagedExePath}\" \"{runningExePath}\" {Environment.ProcessId} {(wasStartedHidden ? "--tray" : "")}",
    UseShellExecute = true,
};
Process.Start(psi);
Application.Exit(); // releases the Mutex; UpdateApplyEntryPoint takes over from here
```

### Pattern 3: MSBuild feature-switch size reduction, no trimming

**What:** Beyond v2.0's four levers (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, the NAudio meta-package split — all already shipped), .NET exposes further **runtime feature switches** that strip specific subsystems' code and data at publish time *without* enabling the IL trimmer's reachability analysis (the thing `CLAUDE.md` explicitly forbids for this COM/P-Invoke-heavy codebase). These are safe because they are simple `if (FeatureSwitch.Enabled) { ... }` branches the compiler/publish pipeline can fold, not a whole-program static-reachability pass that can misjudge reflection/marshalling call sites.

Concretely, for `RigToggle.App.csproj`:
- `<DebugType>none</DebugType>` — suppresses `.pdb` generation entirely for the publish output (this app has no crash-reporting pipeline that consumes symbols, and `debug.log` already covers the project's actual diagnostic story per the existing `EnableDebugLogging`-gated `Trace` listener).
- `<EventSourceSupport>false</EventSourceSupport>` — this app does not use `EventSource`/ETW tracing anywhere in `src/`.
- `<UseSystemResourceKeys>true</UseSystemResourceKeys>` — trims verbose BCL exception-message resource strings; this app already routes its own user-facing messages through `ToggleResultFormatter`, not raw framework exception text, so the loss of framework message fidelity is low-risk.
- `<HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>` — disables `DiagnosticSource` HTTP activity propagation; relevant now that `GitHubReleaseFeed` introduces the app's first `HttpClient` usage, and this app has no distributed-tracing consumer.

**When to use:** Any self-contained publish where `PublishTrimmed` is off by policy but binary size still matters. Verify each switch against actual usage before flipping it (as done above) — these are safe in general but not universally free; e.g. flipping `UseSystemResourceKeys` on a codebase that *does* surface raw BCL exception messages to users would degrade error-message quality, which doesn't apply here but should be re-checked if that changes.

**Trade-offs:** Individually small (a few hundred KB to low single-digit MB each, per community measurements) — expect a meaningfully smaller *additional* cut than v2.0's 57.79%, not a second dramatic drop. Still real and additive, and free of the P/Invoke/COM stripping risk `PublishTrimmed` carries. Must be re-measured with a real `dotnet publish` + file-size diff and a rig cold-boot/toggle-round-trip check, mirroring PERF-01/PERF-02's existing verification discipline — do not just trust the delta without cross-checking `EnableDebugLogging`, `GitHubReleaseFeed`, and the toggle path all still work post-publish.

## Data Flow

### Startup Sequencing (all four reachable paths through `Program.cs`)

```
Process launched (Explorer double-click, autostart Run key, tray "relaunch", or --apply-update helper)
    │
    ▼
SetColorMode() / ApplicationConfiguration.Initialize()        [unchanged, must stay first]
    │
    ▼
StartupArgs.TryGetApplyUpdateArgs(args)?
    │
    ├── YES ──▶ UpdateApplyEntryPoint.Run(request)             [NEW — Pattern 1/2]
    │             wait-for-writable → rename → swap → relaunch real exe → exit
    │             (no Mutex ever touched by this path)
    │
    └── NO
          │
          ▼
    SingleInstanceGuard.TryAcquire()  (named Mutex)            [NEW]
          │
          ├── FAILS (already running) ──▶ SignalExistingInstance() via named pipe,
          │                                best-effort short timeout ──▶ return/exit
          │                                (settings/mode/controllers never touched)
          │
          └── SUCCEEDS (first instance)
                │
                ▼
          settingsStore / modeStore / markerStore bootstrap    [unchanged]
                │
                ▼
          StartupRecoveryChecker.Run(...)                      [unchanged]
                │
                ▼
          controllers + themeProvider + toggleService/orchestrator [unchanged]
                │
                ▼
          mainForm construction, InitializeTrayState(),
          RegisterHotkeyAtStartup()                             [unchanged]
                │
                ▼
          SingleInstanceGuard.StartListening(mainForm.RestoreAndFocus) [NEW]
                │
                ▼
          mainForm.BeginInvoke(UpdateOrchestrator.CheckOnLaunchAsync) [NEW — fire-and-
                │                                                        forget onto the
                │                                                        UI thread once
                │                                                        the message loop
                │                                                        is pumping]
                ▼
          Application.Run(...)                                 [unchanged, branches on
                                                                  StartupArgs.ShouldStartHidden
                                                                  exactly as today]
```

**Open verification question (flag for the phase, not resolved by this research):** `mainForm.BeginInvoke` requires `mainForm.Handle` to already exist. On the visible path this is not a concern (`Show()`/`Application.Run(mainForm)` forces handle creation). On the `--tray` hidden path, `mainForm` is never `Show()`n — `InitializeTrayState()` runs first and very likely forces handle creation as a side effect of wiring up `NotifyIcon`/tray menu (consistent with how that method already primes tray state before either `Application.Run` branch), but this project's own history (`ApplicationContext(mainForm)` theory disproven by rig testing at v1.1, `Form.AutoSize` theory disproven at v2.1 Phase 22) is a standing reminder not to assume WinForms timing behavior — verify `mainForm.Handle != IntPtr.Zero` is already true at the `BeginInvoke` call site under `--tray` on real hardware before trusting this path, and have a documented fallback (e.g., a short one-shot `System.Windows.Forms.Timer` instead of `BeginInvoke`) ready if it isn't.

### Update-check-to-relaunch flow

```
UpdateOrchestrator.CheckOnLaunchAsync()
    │
    ▼
GitHubReleaseFeed.GetLatestReleaseAsync()   -- GET /repos/{owner}/{repo}/releases/latest
    │  (User-Agent header required; endpoint already excludes drafts/prereleases)
    ▼
UpdateVersionComparer.IsNewer(runningAssemblyVersion, releaseInfo.TagName)
    │
    ├── NOT newer ──▶ done, no UI
    │
    └── newer ──▶ UpdatePromptDialog (theme-aware, App layer) shown on UI thread
                    │
                    ├── user declines ──▶ done, no UI, ask again next launch
                    │
                    └── user confirms
                          │
                          ▼
                    IUpdateApplier.DownloadAndStageAsync(releaseInfo.AssetDownloadUrl)
                          │
                          ▼
                    WindowsUpdateApplier.ApplyAndRelaunch(stagedPath)  -- Pattern 2
                          │
                          ▼
                    Application.Exit()  -- releases the Mutex; helper process takes over
```

## Anti-Patterns

### Anti-Pattern 1: Letting the `--apply-update` helper process go through the normal single-instance gate

**What people do:** Add the single-instance guard and the auto-update relaunch step in either order without an explicit bypass, assuming "it'll just work because the original process exits first."

**Why it's wrong:** The helper process *is* `RigToggle.App.exe` (a temp copy of it). If it runs through the same `Program.cs` `Main()` as a normal launch, it will hit the mutex check. Depending on exact timing relative to the original process's exit, this either wedges the helper behind a mutex the original hasn't released yet (original hasn't fully exited when the copy starts trying to acquire), or — worse — the helper itself "wins" the mutex, then the *actual* freshly-swapped-in relaunch a few lines later becomes a second instance that gets signaled and exits, silently killing the update's own final relaunch.

**Do this instead:** Check `TryGetApplyUpdateArgs` as literally the first branch in `Main()`, before the guard is even constructed (Pattern 1). The helper never touches the Mutex at all.

### Anti-Pattern 2: Reaching for Velopack or `AutoUpdater.NET.Official` by default

**What people do:** Pull in a mature, well-known auto-update library rather than hand-rolling the swap dance, on the theory that "this is exactly the kind of fiddly OS-lock-handling code a library should own."

**Why it's wrong here:** Both assume a packaging/installer model this project does not use. Velopack generates its own portable/installer package layout (versioned subfolders under `%LocalAppData%\<AppId>\current\`, its own `Update.exe`, its own channel/delta-package format) — adopting it would restructure the entire distribution model `release.yml` already implements (one flat `.exe` attached directly to a GitHub Release) for no requirement the milestone actually states. `AutoUpdater.NET.Official`'s default flow expects an XML update-manifest hosted at a URL and typically launches an *installer* file, not a raw portable exe swap — again a packaging assumption this project doesn't share, and it would be the app's first NuGet dependency purely for a ~150-250 line mechanism this codebase's own conventions (hand-rolled `IPolicyConfig`, hand-rolled `RegisterHotKey`) already demonstrate a strong, deliberate preference for owning directly rather than depending on for something this size and this central to reliability.

**Do this instead:** Hand-rolled `GitHubReleaseFeed` (HttpClient + `System.Text.Json`, already in the BCL) + hand-rolled process-replication swap (Pattern 2). Revisit only if a future milestone actually wants an installer/MSIX-style distribution model — which `CLAUDE.md`'s "Out of Scope" section already rules out for unrelated reasons (conflicts with the standalone-.exe constraint).

### Anti-Pattern 3: `MessageBox.Show` for the update-available prompt

**What people do:** Use the quickest possible UI for a one-off confirm dialog.

**Why it's wrong:** `MessageBox.Show` renders with native OS chrome and does not participate in `OverridableThemeProvider`/`ThemeApplier` at all — on a Dark-override session it would show as a jarring native-light popup, exactly the kind of "one surface missed the shared theme resolver" regression THEME-09's whole redesign (collapsing three independent `IsDark` copies into one decorator) was built to prevent structurally.

**Do this instead:** A small `UpdatePromptDialog : Form`, constructed with the same `themeProvider` local from the composition root, following `MonitorConfirmDialog`'s existing pattern exactly.

### Anti-Pattern 4: Comparing `System.Version` objects parsed from mismatched component counts

**What people do:** `new Version(tagStringWithVStripped) > Assembly.GetExecutingAssembly().GetName().Version` directly.

**Why it's wrong:** This project's release tags are two-part (`v1.0`, `v2.1`, not `v2.1.0`). `new Version("2.2")` yields `Major=2, Minor=2, Build=-1, Revision=-1`. If `<Version>2.2</Version>` is set in the csproj, .NET normalizes the *assembly's* actual `AssemblyVersion` to `2.2.0.0` (`Build=0, Revision=0`). `Version.CompareTo` treats an unset (`-1`) component as less than an explicit `0`, so a same-version comparison (`2.2` tag vs `2.2.0.0` running assembly) can incorrectly evaluate as "tag is older" purely due to component-count mismatch, not an actual version difference — a subtle false-negative (or false-positive, depending on which side is missing components) that would silently suppress or wrongly trigger the update prompt.

**Do this instead:** `UpdateVersionComparer` should explicitly compare only `Major`/`Minor` from both sides (the actual granularity this project's versioning scheme uses), not raw `Version` object comparison across mismatched component counts.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| GitHub Releases API (`api.github.com/repos/bpivk/monitor-toggle/releases/latest`) | Unauthenticated `HttpClient` GET from `GitHubReleaseFeed` (Core) | Requires a `User-Agent` header or GitHub returns 403. `/releases/latest` already excludes drafts and prereleases server-side — no client-side filtering needed. Unauthenticated rate limit (60/hr per IP) is generous for one launch-time check by a single user; do not add a PAT/auth for this. |
| GitHub Release asset download (the attached `RigToggle.App.exe`) | `HttpClient` streamed download to a staging path, invoked from `WindowsUpdateApplier` (Windows) | Same asset `release.yml` already attaches today (`files: src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe`) — no new CI artifact needed, only the version-stamping change (`-p:Version=...`) so the downloaded exe's embedded version is trustworthy for the *next* check. |
| GitHub Actions (`release.yml`) | Existing tag-triggered workflow, modified to extract the tag and pass `-p:Version=$VERSION` to `dotnet publish` | Currently no version is stamped into the exe at all — this is a genuine gap, not an enhancement; without it, `Assembly.GetExecutingAssembly().GetName().Version` reads the SDK default (`1.0.0.0`) regardless of which tag was actually published, and the update-checker cannot function correctly. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `Program.cs` ↔ `SingleInstanceGuard` | Direct method calls (`TryAcquire`, `StartListening`, `SignalExistingInstance`) | No Core interface — mirrors the existing `GlobalHotkey.cs` precedent (a leaf startup-sequencing concern nothing else needs to fake/mock, unlike `IMonitorController`/`IAudioController` which `ToggleService` genuinely needs to unit-test against). |
| `SingleInstanceGuard` (pipe server) ↔ `MainForm` | `mainForm.Invoke(mainForm.RestoreAndFocus)` from the pipe server's background thread | Must marshal onto the UI thread — the pipe server runs on its own listener thread/`Task`, and `RestoreAndFocus()` touches `Form` state (`Show()`, `WindowState`, `Activate()`), which is not thread-safe to call directly. |
| `UpdateOrchestrator` (Core) ↔ `IUpdateApplier` (Windows impl) | Interface call, same pattern as `ToggleService` ↔ `IMonitorController`/`IAudioController`/`IAppController` | Keeps the "should I prompt/what version is newer" decision testable in `RigToggle.Tests` without touching the filesystem or spawning real processes; only `RigToggle.Windows.Tests` (or manual/rig verification) needs to cover the actual swap-and-relaunch mechanics. |
| `WindowsUpdateApplier` ↔ `UpdateApplyEntryPoint` | Process boundary — `ProcessStartInfo` + command-line args, not a shared in-memory call | Deliberate: this is exactly why the helper must be a *separate process* from a *separate file path* (Pattern 2) — an in-process "restart" cannot release the original exe's file lock. |

## Robustness Considerations (in place of generic user-scale table — this is a single-user local tool)

| Concern | Current single-user reality | What actually needs to hold up |
|---------|------------------------------|-----------------------------|
| Simultaneous double-launches | User double-clicks the exe twice quickly, or Explorer re-triggers a launch while autostart is also starting it | Both processes call `TryAcquire()` near-simultaneously — named `Mutex` acquisition is atomic at the OS level, so exactly one wins regardless of timing; the loser's `SignalExistingInstance()` must tolerate the winner's pipe server not being up yet (short bounded retry, then give up silently — same best-effort posture as `RegisterHotkeyAtStartup`). |
| Update check on a flaky/offline connection | Rig or home network briefly down at launch | `GitHubReleaseFeed`/`UpdateOrchestrator` must fail silently (best-effort, same posture as `EnableDebugLogging`'s trace listener and `RegisterHotkeyAtStartup`) — a failed update check must never block or delay normal startup, and must never surface an error dialog for something this low-stakes. |
| Crash mid-update-apply (helper killed before relaunch) | Power loss / kill mid-swap | Worst case should be "old exe renamed to `.bak`, new exe present but app not running" or "both `.bak` and target exist" — never "neither exists." Recommend: rename-then-move (not delete-then-move) so there is always at least one recoverable file on disk at every intermediate step, and consider a startup check in `Program.cs` (a new, small analog to `StartupRecoveryChecker`) that detects an orphaned `.bak` next to the running exe and cleans it up on next normal launch. |

## Sources

- Direct reading of this repository: `src/RigToggle.App/Program.cs`, `MainForm.cs` (tray restore/focus, resize/minimize handlers), `src/RigToggle.Core/ToggleOrchestrator.cs`, `StartupArgs.cs`, `src/RigToggle.Windows/WindowsAppController.cs`, `RigToggle.App.csproj`, `Properties/PublishProfiles/win-x64.pubxml`, `.github/workflows/build.yml`, `.github/workflows/release.yml`, `git tag -l` (confirms `v1.0`…`v2.1` two-part tag scheme, no existing `<Version>` anywhere in the solution) — HIGH confidence, primary source.
- `.planning/PROJECT.md` — v2.2 milestone framing, constraints (no elevation manifest, no `PublishTrimmed`, standalone `.exe` distribution), full Key Decisions history — HIGH confidence, primary source.
- WebSearch: single-instance WinForms patterns (dotnet-guide.com, autoitconsulting.com, dzimchuk.net "Single instance of a WPF app – part 2 (WM_COPYDATA)") — converges on named Mutex for detection + named pipe (preferred over `WM_COPYDATA` for new designs) for signaling — MEDIUM-HIGH confidence, corroborated across multiple independent sources.
- WebSearch: self-contained single-file exe self-update mechanics (andreasrohner.at "A platform independent way for a C# program to update itself"; Visual Studio Magazine "Replace a Running Application with a New Version"; multiple GitHub issue threads on Windows exe-rename-while-locked behavior) — converges on the process-replication pattern described in Pattern 2 — MEDIUM confidence (community-sourced, not official Microsoft guidance, but internally consistent across independent sources and consistent with well-known Windows file-lock semantics).
- WebSearch: `AutoUpdater.NET.Official` (github.com/ravibpatel/AutoUpdater.NET, NuGet listing) and Velopack (github.com/velopack/velopack, docs.velopack.io) — confirmed both assume a packaging/manifest model distinct from this project's flat-exe-on-GitHub-Release distribution — MEDIUM confidence, used to justify the hand-rolled recommendation, not as a definitive rejection of either library in general.
- WebSearch: Mark-of-the-Web / `Zone.Identifier` / SmartScreen behavior (textslashplain.com "Downloads and the Mark-of-the-Web", Outflank "Mark-of-the-Web from a Red Team's Perspective") — HIGH confidence on the MoTW mechanism itself (well-documented Windows feature); MEDIUM-LOW confidence on the specific claim that `HttpClient`-initiated downloads don't receive MoTW the way browser downloads do — flagged accordingly in Pattern 2, not asserted as certain.
- WebSearch: .NET publish size-reduction feature switches (dotnet/runtime GitHub issues/PRs on `EventSourceSupport`/`UseSystemResourceKeys`/`EnableActivityPropagation`, Microsoft Learn `.NET application publishing overview`) — HIGH confidence on the switches' existence and mechanism (official/near-official sourcing); MEDIUM confidence on exact size-delta magnitude for *this* codebase specifically, since that depends on this app's actual code shape and has not been measured yet — flagged as needing a real `dotnet publish` diff, matching this project's existing PERF-01 verification discipline.
- GitHub REST API documentation conventions (well-established, not separately re-verified this session): `/releases/latest` excludes drafts and prereleases; unauthenticated requests require a `User-Agent` header — HIGH confidence, standard/stable GitHub API behavior.

---
*Architecture research for: Rig Toggle v2.2 (Auto-Update, Single-Instance Guard & Smaller Footprint)*
*Researched: 2026-08-18*
