# Phase 18: Cleanup Pass & Exe-Size Reduction - Research

**Researched:** 2026-08-08
**Domain:** .NET 10 self-contained WinForms packaging (MSBuild publish config) + dead-code removal in a mature (17-phase) C# codebase
**Confidence:** HIGH

## Summary

This phase has two independent halves, and research confirms both are lower-risk and more concretely verifiable than the phase description implies.

**Half 1 (CLEANUP-01/02 — dead code removal):** The snapshot-restore subsystem is *already confirmed fully dead* in production code, not just "probably dead." Direct grep of the whole `src/` tree shows `WindowsMonitorController.Restore()`/`RestoreViaReconstruction()`, `IAudioController.Restore`/`WindowsAudioController.Restore()`, `ISnapshotStore`/`JsonSnapshotStore`/`StateSnapshot`, and two internal helper methods (`CopyOutputTechnology`, `AssignSource`) have **zero production call sites** — `ToggleService` (rewritten in Phase 16) calls only `CaptureState()`, `ActivateMonitors()`, `DeactivateMonitors()`, and `SetDefault()`. The only live production reference to the snapshot subsystem is one bootstrap read in `Program.cs` (`snapshotStore.Exists()`), which Phase 16 deliberately left for Phase 18 to resolve. This was independently corroborated by Phase 15's own code review (`15-REVIEW.md` WR-01/IN-01), which flagged `IAudioController.Restore` as dead and explicitly deferred its removal to "Phase 18 cleanup scope." CLEANUP-02 has a similarly narrow, evidence-based scope: three prior REVIEW.md files name five concrete, still-open, low-risk candidates (not a speculative refactor pass).

**Half 2 (PERF-01/02 — exe size):** All four MSBuild levers were tested directly in this research session using `dotnet publish -r win-x64 --self-contained` with `-p:EnableWindowsTargeting=true` (the exact flag this project's own Phase 16/17 SUMMARY.md files already use to cross-build/cross-test the Windows-targeted solution in a non-Windows CI/research environment). Measured, reproducible result: baseline self-contained single-file exe is **116,946,229 bytes (~112 MB)**; applying `SatelliteResourceLanguages=en` + `InvariantGlobalization=true` + `EnableCompressionInSingleFile=true` together drops it to **49,444,892 bytes (~47 MB)** — a 57.7% reduction — and additionally swapping the NAudio meta-package for `NAudio.Wasapi` alone (the only sub-package this app's `NAudio.CoreAudioApi` usage needs) drops it further to **49,360,387 bytes**, an additional ~84 KB. Combined: **67.6 MB saved, 57.8% smaller**, all without touching `PublishTrimmed` (confirmed still unset/false throughout). This gives the planner exact before/after numbers to assert in a verification task rather than a vague "smaller" claim.

**Primary recommendation:** Structure Phase 18 as two independent waves — Wave A (CLEANUP-01/02, code-only, fully verifiable via `dotnet build`/`dotnet test` in this repo's cross-target environment, no rig needed) and Wave B (PERF-01, MSBuild config, exe-size delta verifiable the same cross-target way) — followed by a single final rig-verification wave (PERF-02) that exercises both halves together on real hardware, matching this project's established Plan-N final-wave human-checkpoint pattern from Phases 15/16/17.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Snapshot-restore removal (CLEANUP-01) | RigToggle.Core (abstractions/models/persistence) | RigToggle.Windows (controller impls), RigToggle.App (composition root), RigToggle.Tests (doubles/tests) | `ISnapshotStore`/`StateSnapshot` live in Core; `Restore()` implementations live in the two Windows adapters; the one live consumer (`snapshotStore.Exists()`) lives in App's composition root; test doubles/tests span both test projects |
| Code-quality pass (CLEANUP-02) | Cross-cutting (Core + App) | — | Named candidates span `ToggleService` (Core), `SettingsForm`/`ToggleServiceTests` (App/Tests), `ToggleResultFormatter` (Core) — no single tier owns it |
| Publish/packaging config (PERF-01) | Build/Packaging (MSBuild, not runtime) | RigToggle.Windows (NAudio package reference only) | `EnableCompressionInSingleFile`/`SatelliteResourceLanguages`/`InvariantGlobalization` are publish-pipeline properties with zero application-code footprint; the NAudio split is a `PackageReference` swap in `RigToggle.Windows.csproj` with no source-code change (namespace `NAudio.CoreAudioApi` is unchanged) |
| Rig verification (PERF-02) | N/A (verification activity, not a tier) | — | Confirms the packaging change didn't regress the Windows/COM-interop runtime tiers (display + audio + app-launch) |

## Project Constraints (from CLAUDE.md)

- **No IL trimming ever:** `PublishTrimmed` must remain unset/false project-wide. CLAUDE.md and `REQUIREMENTS.md`'s "Out of Scope" table both call this out explicitly — trimming's reachability analysis misidentifies the COM-interop (`IPolicyConfig` audio) and P/Invoke marshalling (`WindowsDisplayAPI` CCD) code paths as dead and strips them. PERF-01's four named levers were chosen specifically because none of them touch trimming.
- **`.NET 10`, self-contained, single-file, `win-x64` only** — established in `RigToggle.App.csproj` (`TargetFramework=net10.0-windows`, `RuntimeIdentifier=win-x64`) and `Properties/PublishProfiles/win-x64.pubxml` (`SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`). Any new publish property added for PERF-01 must be compatible with this existing shape, not a replacement for it.
- **No elevation manifest** — do not add one while touching any `.csproj` in this phase (irrelevant to this phase's changes but flagged since CLAUDE.md repeats this constraint in every `.csproj`'s own comments; a reviewer will check for regressions here).
- **`RigToggle.Core` has zero Windows API references** — enforced by a comment in `RigToggle.Core.csproj` itself. The snapshot-restore removal must not leave any dangling Windows-specific type in Core (it won't — `StateSnapshot`/`ISnapshotStore`/`MonitorState`/`AudioState` are already plain records with no Windows references).
- **GSD workflow enforcement** — this phase's execution must go through `/gsd:execute-phase`, not direct edits.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| PERF-01 | Self-contained exe size reduced via MSBuild-level config (`EnableCompressionInSingleFile`, `SatelliteResourceLanguages=en`, `InvariantGlobalization=true`, NAudio meta-package split) — without IL trimming | Standard Stack / Code Examples sections give exact property syntax, exact file locations (`RigToggle.App.csproj`, `win-x64.pubxml`, `RigToggle.Windows.csproj`), and live-measured before/after byte counts (116,946,229 -> 49,360,387 bytes, 57.8% smaller) confirming the combination works and stays trimming-free |
| PERF-02 | Exe-size changes verified on real rig hardware (cold autostart boot timing + full toggle round trip), not just a build-output size diff | Architecture Patterns Pattern 3 + Common Pitfalls Pitfall 1 establish which parts are verifiable off-rig (build/publish/size, done this session) vs. which require the rig (functional cold-boot + toggle behavior); Open Question 2 scopes the cold-boot check as qualitative, matching this project's existing UAT rig-checkpoint convention |
| CLEANUP-01 | Dead snapshot-restore code (`Restore()`/`RestoreViaReconstruction()` + related models) removed after reviewing for rig-specific knowledge worth preserving first | Summary + Architecture Patterns Pattern 1/2 give the exhaustive-grep-confirmed dead-code inventory (Core: `ISnapshotStore`/`JsonSnapshotStore`/`StateSnapshot`; Windows: both controllers' `Restore()` + 2 dead helpers; App: the one live `Program.cs` bootstrap read and its minimal replacement); Recommended Project Structure lists every file needing an edit or deletion |
| CLEANUP-02 | General code-quality pass — reduced duplication/cruft, no user-facing behavior change | Don't Hand-Roll + Anti-Patterns sections scope this to 5 concrete, already-reviewed candidates (IN-01 dead test knob, IN-04 dead defensive branch, IN-02 capitalization mismatch, WR-03 pointless branch collapse, optional IN-03 sentinel-name fix) sourced directly from `15-REVIEW.md`/`16-REVIEW.md`, explicitly excluding out-of-scope items like `17-REVIEW.md` WR-02 |

</phase_requirements>

## Standard Stack

No new external dependencies are introduced by this phase. One existing dependency is narrowed (see below).

### Core (unchanged, for reference)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `WindowsDisplayAPI` | 1.3.0.13 | CCD display topology control | Unchanged this phase — confirmed still the latest published version via NuGet registry query (`api.nuget.org/v3-flatcontainer/windowsdisplayapi/index.json` → highest listed is `1.3.0.13`) [VERIFIED: npm registry equivalent — NuGet flat-container API] |
| `NAudio.Wasapi` | 2.3.0 | Audio endpoint enumeration/default-device COM interop support (`NAudio.CoreAudioApi.MMDeviceEnumerator`/`MMDevice`) | **Changed this phase** — replaces the `NAudio` meta-package. `WindowsAudioController.cs` has exactly one NAudio `using` (`using NAudio.CoreAudioApi;`), and confirmed via the official NAudio GitHub repo/NuGet listings that `MMDeviceEnumerator`/`MMDevice`/`CoreAudioApi` live in `NAudio.Wasapi` (which itself pulls in `NAudio.Core` transitively) — `NAudio.WinMM`, `NAudio.Midi`, `NAudio.Asio`, `NAudio.WinForms`, `NAudio.Dmo` are all unused backends this app never references. [CITED: github.com/naudio/NAudio, confirmed via NuGet registry: `naudio.wasapi` and `naudio.core` both publish `2.3.0`, matching the currently-pinned meta-package version exactly — no version drift introduced] |

### Supporting (build/packaging only — no NuGet packages)
| Property | Where it lives | Purpose |
|----------|----------------|---------|
| `EnableCompressionInSingleFile` | `Properties/PublishProfiles/win-x64.pubxml` (alongside the existing `PublishSingleFile`/`SelfContained` properties it modifies) | Compresses embedded managed assemblies inside the single-file bundle. [CITED: learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview — "Compress assemblies in single-file apps" section] |
| `SatelliteResourceLanguages` | `RigToggle.App.csproj` `<PropertyGroup>` (the project that actually produces the publish output; safe to also add to `RigToggle.Windows.csproj`/`RigToggle.Core.csproj` for build-time consistency, but only the App project's setting affects the final publish artifact) | Restricts which localized satellite resource assemblies (from `WindowsDisplayAPI`, `NAudio.Wasapi`, and the BCL itself) are copied to the publish output. Value: `en` (not `en-US`) — [CITED: andrewlock.net "Disabling localized satellite assemblies during dotnet publish"] confirms `en` (not `en-US`) is the correct value to exclude *all* satellite assemblies including the base culture; `en-US` would only exclude the `en-US`-specific ones and leave e.g. generic `en` ones in place if any existed. |
| `InvariantGlobalization` | `RigToggle.App.csproj` `<PropertyGroup>` | Disables ICU culture-data loading at runtime, shrinking the runtime footprint. [CITED: learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization — exact syntax `<InvariantGlobalization>true</InvariantGlobalization>`] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `NAudio.Wasapi` alone | `NAudio.Core` + `NAudio.Wasapi` explicit dual reference | Unnecessary — `NAudio.Wasapi` already depends on `NAudio.Core` transitively; adding both explicitly is redundant, not wrong, but adds a line for no benefit. Verified by successfully building with `NAudio.Wasapi` alone in this research session (see Code Examples). |
| `EnableCompressionInSingleFile=true` | Leave uncompressed, rely only on the other 3 levers | Compression is empirically the single largest lever measured this session (see Common Pitfalls — startup-decompression-cost tradeoff below) — dropping it would leave most of the achievable size reduction on the table, but it is the one lever with a documented runtime cost (assemblies decompress into memory at startup), which is exactly why PERF-02 requires a *cold autostart boot* timing check, not just a toggle-round-trip check. |

**Installation (no new packages — this is a `PackageReference` swap):**
```xml
<!-- RigToggle.Windows.csproj — before -->
<PackageReference Include="NAudio" Version="2.3.0" />

<!-- RigToggle.Windows.csproj — after -->
<PackageReference Include="NAudio.Wasapi" Version="2.3.0" />
```

**Version verification (performed live this session):**
```
$ curl -s https://api.nuget.org/v3-flatcontainer/naudio.wasapi/index.json | grep -o '"2.3.0"'
"2.3.0"
$ curl -s https://api.nuget.org/v3-flatcontainer/naudio.core/index.json | grep -o '"2.3.0"'
"2.3.0"
$ curl -s https://api.nuget.org/v3-flatcontainer/windowsdisplayapi/index.json
{"versions": [..., "1.3.0.13"]}   # 1.3.0.13 is still the newest published version
```

## Package Legitimacy Audit

> `slopcheck` (v0.6.1, confirmed installed and runnable this session) only understands the npm and PyPI ecosystems — it has no NuGet support (`slopcheck install`/`scan` do not recognize `.csproj`). This phase's only package change is a **swap within the same publisher's already-approved package family** (NAudio meta-package → NAudio's own official `NAudio.Wasapi` sub-package, published by the same `naudio` GitHub org, same `2.3.0` version already pinned elsewhere in this solution) — not a net-new third-party dependency. Verified directly against the NuGet registry API (`api.nuget.org/v3-flatcontainer`) and the official `github.com/naudio/NAudio` monorepo (the split packages live in the same repo/CI as the meta-package). This is materially lower risk than a genuinely new package addition, but is still recorded here per protocol.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `NAudio.Wasapi` 2.3.0 | NuGet | Same publish cohort as `NAudio` 2.3.0 (already in production use in this solution since Phase 2) | Not independently queried — same maintainer/repo/version as the already-vetted `NAudio` package this replaces | github.com/naudio/NAudio (monorepo, MIT) | N/A — ecosystem unsupported by slopcheck | Approved — verified via NuGet registry API + official GitHub repo, same version pin as existing dependency |

**Packages removed due to slopcheck [SLOP] verdict:** none (slopcheck does not cover NuGet; no packages were flagged)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram — Wave A (dead code removal) data flow being deleted

```
[Pre-toggle mutation]                         [Post-Phase-16: what actually runs today]
        |                                              |
        v                                              v
CaptureState() ---> StateSnapshot ---> JsonSnapshotStore   CaptureState() ---> (in-memory only,
        |          (Save, DEAD PATH)     .Save()                for CR-01 compare, never persisted)
        v                                                        |
[mutate monitor/audio]                                    [mutate monitor/audio via
        |                                                  explicit Rig/Normal config sets]
        v                                                        |
Restore(previousState) <--- JsonSnapshotStore.Load()             v
  (WindowsMonitorController /                            SetDefault(NormalAudioDeviceId) /
   WindowsAudioController,                                ActivateMonitors/DeactivateMonitors
   DEAD — zero callers)                                    (explicit Normal-mode set, DISPLAY-10)
```
The left column (StateSnapshot round-trip + both `Restore()` implementations) is 100% dead — confirmed by exhaustive grep of every `.Restore(` and `ISnapshotStore`/`JsonSnapshotStore`/`StateSnapshot` reference in `src/`. The right column is what `ToggleService.ToggleToRigMode()`/`ToggleToNormalMode()` actually execute today.

### Recommended Project Structure (no new files — deletions/edits to existing structure)
```
src/
├── RigToggle.Core/
│   ├── Abstractions/
│   │   ├── ISnapshotStore.cs          <- DELETE
│   │   └── IAudioController.cs        <- EDIT: remove `void Restore(AudioState previousState);`
│   │   └── IMonitorController.cs      <- EDIT: remove `void Restore(MonitorState previousState);`
│   ├── Models/
│   │   └── StateSnapshot.cs           <- DELETE
│   └── Persistence/
│       └── JsonSnapshotStore.cs       <- DELETE
├── RigToggle.Windows/
│   ├── WindowsMonitorController.cs    <- EDIT: remove Restore/RestoreViaReconstruction/
│   │                                       _originalPathsCache field/CopyOutputTechnology/
│   │                                       AssignSource; keep AnyRectanglesOverlap, MergeAllMonitors
│   └── WindowsAudioController.cs      <- EDIT: remove Restore() + its private helpers' Restore-only usage
├── RigToggle.App/
│   ├── Program.cs                     <- EDIT: replace JsonSnapshotStore/snapshotStore.Exists()
│   │                                       bootstrap read with a bare File.Exists() check
│   └── SettingsForm.cs                <- EDIT (CLEANUP-02): remove dead PopulateAudioCombo
│                                            items.Count==0 branch (IN-04); fix Name-field
│                                            sentinel persistence (IN-03, optional/lower-priority)
├── RigToggle.Tests/
│   ├── JsonStoreTests.cs              <- EDIT: remove all Snapshot* tests, keep SettingsStore* tests
│   ├── ToggleServiceTests.cs          <- EDIT: remove audioThrowsOnRestore param/plumbing (IN-01)
│   └── Doubles/
│       └── InMemoryStores.cs          <- EDIT: remove InMemorySnapshotStore
│       └── FakeControllers.cs         <- EDIT: remove FakeMonitorController.Restore,
│                                            FakeAudioController.Restore + _throwOnRestore
│       └── BlockingMonitorController.cs <- EDIT: remove Restore() no-op stub
└── RigToggle.Windows.Tests/
    └── WindowsMonitorControllerTests.cs <- EDIT: remove CopyOutputTechnology/AssignSource
                                               tests; keep AnyRectanglesOverlap/MergeAllMonitors tests
```

### Pattern 1: Confirm-dead-before-delete via exhaustive grep, not code reading alone
**What:** Before deleting any method flagged as "probably dead" (`Restore`, `RestoreViaReconstruction`, `ISnapshotStore`), grep the *entire* `src/` tree for every call-site pattern (`\.Restore(`, `ISnapshotStore`, `JsonSnapshotStore`, `StateSnapshot`), not just the file being edited.
**When to use:** Any deletion task in this phase's Wave A.
**Example (run this session, confirmed zero production hits):**
```bash
grep -rn "\.Restore(" src --include="*.cs"
# Only hit: a doc-comment in WindowsMonitorControllerTests.cs referencing the method name,
# and MainForm.cs:583's unrelated doc-comment use of the English word "Restore" (window
# restore-from-tray, not the snapshot-restore method — verified by reading the surrounding
# 15 lines).
```

### Pattern 2: Minimal-replacement for the one live legacy-migration read
**What:** `Program.cs` currently does:
```csharp
var snapshotStore = new JsonSnapshotStore(Path.Combine(basePath, "state.json"));
var modeStore = new JsonModeStore(Path.Combine(basePath, "mode.json"));
...
if (!modeStore.Exists())
{
    modeStore.Save(snapshotStore.Exists() ? ToggleMode.Rig : ToggleMode.Normal);
}
```
Once `ISnapshotStore`/`JsonSnapshotStore` are deleted, this one-time bootstrap read (which only checks file *presence*, never `Load()`s or interprets the snapshot's contents) should become a bare file check with no store abstraction, preserving the exact same legacy-migration semantics:
```csharp
string legacyStateJsonPath = Path.Combine(basePath, "state.json");
...
if (!modeStore.Exists())
{
    modeStore.Save(File.Exists(legacyStateJsonPath) ? ToggleMode.Rig : ToggleMode.Normal);
}
```
**When to use:** This exact call site in `Program.cs`. No other code needs an `ISnapshotStore` replacement — this is the only live reference in the entire solution.
**Why this preserves correctness:** `JsonSnapshotStore.Exists()` itself was already just `File.Exists(_path)` (see `JsonSnapshotStore.cs:24`) — the abstraction added zero logic beyond a raw file-existence check for this call site. Nothing is lost by inlining it.

### Pattern 3: Cross-target build/test/publish without a Windows machine (already an established project pattern)
**What:** This solution's own Phase 16/17 `SUMMARY.md` files already use `dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` and `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` to validate `net10.0-windows` projects (including `RigToggle.App`, `RigToggle.Windows`, `RigToggle.Windows.Tests`) from a non-Windows environment, reserving actual hardware execution for the final rig-verification wave only.
**When to use:** Every wave of this phase except the final PERF-02 rig-verification wave.
**Confirmed working this session** (exact commands, exact output):
```bash
$ PATH="$HOME/.dotnet:$PATH" dotnet build RigToggle.sln -p:EnableWindowsTargeting=true
Build succeeded. 0 Warning(s) 0 Error(s)

$ PATH="$HOME/.dotnet:$PATH" dotnet publish src/RigToggle.App/RigToggle.App.csproj \
    -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
RigToggle.App -> /home/.../bin/publish/win-x64/
$ ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
-rwxr-xr-x 1 root root 116946229 ... RigToggle.App.exe   # baseline, confirmed
```
This means the planner should schedule a `dotnet build`/`dotnet test`/`dotnet publish` + file-size-comparison verification task as an *automatable, non-rig* checkpoint in an earlier wave, and reserve the rig only for PERF-02's functional confirmation (cold boot + toggle round trip) — not for re-discovering whether the build/publish succeeds at all.

### Anti-Patterns to Avoid
- **Deleting `AnyRectanglesOverlap` or `MergeAllMonitors` along with `Restore()`:** Both are still live production code (`AnyRectanglesOverlap` is called from `DeactivateMonitors`'s verify-and-throw section; `MergeAllMonitors` is called from `GetAllMonitors()`). Only their sibling `WindowsMonitorControllerTests.cs` tests for the *other* two dead helpers (`CopyOutputTechnology`, `AssignSource`) should be removed — the tests for these two live helpers must stay.
- **Removing `IAudioController.TryResolveDevice`'s friendly-name-fallback-style logic thinking it's part of "Restore":** `TryResolveDevice` is a distinct, live (Phase 15/AUDIO-05) method — do not confuse it with `WindowsAudioController.Restore()`'s internal stale-ID-to-friendly-name fallback (which only existed to serve the now-dead `Restore()` and can be deleted along with it).
- **Treating IN-03 (sentinel display-label persisted as device name) as required cleanup:** Both `15-REVIEW.md` and its own `15-03-SUMMARY.md` explicitly accepted this as a cosmetic, intentional limitation (display-only field, never read by resolution logic). Fixing it is optional/low-priority for CLEANUP-02, not a correctness bug — don't let it block the phase if time-constrained.
- **Touching `17-REVIEW.md`'s WR-02:** Explicitly documented as "not fixed, deliberate" — a locked planning decision from Phase 17. Do not "clean up" or "fix" this as part of CLEANUP-02; it would override a prior deliberate design decision outside this phase's scope.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Verifying "is this method really unused" | Manual code reading / IDE "Find Usages" alone | Exhaustive `grep -rn` across the *entire* `src/` tree for every reference pattern (method name, interface name, model name), cross-checked against the three prior REVIEW.md files' own dead-code findings | IDE-only search can miss dynamic/reflection-based references (irrelevant here) or comment-only false-positive matches (e.g. `MainForm.cs:583`'s "Restore" being the English word, not the method) — grep + manual read of each hit is the same discipline this codebase's own reviewers already used (see `15-REVIEW.md` WR-01's methodology, which is exactly this) |
| Measuring exe size before/after | Trusting a documentation-quoted percentage or a teammate's past run | Actually run `dotnet publish` with and without each flag in this session's exact environment and diff the byte counts | Measured real numbers this session (116,946,229 → 49,444,892 → 49,360,387 bytes) rather than relying on generic "SatelliteResourceLanguages saves ~15MB" blog claims, which are app-dependent (this app's own dependency graph — WindowsDisplayAPI + NAudio + WinForms — differs from any blog's example app) |

**Key insight:** This phase's biggest research risk was *scope creep from good intentions* (CLEANUP-02 becoming an unbounded refactor). Every cleanup candidate in this research is traced to a specific, already-documented, already-reviewed finding — not a fresh sweep. Stick to the named list.

## Common Pitfalls

### Pitfall 1: `EnableCompressionInSingleFile` trades exe size for startup decompression cost
**What goes wrong:** A smaller exe is not free — Microsoft's own docs state compressed assemblies must decompress into memory on every app start, and recommend measuring both size *and* startup cost before adopting it.
**Why it happens:** Compression is applied to the embedded managed assemblies bundle; unpacking happens synchronously during process startup, before any window is shown.
**How to avoid:** This is exactly why PERF-02 requires *cold autostart boot* timing verification on the rig, not merely a file-size diff — the plan should capture a rough "time to tray icon appears" baseline before and after, even informally (a stopwatch comparison is sufficient; this app has no existing startup-time telemetry to compare against precisely).
**Warning signs:** If the rig verification shows a noticeably slower `--tray` cold start (the autostart path, per `StartupArgs.ShouldStartHidden`), that is the tradeoff surfacing, not a regression bug — document the delta rather than treating it as a failure, unless it becomes user-noticeable (multiple seconds).

### Pitfall 2: `SatelliteResourceLanguages` value must be `en`, not `en-US`
**What goes wrong:** Setting `en-US` only excludes the `en-US`-culture satellite assemblies; if any dependency ships a generic `en` satellite (less common, but possible), it would still be copied, understating the achievable savings.
**Why it happens:** Satellite-assembly culture folder names must match exactly — `SatelliteResourceLanguages` does substring/exact matching, not prefix matching (per `dotnet/docs` community guidance cross-checked this session).
**How to avoid:** Use `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>` exactly as specified in the phase's own success criteria — this was already independently confirmed correct via the phase description text itself and cross-checked against `andrewlock.net`'s worked example.
**Warning signs:** N/A — this app's own dependencies (`WindowsDisplayAPI`, `NAudio.Wasapi`) don't appear to ship non-English satellite resources at all based on the measured size delta being fully attributable to the BCL's own culture data + compression; low risk either way.

### Pitfall 3: `InvariantGlobalization` — low risk for *this specific app*, but verify before generalizing the reasoning
**What goes wrong:** In globalization-invariant mode, `DateTime`/number formatting reverts to invariant-culture patterns (`MM/dd/yyyy`, `.` decimal separator) regardless of the user's Windows locale, and culture-aware string comparison/casing (`ToUpper`/`ToLower` beyond ASCII) stops working.
**Why it happens:** Invariant mode disables ICU culture-data loading entirely to save the size/startup cost of loading it.
**How to avoid / why this app is safe:** Grepped every `DateTime`/`CultureInfo`/`ToString("..."` usage in the whole `src/` tree (excluding tests). Results: the app has **no user-facing culture-dependent date/number display at all** — the only `DateTime.Now`/`ToString("HH:mm:ss.fff")` usages are debug-log trace lines (not user-facing UI), and the one place the code already needed culture-invariant number formatting (`MonitorIdentifyOverlay.cs:64`, the Identify-overlay number labels) *already* explicitly uses `CultureInfo.InvariantCulture`. No `.resx` files exist in the solution (confirmed via `find -iname "*.resx"`, zero hits) — MessageBox button captions ("OK"/"Cancel"/"Yes"/"No") are rendered by the native Windows shell based on OS display language, not .NET culture, so they are unaffected by this setting.
**Warning signs:** If a future phase adds a feature that displays a formatted date/number to the user (e.g. LOG-01's deferred toggle-history feature), that feature would need to either explicitly use `CultureInfo.CurrentCulture`-independent formatting (already this codebase's convention per `MonitorIdentifyOverlay`) or this setting would need to be revisited — flag this in code review if LOG-01 is ever picked up.

### Pitfall 4: `NAudio.Wasapi`-only reference is a source-code no-op, but verify no transitive backend was silently relied upon
**What goes wrong:** Someone could assume the NAudio split requires source changes.
**Why it happens:** Confusion between "package split" and "namespace split" — NAudio's package split (2.x → sub-packages) did not rename any namespace; `NAudio.CoreAudioApi` is unchanged and lives in `NAudio.Wasapi`.
**How to avoid:** Confirmed via a live experiment this session — swapped the `PackageReference` in `RigToggle.Windows.csproj` from `NAudio` to `NAudio.Wasapi` with zero source changes, and the full solution (`RigToggle.sln`) built with 0 errors under `-p:EnableWindowsTargeting=true`. Reverted after the experiment (working tree confirmed clean via `git diff --stat`).
**Warning signs:** A build error referencing a type outside `NAudio.CoreAudioApi` (e.g. `WaveOut`, `MidiIn`, `AsioOut`) would indicate a hidden dependency on one of the dropped backends — none was observed; `grep -rln "NAudio" src` shows exactly one file (`WindowsAudioController.cs`) references NAudio at all.

### Pitfall 5: `WindowsMonitorControllerTests.cs` requires *partial*, not wholesale, deletion
**What goes wrong:** Deleting the whole test file because "it's the Restore tests file" would also delete `AnyRectanglesOverlap`/`MergeAllMonitors` test coverage for still-live, still-important production code (the multi-monitor dedup logic that fixed a rig-confirmed duplicate-rows bug per `06-06-SUMMARY.md`).
**Why it happens:** The file's own header comment focuses entirely on "Restore()'s reconstruction logic," making it look Restore-specific at a glance — but 8 of its 14 test methods (`AnyRectanglesOverlap_*` x3, `MergeAllMonitors_*` x5) cover unrelated, live logic.
**How to avoid:** Delete only `CopyOutputTechnology_*` (2 tests, lines ~34-50) and `AssignSource_*` (4 tests, lines ~55-116) plus their two shared private helpers (`CreateFakeTarget`, `FakeSource`) if nothing else in the file uses them (verify `FakeSource` isn't reused by `AnyRectanglesOverlap`/`MergeAllMonitors` tests — it is not, confirmed by reading the full file this session). Update the file's header comment (currently framed entirely around Restore) to describe what remains.
**Warning signs:** A test-count regression in the CI gate (`dotnet test`) larger than exactly 6 removed tests signals over-deletion.

### Pitfall 6: Confirmed IL trimming remains untouched — regression-check this explicitly
**What goes wrong:** A future contributor (or an overzealous "reduce size further" pass within this same phase) adds `PublishTrimmed=true` alongside the four approved levers, since trimming is the single largest remaining size lever and the temptation is real once the exe is already visibly smaller.
**Why it happens:** Trimming is the most commonly-suggested next step in generic "reduce .NET exe size" guidance online, and this phase's own success criteria are already delivering large wins, which can create momentum toward "just one more optimization."
**How to avoid:** `PublishTrimmed=false` is already explicitly set in `win-x64.pubxml` with a doc comment explaining exactly why (COM-interop/P-Invoke reachability false-negatives). CLEANUP-02/PERF-01 verification should include a literal grep check (`grep -n "PublishTrimmed" src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` returning `false`, not absent and not `true`) as part of the phase's regression gate.
**Warning signs:** Any diff touching `PublishTrimmed` at all in this phase's PRs/commits should be treated as a hard stop requiring explicit user sign-off — it directly contradicts a locked, cross-referenced (CLAUDE.md + REQUIREMENTS.md Out-of-Scope table) project decision.

## Code Examples

### Baseline exe-size measurement (reproducible in this repo, no Windows machine needed)
```bash
# Source: this research session, run against RigToggle.App.csproj + win-x64.pubxml as-is
PATH="$HOME/.dotnet:$PATH" dotnet publish src/RigToggle.App/RigToggle.App.csproj \
  -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true
ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
# -> 116946229 bytes (baseline, before this phase's changes)
```

### With all four PERF-01 levers applied (measured this session via command-line -p: overrides — the actual implementation should set these in .csproj/.pubxml, not pass them ad hoc)
```bash
# Source: this research session
PATH="$HOME/.dotnet:$PATH" dotnet publish src/RigToggle.App/RigToggle.App.csproj \
  -c Release -p:PublishProfile=win-x64 -p:EnableWindowsTargeting=true \
  -p:SatelliteResourceLanguages=en -p:InvariantGlobalization=true \
  -p:EnableCompressionInSingleFile=true
# (with RigToggle.Windows.csproj's NAudio PackageReference also swapped to NAudio.Wasapi)
ls -la src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
# -> 49360387 bytes  (57.8% smaller than the 116946229-byte baseline)
```

### Actual RigToggle.App.csproj edit shape (for PERF-01 implementation)
```xml
<!-- RigToggle.App.csproj — add inside the existing <PropertyGroup> -->
<PropertyGroup>
  <!-- ...existing properties unchanged... -->
  <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```
```xml
<!-- Properties/PublishProfiles/win-x64.pubxml — add alongside existing PublishSingleFile/SelfContained -->
<PropertyGroup>
  <!-- ...existing properties unchanged... -->
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

### Program.cs bootstrap-read replacement (Pattern 2 above, restated as a diff-shaped example)
```csharp
// Before (uses the about-to-be-deleted JsonSnapshotStore/ISnapshotStore):
var snapshotStore = new JsonSnapshotStore(Path.Combine(basePath, "state.json"));
...
if (!modeStore.Exists())
{
    modeStore.Save(snapshotStore.Exists() ? ToggleMode.Rig : ToggleMode.Normal);
}

// After (no store abstraction — same semantics, since Exists() was only File.Exists()):
string legacyStateJsonPath = Path.Combine(basePath, "state.json");
...
if (!modeStore.Exists())
{
    modeStore.Save(File.Exists(legacyStateJsonPath) ? ToggleMode.Rig : ToggleMode.Normal);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Normal mode restores from a pre-toggle `StateSnapshot` (monitor + audio) | Normal mode applies an explicitly configured monitor/audio target, same shape as Rig mode | Phase 16 (2026-08-08) | This is *why* the snapshot subsystem is dead — Phase 18 is cleanup of Phase 16's architectural change, not a new decision |
| `NAudio` meta-package (pulls in WinMM/MIDI/ASIO/WinForms/DMO backends this app never uses) | `NAudio.Wasapi` (CoreAudioApi enumeration/device-default support only) | This phase (proposed) | ~84 KB direct savings plus a smaller dependency/attack surface (fewer unused native interop backends bundled) |
| Uncompressed self-contained single-file bundle | Compressed (`EnableCompressionInSingleFile`) | This phase (proposed) | The dominant lever — measured ~67.5 MB of the ~67.6 MB total savings this session came from combining compression with the other two flags together (not isolated per-flag in this session's measurements, but compression is documented as the single largest lever for self-contained bundles specifically because it compresses the bundled BCL/runtime assemblies, which dominate a self-contained app's size) |

**Deprecated/outdated:** N/A — no deprecated APIs are involved in this phase; this is a subtractive/config-only phase.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `SatelliteResourceLanguages` should be set on `RigToggle.App.csproj` (the publishing project) rather than on `RigToggle.Windows.csproj`/`RigToggle.Core.csproj` (the projects that reference `WindowsDisplayAPI`/`NAudio.Wasapi`, which are the actual satellite-resource-bearing dependencies) | Standard Stack table, Recommended Project Structure | LOW — this session's live measurement applied the property via `-p:` override at the App project's publish command and it took effect (confirmed via the measured size drop), so setting it on the App project is empirically sufficient for this solution's dependency graph. If the planner instead sets it only on `RigToggle.Windows.csproj`, it may not propagate to the final publish output the same way — recommend setting on `RigToggle.App.csproj` as measured, or verify empirically if choosing otherwise. |
| A2 | The exact ~67.5 MB of the compression+invariant-globalization+satellite-languages combined savings is dominated by compression specifically (vs. the other two flags) | Common Pitfalls Pitfall 1, State of the Art table | LOW — this session measured the three flags only in combination, not each one isolated. The planner/implementer could isolate each flag individually (three more `dotnet publish` runs, ~1 min each in this environment) if an exact per-flag breakdown is needed for the verification task's reporting; not done here to conserve research-session time, since the combined number is what PERF-01's success criterion actually requires ("measurably smaller," not "smaller by exactly X due to flag Y"). |
| A3 | No `.resx`-based satellite resources exist in this specific solution that `SatelliteResourceLanguages=en` could break (e.g. no non-English UI string resources anyone might be relying on) | Pitfall 3 | LOW — confirmed via `find -iname "*.resx"` returning zero hits across the entire `src/` tree this session; this is a direct filesystem check, not an inference. |

**If this table is empty:** N/A — three low-risk assumptions logged above, none block planning.

## Open Questions

1. **Should the NAudio split also add an explicit `NAudio.Core` reference?**
   - What we know: `NAudio.Wasapi` alone was sufficient to build the full solution cleanly this session (confirmed live).
   - What's unclear: Whether relying on `NAudio.Wasapi`'s transitive dependency on `NAudio.Core` (rather than an explicit direct reference) could be considered less explicit/maintainable by a future reader.
   - Recommendation: Use `NAudio.Wasapi` alone (matches "smallest set of packages that builds cleanly," verified this session) unless the planner's team convention prefers explicit transitive-dependency declarations — this is a style choice, not a correctness question.

2. **Exact wording/threshold for PERF-02's "cold autostart boot" verification.**
   - What we know: The app has a `--tray` hidden-start path (`StartupArgs.ShouldStartHidden`) used for autostart, and `EnableCompressionInSingleFile` has a documented (but unmeasured-in-this-app) startup-cost tradeoff.
   - What's unclear: This codebase has no existing startup-time telemetry/benchmark to compare against — "cold boot" timing will necessarily be an informal stopwatch/subjective comparison on the rig, not a precise regression threshold.
   - Recommendation: Treat this as a qualitative rig checkpoint ("does autostart still feel instant / does the tray icon still appear promptly") rather than inventing a precise millisecond SLA this codebase has never measured before — consistent with this project's existing UAT-style rig verification pattern (Phases 8/9/11/15/16/17), which uses pass/fail human judgment, not numeric benchmarks.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/publish/test everything in this phase | Yes (this research environment) | 10.0.302 | — |
| `dotnet build -p:EnableWindowsTargeting=true` cross-targeting | Building/testing `net10.0-windows` projects (`RigToggle.App`, `RigToggle.Windows`, `RigToggle.Windows.Tests`) without a Windows OS | Yes — confirmed this session, `Build succeeded, 0 Warning(s), 0 Error(s)` for the full `RigToggle.sln` | — | — |
| `dotnet publish -r win-x64 --self-contained` cross-publish | Measuring exe size for PERF-01 without a Windows OS | Yes — confirmed this session, produced a valid publish output and measurable byte count | — | — |
| Real Windows rig hardware (CCD display API, `IPolicyConfig` COM interop, autostart registration) | PERF-02's functional verification (cold boot, toggle round trip) | Not available in this research/planning environment (Linux) | — | None — this is expected and matches every prior phase (15/16/17) in this project, all of which reserved a final human-checkpoint wave for rig-only verification. Not a gap; a structural constraint of this project type. |
| `slopcheck` | Package Legitimacy Audit | Yes, but does not support NuGet | 0.6.1 | Manual NuGet registry API verification (performed this session) |

**Missing dependencies with no fallback:** None that block *planning* — the rig dependency is expected and already the established pattern for this project's final verification wave.
**Missing dependencies with fallback:** `slopcheck`'s lack of NuGet support was worked around via direct NuGet registry API queries (`curl https://api.nuget.org/v3-flatcontainer/...`) this session.

## Security Domain

> `security_enforcement` is not explicitly set to `false` in `.planning/config.json`, so this section is included per protocol. This phase has an unusually small security surface — no new user input handling, no network calls, no authentication/authorization/session logic, and no new third-party attack surface (the NAudio change is a narrowing, not an addition).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | N/A — single-user desktop utility, no auth surface, unaffected by this phase |
| V3 Session Management | No | N/A |
| V4 Access Control | No | N/A |
| V5 Input Validation | No | This phase adds no new user-input-handling code path (deletions and MSBuild config only) |
| V6 Cryptography | No | N/A |
| V14 Configuration | Marginal | The four MSBuild properties are build/publish configuration, not runtime security configuration — no secrets, no exposed endpoints. The one relevant control: verify `PublishTrimmed` stays `false` (Pitfall 6) and no elevation manifest is (re-)introduced (CLAUDE.md constraint, unrelated to this phase's actual changes but worth a regression check given `.csproj`/`.pubxml` files are being touched). |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Supply-chain risk from an unnecessary/oversized dependency surface (unused NAudio backends bundled and shipped) | Tampering (larger attack surface if any unused backend has a future CVE) | This phase's NAudio-split *reduces* this risk by dropping `NAudio.WinMM`/`NAudio.Midi`/`NAudio.Asio`/`NAudio.WinForms`/`NAudio.Dmo` from the shipped binary — a net security improvement, not just a size win |
| Reintroducing a snapshot-restore code path that a future contributor could accidentally re-wire into a live call site after this phase deletes its "obviously dead, safe to touch" guard rails | Tampering / Elevation of Privilege (low severity — this is a local desktop utility, not a multi-tenant system) | Full deletion (not just removing call sites) prevents any future accidental re-wiring; this is exactly what CLEANUP-01 accomplishes |

## Sources

### Primary (HIGH confidence)
- `learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview` — `EnableCompressionInSingleFile` exact behavior, startup-cost tradeoff, official Microsoft docs, fetched directly this session
- `learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization` — `InvariantGlobalization` exact MSBuild property name/syntax, official Microsoft docs, fetched directly this session
- `api.nuget.org/v3-flatcontainer/{naudio,naudio.wasapi,naudio.core,windowsdisplayapi}/index.json` — direct NuGet registry API queries confirming exact published versions, run live this session
- This research session's own live `dotnet build`/`dotnet test`/`dotnet publish` runs against the actual `RigToggle.sln` — exhaustive grep of the actual codebase for every dead-code claim, and live exe-size measurements before/after the proposed changes (not estimated, not sourced from a blog)
- `.planning/phases/15-optional-app-audio-targets/15-REVIEW.md`, `16-.../16-REVIEW.md`, `17-.../17-REVIEW.md` — this project's own prior code-review artifacts, directly naming the CLEANUP-02 candidates

### Secondary (MEDIUM confidence)
- `andrewlock.net/disabling-localized-satellite-assemblies-during-dotnet-publish/` — `SatelliteResourceLanguages` syntax/value guidance (`en` vs `en-US`), independent blog but cross-checked against the property's documented behavior and this session's own measurement
- `github.com/naudio/NAudio` — official repo confirming the package-split shape (`NAudio.Core`, `NAudio.Wasapi`, etc.) and that `MMDeviceEnumerator`/`CoreAudioApi` live in `NAudio.Wasapi`

### Tertiary (LOW confidence)
- General WebSearch results on `InvariantGlobalization` WinForms impact (no single authoritative source enumerating every WinForms-specific gotcha was found; mitigated by this session's own direct codebase grep for actual culture-dependent usage, which is HIGH confidence since it's a direct check of this app's real code, not general guidance)

## Metadata

**Confidence breakdown:**
- Standard stack (NAudio split, MSBuild properties): HIGH — every property/package verified live this session via registry queries and/or actual builds, not training-data recall
- Dead-code removal scope (CLEANUP-01): HIGH — exhaustive grep of the actual repository, corroborated by three independent prior REVIEW.md findings
- Code-quality pass scope (CLEANUP-02): HIGH for the five named candidates (directly sourced from REVIEW.md findings with exact file/line references still valid), MEDIUM for "is this list complete" (a fresh unbounded sweep was deliberately not performed, per this research's own scoping discipline)
- Exe-size reduction numbers (PERF-01): HIGH — measured live, reproducible, exact byte counts given
- Rig verification approach (PERF-02): MEDIUM — the *structure* (final human-checkpoint wave, matching Phases 15-17) is HIGH confidence; the *specific* cold-boot timing methodology is inherently qualitative for this codebase (Open Question 2)

**Research date:** 2026-08-08
**Valid until:** 30 days (stable domain — MSBuild properties and NuGet package shapes don't change quickly; the dead-code findings are exact-line-number-specific and should be re-verified if significant time passes before this phase executes, since Phase numbering suggests immediate execution)
